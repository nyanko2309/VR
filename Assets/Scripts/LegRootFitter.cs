using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Debug = UnityEngine.Debug;

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
    public Transform leftToeTarget;
    public Transform rightAnkleTarget;
    public Transform rightKneeHint;
    public Transform rightToeTarget;

    [Header("Invisible Foot IK Targets")]
    public Transform leftFootTarget;
    public Transform rightFootTarget;
    [Tooltip("Fix Mixamo foot axis if needed")]
    public Vector3 footTargetRotationOffset = Vector3.zero;

    [Header("Offsets")]
    public float ankleForwardOffset = 0.05f;
    [Tooltip("Toes pushed forward by this fraction of the ankle forward offset (0.5 = half)")]
    [Range(0f, 2f)]
    public float toeForwardFraction = 0.5f;
    public float hipHeightBelowHMD = 0.55f;
    public float verticalOffset = -0.05f;
    [Tooltip("Minimum gap between legs")]
    public float baseStanceWidth = 0.15f;
    [Tooltip("Scale multiplier applied on top of computed scale")]
    [Range(1f, 2f)]
    public float sizeMultiplier = 1.15f;
    [Tooltip("Shin bone scaled to this fraction of real knee→ankle")]
    [Range(0.5f, 1.2f)]
    public float shinLengthFraction = 0.85f;

    [Header("Flip Guards")]
    [Tooltip("Min ankle→toe distance — skip toe update if closer (sticker lost/flipped)")]
    public float minAnkleToeToeDistance = 0.05f;
    [Tooltip("Max ankle→toe distance — clamp if exceeded")]
    public float maxAnkleToeToeDistance = 0.5f;

    [Header("Toe Smoothing")]
    public float toeSmoothing = 10f;

    public bool hideUpperBody = true;
    public int printEveryNFrames = 30;

    [Header("Smoothing")]
    public float smoothSpeedFast = 15f;
    public float smoothSpeedSlow = 4f;
    public float slowVelocityThreshold = 0.05f;

    // ── Public output for other systems ──────────────────────────────────────
    /// <summary>
    /// The computed hip world position this frame. Updated every LateUpdate.
    /// Used by PhysioBallGenerator to anchor cube spawning and sliding.
    /// </summary>
    public Vector3 HipWorldPosition { get; private set; }

    // Private state
    private bool _hasResolvedBones = false;
    private string _sideLabel = "None";
    private TwoBoneIKConstraint _activeIK;
    private TwoBoneIKConstraint _mirrorIK;
    private Vector3 _lockedHipWorld;
    private Quaternion _lockedBodyRot = Quaternion.identity;
    private bool _hipLocked = false;
    private float _unscaledAvatarThigh = 0f;
    private float _measuredShinWorld = 0f;
    private bool _shinMeasured = false;
    private Transform _activeShinBone = null;
    private Vector3 _smoothedActiveToe;
    private Vector3 _lastGoodActiveToe;
    private bool _toeInitialized = false;
    private bool _hasLastGoodToe = false;
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
            _toeInitialized = false;
            _hasLastGoodToe = false;
            _hipLocked = false;
            avatarRoot.localScale = Vector3.zero;
            // Reset shin scale to avoid flash on re-lock
            if (_activeShinBone != null) _activeShinBone.localScale = Vector3.one;
            DisableBothIKs();
            return;
        }

        if (!_hasResolvedBones)
        {
            avatarRoot.localScale = Vector3.one;
            ResolveIKWeightsOnce();
        }

        if (!bodyTracker.KneeValid || !bodyTracker.AnkleValid) return;

        Vector3 kneePos, anklePos;

        if (bodyEstimator != null)
        {
            kneePos = bodyEstimator.SmoothedKnee;
            anklePos = bodyEstimator.SmoothedAnkle;
        }
        else
        {
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

        PinHip();
        ApplyDynamicScaling(kneePos);
        ApplyShinScaling(kneePos, anklePos);
        UpdateActiveIKTargets(kneePos, anklePos);
        UpdateMirrorIKTargets(kneePos, anklePos);
        UpdateFootTargets();
        EnforceMinLegSeparation();

        if (headBone)
        {
            headBone.position = hmdTransform.position;
            headBone.rotation = hmdTransform.rotation;
        }

        if (Time.frameCount % printEveryNFrames == 0)
            PrintBodyDebug(kneePos, anklePos);
    }

    // ── Foot plane target ─────────────────────────────────────────────────

    void UpdateFootTargets()
    {
        Transform activeAnkleT = (_activeIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform activeToeT = (_activeIK == leftIK) ? leftToeTarget : rightToeTarget;
        Transform activeFootT = (_activeIK == leftIK) ? leftFootTarget : rightFootTarget;

        Transform mirrorAnkleT = (_mirrorIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform mirrorToeT = (_mirrorIK == leftIK) ? leftToeTarget : rightToeTarget;
        Transform mirrorFootT = (_mirrorIK == leftIK) ? leftFootTarget : rightFootTarget;

        UpdateSingleFootTarget(activeFootT, activeAnkleT, activeToeT);
        UpdateSingleFootTarget(mirrorFootT, mirrorAnkleT, mirrorToeT);
    }

    void UpdateSingleFootTarget(Transform footTarget, Transform ankleTarget, Transform toeTarget)
    {
        if (footTarget == null || ankleTarget == null || toeTarget == null) return;

        // 1. Keep the position locked to the ankle
        footTarget.position = ankleTarget.position;

        // 2. Toe offset in avatar local space
        Vector3 toeOffset = toeTarget.position - ankleTarget.position;
        Vector3 localToePos = avatarRoot.InverseTransformDirection(toeOffset);

        // 3. Position-based clamp — only apply if toe is in front of ankle
        if (localToePos.z > 0.001f)
        {
            float minX = localToePos.z * -0.6f;  // ≈ -30°
            float maxX = localToePos.z * 1.8f;   // ≈ +60°
            localToePos.x = Mathf.Clamp(localToePos.x, minX, maxX);
        }

        // 4. Rebuild world direction from clamped local position
        Vector3 cleanLocalDir = -localToePos;
        Vector3 worldDir = avatarRoot.TransformDirection(cleanLocalDir);

        if (worldDir.sqrMagnitude < 0.0001f) return;

        // 5. Compute foot rotation from ankle→toe direction + up
        Quaternion targetRot = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
        targetRot *= Quaternion.Euler(footTargetRotationOffset);
        footTarget.rotation = targetRot;
    }

    // ── IK target drivers ─────────────────────────────────────────────────

    void UpdateActiveIKTargets(Vector3 kneePos, Vector3 anklePos)
    {
        if (_activeIK == null) return;

        Transform ankleT = (_activeIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform kneeT = (_activeIK == leftIK) ? leftKneeHint : rightKneeHint;
        Transform toeT = (_activeIK == leftIK) ? leftToeTarget : rightToeTarget;

        if (ankleT) ankleT.position = anklePos + _pairFwdForToe * ankleForwardOffset;
        if (kneeT) kneeT.position = kneePos;

        // Toe target
        if (toeT != null && bodyTracker.ToesValid)
        {
            Vector3 rawToe = bodyTracker.ToesPosition;
            float dist = Vector3.Distance(rawToe, anklePos);

            if (dist >= minAnkleToeToeDistance && dist <= maxAnkleToeToeDistance)
            {
                _lastGoodActiveToe = rawToe;
                _hasLastGoodToe = true;
            }

            Vector3 goalToe = _hasLastGoodToe ? _lastGoodActiveToe : anklePos + _pairFwdForToe * 0.15f;

            if (!_toeInitialized)
            {
                _smoothedActiveToe = goalToe;
                _toeInitialized = true;
            }
            else
            {
                _smoothedActiveToe = Vector3.Lerp(_smoothedActiveToe, goalToe, toeSmoothing * Time.deltaTime);
            }

            toeT.position = _smoothedActiveToe;
        }
        else if (toeT != null)
        {
            toeT.position = anklePos + _pairFwdForToe * 0.15f;
        }
    }

    // Cached forward for toe offset (avatar facing direction, flat)
    private Vector3 _pairFwdForToe => avatarRoot != null
        ? Vector3.ProjectOnPlane(avatarRoot.forward, Vector3.up).normalized
        : Vector3.forward;

    void UpdateMirrorIKTargets(Vector3 kneePos, Vector3 anklePos)
    {
        if (_mirrorIK == null) return;

        Transform ankleT = (_mirrorIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform kneeT = (_mirrorIK == leftIK) ? leftKneeHint : rightKneeHint;
        Transform toeT = (_mirrorIK == leftIK) ? leftToeTarget : rightToeTarget;
        Transform activeToeT = (_activeIK == leftIK) ? leftToeTarget : rightToeTarget;

        // Mirror in avatar local space
        Vector3 localAnkle = avatarRoot.InverseTransformPoint(anklePos);
        Vector3 localKnee = avatarRoot.InverseTransformPoint(kneePos);
        localAnkle.x = -localAnkle.x;
        localKnee.x = -localKnee.x;

        bool mirrorIsLeft = (_mirrorIK == leftIK);
        float sideShift = baseStanceWidth * (mirrorIsLeft ? -1f : 1f);
        localAnkle.x += sideShift;
        localKnee.x += sideShift;

        if (ankleT) ankleT.position = avatarRoot.TransformPoint(localAnkle);
        if (kneeT) kneeT.position = avatarRoot.TransformPoint(localKnee);

        // Mirror toe — use smoothed active toe so guard/clamp carry over
        if (toeT != null && activeToeT != null)
        {
            Vector3 localToe = avatarRoot.InverseTransformPoint(activeToeT.position);
            localToe.x = -localToe.x + sideShift;
            toeT.position = avatarRoot.TransformPoint(localToe);
        }
    }

    // ── Leg separation ────────────────────────────────────────────────────

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

    // ── Scaling ───────────────────────────────────────────────────────────

    void ApplyDynamicScaling(Vector3 kneePos)
    {
        if (_unscaledAvatarThigh < 0.05f) return;

        Vector3 hipPos = _hipLocked ? _lockedHipWorld : hmdTransform.position + Vector3.down * hipHeightBelowHMD;
        float target = Vector3.Distance(hipPos, kneePos);
        if (target < 0.05f) return;

        avatarRoot.localScale = Vector3.one * (target / _unscaledAvatarThigh) * sizeMultiplier;

        if (!_shinMeasured && _activeShinBone != null && _activeIK?.data.tip != null)
        {
            _measuredShinWorld = Vector3.Distance(_activeShinBone.position, _activeIK.data.tip.position);
            if (_measuredShinWorld > 0.01f)
            {
                _shinMeasured = true;
                Debug.Log($"[body] Shin:{_measuredShinWorld:F3}m");
            }
        }
    }

    void ApplyShinScaling(Vector3 kneePos, Vector3 anklePos)
    {
        if (!_shinMeasured || _activeShinBone == null) return;
        float realShin = Vector3.Distance(kneePos, anklePos) * shinLengthFraction;
        if (realShin < 0.02f) return;
        _activeShinBone.localScale = Vector3.one * (realShin / _measuredShinWorld);
    }

    // ── Hip ───────────────────────────────────────────────────────────────

    void PinHip()
    {
        Vector3 currentHMD = hmdTransform.position;
        Vector3 finalHipPos = new Vector3(currentHMD.x, currentHMD.y - hipHeightBelowHMD + verticalOffset, currentHMD.z);

        if (bodyEstimator != null && bodyEstimator.HasAIHip)
        {
            Vector3 aiHip = bodyEstimator.AIHipPosition;
            float sensitivity = 0.6f;
            finalHipPos.x += (aiHip.x - currentHMD.x) * sensitivity;
            finalHipPos.z += (aiHip.z - currentHMD.z) * sensitivity;
            finalHipPos.y = aiHip.y;
        }

        // Expose for other systems (e.g. PhysioBallGenerator cube spawning)
        HipWorldPosition = finalHipPos;

        Quaternion rot = _hipLocked ? _lockedBodyRot : Quaternion.identity;
        Vector3 pelvisOffset = avatarRoot.InverseTransformPoint(pelvisBone.position);
        avatarRoot.position = finalHipPos - (rot * pelvisOffset);
        avatarRoot.rotation = rot;
    }

    // ── Setup ─────────────────────────────────────────────────────────────

    void ResolveIKWeightsOnce()
    {
        bool isLeft = sideSelector.currentSide == LegSideSelector.LegSide.Left;
        _sideLabel = isLeft ? "Left" : "Right";
        _activeIK = isLeft ? leftIK : rightIK;
        _mirrorIK = isLeft ? rightIK : leftIK;

        if (leftIK) leftIK.weight = 1f;
        if (rightIK) rightIK.weight = 1f;

        if (_activeIK != null)
        {
            _unscaledAvatarThigh = Vector3.Distance(
                _activeIK.data.root.position,
                _activeIK.data.mid.position);
            _activeShinBone = _activeIK.data.mid;
        }

        _lockedHipWorld = (bodyEstimator != null && bodyEstimator.HasAIHip)
            ? bodyEstimator.AIHipPosition
            : hmdTransform.position + Vector3.down * hipHeightBelowHMD + Vector3.up * verticalOffset;

        Vector3 lockedForward = hmdTransform.forward;
        lockedForward.y = 0f;
        if (lockedForward.sqrMagnitude < 0.001f) lockedForward = Vector3.forward;
        _lockedBodyRot = Quaternion.LookRotation(lockedForward.normalized, Vector3.up);
        _hipLocked = true;
        _hasResolvedBones = true;

        Debug.Log($"[body] IK:{_sideLabel} Thigh:{_unscaledAvatarThigh:F3}m Hip:{_lockedHipWorld:F2}");
    }

    void DisableBothIKs()
    {
        if (leftIK) leftIK.weight = 0f;
        if (rightIK) rightIK.weight = 0f;
        _sideLabel = "None";
    }

    // ── Utilities ─────────────────────────────────────────────────────────

    void HideUpperBodyParts()
    {
        foreach (Transform t in avatarRoot.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("head") || n.Contains("neck") || n.Contains("hand") || n.Contains("finger"))
                t.localScale = Vector3.zero;
        }
    }

    void PrintBodyDebug(Vector3 kneePos, Vector3 anklePos)
    {
        Debug.Log($"[body] Side:{_sideLabel} Scale:{avatarRoot.localScale.x:F2} Hip:{HipWorldPosition:F2}");
        if (_activeIK == null) return;

        Transform tip = _activeIK.data.tip;
        Transform ankleT = (_activeIK == leftIK) ? leftAnkleTarget : rightAnkleTarget;
        Transform toeT = (_activeIK == leftIK) ? leftToeTarget : rightToeTarget;

        if (ankleT != null)
            Debug.Log($"[foot] ANKLE TARGET    pos:{ankleT.position:F3}  rot:{ankleT.eulerAngles:F1}");
        if (toeT != null)
            Debug.Log($"[foot] TOE   TARGET    pos:{toeT.position:F3}  rot:{toeT.eulerAngles:F1}");
        if (tip != null)
            Debug.Log($"[foot] ANKLE BONE      pos:{tip.position:F3}  world:{tip.eulerAngles:F1}  local:{tip.localEulerAngles:F1}");
        if (tip != null)
            foreach (Transform child in tip)
                Debug.Log($"[foot] TOE   BONE      pos:{child.position:F3}  world:{child.eulerAngles:F1}  local:{child.localEulerAngles:F1}");
    }

    public void ResetFitter()
    {
        _hasResolvedBones = false;
        _smoothingInitialized = false;
        _toeInitialized = false;
        _hasLastGoodToe = false;
        _hipLocked = false;
        _lockedBodyRot = Quaternion.identity;
        _activeIK = null;
        _mirrorIK = null;
        _unscaledAvatarThigh = 0f;
        _measuredShinWorld = 0f;
        _shinMeasured = false;
        if (_activeShinBone != null) _activeShinBone.localScale = Vector3.one;
        _activeShinBone = null;
        avatarRoot.localScale = Vector3.zero;
        HipWorldPosition = Vector3.zero;

        DisableBothIKs();
        if (bodyEstimator != null) bodyEstimator.Reset();
        if (sideSelector != null) sideSelector.ResetSide();

        Debug.Log("[body] FULL RESET");
    }
}