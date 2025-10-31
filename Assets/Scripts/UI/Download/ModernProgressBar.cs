using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModernProgressBar : MonoBehaviour
{
    [Header("References")]
    public Image fillImage;        // Image Filled Horizontal
    public TMP_Text statusText;    // Texte d'état (facultatif)

    [Header("Animation")]
    [Tooltip("Temps (en secondes) que met la barre pour rattraper une nouvelle valeur. Plus petit = plus nerveux, plus grand = plus smooth.")]
    public float smoothTime = 0.15f;

    // valeur cible envoyée par le loader (0..1)
    private float targetProgress01 = 0f;

    // valeur réellement affichée (0..1)
    private float displayedProgress01 = 0f;

    // vitesse interne utilisée par SmoothDamp
    private float velocity = 0f;

    void Update()
    {
        // SmoothDamp = interpolation critique (comme une inertie), super naturel à l'œil
        displayedProgress01 = Mathf.SmoothDamp(
            displayedProgress01,
            targetProgress01,
            ref velocity,
            smoothTime
        );

        // Clamp sécurité (surtout fin de course >1)
        displayedProgress01 = Mathf.Clamp01(displayedProgress01);

        if (fillImage != null)
        {
            fillImage.fillAmount = displayedProgress01;
        }

        // Affichage texte (affiche la target, pas le displayed, pour que le % soit vrai)
        if (statusText != null)
        {
            int pct = Mathf.RoundToInt(targetProgress01 * 100f);
            statusText.text = pct + " %";
        }
    }

    public void SetProgress(float progress01, string fileName, int index, int total)
    {
        targetProgress01 = Mathf.Clamp01(progress01);

        if (statusText != null && !string.IsNullOrEmpty(fileName))
        {
            int pct = Mathf.RoundToInt(targetProgress01 * 100f);
            statusText.text = $"{fileName} ({index}/{total}) — {pct}%";
        }
    }

    public void ResetProgress()
    {
        targetProgress01 = 0f;
        displayedProgress01 = 0f;
        velocity = 0f;

        if (fillImage != null)
            fillImage.fillAmount = 0f;

        if (statusText != null)
            statusText.text = "0 %";
    }
}
