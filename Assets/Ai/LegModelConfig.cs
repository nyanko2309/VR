using UnityEngine;
using Unity.Sentis;

[CreateAssetMenu(fileName = "LegModelConfig", menuName = "AI/LegModelConfig")]
public class LegModelConfig : ScriptableObject
{
    public ModelAsset modelAsset; // Assign your .onnx file in Inspector
    public string inputName = "images";   // Set to your ONNX model's input name
    public string outputName = "output0"; // Set to your ONNX model's output name
    public int inputSize = 640;           // Set to your model's input size
    public float minJointScore = 0.35f;
    public bool flipY = true;
}
