using UnityEngine;
using TMPro;

public class HandScanManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject scanUI;
    public GameObject virtualHand;
    public TMP_Text instructionText;

    [Header("Targets")]
    public GameObject targetLeft;
    public GameObject targetRight;

    [Header("Hands")]
    public OVRHand leftHand;
    public OVRHand rightHand;
    public Transform leftHandTransform;
    public Transform rightHandTransform;

    [Header("Settings")]
    public float mirrorOffset = 0.3f;
    public float scanDuration = 3f;
    public float touchDistance = 0.20f;
    public int maxMoves = 5;
    public float instructionDuration = 4f;

    private float timer = 0f;
    private bool scanComplete = false;
    private int moveCount = 0;
    private bool canTriggerNextMove = true;

    private float instructionTimer = 0f;
    private bool instructionVisible = false;

    void Start()
    {
        if (targetLeft != null)
            targetLeft.SetActive(false);

        if (targetRight != null)
            targetRight.SetActive(false);

        if (virtualHand != null)
            virtualHand.SetActive(false);

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(false);
            instructionText.text = "Reach the glowing balls and touch them";
        }
    }

    void Update()
    {
        bool leftTracked = leftHand != null && leftHand.IsTracked;
        bool rightTracked = rightHand != null && rightHand.IsTracked;

        if (!scanComplete)
        {
            bool handDetected = leftTracked || rightTracked;

            if (handDetected)
            {
                timer += Time.deltaTime;

                if (timer >= scanDuration)
                {
                    scanComplete = true;

                    if (scanUI != null)
                        scanUI.SetActive(false);

                    if (virtualHand != null)
                        virtualHand.SetActive(true);

                    if (targetLeft != null)
                        targetLeft.SetActive(true);

                    if (targetRight != null)
                        targetRight.SetActive(true);

                    if (instructionText != null)
                    {
                        instructionText.text = "Reach the glowing balls and touch them";
                        instructionText.gameObject.SetActive(true);
                        instructionTimer = 0f;
                        instructionVisible = true;
                    }

                    PlaceMirroredTargets();
                }
            }
            else
            {
                timer = 0f;
            }

            return;
        }

        if (instructionVisible && instructionText != null)
        {
            instructionTimer += Time.deltaTime;

            if (instructionTimer >= instructionDuration)
            {
                instructionText.gameObject.SetActive(false);
                instructionVisible = false;
            }
        }

        Transform activeRealHand = null;

        if (rightTracked && rightHandTransform != null && virtualHand != null)
        {
            activeRealHand = rightHandTransform;

            Vector3 pos = rightHandTransform.position;
            Vector3 mirroredPos = new Vector3(-pos.x - mirrorOffset, pos.y, pos.z);
            virtualHand.transform.position = mirroredPos;
        }
        else if (leftTracked && leftHandTransform != null && virtualHand != null)
        {
            activeRealHand = leftHandTransform;

            Vector3 pos = leftHandTransform.position;
            Vector3 mirroredPos = new Vector3(-pos.x + mirrorOffset, pos.y, pos.z);
            virtualHand.transform.position = mirroredPos;
        }

        if (activeRealHand == null || targetLeft == null || targetRight == null || virtualHand == null)
            return;

        bool touchedAnyTarget =
            Vector3.Distance(activeRealHand.position, targetLeft.transform.position) <= touchDistance ||
            Vector3.Distance(activeRealHand.position, targetRight.transform.position) <= touchDistance ||
            Vector3.Distance(virtualHand.transform.position, targetLeft.transform.position) <= touchDistance ||
            Vector3.Distance(virtualHand.transform.position, targetRight.transform.position) <= touchDistance;

        if (touchedAnyTarget)
        {
            if (canTriggerNextMove)
            {
                moveCount++;
                canTriggerNextMove = false;

                if (moveCount < maxMoves)
                {
                    PlaceMirroredTargets();
                }
                else
                {
                    if (targetLeft != null) targetLeft.SetActive(false);
                    if (targetRight != null) targetRight.SetActive(false);

                    if (instructionText != null)
                    {
                        instructionText.gameObject.SetActive(true);
                        instructionText.text = "Good Job!";
                    }
                }
            }
        }
        else
        {
            canTriggerNextMove = true;
        }
    }

    private void PlaceMirroredTargets()
    {
        if (Camera.main == null || targetLeft == null || targetRight == null)
            return;

        Transform cam = Camera.main.transform;

        float randomSide = Random.Range(0.10f, 0.18f);
        float randomHeight = Random.Range(-0.10f, 0.02f);
        float randomDepth = Random.Range(0.45f, 0.65f);

        Vector3 centerPoint = cam.position + cam.forward * randomDepth + cam.up * randomHeight;
        Vector3 sideOffset = cam.right * randomSide;

        targetLeft.transform.position = centerPoint - sideOffset;
        targetRight.transform.position = centerPoint + sideOffset;

        targetLeft.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
        targetRight.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
    }
}