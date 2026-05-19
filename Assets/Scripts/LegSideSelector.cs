using UnityEngine;
using Debug = UnityEngine.Debug;

public class LegSideSelector : MonoBehaviour
{
    [Header("Tracking Source")]
    public BodyTracker bodyTracker;
    public Transform hmdTransform;

    [Header("Mode")]
    public SelectionMode selectionMode = SelectionMode.Auto;
    public PhysioBallGenerator ballGenerator;

    [Header("Debug")]
    public bool showDebug = true;

    [Header("Runtime Output")]
    public LegSide currentSide = LegSide.Left;
    public bool sideLocked = false;

    public enum LegSide { Left, Right }
    public enum SelectionMode { Auto, ForceLeft, ForceRight }

    private Vector3? _kneeSnapshot;
    private Vector3? _ankleSnapshot;
    private Vector3? _toesSnapshot;
    [Header("UI Elements")]
    public GameObject screenSplitUI;
    void Update()
    {
        if (bodyTracker == null || hmdTransform == null) return;

        // Wait until both markers confirmed
        if (bodyTracker.CurrentStep != BodyTracker.SetupStep.AllTracking)
        {
            if (showDebug && Time.frameCount % 60 == 0)
                Debug.Log("[side] Waiting for FULL setup");
            return;
        }

        // Forced modes
        if (selectionMode == SelectionMode.ForceLeft) { currentSide = LegSide.Left; sideLocked = true; return; }
        if (selectionMode == SelectionMode.ForceRight) { currentSide = LegSide.Right; sideLocked = true; return; }

        if (sideLocked) return;

        // Capture snapshots once
        if (!_kneeSnapshot.HasValue && bodyTracker.KneeValid)
        {
            _kneeSnapshot = bodyTracker.KneePosition;
            if (showDebug) Debug.Log($"[side] Knee snapshot: {_kneeSnapshot.Value}");
        }
        if (!_ankleSnapshot.HasValue && bodyTracker.AnkleValid)
        {
            _ankleSnapshot = bodyTracker.AnklePosition;
            if (showDebug) Debug.Log($"[side] Ankle snapshot: {_ankleSnapshot.Value}");
        }
        if (!_toesSnapshot.HasValue && bodyTracker.ToesValid)
        {
            _toesSnapshot = bodyTracker.ToesPosition;
            if (showDebug) Debug.Log($"[side] Toes snapshot: {_toesSnapshot.Value}");
        }

        if (!_kneeSnapshot.HasValue || !_ankleSnapshot.HasValue || !_toesSnapshot.HasValue) return;

        // Vote using all three markers — more data points = more reliable side detection
        int leftVotes = 0, rightVotes = 0;
        VoteByWorldX(_kneeSnapshot.Value, ref leftVotes, ref rightVotes, "KNEE");
        VoteByWorldX(_ankleSnapshot.Value, ref leftVotes, ref rightVotes, "ANKLE");
        VoteByWorldX(_toesSnapshot.Value, ref leftVotes, ref rightVotes, "TOES");

        currentSide = leftVotes > rightVotes ? LegSide.Left : LegSide.Right;
        sideLocked = true;

        if (showDebug)
            Debug.Log($"[side] LOCKED → {currentSide} (L:{leftVotes} R:{rightVotes})");

        if (screenSplitUI != null && screenSplitUI.activeSelf)
        {
            screenSplitUI.SetActive(false);
        }
    }

    // Vote based on whether marker is to the right of the HMD in its own local space.
    // Using hmdTransform.right (instead of raw world X) means the result is correct
    // regardless of which direction the user is facing.
    void VoteByWorldX(Vector3 worldPos, ref int left, ref int right, string jointName)
    {
        Vector3 toMarker = worldPos - hmdTransform.position;

        // Project onto the HMD's right axis: positive = right of user, negative = left
        float lateralDot = Vector3.Dot(toMarker, hmdTransform.right);

        if (showDebug)
            Debug.Log($"[side] {jointName} world:{worldPos:F2} lateralDot:{lateralDot:F3}");

        if (lateralDot < 0)
        {
            left++;
            if (showDebug) Debug.Log($"[side] {jointName} → LEFT  (dot:{lateralDot:F3})");
        }
        else
        {
            right++;
            if (showDebug) Debug.Log($"[side] {jointName} → RIGHT (dot:{lateralDot:F3})");
        }
    }

    public void ResetSide()
    {
        if (screenSplitUI != null && !screenSplitUI.activeSelf)
        {
            screenSplitUI.SetActive(true);
        }
        sideLocked = false;
        currentSide = LegSide.Left;
        _kneeSnapshot = null;
        _ankleSnapshot = null;
        _toesSnapshot = null;
        if (showDebug) Debug.Log("[side] Reset complete");
        if (ballGenerator != null) ballGenerator.ResetGame(); 

    }
}