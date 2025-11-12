// Assets/Scripts/DVR/AndroidVRDFPreloader.cs
using System;
using System.Collections;
using System.Collections.Generic;      // <— important
using System.IO;
using UnityEngine;
using UnityEngine.Networking;         // <— important

[DisallowMultipleComponent]
public class AndroidVRDFPreloader : MonoBehaviour
{
    [Tooltip("Noms EXACTS des .vrdf dans StreamingAssets (ex: BraTS-GLI-00014-000-t1c_lw.vrdf)")]
    public List<string> streamingAssetFileNames = new List<string>();

    public bool verbose = true;

    // Prêt quand tous les fichiers ont été copiés
    public bool IsReady { get; private set; }

    // Map: code ("t1c") -> chemin en cache (persistentDataPath)
    private readonly Dictionary<string, string> _codeToCachePath = new Dictionary<string, string>();

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        yield return PreloadAll();
#else
        IsReady = true; // pas nécessaire hors Android
        yield break;
#endif
    }

    public bool TryGetPathForCode(string code, out string path)
    {
        path = null;
        if (string.IsNullOrEmpty(code)) return false;

        string lower = code.ToLowerInvariant();

        // 1) Si mappé pendant le preload
        if (_codeToCachePath.TryGetValue(lower, out path) && File.Exists(path))
            return true;

        // 2) Sinon, tente un match par suffixe dans le cache (au cas où)
        try
        {
            string[] files = Directory.GetFiles(Application.persistentDataPath, "*.vrdf", SearchOption.AllDirectories);
            foreach (var f in files)
            {
                if (EndsWithCode(f, lower)) { path = f; return true; }
            }
        }
        catch { /* ignore */ }

        return false;
    }

    private IEnumerator PreloadAll()
    {
        IsReady = false;

        foreach (var fileName in streamingAssetFileNames)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            string saUrl = Path.Combine(Application.streamingAssetsPath, fileName); // jar: url sur Android
            if (verbose) Debug.Log($"[VRDFPreloader] UWR get: {saUrl}");

            using (var uwr = UnityWebRequest.Get(saUrl))
            {
                yield return uwr.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (uwr.result != UnityWebRequest.Result.Success)
#else
                if (uwr.isHttpError || uwr.isNetworkError)
#endif
                {
                    Debug.LogWarning($"[VRDFPreloader] Échec lecture {fileName} : {uwr.error}");
                    continue;
                }

                byte[] bytes = uwr.downloadHandler.data;
                string destPath = Path.Combine(Application.persistentDataPath, fileName);
                string destDir  = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                try
                {
                    File.WriteAllBytes(destPath, bytes);
                    if (verbose) Debug.Log($"[VRDFPreloader] Copié → {destPath} ({bytes?.Length ?? 0} o)");

                    string code = ExtractCodeFromFileName(fileName);
                    if (!string.IsNullOrEmpty(code))
                        _codeToCachePath[code] = destPath;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VRDFPreloader] Échec écriture {destPath} : {e.Message}");
                }
            }

            // petit yield pour garder l'UI réactive
            yield return null;
        }

        IsReady = true;
        if (verbose) Debug.Log("[VRDFPreloader] ✅ Prêt");
    }

    // Extrait le "code" terminal : "BraTS-...-t1c_lw.vrdf" → "t1c"
    private string ExtractCodeFromFileName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName); // ex: BraTS-...-t1c_lw
        if (string.IsNullOrEmpty(name)) return null;

        // on prend tout après le dernier '-' : "...-t1c_lw" → "t1c_lw"
        int dash = name.LastIndexOf('-');
        string tail = dash >= 0 ? name.Substring(dash + 1) : name;

        // on supprime suffixe "_lw" s'il est présent
        if (tail.EndsWith("_lw", StringComparison.OrdinalIgnoreCase))
            tail = tail.Substring(0, tail.Length - 3);

        return tail.ToLowerInvariant(); // "t1c"
    }

    // true si le nom de fichier se termine par "<code>_lw.vrdf"
    private bool EndsWithCode(string pathOrName, string code)
    {
        string fn = Path.GetFileName(pathOrName).ToLowerInvariant();
        return fn.EndsWith($"{code}_lw.vrdf");
    }
}
