using System;
using System.Collections;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class PhysioBallGenerator : MonoBehaviour
{
    [Header("References")]
    public LegSideSelector sideSelector;
    public Transform hmdTransform;
    public GameObject ballPrefab;

    [Header("Passthrough Transparency")]
    public OVRPassthroughLayer passthroughLayer;
    [Tooltip("Passthrough opacity when game is active (0=fully transparent, 1=fully visible)")]
    [Range(0f, 1f)]
    public float gameOpacity = 0.4f;
    [Tooltip("Passthrough opacity when game is NOT active")]
    [Range(0f, 1f)]
    public float normalOpacity = 1f;
    public float opacityFadeSpeed = 2f;

    [Header("Spawn Settings")]
    [Tooltip("Minimum distance in front of player")]
    public float minSpawnDistance = 0.3f;
    [Tooltip("Maximum distance in front of player")]
    public float maxSpawnDistance = 1.0f;
    public float horizontalSpread = 0.4f;
    [Tooltip("Delay in seconds between a cube disappearing and the next one spawning")]
    public float spawnDelay = 0.8f;

    [Header("Height Progression")]
    public float startVerticalOffset = -0.95f;
    public float heightStepPerBall = 0f;
    public float maxVerticalOffset = -0.6f;

    [Header("Difficulty Progression")]
    public int startHitsToDestroy = 1;
    public int hitsIncreasePerBall = 0;
    public int maxHitsToDestroy = 1;

    [Header("Visual Feedback")]
    public float pulseScale = 1.4f;
    public float pulseSpeed = 12f; // faster

    // ── Internal state ────────────────────────────────────────────────────────
    private GameObject _currentActiveBall;
    private int _currentHits = 0;
    private bool _gameStarted = false;
    private bool _isResetting = false;
    private bool _ballBeingDestroyed = false;
    private int _ballsSpawned = 0;
    private int _hitsToDestroy = 1;
    private float _currentVerticalOffset;
    private bool _spawnLeft = true;

    private static readonly float[] _hues = { 0f, 0.08f, 0.15f, 0.33f, 0.55f, 0.66f, 0.75f, 0.9f };
    private int _hueIndex = 0;

    private float _targetOpacity;
    private float _currentOpacity;

    void Start()
    {
        Debug.Log($"[game] Start() — sideSelector={(sideSelector == null ? "NULL" : "OK")} hmdTransform={(hmdTransform == null ? "NULL" : "OK")} ballPrefab={(ballPrefab == null ? "NULL" : "OK")} passthroughLayer={(passthroughLayer == null ? "NULL" : "OK")}");
        _targetOpacity = normalOpacity;
        _currentOpacity = normalOpacity;
        ApplyOpacity(_currentOpacity);
    }

    void Update()
    {
        // Smoothly animate passthrough opacity
        if (passthroughLayer != null)
        {
            _currentOpacity = Mathf.Lerp(_currentOpacity, _targetOpacity, Time.deltaTime * opacityFadeSpeed);
            ApplyOpacity(_currentOpacity);
        }

        if (!_gameStarted)
        {
            if (sideSelector == null)
            {
                if (Time.frameCount % 120 == 0)
                    Debug.LogWarning("[game] sideSelector is NULL — assign it in the Inspector!");
                return;
            }

            if (Time.frameCount % 60 == 0)
                Debug.Log($"[game] Waiting... sideLocked={sideSelector.sideLocked} currentSide={sideSelector.currentSide}");

            if (sideSelector.sideLocked)
            {
                Debug.Log($"[game] Side is locked ({sideSelector.currentSide}) — starting game!");
                _gameStarted = true;
                _isResetting = false;
                _targetOpacity = gameOpacity;
                _spawnLeft = (sideSelector.currentSide == LegSideSelector.LegSide.Right);
                StartCoroutine(SpawnAfterDelay(spawnDelay));
            }
        }
    }

    void ApplyOpacity(float opacity)
    {
        if (passthroughLayer == null) return;
        passthroughLayer.textureOpacity = opacity;
    }

    public void HandleBallHit(GameObject ballObj)
    {
        if (_isResetting) return;
        if (_ballBeingDestroyed) return;
        if (ballObj != _currentActiveBall) return;

        _currentHits++;
        Debug.Log($"[game] Hit {_currentHits}/{_hitsToDestroy}");

        if (_currentHits >= _hitsToDestroy)
        {
            _ballBeingDestroyed = true;
            StartCoroutine(FlashAndDestroy(ballObj));
        }
        else
        {
            SetBallColor(_currentActiveBall);
            StartCoroutine(PulseScale(_currentActiveBall));
            TeleportBall(ballObj);
        }
    }

    public void ResetGame()
    {
        Debug.Log("[game] ResetGame called — stopping all coroutines, clearing ball.");

        _isResetting = true;
        StopAllCoroutines();

        if (_currentActiveBall != null)
        {
            Destroy(_currentActiveBall);
            _currentActiveBall = null;
        }

        _currentHits = 0;
        _gameStarted = false;
        _ballsSpawned = 0;
        _hueIndex = 0;
        _spawnLeft = true;
        _ballBeingDestroyed = false;

        // Restore full passthrough
        _targetOpacity = normalOpacity;

        Debug.Log("[game] Game reset complete.");
    }

    IEnumerator SpawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isResetting)
            SpawnNewBall();
    }

    void SpawnNewBall()
    {
        if (_isResetting) return;

        _ballBeingDestroyed = false;

        if (_currentActiveBall != null)
        {
            Destroy(_currentActiveBall);
            _currentActiveBall = null;
        }

        _currentHits = 0;

        if (hmdTransform == null) { Debug.LogError("[game] hmdTransform is NULL!"); return; }
        if (ballPrefab == null) { Debug.LogError("[game] ballPrefab is NULL!"); return; }

        // Height progression
        _currentVerticalOffset = Mathf.Min(
            startVerticalOffset + (_ballsSpawned * heightStepPerBall),
            maxVerticalOffset
        );

        // Difficulty progression
        _hitsToDestroy = Mathf.Min(
            startHitsToDestroy + (_ballsSpawned * hitsIncreasePerBall),
            maxHitsToDestroy
        );

        _ballsSpawned++;

        // Alternate left/right
        LegSideSelector.LegSide spawnSide = _spawnLeft
            ? LegSideSelector.LegSide.Left
            : LegSideSelector.LegSide.Right;
        _spawnLeft = !_spawnLeft;

        // Random distance between min and max
        float randomDistance = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 spawnPos = ComputeSpawnPosition(spawnSide, randomDistance);
        Debug.Log($"[game] Spawning ball #{_ballsSpawned} on {spawnSide} at {spawnPos:F2} dist={randomDistance:F2} — {_hitsToDestroy} hit(s) needed");

        _currentActiveBall = Instantiate(ballPrefab, spawnPos, Quaternion.identity, transform);

        PhysioBall pb = _currentActiveBall.GetComponent<PhysioBall>();
        if (pb != null)
            pb.Setup(this);
        else
            Debug.LogError("[game] ballPrefab missing PhysioBall component!", ballPrefab);

        SetBallColor(_currentActiveBall);
        StartCoroutine(SpawnPopScale(_currentActiveBall));
    }

    Vector3 ComputeSpawnPosition(LegSideSelector.LegSide side, float distance)
    {
        Vector3 forward = Vector3.ProjectOnPlane(hmdTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float sideSign = (side == LegSideSelector.LegSide.Left) ? -1f : 1f;

        return hmdTransform.position
             + forward * distance
             + right * (horizontalSpread * sideSign)
             + Vector3.up * _currentVerticalOffset;
    }

    void TeleportBall(GameObject ballObj)
    {
        if (ballObj == null) return;
        ballObj.transform.position += Vector3.up * 0.05f;
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    void SetBallColor(GameObject ball)
    {
        if (ball == null) return;
        Renderer rend = ball.GetComponentInChildren<Renderer>();
        if (rend == null) return;
        float hue = _hues[_hueIndex % _hues.Length];
        _hueIndex++;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetColor("_BaseColor", Color.HSVToRGB(hue, 0.9f, 1f));
        block.SetColor("_Color", Color.HSVToRGB(hue, 0.9f, 1f));
        rend.SetPropertyBlock(block);
    }

    void SetBallColorDirect(GameObject ball, Color color)
    {
        if (ball == null) return;
        Renderer rend = ball.GetComponentInChildren<Renderer>();
        if (rend == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        rend.SetPropertyBlock(block);
    }

    IEnumerator SpawnPopScale(GameObject ball)
    {
        if (ball == null) yield break;
        Vector3 original = ball.transform.localScale;
        ball.transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f) { if (ball == null || _isResetting) yield break; t += Time.deltaTime * pulseSpeed; ball.transform.localScale = Vector3.Lerp(Vector3.zero, original * pulseScale, t); yield return null; }
        t = 0f;
        while (t < 1f) { if (ball == null || _isResetting) yield break; t += Time.deltaTime * pulseSpeed; ball.transform.localScale = Vector3.Lerp(original * pulseScale, original, t); yield return null; }
        if (ball != null && !_isResetting) ball.transform.localScale = original;
    }

    IEnumerator PulseScale(GameObject ball)
    {
        if (ball == null) yield break;
        Vector3 original = ball.transform.localScale;
        float t = 0f;
        while (t < 1f) { if (ball == null || _isResetting) yield break; t += Time.deltaTime * pulseSpeed; ball.transform.localScale = Vector3.Lerp(original, original * pulseScale, t); yield return null; }
        t = 0f;
        while (t < 1f) { if (ball == null || _isResetting) yield break; t += Time.deltaTime * pulseSpeed; ball.transform.localScale = Vector3.Lerp(original * pulseScale, original, t); yield return null; }
        if (ball != null && !_isResetting) ball.transform.localScale = original;
    }

    IEnumerator FlashAndDestroy(GameObject ball)
    {
        if (ball == null || _isResetting) yield break;

        PhysioBall pb = ball.GetComponent<PhysioBall>();
        if (pb != null) pb.enabled = false;

        SetBallColorDirect(ball, Color.white);

        // Faster shrink to zero directly
        Vector3 original = ball.transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            if (ball == null || _isResetting) yield break;
            t += Time.deltaTime * pulseSpeed * 2f; // 2x faster
            ball.transform.localScale = Vector3.Lerp(original, Vector3.zero, t);
            yield return null;
        }

        if (_isResetting) yield break;

        Destroy(ball);
        _currentActiveBall = null;
        Debug.Log("[game] Ball destroyed — spawning next after delay.");

        yield return new WaitForSeconds(spawnDelay);
        if (!_isResetting)
            SpawnNewBall();
    }
}