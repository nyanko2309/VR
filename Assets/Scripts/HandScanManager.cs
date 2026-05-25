using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class HandScanManager : MonoBehaviour
{
    public enum ImpairedSide { Left, Right }

    [Header("UI")]
    public GameObject scanUI;
    public GameObject virtualHand;
    public TMP_Text instructionText;
    public TMP_Text scoreText;

    [Header("Targets")]
    public GameObject targetLeft;
    public GameObject targetRight;

    [Header("Hands")]
    public OVRHand leftHand;
    public OVRHand rightHand;
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    [Header("Settings")]
    public float mirrorOffset = 0.15f;
    public float scanDuration = 3f;
    public int maxRounds = 10;
    public float instructionDuration = 4f;

    [Header("Touch Tuning")]
    public float ballRadius = 0.08f;
    public float handRadius = 0.10f;

    [Header("Tempo")]
    [Tooltip("BPM — balls wait 7 beats then reposition")]
    public float bpm = 84f;

    [Header("Sound")]
    public AudioClip bgMusicClip;
    public AudioClip popSoundClip;

    private AudioSource _bgMusic;
    private AudioSource _popSound;

    private float TouchThreshold => ballRadius + handRadius;
    private const int BeatsPerRound = 7;

    private float _beatInterval;
    private float _beatAccum = 0f;
    private int _beatCount = 0;

    private enum Phase { Scanning, WaitingToSpawn, BallsActive, GameOver }
    private Phase _phase = Phase.Scanning;

    private float _scanTimer = 0f;

    private int _round = 0;
    private bool _roundTouched = false;
    private int _successfulTouches = 0;
    private long _sessionStartMs;

    private float _instructionTimer = 0f;
    private bool _instructionVisible = false;

    private Vector3 _leftBasePos;
    private Vector3 _rightBasePos;
    private float _floatTime = 0f;

    private float _leftSpawnT = 0f;
    private float _rightSpawnT = 0f;
    private bool _leftSpawning = false;
    private bool _rightSpawning = false;

    private Material _leftMat;
    private Material _rightMat;
    private Color _leftBaseColor;
    private Color _rightBaseColor;

    void Awake()
    {
        _bgMusic = gameObject.AddComponent<AudioSource>();
        _bgMusic.clip = bgMusicClip;
        _bgMusic.loop = true;
        _bgMusic.playOnAwake = false;
        _bgMusic.spatialBlend = 0f;
        _bgMusic.volume = 0.4f;

        _popSound = gameObject.AddComponent<AudioSource>();
        _popSound.clip = popSoundClip;
        _popSound.loop = false;
        _popSound.playOnAwake = false;
        _popSound.spatialBlend = 0f;
        _popSound.volume = 1f;
    }

    void Start()
    {
        _beatInterval = 60f / bpm;
        _sessionStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (scoreText != null)
            scoreText.text = $"Score: 0 / {maxRounds}";

        SetupBallMaterial(targetLeft, ref _leftMat, ref _leftBaseColor);
        SetupBallMaterial(targetRight, ref _rightMat, ref _rightBaseColor);

        if (targetLeft != null) targetLeft.SetActive(false);
        if (targetRight != null) targetRight.SetActive(false);
        if (virtualHand != null) virtualHand.SetActive(false);

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(false);
            instructionText.text = "Reach the glowing balls and touch them";
        }

        // Music does NOT start here — it starts after scan
    }

    void SetupBallMaterial(GameObject ball, ref Material mat, ref Color baseColor)
    {
        if (ball == null) return;
        Renderer r = ball.GetComponent<Renderer>();
        if (r == null) return;
        mat = r.material;
        baseColor = mat.color;
        mat.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        bool leftTracked = leftHand != null && leftHand.IsTracked;
        bool rightTracked = rightHand != null && rightHand.IsTracked;

        MirrorHand(leftTracked, rightTracked);

        switch (_phase)
        {
            case Phase.Scanning: UpdateScanning(leftTracked, rightTracked); break;
            case Phase.WaitingToSpawn: UpdateWaitingToSpawn(); break;
            case Phase.BallsActive: UpdateBallsActive(); break;
            case Phase.GameOver: break;
        }

        UpdateInstructionTimer();
        UpdateSpawnAnimation();
    }

    void UpdateScanning(bool leftTracked, bool rightTracked)
    {
        if (leftTracked || rightTracked)
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= scanDuration)
            {
                if (scanUI != null) scanUI.SetActive(false);
                if (virtualHand != null) virtualHand.SetActive(true);

                if (instructionText != null)
                {
                    instructionText.text = "Reach the glowing balls and touch them";
                    instructionText.gameObject.SetActive(true);
                    _instructionTimer = 0f;
                    _instructionVisible = true;
                }

                // Music starts here, after scan completes
                if (_bgMusic != null && bgMusicClip != null)
                    _bgMusic.Play();

                _beatAccum = 0f;
                _phase = Phase.WaitingToSpawn;
                Debug.Log("[Game] Scan complete — music started");
            }
        }
        else
        {
            _scanTimer = 0f;
        }
    }

    void UpdateWaitingToSpawn()
    {
        _beatAccum += Time.deltaTime;
        if (_beatAccum >= _beatInterval)
        {
            _beatAccum = 0f;
            SpawnBalls();
        }
    }

    void UpdateBallsActive()
    {
        _floatTime += Time.deltaTime;
        float urgency = Mathf.Clamp01((float)_beatCount / BeatsPerRound);
        AnimateBalls(urgency);

        // Beat counter for repositioning if player misses
        _beatAccum += Time.deltaTime;
        if (_beatAccum >= _beatInterval)
        {
            _beatAccum -= _beatInterval;
            _beatCount++;
            if (_beatCount >= BeatsPerRound)
            {
                MissRound();
                return;
            }
        }

        // Touch detection every frame
        Vector3 vhPos = virtualHand != null ? virtualHand.transform.position : Vector3.zero;
        Vector3 rhPos = rightHandTransform != null ? rightHandTransform.position : Vector3.zero;
        Vector3 lhPos = leftHandTransform != null ? leftHandTransform.position : Vector3.zero;

        bool leftTouched =
            targetLeft != null && targetLeft.activeSelf &&
            (Vector3.Distance(vhPos, targetLeft.transform.position) < TouchThreshold ||
             Vector3.Distance(rhPos, targetLeft.transform.position) < TouchThreshold ||
             Vector3.Distance(lhPos, targetLeft.transform.position) < TouchThreshold);

        bool rightTouched =
            targetRight != null && targetRight.activeSelf &&
            (Vector3.Distance(vhPos, targetRight.transform.position) < TouchThreshold ||
             Vector3.Distance(rhPos, targetRight.transform.position) < TouchThreshold ||
             Vector3.Distance(lhPos, targetRight.transform.position) < TouchThreshold);

        if ((leftTouched || rightTouched) && !_roundTouched)
        {
            _roundTouched = true;
            HitRound();
        }
    }

    void HitRound()
    {
        _successfulTouches++;
        _round++;
        UpdateScoreText();

        Debug.Log($"[Game] HIT — score={_successfulTouches} round={_round}/{maxRounds}");

        if (targetLeft != null)
            SpawnPopEffect(targetLeft.transform.position, GetBallColor(targetLeft));
        if (targetRight != null)
            SpawnPopEffect(targetRight.transform.position, GetBallColor(targetRight));

        if (_popSound != null && popSoundClip != null)
            _popSound.PlayOneShot(popSoundClip);

        if (_round >= maxRounds)
            EndGame();
        else
            SpawnBalls();
    }

    void MissRound()
    {
        Debug.Log($"[Game] MISS — round={_round + 1}");
        _round++;
        UpdateScoreText();

        if (_round >= maxRounds)
            EndGame();
        else
            SpawnBalls();
    }

    void EndRound()
    {
        _round++;
        UpdateScoreText();

        if (_round >= maxRounds)
            EndGame();
        else
        {
            _phase = Phase.WaitingToSpawn;
            _beatAccum = 0f;
        }
    }

    void SpawnBalls()
    {
        if (Camera.main == null) return;

        _roundTouched = false;
        _beatCount = 0;
        _beatAccum = 0f;
        _floatTime = 0f;

        Transform cam = Camera.main.transform;

        float randomHeight = UnityEngine.Random.Range(-0.10f, 0.10f);
        float randomDepth = UnityEngine.Random.Range(0.45f, 0.65f);
        float spread = UnityEngine.Random.Range(0.12f, 0.22f);

        Vector3 center = cam.position + cam.forward * randomDepth + cam.up * randomHeight;
        Vector3 sideOffset = cam.right * spread;

        _leftBasePos = center - sideOffset;
        _rightBasePos = center + sideOffset;

        if (targetLeft != null)
        {
            targetLeft.transform.position = _leftBasePos;
            targetLeft.transform.localScale = Vector3.zero;
            targetLeft.SetActive(true);
            _leftSpawnT = 0f;
            _leftSpawning = true;
        }

        if (targetRight != null)
        {
            targetRight.transform.position = _rightBasePos;
            targetRight.transform.localScale = Vector3.zero;
            targetRight.SetActive(true);
            _rightSpawnT = 0f;
            _rightSpawning = true;
        }

        _phase = Phase.BallsActive;
        Debug.Log($"[Game] Balls spawned — round {_round + 1}/{maxRounds}");
    }

    void HideBalls()
    {
        if (targetLeft != null) targetLeft.SetActive(false);
        if (targetRight != null) targetRight.SetActive(false);
        ResetBallColors();
    }

    void AnimateBalls(float urgency)
    {
        if (targetLeft != null && targetLeft.activeSelf)
        {
            float floatY = Mathf.Sin(_floatTime * 1.8f) * 0.018f;
            float floatX = Mathf.Sin(_floatTime * 1.1f) * 0.006f;
            targetLeft.transform.position = _leftBasePos + new Vector3(floatX, floatY, 0f);
        }

        if (targetRight != null && targetRight.activeSelf)
        {
            float floatY = Mathf.Sin(_floatTime * 1.8f + 1f) * 0.018f;
            float floatX = Mathf.Sin(_floatTime * 1.1f + 0.5f) * 0.006f;
            targetRight.transform.position = _rightBasePos + new Vector3(floatX, floatY, 0f);
        }

        float pulseSpeed = Mathf.Lerp(2f, 10f, urgency);
        float pulse = (Mathf.Sin(_floatTime * pulseSpeed) + 1f) * 0.5f;
        float glowMin = Mathf.Lerp(0.3f, 2.0f, urgency);
        float glowMax = Mathf.Lerp(1.2f, 5.0f, urgency);

        Color leftColor = Color.Lerp(_leftBaseColor, Color.white, urgency);
        Color rightColor = Color.Lerp(_rightBaseColor, Color.white, urgency);

        if (_leftMat != null)
        {
            _leftMat.color = leftColor;
            _leftMat.SetColor("_EmissionColor", leftColor * Mathf.Lerp(glowMin, glowMax, pulse));
        }
        if (_rightMat != null)
        {
            _rightMat.color = rightColor;
            _rightMat.SetColor("_EmissionColor", rightColor * Mathf.Lerp(glowMin, glowMax, pulse));
        }
    }

    void MirrorHand(bool leftTracked, bool rightTracked)
    {
        if (Camera.main == null || virtualHand == null) return;

        if (rightTracked && rightHandTransform != null)
        {
            Vector3 localPos = Camera.main.transform.InverseTransformPoint(rightHandTransform.position);
            localPos.x = -localPos.x;
            virtualHand.transform.position = Camera.main.transform.TransformPoint(localPos);
            virtualHand.transform.rotation = rightHandTransform.rotation;
        }
        else if (leftTracked && leftHandTransform != null)
        {
            Vector3 localPos = Camera.main.transform.InverseTransformPoint(leftHandTransform.position);
            localPos.x = -localPos.x;
            virtualHand.transform.position = Camera.main.transform.TransformPoint(localPos);
            virtualHand.transform.rotation = leftHandTransform.rotation;
        }
    }

    void UpdateInstructionTimer()
    {
        if (!_instructionVisible || instructionText == null) return;
        _instructionTimer += Time.deltaTime;
        if (_instructionTimer >= instructionDuration)
        {
            instructionText.gameObject.SetActive(false);
            _instructionVisible = false;
        }
    }

    void UpdateSpawnAnimation()
    {
        if (_leftSpawning)
        {
            _leftSpawnT += Time.deltaTime / 0.25f;
            float s = EaseOutBack(Mathf.Clamp01(_leftSpawnT));
            if (targetLeft != null)
                targetLeft.transform.localScale = Vector3.one * Mathf.Lerp(0f, 0.06f, s);
            if (_leftSpawnT >= 1f) _leftSpawning = false;
        }

        if (_rightSpawning)
        {
            _rightSpawnT += Time.deltaTime / 0.25f;
            float s = EaseOutBack(Mathf.Clamp01(_rightSpawnT));
            if (targetRight != null)
                targetRight.transform.localScale = Vector3.one * Mathf.Lerp(0f, 0.06f, s);
            if (_rightSpawnT >= 1f) _rightSpawning = false;
        }
    }

    void EndGame()
    {
        _phase = Phase.GameOver;

        HideBalls();
        if (_bgMusic != null) _bgMusic.Stop();

        float successRate = maxRounds > 0 ? (float)_successfulTouches / maxRounds * 100f : 0f;

        string result =
            $"Session Complete!\n" +
            $"Score: {_successfulTouches} / {maxRounds}\n" +
            $"Success Rate: {successRate:F0}%";

        Debug.Log($"[Game] {result}");

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = result;
        }

        SaveSession();
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {_successfulTouches} / {_round}";
    }

    void ResetBallColors()
    {
        if (_leftMat != null)
        {
            _leftMat.color = _leftBaseColor;
            _leftMat.SetColor("_EmissionColor", _leftBaseColor * 0.3f);
        }
        if (_rightMat != null)
        {
            _rightMat.color = _rightBaseColor;
            _rightMat.SetColor("_EmissionColor", _rightBaseColor * 0.3f);
        }
    }

    Color GetBallColor(GameObject ball)
    {
        if (ball == null) return Color.cyan;
        Renderer r = ball.GetComponent<Renderer>();
        return r != null ? r.material.color : Color.cyan;
    }

    float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    void SpawnPopEffect(Vector3 position, Color color)
    {
        GameObject go = new GameObject("PopEffect");
        go.transform.position = position;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.02f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.01f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var rend = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default");

        if (shader != null)
        {
            rend.material = new Material(shader);
            rend.material.color = color;
        }

        ps.Play();
    }

    void SaveSession()
    {
        if (RehabDataManager.Instance == null)
        {
            Debug.LogWarning("[Save] RehabDataManager not found");
            return;
        }

        float successRate = maxRounds > 0 ? (float)_successfulTouches / maxRounds : 0f;

        Debug.Log($"[Save] hits={_successfulTouches} rounds={maxRounds} rate={successRate:F2}");

        var session = new SessionResult
        {
            sessionId = Guid.NewGuid().ToString(),
            date = DateTime.Now.ToString("yyyy-MM-dd"),
            startTimestampMs = _sessionStartMs,
            endTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            totalReps = maxRounds,
            successfulReps = _successfulTouches,
            overallSuccessRate = successRate,
            totalPhrases = 1,
            phrases = new List<PhraseResult>
            {
                new PhraseResult
                {
                    phraseType     = "HandScan",
                    totalReps      = maxRounds,
                    successfulReps = _successfulTouches,
                    averageScore   = successRate,
                    timestampMs    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }
            }
        };

        RehabDataManager.Instance.SaveSession(session);
    }
}