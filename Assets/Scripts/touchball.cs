// BallTrigger.cs
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class BallTrigger : MonoBehaviour
{
    public bool WasTouched { get; set; }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[BallTrigger] Hit by: {other.gameObject.name}");
        WasTouched = true;
    }

    public void Reset() => WasTouched = false;
}