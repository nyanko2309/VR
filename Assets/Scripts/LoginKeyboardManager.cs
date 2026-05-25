using Microsoft.MixedReality.Toolkit.Experimental.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class LoginKeyboardManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    private TMP_InputField activeField;
    private string savedEmailText = "";
    private string savedPasswordText = "";

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

    public void OpenEmailKeyboard() => OpenKeyboard(emailInput);
    public void OpenPasswordKeyboard() => OpenKeyboard(passwordInput);

    private void OpenKeyboard(TMP_InputField field)
    {
        if (field == null || NonNativeKeyboard.Instance == null)
            return;

        // Save both fields before opening
        savedEmailText = emailInput.text;
        savedPasswordText = passwordInput.text;

        NonNativeKeyboard.Instance.OnTextUpdated -= UpdateField;
        NonNativeKeyboard.Instance.OnClosed -= OnKeyboardClosed;

        activeField = field;

        NonNativeKeyboard.Instance.InputField = field;
        NonNativeKeyboard.Instance.OnTextUpdated += UpdateField;
        NonNativeKeyboard.Instance.OnClosed += OnKeyboardClosed;
        NonNativeKeyboard.Instance.PresentKeyboard(field.text);

        // Restore the other field immediately
        if (field == emailInput)
            passwordInput.SetTextWithoutNotify(savedPasswordText);
        else
            emailInput.SetTextWithoutNotify(savedEmailText);
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