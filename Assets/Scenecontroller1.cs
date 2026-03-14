using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Google;

public class SceneController : MonoBehaviour
{
    [Header("Google Sign-In")]
    [SerializeField] private string webClientId = "631638620593-ievlldo6umld0oqd6iu9oofkt7sq0tlo.apps.googleusercontent.com";

    private FirebaseAuth auth;
    private GoogleSignInConfiguration configuration;

    private void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            DependencyStatus dependencyStatus = task.Result;
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError("Firebase dependencies not available: " + dependencyStatus);
                return;
            }

            auth = FirebaseAuth.DefaultInstance;

            configuration = new GoogleSignInConfiguration
            {
                WebClientId = webClientId,
                RequestIdToken = true,
                RequestEmail = true
            };

            GoogleSignIn.Configuration = configuration;

            Debug.Log("Firebase and Google Sign-In are ready.");
        });
    }

    public void ChangeScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void OnGoogleSignInClicked()
    {
        Debug.Log("STEP 1 - Button clicked");

        if (auth == null)
        {
            Debug.LogError("STEP 2 - FirebaseAuth is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(webClientId) || webClientId == "PUT_YOUR_WEB_CLIENT_ID_HERE")
        {
            Debug.LogError("STEP 3 - Web Client ID is missing");
            return;
        }

        Debug.Log("STEP 4 - Starting Google Sign-In");

        GoogleSignIn.Configuration = configuration;
        GoogleSignIn.DefaultInstance.SignIn().ContinueWithOnMainThread(OnGoogleAuthenticated);
    }

    private void OnGoogleAuthenticated(System.Threading.Tasks.Task<GoogleSignInUser> task)
    {
        if (task.IsCanceled)
        {
            Debug.LogWarning("Google Sign-In was canceled.");
            return;
        }

        if (task.IsFaulted)
        {
            Debug.LogError("Google Sign-In failed: " + task.Exception);
            return;
        }

        GoogleSignInUser googleUser = task.Result;

        if (googleUser == null || string.IsNullOrEmpty(googleUser.IdToken))
        {
            Debug.LogError("Google Sign-In succeeded but IdToken is missing.");
            return;
        }

        Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(authTask =>
        {
            if (authTask.IsCanceled)
            {
                Debug.LogWarning("Firebase sign-in was canceled.");
                return;
            }

            if (authTask.IsFaulted)
            {
                Debug.LogError("Firebase sign-in failed: " + authTask.Exception);
                return;
            }

            FirebaseUser user = authTask.Result;
            Debug.Log("Firebase sign-in successful. User: " + user.DisplayName + " | " + user.Email);
        });
    }
}