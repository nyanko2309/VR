using UnityEngine;
using Meta.XR;                 // <-- REQUIRED (this is where PassthroughCameraAccess lives)
using Meta.XR.MRUtilityKit;   // optional but useful for intrinsics

public class CameraGrabberPCA : MonoBehaviour
{
    public PassthroughCameraAccess cameraAccess;  // drag the object with PassthroughCameraAccess
    public LegDetector legDetector;               // drag your leg detector object

    void Update()
    {
        if (cameraAccess == null || legDetector == null)
            return;

        Texture frame = cameraAccess.GetTexture();
        Texture2D tex2D = frame as Texture2D;

        if (tex2D != null)
        {
            Debug.Log("[CameraGrabberPCA] Frame received");
            legDetector.RunInference(tex2D);
        }
    }
}
