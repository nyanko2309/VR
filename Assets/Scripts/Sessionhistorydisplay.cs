using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class SessionHistoryDisplay : MonoBehaviour
{
    [Header("Firestore")]
    public string sessionsCollectionName = "sessions";
    public int maxSessionsToLoad = 20;

    [Header("Testing")]
    public bool useTestUserIfNoLogin = true;
    public string testUserId = "ampcTUbGF3edyN95CG7UrEq3Ask2";

    [Header("UI - Status")]
    public TMP_Text statusText;

    [Header("UI - Session List")]
    public Transform sessionListContent;
    public GameObject sessionCardPrefab;

    [Header("UI - Graph")]
    public Transform graphContent;
    public GameObject graphBarPrefab;
    public float maxBarHeight = 220f;

    private FirebaseFirestore db;
    private FirebaseAuth auth;

    private void Start()
    {
        SetStatus("Initializing Firebase...");

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                auth = FirebaseAuth.DefaultInstance;

                SetStatus("Firebase ready");
                LoadHistory();
            }
            else
            {
                SetStatus("Firebase error: " + task.Result);
                Debug.LogError("Firebase dependency error: " + task.Result);
            }
        });
    }

    public void LoadHistory()
    {
        if (db == null)
        {
            SetStatus("Firebase is not ready");
            return;
        }

        string userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(userId))
        {
            SetStatus("No user id found");
            Debug.LogWarning("[History] No user id found");
            return;
        }

        SetStatus("Loading history...");

        db.Collection(sessionsCollectionName)
            .WhereEqualTo("userId", userId)
            .Limit(maxSessionsToLoad)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled)
                {
                    SetStatus("History loading canceled");
                    return;
                }

                if (task.IsFaulted)
                {
                    SetStatus("Failed to load history");
                    Debug.LogError(task.Exception);
                    return;
                }

                QuerySnapshot snapshot = task.Result;

                List<HistorySessionData> sessions = new List<HistorySessionData>();

                foreach (DocumentSnapshot document in snapshot.Documents)
                {
                    if (!document.Exists)
                        continue;

                    Dictionary<string, object> data = document.ToDictionary();

                    HistorySessionData session = ConvertToSession(document.Id, data);
                    sessions.Add(session);
                }

                sessions = sessions
                    .OrderByDescending(s => s.timestampMs)
                    .Take(maxSessionsToLoad)
                    .ToList();

                DisplaySessions(sessions);
                DisplayGraph(sessions);

                SetStatus("Loaded " + sessions.Count + " sessions");
            });
    }

    private string GetCurrentUserId()
    {
        FirebaseUser currentUser = FirebaseAuth.DefaultInstance.CurrentUser;

        if (currentUser != null)
        {
            PlayerPrefs.SetString("LoggedInUserId", currentUser.UserId);
            PlayerPrefs.SetString("LoggedInUserEmail", currentUser.Email ?? "");
            PlayerPrefs.Save();

            Debug.Log("[History] Using real logged-in user id: " + currentUser.UserId);
            return currentUser.UserId;
        }

        string savedUserId = PlayerPrefs.GetString("LoggedInUserId", "");

        if (!string.IsNullOrWhiteSpace(savedUserId))
        {
            Debug.Log("[History] Using saved user id: " + savedUserId);
            return savedUserId;
        }

        if (useTestUserIfNoLogin && !string.IsNullOrWhiteSpace(testUserId))
        {
            PlayerPrefs.SetString("LoggedInUserId", testUserId);
            PlayerPrefs.SetString("LoggedInUserEmail", "test-user");
            PlayerPrefs.Save();

            Debug.LogWarning("[History] No logged-in user. Using test user id: " + testUserId);
            return testUserId;
        }

        return "";
    }

    private HistorySessionData ConvertToSession(string documentId, Dictionary<string, object> data)
    {
        HistorySessionData session = new HistorySessionData();

        session.sessionId = GetString(data, "sessionId", documentId);
        session.userId = GetString(data, "userId", "Unknown user");
        session.date = GetString(data, "date", "");

        session.startTimestampMs = GetLong(data, "startTimestampMs", 0);
        session.endTimestampMs = GetLong(data, "endTimestampMs", 0);

        session.timestampMs = session.startTimestampMs;

        if (session.timestampMs == 0)
            session.timestampMs = session.endTimestampMs;

        session.durationMs = GetLong(data, "durationMs", 0);

        session.totalPhrases = GetInt(data, "totalPhrases", 0);
        session.totalReps = GetInt(data, "totalReps", 0);
        session.successfulReps = GetInt(data, "successfulReps", 0);

        session.overallSuccessRate = GetFloat(data, "overallSuccessRate", 0f);
        session.longestSuccessStreak = GetInt(data, "longestSuccessStreak", 0);

        session.peakRomUtilization = GetFloat(data, "peakRomUtilization", 0f);
        session.averageRomUtilization = GetFloat(data, "averageRomUtilization", 0f);

        session.formScore = GetFloat(data, "formScore", 0f);
        session.formGrade = GetString(data, "formGrade", "N/A");

        session.difficultyLevel = GetInt(data, "difficultyLevel", 0);

        session.highReachAttempts = GetInt(data, "highReachAttempts", 0);
        session.highReachSuccesses = GetInt(data, "highReachSuccesses", 0);

        session.wideSpreadAttempts = GetInt(data, "wideSpreadAttempts", 0);
        session.wideSpreadSuccesses = GetInt(data, "wideSpreadSuccesses", 0);

        session.highReachRate = GetFloat(data, "highReachRate", 0f);
        session.wideSpreadRate = GetFloat(data, "wideSpreadRate", 0f);

        session.calibVertRangeM = GetFloat(data, "calibVertRangeM", 0f);
        session.calibLatRangeM = GetFloat(data, "calibLatRangeM", 0f);

        session.vertRomHitM = GetFloat(data, "vertRomHitM", 0f);
        session.latRomHitM = GetFloat(data, "latRomHitM", 0f);

        if (string.IsNullOrWhiteSpace(session.date))
            session.date = FormatDate(session.timestampMs);

        return session;
    }

    private void DisplaySessions(List<HistorySessionData> sessions)
    {
        ClearChildren(sessionListContent);

        if (sessions.Count == 0)
        {
            SetStatus("No history found for this user");
            return;
        }

        foreach (HistorySessionData session in sessions)
        {
            GameObject card = CreateSessionCard(session);
            card.transform.SetParent(sessionListContent, false);
        }
    }

    private GameObject CreateSessionCard(HistorySessionData session)
    {
        GameObject card;

        if (sessionCardPrefab != null)
        {
            card = Instantiate(sessionCardPrefab);
        }
        else
        {
            card = new GameObject("SessionCard", typeof(RectTransform), typeof(Image));

            Image bg = card.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(900f, 260f);

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(card.transform, false);

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(20f, 10f);
            textRect.offsetMax = new Vector2(-20f, -10f);

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 24;
            tmp.enableWordWrapping = true;
            tmp.color = Color.white;
        }

        TMP_Text text = card.GetComponentInChildren<TMP_Text>();

        if (text != null)
        {
            text.text =
                "Session: " + session.sessionId + "\n" +
                "Date: " + session.date + "\n" +
                "User ID: " + session.userId + "\n" +
                "Reps: " + session.successfulReps + " / " + session.totalReps + "\n" +
                "Success Rate: " + ToPercent(session.overallSuccessRate) + "\n" +
                "Peak ROM: " + ToPercent(session.peakRomUtilization) + "\n" +
                "Average ROM: " + ToPercent(session.averageRomUtilization) + "\n" +
                "Vertical ROM Hit: " + session.vertRomHitM.ToString("0.00") + " m\n" +
                "Lateral ROM Hit: " + session.latRomHitM.ToString("0.00") + " m\n" +
                "Form Score: " + session.formScore.ToString("0.0") + " | Grade: " + session.formGrade;
        }

        return card;
    }

    private void DisplayGraph(List<HistorySessionData> sessions)
    {
        ClearChildren(graphContent);

        if (graphContent == null)
            return;

        if (sessions.Count == 0)
            return;

        float maxValue = sessions.Max(s => s.vertRomHitM);

        if (maxValue <= 0f)
            maxValue = sessions.Max(s => s.peakRomUtilization);

        if (maxValue <= 0f)
            maxValue = 1f;

        foreach (HistorySessionData session in sessions)
        {
            GameObject bar = CreateGraphBar(session, maxValue);
            bar.transform.SetParent(graphContent, false);
        }
    }

    private GameObject CreateGraphBar(HistorySessionData session, float maxValue)
    {
        GameObject barRoot;

        if (graphBarPrefab != null)
        {
            barRoot = Instantiate(graphBarPrefab);
        }
        else
        {
            barRoot = new GameObject("GraphBar", typeof(RectTransform));

            RectTransform rootRect = barRoot.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(70f, maxBarHeight + 80f);

            GameObject barObj = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barObj.transform.SetParent(barRoot.transform, false);

            Image img = barObj.GetComponent<Image>();
            img.color = new Color(0.25f, 0.65f, 1f, 1f);

            RectTransform barRect = barObj.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 40f);
            barRect.sizeDelta = new Vector2(45f, 100f);

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(barRoot.transform, false);

            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            label.fontSize = 16;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);
            labelRect.sizeDelta = new Vector2(70f, 40f);
        }

        float value = session.vertRomHitM;

        if (value <= 0f)
            value = session.peakRomUtilization;

        float normalized = Mathf.Clamp01(value / maxValue);
        float height = Mathf.Max(10f, normalized * maxBarHeight);

        Transform barTransform = barRoot.transform.Find("Bar");

        if (barTransform != null)
        {
            RectTransform rect = barTransform.GetComponent<RectTransform>();

            if (rect != null)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        TMP_Text labelText = barRoot.GetComponentInChildren<TMP_Text>();

        if (labelText != null)
            labelText.text = value.ToString("0.00");

        return barRoot;
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private string FormatDate(long timestampMs)
    {
        if (timestampMs <= 0)
            return "Unknown date";

        try
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            DateTime localTime = dateTimeOffset.LocalDateTime;
            return localTime.ToString("dd/MM/yyyy HH:mm");
        }
        catch
        {
            return "Unknown date";
        }
    }

    private string ToPercent(float value)
    {
        if (value <= 1f)
            return (value * 100f).ToString("0.0") + "%";

        return value.ToString("0.0") + "%";
    }

    private string GetString(Dictionary<string, object> data, string key, string defaultValue)
    {
        if (data == null || !data.ContainsKey(key) || data[key] == null)
            return defaultValue;

        return data[key].ToString();
    }

    private int GetInt(Dictionary<string, object> data, string key, int defaultValue)
    {
        if (data == null || !data.ContainsKey(key) || data[key] == null)
            return defaultValue;

        try
        {
            object value = data[key];

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
            return defaultValue;
        }
    }

    private long GetLong(Dictionary<string, object> data, string key, long defaultValue)
    {
        if (data == null || !data.ContainsKey(key) || data[key] == null)
            return defaultValue;

        try
        {
            object value = data[key];

            if (value is long l)
                return l;

            if (value is int i)
                return i;

            if (value is double d)
                return (long)d;

            if (value is float f)
                return (long)f;

            return Convert.ToInt64(value);
        }
        catch
        {
            return defaultValue;
        }
    }

    private float GetFloat(Dictionary<string, object> data, string key, float defaultValue)
    {
        if (data == null || !data.ContainsKey(key) || data[key] == null)
            return defaultValue;

        try
        {
            object value = data[key];

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
            return defaultValue;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log("[History] " + message);
    }
}

[Serializable]
public class HistorySessionData
{
    public string sessionId;
    public string userId;
    public string date;

    public long timestampMs;
    public long startTimestampMs;
    public long endTimestampMs;
    public long durationMs;

    public int totalPhrases;
    public int totalReps;
    public int successfulReps;

    public float overallSuccessRate;
    public int longestSuccessStreak;

    public float peakRomUtilization;
    public float averageRomUtilization;

    public float formScore;
    public string formGrade;

    public int difficultyLevel;

    public int highReachAttempts;
    public int highReachSuccesses;

    public int wideSpreadAttempts;
    public int wideSpreadSuccesses;

    public float highReachRate;
    public float wideSpreadRate;

    public float calibVertRangeM;
    public float calibLatRangeM;

    public float vertRomHitM;
    public float latRomHitM;
}