using UnityEngine;
using UnityEngine.Animations.Rigging;
using Debug = UnityEngine.Debug;

/// <summary>
/// Positions the avatar root and drives all IK targets.
///
/// FIX: This script is now the SINGLE owner of all IK target driving.
/// UpdateActiveIKTargets() is always called after PinHip() and ApplyDynamicScaling()
/// so that targets are written in the correct world space after avatarRoot has moved.
/// 
/// When bodyEstimator is assigned it reads SmoothedKnee/SmoothedAnkle from it,
/// then drives active + mirror IK targets itself.
/// SittingBodyEstimator no longer calls DriveIKTargets().
///
/// EXECUTION ORDER: Must run AFTER SittingBodyEstimator.
/// Set in Edit > Project Settings > Script Execution Order:
///   SittingBodyEstimator = -100
///   LegRootFitter        = 0 (default)
/// </summary>
public class LegRootFitter : MonoBehaviour
{
    [Header("Hardware & Decision")]
    public BodyTracker bodyTracker;
    public LegSideSelector sideSelector;
    public Transform hmdTransform;

    [Header("Body Estimator")]
    public SittingBodyEstimator bodyEstimator;

    [Header("Avatar Structure")]
    public Transform avatarRoot;
    public Transform pelvisBone;
    public Transform headBone;

    [Header("IK Constraints")]
    public TwoBoneIKConstraint leftIK;
    public TwoBoneIKConstraint rightIK;

    [Header("IK Target Transforms")]
    public Transform leftAnkleTarget;
    public Transform leftKneeHint;
    public Transform rightAnkleTarget;
    public Transform rightKneeHint;

    [Header("Settings")]
    public float hipHeightBelowHMD = 0.55f;
    public float verticalOffset = -0.05f;
    [Tooltip("Minimum gap between legs")]
    public float baseStanceWidth = 0.15f;
    [Tooltip("Scale multiplier applied on top of computed scale")]
    [Range(1f, 2f)]
    public float sizeMultiplier = 1.15f;
    public bool hideUpperBody = true;
    public int printEveryNFrames = 30;

    [Header("Smoothing (used when bodyEstimator is null)")]
    public float smoothSpeedFast = 15f;
    public float smoothSpeedSlow = 4f;
    public float slowVelocityThreshold = 0.05f;

    private bool _hasResolvedBones = false;
    private string _sideLabel = "None";
    private TwoBoneIKConstraint _activeIK;
    private TwoBoneIKConstraint _mirrorIK;

    private Vector3 _lockedHipWorld;
    private Quaternion _lockedBodyRot = Quaternion.identity;
    private bool _hipLocked = false;

    private float _unscaledAvatarThigh = 0f;

    // Fallback smoothing when no estimator is assigned
    private Vector3 _smoothedKnee;
    private Vector3 _smoothedAnkle;
    private Vector3 _prevAnkle;
    private bool _smoothingInitialized = false;

    void Start()
    {
        avatarRoot.localScale = Vector3.zero;
        if (hideUpperBody) HideUpperBodyParts();
    }

    void LateUpdate()
    {
        if (bodyTracker == null || hmdTransform == null || sideSelector == null) return;

        if (!sideSelector.sideLocked)
        {
            _hasResolvedBones = false;
            _smoothingInitialized = false;
            _hipLocked = false;
            avatarRoot.localScale = Vector3.zero;
            DisableBothIKs();
            return;
        }

        if (!_hasResolvedBones)
        {
            avatarRoot.localScale = Vector3.one;
            ResolveIKWeightsOnce();
        }

        if (!bodyTracker.KneeValid || !bodyTracker.AnkleValid) return;

        // ── Gather smoothed positions ─────────────────────────────────────────
        Vector3 kneePos, anklePos;

        if (bodyEstimator != null)
        {
            // Estimator has already run its LateUpdate (earlier execution order)
            // and populated SmoothedKnee / SmoothedAnkle. Just read them.
            kneePos = bodyEstimator.SmoothedKnee;
            anklePos = bodyEstimator.SmoothedAnkle;
        }
        else
        {
            // Fallback: smooth raw tracker positions here
            if (!_smoothingInitialized)
            {
                _smoothedKnee = bodyTracker.KneePosition;
                _smoothedAnkle = bodyTracker.AnklePosition;
                _prevAnkle = _smoothedAnkle;
                _smoothingInitialized = true;
            }

            float vel = Vector3.Distance(bodyTracker.AnklePosition, _prevAnkle) / Time.deltaTime;
            float speed = vel < slowVelocityThreshold ? smoothSpeedSlow : smoothSpeedFast;

            _smoothedKnee = Vector3.Lerp(_smoothedKnee, bodyTracker.KneePosition, speed * Time.deltaTime);
            _smoothedAnkle = Vector3.Lerp(_smoothedAnkle, bodyTracker.AnklePosition, speed * Time.deltaTime);
            _prevAnkle = _smoothedAnkle;

            kneePos = _smoothedKnee;
            anklePos = _smoothedAnkle;
        }

        // ── 1. Pin hip (moves avatarRoot) ─────────────────────────────────────
        PinHip();

        // ── 2. Scale avatar to match real thigh length ────────────────────────
        ApplyDynamicScaling(kneePos);

        // ── 3. Drive active leg IK targets ────────────────────────────────────
        // MUST happen after PinHip + ApplyDynamicScaling so avatarRoot is in
        // its final position before we write world-space target positions.
        UpdateActiveIKTargets(kneePos, anklePos);

        // ── 4. Mirror leg IK targets ──────────────────────────────────────────
        UpdateMirrorIKTargets(kneePos, anklePos);

        // ── 5. Enforce minimum separation between legs ────────────────────────
        EnforceMinLegSeparation();

        // ── 6. Pin head to HMD ────────────────────────────────────────────────
        if (headBone)
        {
            headBone.position = hmdTransform.position;
            headBone.rotation = hmdTransform.rotation;
        }

        if (Time.frameCount % printEveryNFrames == 0)
            PrintBodyDebug(kneePos, anklePos);
    }

    void ResolveIKWeightsOnce()
    {
        bool isLeft = sideSelector.currentSide == LegSideSelector.LegSide.Left;
        _sideLabel = isLeft ? "Left" : "Right";

        _activeIK = isLeft ? leftIK : rightIK;
        _mirrorIK = isLeft ? rightIK : leftIK;

        if (leftIK) leftIK.weight = 1f;
        if (rightIK) rightIK.weight = 1f;

        if (_activeIK != null)
            _unscaledAvatarThigh = Vector3.Distance(
                _activeIK.data.root.position,
                _activeIK.data.mid.position);

        // Lock hip position at selection moment
        if (bodyEstimator != null && bodyEstimator.HasAIHip)
            _lockedHipWorld = bodyEstimator.AIHipPosition;
        else
            _lockedHipWorld = hmdTransform.position
                            + Vector3.down * hipHeightBelowHMD
                            + Vector3.up * verticalOffset;

        // Lock avatar forward direction from HMD — never update again
        Vector3 lockedForward = hmdTransform.forward;
        lockedForward.y = 0f;
        if (lockedForward.sqrMagnitude < 0.001f) lockedForward = Vector3.forward;
        _lockedBodyRot = Quaternion.LookRotation(lockedForward.normalized, Vector3.up);
        _hipLocked = true;

        _hasResolvedBones = true;
        Debug.Log($"[body] IK: {_sideLabel} | Thigh: {_unscaledAvatarThigh:F3}m | Hip: {_lockedHipWorld:F2}");
    }

    void DisableBothIKs()
    {
        if (leftIK) leftIK.weight = 0f;
        if (rightIK) rightIK.weight = 0f;
        _sideLabel = "None";
    }

    void PinHip()
    {
        // 1. Calculate the 'Perfect Center' (Directly under HMD)
        Vector3 currentHMD = hmdTransform.position;
        Vector3 centerUnderHMD = new Vector3(currentHMD.x, currentHMD.y - hipHeightBelowHMD + verticalOffset, currentHMD.z);

        Vector3 finalHipPos = centerUnderHMD;

        // 2. Apply AI 'Lean' as an offset
        if (bodyEstimator != null && bodyEstimator.HasAIHip)
        {
            // Calculate how far the AI hip is from the AI head/skeleton root
            // If your AI skeleton is offset from your HMD, we capture that 'lean' vector
            Vector3 aiHip = bodyEstimator.AIHipPosition;

            // We only care about the horizontal leaning (X and Z)
            // We calculate the delta between the AI hip and the HMD
            float leanX = aiHip.x - currentHMD.x;
            float leanZ = aiHip.z - currentHMD.z;

            // Apply a multiplier to the lean. 
            // 1.0f = full AI lean, 0.5f = dampened/conservative lean
            float leanSensitivity = 0.6f;

            finalHipPos.x += (leanX * leanSensitivity);
            finalHipPos.z += (leanZ * leanSensitivity);

            // Vertical: Always trust the AI for sitting height if available
            finalHipPos.y = aiHip.y;
        }

        // 3. Position the Avatar
        Quaternion rot = _hipLocked ? _lockedBodyRot : Quaternion.identity;

        // This part ensures the 'Pelvis' bone specifically ends up at finalHipPos
        Vector3 pelvisOffset = avatarRoot.InverseTransformPoint(pelvisBone.position);
        avatarRoot.position = finalHipPos - (rot * pelvisOffset);
        avatarRoot.rotation = rot;
    }

    void ApplyDynamicScaling(Vector3 kneePos)
    {
        if (_unscaledAvatarThigh < 0.05f) return;

        Vector3 hipPos = _hipLocked
            ? _lockedHipWorld
            : hmdTransform.position + Vector3.down * hipHeightBelowHMD;

        float target = Vector3.Distance(hipPos, kneePos);
        if (target < 0.05f) return;

        avatarRoot.localScale = Vector3.one * (target / _unscaledAvatarThigh) * sizeMultiplier;
    }

    /// <summary>
    /// Drives the active (tracked) leg IK targets.
    /// Always called after PinHip + ApplyDynamicScaling so avatarRoot is settled.
    /// Also applies AI foot rotation from bodyEstimator if available.
    /// </summary>
    void UpdateActiveIKTargets(Vector3 kneePos, Vector3 anklePos)
    {
        Transform ankleT = (_activeIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform kneeT = (_activeIK == leftIK) ? leftKneeHint : rightKneeHint;

        if (ankleT != null)
        {
            ankleT.position = anklePos;

            // Apply AI foot rotation if estimator has it
            if (bodyEstimator != null && bodyEstimator.HasAIRotation)
                ankleT.rotation = bodyEstimator.AIFootRotation;
        }

        if (kneeT != null)
            kneeT.position = kneePos;
    }

    void UpdateMirrorIKTargets(Vector3 kneePos, Vector3 anklePos)
    {
        if (_mirrorIK == null) return;

        Transform ankleT = (_mirrorIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform kneeT = (_mirrorIK == leftIK) ? leftKneeHint : rightKneeHint;

        Vector3 localAnkle = avatarRoot.InverseTransformPoint(anklePos);
        Vector3 localKnee = avatarRoot.InverseTransformPoint(kneePos);

        localAnkle.x = -localAnkle.x;
        localKnee.x = -localKnee.x;

        bool mirrorIsLeft = (_mirrorIK == leftIK);
        float sideShift = baseStanceWidth * (mirrorIsLeft ? -1f : 1f);
        localAnkle.x += sideShift;
        localKnee.x += sideShift;

        // Hard clamp — mirror leg never crosses centre
        if (mirrorIsLeft)
        {
            localAnkle.x = Mathf.Min(localAnkle.x, -0.01f);
            localKnee.x = Mathf.Min(localKnee.x, -0.01f);
        }
        else
        {
            localAnkle.x = Mathf.Max(localAnkle.x, 0.01f);
            localKnee.x = Mathf.Max(localKnee.x, 0.01f);
        }

        if (ankleT) ankleT.position = avatarRoot.TransformPoint(localAnkle);
        if (kneeT) kneeT.position = avatarRoot.TransformPoint(localKnee);
    }

    void EnforceMinLegSeparation()
    {
        Transform activeAnkle = (_activeIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform mirrorAnkle = (_mirrorIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform activeKnee = (_activeIK == leftIK) ? leftKneeHint : rightKneeHint;
        Transform mirrorKnee = (_mirrorIK == leftIK) ? leftKneeHint : rightKneeHint;

        if (activeAnkle == null || mirrorAnkle == null) return;

        Vector3 activeLocal = avatarRoot.InverseTransformPoint(activeAnkle.position);
        Vector3 mirrorLocal = avatarRoot.InverseTransformPoint(mirrorAnkle.position);

        if (Mathf.Abs(mirrorLocal.x - activeLocal.x) < baseStanceWidth)
        {
            float half = baseStanceWidth * 0.5f;
            bool activeIsLeft = (_activeIK == leftIK);

            activeLocal.x = activeIsLeft ? -half : half;
            mirrorLocal.x = activeIsLeft ? half : -half;

            activeAnkle.position = avatarRoot.TransformPoint(activeLocal);
            mirrorAnkle.position = avatarRoot.TransformPoint(mirrorLocal);

            if (activeKnee != null)
            {
                Vector3 k = avatarRoot.InverseTransformPoint(activeKnee.position);
                k.x = activeLocal.x;
                activeKnee.position = avatarRoot.TransformPoint(k);
            }
            if (mirrorKnee != null)
            {
                Vector3 k = avatarRoot.InverseTransformPoint(mirrorKnee.position);
                k.x = mirrorLocal.x;
                mirrorKnee.position = avatarRoot.TransformPoint(k);
            }
        }
    }

    void HideUpperBodyParts()
    {
        foreach (Transform t in avatarRoot.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("head") || n.Contains("neck") ||
                n.Contains("hand") || n.Contains("finger"))
                t.localScale = Vector3.zero;
        }
    }

    void PrintBodyDebug(Vector3 kneePos, Vector3 anklePos)
    {
        string metrics = bodyEstimator != null ? bodyEstimator.GetMetricsSummary() : "";
        Debug.Log($"[body] Side:{_sideLabel} Scale:{avatarRoot.localScale.x:F2} {metrics}");
    }

    public void ResetFitter()
    {
        _hasResolvedBones = false;
        _smoothingInitialized = false;
        _hipLocked = false;
        _lockedBodyRot = Quaternion.identity;
        _activeIK = null;
        _mirrorIK = null;
        _unscaledAvatarThigh = 0f;
        avatarRoot.localScale = Vector3.zero;

        DisableBothIKs();

        if (bodyEstimator != null) bodyEstimator.Reset();
        if (sideSelector != null) sideSelector.ResetSide();

        Debug.Log("[body] FULL RESET");
    }
}