using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using Microsoft.MixedReality.Toolkit.Experimental.UI;

public class LoginKeyboardManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    private TMP_InputField activeField;
    private NonNativeKeyboard keyboard;

    private void Start()
    {
        keyboard = NonNativeKeyboard.Instance;

        if (keyboard == null)
            keyboard = FindObjectOfType<NonNativeKeyboard>(true);

        if (keyboard == null)
        {
            Debug.LogError("NonNativeKeyboard not found in scene.");
            return;
        }

        keyboard.OnTextUpdated -= HandleTextUpdated;
        keyboard.OnClosed -= HandleKeyboardClosed;

        keyboard.OnTextUpdated += HandleTextUpdated;
        keyboard.OnClosed += HandleKeyboardClosed;

        if (keyboard.gameObject.activeSelf)
            keyboard.Close();

        keyboard.gameObject.SetActive(false);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        RegisterField(emailInput);
        RegisterField(passwordInput);
    }

    private void RegisterField(TMP_InputField field)
    {
        if (field == null)
            return;

        field.onSelect.AddListener(_ => OpenKeyboard(field));
        AddPointerClickTrigger(field, () => OpenKeyboard(field));
    }

    private void AddPointerClickTrigger(TMP_InputField field, System.Action callback)
    {
        EventTrigger trigger = field.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = field.gameObject.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener(_ => callback());
        trigger.triggers.Add(entry);
    }

    public void OpenEmailKeyboard()
    {
        OpenKeyboard(emailInput);
    }

    public void OpenPasswordKeyboard()
    {
        OpenKeyboard(passwordInput);
    }

    private void OpenKeyboard(TMP_InputField field)
    {
        if (field == null || keyboard == null)
            return;

        activeField = field;

        field.Select();
        field.ActivateInputField();

        if (!keyboard.gameObject.activeSelf)
            keyboard.gameObject.SetActive(true);

        keyboard.InputField = field;
        keyboard.PresentKeyboard(field.text);
    }

    private void HandleTextUpdated(string text)
    {
        if (activeField == null)
            return;

        activeField.text = text;
        activeField.caretPosition = activeField.text.Length;
        activeField.selectionAnchorPosition = activeField.text.Length;
        activeField.selectionFocusPosition = activeField.text.Length;
        activeField.ForceLabelUpdate();
    }

    private void HandleKeyboardClosed(object sender, System.EventArgs e)
    {
        activeField = null;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnDestroy()
    {
        if (keyboard != null)
        {
            keyboard.OnTextUpdated -= HandleTextUpdated;
            keyboard.OnClosed -= HandleKeyboardClosed;
        }
    }
}