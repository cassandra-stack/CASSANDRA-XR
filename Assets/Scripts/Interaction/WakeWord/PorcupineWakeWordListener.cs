using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Android;
using Pv.Unity; // PorcupineManager

public class PorcupineWakeWordListener : MonoBehaviour
{
    [Header("Picovoice / Porcupine")]
    [SerializeField] private string accessKey = "ndia7zusUmtQe/m9qRri6u4lemMKpB1vDM4fntsstWnCknY6aL/Dwg==";

    [Header("Fichiers (dans StreamingAssets)")]
    // Chemins RELATIFS depuis StreamingAssets
    // Ex: Porcupine/keywords/cassandra_fr_android_v3_0_0.ppn
    [SerializeField] private string keywordPathAndroid = "Porcupine/keywords/cassandra_fr_android_v3_0_0.ppn";
    [SerializeField] private string keywordPathWindows = "Porcupine/keywords/cassandra_fr_windows_v3_0_0.ppn";
    [SerializeField] private string modelPath = "Porcupine/models/porcupine_params_fr.pv";

    [Header("Sensibilité (0..1)")]
    [Range(0f, 1f)] [SerializeField] private float customSensitivity = 0.6f;

    [Header("Fallback si pas de .ppn")]
    [SerializeField] private Porcupine.BuiltInKeyword builtInKeyword = Porcupine.BuiltInKeyword.PORCUPINE;

    [Header("Lifecycle")]
    [SerializeField] private bool autoStart = true;

    public event Action OnWakeWordDetected;

    private PorcupineManager porcupineManager;
    private bool isRunning = false;
    private bool isPaused = false;

    private void Start()
    {
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Permission micro
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            yield return null;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                Debug.LogError("[WakeWord] Microphone permission denied.");
                yield break;
            }
        }
#endif

        string relKeyword = SelectKeywordForThisPlatform();
        string relModel = modelPath;

        string absKeyword = null;
        string absModel = null;

        // Sur Android : copier StreamingAssets -> persistentDataPath (Porcupine exige un vrai fichier)
        yield return CopyFromStreamingAssets(relKeyword, p => absKeyword = p);
        yield return CopyFromStreamingAssets(relModel,    p => absModel   = p);

        Debug.Log($"[WakeWord] model={absModel}");
        Debug.Log($"[WakeWord] keyword={absKeyword}");

        bool hasMic = Permission.HasUserAuthorizedPermission(Permission.Microphone);
        Debug.Log($"[WakeWord] Mic permission? {hasMic}");
#if !UNITY_EDITOR && UNITY_ANDROID
        var devs = Microphone.devices;
        Debug.Log($"[WakeWord] Microphone.devices = {(devs != null ? string.Join(",", devs) : "null")}");
#endif

        Debug.Log($"[WakeWord] AccessKey length = {accessKey?.Length ?? 0}");
        Debug.Log($"[WakeWord] AccessKey = {accessKey ?? ""}");

        try
        {
            if (!string.IsNullOrEmpty(absKeyword) && File.Exists(absKeyword))
            {
                porcupineManager = PorcupineManager.FromKeywordPaths(
                    accessKey,
                    new List<string> { absKeyword },
                    HandleWakeWordDetected,
                    absModel,
                    new List<float> { customSensitivity },
                    OnPorcupineError
                );
                Debug.Log("[WakeWord] ✅ Porcupine initialized with custom keyword.");
            }
            else
            {
                // Fallback built-in
                porcupineManager = PorcupineManager.FromBuiltInKeywords(
                    accessKey,
                    new List<Porcupine.BuiltInKeyword> { builtInKeyword },
                    HandleWakeWordDetected,
                    modelPath: string.IsNullOrEmpty(absModel) ? null : absModel,
                    sensitivities: new List<float> { customSensitivity },
                    processErrorCallback: OnPorcupineError
                );
                Debug.LogWarning("[WakeWord] Using built-in keyword fallback.");
            }

            if (autoStart) StartListening();
        }
        catch (PorcupineException ex) {
            Debug.LogError($"[WakeWord] ❌ Porcupine init failed: {ex}");
        }
    }

    private void OnDestroy()
    {
        if (porcupineManager != null)
        {
            if (isRunning) porcupineManager.Stop();
            porcupineManager.Delete();
            porcupineManager = null;
        }
    }

    // ========= Callbacks =========
    private void HandleWakeWordDetected(int keywordIndex)
    {
        if (isPaused)
        {
            Debug.Log("[WakeWord] Wake word détecté mais ignoré (pause)");
            return;
        }
        Debug.Log("[WakeWord] ✅ wake word détecté, index " + keywordIndex);
        OnWakeWordDetected?.Invoke();
    }

    private void OnPorcupineError(PorcupineException e)
    {
        Debug.LogError("[WakeWord] 🚨 Porcupine runtime error: " + e.Message);
    }

    // ========= API publique =========
    public void StartListening()
    {
        if (porcupineManager == null) return;
        if (!isRunning) { porcupineManager.Start(); isRunning = true; }
        isPaused = false;
        Debug.Log("[WakeWord] ▶ StartListening -> running");
    }

    public void StopListening()
    {
        if (porcupineManager == null) return;
        if (isRunning) { porcupineManager.Stop(); isRunning = false; }
        isPaused = true;
        Debug.Log("[WakeWord] ⏹ StopListening -> stopped");
    }

    public void PauseListening()
    {
        if (porcupineManager == null) return;
        if (isRunning) { porcupineManager.Stop(); isRunning = false; }
        isPaused = true;
        Debug.Log("[WakeWord] ⏸ PauseListening -> paused");
    }

    public void ResumeListening()
    {
        if (porcupineManager == null) return;
        if (!isRunning) { porcupineManager.Start(); isRunning = true; }
        isPaused = false;
        Debug.Log("[WakeWord] ▶ ResumeListening -> running");
    }

    // ========= Helpers =========
    private string SelectKeywordForThisPlatform()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return keywordPathAndroid;
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        return keywordPathWindows;
#else
        // Ajoute d'autres variantes si besoin (linux, mac…)
        return keywordPathWindows;
#endif
    }

    private static IEnumerator CopyFromStreamingAssets(string relativePath, Action<string> onReadyPath)
    {
        if (string.IsNullOrEmpty(relativePath)) { onReadyPath?.Invoke(null); yield break; }

        string src = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath).Replace("\\", "/");
        string dst = System.IO.Path.Combine(Application.persistentDataPath, relativePath);

        var dir = System.IO.Path.GetDirectoryName(dst);
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

    #if UNITY_ANDROID && !UNITY_EDITOR
        using (var req = UnityWebRequest.Get(src))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[WakeWord] Copy failed: " + src + " -> " + req.error);
                onReadyPath?.Invoke(null);
                yield break;
            }
            try { System.IO.File.WriteAllBytes(dst, req.downloadHandler.data); }
            catch (Exception e)
            {
                Debug.LogError("[WakeWord] Copy write exception: " + e.Message);
                onReadyPath?.Invoke(null);
                yield break;
            }
        }
    #else
        try { System.IO.File.Copy(src, dst, true); }
        catch (Exception e)
        {
            Debug.LogError("[WakeWord] Copy exception: " + e.Message);
            onReadyPath?.Invoke(null);
            yield break;
        }
    #endif

        onReadyPath?.Invoke(dst);
    }
}
