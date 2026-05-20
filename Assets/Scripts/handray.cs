using UnityEngine;
using Meta.XR;
[RequireComponent(typeof(LineRenderer))]
public class RayVisual : MonoBehaviour
{
    public Transform rayOrigin;
    public EnvironmentRaycastManager raycastManager;
    public float fallbackLength = 5f;

    private LineRenderer _lr;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.positionCount = 2;
        _lr.startWidth = 0.005f;
        _lr.endWidth = 0.002f;
        _lr.useWorldSpace = true;
    }

    void Update()
    {
        if (rayOrigin == null || raycastManager == null) return;

        Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);
        bool hit = raycastManager.Raycast(ray, out var hitInfo);

        _lr.SetPosition(0, ray.origin);
        _lr.SetPosition(1, hit ? hitInfo.point : ray.origin + ray.direction * fallbackLength);
    }
}