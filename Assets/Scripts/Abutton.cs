using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

public class ControllerActionEventLinker : MonoBehaviour
{
    [Header("Input Action")]
    [Tooltip("Assign 'XRI Right Hand/Primary Button' for the physical A Button on Quest controllers.")]
    public InputActionReference buttonAction;

    [Header("Triggered Event")]
    [Tooltip("Add actions here just like a UI Button's On Click list!")]
    public UnityEvent OnActionExecuted;

    private void OnEnable()
    {
        if (buttonAction != null)
        {
            buttonAction.action.Enable();
            buttonAction.action.performed += OnButtonPressed;
        }
    }

    private void OnDisable()
    {
        if (buttonAction != null)
        {
            buttonAction.action.performed -= OnButtonPressed;
        }
    }

    private void OnButtonPressed(InputAction.CallbackContext context)
    {
        // Debug statement reading the action name with the [Abutton] tag
        Debug.Log($"[Abutton] Controller button pressed! Action Name: {context.action.name}");

        // Safely invoke whatever is assigned in the Unity Inspector
        OnActionExecuted?.Invoke();
    }
}