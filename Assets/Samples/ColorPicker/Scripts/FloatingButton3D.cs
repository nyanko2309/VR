using UnityEngine;
using TMPro;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(Collider))]
public class FloatingButton3D : MonoBehaviour
{
    [Header("Label")]
    public TextMeshPro label;
    public string buttonText = "Back";

    [Header("Visual")]
    public Color normalColor = new Color(0.15f, 0.15f, 0.20f, 0.90f);
    public Color hoveredColor = new Color(0.30f, 0.55f, 1.00f, 1.00f);
    public Color pressedColor = new Color(1.00f, 1.00f, 1.00f, 1.00f);

    [Header("Ray Source")]
    [Tooltip("Assign the same rayOrigin used by BodyTracker / laser")]
    public Transform rayOrigin;

    [Header("Action")]
    [Tooltip("Drag in the scene component + method to call when button is pressed")]
    public UnityEngine.Events.UnityEvent onPressed;

    // ── Private ───────────────────────────────────────────────────────────
    private Renderer _rend;
    private MaterialPropertyBlock _block;
    private bool _hovered = false;

    void Awake()
    {
        _rend = GetComponentInChildren<Renderer>();
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

        // ── Any button press ──────────────────────────────────────────
        bool anyDown = OVRInput.GetDown(OVRInput.Button.Any);

        // ── Debug: log any controller input ──────────────────────────
        if (anyDown)
            Debug.Log($"[FloatingButton3D] Button pressed (hovered={_hovered})", this);

        // ── Trigger action if hovered ─────────────────────────────────
        if (_hovered && anyDown)
        {
            SetVisual(pressedColor);
            Debug.Log($"[FloatingButton3D] '{buttonText}' pressed");
            onPressed?.Invoke();
        }

        if (!anyDown && _hovered)
            SetVisual(hoveredColor);
    }

    void SetVisual(Color col)
    {
        if (_rend == null) return;
        _rend.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", col);
        _block.SetColor("_EmissionColor", col * 0.4f);
        _rend.SetPropertyBlock(_block);
    }
}