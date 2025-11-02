using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class SessionDataController : MonoBehaviour
{
    [Header("Services API")]
    public StudyService studyService;

    [Header("Runtime State (SO)")]
    public StudyRuntimeSO studyState;

    [Header("Cache")]
    [Tooltip("Par défaut: Application.persistentDataPath")]
    public string customCachePath = null;

    public string LastStudyCode { get; private set; }
    public bool LastIsVr { get; private set; }

    public event Action OnReloadRequested;
    public event Action<List<VrdfAsset>, string> OnStudiesReady;
    public event Action<float, int, int, string> OnDownloadProgress;
    public event Action<string> OnDownloadCompleted;
    public event Action<string> OnError;

    private string _cachePath;
    private List<VrdfAsset> _vrdfAssets = new List<VrdfAsset>();
    private bool _wsSubscribed = false;
    private string _lastStudyCodeFromWs;
    private bool _lastIsVrFromWs;

    private void Awake()
    {
        _cachePath = string.IsNullOrEmpty(customCachePath)
            ? Application.persistentDataPath
            : customCachePath;

        if (!Directory.Exists(_cachePath))
            Directory.CreateDirectory(_cachePath);
    }

    private void OnEnable()
    {
        TrySubscribeToWs();
    }

    private void OnDisable()
    {
        TryUnsubscribeFromWs();
    }

    private void Update()
    {
        if (!_wsSubscribed)
            TrySubscribeToWs();
    }

    private void TrySubscribeToWs()
    {
        if (_wsSubscribed) return;
        if (PusherClient.Instance == null) return;

        PusherClient.Instance.OnVrStatusChanged += HandleVrStatusChanged;
        _wsSubscribed = true;
        Debug.Log("[SessionDataController] WS subscribed.");
    }

    private void TryUnsubscribeFromWs()
    {
        if (!_wsSubscribed) return;
        if (PusherClient.Instance != null)
            PusherClient.Instance.OnVrStatusChanged -= HandleVrStatusChanged;

        _wsSubscribed = false;
        Debug.Log("[SessionDataController] WS unsubscribed.");
    }

    private void HandleVrStatusChanged(PusherClient.VrStatusPayload payload)
    {
        bool changedStudy = (_lastStudyCodeFromWs != payload.study_code);
        bool changedVr    = (_lastIsVrFromWs    != payload.is_vr);

        if (!changedStudy && !changedVr)
        {
            Debug.Log("[SessionDataController] WS event but nothing relevant changed → skip.");
            return;
        }

        _lastStudyCodeFromWs = payload.study_code;
        _lastIsVrFromWs = payload.is_vr;

        if (studyState != null) studyState.Clear();

        Debug.Log("[SessionDataController] WS change detected → request reload.");
        OnReloadRequested?.Invoke();
    }

    /// <summary>
    /// Lance tout le pipeline de données (fetch study + download assets).
    /// defaultCode = modalité choisie (dropdown) utilisée par LoadVolume ensuite.
    /// </summary>
    public void BeginLoad(string defaultCode)
    {
        if (studyState != null) studyState.Clear();
        StartCoroutine(RunDataPipeline(defaultCode));
    }

    private IEnumerator RunDataPipeline(string defaultCode)
    {
        List<StudyForUnity> studiesResult = null;
        bool fetchDone = false;

        if (studyService == null)
        {
            OnError?.Invoke("[SessionDataController] StudyService non assigné.");
            yield break;
        }

        yield return StartCoroutine(
            studyService.FetchStudies((studies, codeFromService) =>
            {
                if (studies == null || studies.Count == 0)
                {
                    Debug.LogError("[SessionDataController] No studies fetched from API.");
                    _vrdfAssets = new List<VrdfAsset>();
                }
                else
                {
                    var study = studies[0];
                    _vrdfAssets = study.vrdfAssets ?? new List<VrdfAsset>();

                    LastStudyCode = study.code;
                    LastIsVr      = study.isVr;
                }

                studiesResult = studies;
                fetchDone = true;
            }, defaultCode)
        );

        if (!fetchDone)
            Debug.LogWarning("[SessionDataController] Fetch finished but fetchDone=false (callback non appelé ?)");

        if (_vrdfAssets == null || _vrdfAssets.Count == 0)
        {
            OnError?.Invoke("[SessionDataController] Aucune ressource VRDF trouvée dans la study.");
            yield break;
        }

        OnStudiesReady?.Invoke(_vrdfAssets, defaultCode);

        var study = (studiesResult != null && studiesResult.Count > 0) ? studiesResult[0] : null;
        if (studyState != null)
        {
            studyState.Apply(study);
        }

        yield return StartCoroutine(DownloadAllVrdfWithProgress(_vrdfAssets));

        if (_downloadFailed)
            yield break;

        Debug.Log($"[VRDF] persistentDataPath = {Application.persistentDataPath}");
        foreach (var f in Directory.GetFiles(Application.persistentDataPath, "*.vrdf", SearchOption.AllDirectories))
        {
            var fi = new System.IO.FileInfo(f);
            Debug.Log($"[VRDF] trouvé: {f}  ({fi.Length} bytes)");
        }

        OnDownloadCompleted?.Invoke(defaultCode);
    }

    private bool _downloadFailed = false;

private IEnumerator DownloadAllVrdfWithProgress(List<VrdfAsset> assets)
{
    _downloadFailed = false;

    int total = assets.Count;
    int done = 0;

    for (int i = 0; i < total; i++)
    {
        var asset    = assets[i];
        string fileName = asset.filename;
        string destPath = Path.Combine(_cachePath, fileName);

        string dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(destPath))
        {
            float fakeDuration = 0.4f, t = 0f;
            while (t < fakeDuration)
            {
                t += Time.deltaTime;
                float fake = Mathf.Clamp01(t / fakeDuration);
                float globalProgress = (done + fake) / total;
                OnDownloadProgress?.Invoke(globalProgress, done + 1, total, fileName);
                yield return null;
            }
        }
        else
        {
            Debug.Log($"[SessionDataController] Downloading {fileName} from {asset.downloadUrl}");

            string tempPath = destPath + ".part";
            if (File.Exists(tempPath)) File.Delete(tempPath);

            using (UnityWebRequest www = new UnityWebRequest(asset.downloadUrl, UnityWebRequest.kHttpVerbGET))
            {
                www.certificateHandler = new CertsHandler();
                www.downloadHandler    = new DownloadHandlerFile(tempPath);

                var op = www.SendWebRequest();
                while (!op.isDone)
                {
                    float fileProgress  = Mathf.Clamp01(www.downloadProgress);
                    float globalProgress = (done + fileProgress) / total;
                    OnDownloadProgress?.Invoke(globalProgress, done + 1, total, fileName);
                    yield return null;
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    string err = $"[SessionDataController] Error downloading {fileName}: {www.error}";
                    Debug.LogError(err);
                    OnError?.Invoke(err);
                    _downloadFailed = true;
                    yield break;
                }
            }

            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(tempPath, destPath);
            Debug.Log($"[SessionDataController] Saved {fileName} to cache {destPath}");
        }

        done++;
        float finalGlobal = (float)done / total;
        OnDownloadProgress?.Invoke(finalGlobal, done, total, fileName);
        yield return null;
    }
}
}
