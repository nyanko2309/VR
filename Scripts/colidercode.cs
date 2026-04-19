using UnityEngine;
using Debug = UnityEngine.Debug;   // Fixes ambiguity with System.Diagnostics.Debug

/// <summary>
/// Attach to the ball/cube prefab root.
/// Requires: any Collider with "Is Trigger" ticked, plus a Rigidbody with "Is Kinematic" ticked.
/// Both are confirmed present in the Inspector screenshots.
/// </summary>
public class PhysioBall : MonoBehaviour
{
    private PhysioBallGenerator _manager;

    /// <summary>Called by PhysioBallGenerator immediately after Instantiate.</summary>
    public void Setup(PhysioBallGenerator manager)
    {
        _manager = manager;
        if (_manager == null)
            Debug.LogError("[PhysioBall] Setup() called with null manager!");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Accept hits from avatar limbs or anything tagged "Player"
        bool isAvatarLimb = other.GetComponentInParent<LegRootFitter>() != null;
        //bool isPlayerTag = other.CompareTag("Player");
        Debug.Log($"[game] OnTriggerEnter hit by: {other.gameObject.name} hasLegRoot={other.GetComponentInParent<LegRootFitter>() != null}");

        if (_manager == null)
        {
            Debug.LogWarning("[PhysioBall] Hit detected but _manager is null. Was Setup() called?");
            return;
        }

        _manager.HandleBallHit(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)       { Debug.LogWarning("[PhysioBall] No Collider on root!", this); return; }
        if (!col.isTrigger)      Debug.LogWarning("[PhysioBall] Collider 'Is Trigger' must be ticked.", this);
        if (GetComponent<Rigidbody>() == null)
                                 Debug.LogWarning("[PhysioBall] Add a Rigidbody (Is Kinematic = true).", this);
    }
#endif
}