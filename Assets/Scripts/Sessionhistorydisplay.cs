using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;
using Debug = UnityEngine.Debug;

public class SessionHistoryDisplay : MonoBehaviour
{
    [Header("Test User")]
    [Tooltip("Fallback test user ID — used only if no Firebase user is logged in")]
    public string testUserId = "ampcTUbGF3edyN95CG7UrEq3Ask2";

    [Header("How many sessions to load")]
    public int maxSessions = 10;

    [Header("Session Card List")]
    [Tooltip("Parent RectTransform that holds the session card prefabs")]
    public RectTransform cardContainer;

    public GameObject sessionCardPrefab;

    [Header("Graph Bars")]
    [Tooltip("Parent that holds bar GameObjects")]
    public RectTransform graphContainer;

    public GameObject barPrefab;

    [Tooltip("Max bar height in pixels")]
    public float maxBarHeight = 200f;

    [Tooltip("Color for vertical ROM bars")]
    public Color romBarColor = new Color(0.20f, 0.85f, 0.40f);

    public Color successBarColor = new Color(0.20f, 0.60f, 1.00f);

    [Header("Summary Text")]
    public TextMeshProUGUI summaryText;
    public TextMeshProUGUI loadingText;

    private FirebaseFirestore _db;
    private List<SessionSummary> _sessions = new List<SessionSummary>();

    private class SessionSummary
    {
        public string date;
        public float vertRomCm;
        public float latRomCm;
        public float successRate;
        public int totalReps;
        public int successfulReps;
        public string formGrade;
        public long timestampMs;
    }

    void Start()
    {
        SetLoading(true);

        Debug.Log("[history] Starting Firebase dependency check...");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                string error = task.Exception != null
                    ? task.Exception.GetBaseException().Message
                    : "Unknown Firebase dependency error";

                ShowError("Firebase dependency check failed:\n" + error);
                return;
            }

            DependencyStatus status = task.Result;

            Debug.Log("[history] Firebase dependency status: " + status);

            if (status != DependencyStatus.Available)
            {
                ShowError("Firebase unavailable:\n" + status);
                return;
            }

            try
            {
                _db = FirebaseFirestore.DefaultInstance;
            }
            catch (Exception e)
            {
                ShowError("Firestore initialization failed:\n" + e.Message);
                return;
            }

            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;

            if (user != null)
            {
                testUserId = user.UserId;
                Debug.Log("[history] Using logged-in user ID: " + testUserId);
            }
            else
            {
                Debug.LogWarning("[history] No logged-in user found. Using fallback testUserId: " + testUserId);
            }

            LoadSessions();
        });
    }

    void LoadSessions()
    {
        if (_db == null)
        {
            ShowError("Firestore database is not initialized.");
            return;
        }

        Debug.Log("[history] Loading sessions for user: " + testUserId);

        _db.Collection("sessions")
           .WhereEqualTo("userId", testUserId)
           .GetSnapshotAsync()
           .ContinueWithOnMainThread(task =>
           {
               if (task.IsFaulted)
               {
                   string error = task.Exception != null
                       ? task.Exception.GetBaseException().Message
                       : "Unknown Firestore load error";

                   ShowError("Load failed:\n" + error);
                   return;
               }

               if (task.IsCanceled)
               {
                   ShowError("Load canceled.");
                   return;
               }

               if (task.Result == null)
               {
                   ShowError("Firestore returned null result.");
                   return;
               }

               _sessions.Clear();

               foreach (DocumentSnapshot doc in task.Result.Documents)
               {
                   try
                   {
                       SessionSummary s = new SessionSummary();

                       s.date = GetStringSafe(doc, "date", "—");
                       s.successRate = GetFloatSafe(doc, "overallSuccessRate", 0f);
                       s.totalReps = GetIntSafe(doc, "totalReps", 0);
                       s.successfulReps = GetIntSafe(doc, "successfulReps", 0);
                       s.formGrade = GetStringSafe(doc, "formGrade", "—");
                       s.timestampMs = GetLongSafe(doc, "startTimestampMs", 0);

                       float peak = GetFloatSafe(doc, "peakRomUtilization", 0f);
                       float wide = GetFloatSafe(doc, "wideSpreadRate", 0f);

                       s.vertRomCm = peak * 100f;
                       s.latRomCm = wide * 100f;

                       _sessions.Add(s);
                   }
                   catch (Exception e)
                   {
                       Debug.LogWarning("[history] Failed to parse session doc: " + e.Message);
                   }
               }

               _sessions = _sessions
                   .OrderByDescending(s => s.timestampMs)
                   .Take(maxSessions)
                   .ToList();

               SetLoading(false);

               if (_sessions.Count == 0)
               {
                   if (loadingText != null)
                   {
                       loadingText.text = "No sessions found yet.\nComplete a game session first!";
                       loadingText.gameObject.SetActive(true);
                   }

                   return;
               }

               BuildCards();
               BuildGraph();
               BuildSummary();
           });
    }

    void BuildCards()
    {
        if (cardContainer == null || sessionCardPrefab == null)
        {
            Debug.LogWarning("[history] Missing cardContainer or sessionCardPrefab.");
            return;
        }

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (SessionSummary s in _sessions)
        {
            GameObject card = Instantiate(sessionCardPrefab, cardContainer);

            SetChildText(card, "DateText", s.date);
            SetChildText(card, "ScoreText", "Grade: " + s.formGrade + "   Hit: " + s.successfulReps + "/" + s.totalReps + " (" + FormatPercent(s.successRate) + ")");
            SetChildText(card, "RomText", "ROM  ↕ " + s.vertRomCm.ToString("F0") + " cm   ↔ " + s.latRomCm.ToString("F0") + " cm");
        }
    }

    void BuildGraph()
    {
        if (graphContainer == null || barPrefab == null)
        {
            Debug.LogWarning("[history] Missing graphContainer or barPrefab.");
            return;
        }

        foreach (Transform child in graphContainer)
        {
            Destroy(child.gameObject);
        }

        List<SessionSummary> ordered = _sessions.OrderBy(s => s.timestampMs).ToList();

        if (ordered.Count == 0)
            return;

        float maxRom = ordered.Max(s => s.vertRomCm);

        if (maxRom <= 0f)
            maxRom = 1f;

        foreach (SessionSummary s in ordered)
        {
            GameObject bar = Instantiate(barPrefab, graphContainer);

            UIImage img = bar.GetComponent<UIImage>();

            if (img != null)
            {
                img.color = romBarColor;
            }

            float heightFraction = Mathf.Clamp01(s.vertRomCm / maxRom);

            RectTransform rt = bar.GetComponent<RectTransform>();

            if (rt != null)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, maxBarHeight * heightFraction);
                rt.anchorMin = new Vector2(rt.anchorMin.x, 0f);
                rt.anchorMax = new Vector2(rt.anchorMax.x, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
            }

            SetChildText(bar, "DateLabel", GetShortDate(s.date));
        }
    }

    void BuildSummary()
    {
        if (summaryText == null || _sessions.Count == 0)
            return;

        float avgSuccess = _sessions.Average(s => s.successRate);
        float avgRom = _sessions.Average(s => s.vertRomCm);
        float peakRom = _sessions.Max(s => s.vertRomCm);
        int total = _sessions.Count;

        string trend = "—";

        if (total >= 4)
        {
            List<SessionSummary> newestFirst = _sessions.OrderByDescending(s => s.timestampMs).ToList();

            int half = total / 2;

            float recentAvg = newestFirst.Take(half).Average(s => s.vertRomCm);
            float olderAvg = newestFirst.Skip(half).Take(half).Average(s => s.vertRomCm);

            float change = recentAvg - olderAvg;

            if (change > 2f)
                trend = "Improving";
            else if (change < -2f)
                trend = "Declining";
            else
                trend = "Stable";
        }

        summaryText.text =
            "Last " + total + " sessions\n\n" +
            "Avg success rate:  " + FormatPercent(avgSuccess) + "\n" +
            "Avg vertical ROM:  " + avgRom.ToString("F0") + " cm\n" +
            "Peak vertical ROM: " + peakRom.ToString("F0") + " cm\n\n" +
            "Trend: " + trend;
    }

    void SetChildText(GameObject parent, string childName, string text)
    {
        if (parent == null)
            return;

        Transform t = parent.transform.Find(childName);

        if (t == null)
        {
            Debug.LogWarning("[history] Missing child text object: " + childName);
            return;
        }

        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();

        if (tmp != null)
        {
            tmp.text = text;
        }
    }

    void SetLoading(bool on)
    {
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(on);

            if (on)
            {
                loadingText.text = "Loading sessions...";
            }
        }

        if (cardContainer != null)
            cardContainer.gameObject.SetActive(!on);

        if (graphContainer != null)
            graphContainer.gameObject.SetActive(!on);

        if (summaryText != null)
            summaryText.gameObject.SetActive(!on);
    }

    void ShowError(string msg)
    {
        SetLoading(false);

        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(true);
            loadingText.text = msg;
        }

        Debug.LogError("[history] " + msg);
    }

    private string GetStringSafe(DocumentSnapshot doc, string field, string fallback)
    {
        if (doc == null || !doc.ContainsField(field))
            return fallback;

        try
        {
            return doc.GetValue<string>(field);
        }
        catch
        {
            return fallback;
        }
    }

    private int GetIntSafe(DocumentSnapshot doc, string field, int fallback)
    {
        if (doc == null || !doc.ContainsField(field))
            return fallback;

        try
        {
            object value = doc.GetValue<object>(field);

            if (value is int)
                return (int)value;

            if (value is long)
                return Convert.ToInt32((long)value);

            if (value is double)
                return Convert.ToInt32((double)value);

            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private long GetLongSafe(DocumentSnapshot doc, string field, long fallback)
    {
        if (doc == null || !doc.ContainsField(field))
            return fallback;

        try
        {
            object value = doc.GetValue<object>(field);

            if (value is long)
                return (long)value;

            if (value is int)
                return (int)value;

            if (value is double)
                return Convert.ToInt64((double)value);

            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private float GetFloatSafe(DocumentSnapshot doc, string field, float fallback)
    {
        if (doc == null || !doc.ContainsField(field))
            return fallback;

        try
        {
            object value = doc.GetValue<object>(field);

            if (value is float)
                return (float)value;

            if (value is double)
                return Convert.ToSingle((double)value);

            if (value is int)
                return (int)value;

            if (value is long)
                return Convert.ToSingle((long)value);

            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private string GetShortDate(string date)
    {
        if (string.IsNullOrEmpty(date))
            return "—";

        if (date.Length >= 10)
            return date.Substring(5, 5);

        if (date.Length >= 5)
            return date.Substring(date.Length - 5);

        return date;
    }

    private string FormatPercent(float value)
    {
        return (value * 100f).ToString("F0") + "%";
    }
}