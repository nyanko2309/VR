using UnityEngine;
using TMPro;

public class HandScanManager : MonoBehaviour
{
    public GameObject scanUI;
    public GameObject virtualHand;

    public float scanDuration = 3f;
    private float timer = 0f;
    private bool handDetected = false;
    private bool scanComplete = false;

    void Update()
    {
        handDetected = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger) > 0.1f  || OVRInput.Get(OVRInput.Axis1D.SecondaryHandTrigger) > 0.1f;

        if (scanComplete) return;

        if (handDetected)
        {
            timer += Time.deltaTime;

            if (timer >= scanDuration)
            {
                scanComplete = true;
                scanUI.SetActive(false);
                virtualHand.SetActive(true);
            }
        }
        else
        {
            timer = 0f;
        }
    }

    public void SetHandDetected(bool detected)
    {
        handDetected = detected;
    }
}