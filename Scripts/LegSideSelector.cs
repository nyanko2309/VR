using UnityEngine;
using Debug = UnityEngine.Debug;

public class LegSideSelector : MonoBehaviour
{
    [Header("Tracking Source")]
    public BodyTracker bodyTracker;
    public Transform hmdTransform;

    [Header("Mode")]
    public SelectionMode selectionMode = SelectionMode.Auto;

    [Header("Debug")]
    public bool showDebug = true;

    [Header("Runtime Output")]
    public LegSide currentSide = LegSide.Left;
    public bool sideLocked = false;

    public enum LegSide { Left, Right }
    public enum SelectionMode { Auto, ForceLeft, ForceRight }

    private Vector3? _kneeSnapshot;
    private Vector3? _ankleSnapshot;

    [Header("UI Elements")]
    public GameObject screenSplitUI;

    [Header("Game Reference")]
    public PhysioBallGenerator ballGenerator;

    void Update()
    {
        if (bodyTracker == null || hmdTransform == null) return;

        if (bodyTracker.CurrentStep != BodyTracker.SetupStep.AllTracking)
        {
            if (showDebug && Time.frameCount % 60 == 0)
                Debug.Log("[side] Waiting for FULL setup");
            return;
        }

        // Forced modes
        if (selectionMode == SelectionMode.ForceLeft)
        {
            currentSide = LegSide.Left;
            if (!sideLocked) { sideLocked = true; HideSplitScreen(); if (showDebug) Debug.Log("[side] FORCED → Left"); }
            return;
        }
        if (selectionMode == SelectionMode.ForceRight)
        {
            currentSide = LegSide.Right;
            if (!sideLocked) { sideLocked = true; HideSplitScreen(); if (showDebug) Debug.Log("[side] FORCED → Right"); }
            return;
        }

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

        if (!_kneeSnapshot.HasValue || !_ankleSnapshot.HasValue) return;

        int leftVotes = 0, rightVotes = 0;
        VoteByWorldX(_kneeSnapshot.Value, ref leftVotes, ref rightVotes, "KNEE");
        VoteByWorldX(_ankleSnapshot.Value, ref leftVotes, ref rightVotes, "ANKLE");

        currentSide = leftVotes > rightVotes ? LegSide.Left : LegSide.Right;
        sideLocked = true;

        if (showDebug)
            Debug.Log($"[side] LOCKED → {currentSide} (L:{leftVotes} R:{rightVotes})");

        HideSplitScreen();
    }

    void HideSplitScreen()
    {
        if (screenSplitUI != null && screenSplitUI.activeSelf)
            screenSplitUI.SetActive(false);
    }

    void VoteByWorldX(Vector3 worldPos, ref int left, ref int right, string jointName)
    {
        Vector3 relative = hmdTransform.InverseTransformPoint(worldPos);

        if (showDebug)
            Debug.Log($"[side] {jointName} world:{worldPos:F2} relative:{relative:F2}");

        if (relative.x < 0)
        {
            left++;
            if (showDebug) Debug.Log($"[side] {jointName} → LEFT  (rel.x:{relative.x:F3})");
        }
        else
        {
            right++;
            if (showDebug) Debug.Log($"[side] {jointName} → RIGHT (rel.x:{relative.x:F3})");
        }
    }

    public void ResetSide()
    {
        // Restore split-screen UI
        if (screenSplitUI != null && !screenSplitUI.activeSelf)
            screenSplitUI.SetActive(true);

        // Reset side detection
        sideLocked = false;
        currentSide = LegSide.Left;
        _kneeSnapshot = null;
        _ankleSnapshot = null;

        // Reset the game — destroys ball and clears fog
        if (ballGenerator != null)
            ballGenerator.ResetGame();
        else
            Debug.LogWarning("[side] ballGenerator not assigned — game won't reset!");

        if (showDebug) Debug.Log("[side] Reset complete");
    }
}