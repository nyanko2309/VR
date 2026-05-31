using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

public enum GamePhase { Idle, Calibration, Playing, Finished }
public enum ExercisePhase { KneeRaises, LegExtensions }

public class PhysioBallGenerator : MonoBehaviour
{
    private enum CalibDirection { Up, Down, Lateral }

    [Header("Core References")]
    public LegRootFitter legRootFitter;
    public LegSideSelector sideSelector;
    public BodyTracker bodyTracker;
    public Transform hmdTransform;
    public Transform bodyCentreTransform;
    public GameObject ballPrefab;
    public GameObject scoreCanvas;
    public TextMeshProUGUI scoreText;
    public GameObject instructionsCanvas;
    public TextMeshProUGUI instructionsText;

    [Header("Passthrough")]
    public OVRPassthroughLayer passthroughLayer;
    public float gameOpacity = 0.4f;
    public float normalOpacity = 1f;

    [Header("Movement")]
    [Tooltip("How far cubes spawn in front of player (metres)")]
    public float spawnDepth = 2.3f;
    [Tooltip("Cube slide speed in m/s")]
    public float slideSpeed = 0.8f;
    [Tooltip("Cubes disappear when they reach this distance (metres) in front of the body centre")]
    public float missDistance = 0.15f;

    [Header("Rhythm")]
    public float bpm = 84f;
    [Tooltip("Spawn every N beats")]
    public int beatsPerSpawn = 4;

    [Header("Session")]
    [Tooltip("Reps per exercise phase")]
    public int repsPerPhase = 10;
    public float repTimeout = 6f;

    [Header("Visual")]
    public float pulseScale = 1.25f;
    public float pulseSpeed = 10f;
    public GameObject particlePrefab;
    public float colorCycleSpeed = 0.6f;
    public float glowIntensity = 1.8f;

    [Header("Calibration")]
    public float calibStep = 0.10f;
    public float calibArmTimeout = 5f;
    public float calibCubeScale = 1.6f;
    public int calibCubesPerArm = 5;

    [Header("Knee Raises")]
    [Tooltip("Height target as fraction of calibrated vertical ROM (higher = harder)")]
    [Range(0f, 1f)]
    public float kneeRaiseHeightFraction = 0.75f;
    [Tooltip("Fixed lateral gap between the two knee-raise cubes (metres) — kept tight so both knees hit together")]
    public float kneeRaiseLateralOffset = 0.12f;

    [Header("Leg Extensions — Variety")]
    [Tooltip("Extra forward depth for extension cubes on top of spawnDepth")]
    public float legExtensionExtraDepth = 0.3f;
    [Tooltip("Lower bound of vertical variety, as fraction of calibrated vertical ROM (0 = patient's lowest reached point)")]
    [Range(0f, 1f)]
    public float legExtensionHeightFractionLow = 0.05f;
    [Tooltip("Upper bound of vertical variety, as fraction of calibrated vertical ROM (1 = patient's highest reached point)")]
    [Range(0f, 1f)]
    public float legExtensionHeightFractionHigh = 0.45f;
    [Tooltip("Lower bound of lateral variety, as fraction of calibrated lateral ROM (0 = centre)")]
    [Range(0f, 1f)]
    public float legExtensionLateralFractionLow = 0.25f;
    [Tooltip("Upper bound of lateral variety, as fraction of calibrated lateral ROM (1 = furthest the patient reached)")]
    [Range(0f, 1f)]
    public float legExtensionLateralFractionHigh = 0.85f;

    [Header("Audio")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public AudioClip hitSound;
    public AudioClip missSound;
    [Range(0f, 1f)] public float hitVolume = 0.6f;

    // ── Private state ─────────────────────────────────────────────────────

    private GamePhase _phase = GamePhase.Idle;
    private ExercisePhase _currentExercise = ExercisePhase.KneeRaises;
    private CalibDirection _currentCalibDirection;

    private float _centreX, _centreY, _centreZ;
    private Vector3 _fwd, _right;

    private float _romYMin, _romYMax;
    private float _romXMin, _romXMax;

    private GameObject _leftBall, _rightBall;
    private bool _leftHit, _rightHit;
    private bool _pairActive;
    private bool _singleActive;

    private float _sessionHighestHitY = float.MinValue;
    private float _sessionLowestHitY = float.MaxValue;
    private float _sessionFurthestHitX = 0f;
    private long _sessionStartMs = 0;
    private int _repsCompleted = 0;
    private int _repsHit = 0;
    private int _totalReps = 0;
    private int _totalHit = 0;

    private float _beatInterval;
    private float _nextBeatTime;

    private List<float> _calibHitsUp = new List<float>();
    private List<float> _calibHitsDown = new List<float>();
    private List<float> _calibHitsLeft = new List<float>();
    private List<float> _calibHitsRight = new List<float>();

    private List<GameObject> _calibCubesMain = new List<GameObject>();
    private List<GameObject> _calibCubesRight = new List<GameObject>();
    private int _calibHitMain = 0;
    private int _calibHitRight = 0;

    private float _targetOpacity;
    private float _currentOpacity;

    private AudioSource _audioSource;
    private AudioSource _musicSource;

    private static readonly Color ColLeft = new Color(1.0f, 0.55f, 0.05f);
    private static readonly Color ColRight = new Color(0.20f, 0.60f, 1.00f);
    private static readonly Color ColKneeRaise = new Color(0.20f, 0.85f, 0.40f);
    private static readonly Color ColExtension = new Color(0.85f, 0.20f, 0.80f);
    private static readonly Color ColCalibUp = new Color(0.20f, 0.85f, 0.40f);
    private static readonly Color ColCalibDown = new Color(0.95f, 0.60f, 0.10f);
    private static readonly Color ColCalibLeft = new Color(0.20f, 0.60f, 1.00f);
    private static readonly Color ColCalibRight = new Color(0.95f, 0.25f, 0.60f);

    // ── Unity lifecycle ───────────────────────────────────────────────────

    void Start()
    {
        _targetOpacity = normalOpacity;
        _currentOpacity = normalOpacity;
        _beatInterval = 60f / bpm;

        if (scoreCanvas != null) scoreCanvas.SetActive(false);
        if (instructionsCanvas != null) instructionsCanvas.SetActive(false);

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.spatialBlend = 0f;
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;
        _musicSource.volume = 0f;
    }

    void Update()
    {
        if (passthroughLayer != null)
            passthroughLayer.textureOpacity = Mathf.Lerp(
                passthroughLayer.textureOpacity, _targetOpacity, Time.deltaTime * 3f);

        if (_pairActive) SlidePair();
        if (_singleActive) SlideActive();

        if (OVRInput.GetDown(OVRInput.Button.Four) || OVRInput.GetDown(OVRInput.Button.Two))
        {
            ResetGame();
            if (sideSelector != null) sideSelector.ResetSide();
        }

        if (_phase == GamePhase.Idle && sideSelector != null && sideSelector.sideLocked)
            BeginSession();
    }

    // ── Hip helper ────────────────────────────────────────────────────────

    Vector3 GetHipPosition()
    {
        if (legRootFitter != null && legRootFitter.HipWorldPosition != Vector3.zero)
            return legRootFitter.HipWorldPosition;
        if (bodyCentreTransform != null)
            return bodyCentreTransform.position;
        return hmdTransform.position + Vector3.down * 0.6f;
    }

    // ── Body centre helper (XZ only, Y ignored so height doesn't drift) ──

    Vector3 GetBodyCentreXZ(float atY)
    {
        return new Vector3(_centreX, atY, _centreZ);
    }

    // ── Distance ahead of body along _fwd ────────────────────────────────

    float DistAheadOfBody(Vector3 worldPos)
    {
        Vector3 bodyRef = new Vector3(_centreX, worldPos.y, _centreZ);
        return Vector3.Dot(worldPos - bodyRef, _fwd);
    }

    // ── Sliding ───────────────────────────────────────────────────────────

    void SlidePair()
    {
        float step = slideSpeed * Time.deltaTime;
        if (_leftBall != null) _leftBall.transform.position -= _fwd * step;
        if (_rightBall != null) _rightBall.transform.position -= _fwd * step;

        GameObject probe = _leftBall ?? _rightBall;
        if (probe == null) return;

        // FIX: use forward-axis distance from body centre, not world Z
        if (DistAheadOfBody(probe.transform.position) <= missDistance)
            MissedPair();
    }

    void SlideActive()
    {
        float step = slideSpeed * Time.deltaTime;
        if (_leftBall != null) _leftBall.transform.position -= _fwd * step;
        if (_rightBall != null) _rightBall.transform.position -= _fwd * step;

        GameObject probe = _leftBall ?? _rightBall;
        if (probe == null) { _singleActive = false; return; }

        // FIX: use forward-axis distance from body centre, not world Z
        if (DistAheadOfBody(probe.transform.position) <= missDistance)
            MissedSingle();
    }

    void MissedPair()
    {
        PlayHit(false);
        DestroyPair();
        _repsCompleted++;
    }

    void MissedSingle()
    {
        PlayHit(false);
        DestroySingle();
        _repsCompleted++;
    }

    void DestroyPair()
    {
        if (_leftBall != null) { Destroy(_leftBall); _leftBall = null; }
        if (_rightBall != null) { Destroy(_rightBall); _rightBall = null; }
        _pairActive = false;
    }

    void DestroySingle()
    {
        if (_leftBall != null) { Destroy(_leftBall); _leftBall = null; }
        if (_rightBall != null) { Destroy(_rightBall); _rightBall = null; }
        _singleActive = false;
    }

    // ── Session start ─────────────────────────────────────────────────────

    void BeginSession()
    {
        _phase = GamePhase.Calibration;
        _targetOpacity = gameOpacity;
        _beatInterval = 60f / bpm;

        Vector3 hip = GetHipPosition();
        _centreX = hip.x; _centreY = hip.y; _centreZ = hip.z;
        _fwd = Vector3.ProjectOnPlane(hmdTransform.forward, Vector3.up).normalized;
        _right = Vector3.Cross(Vector3.up, _fwd).normalized;

        _sessionHighestHitY = float.MinValue;
        _sessionLowestHitY = float.MaxValue;
        _sessionFurthestHitX = 0f;
        _sessionStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _repsCompleted = _repsHit = _totalReps = _totalHit = 0;

        _romYMin = _centreY - 0.10f;
        _romYMax = _centreY;
        _romXMin = -0.15f;
        _romXMax = 0.15f;

        if (scoreCanvas != null) scoreCanvas.SetActive(false);
        if (instructionsCanvas != null) instructionsCanvas.SetActive(false);

        StartCoroutine(FadeMusic(0f, musicVolume, 2f));
        StartCoroutine(RunCalibration());
    }

    // ── Calibration ───────────────────────────────────────────────────────

    IEnumerator RunCalibration()
    {
        ShowHint("Calibration!\nPop cubes in each direction");
        yield return new WaitForSeconds(2f);

        // FIX: spawn calib cubes in front of the player using _fwd, not raw Z offset
        Vector3 base3 = new Vector3(_centreX, _centreY, _centreZ) + _fwd * 0.6f;

        _currentCalibDirection = CalibDirection.Up;
        ShowHint("Reach UP ↑\n(raise your knee as high as you can)");
        _calibHitsUp.Clear(); _calibHitMain = 0;
        yield return StartCoroutine(RunCalibArm(base3, Vector3.up, calibStep, ColCalibUp, isRight: false));

        _currentCalibDirection = CalibDirection.Down;
        ShowHint("Reach DOWN ↓\n(extend your leg forward and down)");
        _calibHitsDown.Clear(); _calibHitMain = 0;
        yield return StartCoroutine(RunCalibArm(base3, Vector3.down, calibStep, ColCalibDown, isRight: false));

        _currentCalibDirection = CalibDirection.Lateral;
        ShowHint("Reach LEFT and RIGHT ←→\n(swing your leg outward)");
        _calibHitsLeft.Clear(); _calibHitsRight.Clear();
        _calibHitMain = 0; _calibHitRight = 0;
        Coroutine la = StartCoroutine(RunCalibArm(base3, -_right, calibStep, ColCalibLeft, isRight: false));
        Coroutine ra = StartCoroutine(RunCalibArm(base3, _right, calibStep, ColCalibRight, isRight: true));
        yield return la; yield return ra;

        _romYMax = TopAverage(_calibHitsUp, take: 3, highest: true, fallback: _centreY + 0.4f);
        _romYMin = TopAverage(_calibHitsDown, take: 3, highest: false, fallback: _centreY - 0.2f);

        var leftOffsets = new List<float>(); foreach (float x in _calibHitsLeft) leftOffsets.Add(_centreX - x);
        var rightOffsets = new List<float>(); foreach (float x in _calibHitsRight) rightOffsets.Add(x - _centreX);
        float maxLeft = TopAverage(leftOffsets, take: 3, highest: true, fallback: 0.3f);
        float maxRight = TopAverage(rightOffsets, take: 3, highest: true, fallback: 0.3f);
        _romXMin = -maxLeft;
        _romXMax = maxRight;

        _romYMax = Mathf.Max(_romYMax, _centreY + 0.05f);
        _romYMin = Mathf.Min(_romYMin, _centreY - 0.05f);
        _romXMax = Mathf.Max(_romXMax, 0.05f);
        _romXMin = Mathf.Min(_romXMin, -0.05f);

        Debug.Log($"[calib] Y:{_romYMin:F2}–{_romYMax:F2}  X:{_romXMin:F2}–{_romXMax:F2}");

        ShowHint("Starting!");
        yield return new WaitForSeconds(1f);
        ShowHint("");

        // Re-sample heading after calibration in case player turned slightly
        _fwd = Vector3.ProjectOnPlane(hmdTransform.forward, Vector3.up).normalized;
        _right = Vector3.Cross(Vector3.up, _fwd).normalized;

        _phase = GamePhase.Playing;
        StartCoroutine(RunSession());
    }

    IEnumerator RunCalibArm(Vector3 basePos, Vector3 dir, float step, Color col, bool isRight)
    {
        var list = isRight ? _calibCubesRight : _calibCubesMain;
        list.Clear();
        if (isRight) _calibHitRight = 0; else _calibHitMain = 0;

        for (int i = 1; i <= calibCubesPerArm; i++)
        {
            var cube = Instantiate(ballPrefab, basePos + dir * (step * i), Quaternion.identity, transform);
            cube.transform.localScale = Vector3.one * calibCubeScale;
            var pb = cube.GetComponent<PhysioBall>();
            if (pb != null) pb.Setup(this);
            SetColor(cube, col);
            list.Add(cube);
            StartCoroutine(PulseIn(cube));
        }

        float elapsed = 0f;
        int Count() => isRight ? _calibHitRight : _calibHitMain;
        while (elapsed < calibArmTimeout && Count() < calibCubesPerArm)
        { elapsed += Time.deltaTime; yield return null; }

        foreach (var c in list) if (c != null) Destroy(c);
        list.Clear();
    }

    float TopAverage(List<float> vals, int take, bool highest, float fallback)
    {
        if (vals.Count == 0) return fallback;
        vals.Sort();
        if (highest) vals.Reverse();
        float sum = 0f;
        int n = Mathf.Min(take, vals.Count);
        for (int i = 0; i < n; i++) sum += vals[i];
        return sum / n;
    }

    // ── Session ───────────────────────────────────────────────────────────

    IEnumerator RunSession()
    {
        // ── Phase 1: Knee Raises ──────────────────────────────────────
        _currentExercise = ExercisePhase.KneeRaises;
        ShowHint("🦵 Knee Raises!\nRaise both knees up together\nto hit the cubes");
        yield return new WaitForSeconds(3f);
        ShowHint("");

        yield return StartCoroutine(RunExercisePhase(ExercisePhase.KneeRaises, repsPerPhase));

        // ── Rest ──────────────────────────────────────────────────────
        ShowHint("Rest — breathe 😮‍💨");
        yield return new WaitForSeconds(4f);
        ShowHint("");

        // ── Phase 2: Leg Extensions ───────────────────────────────────
        _currentExercise = ExercisePhase.LegExtensions;
        ShowHint("🦵 Leg Extensions!\nExtend both legs forward\nand down together");
        yield return new WaitForSeconds(3f);
        ShowHint("");

        yield return StartCoroutine(RunExercisePhase(ExercisePhase.LegExtensions, repsPerPhase));

        EndSession();
    }

    IEnumerator RunExercisePhase(ExercisePhase exercise, int targetReps)
    {
        _repsCompleted = 0;
        _repsHit = 0;
        _beatInterval = 60f / bpm;
        float travelTime = spawnDepth / slideSpeed;
        _nextBeatTime = Time.time + _beatInterval;

        while (_repsCompleted < targetReps)
        {
            float spawnTime = _nextBeatTime - travelTime;
            yield return new WaitUntil(() => Time.time >= spawnTime);

            if (!_pairActive) SpawnForExercise(exercise);

            _nextBeatTime += _beatInterval * beatsPerSpawn;

            float waited = 0f;
            while (waited < repTimeout)
            {
                if (!_pairActive) break;
                waited += Time.deltaTime;
                yield return null;
            }

            DestroyPair();
        }

        _totalReps += _repsCompleted;
        _totalHit += _repsHit;
    }

    // ── Spawn logic per exercise ──────────────────────────────────────────

    void SpawnForExercise(ExercisePhase exercise)
    {
        if (exercise == ExercisePhase.KneeRaises)
            SpawnKneeRaises();
        else
            SpawnLegExtensions();
    }

    void SpawnKneeRaises()
    {
        _leftHit = false; _rightHit = false;

        // FIX: re-sample hip XZ each spawn so cubes always originate from current body position
        Vector3 hip = GetHipPosition();
        _centreX = hip.x; _centreZ = hip.z;

        // FIX: also refresh heading so cubes fly toward wherever the player is now facing
        _fwd = Vector3.ProjectOnPlane(hmdTransform.forward, Vector3.up).normalized;
        _right = Vector3.Cross(Vector3.up, _fwd).normalized;

        float vertRange = _romYMax - _romYMin;
        float targetY = _romYMin + vertRange * kneeRaiseHeightFraction;
        // Keep cubes close together so both knees must rise together to hit them
        float lateralOffset = kneeRaiseLateralOffset;

        Vector3 origin = new Vector3(_centreX, targetY, _centreZ) + _fwd * spawnDepth;
        _leftBall = SpawnBall(origin - _right * lateralOffset, ColKneeRaise);
        _rightBall = SpawnBall(origin + _right * lateralOffset, ColKneeRaise);
        _pairActive = true;

        StartCoroutine(PulseIn(_leftBall));
        StartCoroutine(PulseIn(_rightBall));
    }

    void SpawnLegExtensions()
    {
        _leftHit = false; _rightHit = false;

        // FIX: re-sample hip XZ each spawn so cubes always originate from current body position
        Vector3 hip = GetHipPosition();
        _centreX = hip.x; _centreZ = hip.z;

        // FIX: also refresh heading so cubes fly toward wherever the player is now facing
        _fwd = Vector3.ProjectOnPlane(hmdTransform.forward, Vector3.up).normalized;
        _right = Vector3.Cross(Vector3.up, _fwd).normalized;

        float vertRange = _romYMax - _romYMin;

        // Derive actual metre ranges from calibrated ROM so variety
        // stays within what the patient physically demonstrated
        float yLow = _romYMin + vertRange * legExtensionHeightFractionLow;
        float yHigh = _romYMin + vertRange * legExtensionHeightFractionHigh;

        float calibLateralMax = (Mathf.Abs(_romXMin) + _romXMax) * 0.5f; // avg of left/right reach
        float latLow = calibLateralMax * legExtensionLateralFractionLow;
        float latHigh = calibLateralMax * legExtensionLateralFractionHigh;

        float targetY = UnityEngine.Random.Range(yLow, yHigh);
        float lateralOffset = UnityEngine.Random.Range(latLow, latHigh);

        Vector3 origin = new Vector3(_centreX, targetY, _centreZ) + _fwd * (spawnDepth + legExtensionExtraDepth);
        _leftBall = SpawnBall(origin - _right * lateralOffset, ColExtension);
        _rightBall = SpawnBall(origin + _right * lateralOffset, ColExtension);
        _pairActive = true;

        StartCoroutine(PulseIn(_leftBall));
        StartCoroutine(PulseIn(_rightBall));
    }

    GameObject SpawnBall(Vector3 pos, Color col)
    {
        var go = Instantiate(ballPrefab, pos, Quaternion.identity, transform);
        var pb = go.GetComponent<PhysioBall>();
        if (pb != null) pb.Setup(this);
        SetColor(go, col);
        if (colorCycleSpeed > 0f) StartCoroutine(CycleColor(go, col));
        return go;
    }

    // ── Hit handling ──────────────────────────────────────────────────────

    public void HandleBallHit(GameObject ballObj)
    {
        // ── Calibration ───────────────────────────────────────────────
        if (_phase == GamePhase.Calibration)
        {
            int idx = _calibCubesMain.IndexOf(ballObj);
            if (idx >= 0)
            {
                Vector3 pos = ballObj.transform.position;
                _calibCubesMain[idx] = null;
                _calibHitMain++;
                if (_currentCalibDirection == CalibDirection.Up) _calibHitsUp.Add(pos.y);
                else if (_currentCalibDirection == CalibDirection.Down) _calibHitsDown.Add(pos.y);
                else if (_currentCalibDirection == CalibDirection.Lateral) _calibHitsLeft.Add(pos.x);
                Destroy(ballObj); PlayHit(true); return;
            }
            idx = _calibCubesRight.IndexOf(ballObj);
            if (idx >= 0)
            {
                Vector3 pos = ballObj.transform.position;
                _calibCubesRight[idx] = null;
                _calibHitRight++;
                _calibHitsRight.Add(pos.x);
                Destroy(ballObj); PlayHit(true); return;
            }
            return;
        }

        // ── Flutter kick (single) ─────────────────────────────────────
        if (_singleActive)
        {
            bool isActive = (ballObj == _leftBall || ballObj == _rightBall);
            if (!isActive) return;

            Vector3 hitPos = ballObj.transform.position;
            TrackHit(hitPos);
            PlayHit(true);

            var b = ballObj == _leftBall ? _leftBall : _rightBall;
            if (ballObj == _leftBall) _leftBall = null;
            if (ballObj == _rightBall) _rightBall = null;
            StartCoroutine(FlashAndDestroy(b));

            _singleActive = false;
            _repsCompleted++;
            _repsHit++;
            return;
        }

        // ── Pair (knee raises / extensions) ──────────────────────────
        if (!_pairActive) return;

        bool isLeft = (ballObj == _leftBall);
        bool isRight = (ballObj == _rightBall);
        if (!isLeft && !isRight) return;

        TrackHit(ballObj.transform.position);
        PlayHit(true);

        if (isLeft && !_leftHit) { _leftHit = true; var b = _leftBall; _leftBall = null; StartCoroutine(FlashAndDestroy(b)); }
        if (isRight && !_rightHit) { _rightHit = true; var b = _rightBall; _rightBall = null; StartCoroutine(FlashAndDestroy(b)); }

        if (_leftHit && _rightHit)
        {
            _pairActive = false;
            _repsCompleted++;
            _repsHit++;
        }
    }

    void TrackHit(Vector3 hitPos)
    {
        if (hitPos.y > _sessionHighestHitY) _sessionHighestHitY = hitPos.y;
        if (hitPos.y < _sessionLowestHitY) _sessionLowestHitY = hitPos.y;
        float lateralOffset = Mathf.Abs(Vector3.Dot(
            hitPos - new Vector3(_centreX, hitPos.y, _centreZ), _right));
        if (lateralOffset > _sessionFurthestHitX) _sessionFurthestHitX = lateralOffset;
    }

    // ── Session end ───────────────────────────────────────────────────────

    void EndSession()
    {
        _phase = GamePhase.Finished;
        _targetOpacity = normalOpacity;
        DestroyPair();
        DestroySingle();
        StartCoroutine(FadeMusic(musicVolume, 0f, 2f));

        float vertROM = _sessionHighestHitY > float.MinValue
            ? _sessionHighestHitY - _sessionLowestHitY : 0f;
        float latROM = _sessionFurthestHitX * 2f;

        float successRate = _totalReps > 0 ? (float)_totalHit / _totalReps : 0f;

        // highReach = knee raise phase, wideSpread = leg extension phase
        // Each phase runs repsPerPhase reps; hits are split evenly across phases
        // (we track total hits only, so approximate 50/50 split per phase)
        int kneeRaiseReps = repsPerPhase;
        int extensionReps = repsPerPhase;
        int kneeRaiseHits = Mathf.Min(_totalHit, kneeRaiseReps);        // hits up to knee raise quota
        int extensionHits = Mathf.Max(0, _totalHit - kneeRaiseHits);    // remainder goes to extensions

        var session = new SessionResult
        {
            sessionId = Guid.NewGuid().ToString("N")[..8],
            userId = GetUserId(),
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            startTimestampMs = _sessionStartMs,                                       // real start
            endTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),        // real end
            totalReps = _totalReps,
            successfulReps = _totalHit,
            overallSuccessRate = successRate,
            peakRomUtilization = _calibVertRange > 0f ? Mathf.Clamp01(vertROM / _calibVertRange) : 0f,
            averageRomUtilization = _calibVertRange > 0f ? Mathf.Clamp01((vertROM * 0.7f) / _calibVertRange) : 0f,
            formScore = successRate,
            formGrade = ScoreToGrade(successRate),
            difficultyLevel = 1,
            phrases = new List<PhraseResult>(),
            // Knee raises = "high reach", leg extensions = "wide spread"
            highReachAttempts = kneeRaiseReps,
            highReachSuccesses = kneeRaiseHits,
            wideSpreadAttempts = extensionReps,
            wideSpreadSuccesses = extensionHits,
        };
        session.highReachRate = session.highReachAttempts > 0 ? (float)session.highReachSuccesses / session.highReachAttempts : 0f;
        session.wideSpreadRate = session.wideSpreadAttempts > 0 ? (float)session.wideSpreadSuccesses / session.wideSpreadAttempts : 0f;

        // Attach calibrated ROM so the backend has the patient's baseline
        session.calibVertRangeM = _calibVertRange;
        session.calibLatRangeM = Mathf.Abs(_romXMin) + _romXMax;
        session.vertRomHitM = vertROM;
        session.latRomHitM = latROM;

        if (RehabDataManager.Instance != null)
            RehabDataManager.Instance.SaveSession(session);

        ShowEndScreen(vertROM, latROM);
    }

    float _calibVertRange => Mathf.Abs(_romYMax - _romYMin);

    void ShowEndScreen(float vertROM, float latROM)
    {
        if (scoreCanvas == null) return;

        if (hmdTransform != null)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(hmdTransform.forward, Vector3.up).normalized;
            scoreCanvas.transform.position = hmdTransform.position + fwd * 1.2f + Vector3.up * -0.1f;
            scoreCanvas.transform.LookAt(hmdTransform.position);
            scoreCanvas.transform.Rotate(0, 180, 0);
        }

        scoreCanvas.SetActive(true);

        if (scoreText != null)
            scoreText.text = $"Score: {_totalHit} / {_totalReps}";
    }

    // ── Reset ─────────────────────────────────────────────────────────────

    public void ResetGame()
    {
        StopAllCoroutines();
        _phase = GamePhase.Idle;
        _pairActive = false;
        _singleActive = false;
        DestroyPair();
        DestroySingle();
        foreach (var c in _calibCubesMain) if (c != null) Destroy(c);
        foreach (var c in _calibCubesRight) if (c != null) Destroy(c);
        _calibCubesMain.Clear();
        _calibCubesRight.Clear();
        _repsCompleted = _repsHit = _totalReps = _totalHit = 0;
        _targetOpacity = normalOpacity;
        if (_musicSource != null) { _musicSource.Stop(); _musicSource.volume = 0f; }
        if (scoreCanvas != null) scoreCanvas.SetActive(false);
        if (instructionsCanvas != null) instructionsCanvas.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    void ShowHint(string msg)
    {
        if (instructionsText != null) instructionsText.text = msg;
        if (instructionsCanvas != null) instructionsCanvas.SetActive(!string.IsNullOrEmpty(msg));
    }

    string ScoreToGrade(float s) =>
        s >= 0.90f ? "A" : s >= 0.75f ? "B" : s >= 0.55f ? "C" : "D";

    string GetUserId()
    {
        try
        {
            var auth = Firebase.Auth.FirebaseAuth.GetAuth(Firebase.FirebaseApp.DefaultInstance);
            if (auth != null && auth.CurrentUser != null) return auth.CurrentUser.UserId;
        }
        catch { }
        return "ampcTUbGF3edyN95CG7UrEq3Ask2";
    }

    void PlayHit(bool success)
    {
        if (_audioSource == null) return;
        if (success && hitSound != null) _audioSource.PlayOneShot(hitSound, hitVolume);
        if (!success && missSound != null) _audioSource.PlayOneShot(missSound, hitVolume * 0.4f);
    }

    void SetColor(GameObject go, Color col)
    {
        if (go == null) return;
        var rend = go.GetComponentInChildren<Renderer>();
        if (rend == null) return;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", col);
        block.SetColor("_EmissionColor", col * glowIntensity);
        rend.SetPropertyBlock(block);
    }

    IEnumerator FlashAndDestroy(GameObject ball)
    {
        if (ball == null) yield break;
        Color burstColor = Color.white;
        var rend = ball.GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var readBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(readBlock);
            burstColor = readBlock.GetColor("_BaseColor");
            if (burstColor == Color.clear) burstColor = Color.white;
        }
        SpawnGlowBurst(ball.transform.position, burstColor);
        SetColor(ball, Color.white);
        float t = 0f;
        Vector3 start = ball.transform.localScale;
        while (t < 1f && ball != null)
        {
            t += Time.deltaTime * pulseSpeed * 1.8f;
            ball.transform.localScale = Vector3.Lerp(start, Vector3.zero, t);
            yield return null;
        }
        if (ball != null) Destroy(ball);
    }

    IEnumerator CycleColor(GameObject go, Color baseCol)
    {
        Color.RGBToHSV(baseCol, out float hue, out float sat, out float val);
        float t = 0f;
        while (go != null)
        {
            t += Time.deltaTime * colorCycleSpeed;
            float hueShift = Mathf.Sin(t * Mathf.PI * 2f) * 0.04f;
            float glowPulse = 1f + Mathf.Sin(t * Mathf.PI * 2f * 1.5f) * 0.5f;
            Color cycled = Color.HSVToRGB((hue + hueShift + 1f) % 1f, sat, val);
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", cycled);
                block.SetColor("_EmissionColor", cycled * glowIntensity * glowPulse);
                rend.SetPropertyBlock(block);
            }
            yield return null;
        }
    }

    void SpawnGlowBurst(Vector3 position, Color color)
    {
        if (particlePrefab != null)
        {
            var ps = Instantiate(particlePrefab, position, Quaternion.identity);
            var p = ps.GetComponent<ParticleSystem>();
            if (p != null) { var main = p.main; main.startColor = color; }
            Destroy(ps, 2f);
            return;
        }
        var go = new GameObject("HitBurst");
        go.transform.position = position;
        var ps2 = go.AddComponent<ParticleSystem>();
        var main2 = ps2.main;
        main2.startLifetime = 0.6f;
        main2.startSpeed = 1.8f;
        main2.startSize = 0.06f;
        main2.startColor = color;
        main2.gravityModifier = 0.1f;
        main2.loop = false;
        main2.playOnAwake = false;
        main2.maxParticles = 40;
        var emission = ps2.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });
        var shape = ps2.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;
        var col = ps2.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(color,       0.3f),
                new GradientColorKey(color,       1f) },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.8f, 0.3f),
                new GradientAlphaKey(0f,   1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);
        var sizeOL = ps2.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        ps2.Play();
        Destroy(go, 2f);
    }

    IEnumerator PulseIn(GameObject ball)
    {
        if (ball == null) yield break;
        Vector3 target = Vector3.one * 0.14f;
        float t = 0f;
        while (t < 1f)
        {
            if (ball == null) yield break;
            t += Time.deltaTime * pulseSpeed;
            ball.transform.localScale = Vector3.Lerp(Vector3.zero, target * pulseScale, t);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            if (ball == null) yield break;
            t += Time.deltaTime * pulseSpeed;
            ball.transform.localScale = Vector3.Lerp(target * pulseScale, target, t);
            yield return null;
        }
    }

    IEnumerator FadeMusic(float from, float to, float dur)
    {
        if (_musicSource == null || backgroundMusic == null) yield break;
        if (!_musicSource.isPlaying)
        {
            _musicSource.clip = backgroundMusic;
            _musicSource.volume = from;
            _musicSource.Play();
        }
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(from, to, e / dur);
            yield return null;
        }
        _musicSource.volume = to;
        if (to <= 0f) _musicSource.Stop();
    }
}