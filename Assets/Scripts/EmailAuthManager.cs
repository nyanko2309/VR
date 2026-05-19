using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EmailAuthManager : MonoBehaviour
{
    [Header("UI")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_Text statusText;

    [Header("Navigation")]
    public string nextSceneName = "Menu";

    private FirebaseAuth auth;

    private void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                SetStatus("Firebase ready");
            }
            else
            {
                SetStatus("Firebase error: " + task.Result);
                Debug.LogError("Firebase dependency error: " + task.Result);
            }
        });
    }

    public void LoginUser()
    {
        if (auth == null)
        {
            SetStatus("Firebase not initialized");
            return;
        }

        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (!ValidateInput(email, password))
            return;

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                SetStatus("Login canceled");
                return;
            }

            if (task.IsFaulted)
            {
                SetStatus(GetErrorMessage(task.Exception));
                return;
            }

            FirebaseUser user = task.Result.User;
            SetStatus("Login success: " + user.Email);

            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        });
    }

    public void RegisterUser()
    {
        if (auth == null)
        {
            SetStatus("Firebase not initialized");
            return;
        }

        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (!ValidateInput(email, password))
            return;

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                SetStatus("Registration canceled");
                return;
            }

            if (task.IsFaulted)
            {
                SetStatus(GetErrorMessage(task.Exception));
                return;
            }

            FirebaseUser user = task.Result.User;
            SetStatus("Registration success: " + user.Email);

            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        });
    }

    private bool ValidateInput(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            SetStatus("Please enter email");
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Please enter password");
            return false;
        }

        if (password.Length < 6)
        {
            SetStatus("Password must be at least 6 characters");
            return false;
        }

        return true;
    }

    private string GetErrorMessage(System.AggregateException exception)
    {
        if (exception == null)
            return "Unknown error";

        FirebaseException firebaseException = exception.GetBaseException() as FirebaseException;
        if (firebaseException == null)
            return "Firebase authentication error";

        AuthError errorCode = (AuthError)firebaseException.ErrorCode;

        switch (errorCode)
        {
            case AuthError.InvalidEmail:
                return "Invalid email";
            case AuthError.MissingEmail:
                return "Missing email";
            case AuthError.MissingPassword:
                return "Missing password";
            case AuthError.WeakPassword:
                return "Weak password";
            case AuthError.EmailAlreadyInUse:
                return "Email already in use";
            case AuthError.WrongPassword:
                return "Wrong password";
            case AuthError.UserNotFound:
                return "User not found";
            case AuthError.InvalidCredential:
                return "Invalid credentials";
            default:
                return "Firebase error: " + errorCode;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log(message);
    }
}