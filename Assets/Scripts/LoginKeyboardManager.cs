using Microsoft.MixedReality.Toolkit.Experimental.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class LoginKeyboardManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    private TMP_InputField activeField;

    void Start()
    {
        if (emailInput != null)
        {
            emailInput.onSelect.AddListener(_ => OpenKeyboard(emailInput));
            AddPointerClickTrigger(emailInput, () => OpenKeyboard(emailInput));
        }

        if (passwordInput != null)
        {
            passwordInput.onSelect.AddListener(_ => OpenKeyboard(passwordInput));
            AddPointerClickTrigger(passwordInput, () => OpenKeyboard(passwordInput));
        }
    }

    private void AddPointerClickTrigger(TMP_InputField field, System.Action callback)
    {
        EventTrigger trigger = field.gameObject.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = field.gameObject.AddComponent<EventTrigger>();

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
        if (field == null || NonNativeKeyboard.Instance == null)
            return;

        NonNativeKeyboard.Instance.OnTextUpdated -= UpdateField;
        NonNativeKeyboard.Instance.OnClosed -= OnKeyboardClosed;

        activeField = field;

        string currentText = field.text;

        field.caretPosition = field.text.Length;
        field.ForceLabelUpdate();

        NonNativeKeyboard.Instance.InputField = field;

        NonNativeKeyboard.Instance.OnTextUpdated += UpdateField;
        NonNativeKeyboard.Instance.OnClosed += OnKeyboardClosed;

        NonNativeKeyboard.Instance.PresentKeyboard(currentText);
    }

    private void UpdateField(string text)
    {
        if (activeField == null)
            return;

        activeField.SetTextWithoutNotify(text);
        activeField.caretPosition = activeField.text.Length;
        activeField.ForceLabelUpdate();
    }

    private void OnKeyboardClosed(object sender, System.EventArgs e)
    {
        NonNativeKeyboard.Instance.OnTextUpdated -= UpdateField;
        NonNativeKeyboard.Instance.OnClosed -= OnKeyboardClosed;

        activeField = null;
    }
}