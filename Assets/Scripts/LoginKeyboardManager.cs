using Microsoft.MixedReality.Toolkit.Experimental.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class LoginKeyboardManager : MonoBehaviour
{
    private const string TAG = "[keyboard] ";

    [Header("Input Fields")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    private TMP_InputField activeField;

    private string savedEmailText = "";
    private string savedPasswordText = "";

    private int savedEmailCaretPosition = 0;
    private int savedPasswordCaretPosition = 0;

    private void Start()
    {
        Debug.Log(TAG + "LoginKeyboardManager started");

        if (emailInput == null)
        {
            Debug.LogError(TAG + "emailInput is not assigned");
            return;
        }

        if (passwordInput == null)
        {
            Debug.LogError(TAG + "passwordInput is not assigned");
            return;
        }

        emailInput.onSelect.AddListener(_ => OpenKeyboard(emailInput));
        passwordInput.onSelect.AddListener(_ => OpenKeyboard(passwordInput));

        AddPointerClickTrigger(emailInput, () => OpenKeyboard(emailInput));
        AddPointerClickTrigger(passwordInput, () => OpenKeyboard(passwordInput));
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

        Debug.Log(TAG + "Pointer click trigger added for: " + field.name);
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
        if (field == null)
        {
            Debug.LogError(TAG + "Tried to open keyboard for null field");
            return;
        }

        if (NonNativeKeyboard.Instance == null)
        {
            Debug.LogError(TAG + "NonNativeKeyboard.Instance is null");
            return;
        }

        SaveCurrentFieldText();

        activeField = field;

        string textToShow = GetSavedTextForField(field);
        int caretPosition = GetSavedCaretForField(field);

        field.text = textToShow;
        field.caretPosition = Mathf.Clamp(caretPosition, 0, field.text.Length);
        field.selectionAnchorPosition = field.caretPosition;
        field.selectionFocusPosition = field.caretPosition;

        NonNativeKeyboard.Instance.OnTextUpdated -= UpdateField;
        NonNativeKeyboard.Instance.OnClosed -= OnKeyboardClosed;

        NonNativeKeyboard.Instance.InputField = field;
        NonNativeKeyboard.Instance.PresentKeyboard(field.text);

        NonNativeKeyboard.Instance.OnTextUpdated += UpdateField;
        NonNativeKeyboard.Instance.OnClosed += OnKeyboardClosed;

        Debug.Log(TAG + "Keyboard opened for: " + field.name + " | text: " + field.text);
    }

    private void UpdateField(string text)
    {
        if (activeField == null)
            return;

        activeField.text = text;

        activeField.caretPosition = activeField.text.Length;
        activeField.selectionAnchorPosition = activeField.caretPosition;
        activeField.selectionFocusPosition = activeField.caretPosition;

        SaveCurrentFieldText();

        Debug.Log(TAG + "Updated field: " + activeField.name + " | text: " + text);
    }

    private void SaveCurrentFieldText()
    {
        if (activeField == null)
            return;

        if (activeField == emailInput)
        {
            savedEmailText = emailInput.text;
            savedEmailCaretPosition = emailInput.caretPosition;

            Debug.Log(TAG + "Saved email text: " + savedEmailText);
        }
        else if (activeField == passwordInput)
        {
            savedPasswordText = passwordInput.text;
            savedPasswordCaretPosition = passwordInput.caretPosition;

            Debug.Log(TAG + "Saved password text");
        }
    }

    private string GetSavedTextForField(TMP_InputField field)
    {
        if (field == emailInput)
            return savedEmailText;

        if (field == passwordInput)
            return savedPasswordText;

        return field.text;
    }

    private int GetSavedCaretForField(TMP_InputField field)
    {
        if (field == emailInput)
            return savedEmailCaretPosition;

        if (field == passwordInput)
            return savedPasswordCaretPosition;

        return field.caretPosition;
    }

    private void OnKeyboardClosed(object sender, System.EventArgs e)
    {
        SaveCurrentFieldText();

        NonNativeKeyboard.Instance.OnTextUpdated -= UpdateField;
        NonNativeKeyboard.Instance.OnClosed -= OnKeyboardClosed;

        Debug.Log(TAG + "Keyboard closed");

        activeField = null;
    }
}