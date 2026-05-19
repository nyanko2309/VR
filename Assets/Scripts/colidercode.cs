using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Attach to the ball/cube prefab root.
/// Requires: Collider (Is Trigger = true) + Rigidbody (Is Kinematic = true).
///
/// Spawn immunity: ignores hits for the first 0.25s after spawning.
/// Prevents the leg collider already overlapping the spawn area from
/// instantly triggering a hit.
/// </summary>
public class PhysioBall : MonoBehaviour
{
    [Tooltip("Seconds after spawn during which hits are ignored")]
    public float immunityDuration = 0.25f;

    private PhysioBallGenerator _manager;
    private bool _immune = true;
    private bool _hit = false; // prevent double-fire

    public void Setup(PhysioBallGenerator manager)
    {
        _manager = manager;
        if (_manager == null)
            Debug.LogError("[PhysioBall] Setup() called with null manager!");
    }

    void Start()
    {
        Invoke(nameof(ClearImmunity), immunityDuration);
    }

    void ClearImmunity() => _immune = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_immune || _hit) return;

        bool isLimb = other.GetComponentInParent<LegRootFitter>() != null;
        Debug.Log($"[PhysioBall] hit by {other.gameObject.name} isLimb={isLimb}");

        if (!isLimb) return;

        if (_manager == null)
        {
            Debug.LogWarning("[PhysioBall] _manager is null — was Setup() called?");
            return;
        }

        _hit = true; // lock out further hits on this ball
        _manager.HandleBallHit(gameObject);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)     { Debug.LogWarning("[PhysioBall] No Collider!", this); return; }
        if (!col.isTrigger)    Debug.LogWarning("[PhysioBall] Set Collider Is Trigger = true", this);
        if (GetComponent<Rigidbody>() == null)
                               Debug.LogWarning("[PhysioBall] Add Rigidbody (Is Kinematic = true)", this);
    }
#endif
}