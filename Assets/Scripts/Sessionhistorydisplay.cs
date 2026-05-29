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

// ─────────────────────────────────────────────────────────────────────────────
//  SessionHistoryDisplay
//
//  Inspector wiring — drag exactly these 4 objects:
//    statusText         → Canvas/LoadingText
//    summaryText        → Canvas/SummeryText
//    sessionListContent → Canvas/Scroll View/Viewport/CardContainer
//    graphContent       → Canvas/GraphPanel/GraphContainer
//
//  sessionCardPrefab is optional — leave None and cards are built in code.
//  No other scene changes needed. Everything else is configured at runtime.
// ─────────────────────────────────────────────────────────────────────────────
public class SessionHistoryDisplay : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Firestore")]
    public string sessionsCollectionName = "sessions";
    public int maxSessionsToLoad = 50;

    [Header("Testing")]
    public bool useTestUserIfNoLogin = true;
    public string testUserId = "ampcTUbGF3edyN95CG7UrEq3Ask2";

    [Header("UI  ←  drag these 4 objects")]
    public TMP_Text statusText;           // LoadingText
    public TMP_Text summaryText;          // SummeryText
    public RectTransform sessionListContent;// CardContainer
    public RectTransform graphContent;      // GraphContainer

    [Header("Optional prefab")]
    public GameObject sessionCardPrefab;    // leave None to use code-built cards

    [Header("Graph")]
    public float maxBarHeight = 320f;
    public float barWidth = 50f;

    // ── Private ───────────────────────────────────────────────────────────────
    private const int FETCH_LIMIT = 200;
    private FirebaseFirestore db;
    private FirebaseAuth auth;
    private List<HistorySessionData> cachedSessions;

    // =========================================================================
    //  Unity lifecycle
    // =========================================================================
    private void Start()
    {
        SetStatus("Initializing…");
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                auth = FirebaseAuth.DefaultInstance;
                SetStatus("Connected");
                BootstrapUI();
                LoadHistory();
            }
            else
            {
                SetStatus("Firebase error: " + task.Result);
                Debug.LogError("[History] " + task.Result);
            }
        });
    }

    // =========================================================================
    //  One-time UI setup — runs before any cards are spawned
    // =========================================================================
    private void BootstrapUI()
    {
        SetupCardContainer();
        SetupGraphContainer();
    }

    // ── CardContainer  (Scroll View > Viewport > CardContainer) ──────────────
    //
    //  Correct Unity scroll-view layout pattern:
    //    CardContainer anchors stretch the full Viewport width, pivot top-centre,
    //    a VerticalLayoutGroup stacks children with fixed spacing,
    //    a ContentSizeFitter makes the rect grow downward as cards are added,
    //    childControlHeight = FALSE  so each card keeps its own explicit height.
    // ─────────────────────────────────────────────────────────────────────────
    private void SetupCardContainer()
    {
        if (sessionListContent == null) return;

        // Anchor: stretch horizontally, top-aligned vertically
        sessionListContent.anchorMin = new Vector2(0f, 1f);
        sessionListContent.anchorMax = new Vector2(1f, 1f);
        sessionListContent.pivot = new Vector2(0.5f, 1f);
        sessionListContent.anchoredPosition = Vector2.zero;
        sessionListContent.sizeDelta = new Vector2(0f, 0f); // width from anchors

        // VerticalLayoutGroup stacks cards without controlling their heights
        var vlg = GetOrAdd<VerticalLayoutGroup>(sessionListContent.gameObject);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;   // fill container width
        vlg.childControlHeight = false;  // CRITICAL: cards set their own height
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(6, 6, 6, 6);

        // ContentSizeFitter expands container height to fit all cards
        var csf = GetOrAdd<ContentSizeFitter>(sessionListContent.gameObject);
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire ScrollRect.content if it isn't already set
        // Walk up: CardContainer → Viewport → Scroll View
        if (sessionListContent.parent != null &&
            sessionListContent.parent.parent != null)
        {
            var scrollGO = sessionListContent.parent.parent.gameObject;
            var sr = scrollGO.GetComponent<ScrollRect>();
            if (sr != null && sr.content == null)
                sr.content = sessionListContent;
        }
    }

    // ── GraphContainer ────────────────────────────────────────────────────────
    //
    //  GraphPanel  (sibling of Scroll View in Canvas)
    //    └── GraphContainer  ← graphContent, assigned in Inspector
    //
    //  At runtime:
    //   1. RectMask2D on GraphPanel clips bars to the panel bounds
    //   2. Horizontal ScrollRect on GraphPanel lets many bars scroll sideways
    //   3. GraphContainer is the ScrollRect content — grows rightward
    //   4. GraphContainer anchored to fill GraphPanel height
    // ─────────────────────────────────────────────────────────────────────────
    private void SetupGraphContainer()
    {
        if (graphContent == null) return;

        // GraphPanel = parent of GraphContainer
        Transform graphPanel = graphContent.parent;
        if (graphPanel == null) return;

        // 1. Clip overflow
        GetOrAdd<RectMask2D>(graphPanel.gameObject);

        // 2. Horizontal scroll on GraphPanel
        var sr = GetOrAdd<ScrollRect>(graphPanel.gameObject);
        sr.horizontal = true;
        sr.vertical = false;
        sr.content = graphContent;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 20f;

        // 3. GraphContainer anchors: stretch vertically, hug left
        graphContent.anchorMin = new Vector2(0f, 0f);
        graphContent.anchorMax = new Vector2(0f, 1f);
        graphContent.pivot = new Vector2(0f, 0.5f);
        graphContent.anchoredPosition = Vector2.zero;
        graphContent.sizeDelta = Vector2.zero;

        // 4. HorizontalLayoutGroup stacks bars left to right
        var hlg = GetOrAdd<HorizontalLayoutGroup>(graphContent.gameObject);
        hlg.childAlignment = TextAnchor.LowerLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = 4f;
        hlg.padding = new RectOffset(8, 8, 8, 8);

        // ContentSizeFitter grows GraphContainer rightward as bars are added
        var csf = GetOrAdd<ContentSizeFitter>(graphContent.gameObject);
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    // =========================================================================
    //  Firestore
    // =========================================================================
    public void LoadHistory()
    {
        if (db == null) { SetStatus("Firebase not ready"); return; }

        string uid = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(uid)) { SetStatus("No user logged in"); return; }

        SetStatus("Loading history…");

        db.Collection(sessionsCollectionName)
          .WhereEqualTo("userId", uid)
          .Limit(FETCH_LIMIT)
          .GetSnapshotAsync()
          .ContinueWithOnMainThread(task =>
          {
              if (task.IsCanceled) { SetStatus("Cancelled"); return; }
              if (task.IsFaulted) { SetStatus("Load failed"); Debug.LogError(task.Exception); return; }

              var raw = new List<HistorySessionData>();
              foreach (DocumentSnapshot doc in task.Result.Documents)
                  if (doc.Exists)
                      raw.Add(ConvertToSession(doc.Id, doc.ToDictionary()));

              cachedSessions = raw
                  .OrderByDescending(s => s.timestampMs)
                  .Take(maxSessionsToLoad)
                  .ToList();

              RenderAll(cachedSessions);
              SetStatus($"Loaded {cachedSessions.Count} sessions");
          });
    }

    // =========================================================================
    //  Render
    // =========================================================================
    private void RenderAll(List<HistorySessionData> sessions)
    {
        WriteSummary(sessions);
        BuildCards(sessions);
        BuildGraph(sessions.OrderBy(s => s.timestampMs).ToList());
    }

    // ── Summary text (SummeryText) ────────────────────────────────────────────
    private void WriteSummary(List<HistorySessionData> sessions)
    {
        if (summaryText == null) return;
        summaryText.richText = true;

        if (sessions == null || sessions.Count == 0)
        {
            summaryText.text = "No session history found.";
            return;
        }

        float avgSuccess = sessions.Average(s => s.overallSuccessRate);
        float avgRom = sessions.Average(s => s.peakRomUtilization);
        int bestStreak = sessions.Max(s => s.longestSuccessStreak);
        string grade = sessions.First().formGrade;

        string trend = "—";
        if (sessions.Count >= 3)
        {
            var ch = sessions.OrderBy(s => s.timestampMs).ToList();
            float e = ch.Take(3).Average(s => s.overallSuccessRate);
            float r = ch.TakeLast(3).Average(s => s.overallSuccessRate);
            float d = r - e;
            string arr = d > 0.02f ? "↑" : d < -0.02f ? "↓" : "→";
            string lbl = d > 0.02f ? "Improving" : d < -0.02f ? "Declining" : "Stable";
            trend = $"{lbl} {arr}  ({ToPercent(e)} → {ToPercent(r)})";
        }

        summaryText.text =
            $"<b>Total Sessions:</b> {sessions.Count}   " +
            $"<b>Avg Success:</b> {ToPercent(avgSuccess)}   " +
            $"<b>Avg Peak ROM:</b> {ToPercent(avgRom)}\n" +
            $"<b>Best Streak:</b> {bestStreak}   " +
            $"<b>Last Grade:</b> <color={GradeHex(grade)}>{grade}</color>   " +
            $"<b>Trend:</b> {trend}";
    }

    // ── Session cards ─────────────────────────────────────────────────────────
    private void BuildCards(List<HistorySessionData> sessions)
    {
        ClearChildren(sessionListContent);
        if (sessions == null || sessions.Count == 0) return;

        int i = 1;
        foreach (var s in sessions)
            MakeCard(s, i++);

        // Force the layout to rebuild immediately so CSF recalculates height
        LayoutRebuilder.ForceRebuildLayoutImmediate(sessionListContent);
    }

    private void MakeCard(HistorySessionData s, int index)
    {
        // ── Card height: measure the text first, then size the card ──────────
        //  We use a fixed line-height approach so the card height is known
        //  before Unity's layout pass. 7 lines × 22px + top/bottom padding.
        const float LINE_H = 22f;
        const int NUM_LINES = 7;    // header + divider + 5 data rows
        const float PADDING_V = 20f;
        float cardHeight = NUM_LINES * LINE_H + PADDING_V;

        GameObject card;

        if (sessionCardPrefab != null)
        {
            card = UnityEngine.Object.Instantiate(sessionCardPrefab);
        }
        else
        {
            card = new GameObject($"Card_{index:D3}",
                typeof(RectTransform), typeof(Image));

            // Alternating dark rows
            card.GetComponent<Image>().color = index % 2 == 0
                ? new Color(0.09f, 0.12f, 0.17f, 1f)
                : new Color(0.07f, 0.10f, 0.14f, 1f);

            // ── Grade accent strip on the left ────────────────────────────────
            var stripe = new GameObject("Stripe",
                typeof(RectTransform), typeof(Image));
            stripe.transform.SetParent(card.transform, false);
            stripe.GetComponent<Image>().color = GradeColor(s.formGrade);
            var srt = stripe.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(0f, 1f);
            srt.pivot = new Vector2(0f, 0.5f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = new Vector2(5f, 0f);

            // ── Text ──────────────────────────────────────────────────────────
            var tgo = new GameObject("Text",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            tgo.transform.SetParent(card.transform, false);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 8f);
            trt.offsetMax = new Vector2(-8f, -8f);

            var tmp = tgo.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 13f;
            tmp.lineSpacing = 5f;
            tmp.enableWordWrapping = false;      // one row per line, no wrapping
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.richText = true;
        }

        // ── Explicit size — no ContentSizeFitter on cards ─────────────────────
        //  childControlHeight = false on the parent VLG, so we set sizeDelta.y
        var rt = card.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, cardHeight);

        // Also set a LayoutElement so the VLG respects the height
        var le = GetOrAdd<LayoutElement>(card);
        le.minHeight = cardHeight;
        le.preferredHeight = cardHeight;

        card.transform.SetParent(sessionListContent, false);

        // ── Fill text ─────────────────────────────────────────────────────────
        var txt = card.GetComponentInChildren<TMP_Text>();
        if (txt == null) return;

        txt.fontSize = 13f;
        txt.lineSpacing = 5f;
        txt.enableWordWrapping = false;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.alignment = TextAlignmentOptions.TopLeft;
        txt.richText = true;

        long dur = s.durationMs / 1000;
        string durStr = dur > 0 ? $"{dur / 60}m {dur % 60}s" : "—";

        txt.text =
            // Row 0 — header
            $"<size=14><b>#{index:D2}  {s.date}</b></size>  " +
            $"<color=#555555><size=11>{ShortId(s.sessionId)}</size></color>\n" +
            // Divider
            $"<color=#2a3040>{'─'.ToString().PadRight(80, '─')}</color>\n" +
            // Row 1 — reps / success / streak / duration
            $"<b>Reps</b> {s.successfulReps}/{s.totalReps}   " +
            $"<b>Success</b> {ToPercent(s.overallSuccessRate)}   " +
            $"<b>Streak</b> {s.longestSuccessStreak}   " +
            $"<b>Duration</b> {durStr}\n" +
            // Row 2 — ROM
            $"<b>Peak ROM</b> {ToPercent(s.peakRomUtilization)}   " +
            $"<b>Avg ROM</b> {ToPercent(s.averageRomUtilization)}   " +
            $"<b>Vert hit</b> {s.vertRomHitM:0.00} m   " +
            $"<b>Lat hit</b> {s.latRomHitM:0.00} m\n" +
            // Row 3 — calibration / difficulty
            $"<b>Calib V</b> {s.calibVertRangeM:0.00} m   " +
            $"<b>Calib L</b> {s.calibLatRangeM:0.00} m   " +
            $"<b>Difficulty</b> {s.difficultyLevel}   " +
            $"<b>Phrases</b> {s.totalPhrases}\n" +
            // Row 4 — form
            $"<b>Form</b> {s.formScore:0.0}   " +
            $"<b>Grade</b> <color={GradeHex(s.formGrade)}><b>{s.formGrade}</b></color>   " +
            $"<b>High reach</b> {s.highReachSuccesses}/{s.highReachAttempts} ({ToPercent(s.highReachRate)})   " +
            $"<b>Wide spread</b> {s.wideSpreadSuccesses}/{s.wideSpreadAttempts} ({ToPercent(s.wideSpreadRate)})";
    }

    // ── Bar chart (GraphContainer) ────────────────────────────────────────────
    private void BuildGraph(List<HistorySessionData> chrono)
    {
        ClearChildren(graphContent);
        if (graphContent == null || chrono == null || chrono.Count == 0) return;

        // Each session picks its own best metric:
        //   vertRomHitM > 0  → use it (leg rehab)
        //   peakRomUtilization > 0 → use it (hand scan proxy)
        //   otherwise → overallSuccessRate
        // maxVal is the highest value across all sessions using the same scale (0-1 or metres).
        // Since leg uses metres and hand uses 0-1, we normalise everything to 0-1 for the bar.

        float maxVal = 0.01f;
        foreach (var s in chrono)
        {
            float v = BestMetric(s);
            // normalise metres to 0-1 using a 1m reference for leg sessions
            float norm = s.vertRomHitM > 0f ? v : v;
            if (v > maxVal) maxVal = v;
        }

        foreach (var s in chrono)
        {
            float val = BestMetric(s);
            string label = s.vertRomHitM > 0f
                ? $"{val:0.00}m"
                : ToPercent(val);
            Color col = LerpHealthColor(s.overallSuccessRate); // colour by success always
            MakeBar(s.date, val, maxVal, label, col);
        }
    }

    // Returns the most meaningful single metric for a session's bar height.
    // Leg sessions: vertRomHitM (metres, 0–~1)
    // Hand sessions: peakRomUtilization (0–1 proxy)
    // Fallback: overallSuccessRate (0–1)
    private static float BestMetric(HistorySessionData s)
    {
        if (s.vertRomHitM > 0f) return s.vertRomHitM;
        if (s.peakRomUtilization > 0f) return s.peakRomUtilization;
        return s.overallSuccessRate;
    }

    private void MakeBar(string date, float value, float maxVal,
                         string label, Color color)
    {
        float norm = maxVal > 0f ? Mathf.Clamp01(value / maxVal) : 0f;
        float barH = Mathf.Max(6f, norm * maxBarHeight);
        float totalH = maxBarHeight + 56f;

        // Root
        var root = new GameObject($"Bar_{date}",
            typeof(RectTransform), typeof(LayoutElement));
        root.transform.SetParent(graphContent, false);

        var le = root.GetComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = barWidth;
        le.minHeight = le.preferredHeight = totalH;

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(barWidth, totalH);

        // Fill
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(root.transform, false);
        fill.GetComponent<Image>().color = color;
        var frt = fill.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0.5f, 0f);
        frt.anchorMax = new Vector2(0.5f, 0f);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = new Vector2(0f, 28f);
        frt.sizeDelta = new Vector2(barWidth * 0.68f, barH);

        // Value label — sits just above the bar
        MakeBarLabel(root.transform, label, 10f,
            new Vector2(0f, 28f + barH + 2f), new Vector2(barWidth, 16f),
            Color.white);

        // Date label — sits at bottom
        MakeBarLabel(root.transform, ShortDate(date), 9f,
            new Vector2(0f, 5f), new Vector2(barWidth, 18f),
            new Color(0.65f, 0.65f, 0.65f));
    }

    private static void MakeBarLabel(Transform parent, string text,
        float fontSize, Vector2 pos, Vector2 size, Color col)
    {
        var go = new GameObject("Lbl", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = col;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // =========================================================================
    //  Data conversion
    // =========================================================================
    private HistorySessionData ConvertToSession(string docId,
        Dictionary<string, object> d)
    {
        var s = new HistorySessionData
        {
            sessionId = GetStr(d, "sessionId", docId),
            userId = GetStr(d, "userId", ""),
            date = GetStr(d, "date", ""),
            formGrade = GetStr(d, "formGrade", "N/A"),
            startTimestampMs = GetLng(d, "startTimestampMs", 0),
            endTimestampMs = GetLng(d, "endTimestampMs", 0),
            durationMs = GetLng(d, "durationMs", 0),
            totalPhrases = GetInt(d, "totalPhrases", 0),
            totalReps = GetInt(d, "totalReps", 0),
            successfulReps = GetInt(d, "successfulReps", 0),
            longestSuccessStreak = GetInt(d, "longestSuccessStreak", 0),
            difficultyLevel = GetInt(d, "difficultyLevel", 0),
            highReachAttempts = GetInt(d, "highReachAttempts", 0),
            highReachSuccesses = GetInt(d, "highReachSuccesses", 0),
            wideSpreadAttempts = GetInt(d, "wideSpreadAttempts", 0),
            wideSpreadSuccesses = GetInt(d, "wideSpreadSuccesses", 0),
            overallSuccessRate = GetFlt(d, "overallSuccessRate", 0f),
            peakRomUtilization = GetFlt(d, "peakRomUtilization", 0f),
            averageRomUtilization = GetFlt(d, "averageRomUtilization", 0f),
            formScore = GetFlt(d, "formScore", 0f),
            highReachRate = GetFlt(d, "highReachRate", 0f),
            wideSpreadRate = GetFlt(d, "wideSpreadRate", 0f),
            calibVertRangeM = GetFlt(d, "calibVertRangeM", 0f),
            calibLatRangeM = GetFlt(d, "calibLatRangeM", 0f),
            vertRomHitM = GetFlt(d, "vertRomHitM", 0f),
            latRomHitM = GetFlt(d, "latRomHitM", 0f),
        };
        s.timestampMs = s.startTimestampMs > 0 ? s.startTimestampMs : s.endTimestampMs;
        if (string.IsNullOrWhiteSpace(s.date)) s.date = FmtDate(s.timestampMs);
        return s;
    }

    // =========================================================================
    //  Auth
    // =========================================================================
    private string GetCurrentUserId()
    {
        var user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user != null)
        {
            PlayerPrefs.SetString("LoggedInUserId", user.UserId);
            PlayerPrefs.SetString("LoggedInUserEmail", user.Email ?? "");
            PlayerPrefs.Save();
            return user.UserId;
        }
        string saved = PlayerPrefs.GetString("LoggedInUserId", "");
        if (!string.IsNullOrWhiteSpace(saved)) return saved;
        if (useTestUserIfNoLogin && !string.IsNullOrWhiteSpace(testUserId))
        {
            PlayerPrefs.SetString("LoggedInUserId", testUserId);
            PlayerPrefs.SetString("LoggedInUserEmail", "test-user");
            PlayerPrefs.Save();
            Debug.LogWarning("[History] Using test user: " + testUserId);
            return testUserId;
        }
        return "";
    }

    // =========================================================================
    //  Utilities
    // =========================================================================
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }

    private static string FmtDate(long ms)
    {
        if (ms <= 0) return "Unknown";
        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms)
                                   .LocalDateTime.ToString("dd/MM/yyyy HH:mm");
        }
        catch { return "Unknown"; }
    }

    private static string ShortDate(string d)
        => string.IsNullOrWhiteSpace(d) ? "" : (d.Length >= 5 ? d.Substring(0, 5) : d);

    private static string ShortId(string id)
        => string.IsNullOrWhiteSpace(id) ? "" :
           (id.Length <= 22 ? id : id.Substring(0, 22) + "…");

    private static string ToPercent(float v)
        => (v <= 1f ? v * 100f : v).ToString("0.0") + "%";

    private static Color LerpHealthColor(float t)
        => Color.Lerp(new Color(0.9f, 0.25f, 0.2f),
                      new Color(0.18f, 0.78f, 0.35f), Mathf.Clamp01(t));

    private static Color GradeColor(string g)
    {
        if (string.IsNullOrEmpty(g)) return Color.gray;
        switch (g.ToUpper())
        {
            case "A": case "A+": return new Color(0.18f, 0.78f, 0.35f);
            case "B": case "B+": return new Color(0.40f, 0.75f, 0.20f);
            case "C": case "C+": return new Color(0.95f, 0.75f, 0.10f);
            case "D": return new Color(0.95f, 0.45f, 0.10f);
            default: return new Color(0.80f, 0.20f, 0.20f);
        }
    }

    private static string GradeHex(string g)
        => "#" + ColorUtility.ToHtmlStringRGB(GradeColor(g));

    // ── Firestore type helpers ────────────────────────────────────────────────
    private static string GetStr(Dictionary<string, object> d, string k, string def)
    { if (d == null || !d.ContainsKey(k) || d[k] == null) return def; return d[k].ToString(); }

    private static int GetInt(Dictionary<string, object> d, string k, int def)
    {
        if (d == null || !d.ContainsKey(k) || d[k] == null) return def;
        try
        {
            var v = d[k];
            if (v is int i) return i;
            if (v is long l) return (int)l;
            if (v is double dv) return Mathf.RoundToInt((float)dv);
            if (v is float f) return Mathf.RoundToInt(f);
            return Convert.ToInt32(v);
        }
        catch { return def; }
    }

    private static long GetLng(Dictionary<string, object> d, string k, long def)
    {
        if (d == null || !d.ContainsKey(k) || d[k] == null) return def;
        try
        {
            var v = d[k];
            if (v is long l) return l;
            if (v is int i) return i;
            if (v is double dv) return (long)dv;
            if (v is float f) return (long)f;
            return Convert.ToInt64(v);
        }
        catch { return def; }
    }

    private static float GetFlt(Dictionary<string, object> d, string k, float def)
    {
        if (d == null || !d.ContainsKey(k) || d[k] == null) return def;
        try
        {
            var v = d[k];
            if (v is float f) return f;
            if (v is double dv) return (float)dv;
            if (v is int i) return i;
            if (v is long l) return l;
            return Convert.ToSingle(v);
        }
        catch { return def; }
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log("[History] " + msg);
    }
}

// =============================================================================
[Serializable]
public class HistorySessionData
{
    public string sessionId, userId, date, formGrade;
    public long timestampMs, startTimestampMs, endTimestampMs, durationMs;
    public int totalPhrases, totalReps, successfulReps, longestSuccessStreak;
    public int difficultyLevel;
    public int highReachAttempts, highReachSuccesses;
    public int wideSpreadAttempts, wideSpreadSuccesses;
    public float overallSuccessRate, peakRomUtilization, averageRomUtilization;
    public float formScore, highReachRate, wideSpreadRate;
    public float calibVertRangeM, calibLatRangeM, vertRomHitM, latRomHitM;
}