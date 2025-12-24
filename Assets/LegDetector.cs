using UnityEngine;
using Unity.Sentis;

public class LegDetector : MonoBehaviour
{
    [Header("Model")]
    public ModelAsset modelAsset;          // Drag your ONNX asset here in the Inspector
    public string inputName = "images";    // Must match the model's input name
    public string outputName = "output0";  // Must match the model's output name
    public int inputSize = 640;            // 640x640 if that's what your model expects
    public BackendType backend = BackendType.GPUCompute; // or CPU if GPU not available

    private Model _model;
    private Worker _worker;
    private Tensor<float> _inputTensor;    // Shape: [1, 3, H, W]

    void Awake()
    {
        if (modelAsset == null)
        {
            Debug.LogError("[LegDetector] No modelAsset assigned!");
            return;
        }

        // Load the Sentis model and create a worker
        _model = ModelLoader.Load(modelAsset);
        _worker = new Worker(_model, backend);

        // Allocate input tensor in NCHW format: [N=1, C=3, H=inputSize, W=inputSize]
        _inputTensor = new Tensor<float>(new TensorShape(1, 3, inputSize, inputSize));

        Debug.Log("[LegDetector] Model loaded and worker created.");
    }

    /// <summary>
    /// Call this with a camera frame (Texture2D) to run the model once.
    /// </summary>
    public void RunInference(Texture2D cameraImage)
    {
        if (_worker == null)
        {
            Debug.LogWarning("[LegDetector] Worker not initialized.");
            return;
        }

        if (cameraImage == null)
        {
            Debug.LogWarning("[LegDetector] cameraImage is null.");
            return;
        }

        // Convert the camera texture into the input tensor (resizing to inputSize x inputSize)
        var transform = new TextureTransform().SetDimensions(inputSize, inputSize);
        TextureConverter.ToTensor(cameraImage, _inputTensor, transform);

        // Set input and run the model
        _worker.SetInput(inputName, _inputTensor);
        _worker.Schedule();
        //_worker.Complete();   // ensures inference is finished before reading output


        // Get the output tensor from the worker
        var outputTensor = _worker.PeekOutput(outputName) as Tensor<float>;
        if (outputTensor == null)
        {
            Debug.LogError("[LegDetector] Failed to get output tensor.");
            return;
        }

        // Read back to CPU so we can inspect the values
        var cpuCopy = outputTensor.ReadbackAndClone();
        float[] outputData = cpuCopy.DownloadToArray();

        // --- SIMPLE CHECK: is the model producing anything non-zero? ---
        bool anythingDetected = false;
        for (int i = 0; i < outputData.Length; i++)
        {
            if (Mathf.Abs(outputData[i]) > 0.0001f)
            {
                anythingDetected = true;
                break;
            }
        }

        // Also print the first few values so you can see something changing
        int maxToPrint = Mathf.Min(10, outputData.Length);
        string preview = "";
        for (int i = 0; i < maxToPrint; i++)
        {
            preview += outputData[i].ToString("F4") + (i < maxToPrint - 1 ? ", " : "");
        }

        Debug.Log($"[LegDetector] Output length: {outputData.Length}, first values: [{preview}]");

        if (anythingDetected)
        {
            Debug.Log("[LegDetector] ✅ AI is running and producing non-zero output (likely keypoints).");
        }
        else
        {
            Debug.Log("[LegDetector] ⚠ Output is all zeros or extremely small.");
        }

        // Clean up CPU copy (don't dispose outputTensor itself)
        cpuCopy.Dispose();
    }

    void OnDestroy()
    {
        if (_inputTensor != null)
        {
            _inputTensor.Dispose();
            _inputTensor = null;
        }

        if (_worker != null)
        {
            _worker.Dispose();
            _worker = null;
        }
    }
}
