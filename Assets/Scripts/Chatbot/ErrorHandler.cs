using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized error handling system for all chatbot communication modules.
/// Provides consistent error codes, user-friendly messages, and retry strategies.
/// </summary>
public class ErrorHandler : MonoBehaviour
{
    public static ErrorHandler Instance { get; private set; }

    [Header("Error Notification")]
    [SerializeField] private bool showUserNotifications = true;
    [SerializeField] private float notificationDuration = 3f;

    [Header("Logging")]
    [SerializeField] private bool logErrors = true;
    [SerializeField] private bool logWarnings = true;
    [SerializeField] private bool verboseLogging = false;

    public event Action<ErrorInfo> OnError;
    public event Action<ErrorInfo> OnWarning;

    private Dictionary<ErrorCode, int> errorCounts = new Dictionary<ErrorCode, int>();
    private List<ErrorInfo> errorHistory = new List<ErrorInfo>();
    private const int maxHistorySize = 100;

    public enum ErrorCode
    {
        // Network errors
        NetworkTimeout,
        NetworkConnectionFailed,
        NetworkUnreachable,
        
        // HTTP errors
        HTTPBadRequest,          // 400
        HTTPUnauthorized,        // 401
        HTTPForbidden,           // 403
        HTTPNotFound,            // 404
        HTTPRateLimited,         // 429
        HTTPServerError,         // 500-599
        
        // Service-specific errors
        STTServiceUnavailable,
        STTTranscriptionFailed,
        GeminiServiceUnavailable,
        GeminiResponseInvalid,
        TTSServiceUnavailable,
        TTSAudioGenerationFailed,
        
        // Client errors
        MicrophoneNotFound,
        MicrophonePermissionDenied,
        AudioPlaybackFailed,
        InvalidConfiguration,
        
        // Cache errors
        CacheCorrupted,
        CacheExpired,
        
        // Queue errors
        QueueFull,
        RequestCancelled,
        RequestTimeout,
        
        // General
        Unknown
    }

    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class ErrorInfo
    {
        public ErrorCode code;
        public ErrorSeverity severity;
        public string message;
        public string technicalDetails;
        public string userFriendlyMessage;
        public DateTime timestamp;
        public string module;
        public bool canRetry;
        public int retryAttempts;
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

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Reports an error with automatic classification and user-friendly messaging.
    /// </summary>
    public void ReportError(
        ErrorCode code,
        string module,
        string technicalDetails = null,
        ErrorSeverity severity = ErrorSeverity.Error,
        bool canRetry = false,
        int retryAttempts = 0)
    {
        ErrorInfo errorInfo = new ErrorInfo
        {
            code = code,
            severity = severity,
            message = GetErrorMessage(code),
            technicalDetails = technicalDetails,
            userFriendlyMessage = GetUserFriendlyMessage(code),
            timestamp = DateTime.UtcNow,
            module = module,
            canRetry = canRetry,
            retryAttempts = retryAttempts
        };

        TrackError(errorInfo);
        LogError(errorInfo);

        if (severity == ErrorSeverity.Warning)
            OnWarning?.Invoke(errorInfo);
        else
            OnError?.Invoke(errorInfo);
    }

    /// <summary>
    /// Parses an HTTP status code and returns the appropriate ErrorCode.
    /// </summary>
    public ErrorCode ParseHTTPError(int statusCode)
    {
        switch (statusCode)
        {
            case 400: return ErrorCode.HTTPBadRequest;
            case 401: return ErrorCode.HTTPUnauthorized;
            case 403: return ErrorCode.HTTPForbidden;
            case 404: return ErrorCode.HTTPNotFound;
            case 408: return ErrorCode.NetworkTimeout;
            case 429: return ErrorCode.HTTPRateLimited;
            case >= 500 and <= 599: return ErrorCode.HTTPServerError;
            default: return ErrorCode.Unknown;
        }
    }

    /// <summary>
    /// Determines if an error should trigger a retry.
    /// </summary>
    public bool ShouldRetry(ErrorCode code, int attemptCount, int maxAttempts)
    {
        if (attemptCount >= maxAttempts)
            return false;

        switch (code)
        {
            case ErrorCode.NetworkTimeout:
            case ErrorCode.NetworkConnectionFailed:
            case ErrorCode.HTTPRateLimited:
            case ErrorCode.HTTPServerError:
            case ErrorCode.STTServiceUnavailable:
            case ErrorCode.GeminiServiceUnavailable:
            case ErrorCode.TTSServiceUnavailable:
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Calculates the retry delay based on error type and attempt count.
    /// </summary>
    public float CalculateRetryDelay(ErrorCode code, int attemptCount)
    {
        float baseDelay = code switch
        {
            ErrorCode.HTTPRateLimited => 5f,
            ErrorCode.NetworkTimeout => 2f,
            ErrorCode.HTTPServerError => 3f,
            _ => 1f
        };

        // Exponential backoff with jitter
        float delay = baseDelay * Mathf.Pow(2, attemptCount - 1);
        delay = Mathf.Min(delay, 30f); // Cap at 30 seconds
        
        // Add jitter (±20%)
        delay += UnityEngine.Random.Range(-delay * 0.2f, delay * 0.2f);

        return delay;
    }

    private void TrackError(ErrorInfo errorInfo)
    {
        // Update error counts
        if (!errorCounts.ContainsKey(errorInfo.code))
            errorCounts[errorInfo.code] = 0;
        
        errorCounts[errorInfo.code]++;

        // Add to history
        errorHistory.Add(errorInfo);
        if (errorHistory.Count > maxHistorySize)
            errorHistory.RemoveAt(0);
    }

    private void LogError(ErrorInfo errorInfo)
    {
        string logMessage = $"[{errorInfo.module}] {errorInfo.code}: {errorInfo.message}";
        
        if (!string.IsNullOrEmpty(errorInfo.technicalDetails))
            logMessage += $"\nDetails: {errorInfo.technicalDetails}";

        switch (errorInfo.severity)
        {
            case ErrorSeverity.Info:
                if (verboseLogging)
                    Debug.Log(logMessage);
                break;

            case ErrorSeverity.Warning:
                if (logWarnings)
                    Debug.LogWarning(logMessage);
                break;

            case ErrorSeverity.Error:
                if (logErrors)
                    Debug.LogError(logMessage);
                break;

            case ErrorSeverity.Critical:
                Debug.LogError($"[CRITICAL] {logMessage}");
                break;
        }
    }

    private string GetErrorMessage(ErrorCode code)
    {
        return code switch
        {
            ErrorCode.NetworkTimeout => "Network request timed out",
            ErrorCode.NetworkConnectionFailed => "Failed to establish network connection",
            ErrorCode.NetworkUnreachable => "Network is unreachable",
            ErrorCode.HTTPBadRequest => "Invalid request format",
            ErrorCode.HTTPUnauthorized => "Authentication required",
            ErrorCode.HTTPForbidden => "Access forbidden",
            ErrorCode.HTTPNotFound => "Resource not found",
            ErrorCode.HTTPRateLimited => "Rate limit exceeded",
            ErrorCode.HTTPServerError => "Server error occurred",
            ErrorCode.STTServiceUnavailable => "Speech-to-text service unavailable",
            ErrorCode.STTTranscriptionFailed => "Failed to transcribe audio",
            ErrorCode.GeminiServiceUnavailable => "Gemini service unavailable",
            ErrorCode.GeminiResponseInvalid => "Invalid response from Gemini",
            ErrorCode.TTSServiceUnavailable => "Text-to-speech service unavailable",
            ErrorCode.TTSAudioGenerationFailed => "Failed to generate audio",
            ErrorCode.MicrophoneNotFound => "No microphone detected",
            ErrorCode.MicrophonePermissionDenied => "Microphone permission denied",
            ErrorCode.AudioPlaybackFailed => "Audio playback failed",
            ErrorCode.InvalidConfiguration => "Invalid configuration",
            ErrorCode.CacheCorrupted => "Cache data corrupted",
            ErrorCode.CacheExpired => "Cached data expired",
            ErrorCode.QueueFull => "Request queue is full",
            ErrorCode.RequestCancelled => "Request was cancelled",
            ErrorCode.RequestTimeout => "Request timed out",
            _ => "Unknown error occurred"
        };
    }

    private string GetUserFriendlyMessage(ErrorCode code)
    {
        return code switch
        {
            ErrorCode.NetworkTimeout => "The connection is taking too long. Please check your internet and try again.",
            ErrorCode.NetworkConnectionFailed => "Unable to connect. Please check your internet connection.",
            ErrorCode.NetworkUnreachable => "No internet connection available.",
            ErrorCode.HTTPRateLimited => "Too many requests. Please wait a moment and try again.",
            ErrorCode.HTTPServerError => "The server is experiencing issues. Please try again later.",
            ErrorCode.STTServiceUnavailable => "Voice recognition is temporarily unavailable.",
            ErrorCode.STTTranscriptionFailed => "Couldn't understand the audio. Please try speaking again.",
            ErrorCode.GeminiServiceUnavailable => "Cassandra is temporarily unavailable. Please try again later.",
            ErrorCode.GeminiResponseInvalid => "Received an unexpected response. Please try again.",
            ErrorCode.TTSServiceUnavailable => "Voice playback is temporarily unavailable.",
            ErrorCode.TTSAudioGenerationFailed => "Couldn't generate voice audio. The text will be shown instead.",
            ErrorCode.MicrophoneNotFound => "No microphone found. Please connect a microphone.",
            ErrorCode.MicrophonePermissionDenied => "Microphone access denied. Please grant permission in settings.",
            ErrorCode.AudioPlaybackFailed => "Audio playback failed. Please check your audio settings.",
            ErrorCode.InvalidConfiguration => "System configuration error. Please contact support.",
            ErrorCode.QueueFull => "Too many requests in progress. Please wait a moment.",
            ErrorCode.RequestCancelled => "Operation cancelled.",
            ErrorCode.RequestTimeout => "Operation took too long and was cancelled.",
            _ => "An error occurred. Please try again."
        };
    }

    // Public API for monitoring
    public int GetErrorCount(ErrorCode code)
    {
        return errorCounts.ContainsKey(code) ? errorCounts[code] : 0;
    }

    public List<ErrorInfo> GetRecentErrors(int count = 10)
    {
        int start = Mathf.Max(0, errorHistory.Count - count);
        return errorHistory.GetRange(start, errorHistory.Count - start);
    }

    public Dictionary<ErrorCode, int> GetErrorStatistics()
    {
        return new Dictionary<ErrorCode, int>(errorCounts);
    }

    public void ClearErrorHistory()
    {
        errorHistory.Clear();
        errorCounts.Clear();
    }
}
