using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a priority-based request queue to prevent server overload and ensure orderly request processing.
/// Supports request prioritization, cancellation, and timeout handling.
/// </summary>
public class RequestQueueManager : MonoBehaviour
{
    public static RequestQueueManager Instance { get; private set; }

    [Header("Queue Settings")]
    [SerializeField] private int maxConcurrentRequests = 3;
    [SerializeField] private float defaultTimeout = 30f;
    [SerializeField] private bool allowPriorityOverride = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Queue<QueuedRequest> normalPriorityQueue = new Queue<QueuedRequest>();
    private Queue<QueuedRequest> highPriorityQueue = new Queue<QueuedRequest>();
    private List<QueuedRequest> activeRequests = new List<QueuedRequest>();
    private Dictionary<string, QueuedRequest> requestLookup = new Dictionary<string, QueuedRequest>();

    public enum RequestPriority
    {
        Normal,
        High
    }

    public enum RequestType
    {
        STT,
        Gemini,
        TTS,
        Other
    }

    private class QueuedRequest
    {
        public string id;
        public RequestType type;
        public RequestPriority priority;
        public IEnumerator coroutine;
        public Action onComplete;
        public Action<string> onError;
        public Action onTimeout;
        public float enqueuedTime;
        public float timeout;
        public bool isCancelled;
        public Coroutine activeCoroutine;
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
        StartCoroutine(ProcessQueueRoutine());
        StartCoroutine(TimeoutCheckRoutine());
    }

    private void OnDestroy()
    {
        CancelAllRequests();
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Enqueues a request with specified priority and type.
    /// Returns a unique request ID that can be used for cancellation.
    /// </summary>
    public string EnqueueRequest(
        IEnumerator requestCoroutine,
        RequestType type,
        RequestPriority priority = RequestPriority.Normal,
        Action onComplete = null,
        Action<string> onError = null,
        float timeout = -1f,
        Action onTimeout = null)
    {
        if (timeout < 0)
            timeout = defaultTimeout;

        string requestId = GenerateRequestId();

        QueuedRequest request = new QueuedRequest
        {
            id = requestId,
            type = type,
            priority = priority,
            coroutine = requestCoroutine,
            onComplete = onComplete,
            onError = onError,
            onTimeout = onTimeout,
            enqueuedTime = Time.realtimeSinceStartup,
            timeout = timeout,
            isCancelled = false
        };

        requestLookup[requestId] = request;

        if (priority == RequestPriority.High)
            highPriorityQueue.Enqueue(request);
        else
            normalPriorityQueue.Enqueue(request);

        if (verboseLogging)
            Debug.Log($"[RequestQueue] Enqueued {type} request {requestId} with {priority} priority");

        return requestId;
    }

    /// <summary>
    /// Cancels a specific request by ID if it hasn't started processing yet.
    /// </summary>
    public bool CancelRequest(string requestId)
    {
        if (requestLookup.TryGetValue(requestId, out QueuedRequest request))
        {
            request.isCancelled = true;

            if (request.activeCoroutine != null)
            {
                StopCoroutine(request.activeCoroutine);
                activeRequests.Remove(request);
            }

            requestLookup.Remove(requestId);

            if (verboseLogging)
                Debug.Log($"[RequestQueue] Cancelled request {requestId}");

            return true;
        }

        return false;
    }

    /// <summary>
    /// Cancels all requests of a specific type.
    /// </summary>
    public int CancelRequestsByType(RequestType type)
    {
        int cancelledCount = 0;
        List<string> toCancel = new List<string>();

        foreach (var kvp in requestLookup)
        {
            if (kvp.Value.type == type)
            {
                toCancel.Add(kvp.Key);
            }
        }

        foreach (var id in toCancel)
        {
            if (CancelRequest(id))
                cancelledCount++;
        }

        if (verboseLogging && cancelledCount > 0)
            Debug.Log($"[RequestQueue] Cancelled {cancelledCount} {type} requests");

        return cancelledCount;
    }

    /// <summary>
    /// Cancels all pending and active requests.
    /// </summary>
    public void CancelAllRequests()
    {
        foreach (var request in activeRequests)
        {
            if (request.activeCoroutine != null)
                StopCoroutine(request.activeCoroutine);
        }

        activeRequests.Clear();
        highPriorityQueue.Clear();
        normalPriorityQueue.Clear();
        requestLookup.Clear();

        if (verboseLogging)
            Debug.Log("[RequestQueue] Cancelled all requests");
    }

    private IEnumerator ProcessQueueRoutine()
    {
        while (true)
        {
            // Process high priority requests first
            while (activeRequests.Count < maxConcurrentRequests && 
                   (highPriorityQueue.Count > 0 || normalPriorityQueue.Count > 0))
            {
                QueuedRequest request = null;

                if (highPriorityQueue.Count > 0)
                    request = highPriorityQueue.Dequeue();
                else if (normalPriorityQueue.Count > 0)
                    request = normalPriorityQueue.Dequeue();

                if (request != null && !request.isCancelled)
                {
                    StartProcessingRequest(request);
                }
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    private void StartProcessingRequest(QueuedRequest request)
    {
        activeRequests.Add(request);

        if (verboseLogging)
            Debug.Log($"[RequestQueue] Starting {request.type} request {request.id}");

        request.activeCoroutine = StartCoroutine(ExecuteRequest(request));
    }

    private IEnumerator ExecuteRequest(QueuedRequest request)
    {
        yield return request.coroutine;

        if (!request.isCancelled)
        {
            request.onComplete?.Invoke();

            if (verboseLogging)
                Debug.Log($"[RequestQueue] Completed {request.type} request {request.id}");
        }

        activeRequests.Remove(request);
        requestLookup.Remove(request.id);
    }

    private IEnumerator TimeoutCheckRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            List<QueuedRequest> timedOut = new List<QueuedRequest>();
            float currentTime = Time.realtimeSinceStartup;

            foreach (var request in activeRequests)
            {
                float elapsed = currentTime - request.enqueuedTime;
                if (elapsed > request.timeout)
                {
                    timedOut.Add(request);
                }
            }

            foreach (var request in timedOut)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[RequestQueue] Request {request.id} timed out after {request.timeout}s");

                if (request.activeCoroutine != null)
                    StopCoroutine(request.activeCoroutine);

                request.onTimeout?.Invoke();
                request.onError?.Invoke($"Request timed out after {request.timeout}s");

                activeRequests.Remove(request);
                requestLookup.Remove(request.id);
            }
        }
    }

    private string GenerateRequestId()
    {
        return $"req_{Guid.NewGuid().ToString("N").Substring(0, 8)}_{Time.realtimeSinceStartup}";
    }

    // Public API for monitoring
    public int GetQueueSize() => highPriorityQueue.Count + normalPriorityQueue.Count;
    public int GetActiveRequestCount() => activeRequests.Count;
    public int GetQueueSizeByType(RequestType type)
    {
        int count = 0;
        foreach (var req in highPriorityQueue)
            if (req.type == type) count++;
        foreach (var req in normalPriorityQueue)
            if (req.type == type) count++;
        return count;
    }

    public Dictionary<RequestType, int> GetQueueStatistics()
    {
        var stats = new Dictionary<RequestType, int>();
        
        foreach (RequestType type in Enum.GetValues(typeof(RequestType)))
        {
            stats[type] = GetQueueSizeByType(type);
        }

        return stats;
    }
}
