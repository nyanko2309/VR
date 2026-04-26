using UnityEngine;
using TMPro;
using Debug = UnityEngine.Debug;
public class KeyboardDebug : MonoBehaviour
{
    [SerializeField] private TMP_InputField emailInput;

    void Start()
    {
        emailInput.onSelect.AddListener(_ => {
            Debug.Log("=== FIELD SELECTED ===");
            var kb = TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
            Debug.Log("Keyboard active: " + kb.active);
            Debug.Log("Keyboard status: " + kb.status);
        });
    }
}