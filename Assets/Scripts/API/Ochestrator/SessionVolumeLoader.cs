using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SessionVolumeLoader : MonoBehaviour
{
    [Header("Scene References (UI & Volume)")]
    public Dropdown dropdown;               // Source de la modalité (ex: "mri", "ct", ...)
    public VolumeDVR volumeDVR;             // Composant qui charge/affiche le volume
    public SessionDataController dataCtrl;  // NOUVEAU: référence au contrôleur API

    [Header("Progress UI")]
    public ModernProgressBar progressBar;
    public TMP_Text progressLabelTMP;
    public GameObject progressPanelRoot;
    public ProgressPanelAnimator progressAnimator;

    [Header("Loading Placeholders")]
    [Tooltip("Objet 3D 'cerveau' provisoire visible tant que le DVR n'est pas prêt.")]
    public GameObject brainPlaceholder;

    [Tooltip("Objet qui contient VolumeDVR + raymarch renderer (l'enfant).")]
    public GameObject volumeDVRObject;

    [Tooltip("Parent vide au-dessus du volumeDVRObject, utilisé pour l'animation de scale.")]
    public Transform volumeDVRParent;

    [Header("Transition Settings")]
    [Tooltip("Durée de l'apparition du volume (en secondes).")]
    public float revealDuration = 0.8f;

    [Tooltip("Facteur de départ (0 = point minuscule, 0.05 = petite noisette).")]
    public float startScaleFactor = 0.02f;

    // internes
    private Vector3 _targetParentScale = Vector3.one;
    private Coroutine _appearRoutine;

    private void Reset()
    {
        // Valeurs par défaut en cas d'ajout du composant
        startScaleFactor = 0.02f;
        revealDuration = 0.8f;
    }

    private void OnEnable()
    {
        if (dataCtrl != null)
        {
            dataCtrl.OnReloadRequested += HandleReloadRequestedByWs;
            dataCtrl.OnStudiesReady += HandleStudiesReady;
            dataCtrl.OnDownloadProgress += HandleDownloadProgress;
            dataCtrl.OnDownloadCompleted += HandleDownloadCompleted;
            dataCtrl.OnError += HandleError;
        }
    }

    private void OnDisable()
    {
        if (dataCtrl != null)
        {
            dataCtrl.OnReloadRequested -= HandleReloadRequestedByWs;
            dataCtrl.OnStudiesReady -= HandleStudiesReady;
            dataCtrl.OnDownloadProgress -= HandleDownloadProgress;
            dataCtrl.OnDownloadCompleted -= HandleDownloadCompleted;
            dataCtrl.OnError -= HandleError;
        }
    }

    private void Start()
    {
        if (dropdown == null || volumeDVR == null || dataCtrl == null)
        {
            Debug.LogError("[SessionVolumeLoader] Missing references in inspector (dropdown/volumeDVR/dataCtrl).");
            return;
        }
        if (volumeDVRParent == null)
        {
            Debug.LogError("[SessionVolumeLoader] volumeDVRParent is null. Assign it in inspector.");
            return;
        }

        // UI init
        if (progressLabelTMP != null)
            progressLabelTMP.text = "Initialisation du volume...\nPréparation de la session XR";
        if (progressPanelRoot) progressPanelRoot.SetActive(true);

        // état visuel initial
        _targetParentScale = volumeDVRParent.localScale;
        if (brainPlaceholder) brainPlaceholder.SetActive(true);
        if (volumeDVRObject) volumeDVRObject.SetActive(true);

        volumeDVRParent.localScale = _targetParentScale * startScaleFactor;
        ForceRendererAlpha(volumeDVRObject, 0f);

        // Lancer le pipeline de données via le contrôleur
        dataCtrl.BeginLoad(GetDefaultCode());
    }

    // =========================
    // Callbacks du contrôleur
    // =========================

    private void HandleReloadRequestedByWs()
    {
        // Préparer l’UI/visuels avant de relancer un chargement
        PrepareVisualsForReload();

        // Relance le pipeline (le "defaultCode" vient du dropdown actuel)
        dataCtrl.BeginLoad(GetDefaultCode());
    }

    private void HandleStudiesReady(List<VrdfAsset> assets, string defaultCode)
    {
        // On a la liste des assets → laisser la barre afficher "Récupération..." via progress
        // (Rien de spécifique ici, mais c’est le bon hook si tu veux réagir à la méta)
    }

    private void HandleDownloadProgress(float global01, int index, int total, string currentFile)
    {
        // Mettre à jour l’UI de progression
        if (progressBar != null)
            progressBar.SetProgress(global01, currentFile, index, total);

        if (progressLabelTMP != null)
            progressLabelTMP.text = BuildLoadingMessage(global01, index, total, currentFile);
    }

    private void HandleDownloadCompleted(string defaultCode)
    {
        // Tous les fichiers sont dispo → charger le volume
        volumeDVR.LoadVolumeByCode(defaultCode);

        if (progressLabelTMP != null)
            progressLabelTMP.text = "Préparation du volume 3D...\nAnalyse et reconstruction...";

        if (progressPanelRoot) progressPanelRoot.SetActive(false);

        // Apparition du vrai volume
        if (_appearRoutine != null) StopCoroutine(_appearRoutine);
        _appearRoutine = StartCoroutine(CrossfadeBrainToVolume(revealDuration));

        if (progressAnimator != null)
            progressAnimator.FadeOutAndDisable();

        Debug.Log("[SessionVolumeLoader] Volume chargé et affiché ✅");
    }

    private void HandleError(string message)
    {
        if (progressPanelRoot) progressPanelRoot.SetActive(true);
        if (progressLabelTMP != null)
            progressLabelTMP.text = message;

        Debug.LogError(message);
    }

    // =========================
    // Helpers UI / Animation
    // =========================

    private void PrepareVisualsForReload()
    {
        if (progressPanelRoot) progressPanelRoot.SetActive(true);

        if (progressLabelTMP != null)
            progressLabelTMP.text = "Mise à jour des données patient...\nSynchronisation XR";

        if (brainPlaceholder) brainPlaceholder.SetActive(true);

        // rendre le vrai volume invisible le temps du rechargement
        ForceRendererAlpha(volumeDVRObject, 0f);

        // remet l’échelle minuscule pour rejouer l’apparition
        volumeDVRParent.localScale = _targetParentScale * startScaleFactor;
    }

    private string GetDefaultCode()
    {
        if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
            return string.Empty;

        return dropdown.options[dropdown.value].text.Trim().ToLowerInvariant();
    }

    public string BuildLoadingMessage(float progress01, int index, int total, string filename)
    {
        int pct = Mathf.RoundToInt(progress01 * 100f);

        if (pct <= 5)
            return "Initialisation du volume...\nPréparation de la session XR";

        if (pct < 100)
            return $"Récupération des données patient...\n{filename}  ({index}/{total})\n{pct} %";

        return "Préparation du volume 3D...\nAnalyse et reconstruction...";
    }

    public IEnumerator CrossfadeBrainToVolume(float duration)
    {
        if (brainPlaceholder == null || volumeDVRObject == null || volumeDVRParent == null)
            yield break;

        Renderer[] placeholderRenderers = brainPlaceholder.GetComponentsInChildren<Renderer>(true);
        Renderer[] dvrRenderers         = volumeDVRObject.GetComponentsInChildren<Renderer>(true);

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float ease = Mathf.SmoothStep(0f, 1f, k);

            float alphaOut = 1f - ease;
            float alphaIn  = ease;

            // fade out du placeholder
            foreach (var r in placeholderRenderers)
            {
                if (r != null && r.material != null && r.material.HasProperty("_Color"))
                {
                    Color c = r.material.color;
                    c.a = alphaOut;
                    r.material.color = c;
                }
            }

            // fade in du vrai volume
            foreach (var r in dvrRenderers)
            {
                if (r != null && r.material != null && r.material.HasProperty("_Color"))
                {
                    Color c = r.material.color;
                    c.a = alphaIn;
                    r.material.color = c;
                }
            }

            float scaledFactor = Mathf.Lerp(startScaleFactor, 1f, ease);
            volumeDVRParent.localScale = _targetParentScale * scaledFactor;

            yield return null;
        }

        brainPlaceholder.SetActive(false);
        ForceRendererAlpha(volumeDVRObject, 1f);
        volumeDVRParent.localScale = _targetParentScale;
    }

    public void ForceRendererAlpha(GameObject go, float alpha)
    {
        if (go == null) return;
        var rends = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            if (r != null && r.material != null && r.material.HasProperty("_Color"))
            {
                Color c = r.material.color;
                c.a = alpha;
                r.material.color = c;
            }
        }
    }
}
