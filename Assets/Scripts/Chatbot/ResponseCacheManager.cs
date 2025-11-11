using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches TTS audio clips and API responses to reduce redundant network requests.
/// Implements LRU (Least Recently Used) eviction policy.
/// </summary>
public class ResponseCacheManager : MonoBehaviour
{
    public static ResponseCacheManager Instance { get; private set; }

    [Header("Cache Settings")]
    [SerializeField] private int maxCacheSize = 50;
    [SerializeField] private int maxAudioCacheSize = 20;
    [SerializeField] private float cacheExpirationMinutes = 30f;
    [SerializeField] private bool enableCache = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private Dictionary<string, CachedResponse> responseCache = new Dictionary<string, CachedResponse>();
    private Dictionary<string, CachedAudio> audioCache = new Dictionary<string, CachedAudio>();
    private LinkedList<string> responseLRU = new LinkedList<string>();
    private LinkedList<string> audioLRU = new LinkedList<string>();

    private int cacheHits = 0;
    private int cacheMisses = 0;

    private class CachedResponse
    {
        public string content;
        public DateTime timestamp;
        public LinkedListNode<string> lruNode;
    }

    private class CachedAudio
    {
        public AudioClip clip;
        public DateTime timestamp;
        public int accessCount;
        public LinkedListNode<string> lruNode;
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
        StartCoroutine(CleanupExpiredCacheRoutine());
    }

    private void OnDestroy()
    {
        ClearAllCaches();
        if (Instance == this) Instance = null;
    }

    #region Text Response Cache

    /// <summary>
    /// Attempts to get a cached text response.
    /// </summary>
    public bool TryGetCachedResponse(string key, out string response)
    {
        response = null;

        if (!enableCache)
        {
            cacheMisses++;
            return false;
        }

        string cacheKey = GenerateCacheKey(key);

        if (responseCache.TryGetValue(cacheKey, out CachedResponse cached))
        {
            // Check if expired
            if (IsExpired(cached.timestamp))
            {
                RemoveFromResponseCache(cacheKey);
                cacheMisses++;
                return false;
            }

            // Move to front of LRU
            responseLRU.Remove(cached.lruNode);
            cached.lruNode = responseLRU.AddFirst(cacheKey);

            response = cached.content;
            cacheHits++;

            if (verboseLogging)
                Debug.Log($"[Cache] Hit for response key: {key}");

            return true;
        }

        cacheMisses++;
        return false;
    }

    /// <summary>
    /// Caches a text response.
    /// </summary>
    public void CacheResponse(string key, string response)
    {
        if (!enableCache || string.IsNullOrEmpty(response))
            return;

        string cacheKey = GenerateCacheKey(key);

        // Check if we need to evict
        if (responseCache.Count >= maxCacheSize && !responseCache.ContainsKey(cacheKey))
        {
            EvictLRUResponse();
        }

        // Remove if exists (to update)
        if (responseCache.ContainsKey(cacheKey))
        {
            RemoveFromResponseCache(cacheKey);
        }

        // Add to cache
        var lruNode = responseLRU.AddFirst(cacheKey);
        responseCache[cacheKey] = new CachedResponse
        {
            content = response,
            timestamp = DateTime.UtcNow,
            lruNode = lruNode
        };

        if (verboseLogging)
            Debug.Log($"[Cache] Cached response for key: {key}");
    }

    private void EvictLRUResponse()
    {
        if (responseLRU.Count == 0)
            return;

        string lruKey = responseLRU.Last.Value;
        RemoveFromResponseCache(lruKey);

        if (verboseLogging)
            Debug.Log($"[Cache] Evicted LRU response: {lruKey}");
    }

    private void RemoveFromResponseCache(string cacheKey)
    {
        if (responseCache.TryGetValue(cacheKey, out CachedResponse cached))
        {
            responseLRU.Remove(cached.lruNode);
            responseCache.Remove(cacheKey);
        }
    }

    #endregion

    #region Audio Cache

    /// <summary>
    /// Attempts to get a cached audio clip for TTS.
    /// </summary>
    public bool TryGetCachedAudio(string text, out AudioClip clip)
    {
        clip = null;

        if (!enableCache)
        {
            cacheMisses++;
            return false;
        }

        string cacheKey = GenerateCacheKey(text);

        if (audioCache.TryGetValue(cacheKey, out CachedAudio cached))
        {
            // Check if expired
            if (IsExpired(cached.timestamp))
            {
                RemoveFromAudioCache(cacheKey);
                cacheMisses++;
                return false;
            }

            // Check if clip is still valid
            if (cached.clip == null || cached.clip.loadState != AudioDataLoadState.Loaded)
            {
                RemoveFromAudioCache(cacheKey);
                cacheMisses++;
                return false;
            }

            // Move to front of LRU
            audioLRU.Remove(cached.lruNode);
            cached.lruNode = audioLRU.AddFirst(cacheKey);
            cached.accessCount++;

            clip = cached.clip;
            cacheHits++;

            if (verboseLogging)
                Debug.Log($"[Cache] Hit for audio text: {text.Substring(0, Math.Min(30, text.Length))}...");

            return true;
        }

        cacheMisses++;
        return false;
    }

    /// <summary>
    /// Caches an audio clip for TTS.
    /// </summary>
    public void CacheAudio(string text, AudioClip clip)
    {
        if (!enableCache || clip == null || string.IsNullOrEmpty(text))
            return;

        string cacheKey = GenerateCacheKey(text);

        // Check if we need to evict
        if (audioCache.Count >= maxAudioCacheSize && !audioCache.ContainsKey(cacheKey))
        {
            EvictLRUAudio();
        }

        // Remove if exists (to update)
        if (audioCache.ContainsKey(cacheKey))
        {
            RemoveFromAudioCache(cacheKey);
        }

        // Add to cache
        var lruNode = audioLRU.AddFirst(cacheKey);
        audioCache[cacheKey] = new CachedAudio
        {
            clip = clip,
            timestamp = DateTime.UtcNow,
            accessCount = 0,
            lruNode = lruNode
        };

        if (verboseLogging)
            Debug.Log($"[Cache] Cached audio for text: {text.Substring(0, Math.Min(30, text.Length))}...");
    }

    private void EvictLRUAudio()
    {
        if (audioLRU.Count == 0)
            return;

        string lruKey = audioLRU.Last.Value;
        RemoveFromAudioCache(lruKey);

        if (verboseLogging)
            Debug.Log($"[Cache] Evicted LRU audio: {lruKey}");
    }

    private void RemoveFromAudioCache(string cacheKey)
    {
        if (audioCache.TryGetValue(cacheKey, out CachedAudio cached))
        {
            audioLRU.Remove(cached.lruNode);
            
            // Don't destroy the clip as it might still be in use
            // Unity will handle cleanup when no longer referenced
            
            audioCache.Remove(cacheKey);
        }
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Clears all cached responses.
    /// </summary>
    public void ClearResponseCache()
    {
        responseCache.Clear();
        responseLRU.Clear();
        
        if (verboseLogging)
            Debug.Log("[Cache] Cleared all response cache");
    }

    /// <summary>
    /// Clears all cached audio clips.
    /// </summary>
    public void ClearAudioCache()
    {
        audioCache.Clear();
        audioLRU.Clear();
        
        if (verboseLogging)
            Debug.Log("[Cache] Cleared all audio cache");
    }

    /// <summary>
    /// Clears all caches.
    /// </summary>
    public void ClearAllCaches()
    {
        ClearResponseCache();
        ClearAudioCache();
        cacheHits = 0;
        cacheMisses = 0;
    }

    private System.Collections.IEnumerator CleanupExpiredCacheRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(60f); // Check every minute

            // Cleanup expired responses
            List<string> expiredResponses = new List<string>();
            foreach (var kvp in responseCache)
            {
                if (IsExpired(kvp.Value.timestamp))
                {
                    expiredResponses.Add(kvp.Key);
                }
            }

            foreach (var key in expiredResponses)
            {
                RemoveFromResponseCache(key);
            }

            // Cleanup expired audio
            List<string> expiredAudio = new List<string>();
            foreach (var kvp in audioCache)
            {
                if (IsExpired(kvp.Value.timestamp))
                {
                    expiredAudio.Add(kvp.Key);
                }
            }

            foreach (var key in expiredAudio)
            {
                RemoveFromAudioCache(key);
            }

            if (verboseLogging && (expiredResponses.Count > 0 || expiredAudio.Count > 0))
            {
                Debug.Log($"[Cache] Cleaned up {expiredResponses.Count} expired responses and {expiredAudio.Count} expired audio clips");
            }
        }
    }

    private bool IsExpired(DateTime timestamp)
    {
        return (DateTime.UtcNow - timestamp).TotalMinutes > cacheExpirationMinutes;
    }

    private string GenerateCacheKey(string input)
    {
        // Simple hash-based key generation
        return input.GetHashCode().ToString();
    }

    #endregion

    #region Statistics

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            responseCacheSize = responseCache.Count,
            audioCacheSize = audioCache.Count,
            cacheHits = cacheHits,
            cacheMisses = cacheMisses,
            hitRate = cacheMisses > 0 ? (float)cacheHits / (cacheHits + cacheMisses) : 0f
        };
    }

    public struct CacheStatistics
    {
        public int responseCacheSize;
        public int audioCacheSize;
        public int cacheHits;
        public int cacheMisses;
        public float hitRate;
    }

    #endregion
}
