using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using System.Collections;
using TMPro;
using System;
using System.Text.RegularExpressions;

public class GeminiVoiceInterface : MonoBehaviour
{
    [Header("Google Cloud / Backend URLs")]
    public string speechToText_URL = "https://stt-92429070891.europe-west1.run.app/";
    public string gemini_URL;
    public string textToSpeech_URL = "https://tts-92429070891.europe-west1.run.app/";

    [Header("Confidential Mode")]
    public bool confidentialMode = false;
    [Tooltip("Hide patient name, date of birth, and study code/title in UI.")]
    public bool redactUiIdentity = false;
    [Tooltip("Redact PHI from user prompts and model replies shown in chat.")]
    public bool redactChatContent = false;
    [Tooltip("Skip TTS when confidential mode is ON.")]
    public bool disableTtsInConfidential = false;
    [Tooltip("Do not fetch/restore past conversation while confidential.")]
    public bool skipHistoryInConfidential = false;
    [Tooltip("Minimize logs when confidential.")]
    public bool suppressVerboseLogs = true;

    [Header("Input / Wake")]
    public InputActionReference voiceAction;
    public PorcupineWakeWordListener wakeWordListener;

    [Header("UI")]
    public TextMeshProUGUI statusText;
    public AudioSource audioSource;

    [Header("Chat Binding")]
    public ChatManager chatManager;

    [Header("Study State")]
    public StudyRuntimeSO studyState;

    [Header("Silence Detection (VAD)")]
    public float silenceThreshold = 0.01f;
    public float silenceDuration = 5.0f;
    private float silenceTimer = 0f;
    private int sampleWindow = 512;

    private bool isRecording = false;
    private bool isSpeaking = false;
    private AudioClip recording;
    private const int RECORD_DURATION = 15;
    private const int SAMPLE_RATE = 16000;

    private bool _geminiInFlight = false;

    public event System.Action OnStartListening;
    public event System.Action OnStopListening;

    private GeminiClient geminiClient;

    private bool _conversationLoading = false;
    private bool _welcomeShown = false;
    
    private string _currentGeminiRequestId;
    private string _currentTTSRequestId;

    private void Awake()
    {
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (statusText != null)
            statusText.text = "Say the keyword to speak.";

        if (wakeWordListener == null)
        {
            Debug.LogWarning("[GeminiVoiceInterface] WakeWordListener pas assigné.");
        }
        else
        {
            wakeWordListener.OnWakeWordDetected += OnWakeWordHeard;
        }

        geminiClient = new GeminiClient(gemini_URL);
    }

    private void Start()
    {
        confidentialMode = false;
        redactUiIdentity = false;
        redactChatContent = false;
        disableTtsInConfidential = false;
        skipHistoryInConfidential = false;
        suppressVerboseLogs = false;

        Debug.Log("[GeminiVoiceInterface] Confidential mode forced OFF at startup.");
    }

    private void OnEnable()
    {
        if (voiceAction != null)
            voiceAction.action.performed += OnVoiceButtonPressed;

        if (studyState != null)
        {
            studyState.OnChanged += HandleStudyChanged;
            HandleStudyChanged();
        }
        geminiClient.ConfidentialHeader = confidentialMode;

    }

    private void HandleStudyChanged()
    {
        string endpoint = (studyState != null) ? studyState.converationURL : null;
        endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();

        geminiClient.UpdateEndpoint(endpoint);

        chatManager?.ClearChat();
        _welcomeShown = false;

        if (!geminiClient.HasEndpoint)
        {
            chatManager?.AddBotMessage("No active conversation for this study.");
            if (statusText != null)
                statusText.text = "No active conversation (WS/Study).";
            return;
        }

        if (statusText != null)
            statusText.text = "Fetching conversation...";

        if (skipHistoryInConfidential && confidentialMode)
        {
            PostWelcomeOnce();
            if (statusText != null) statusText.text = "New conversation (confidential).";
            return;
        }

        if (!_conversationLoading)
            StartCoroutine(FetchAndPopulateConversation());

    }


    private void OnDisable()
    {
        if (voiceAction != null)
            voiceAction.action.performed -= OnVoiceButtonPressed;

        if (studyState != null)
            studyState.OnChanged -= HandleStudyChanged;
    }

    private void OnVoiceButtonPressed(InputAction.CallbackContext ctx)
    {
        ToggleRecordingOrStop();
    }

    private void OnWakeWordHeard()
    {
        if (isSpeaking)
        {
            Debug.Log("[GeminiVoiceInterface] Wake word reçu pendant TTS -> on coupe");
            ForceStopTTS();
            return;
        }

        if (!isRecording)
            ToggleRecordingOrStop();
    }

    private void ToggleRecordingOrStop()
    {
        if (!isRecording)
            StartRecording();
        else
            StopAndProcessRecording();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
            ToggleRecordingOrStop();

        if (!isRecording)
            return;

        float[] samples = new float[sampleWindow];
        int micPosition = Microphone.GetPosition(null);
        if (micPosition <= 0) return;

        int startPos = micPosition - sampleWindow;
        if (startPos < 0) startPos = 0;
        recording.GetData(samples, startPos);

        float sum = 0;
        for (int i = 0; i < sampleWindow; i++)
            sum += Mathf.Abs(samples[i]);
        float avgVolume = sum / sampleWindow;

        if (avgVolume > silenceThreshold)
            silenceTimer = 0f;
        else
            silenceTimer += Time.deltaTime;

        if (silenceTimer >= silenceDuration)
        {
            Debug.Log($"[GeminiVoiceInterface] Silence {silenceDuration}s -> stop auto.");
            StopAndProcessRecording();
        }
    }

    private void StartRecording()
    {
        try
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[GeminiVoiceInterface] No microphone detected!");
                if (statusText != null) statusText.text = "No microphone found. Please connect a microphone.";
                return;
            }

            // Cancel any ongoing operations before starting new recording
            if (_geminiInFlight || isSpeaking)
            {
                Debug.Log("[GeminiVoiceInterface] Cancelling ongoing operations before new recording");
                CancelAllOperations();
            }

            wakeWordListener?.PauseListening();

            silenceTimer = 0f;
            isRecording = true;
            OnStartListening?.Invoke();

            chatManager?.CreateListeningBubble();
            if (statusText != null) statusText.text = "I'm listening... (auto-stop on silence)";

            Debug.Log("[GeminiVoiceInterface] StartRecording()");
            recording = Microphone.Start(null, false, RECORD_DURATION, SAMPLE_RATE);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GeminiVoiceInterface] Exception in StartRecording: {ex.Message}");
            if (statusText != null) statusText.text = "Microphone error. Please try again.";
            isRecording = false;
            wakeWordListener?.ResumeListening();
        }
    }

    private void StopAndProcessRecording()
    {
        if (!isRecording) return;

        try
        {
            isRecording = false;
            OnStopListening?.Invoke();
            Microphone.End(null);
            silenceTimer = 0f;

            if (statusText != null)
                statusText.text = "Transcription en cours...";

            Debug.Log("[GeminiVoiceInterface] ⏹ StopRecording() -> STT");

            chatManager?.RemoveListeningBubble();
            chatManager?.CreateUserTypingBubble();
            wakeWordListener?.ResumeListening();

            if (recording == null)
            {
                Debug.LogError("[GeminiVoiceInterface] Recording is null!");
                chatManager?.FinalizeUserTypingBubble("[Recording error]");
                if (statusText != null) statusText.text = "Recording error. Please try again.";
                return;
            }

            byte[] audioData = WavUtility.FromAudioClip(recording);
            
            if (audioData == null || audioData.Length == 0)
            {
                Debug.LogError("[GeminiVoiceInterface] Audio data is empty!");
                chatManager?.FinalizeUserTypingBubble("[Empty audio]");
                if (statusText != null) statusText.text = "No audio recorded. Please try again.";
                return;
            }

            StartCoroutine(SendAudioToSTT(audioData));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GeminiVoiceInterface] Exception in StopAndProcessRecording: {ex.Message}");
            chatManager?.FinalizeUserTypingBubble("[Recording error]");
            if (statusText != null) statusText.text = "Recording processing error. Please try again.";
            isRecording = false;
            wakeWordListener?.ResumeListening();
        }
    }

    private IEnumerator SendAudioToSTT(byte[] audioData)
    {
        const int maxRetries = 3;
        float retryDelay = 1f;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            UnityWebRequest request = null;
            bool hasException = false;
            string exceptionMessage = null;
            
            request = UnityWebRequest.PostWwwForm(speechToText_URL, "POST");
            request.certificateHandler = new CertsHandler();
            request.uploadHandler = new UploadHandlerRaw(audioData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "audio/wav");
            request.timeout = 15; // 15 second timeout

            yield return request.SendWebRequest();

            // Process result outside of try-catch to allow proper control flow
            bool success = false;
            bool shouldRetryRequest = false;
            bool shouldBreak = false;
            float waitTime = 0f;
            string transcription = null;

            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    transcription = request.downloadHandler.text;
                    
                    // Validate transcription
                    if (string.IsNullOrWhiteSpace(transcription))
                    {
                        Debug.LogWarning("[GeminiVoiceInterface] STT returned empty transcription");
                        if (attempt < maxRetries)
                        {
                            Debug.Log($"[GeminiVoiceInterface] Retrying STT ({attempt}/{maxRetries})...");
                            request?.Dispose();
                            shouldRetryRequest = true;
                            waitTime = retryDelay;
                            retryDelay *= 2f;
                        }
                        else
                        {
                            if (statusText != null) statusText.text = "Could not understand audio. Please try again.";
                            chatManager?.FinalizeUserTypingBubble("[No speech detected]");
                            wakeWordListener?.ResumeListening();
                            request?.Dispose();
                            shouldBreak = true;
                        }
                    }
                    else
                    {
                        Debug.Log("[GeminiVoiceInterface] 🗣 STT -> " + transcription);
                        chatManager?.FinalizeUserTypingBubble(transcription);
                        success = true;
                    }
                }
                else
                {
                    // Handle specific error cases
                    string errorMsg = $"STT Error (attempt {attempt}/{maxRetries}): {request.error}";
                    
                    if (request.responseCode == 429) // Rate limited
                    {
                        Debug.LogWarning($"[GeminiVoiceInterface] {errorMsg} - Rate limited");
                        if (attempt < maxRetries)
                        {
                            request?.Dispose();
                            shouldRetryRequest = true;
                            waitTime = retryDelay * 2f;
                            retryDelay *= 2f;
                        }
                    }
                    else if (request.responseCode >= 500 && request.responseCode < 600) // Server error
                    {
                        Debug.LogWarning($"[GeminiVoiceInterface] {errorMsg} - Server error");
                        if (attempt < maxRetries)
                        {
                            request?.Dispose();
                            shouldRetryRequest = true;
                            waitTime = retryDelay;
                            retryDelay *= 2f;
                        }
                    }
                    else if (request.responseCode == 0) // Network error
                    {
                        Debug.LogWarning($"[GeminiVoiceInterface] {errorMsg} - Network error");
                        if (attempt < maxRetries)
                        {
                            request?.Dispose();
                            shouldRetryRequest = true;
                            waitTime = retryDelay;
                            retryDelay *= 2f;
                        }
                    }
                    
                    if (!shouldRetryRequest)
                    {
                        Debug.LogError($"[GeminiVoiceInterface] {errorMsg}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                hasException = true;
                exceptionMessage = ex.Message;
            }
            finally
            {
                request?.Dispose();
            }

            // Handle control flow outside of try-catch
            if (shouldBreak)
            {
                yield break;
            }

            if (shouldRetryRequest)
            {
                yield return new WaitForSeconds(waitTime);
                continue;
            }

            if (success && !string.IsNullOrEmpty(transcription))
            {
                // Start Gemini request
                StartCoroutine(SendPromptToGemini(transcription));
                yield break;
            }

            if (hasException)
            {
                Debug.LogError($"[GeminiVoiceInterface] STT Exception (attempt {attempt}/{maxRetries}): {exceptionMessage}");
                if (attempt < maxRetries)
                {
                    yield return new WaitForSeconds(retryDelay);
                    retryDelay *= 2f;
                    continue;
                }
            }
        }

        // All retries failed
        if (statusText != null) statusText.text = "Speech recognition service unavailable. Please try again.";
        chatManager?.FinalizeUserTypingBubble("[Transcription failed]");
        wakeWordListener?.ResumeListening();
    }

    private IEnumerator SendPromptToGemini(string userText)
    {
        if (_geminiInFlight) yield break;
        _geminiInFlight = true;

        if (!geminiClient.HasEndpoint)
        {
            chatManager?.FinalizeBotTypingBubble("⚠️ Cannot send: no active conversation.");
            if (statusText != null) statusText.text = "No active conversation.";
            _geminiInFlight = false;
            yield break;
        }

        if (statusText != null) statusText.text = "Cassandra is thinking...";
        chatManager?.CreateBotTypingBubble();

        string toSend = userText;
        if (confidentialMode && redactChatContent)
            toSend = RedactFreeText(userText);

        yield return geminiClient.SendPrompt(
            toSend,
            onSuccess: geminiJson =>
            {
                _geminiInFlight = false;
                HandleGeminiResponseAndSpeak(geminiJson);
            },
            onError: error =>
            {
                _geminiInFlight = false;
                string userMsg = "Cassandra error.";
                if (!string.IsNullOrEmpty(error) && error.StartsWith("SERVER_UNRESPONSIVE|"))
                    userMsg = "The server is not responding. Please try again later.";

                if (!suppressVerboseLogs) Debug.LogError("[GeminiVoiceInterface] " + error);
                if (statusText != null) statusText.text = userMsg;
                chatManager?.FinalizeBotTypingBubble(userMsg);
                wakeWordListener?.ResumeListening();
            }
        );
    }

    [System.Serializable]
    private class GeminiContinueResponse
    {
        public string conversation_id;
        public string response;
        public bool feedback;
    }

    private void HandleGeminiResponseAndSpeak(string geminiJson)
    {
        try
        {
            HandleGeminiResponseAndSpeakInternal(geminiJson);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GeminiVoiceInterface] Exception in HandleGeminiResponseAndSpeak: {ex.Message}\n{ex.StackTrace}");
            if (statusText != null) statusText.text = "Error processing response. Please try again.";
            chatManager?.FinalizeBotTypingBubble("An error occurred while processing the response.");
            wakeWordListener?.ResumeListening();
        }
    }

    private void HandleGeminiResponseAndSpeakInternal(string geminiJson)
    {
        GeminiContinueResponse parsed = null;
        try { parsed = JsonUtility.FromJson<GeminiContinueResponse>(geminiJson); } catch { }

        string outputText;
        if (parsed != null)
        {
            string expectedId = ExtractConversationIdFromUrl(studyState != null ? studyState.converationURL : null);
            if (!string.IsNullOrEmpty(parsed.conversation_id) && !string.IsNullOrEmpty(expectedId))
            {
                if (!string.Equals(parsed.conversation_id, expectedId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!suppressVerboseLogs)
                        Debug.LogWarning($"[GeminiVoiceInterface] Conversation ID mismatch. Expected={expectedId} Got={parsed.conversation_id}");
                    chatManager?.AddBotMessage("Conversation ID mismatch (server/client).");
                }
            }

            outputText = parsed.feedback
                ? "Your correction has been accepted."
                : (string.IsNullOrWhiteSpace(parsed.response) ? "[Empty response]" : parsed.response);
        }
        else
        {
            outputText = string.IsNullOrWhiteSpace(geminiJson) ? "[Empty response]" : geminiJson;
        }

        if (confidentialMode && redactChatContent)
            outputText = RedactFreeText(outputText);

        chatManager?.FinalizeBotTypingBubble(outputText);

        if (!(confidentialMode && disableTtsInConfidential))
            StartCoroutine(SendToTTSAndPlay(outputText));

        if (confidentialMode && disableTtsInConfidential && statusText != null)
            statusText.text = "Confidential mode: voice playback disabled.";
    }


    // Accepte: /conversation/{id}/continue  ou /conversations/{id}/continue
    //          /conversation/{id}          ou /conversations/{id}
    private static string ExtractConversationIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var u = new Uri(url.Trim());
            var segs = u.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < segs.Length; i++)
            {
                var s = segs[i];
                if (s.Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("conversations", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < segs.Length)
                    {
                        var candidate = segs[i + 1];
                        if (!candidate.Equals("continue", StringComparison.OrdinalIgnoreCase) &&
                            !string.IsNullOrWhiteSpace(candidate))
                            return candidate;
                    }
                }
            }
        }
        catch { /* ignore parse error */ }

        var m = Regex.Match(url, @"([0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12})");
        if (m.Success) return m.Groups[1].Value;

        return null;
    }

    // ---- TTS ----
    private IEnumerator SendToTTSAndPlay(string fullText)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            yield break;

        if (statusText != null)
            statusText.text = "Playing the response...";

        string clean = NormalizeWhitespace(fullText);
        var chunks = BuildTtsChunks(clean,
                                    maxCharsPerChunk: 900,
                                    hardMaxChars: 1200,
                                    maxChunks: 8);

        if (chunks.Count == 0)
            chunks.Add("...");

        isSpeaking = true;

        // Cache manager lookup (do once, not in loop)
        var cacheManager = GameObject.FindObjectOfType(System.Type.GetType("ResponseCacheManager"));
        var tryGetMethod = cacheManager?.GetType().GetMethod("TryGetCachedAudio");
        var cacheMethod = cacheManager?.GetType().GetMethod("CacheAudio");

        for (int i = 0; i < chunks.Count; i++)
        {
            if (!isSpeaking) break;

            var chunk = chunks[i];
            AudioClip clip = null;
            bool cacheException = false;

            // Try to check cache if available
            if (tryGetMethod != null)
            {
                try
                {
                    object[] parameters = new object[] { chunk, null };
                    bool cached = (bool)tryGetMethod.Invoke(cacheManager, parameters);
                    if (cached && parameters[1] != null)
                    {
                        clip = (AudioClip)parameters[1];
                        Debug.Log($"[GeminiVoiceInterface] Using cached TTS audio ({i + 1}/{chunks.Count})");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[GeminiVoiceInterface] Cache lookup failed: {ex.Message}");
                    cacheException = true;
                }
            }

            if (clip == null)
            {
                // Fetch from TTS service
                bool fetchComplete = false;
                yield return TtsFetchWithRetry(chunk, c => 
                {
                    clip = c;
                    fetchComplete = true;
                    
                    // Try to cache the audio clip if cache manager exists
                    if (c != null && cacheMethod != null && !cacheException)
                    {
                        try
                        {
                            cacheMethod.Invoke(cacheManager, new object[] { chunk, c });
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[GeminiVoiceInterface] Failed to cache TTS audio: {ex.Message}");
                        }
                    }
                }, err =>
                {
                    Debug.LogError($"[GeminiVoiceInterface][TTS] Chunk {i + 1}/{chunks.Count} failed: {err}");
                    fetchComplete = true;
                });

                // Safety timeout
                float timeout = 30f;
                float elapsed = 0f;
                while (!fetchComplete && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (elapsed >= timeout)
                {
                    Debug.LogError($"[GeminiVoiceInterface][TTS] Chunk {i + 1}/{chunks.Count} timed out");
                    continue;
                }
            }

            if (!isSpeaking) break;

            if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
            {
                Debug.LogWarning($"[GeminiVoiceInterface] TTS: chunk {i + 1}/{chunks.Count} audio invalid, skipping.");
                continue;
            }

            bool playbackException = false;
            try
            {
                audioSource.clip = clip;
                audioSource.Play();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GeminiVoiceInterface] Audio playback error: {ex.Message}");
                playbackException = true;
            }

            if (playbackException)
                break;

            yield return new WaitWhile(() => isSpeaking && audioSource.isPlaying);
        }

        isSpeaking = false;

        if (statusText != null)
            statusText.text = "Say the wake word or press to speak";

        wakeWordListener?.ResumeListening();
    }

    public void ForceStopTTS()
    {
        if (isSpeaking && audioSource != null && audioSource.isPlaying)
        {
            Debug.Log("[GeminiVoiceInterface] ⛔ Interruption vocale demandée");
            audioSource.Stop();
            isSpeaking = false;
            
            // Try to cancel any pending TTS requests if queue manager exists
            if (!string.IsNullOrEmpty(_currentTTSRequestId))
            {
                var queueManager = GameObject.FindObjectOfType(System.Type.GetType("RequestQueueManager"));
                if (queueManager != null)
                {
                    var cancelMethod = queueManager.GetType().GetMethod("CancelRequest");
                    if (cancelMethod != null)
                    {
                        cancelMethod.Invoke(queueManager, new object[] { _currentTTSRequestId });
                    }
                }
                _currentTTSRequestId = null;
            }
            
            if (statusText != null)
                statusText.text = "Say the wake word or press to speak";
        }
        else
        {
            Debug.Log("[GeminiVoiceInterface] ForceStopTTS() appelé mais rien à stopper.");
        }
    }

    /// <summary>
    /// Cancels any in-progress Gemini request.
    /// </summary>
    public void CancelGeminiRequest()
    {
        if (_geminiInFlight)
        {
            if (!string.IsNullOrEmpty(_currentGeminiRequestId))
            {
                var queueManager = GameObject.FindObjectOfType(System.Type.GetType("RequestQueueManager"));
                if (queueManager != null)
                {
                    var cancelMethod = queueManager.GetType().GetMethod("CancelRequest");
                    if (cancelMethod != null)
                    {
                        cancelMethod.Invoke(queueManager, new object[] { _currentGeminiRequestId });
                    }
                }
                _currentGeminiRequestId = null;
            }
            
            _geminiInFlight = false;
            
            if (statusText != null)
                statusText.text = "Request cancelled";
            
            Debug.Log("[GeminiVoiceInterface] Gemini request cancelled by user");
        }
    }

    /// <summary>
    /// Cancels all in-progress operations (STT, Gemini, TTS).
    /// </summary>
    public void CancelAllOperations()
    {
        CancelGeminiRequest();
        ForceStopTTS();
        
        // Stop recording if active
        if (isRecording)
        {
            isRecording = false;
            OnStopListening?.Invoke();
            Microphone.End(null);
            chatManager?.RemoveListeningBubble();
            wakeWordListener?.ResumeListening();
        }
        
        if (statusText != null)
            statusText.text = "All operations cancelled";
        
        Debug.Log("[GeminiVoiceInterface] All operations cancelled");
    }

    // -------- Retry policy TTS --------
    [SerializeField] private int ttsMaxAttempts = 4;
    [SerializeField] private float ttsInitialBackoffSec = 1.0f;
    [SerializeField] private float ttsMaxBackoffSec = 8.0f;
    [SerializeField] private float ttsJitterMin = 0.5f;
    [SerializeField] private float ttsJitterMax = 1.5f;

    private IEnumerator TtsFetchWithRetry(string text, System.Action<AudioClip> onSuccess, System.Action<string> onError)
    {
        TTSRequest data = new TTSRequest { text = text };
        string jsonData = JsonUtility.ToJson(data);

        int attempt = 0;
        float backoff = ttsInitialBackoffSec;

        while (true)
        {
            attempt++;

            UnityWebRequest request = new UnityWebRequest(textToSpeech_URL, "POST");
            request.certificateHandler = new CertsHandler();
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);


            request.downloadHandler = new DownloadHandlerAudioClip(textToSpeech_URL, AudioType.MPEG);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "audio/mpeg, audio/mpeg3, audio/mp3, */*");

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            int status = (int)request.responseCode;

            if (ok)
            {
                var clip = DownloadHandlerAudioClip.GetContent(request);
                onSuccess?.Invoke(clip);
                yield break;
            }

            if (!ShouldRetryTts(status, request.error))
            {
                onError?.Invoke(BuildTtsError(status, request.error));
                yield break;
            }

            if (attempt >= ttsMaxAttempts)
            {
                onError?.Invoke(BuildTtsError(status, request.error) + $" (retried {attempt}x)");
                yield break;
            }

            float delay = ComputeDelayFromRetryAfter(request.GetResponseHeader("Retry-After"));
            if (delay <= 0f)
            {
                delay = Mathf.Min(backoff, ttsMaxBackoffSec);
                float jitter = UnityEngine.Random.Range(ttsJitterMin, ttsJitterMax);
                delay *= jitter;
            }

            Debug.LogWarning($"[GeminiVoiceInterface][TTS] HTTP {status} -> retry {attempt}/{ttsMaxAttempts - 1} in {delay:0.00}s");
            yield return new WaitForSecondsRealtime(delay);
            backoff = Mathf.Min(backoff * 2f, ttsMaxBackoffSec);
        }
    }

    private static bool ShouldRetryTts(int status, string transportError)
    {
        if (status == 429) return true;
        if (status >= 500 && status <= 599) return true;

        if (!string.IsNullOrEmpty(transportError))
        {
            var e = transportError.ToLowerInvariant();
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

        if (System.DateTime.TryParse(retryAfterHeader, out var when))
        {
            var delta = (float)(when.ToUniversalTime() - System.DateTime.UtcNow).TotalSeconds;
            return Mathf.Max(0f, delta);
        }
        return 0f;
    }

    private static string BuildTtsError(int status, string unityError)
    {
        var sb = new System.Text.StringBuilder();
        if (status > 0) sb.Append($"HTTP {status}. ");
        if (!string.IsNullOrEmpty(unityError)) sb.Append(unityError);
        return sb.ToString();
    }

    // --------- Utilities texte → chunks ---------

    private static string NormalizeWhitespace(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Replace("\r\n", "\n").Replace("\r", "\n");
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        while (s.Contains("\n\n\n")) s = s.Replace("\n\n\n", "\n\n");
        return s.Trim();
    }

    private static System.Collections.Generic.List<string> BuildTtsChunks(string text, int maxCharsPerChunk, int hardMaxChars, int maxChunks)
    {
        var chunks = new System.Collections.Generic.List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var sentences = text.Split(new[] { ". ", "? ", "! ", "\n" }, System.StringSplitOptions.None);

        var current = new System.Text.StringBuilder();
        foreach (var raw in sentences)
        {
            var sentence = raw?.Trim();
            if (string.IsNullOrEmpty(sentence)) continue;

            if (sentence.Length > hardMaxChars)
                sentence = sentence.Substring(0, hardMaxChars - 3) + "...";

            if (current.Length > 0 && current.Length + sentence.Length + 2 > maxCharsPerChunk)
            {
                chunks.Add(current.ToString().Trim());
                current.Length = 0;

                if (chunks.Count >= maxChunks) break;
            }

            if (current.Length > 0) current.Append(" ");
            current.Append(sentence);
        }

        if (current.Length > 0 && chunks.Count < maxChunks)
            chunks.Add(current.ToString().Trim());

        if (chunks.Count == 0)
            chunks.Add(text.Length > hardMaxChars ? text.Substring(0, hardMaxChars - 3) + "..." : text);

        return chunks;
    }

    [System.Serializable]
    private class ConversationHistoryResponse
    {
        public string conversation_id;
        public int study_id;
        public ConversationMessage[] messages;
    }

    [System.Serializable]
    private class ConversationMessage
    {
        public string role;
        public string content;
    }

    private IEnumerator FetchAndPopulateConversation()
    {
        _conversationLoading = true;

        string continueUrl = studyState?.converationURL;
        string convId = ExtractConversationIdFromUrl(continueUrl);

        if (string.IsNullOrEmpty(convId))
        {
            chatManager?.AddBotMessage("No active conversation for this study.");
            if (statusText != null) statusText.text = " Conversation ID not found for this study.";
            _conversationLoading = false;
            yield break;
        }

        string getUrl = BuildGetConversationUrl(continueUrl, convId);
        if (string.IsNullOrEmpty(getUrl))
        {
            chatManager?.AddBotMessage("GET conversation endpoint not found.");
            if (statusText != null) statusText.text = "GET conversation endpoint not found.";
            _conversationLoading = false;
            yield break;
        }

        using (var req = UnityWebRequest.Get(getUrl))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.certificateHandler = new CertsHandler();
            req.SetRequestHeader("Accept", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var json = req.downloadHandler.text;
                ConversationHistoryResponse resp = null;
                try { resp = JsonUtility.FromJson<ConversationHistoryResponse>(json); } catch { }

                bool hasHistory = resp != null && resp.messages != null && resp.messages.Length > 0;

                if (hasHistory && resp.messages.Length <= 3)
                {
                    Debug.Log("[GeminiVoiceInterface] Only 3 messages found → skipping history and showing welcome message.");
                    chatManager?.ClearChat();
                    PostWelcomeOnce();
                    if (statusText != null) statusText.text = "New conversation (short history ignored).";
                }
                else if (hasHistory)
                {
                    foreach (var m in resp.messages)
                    {
                        if (m == null || string.IsNullOrWhiteSpace(m.content)) continue;
                        if (m.role == "user") chatManager?.AddUserMessage(m.content);
                        else if (m.role == "assistant") chatManager?.AddBotMessage(m.content);
                    }
                    if (statusText != null) statusText.text = "Conversation restored.";
                }
                else
                {
                    PostWelcomeOnce();
                    if (statusText != null) statusText.text = "New conversation.";
                }
            }
            else
            {
                Debug.LogError("[GeminiVoiceInterface] GET conversation error: " + req.error);
                PostWelcomeOnce();
                if (statusText != null) statusText.text = "New conversation (GET failed).";
            }
        }

        _conversationLoading = false;
    }

    private void PostWelcomeOnce()
    {
        if (_welcomeShown) return;
        _welcomeShown = true;
        chatManager?.AddBotMessage(BuildWelcomeMessage());
        if (confidentialMode)
            PostConfidentialBanner();
    }

    private void PostConfidentialBanner()
    {
        chatManager?.AddBotMessage("🔒 Confidential mode is enabled. Identifying details are redacted.");
    }

    private string BuildWelcomeMessage()
    {
        // Study
        string studyLabel = !string.IsNullOrWhiteSpace(studyState?.title)
            ? studyState.title
            : (string.IsNullOrWhiteSpace(studyState?.code) ? "Unspecified study" : studyState.code);

        // Patient
        string fullName = "";
        if (studyState?.patient != null)
        {
            var first = studyState.patient.firstName ?? "";
            var last = studyState.patient.lastName ?? "";
            fullName = (first + " " + last).Trim();
        }
        if (string.IsNullOrWhiteSpace(fullName)) fullName = "Unspecified patient";

        string dob = studyState?.patient?.dateOfBirth;

        if (confidentialMode && redactUiIdentity)
        {
            studyLabel = RedactStudyLabel(studyLabel);
            (fullName, dob) = RedactPatientIdentity(fullName, dob);
        }

        string dobText = "";
        if (!string.IsNullOrWhiteSpace(dob))
        {
            if (DateTime.TryParse(dob, out var dt))
                dobText = $" (born {dt:yyyy-MM-dd})";
        }

        return
            $"Hello, I'm Cassandra, your clinical analysis assistant.\n" +
            $"Active case: <b>{studyLabel}</b>.\n" +
            $"Patient: <b>{fullName}</b>{dobText}.\n" +
            $"Would you like me to summarize the segmentation metrics, check data consistency, or provide a clinical interpretation?";
    }

    private static string RedactStudyLabel(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Study";
        // Keep modality hint if present, otherwise generic
        // e.g., "BraTS-GLI-00014-000-seg" -> "Study (redacted)"
        return "Study (redacted)";
    }

    private static (string name, string dob) RedactPatientIdentity(string fullName, string dob)
    {
        // Reduce "John Smith" -> "J. S." ; remove DOB or keep year only
        string initials = "Patient";
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var parts = fullName.Trim().Split(' ');
            if (parts.Length >= 2)
            {
                string i1 = string.IsNullOrEmpty(parts[0]) ? "" : parts[0].Substring(0,1).ToUpper();
                string i2 = string.IsNullOrEmpty(parts[^1]) ? "" : parts[^1].Substring(0,1).ToUpper();
                initials = $"{i1}. {i2}.";
            }
            else
            {
                var p = parts[0];
                initials = string.IsNullOrEmpty(p) ? "Patient" : $"{char.ToUpper(p[0])}.";
            }
        }

        string dobRedacted = null;
        if (!string.IsNullOrWhiteSpace(dob))
        {
            // Keep year only if we can parse, otherwise drop
            if (DateTime.TryParse(dob, out var dt)) dobRedacted = dt.Year.ToString();
            else dobRedacted = null;
        }
        return (initials, dobRedacted);
    }


    // Construit l’URL GET à partir de l’URL "continue" du SO.
    // Exemples:
    //  in: .../conversation/{id}/continue  -> out: .../conversation/{id}
    //  in: .../conversations/{id}/continue -> out: .../conversations/{id}
    //  in: .../conversation/{id}           -> out: .../conversation/{id} (inchangé)
    private static string BuildGetConversationUrl(string continueUrl, string conversationId)
    {
        if (string.IsNullOrWhiteSpace(continueUrl) || string.IsNullOrWhiteSpace(conversationId))
            return null;

        try
        {
            var u = new Uri(continueUrl.Trim());
            var segs = new System.Collections.Generic.List<string>(
                u.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            );

            for (int i = 0; i < segs.Count; i++)
            {
                bool isConv = segs[i].Equals("conversation", StringComparison.OrdinalIgnoreCase) ||
                            segs[i].Equals("conversations", StringComparison.OrdinalIgnoreCase);

                if (!isConv) continue;

                if (i + 1 < segs.Count &&
                    segs[i + 1].Equals(conversationId, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 2 < segs.Count &&
                        segs[i + 2].Equals("continue", StringComparison.OrdinalIgnoreCase))
                    {
                        segs.RemoveAt(i + 2);
                    }

                    string newPath = "/" + string.Join("/", segs);
                    var b = new UriBuilder(u.Scheme, u.Host, u.IsDefaultPort ? -1 : u.Port, newPath);
                    return b.Uri.ToString();
                }
            }

            var left = u.GetLeftPart(UriPartial.Authority);
            return $"{left}/conversation/{conversationId}";
        }
        catch
        {
            var parts = continueUrl.Split(new[] { "://" }, StringSplitOptions.None);
            if (parts.Length >= 2)
            {
                var hostAndAfter = parts[1];
                var hostOnly = hostAndAfter.Split('/')[0];
                return $"{parts[0]}://{hostOnly}/conversation/{conversationId}";
            }
            return null;
        }
    }

    private static string RedactFreeText(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;

        s = Regex.Replace(s, @"\b(\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{2,4})\b", "[DATE]");

        s = Regex.Replace(s, @"\b[0-9A-Za-z]{3,}[-_][0-9A-Za-z\-_.]{3,}\b", "[ID]");

        s = Regex.Replace(s, @"\b([A-Z][a-z]+)\s+([A-Z][a-z]+)\b", m => $"{m.Groups[1].Value[0]}. {m.Groups[2].Value[0]}."); 

        s = Regex.Replace(s, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", "[EMAIL]");
        s = Regex.Replace(s, @"\+?\d[\d\s\-\(\)]{6,}\d", "[PHONE]");

        return s;
    }

}


[System.Serializable]
public class TTSRequest { public string text; }
