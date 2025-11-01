using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;

public class NetworkSmokeTest : MonoBehaviour
{
    // Mets exactement l’URL WS que tu utilises dans PusherClient
    [SerializeField] string wsUrl = "ws://holonauts.fr:2025/app/9weqrk5tbh6jukkrngvb?protocol=7&client=js&version=8.2.0&flash=false";

    void Start()
    {
        Debug.Log("[SMOKE] App id: " + Application.identifier + "  Platform: " + Application.platform);
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        // 1) HTTP clair (vérifie cleartext autorisé)
        yield return HttpGet("http://neverssl.com/");     // doit renvoyer 200 en clair
        yield return HttpGet("http://holonauts.fr/");     // même si 301/404, l’important est: pas d’erreur réseau bloquante
        yield return HttpGet("https://holonauts.fr/active"); // l’API REST (HTTPS) doit répondre

        // 2) WebSocket (mêmes callbacks que ton Pusher)
        Debug.Log("[SMOKE][WS] Connecting to: " + wsUrl);
        using (var ws = new WebSocket(wsUrl))
        {
            bool opened = false, closed = false;

            ws.OnOpen += (s, e) => { opened = true; Debug.Log("[SMOKE][WS] OPEN"); };
            ws.OnMessage += (s, e) => Debug.Log("[SMOKE][WS] MSG: " + (e.IsText ? e.Data : "<binary>"));
            ws.OnError += (s, e) => Debug.LogError("[SMOKE][WS] ERROR: " + e.Message);
            ws.OnClose += (s, e) => { closed = true; Debug.LogWarning("[SMOKE][WS] CLOSE: " + e.Reason + " code:" + e.Code); };

            ws.ConnectAsync();

            // attend ~5s max l’open/message/erreur/close
            float t = 0f;
            while (!opened && !closed && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }

            if (!opened && !closed) Debug.LogWarning("[SMOKE][WS] No response in 5s (ni OPEN ni ERROR).");
        }
    }

    IEnumerator HttpGet(string url)
    {
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[SMOKE][HTTP] {url} -> ERROR: {req.result} / {req.error}");
        else
            Debug.Log($"[SMOKE][HTTP] {url} -> OK {req.responseCode}, {req.downloadHandler?.text?.Substring(0, Mathf.Min(80, req.downloadedBytes > 0 ? req.downloadHandler.text.Length : 0))}");
    }
}
