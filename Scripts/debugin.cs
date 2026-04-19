using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using Debug = UnityEngine.Debug;   // Fixes ambiguity with System.Diagnostics.Debug

public class ManualUIPresser : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor _ray;

    void Start()
    {
        _ray = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
    }

    void Update()
    {
        // Log every frame what we're hovering
        if (_ray.TryGetCurrentUIRaycastResult(out var result))
        {
            Debug.Log($"[ray] Hovering: {result.gameObject.name}");

            // Check every device for any button press
            foreach (var device in InputSystem.devices)
            {
                foreach (var control in device.allControls)
                {
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl btn && btn.wasPressedThisFrame)
                    {
                        Debug.Log($"[ray] {btn.name} pressed while hovering — invoking button!");
                        var button = result.gameObject.GetComponentInParent<Button>();
                        if (button != null) button.onClick.Invoke();
                    }
                }
            }
        }
    }
}