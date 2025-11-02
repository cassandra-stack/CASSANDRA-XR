using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiClient
{
    private string _continueUrl;
    private readonly string _contentType;

    private const int    MaxAttempts             = 4;
    private const float  InitialBackoffSec       = 2.0f;
    private const float  MaxBackoffSec           = 10.0f;
    private const float  JitterMinFactor         = 0.5f;
    private const float  JitterMaxFactor         = 1.5f;
    private const float  MaxTotalRetryTimeSec    = 15.0f;
    private const int MaxAttemptsForHttp500 = 4;

    public bool ConfidentialHeader { get; set; } = false;

    public bool HasEndpoint => !string.IsNullOrWhiteSpace(_continueUrl);
    public string GetEndpointForDebug() => _continueUrl ?? "<null>";

    public GeminiClient(string continueUrl, string contentType = "application/json")
    {
        _continueUrl = continueUrl?.Trim();
        _contentType = contentType;
    }

    public void UpdateEndpoint(string continueUrl)
    {
        _continueUrl = continueUrl?.Trim();
    }

    public IEnumerator SendPrompt(string userText, Action<string> onSuccess, Action<string> onError = null)
    {
        if (!HasEndpoint)
        {
            onError?.Invoke("Gemini Error: missing conversation endpoint.");
            yield break;
        }

        var payload = new GeminiContinueRequest { question = userText };
        string jsonData = JsonUtility.ToJson(payload);

        int attempt = 0;
        float backoff = InitialBackoffSec;
        float startTs = Time.realtimeSinceStartup;
        int firstStatus = 0;

        while (true)
        {
            attempt++;

            using (var req = new UnityWebRequest(_continueUrl, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
                req.certificateHandler = new CertsHandler();
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", _contentType);
                req.SetRequestHeader("Accept", "application/json");
                if (ConfidentialHeader)
                    req.SetRequestHeader("X-Confidential-Mode", "true");

                yield return req.SendWebRequest();

                bool success = req.result == UnityWebRequest.Result.Success;
                int status = (int)req.responseCode;
                if (firstStatus == 0) firstStatus = status;

                if (success)
                {
                    onSuccess?.Invoke(req.downloadHandler.text);
                    yield break;
                }

                bool retryable = ShouldRetry(status, req.error);

                int allowedMaxAttempts = (status == 500) ? Mathf.Min(MaxAttempts, MaxAttemptsForHttp500) : MaxAttempts;

                if (!retryable)
                {
                    var debug = BuildErrorMessage(status, req.error, req.downloadHandler?.text);
                    onError?.Invoke($"REQUEST_FAILED|Une erreur est survenue lors de l’envoi de la requête.\n{debug}");
                    yield break;
                }

                float elapsed = Time.realtimeSinceStartup - startTs;
                if (attempt >= allowedMaxAttempts || elapsed >= MaxTotalRetryTimeSec)
                {
                    var debug = BuildErrorMessage(status, req.error, req.downloadHandler?.text,
                                                $"(retried {attempt}x, total {elapsed:0.0}s, first HTTP {firstStatus})");
                    onError?.Invoke($"SERVER_UNRESPONSIVE|Le serveur ne répond pas. Merci de réessayer plus tard.\n{debug}");
                    yield break;
                }

                float delaySec = ComputeDelayFromRetryAfter(req.GetResponseHeader("Retry-After"));
                if (delaySec <= 0f)
                {
                    delaySec = Mathf.Min(backoff, MaxBackoffSec);
                    float jitter = UnityEngine.Random.Range(JitterMinFactor, JitterMaxFactor);
                    delaySec *= jitter;
                }

                Debug.LogWarning($"[GeminiClient] HTTP {status} -> retry {attempt}/{allowedMaxAttempts} in {delaySec:0.00}s");

                yield return new WaitForSecondsRealtime(delaySec);

                backoff = Mathf.Min(backoff * 2f, MaxBackoffSec);
            }
        }
    }

    private static bool ShouldRetry(int status, string transportError)
    {
        if (status == 429) return true;
        if (status >= 500 && status <= 599) return true;

        if (!string.IsNullOrEmpty(transportError))
        {
            string e = transportError.ToLowerInvariant();
            if (e.Contains("timed out") || e.Contains("timeout") ||
                e.Contains("temporary") || e.Contains("could not resolve host") ||
                e.Contains("connection") || e.Contains("ssl") || e.Contains("abort"))
                return true;
        }

        return false;
    }

    private static float ComputeDelayFromRetryAfter(string retryAfterHeader)
    {
        if (string.IsNullOrWhiteSpace(retryAfterHeader)) return 0f;

        // Format 1: secondes "120"
        if (int.TryParse(retryAfterHeader.Trim(), out int seconds) && seconds > 0)
            return seconds;

        // Format 2: date (RFC1123)
        if (DateTime.TryParse(retryAfterHeader, out var when))
        {
            var delta = (float)(when.ToUniversalTime() - DateTime.UtcNow).TotalSeconds;
            return Mathf.Max(0f, delta);
        }

        return 0f;
    }

    private static string BuildErrorMessage(int status, string unityError, string body, string suffix = null)
    {
        var sb = new StringBuilder();
        if (status > 0) sb.Append($"HTTP {status}. ");
        if (!string.IsNullOrEmpty(unityError)) sb.Append(unityError).Append(' ');
        if (!string.IsNullOrEmpty(body)) sb.Append("| Body: ").Append(body);
        if (!string.IsNullOrEmpty(suffix)) sb.Append(' ').Append(suffix);
        return sb.ToString();
    }

}

[Serializable] public class GeminiContinueRequest { public string question; }
