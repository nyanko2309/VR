using UnityEngine;
using TMPro;
using Debug = UnityEngine.Debug;

/// <summary>
/// Attach to a 3D GameObject (e.g. a Quad or Cube with a collider).
/// When the player's laser ray hits the collider and the trigger is pressed,
/// it runs the assigned UnityEvent action.
/// Position it in world space — it won't move unless you parent it to something.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FloatingButton3D : MonoBehaviour
{
    [Header("Label")]
    public TextMeshPro label;
    public string buttonText = "Back";

    [Header("Visual")]
    public Color normalColor   = new Color(0.15f, 0.15f, 0.20f, 0.90f);
    public Color hoveredColor  = new Color(0.30f, 0.55f, 1.00f, 1.00f);
    public Color pressedColor  = new Color(1.00f, 1.00f, 1.00f, 1.00f);

    [Header("Ray Source")]
    [Tooltip("Assign the same rayOrigin used by BodyTracker / laser")]
    public Transform rayOrigin;

    [Header("Action")]
    [Tooltip("Drag in the scene component + method to call when button is pressed")]
    public UnityEngine.Events.UnityEvent onPressed;

    // ── Private ───────────────────────────────────────────────────────────

    private Renderer _rend;
    private MaterialPropertyBlock _block;
    private bool _hovered  = false;
    private bool _wasDown  = false;

    void Awake()
    {
        _rend  = GetComponentInChildren<Renderer>();
        _block = new MaterialPropertyBlock();
        if (label != null) label.text = buttonText;
        SetVisual(normalColor);
    }

    void Update()
    {
        // ── Hover detection via ray ───────────────────────────────────
        bool nowHovered = false;
        if (rayOrigin != null)
        {
            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
            if (GetComponent<Collider>().Raycast(ray, out _, 10f))
                nowHovered = true;
        }

        if (nowHovered != _hovered)
        {
            _hovered = nowHovered;
            SetVisual(_hovered ? hoveredColor : normalColor);
        }

        // ── Trigger press while hovered ───────────────────────────────
        float trigger = Mathf.Max(
            OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger),
            OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger));
        bool triggerDown = trigger > 0.8f;

        if (_hovered && triggerDown && !_wasDown)
        {
            SetVisual(pressedColor);
            Debug.Log($"[FloatingButton3D] '{buttonText}' pressed");
            onPressed?.Invoke();
        }

        if (!triggerDown && _wasDown && _hovered)
            SetVisual(hoveredColor);

        _wasDown = triggerDown;
    }

    void SetVisual(Color col)
    {
        if (_rend == null) return;
        _rend.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor",     col);
        _block.SetColor("_EmissionColor", col * 0.4f);
        _rend.SetPropertyBlock(_block);
    }
}
