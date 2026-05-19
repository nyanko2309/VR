using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

// ─────────────────────────────────────────────────────────────────────────────
//  RehabDataManager
//  Singleton – place on a DontDestroyOnLoad GameObject.
//  PhysioBallGenerator calls SaveSession() at the end of every session.
//
//  Firestore structure:
//    sessions/{uid}_{sessionId}    ← full session document
//    rom_progress/{uid}_{date}     ← lightweight time-series per user per day
// ─────────────────────────────────────────────────────────────────────────────

public class RehabDataManager : MonoBehaviour
{
    public static RehabDataManager Instance { get; private set; }

    private FirebaseFirestore _db;
    private string _userId = "anonymous";
    private bool _ready = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                _db = FirebaseFirestore.DefaultInstance;
                _ready = true;

                var user = FirebaseAuth.DefaultInstance.CurrentUser;
                if (user != null) _userId = user.UserId;

                FirebaseAuth.DefaultInstance.StateChanged += OnAuthStateChanged;
                Debug.Log("[RehabDataManager] Firestore ready");
            }
            else
            {
                Debug.LogError("[RehabDataManager] Firebase dependency error: " + task.Result);
            }
        });
    }

    void OnDestroy()
    {
        try { FirebaseAuth.DefaultInstance.StateChanged -= OnAuthStateChanged; }
        catch { }
    }

    void OnAuthStateChanged(object sender, EventArgs e)
    {
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        _userId = user != null ? user.UserId : "anonymous";
        Debug.Log($"[RehabDataManager] Auth state changed — userId: {_userId}");
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Save session
    // ─────────────────────────────────────────────────────────────────────

    public void SaveSession(SessionResult session)
    {
        session.userId = _userId;

        if (!_ready || _db == null)
        {
            Debug.LogWarning("[RehabDataManager] Not ready — session not saved.");
            return;
        }

        // Convert phrases list
        var phraseDocs = new List<object>();
        if (session.phrases != null)
        {
            foreach (var p in session.phrases)
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

        var doc = new Dictionary<string, object>
        {
            // ── Identity ──────────────────────────────────────────────
            ["sessionId"] = session.sessionId ?? "",
            ["userId"] = session.userId ?? "",
            ["date"] = session.date ?? "",
            ["startTimestampMs"] = session.startTimestampMs,
            ["endTimestampMs"] = session.endTimestampMs,
            ["durationMs"] = durationMs,              // NEW: real session duration

            // ── Rep counts ────────────────────────────────────────────
            ["totalPhrases"] = session.totalPhrases,
            ["totalReps"] = session.totalReps,
            ["successfulReps"] = session.successfulReps,
            ["overallSuccessRate"] = (double)session.overallSuccessRate,
            ["longestSuccessStreak"] = session.longestSuccessStreak,

            // ── ROM — utilisation (fraction of calibrated range) ──────
            ["peakRomUtilization"] = (double)session.peakRomUtilization,
            ["averageRomUtilization"] = (double)session.averageRomUtilization,

            // ── ROM — absolute metres (NEW) ───────────────────────────
            ["calibVertRangeM"] = (double)session.calibVertRangeM,
            ["calibLatRangeM"] = (double)session.calibLatRangeM,
            ["vertRomHitM"] = (double)session.vertRomHitM,
            ["latRomHitM"] = (double)session.latRomHitM,

            // ── Form ──────────────────────────────────────────────────
            ["formScore"] = (double)session.formScore,
            ["formGrade"] = session.formGrade ?? "",
            ["difficultyLevel"] = session.difficultyLevel,

            // ── Per-exercise (knee raises = highReach, extensions = wideSpread) ──
            ["highReachAttempts"] = session.highReachAttempts,
            ["highReachSuccesses"] = session.highReachSuccesses,
            ["highReachRate"] = (double)session.highReachRate,
            ["wideSpreadAttempts"] = session.wideSpreadAttempts,
            ["wideSpreadSuccesses"] = session.wideSpreadSuccesses,
            ["wideSpreadRate"] = (double)session.wideSpreadRate,

            ["phrases"] = phraseDocs
        };

        // ── Write full session doc ────────────────────────────────────────
        string sessionDocId = $"{_userId}_{session.sessionId}";
        _db.Collection("sessions")
           .Document(sessionDocId)
           .SetAsync(doc)
           .ContinueWithOnMainThread(t =>
           {
               if (t.IsFaulted)
                   Debug.LogError("[RehabDataManager] Session write failed: "
                                  + t.Exception?.GetBaseException()?.Message);
               else
                   Debug.Log($"[RehabDataManager] Session saved → sessions/{sessionDocId}");
           });

        // ── Write lightweight ROM progress entry ──────────────────────────
        // Uses SetAsync with merge so multiple sessions on the same day keep the best values
        string romDocId = $"{_userId}_{session.date}";
        var romEntry = new Dictionary<string, object>
        {
            ["userId"] = _userId,
            ["date"] = session.date ?? "",

            // utilisation fractions
            ["peakRomUtil"] = (double)session.peakRomUtilization,
            ["avgRomUtil"] = (double)session.averageRomUtilization,
            ["successRate"] = (double)session.overallSuccessRate,
            ["longestStreak"] = session.longestSuccessStreak,
            ["formScore"] = (double)session.formScore,
            ["formGrade"] = session.formGrade ?? "",
            ["difficultyLevel"] = session.difficultyLevel,
            ["totalReps"] = session.totalReps,
            ["successfulReps"] = session.successfulReps,

            // absolute ROM metres (NEW — useful for physio dashboard)
            ["calibVertRangeM"] = (double)session.calibVertRangeM,
            ["calibLatRangeM"] = (double)session.calibLatRangeM,
            ["vertRomHitM"] = (double)session.vertRomHitM,
            ["latRomHitM"] = (double)session.latRomHitM,

            // per-exercise rates
            ["highReachRate"] = (double)session.highReachRate,
            ["highReachAtt"] = session.highReachAttempts,
            ["wideSpreadRate"] = (double)session.wideSpreadRate,
            ["wideSpreadAtt"] = session.wideSpreadAttempts,
        };

        _db.Collection("rom_progress")
           .Document(romDocId)
           .SetAsync(romEntry)
           .ContinueWithOnMainThread(t =>
           {
               if (t.IsFaulted)
                   Debug.LogError("[RehabDataManager] ROM progress write failed: "
                                  + t.Exception?.GetBaseException()?.Message);
               else
                   Debug.Log($"[RehabDataManager] ROM progress saved → rom_progress/{romDocId}");
           });
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Load ROM progress (last N days)
    // ─────────────────────────────────────────────────────────────────────

    public void LoadRomProgress(int days, Action<List<RomProgressEntry>> callback)
    {
        if (!_ready || _db == null)
        {
            Debug.LogWarning("[RehabDataManager] Not ready — cannot load ROM progress.");
            callback?.Invoke(new List<RomProgressEntry>());
            return;
        }

        _db.Collection("rom_progress")
           .WhereEqualTo("userId", _userId)
           .OrderByDescending("date")
           .Limit(days)
           .GetSnapshotAsync()
           .ContinueWithOnMainThread(task =>
           {
               var list = new List<RomProgressEntry>();
               if (task.IsFaulted || task.Result == null)
               {
                   Debug.LogError("[RehabDataManager] LoadRomProgress failed: "
                                  + task.Exception?.GetBaseException()?.Message);
                   callback?.Invoke(list);
                   return;
               }

               foreach (var doc in task.Result.Documents)
               {
                   var e = new RomProgressEntry
                   {
                       date = doc.ContainsField("date") ? doc.GetValue<string>("date") : "",
                       peakRomUtil = doc.ContainsField("peakRomUtil") ? (float)doc.GetValue<double>("peakRomUtil") : 0f,
                       avgRomUtil = doc.ContainsField("avgRomUtil") ? (float)doc.GetValue<double>("avgRomUtil") : 0f,
                       successRate = doc.ContainsField("successRate") ? (float)doc.GetValue<double>("successRate") : 0f,
                       longestStreak = doc.ContainsField("longestStreak") ? doc.GetValue<int>("longestStreak") : 0,
                       formGrade = doc.ContainsField("formGrade") ? doc.GetValue<string>("formGrade") : "—",
                       difficultyLevel = doc.ContainsField("difficultyLevel") ? doc.GetValue<int>("difficultyLevel") : 1,
                       // NEW absolute ROM fields
                       calibVertRangeM = doc.ContainsField("calibVertRangeM") ? (float)doc.GetValue<double>("calibVertRangeM") : 0f,
                       calibLatRangeM = doc.ContainsField("calibLatRangeM") ? (float)doc.GetValue<double>("calibLatRangeM") : 0f,
                       vertRomHitM = doc.ContainsField("vertRomHitM") ? (float)doc.GetValue<double>("vertRomHitM") : 0f,
                       latRomHitM = doc.ContainsField("latRomHitM") ? (float)doc.GetValue<double>("latRomHitM") : 0f,
                   };
                   list.Add(e);
               }

               Debug.Log($"[RehabDataManager] Loaded {list.Count} ROM progress entries");
               callback?.Invoke(list);
           });
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
    // Absolute ROM metres
    public float calibVertRangeM;
    public float calibLatRangeM;
    public float vertRomHitM;
    public float latRomHitM;
}