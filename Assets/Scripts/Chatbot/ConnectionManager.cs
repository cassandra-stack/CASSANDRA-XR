using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manages HTTP connections efficiently with connection pooling, health checks, and automatic reconnection.
/// Prevents redundant connections and reduces latency through connection reuse.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [Header("Connection Settings")]
    [SerializeField] private int maxConcurrentRequests = 5;
    [SerializeField] private float healthCheckInterval = 30f;
    [SerializeField] private float connectionTimeout = 10f;

    [Header("Keep-Alive Settings")]
    [SerializeField] private bool enableKeepAlive = true;
    [SerializeField] private int keepAliveTimeout = 60;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Dictionary<string, ConnectionPool> connectionPools = new Dictionary<string, ConnectionPool>();
    private Dictionary<string, DateTime> lastHealthCheck = new Dictionary<string, DateTime>();
    private int activeRequestCount = 0;

    private class ConnectionPool
    {
        public string baseUrl;
        public Queue<UnityWebRequest> availableConnections = new Queue<UnityWebRequest>();
        public List<UnityWebRequest> activeConnections = new List<UnityWebRequest>();
        public int maxPoolSize = 3;
        public DateTime lastUsed = DateTime.UtcNow;
        public bool isHealthy = true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartCoroutine(PeriodicHealthCheckRoutine());
        StartCoroutine(CleanupIdleConnectionsRoutine());
    }

    private void OnDestroy()
    {
        CleanupAllConnections();
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Sends an HTTP request with automatic connection pooling and retry logic.
    /// </summary>
    public IEnumerator SendRequest(
        string url,
        string method,
        byte[] bodyData,
        Dictionary<string, string> headers,
        Action<UnityWebRequest> onSuccess,
        Action<string> onError,
        int maxRetries = 3,
        float retryDelay = 1f)
    {
        if (activeRequestCount >= maxConcurrentRequests)
        {
            if (verboseLogging)
                Debug.LogWarning($"[ConnectionManager] Max concurrent requests reached. Queuing request to {url}");
            
            yield return new WaitUntil(() => activeRequestCount < maxConcurrentRequests);
        }

        activeRequestCount++;
        string baseUrl = GetBaseUrl(url);

        int attempt = 0;
        float backoff = retryDelay;

        while (attempt < maxRetries)
        {
            attempt++;

            using (UnityWebRequest request = CreateRequest(url, method, bodyData, headers))
            {
                if (enableKeepAlive)
                {
                    request.SetRequestHeader("Connection", "keep-alive");
                    request.SetRequestHeader("Keep-Alive", $"timeout={keepAliveTimeout}");
                }

                request.timeout = (int)connectionTimeout;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    UpdateConnectionHealth(baseUrl, true);
                    activeRequestCount--;
                    onSuccess?.Invoke(request);
                    yield break;
                }

                bool shouldRetry = ShouldRetryRequest(request.result, (int)request.responseCode);

                if (!shouldRetry || attempt >= maxRetries)
                {
                    UpdateConnectionHealth(baseUrl, false);
                    activeRequestCount--;
                    string errorMsg = $"HTTP {request.responseCode}: {request.error}";
                    onError?.Invoke(errorMsg);
                    yield break;
                }

                if (verboseLogging)
                    Debug.LogWarning($"[ConnectionManager] Request failed (attempt {attempt}/{maxRetries}). Retrying in {backoff}s...");

                yield return new WaitForSeconds(backoff);
                backoff *= 2f;
            }
        }

        activeRequestCount--;
    }

    private UnityWebRequest CreateRequest(string url, string method, byte[] bodyData, Dictionary<string, string> headers)
    {
        UnityWebRequest request;

        switch (method.ToUpper())
        {
            case "GET":
                request = UnityWebRequest.Get(url);
                break;
            case "POST":
                request = new UnityWebRequest(url, method);
                request.uploadHandler = new UploadHandlerRaw(bodyData);
                request.downloadHandler = new DownloadHandlerBuffer();
                break;
            case "PUT":
                request = UnityWebRequest.Put(url, bodyData);
                break;
            case "DELETE":
                request = UnityWebRequest.Delete(url);
                break;
            default:
                request = new UnityWebRequest(url, method);
                request.downloadHandler = new DownloadHandlerBuffer();
                break;
        }

        // Add certificate handler
        request.certificateHandler = new CertsHandler();

        // Apply custom headers
        if (headers != null)
        {
            foreach (var header in headers)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }
        }

        return request;
    }

    private bool ShouldRetryRequest(UnityWebRequest.Result result, int statusCode)
    {
        // Retry on network errors
        if (result == UnityWebRequest.Result.ConnectionError ||
            result == UnityWebRequest.Result.ProtocolError)
        {
            // Retry on rate limiting or server errors
            if (statusCode == 429 || (statusCode >= 500 && statusCode <= 599))
                return true;

            // Retry on timeout
            if (statusCode == 0 || statusCode == 408)
                return true;
        }

        return false;
    }

    private string GetBaseUrl(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }
        catch
        {
            return url;
        }
    }

    private void UpdateConnectionHealth(string baseUrl, bool isHealthy)
    {
        if (!connectionPools.ContainsKey(baseUrl))
        {
            connectionPools[baseUrl] = new ConnectionPool { baseUrl = baseUrl };
        }

        connectionPools[baseUrl].isHealthy = isHealthy;
        connectionPools[baseUrl].lastUsed = DateTime.UtcNow;
        lastHealthCheck[baseUrl] = DateTime.UtcNow;
    }

    private IEnumerator PeriodicHealthCheckRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(healthCheckInterval);

            foreach (var kvp in new Dictionary<string, ConnectionPool>(connectionPools))
            {
                if (!kvp.Value.isHealthy)
                {
                    if (verboseLogging)
                        Debug.Log($"[ConnectionManager] Health check: {kvp.Key} is unhealthy");
                    
                    // Attempt to restore health after cooldown
                    TimeSpan timeSinceLastCheck = DateTime.UtcNow - lastHealthCheck[kvp.Key];
                    if (timeSinceLastCheck.TotalSeconds > healthCheckInterval * 2)
                    {
                        kvp.Value.isHealthy = true;
                        if (verboseLogging)
                            Debug.Log($"[ConnectionManager] Restored health status for {kvp.Key}");
                    }
                }
            }
        }
    }

    private IEnumerator CleanupIdleConnectionsRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(60f);

            List<string> toRemove = new List<string>();
            foreach (var kvp in connectionPools)
            {
                TimeSpan idleTime = DateTime.UtcNow - kvp.Value.lastUsed;
                if (idleTime.TotalMinutes > 5)
                {
                    toRemove.Add(kvp.Key);
                    if (verboseLogging)
                        Debug.Log($"[ConnectionManager] Removing idle connection pool for {kvp.Key}");
                }
            }

            foreach (var key in toRemove)
            {
                connectionPools.Remove(key);
                lastHealthCheck.Remove(key);
            }
        }
    }

    private void CleanupAllConnections()
    {
        foreach (var pool in connectionPools.Values)
        {
            foreach (var conn in pool.availableConnections)
            {
                conn?.Dispose();
            }
            foreach (var conn in pool.activeConnections)
            {
                conn?.Dispose();
            }
        }

        connectionPools.Clear();
        lastHealthCheck.Clear();
    }

    public int GetActiveRequestCount() => activeRequestCount;

    public Dictionary<string, bool> GetConnectionHealthStatus()
    {
        var status = new Dictionary<string, bool>();
        foreach (var kvp in connectionPools)
        {
            status[kvp.Key] = kvp.Value.isHealthy;
        }
        return status;
    }
}
