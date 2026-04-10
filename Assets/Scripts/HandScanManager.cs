using UnityEngine;

public class HandScanManager : MonoBehaviour
{
    public GameObject scanUI;
    public GameObject virtualHand;

    public OVRHand leftHand;
    public OVRHand rightHand;

    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public float mirrorOffset = 0.3f;

    public float scanDuration = 3f;
    private float timer = 0f;
    private bool scanComplete = false;

    void Update()
    {
        bool leftTracked = leftHand != null && leftHand.IsTracked;
        bool rightTracked = rightHand != null && rightHand.IsTracked;

        Debug.Log("Left tracked: " + leftTracked + " | Right tracked: " + rightTracked);

        if (!scanComplete)
        {
            bool handDetected = leftTracked || rightTracked;

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
        if (scanComplete)
        {
            if (rightTracked && rightHandTransform != null)
            {
                Vector3 pos = rightHandTransform.position;
                Vector3 mirroredPos = new Vector3(-pos.x - mirrorOffset, pos.y, pos.z);

                virtualHand.transform.position = mirroredPos;

               
            }
            else if (leftTracked && leftHandTransform != null)
            {
                Vector3 pos = leftHandTransform.position;
                Vector3 mirroredPos = new Vector3(-pos.x + mirrorOffset, pos.y, pos.z);

                virtualHand.transform.position = mirroredPos;

               
            }
        }
    }
}