using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class ProgressPanelAnimator : MonoBehaviour
{
    [Header("Animation settings")]
    public float delayBeforeFade = 0.3f;
    public float fadeDuration = 0.5f;
    public float scaleShrink = 0.9f;

    private CanvasGroup cg;
    private Vector3 initialScale;
    private Coroutine fadeRoutine;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        initialScale = transform.localScale;
    }

    /// <summary>
    /// Démarre une animation de fade-out avec shrink.
    /// </summary>
    public void FadeOutAndDisable()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // Petite pause avant le fade
        yield return new WaitForSeconds(delayBeforeFade);

        float t = 0f;
        float startAlpha = cg.alpha;
        Vector3 startScale = initialScale;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);

            // Fade alpha
            cg.alpha = Mathf.Lerp(startAlpha, 0f, k);

            // Shrink scale
            transform.localScale = Vector3.Lerp(startScale, startScale * scaleShrink, k);

            yield return null;
        }

        // Fin : désactive le panneau
        cg.alpha = 0f;
        transform.localScale = startScale * scaleShrink;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Réinitialise le panneau avant un nouveau chargement.
    /// </summary>
    public void ResetPanel()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        cg.alpha = 1f;
        transform.localScale = initialScale;
        gameObject.SetActive(true);
    }
}
