using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[DisallowMultipleComponent]
public class DropdownVolumeLoader : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Le Dropdown UI qui contient les 4 options (t1c, t1n, t2f, t2w)")]
    public Dropdown dropdown;

    [Tooltip("Panel racine du chargement (celui avec le CanvasGroup/ProgressPanelAnimator)")]
    public GameObject progressPanelRoot;

    [Tooltip("Composant qui gère le fade/shrink du panel de progression")]
    public ProgressPanelAnimator progressAnimator;

    [Tooltip("Barre de progression lissée")]
    public ModernProgressBar progressBar;

    [Tooltip("Gros texte / statut du chargement (TMP)")]
    public TMP_Text progressLabelTMP;

    [Header("Scene / Volume refs")]
    [Tooltip("Le gestionnaire principal de session (celui qui fait le bootstrap au démarrage)")]
    public SessionVolumeLoader sessionInitializer;

    [Tooltip("Le volume actuel qui rend le cerveau (script VolumeDVR sur l'objet du volume)")]
    public VolumeDVR volumeDVR;

    [Tooltip("Placeholder cerveau provisoire affiché pendant chargement")]
    public GameObject brainPlaceholder;

    [Tooltip("Parent transform autour de VolumeDVRObject qui sert à l'anim de scale/reveal")]
    public Transform volumeDVRParent;

    [Header("Animation settings")]
    [Tooltip("Durée du faux chargement quand on change de modalité via le dropdown")]
    public float fakeLoadDuration = 0.6f;

    [Tooltip("Durée de l'apparition du cerveau médical (doit matcher SessionVolumeInitializer.revealDuration)")]
    public float revealDuration = 0.8f;

    [Tooltip("Facteur d'échelle de départ (volume minuscule au début du reveal)")]
    public float startScaleFactor = 0.02f;

    // on stocke l'échelle cible du parent pour pouvoir la restaurer après shrink
    private Vector3 _targetScale = Vector3.one;

    // pour éviter les chevauchements si l'utilisateur spamme le dropdown
    private Coroutine _currentRoutine;

    void Awake()
    {
        if (dropdown == null)
            dropdown = GetComponent<Dropdown>();

        if (volumeDVRParent != null)
            _targetScale = volumeDVRParent.localScale;

        dropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDestroy()
    {
        dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
    }

    private void OnDropdownChanged(int idx)
    {
        if (sessionInitializer == null || volumeDVR == null)
        {
            Debug.LogError("[DropdownVolumeLoader] Missing references (SessionVolumeInitializer / VolumeDVR).");
            return;
        }

        string modalityCode = dropdown.options[idx].text.Trim().ToLowerInvariant();
        Debug.Log($"[DropdownVolumeLoader] User selected modality: {modalityCode}");

        // Annule une précédente séquence si l'utilisateur reclique vite
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
        }

        _currentRoutine = StartCoroutine(ChangeVolumeRoutine(modalityCode));
    }

    private IEnumerator ChangeVolumeRoutine(string modalityCode)
    {
        if (progressPanelRoot != null)
            progressPanelRoot.SetActive(true);

        if (progressAnimator != null)
            progressAnimator.ResetPanel(); // remet alpha = 1, scale normale, active l'objet

        if (progressBar != null)
            progressBar.ResetProgress();

        if (progressLabelTMP != null)
            progressLabelTMP.text = "Initialisation du volume...\nPréparation du volume 3D";

        if (brainPlaceholder != null)
            brainPlaceholder.SetActive(true);

        if (volumeDVRParent != null)
            volumeDVRParent.localScale = _targetScale * startScaleFactor;

        if (sessionInitializer != null && volumeDVR != null)
            sessionInitializer.ForceRendererAlpha(volumeDVR.gameObject, 0f);

        //
        // 3. FAUX CHARGEMENT FLUIDE (barre + texte dynamiques)
        //
        float t = 0f;
        while (t < fakeLoadDuration)
        {
            t += Time.deltaTime;
            float fake = Mathf.Clamp01(t / fakeLoadDuration);

            string filenameGuess = modalityCode + "_lw.vrdf";

            // barre de progression
            if (progressBar != null)
            {
                progressBar.SetProgress(fake, filenameGuess, 1, 1);
            }

            // texte dynamique (reprend la logique du SessionVolumeInitializer)
            if (progressLabelTMP != null && sessionInitializer != null)
            {
                progressLabelTMP.text = sessionInitializer.BuildLoadingMessage(fake, 1, 1, filenameGuess);
            }

            yield return null;
        }


        volumeDVR.LoadVolumeByCode(modalityCode);

        if (progressLabelTMP != null)
        {
            progressLabelTMP.text = "Préparation du volume 3D...\nAnalyse et reconstruction...";
        }


        if (sessionInitializer != null)
        {
            yield return sessionInitializer.CrossfadeBrainToVolume(revealDuration);
        }
        else
        {
            if (brainPlaceholder != null) brainPlaceholder.SetActive(false);
            if (sessionInitializer != null && volumeDVR != null)
                sessionInitializer.ForceRendererAlpha(volumeDVR.gameObject, 1f);

            if (volumeDVRParent != null)
                volumeDVRParent.localScale = _targetScale;
        }


        if (progressAnimator != null)
        {
            progressAnimator.FadeOutAndDisable();
            yield return new WaitForSeconds(progressAnimator.fadeDuration + 0.1f);
        }
        else
        {
            if (progressPanelRoot != null)
                progressPanelRoot.SetActive(false);
        }

        if (progressPanelRoot != null && progressPanelRoot.activeSelf)
        {
            progressPanelRoot.SetActive(false);
        }

        Debug.Log($"[DropdownVolumeLoader] Volume {modalityCode} loaded and panel hidden ✅");
    }
}
