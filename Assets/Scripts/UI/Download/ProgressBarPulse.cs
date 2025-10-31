using UnityEngine;
using UnityEngine.UI;

public class ProgressBarPulse : MonoBehaviour
{
    public Image fillImage;
    public float pulseSpeed = 2f;
    public float pulseIntensity = 0.15f;

    void Update()
    {
        if (fillImage == null) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        Color c = fillImage.color;
        c.a = 0.85f + t * pulseIntensity;
        fillImage.color = c;
    }
}
