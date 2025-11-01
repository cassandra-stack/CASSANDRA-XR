using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public static class SAFile
{
    public static IEnumerator CopyFromStreamingAssets(string relativePath, System.Action<string> onReadyPath)
    {
        string src = Path.Combine(Application.streamingAssetsPath, relativePath)
                         .Replace("\\", "/");
        string dst = Path.Combine(Application.persistentDataPath, relativePath);

        // déjà copié ?
        if (File.Exists(dst))
        {
            onReadyPath?.Invoke(dst);
            yield break;
        }

        // crée le dossier de destination
        Directory.CreateDirectory(Path.GetDirectoryName(dst));

#if UNITY_ANDROID && !UNITY_EDITOR
        using (UnityWebRequest req = UnityWebRequest.Get(src))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                throw new IOException("CopyFromStreamingAssets failed: " + src + " -> " + req.error);
            File.WriteAllBytes(dst, req.downloadHandler.data);
        }
#else
        // Sur éditeur/desktop, StreamingAssets est lisible directement
        File.Copy(src, dst, true);
#endif
        onReadyPath?.Invoke(dst);
    }
}
