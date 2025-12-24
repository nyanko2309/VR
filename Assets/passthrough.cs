using System.Diagnostics;
using UnityEngine;

public class EnablePassthrough : MonoBehaviour
{
    void Start()
    {
        if (OVRManager.instance != null)
        {
            OVRManager.instance.isInsightPassthroughEnabled = true;
        }
        else
        {
        }
    }
}
