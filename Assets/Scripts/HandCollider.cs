// HandCollider.cs
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;


public class HandCollider : MonoBehaviour
{
    private SphereCollider _col;

    void Awake()
    {
        _col = gameObject.AddComponent<SphereCollider>();
        _col.isTrigger = true;
        _col.radius = 0.08f;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    void Update()
    {
        if (Time.frameCount % 30 == 0)
            Debug.Log($"[HandCollider] pos={transform.position}");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[HandCollider] Trigger hit: {other.gameObject.name}");

        BallTrigger bt = other.GetComponent<BallTrigger>();
        if (bt != null)
            bt.WasTouched = true;
    }
}