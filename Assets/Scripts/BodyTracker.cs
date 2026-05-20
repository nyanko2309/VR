using Meta.XR;
using TMPro;
using UnityEngine;
using Unity.Collections;
using Debug = UnityEngine.Debug;

public class BodyTracker : MonoBehaviour
{
    [Header("XR")]
    public Transform rayOrigin;
    public Camera vrCamera;
    public EnvironmentRaycastManager raycastManager;
    public PassthroughCameraAccess cameraAccess;

    [Header("Detectors — assign in Inspector")]
    public BlobDetector kneeDetector;
    public BlobDetector ankleDetector;
    public BlobDetector toeDetector;

    [Header("UI")]
    public GameObject instructionsCanvas;
    public TMPro.TextMeshProUGUI instructionsText;

    [Header("Fitter")]
    public LegRootFitter legRootFitter;

    [Header("Game Reset")]
    public LegSideSelector legSideSelector;
    public PhysioBallGenerator ballGenerator;

    [Header("Laser")]
    public LineRenderer laser;
    public float laserLength = 5f;

    [Header("Model Size Guard")]
    [Tooltip("Max fractional avatar scale change a new knee position may cause before being rejected. 0 = disabled.")]
    [Range(0f, 1f)]
    public float maxScaleChangeFraction = 0.25f;
    [Tooltip("Max fractional shin scale change a new ankle position may cause before being rejected. 0 = disabled.")]
    [Range(0f, 1f)]
    public float maxShinChangeFraction = 0.25f;

    public enum SetupStep { Idle, WaitingKnee, WaitingAnkle, WaitingToes, AllTracking }
    public SetupStep CurrentStep { get; private set; } = SetupStep.Idle;

    private bool _triggerWasDown = false;

    public Vector3 KneePosition => kneeDetector.LastKnownWorldPosition;
    public Vector3 AnklePosition => ankleDetector.LastKnownWorldPosition;
    public Vector3 ToesPosition => toeDetector != null ? toeDetector.LastKnownWorldPosition : Vector3.zero;

    public bool KneeValid => kneeDetector != null && kneeDetector.HasValidLocation;
    public bool AnkleValid => ankleDetector != null && ankleDetector.HasValidLocation;
    public bool ToesValid => toeDetector != null && toeDetector.HasValidLocation;

    void Start()
    {
        if (laser) laser.positionCount = 2;
        CurrentStep = SetupStep.Idle;
        Debug.Log("[model] Press trigger to detect KNEE sticker");
        if (instructionsText != null) instructionsText.text = "▶ Point at KNEE sticker → squeeze trigger";
        if (instructionsCanvas != null) instructionsCanvas.SetActive(true);

        // When a detector times out after 5s, reset that sticker and prompt re-aim
        if (kneeDetector != null) kneeDetector.OnSearchTimedOut += () => OnDetectorTimedOut("KNEE");
        if (ankleDetector != null) ankleDetector.OnSearchTimedOut += () => OnDetectorTimedOut("ANKLE");
        if (toeDetector != null) toeDetector.OnSearchTimedOut += () => OnDetectorTimedOut("TOES");

        // Wire size guards. Knee drives avatar body scale; ankle drives shin bone scale.
        // Each validator receives the candidate hit.point and returns false to reject it.
        if (kneeDetector != null)
            kneeDetector.WorldPositionValidator = ValidateKneePosition;
        if (ankleDetector != null)
            ankleDetector.WorldPositionValidator = ValidateAnklePosition;
    }

    /// <summary>
    /// Called by kneeDetector before accepting a new world position.
    /// Returns false (reject) if the position would shift avatar scale by more
    /// than maxScaleChangeFraction relative to the current scale.
    /// </summary>
    bool ValidateKneePosition(Vector3 candidateKneeWorld)
    {
        if (legRootFitter == null || maxScaleChangeFraction <= 0f) return true;

        float currentScale = legRootFitter.avatarRoot != null
            ? legRootFitter.avatarRoot.localScale.x
            : 0f;

        // Can't validate before the avatar has a real scale — let it through.
        if (currentScale < 0.01f) return true;

        // Replicate LegRootFitter.ApplyDynamicScaling's scale formula:
        // scale = (hip→knee / unscaledThigh) * sizeMultiplier
        // We expose HipWorldPosition from LegRootFitter for this.
        Vector3 hipPos = legRootFitter.HipWorldPosition;
        if (hipPos == Vector3.zero) return true;

        float unscaledThigh = legRootFitter.avatarRoot.localScale.x > 0.01f
            ? currentScale / legRootFitter.sizeMultiplier  // back-compute: scale/mult = dist/thigh
            : 0f;

        // We don't have direct access to _unscaledAvatarThigh, so derive candidate scale
        // from the ratio of new hip→knee vs current hip→knee.
        // No baseline yet — knee has never had a valid position, let the first one through.
        if (kneeDetector.LastKnownWorldPosition == Vector3.zero) return true;

        float currentHipToKnee = Vector3.Distance(hipPos, kneeDetector.LastKnownWorldPosition);
        float candidateHipToKnee = Vector3.Distance(hipPos, candidateKneeWorld);

        float scaleRatio = candidateHipToKnee / currentHipToKnee;
        float fractionalChange = Mathf.Abs(scaleRatio - 1f);

        if (fractionalChange > maxScaleChangeFraction)
        {
            Debug.Log($"[model] kneeDetector Size guard REJECTED — scale would shift " +
                      $"{fractionalChange * 100f:F1}% (limit {maxScaleChangeFraction * 100f:F0}%) " +
                      $"hipToKnee: {currentHipToKnee:F3}→{candidateHipToKnee:F3}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Called by ankleDetector before accepting a new world position.
    /// Returns false (reject) if the position would shift the shin bone scale by more
    /// than maxShinChangeFraction relative to the current knee→ankle distance.
    /// ApplyShinScaling drives shinBone.localScale ∝ knee→ankle, so a bad ankle
    /// position causes the same kind of dramatic pop as a bad knee position does
    /// for the body scale.
    /// </summary>
    bool ValidateAnklePosition(Vector3 candidateAnkleWorld)
    {
        if (legRootFitter == null || maxShinChangeFraction <= 0f) return true;

        // Need a stable knee position to measure against.
        Vector3 kneePos = kneeDetector != null ? kneeDetector.LastKnownWorldPosition : Vector3.zero;
        if (kneePos == Vector3.zero) return true;

        // No baseline yet — ankle has never had a valid position, let the first one through.
        if (ankleDetector.LastKnownWorldPosition == Vector3.zero) return true;

        float currentKneeToAnkle = Vector3.Distance(kneePos, ankleDetector.LastKnownWorldPosition);

        float candidateKneeToAnkle = Vector3.Distance(kneePos, candidateAnkleWorld);
        float shinRatio = candidateKneeToAnkle / currentKneeToAnkle;
        float fractionalChange = Mathf.Abs(shinRatio - 1f);

        if (fractionalChange > maxShinChangeFraction)
        {
            Debug.Log($"[model] ankleDetector Shin guard REJECTED — shin would shift " +
                      $"{fractionalChange * 100f:F1}% (limit {maxShinChangeFraction * 100f:F0}%) " +
                      $"kneeToAnkle: {currentKneeToAnkle:F3}→{candidateKneeToAnkle:F3}");
            return false;
        }

        return true;
    }

    void OnDetectorTimedOut(string stickerName)
    {
        Debug.Log($"[model] {stickerName} sticker lost too long — triggering full game reset");
        ResetAll();
    }

    void Update()
    {
        if (!cameraAccess || !cameraAccess.IsPlaying) return;

        NativeArray<Color32> pixels = cameraAccess.GetColors();
        Vector2Int res = cameraAccess.CurrentResolution;
        if (!pixels.IsCreated || pixels.Length == 0) return;

        // Always tick all three so they are ready the moment user presses
        kneeDetector.Tick(pixels, res);
        ankleDetector.Tick(pixels, res);
        if (toeDetector != null) toeDetector.Tick(pixels, res);

        // Laser
        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        bool hitSurface = raycastManager.Raycast(ray, out var hit);
        Vector3 laserEnd = hitSurface ? hit.point : ray.origin + ray.direction * laserLength;
        if (laser)
        {
            laser.SetPosition(0, ray.origin);
            laser.SetPosition(1, laserEnd);
        }

        // Trigger
        float trigger = Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger),
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger));

        bool triggerDown = trigger > 0.8f;
        if (triggerDown && !_triggerWasDown)
        {
            HandleButton(hitSurface, hit);
            Debug.Log("[model] Trigger pressed");
        }
        _triggerWasDown = triggerDown;

        // Periodic re-scan every 100 frames to reacquire lost stickers
        if (CurrentStep == SetupStep.AllTracking && Time.frameCount % 100 == 0)
        {
            if (kneeDetector != null && !kneeDetector.IsTracking && kneeDetector.LastKnownWorldPosition != Vector3.zero)
                kneeDetector.TriggerDetect(kneeDetector.LastKnownWorldPosition, kneeDetector.LastKnownNormal);
            if (ankleDetector != null && !ankleDetector.IsTracking && ankleDetector.LastKnownWorldPosition != Vector3.zero)
                ankleDetector.TriggerDetect(ankleDetector.LastKnownWorldPosition, ankleDetector.LastKnownNormal);
            if (toeDetector != null && !toeDetector.IsTracking && toeDetector.LastKnownWorldPosition != Vector3.zero)
                toeDetector.TriggerDetect(toeDetector.LastKnownWorldPosition, toeDetector.LastKnownNormal);
        }
    }

    void HandleButton(bool hitSurface, EnvironmentRaycastHit hit)
    {
        // Press 3 — full reset
        if (CurrentStep == SetupStep.AllTracking)
        {
            ResetAll();
            return;
        }

        if (!hitSurface)
        {
            Debug.Log("[model] No surface hit — point at sticker");
            return;
        }

        // Press 1 — detect knee
        if (CurrentStep == SetupStep.Idle)
        {
            if (kneeDetector == null) { Debug.LogError("[model] kneeDetector not assigned!"); return; }
            CurrentStep = SetupStep.WaitingKnee;
            kneeDetector.TriggerDetect(hit.point, hit.normal);
            if (kneeDetector.IsTracking)
            {
                CurrentStep = SetupStep.WaitingAnkle;
                LogStep();
            }
            else
                Debug.Log("[model] Knee detect failed — try again");
            return;
        }

        // Press 2 — detect ankle
        if (CurrentStep == SetupStep.WaitingAnkle)
        {
            if (ankleDetector == null) { Debug.LogError("[model] ankleDetector not assigned!"); return; }
            ankleDetector.TriggerDetect(hit.point, hit.normal);
            if (ankleDetector.IsTracking)
            {
                CurrentStep = SetupStep.WaitingToes;
                LogStep();
            }
            else
                Debug.Log("[model] Ankle detect failed — try again");
            return;
        }

        // Press 3 — detect toes
        if (CurrentStep == SetupStep.WaitingToes)
        {
            if (toeDetector == null) { Debug.LogError("[model] toeDetector not assigned!"); return; }
            toeDetector.TriggerDetect(hit.point, hit.normal);
            if (toeDetector.IsTracking)
            {
                CurrentStep = SetupStep.AllTracking;
                LogStep();
            }
            else
                Debug.Log("[model] Toes detect failed — try again");
        }
    }

    void ResetAll()
    {
        kneeDetector.ResetDetector();
        ankleDetector.ResetDetector();
        if (toeDetector != null) toeDetector.ResetDetector();
        CurrentStep = SetupStep.Idle;

        if (legRootFitter != null)
            legRootFitter.ResetFitter();

        LogStep();
    }

    void LogStep()
    {
        string msg = CurrentStep switch
        {
            SetupStep.Idle => "▶ Point at KNEE sticker → squeeze trigger",
            SetupStep.WaitingAnkle => "👆 Point at ANKLE sticker → squeeze trigger",
            SetupStep.WaitingToes => "👆 Point at TOES sticker → squeeze trigger",
            SetupStep.AllTracking => "✅ Knee + Ankle + Toes tracking!",
            _ => ""
        };

        Debug.Log($"[model] Step: {msg}");

        if (instructionsText != null)
            instructionsText.text = msg;

        if (instructionsCanvas != null)
            instructionsCanvas.SetActive(CurrentStep != SetupStep.AllTracking);
    }
}