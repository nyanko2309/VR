using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;   // Fixes ambiguity with System.Diagnostics.Debug

public class SceneController : MonoBehaviour
{
    public void ChangeScene(string sceneName)
    {
        Debug.Log($"[ray] xhanging scene");
        SceneManager.LoadScene(sceneName);
    }
}
