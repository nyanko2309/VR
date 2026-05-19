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
        Debug.Log("[start] Press trigger to detect KNEE sticker");
        if (instructionsText != null) instructionsText.text = "▶ Point at KNEE sticker → squeeze trigger";
        if (instructionsCanvas != null) instructionsCanvas.SetActive(true);

        // When a detector times out after 5s, reset that sticker and prompt re-aim
        if (kneeDetector != null) kneeDetector.OnSearchTimedOut += () => OnDetectorTimedOut("KNEE");
        if (ankleDetector != null) ankleDetector.OnSearchTimedOut += () => OnDetectorTimedOut("ANKLE");
        if (toeDetector != null) toeDetector.OnSearchTimedOut += () => OnDetectorTimedOut("TOES");
    }

    void OnDetectorTimedOut(string stickerName)
    {
        Debug.Log($"[BodyTracker] {stickerName} sticker lost — resetting to re-aim");

        // Reset whichever detector timed out and go back to its setup step
        if (stickerName == "KNEE")
        {
            kneeDetector.ResetDetector();
            CurrentStep = SetupStep.Idle;
        }
        else if (stickerName == "ANKLE")
        {
            ankleDetector.ResetDetector();
            CurrentStep = SetupStep.WaitingAnkle;
        }
        else if (stickerName == "TOES")
        {
            toeDetector?.ResetDetector();
            CurrentStep = SetupStep.WaitingToes;
        }

        LogStep();
        if (instructionsCanvas != null) instructionsCanvas.SetActive(true);
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
            Debug.Log("[button] pressed");
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
            Debug.Log("[BodyTracker] No surface hit — point at sticker");
            return;
        }

        // Press 1 — detect knee
        if (CurrentStep == SetupStep.Idle)
        {
            if (kneeDetector == null) { Debug.LogError("[BodyTracker] kneeDetector not assigned!"); return; }
            CurrentStep = SetupStep.WaitingKnee;
            kneeDetector.TriggerDetect(hit.point, hit.normal);
            if (kneeDetector.IsTracking)
            {
                CurrentStep = SetupStep.WaitingAnkle;
                LogStep();
            }
            else
                Debug.Log("[BodyTracker] Knee detect failed — try again");
            return;
        }

        // Press 2 — detect ankle
        if (CurrentStep == SetupStep.WaitingAnkle)
        {
            if (ankleDetector == null) { Debug.LogError("[BodyTracker] ankleDetector not assigned!"); return; }
            ankleDetector.TriggerDetect(hit.point, hit.normal);
            if (ankleDetector.IsTracking)
            {
                CurrentStep = SetupStep.WaitingToes;
                LogStep();
            }
            else
                Debug.Log("[BodyTracker] Ankle detect failed — try again");
            return;
        }

        // Press 3 — detect toes
        if (CurrentStep == SetupStep.WaitingToes)
        {
            if (toeDetector == null) { Debug.LogError("[BodyTracker] toeDetector not assigned!"); return; }
            toeDetector.TriggerDetect(hit.point, hit.normal);
            if (toeDetector.IsTracking)
            {
                CurrentStep = SetupStep.AllTracking;
                LogStep();
            }
            else
                Debug.Log("[BodyTracker] Toes detect failed — try again");
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

        Debug.Log(msg);

        if (instructionsText != null)
            instructionsText.text = msg;

        if (instructionsCanvas != null)
            instructionsCanvas.SetActive(CurrentStep != SetupStep.AllTracking);
    }
}