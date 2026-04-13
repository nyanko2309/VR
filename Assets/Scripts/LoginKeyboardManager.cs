using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using Microsoft.MixedReality.Toolkit.Experimental.UI;

public class LoginKeyboardManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    private TMP_InputField activeField;

    void Start()
    {
        emailInput.onSelect.AddListener(_ => OpenKeyboard(emailInput));
        passwordInput.onSelect.AddListener(_ => OpenKeyboard(passwordInput));

        AddPointerClickTrigger(emailInput, () => OpenKeyboard(emailInput));
        AddPointerClickTrigger(passwordInput, () => OpenKeyboard(passwordInput));
    }

    private void AddPointerClickTrigger(TMP_InputField field, System.Action callback)
    {
        var trigger = field.gameObject.GetComponent<EventTrigger>()
                      ?? field.gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry();
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
        activeField = field;

        NonNativeKeyboard.Instance.OnTextUpdated -= UpdateField;
        NonNativeKeyboard.Instance.OnClosed -= OnKeyboardClosed;

        // Force close first if already open
        if (NonNativeKeyboard.Instance.gameObject.activeSelf)
            NonNativeKeyboard.Instance.Close();

        NonNativeKeyboard.Instance.InputField = field;
        NonNativeKeyboard.Instance.PresentKeyboard(field.text);

        NonNativeKeyboard.Instance.OnTextUpdated += UpdateField;
        NonNativeKeyboard.Instance.OnClosed += OnKeyboardClosed;
    }

    private void UpdateField(string text)
    {
        if (activeField != null)
            activeField.text = text;
    }

    private void OnKeyboardClosed(object sender, System.EventArgs e)
    {
        NonNativeKeyboard.Instance.OnTextUpdated -= UpdateField;
        NonNativeKeyboard.Instance.OnClosed -= OnKeyboardClosed;
        activeField = null;
    }
}