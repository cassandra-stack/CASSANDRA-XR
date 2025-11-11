using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Optimized Gemini API client with connection pooling, caching, and efficient request handling.
/// Uses ConnectionManager for connection reuse and ResponseCacheManager for response caching.
/// </summary>
public class OptimizedGeminiClient
{
    private string _continueUrl;
    private readonly string _contentType;

    private const int MaxAttempts = 4;
    private const float InitialBackoffSec = 2.0f;
    private const float MaxBackoffSec = 10.0f;
    private const float JitterMinFactor = 0.5f;
    private const float JitterMaxFactor = 1.5f;
    private const float MaxTotalRetryTimeSec = 15.0f;
    private const int MaxAttemptsForHttp500 = 4;

    public bool ConfidentialHeader { get; set; } = false;
    public bool EnableCaching { get; set; } = true;
    public bool HasEndpoint => !string.IsNullOrWhiteSpace(_continueUrl);
    public string GetEndpointForDebug() => _continueUrl ?? "<null>";

    private string _currentRequestId;

    public OptimizedGeminiClient(string continueUrl, string contentType = "application/json")
    {
        _continueUrl = continueUrl?.Trim();
        _contentType = contentType;
    }

    public void UpdateEndpoint(string continueUrl)
    {
        _continueUrl = continueUrl?.Trim();
    }

    /// <summary>
    /// Sends a prompt to Gemini with automatic retry, caching, and connection management.
    /// </summary>
    public IEnumerator SendPrompt(string userText, Action<string> onSuccess, Action<string> onError = null)
    {
        if (!HasEndpoint)
        {
            onError?.Invoke("Gemini Error: missing conversation endpoint.");
            yield break;
        }

        // Check cache first
        if (EnableCaching && ResponseCacheManager.Instance != null)
        {
            string cacheKey = GenerateCacheKey(userText);
            if (ResponseCacheManager.Instance.TryGetCachedResponse(cacheKey, out string cachedResponse))
            {
                Debug.Log("[OptimizedGeminiClient] Using cached response");
                onSuccess?.Invoke(cachedResponse);
                yield break;
            }
        }

        // Queue the request
        if (RequestQueueManager.Instance != null)
        {
            bool requestCompleted = false;
            string requestResponse = null;
            string requestError = null;

            _currentRequestId = RequestQueueManager.Instance.EnqueueRequest(
                SendPromptInternal(userText, 
                    response => { requestResponse = response; requestCompleted = true; },
                    error => { requestError = error; requestCompleted = true; }),
                RequestQueueManager.RequestType.Gemini,
                RequestQueueManager.RequestPriority.High,
                onComplete: () => 
                {
                    if (requestResponse != null)
                    {
                        // Cache successful response
                        if (EnableCaching && ResponseCacheManager.Instance != null)
                        {
                            string cacheKey = GenerateCacheKey(userText);
                            ResponseCacheManager.Instance.CacheResponse(cacheKey, requestResponse);
                        }
                        onSuccess?.Invoke(requestResponse);
                    }
                    else if (requestError != null)
                    {
                        onError?.Invoke(requestError);
                    }
                },
                onError: onError,
                timeout: MaxTotalRetryTimeSec + 5f
            );

            yield return new WaitUntil(() => requestCompleted);
        }
        else
        {
            // Fallback if queue manager not available
            yield return SendPromptInternal(userText, onSuccess, onError);
        }
    }

    /// <summary>
    /// Internal method to send the actual HTTP request with retry logic.
    /// </summary>
    private IEnumerator SendPromptInternal(string userText, Action<string> onSuccess, Action<string> onError)
    {
        var payload = new GeminiContinueRequest { question = userText };
        string jsonData = JsonUtility.ToJson(payload);
        byte[] bodyData = Encoding.UTF8.GetBytes(jsonData);

        var headers = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Content-Type", _contentType },
            { "Accept", "application/json" }
        };

        if (ConfidentialHeader)
            headers["X-Confidential-Mode"] = "true";

        // Use ConnectionManager if available
        if (ConnectionManager.Instance != null)
        {
            bool completed = false;
            string result = null;
            string error = null;

            yield return ConnectionManager.Instance.SendRequest(
                _continueUrl,
                "POST",
                bodyData,
                headers,
                onSuccess: request =>
                {
                    result = request.downloadHandler.text;
                    completed = true;
                },
                onError: err =>
                {
                    error = err;
                    completed = true;
                },
                maxRetries: MaxAttempts,
                retryDelay: InitialBackoffSec
            );

            yield return new WaitUntil(() => completed);

            if (result != null)
                onSuccess?.Invoke(result);
            else
                onError?.Invoke(error ?? "Unknown error");
        }
        else
        {
            // Fallback to direct request
            yield return SendPromptDirect(bodyData, headers, onSuccess, onError);
        }
    }

    /// <summary>
    /// Direct request method (fallback when managers are not available).
    /// </summary>
    private IEnumerator SendPromptDirect(byte[] bodyData, System.Collections.Generic.Dictionary<string, string> headers, 
                                         Action<string> onSuccess, Action<string> onError)
    {
        int attempt = 0;
        float backoff = InitialBackoffSec;
        float startTs = Time.realtimeSinceStartup;
        int firstStatus = 0;

        while (true)
        {
            attempt++;

            using (var req = new UnityWebRequest(_continueUrl, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(bodyData);
                req.certificateHandler = new CertsHandler();
                req.downloadHandler = new DownloadHandlerBuffer();

                foreach (var header in headers)
                {
                    req.SetRequestHeader(header.Key, header.Value);
                }

                // Add keep-alive headers
                req.SetRequestHeader("Connection", "keep-alive");
                req.SetRequestHeader("Keep-Alive", "timeout=60");

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
                    onError?.Invoke($"REQUEST_FAILED|{debug}");
                    yield break;
                }

                float elapsed = Time.realtimeSinceStartup - startTs;
                if (attempt >= allowedMaxAttempts || elapsed >= MaxTotalRetryTimeSec)
                {
                    var debug = BuildErrorMessage(status, req.error, req.downloadHandler?.text,
                                                $"(retried {attempt}x, total {elapsed:0.0}s, first HTTP {firstStatus})");
                    onError?.Invoke($"SERVER_UNRESPONSIVE|{debug}");
                    yield break;
                }

                float delaySec = ComputeDelayFromRetryAfter(req.GetResponseHeader("Retry-After"));
                if (delaySec <= 0f)
                {
                    delaySec = Mathf.Min(backoff, MaxBackoffSec);
                    float jitter = UnityEngine.Random.Range(JitterMinFactor, JitterMaxFactor);
                    delaySec *= jitter;
                }

                Debug.LogWarning($"[OptimizedGeminiClient] HTTP {status} -> retry {attempt}/{allowedMaxAttempts} in {delaySec:0.00}s");

                yield return new WaitForSecondsRealtime(delaySec);
                backoff = Mathf.Min(backoff * 2f, MaxBackoffSec);
            }
        }
    }

    /// <summary>
    /// Cancels the current request if one is in progress.
    /// </summary>
    public void CancelCurrentRequest()
    {
        if (!string.IsNullOrEmpty(_currentRequestId) && RequestQueueManager.Instance != null)
        {
            RequestQueueManager.Instance.CancelRequest(_currentRequestId);
            _currentRequestId = null;
        }
    }

    private string GenerateCacheKey(string userText)
    {
        // Include endpoint in cache key to avoid cross-conversation collisions
        return $"{_continueUrl}:{userText}";
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

        if (int.TryParse(retryAfterHeader.Trim(), out int seconds) && seconds > 0)
            return seconds;

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
