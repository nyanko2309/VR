using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EmailAuthManager : MonoBehaviour
{
    private const string TAG = "[Firebase] ";

    [Header("UI")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_Text statusText;

    [Header("Navigation")]
    public string nextSceneName = "Menu";

    [Header("Debug")]
    public int firebaseInitTimeoutSeconds = 10;

    private FirebaseAuth auth;
    private bool firebaseReady = false;

    private void Awake()
    {
        Debug.Log(TAG + "Awake called on object: " + gameObject.name + " | Instance ID: " + GetInstanceID());
    }

    private async void Start()
    {
        SetStatus("Starting Firebase...");
        Debug.Log(TAG + "Start called. Beginning Firebase initialization.");

        try
        {
            Task<DependencyStatus> dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
            Task timeoutTask = Task.Delay(firebaseInitTimeoutSeconds * 1000);

            Task finishedTask = await Task.WhenAny(dependencyTask, timeoutTask);

            if (finishedTask == timeoutTask)
            {
                firebaseReady = false;
                auth = null;

                SetStatus("Firebase init timeout - check Console");
                Debug.LogError(TAG + "Firebase initialization timed out after " + firebaseInitTimeoutSeconds + " seconds.");
                return;
            }

            DependencyStatus dependencyStatus = dependencyTask.Result;

            Debug.Log(TAG + "Firebase dependency status: " + dependencyStatus);

            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                firebaseReady = true;

                SetStatus("Firebase ready");
                Debug.Log(TAG + "Firebase Auth initialized successfully.");
            }
            else
            {
                firebaseReady = false;
                auth = null;

                SetStatus("Firebase error: " + dependencyStatus);
                Debug.LogError(TAG + "Firebase dependency error: " + dependencyStatus);
            }
        }
        catch (System.Exception ex)
        {
            firebaseReady = false;
            auth = null;

            SetStatus("Firebase exception - check Console");
            Debug.LogError(TAG + "Firebase initialization exception: " + ex);
        }
    }

    public void LoginUser()
    {
        Debug.Log(TAG + "LoginUser clicked on object: " + gameObject.name + " | Instance ID: " + GetInstanceID());
        Debug.Log(TAG + "firebaseReady = " + firebaseReady + " | auth is null = " + (auth == null));

        if (!firebaseReady || auth == null)
        {
            SetStatus("Firebase not ready yet");
            Debug.LogWarning(TAG + "Login blocked because Firebase is not ready.");
            return;
        }

        string email = emailInput != null ? emailInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text : "";

        Debug.Log(TAG + "Login attempt for email: " + email);

        if (!ValidateInput(email, password))
            return;

        SetStatus("Logging in...");

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            UnityMainThreadDispatcher.RunOnMainThread(() =>
            {
                if (task.IsCanceled)
                {
                    SetStatus("Login canceled");
                    Debug.LogWarning(TAG + "Login task was canceled.");
                    return;
                }

                if (task.IsFaulted)
                {
                    string errorMessage = GetErrorMessage(task.Exception);
                    SetStatus(errorMessage);

                    Debug.LogError(TAG + "Login failed: " + errorMessage);
                    Debug.LogError(TAG + "Full login exception: " + task.Exception);
                    return;
                }

                FirebaseUser user = task.Result.User;

                SetStatus("Login success: " + user.Email);
                Debug.Log(TAG + "Login successful. User email: " + user.Email);
                Debug.Log(TAG + "User ID: " + user.UserId);

                if (!string.IsNullOrEmpty(nextSceneName))
                {
                    Debug.Log(TAG + "Loading scene: " + nextSceneName);
                    SceneManager.LoadScene(nextSceneName);
                }
                else
                {
                    Debug.LogWarning(TAG + "nextSceneName is empty. Scene will not change.");
                }
            });
        });
    }

    public void RegisterUser()
    {
        Debug.Log(TAG + "RegisterUser clicked on object: " + gameObject.name + " | Instance ID: " + GetInstanceID());
        Debug.Log(TAG + "firebaseReady = " + firebaseReady + " | auth is null = " + (auth == null));

        if (!firebaseReady || auth == null)
        {
            SetStatus("Firebase not ready yet");
            Debug.LogWarning(TAG + "Register blocked because Firebase is not ready.");
            return;
        }

        string email = emailInput != null ? emailInput.text.Trim() : "";
        string password = passwordInput != null ? passwordInput.text : "";

        Debug.Log(TAG + "Register attempt for email: " + email);

        if (!ValidateInput(email, password))
            return;

        SetStatus("Registering...");

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            UnityMainThreadDispatcher.RunOnMainThread(() =>
            {
                if (task.IsCanceled)
                {
                    SetStatus("Registration canceled");
                    Debug.LogWarning(TAG + "Registration task was canceled.");
                    return;
                }

                if (task.IsFaulted)
                {
                    string errorMessage = GetErrorMessage(task.Exception);
                    SetStatus(errorMessage);

                    Debug.LogError(TAG + "Registration failed: " + errorMessage);
                    Debug.LogError(TAG + "Full registration exception: " + task.Exception);
                    return;
                }

                FirebaseUser user = task.Result.User;

                SetStatus("Registration success: " + user.Email);
                Debug.Log(TAG + "Registration successful. User email: " + user.Email);
                Debug.Log(TAG + "User ID: " + user.UserId);

                if (!string.IsNullOrEmpty(nextSceneName))
                {
                    Debug.Log(TAG + "Loading scene: " + nextSceneName);
                    SceneManager.LoadScene(nextSceneName);
                }
                else
                {
                    Debug.LogWarning(TAG + "nextSceneName is empty. Scene will not change.");
                }
            });
        });
    }

    private bool ValidateInput(string email, string password)
    {
        if (emailInput == null)
        {
            SetStatus("Email input is missing");
            Debug.LogError(TAG + "emailInput is not assigned in the Inspector.");
            return false;
        }

        if (passwordInput == null)
        {
            SetStatus("Password input is missing");
            Debug.LogError(TAG + "passwordInput is not assigned in the Inspector.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            SetStatus("Please enter email");
            Debug.LogWarning(TAG + "Validation failed: email is empty.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Please enter password");
            Debug.LogWarning(TAG + "Validation failed: password is empty.");
            return false;
        }

        if (password.Length < 6)
        {
            SetStatus("Password must be at least 6 characters");
            Debug.LogWarning(TAG + "Validation failed: password is shorter than 6 characters.");
            return false;
        }

        Debug.Log(TAG + "Input validation passed.");
        return true;
    }

    private string GetErrorMessage(System.AggregateException exception)
    {
        if (exception == null)
        {
            Debug.LogError(TAG + "Firebase exception is null.");
            return "Unknown error";
        }

        System.Exception baseException = exception.GetBaseException();

        Debug.LogError(TAG + "Base Firebase exception: " + baseException);

        FirebaseException firebaseException = baseException as FirebaseException;

        if (firebaseException == null)
        {
            return "Firebase authentication error";
        }

        AuthError errorCode = (AuthError)firebaseException.ErrorCode;

        Debug.LogError(TAG + "Firebase Auth error code: " + errorCode);

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

            case AuthError.NetworkRequestFailed:
                return "Network error";

            default:
                return "Firebase error: " + errorCode;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.ForceMeshUpdate();
        }
        else
        {
            Debug.LogError(TAG + "statusText is not assigned in the Inspector.");
        }

        Debug.Log(TAG + "Status: " + message);
    }
}

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly System.Collections.Generic.Queue<System.Action> actions =
        new System.Collections.Generic.Queue<System.Action>();

    private static UnityMainThreadDispatcher instance;

    public static void RunOnMainThread(System.Action action)
    {
        if (action == null)
            return;

        lock (actions)
        {
            actions.Enqueue(action);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
            {
                actions.Dequeue().Invoke();
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null)
            return;

        GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
        instance = dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
        DontDestroyOnLoad(dispatcherObject);
    }
}