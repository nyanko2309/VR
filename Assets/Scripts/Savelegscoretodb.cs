using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class RehabDataManager : MonoBehaviour
{
    public static RehabDataManager Instance { get; private set; }

    [Header("Testing")]
    public bool useTestUserIfNoLogin = true;
    public string testUserId = "ampcTUbGF3edyN95CG7UrEq3Ask2";

    private FirebaseFirestore _db;
    private FirebaseAuth _auth;

    private string _userId = "";
    private bool _ready = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                _db = FirebaseFirestore.DefaultInstance;
                _auth = FirebaseAuth.DefaultInstance;
                _ready = true;

                UpdateCurrentUserId();

                _auth.StateChanged += OnAuthStateChanged;

                Debug.Log("[RehabDataManager] Firestore ready. userId: " + _userId);
            }
            else
            {
                Debug.LogError("[RehabDataManager] Firebase dependency error: " + task.Result);
            }
        });
    }

    private void OnDestroy()
    {
        try
        {
            if (_auth != null)
                _auth.StateChanged -= OnAuthStateChanged;
        }
        catch
        {
        }
    }

    private void OnAuthStateChanged(object sender, EventArgs e)
    {
        UpdateCurrentUserId();
        Debug.Log("[RehabDataManager] Auth state changed. userId: " + _userId);
    }

    private void UpdateCurrentUserId()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;

        if (user != null)
        {
            _userId = user.UserId;

            PlayerPrefs.SetString("LoggedInUserId", user.UserId);
            PlayerPrefs.SetString("LoggedInUserEmail", user.Email ?? "");
            PlayerPrefs.Save();

            return;
        }

        string savedUserId = PlayerPrefs.GetString("LoggedInUserId", "");

        if (!string.IsNullOrWhiteSpace(savedUserId))
        {
            _userId = savedUserId;
            return;
        }

        if (useTestUserIfNoLogin && !string.IsNullOrWhiteSpace(testUserId))
        {
            _userId = testUserId;

            PlayerPrefs.SetString("LoggedInUserId", testUserId);
            PlayerPrefs.SetString("LoggedInUserEmail", "test-user");
            PlayerPrefs.Save();

            Debug.LogWarning("[RehabDataManager] No logged-in user. Using test user id: " + testUserId);
            return;
        }

        _userId = "";
    }

    public void SaveSession(SessionResult session)
    {
        if (!_ready || _db == null)
        {
            Debug.LogWarning("[RehabDataManager] Not ready — session not saved.");
            return;
        }

        if (session == null)
        {
            Debug.LogWarning("[RehabDataManager] Session is null — session not saved.");
            return;
        }

        UpdateCurrentUserId();

        if (string.IsNullOrWhiteSpace(_userId))
        {
            Debug.LogWarning("[RehabDataManager] No user id available — session not saved.");
            return;
        }

        session.userId = _userId;

        if (string.IsNullOrWhiteSpace(session.sessionId))
            session.sessionId = Guid.NewGuid().ToString();

        if (string.IsNullOrWhiteSpace(session.date))
            session.date = DateTime.Now.ToString("yyyy-MM-dd");

        List<object> phraseDocs = new List<object>();

        if (session.phrases != null)
        {
            foreach (PhraseResult p in session.phrases)
            {
                phraseDocs.Add(new Dictionary<string, object>
                {
                    ["phraseType"] = p.phraseType ?? "",
                    ["totalReps"] = p.totalReps,
                    ["successfulReps"] = p.successfulReps,
                    ["averageScore"] = (double)p.averageScore,
                    ["peakRomUtilization"] = (double)p.peakRomUtilization,
                    ["longestStreak"] = p.longestStreak,
                    ["timestampMs"] = p.timestampMs
                });
            }
        }

        long durationMs = session.endTimestampMs - session.startTimestampMs;

        if (durationMs < 0)
            durationMs = 0;

        Dictionary<string, object> doc = new Dictionary<string, object>
        {
            ["sessionId"] = session.sessionId ?? "",
            ["userId"] = session.userId ?? "",
            ["date"] = session.date ?? "",

            ["startTimestampMs"] = session.startTimestampMs,
            ["endTimestampMs"] = session.endTimestampMs,
            ["durationMs"] = durationMs,

            ["totalPhrases"] = session.totalPhrases,
            ["totalReps"] = session.totalReps,
            ["successfulReps"] = session.successfulReps,
            ["overallSuccessRate"] = (double)session.overallSuccessRate,
            ["longestSuccessStreak"] = session.longestSuccessStreak,

            ["peakRomUtilization"] = (double)session.peakRomUtilization,
            ["averageRomUtilization"] = (double)session.averageRomUtilization,

            ["calibVertRangeM"] = (double)session.calibVertRangeM,
            ["calibLatRangeM"] = (double)session.calibLatRangeM,
            ["vertRomHitM"] = (double)session.vertRomHitM,
            ["latRomHitM"] = (double)session.latRomHitM,

            ["formScore"] = (double)session.formScore,
            ["formGrade"] = session.formGrade ?? "",
            ["difficultyLevel"] = session.difficultyLevel,

            ["highReachAttempts"] = session.highReachAttempts,
            ["highReachSuccesses"] = session.highReachSuccesses,
            ["highReachRate"] = (double)session.highReachRate,

            ["wideSpreadAttempts"] = session.wideSpreadAttempts,
            ["wideSpreadSuccesses"] = session.wideSpreadSuccesses,
            ["wideSpreadRate"] = (double)session.wideSpreadRate,

            ["phrases"] = phraseDocs
        };

        string sessionDocId = _userId + "_" + session.sessionId;

        _db.Collection("sessions")
            .Document(sessionDocId)
            .SetAsync(doc)
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.LogError("[RehabDataManager] Session write failed: " +
                                   t.Exception?.GetBaseException()?.Message);
                }
                else
                {
                    Debug.Log("[RehabDataManager] Session saved → sessions/" + sessionDocId);
                }
            });

        string romDocId = _userId + "_" + session.date;

        Dictionary<string, object> romEntry = new Dictionary<string, object>
        {
            ["userId"] = _userId,
            ["date"] = session.date ?? "",

            ["peakRomUtil"] = (double)session.peakRomUtilization,
            ["avgRomUtil"] = (double)session.averageRomUtilization,
            ["successRate"] = (double)session.overallSuccessRate,
            ["longestStreak"] = session.longestSuccessStreak,

            ["formScore"] = (double)session.formScore,
            ["formGrade"] = session.formGrade ?? "",
            ["difficultyLevel"] = session.difficultyLevel,

            ["totalReps"] = session.totalReps,
            ["successfulReps"] = session.successfulReps,

            ["calibVertRangeM"] = (double)session.calibVertRangeM,
            ["calibLatRangeM"] = (double)session.calibLatRangeM,
            ["vertRomHitM"] = (double)session.vertRomHitM,
            ["latRomHitM"] = (double)session.latRomHitM,

            ["highReachRate"] = (double)session.highReachRate,
            ["highReachAtt"] = session.highReachAttempts,

            ["wideSpreadRate"] = (double)session.wideSpreadRate,
            ["wideSpreadAtt"] = session.wideSpreadAttempts
        };

        _db.Collection("rom_progress")
            .Document(romDocId)
            .SetAsync(romEntry)
            .ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.LogError("[RehabDataManager] ROM progress write failed: " +
                                   t.Exception?.GetBaseException()?.Message);
                }
                else
                {
                    Debug.Log("[RehabDataManager] ROM progress saved → rom_progress/" + romDocId);
                }
            });
    }

    public void LoadRomProgress(int days, Action<List<RomProgressEntry>> callback)
    {
        if (!_ready || _db == null)
        {
            Debug.LogWarning("[RehabDataManager] Not ready — cannot load ROM progress.");
            callback?.Invoke(new List<RomProgressEntry>());
            return;
        }

        UpdateCurrentUserId();

        if (string.IsNullOrWhiteSpace(_userId))
        {
            Debug.LogWarning("[RehabDataManager] No user id — cannot load ROM progress.");
            callback?.Invoke(new List<RomProgressEntry>());
            return;
        }

        _db.Collection("rom_progress")
            .WhereEqualTo("userId", _userId)
            .Limit(days)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                List<RomProgressEntry> list = new List<RomProgressEntry>();

                if (task.IsFaulted || task.Result == null)
                {
                    Debug.LogError("[RehabDataManager] LoadRomProgress failed: " +
                                   task.Exception?.GetBaseException()?.Message);

                    callback?.Invoke(list);
                    return;
                }

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    RomProgressEntry e = new RomProgressEntry
                    {
                        date = doc.ContainsField("date") ? doc.GetValue<string>("date") : "",

                        peakRomUtil = doc.ContainsField("peakRomUtil") ? ConvertToFloat(doc.GetValue<object>("peakRomUtil")) : 0f,
                        avgRomUtil = doc.ContainsField("avgRomUtil") ? ConvertToFloat(doc.GetValue<object>("avgRomUtil")) : 0f,
                        successRate = doc.ContainsField("successRate") ? ConvertToFloat(doc.GetValue<object>("successRate")) : 0f,

                        longestStreak = doc.ContainsField("longestStreak") ? ConvertToInt(doc.GetValue<object>("longestStreak")) : 0,

                        formGrade = doc.ContainsField("formGrade") ? doc.GetValue<string>("formGrade") : "—",
                        difficultyLevel = doc.ContainsField("difficultyLevel") ? ConvertToInt(doc.GetValue<object>("difficultyLevel")) : 1,

                        calibVertRangeM = doc.ContainsField("calibVertRangeM") ? ConvertToFloat(doc.GetValue<object>("calibVertRangeM")) : 0f,
                        calibLatRangeM = doc.ContainsField("calibLatRangeM") ? ConvertToFloat(doc.GetValue<object>("calibLatRangeM")) : 0f,
                        vertRomHitM = doc.ContainsField("vertRomHitM") ? ConvertToFloat(doc.GetValue<object>("vertRomHitM")) : 0f,
                        latRomHitM = doc.ContainsField("latRomHitM") ? ConvertToFloat(doc.GetValue<object>("latRomHitM")) : 0f,
                    };

                    list.Add(e);
                }

                list = list
                    .OrderByDescending(e => e.date)
                    .Take(days)
                    .ToList();

                Debug.Log("[RehabDataManager] Loaded " + list.Count + " ROM progress entries");

                callback?.Invoke(list);
            });
    }

    private static float ConvertToFloat(object value)
    {
        if (value == null)
            return 0f;

        try
        {
            if (value is float f)
                return f;

            if (value is double d)
                return (float)d;

            if (value is int i)
                return i;

            if (value is long l)
                return l;

            return Convert.ToSingle(value);
        }
        catch
        {
            return 0f;
        }
    }

    private static int ConvertToInt(object value)
    {
        if (value == null)
            return 0;

        try
        {
            if (value is int i)
                return i;

            if (value is long l)
                return (int)l;

            if (value is double d)
                return Mathf.RoundToInt((float)d);

            if (value is float f)
                return Mathf.RoundToInt(f);

            return Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }
}

[Serializable]
public class RomProgressEntry
{
    public string date;

    public float peakRomUtil;
    public float avgRomUtil;
    public float successRate;

    public int longestStreak;

    public string formGrade;
    public int difficultyLevel;

    public float calibVertRangeM;
    public float calibLatRangeM;
    public float vertRomHitM;
    public float latRomHitM;
}