using System;
using System.Collections.Generic;
using UnityEngine;
// ─────────────────────────────────────────────────────────────────────────────
//  RehabDataStructures.cs
// ─────────────────────────────────────────────────────────────────────────────
[Serializable]
public class RepData
{
    public float smoothnessScore;
    public float timingAccuracy;
    public float romUtilization;
    public float holdStability;
    public float returnQuality;
    public float repScore;
    public bool wasSuccess;
    public string exerciseType;
    // Difficulty signature — what kind of cube was this rep?
    // 0 = easiest (low/close), 1 = hardest (high/wide)
    public float heightScore;   // 0-1, how high the cube spawned
    public float spreadScore;   // 0-1, how wide the pair was spread
    public bool isHighReach;   // true if heightScore > 0.65
    public bool isWideSpread;  // true if spreadScore > 0.65
}
[Serializable]
public class PhraseResult
{
    public string phraseType;
    public int totalReps;
    public int successfulReps;
    public float averageScore;
    public float peakRomUtilization;
    public int longestStreak;
    public long timestampMs;
}
[Serializable]
public class SessionResult
{
    public string sessionId;
    public string userId;
    public string date;
    public long startTimestampMs;
    public long endTimestampMs;
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
    public List<PhraseResult> phrases;
    // ROM proxy — hard cube success rates
    // These tell the clinician whether the patient can reach challenging positions
    public int highReachAttempts;    // cubes that spawned in upper height zone
    public int highReachSuccesses;   // how many of those were hit
    public int wideSpreadAttempts;   // cubes that spawned at wide spread
    public int wideSpreadSuccesses;  // how many of those were hit
    public float highReachRate;      // highReachSuccesses / highReachAttempts
    public float wideSpreadRate;     // wideSpreadSuccesses / wideSpreadAttempts
    // Absolute ROM in metres — populated by PhysioBallGenerator.EndSession()
    public float calibVertRangeM;   // patient's full calibrated vertical range
    public float calibLatRangeM;    // patient's full calibrated lateral range
    public float vertRomHitM;       // actual vertical range covered by hits this session
    public float latRomHitM;        // actual lateral range covered by hits this session
}