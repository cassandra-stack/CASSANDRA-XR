using UnityEngine;
using UnityEngine.XR;

public class ForceFFR : MonoBehaviour
{
    private void Start()
    {
        OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
        // OVRManager.eyeTrackedFoveatedRenderingEnabled = true;
    }
}
