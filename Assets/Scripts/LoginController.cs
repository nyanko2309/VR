using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NewBehaviourScript : MonoBehaviour
{
    // פונקציה שתופעל כשילחצו על כפתור ה-Login
    public void OnLoginButtonClicked()
    {
        // השם "Main" חייב להיות בדיוק כמו שם הסצנה השנייה שלך
        SceneManager.LoadScene("Main");
    }
}
