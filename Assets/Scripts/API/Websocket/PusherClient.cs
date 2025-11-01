using UnityEngine;
using WebSocketSharp;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public class PusherClient : MonoBehaviour
{
    [Header("Endpoint")]
    public string wsUrl = "ws://holonauts.fr:2025/app/9weqrk5tbh6jukkrngvb?protocol=7&client=js&version=8.2.0&flash=false";

    [Header("Channel")]
    public string channelName = "vr-status";

    public static PusherClient Instance { get; private set; }

    private WebSocket socket;
    private bool _isConnecting = false;
    private float _nextReconnectAllowedTime = 0f;
    private int _connectAttempts = 0;

    // ces deux là ne DOIVENT être écrits que dans Update()
    private float lastServerActivityTime = 0f;
    private float activityTimeoutSeconds = 30f;

    // flags thread-safe depuis le thread réseau
    private volatile bool _heartbeatPending = false;
    private volatile bool _connectionOpenedPending = false;
    private volatile bool _connectionClosedPending = false;

    // queue thread-safe d'événements métier
    private readonly Queue<VrStatusPayload> _pendingVrEvents = new Queue<VrStatusPayload>();
    private readonly object _lockQueue = new object();

    private const bool VERBOSE_LOG = true;

    // === EVENT PUBLIC exposé à Unity ===
    public event Action<VrStatusPayload> OnVrStatusChanged;

    [Serializable]
    public class VrStatusPayload
    {
        public int study_id;
        public string study_code;
        public string study_title;
        public string patient_name;
        public bool is_vr;
        public string action;       // "enabled"/"disabled"
        public string timestamp;
        public string message;
    }

    [Serializable]
    private class PusherFrame
    {
        [JsonProperty("event")]
        public string Event;

        [JsonProperty("data")]
        public object Data;

        [JsonProperty("channel")]
        public string Channel;

        [JsonIgnore]
        public string DataRaw
        {
            get
            {
                if (Data == null) return null;
                if (Data is string s) return s;
                return JsonConvert.SerializeObject(Data);
            }
        }
    }

    [Serializable]
    private class ConnectionEstablishedData
    {
        public string socket_id;
        public float activity_timeout;
    }

    [Serializable]
    private class OutgoingFrame
    {
        [JsonProperty("event")]
        public string Event;

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data;
    }

    private float _lastHealthCheck = 0f;
    private const float HEALTH_CHECK_INTERVAL = 5.0f;

    // ---------- UNITY LIFECYCLE ----------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WS] Duplicate PusherClient detected -> destroying this one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[PUSHER] Awake, wsUrl=" + wsUrl);
    }

    private void Start()
    {
        Debug.Log("[PUSHER] Start: connecting…");
        ForceConnect();
    }

    private void Update()
    {
        // Consommer les heartbeats / open
        if (_heartbeatPending || _connectionOpenedPending)
        {
            lastServerActivityTime = Time.realtimeSinceStartup;
            _heartbeatPending = false;
            _connectionOpenedPending = false;
        }

        // Consommer le flag "closed"
        if (_connectionClosedPending)
        {
            _connectionClosedPending = false;
            // socket déjà nullifié dans SafeOnClose()
        }

        // Dispatcher les payloads vr.status.changed sur le thread Unity
        DispatchPendingVrEventsOnMainThread();

        // Maintenance/reconnexion (cooldown)
        MaintainConnection();
    }

    private void OnDestroy()
    {
        CleanupSocket();
        if (Instance == this) Instance = null;
    }

    // ---------- CONNECTION MGMT ----------
    private void ForceConnect()
    {
        if (_isConnecting)
            return;

        _isConnecting = true;
        _connectAttempts++;

        if (VERBOSE_LOG)
            Debug.Log("[WS] Connect() called. Total connect attempts = " + _connectAttempts);

        CleanupSocket();

        socket = new WebSocket(wsUrl);

        socket.OnOpen += SafeOnOpen;
        socket.OnMessage += SafeOnMessage;
        socket.OnClose += SafeOnClose;
        socket.OnError += SafeOnError;

        socket.ConnectAsync(); // thread worker
    }

    private void MaintainConnection()
    {
        if (Time.realtimeSinceStartup - _lastHealthCheck < HEALTH_CHECK_INTERVAL)
            return;
        _lastHealthCheck = Time.realtimeSinceStartup;

        bool alive = (socket != null && socket.IsAlive);
        bool needConnect = !alive && !_isConnecting;

        bool idleTooLong = false;
        if (alive)
        {
            float idleFor = Time.realtimeSinceStartup - lastServerActivityTime;
            if (idleFor > (activityTimeoutSeconds + 5f))
            {
                idleTooLong = true;
            }
        }

        if ((needConnect || idleTooLong) && Time.realtimeSinceStartup >= _nextReconnectAllowedTime)
        {
            if (VERBOSE_LOG)
            {
                Debug.Log("[WS] Reconnect requested... (needConnect=" + needConnect + ", idleTooLong=" + idleTooLong + ")");
            }

            _nextReconnectAllowedTime = Time.realtimeSinceStartup + 5f;
            ForceConnect();
        }
    }

    private void CleanupSocket()
    {
        if (socket != null)
        {
            try
            {
                socket.OnOpen -= SafeOnOpen;
                socket.OnMessage -= SafeOnMessage;
                socket.OnClose -= SafeOnClose;
                socket.OnError -= SafeOnError;

                if (socket.IsAlive)
                    socket.Close();
            }
            catch { /* ignore */ }

            socket = null;
        }
    }

    // ---------- SAFE CALLBACKS (THREAD WS) ----------
    private void SafeOnOpen(object sender, EventArgs e)
    {
        try
        {
            _isConnecting = false;
            _connectionOpenedPending = true;
            Debug.Log("[WS] Connected.");
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] SafeOnOpen exception: " + ex.Message);
        }
    }

    private void SafeOnClose(object sender, CloseEventArgs e)
    {
        try
        {
            Debug.LogWarning("[WS] Closed. Code=" + e.Code + " Reason=" + e.Reason);
            _isConnecting = false;
            socket = null;
            _connectionClosedPending = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] SafeOnClose exception: " + ex.Message);
        }
    }

    private void SafeOnError(object sender, ErrorEventArgs e)
    {
        try
        {
            Debug.LogError("[WS] Socket error: " + e.Message);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] SafeOnError exception: " + ex.Message);
        }
    }

    private void SafeOnMessage(object sender, MessageEventArgs e)
    {
        try
        {
            if (!e.IsText)
                return;

            string raw = e.Data;

            // ping/pong
            if (raw.Contains("\"event\":\"pusher:ping\""))
            {
                SendFrame(new OutgoingFrame { Event = "pusher:pong" });
                _heartbeatPending = true;
                return;
            }

            // éviter de parser du JSON pour rien
            if (!(raw.Contains("pusher:connection_established")
               || raw.Contains("pusher_internal:subscription_succeeded")
               || raw.Contains("vr.status.changed")
               || raw.Contains("pusher:error")))
            {
                _heartbeatPending = true;
                return;
            }

            PusherFrame frame;
            try
            {
                frame = JsonConvert.DeserializeObject<PusherFrame>(raw);
            }
            catch (Exception ex2)
            {
                Debug.LogError("[WS] JSON frame parse error: " + ex2.Message);
                return;
            }

            if (frame == null || string.IsNullOrEmpty(frame.Event))
            {
                _heartbeatPending = true;
                return;
            }

            switch (frame.Event)
            {
                case "pusher:connection_established":
                    HandleConnectionEstablished_FromWorker(frame);
                    break;

                case "pusher_internal:subscription_succeeded":
                    Debug.Log("[WS] Subscription ok for " + frame.Channel);
                    break;

                case "vr.status.changed":
                    HandleVrStatusChanged_FromWorker(frame);
                    break;

                case "pusher:error":
                    Debug.LogError("[WS] Pusher error: " + frame.DataRaw);
                    break;
            }

            _heartbeatPending = true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] SafeOnMessage exception: " + ex.Message);
        }
    }

    // ---------- HANDLERS (THREAD WS) ----------
    private void HandleConnectionEstablished_FromWorker(PusherFrame frame)
    {
        ConnectionEstablishedData inner = null;
        try
        {
            inner = JsonConvert.DeserializeObject<ConnectionEstablishedData>(frame.DataRaw);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] connection_established parse error: " + ex.Message);
        }

        if (inner != null)
        {
            activityTimeoutSeconds = inner.activity_timeout;
            Debug.Log("[WS] Established. timeout=" + activityTimeoutSeconds + "s");
        }

        // subscribe au channel
        Subscribe(channelName);
    }

    private void HandleVrStatusChanged_FromWorker(PusherFrame frame)
    {
        VrStatusPayload payload = null;

        try
        {
            payload = JsonConvert.DeserializeObject<VrStatusPayload>(frame.DataRaw);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] vr.status.changed payload parse error: " + ex.Message);
            return;
        }

        if (payload == null)
            return;

        lock (_lockQueue)
        {
            _pendingVrEvents.Enqueue(payload);
        }
    }

    // ---------- SEND ----------
    private void Subscribe(string channel)
    {
        var sub = new
        {
            // pusher:subscribe est le message valide
            @event = "pusher:subscribe",
            data = new { channel = channel }
        };

        string json = JsonConvert.SerializeObject(sub);
        SafeSend(json);
    }

    private void SendFrame(OutgoingFrame frame)
    {
        string json = JsonConvert.SerializeObject(frame);
        SafeSend(json);
    }

    private void SafeSend(string json)
    {
        var s = socket;
        if (s == null) return;
        if (!s.IsAlive) return;

        try
        {
            s.Send(json);
        }
        catch (Exception ex)
        {
            Debug.LogError("[WS] Send error: " + ex.Message);
        }
    }

    // ---------- MAIN THREAD DISPATCH ----------
    private void DispatchPendingVrEventsOnMainThread()
    {
        if (_pendingVrEvents.Count == 0)
            return;

        while (true)
        {
            VrStatusPayload evt = null;
            lock (_lockQueue)
            {
                if (_pendingVrEvents.Count > 0)
                    evt = _pendingVrEvents.Dequeue();
                else
                    evt = null;
            }

            if (evt == null)
                break;

            try
            {
                OnVrStatusChanged?.Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogError("[WS] Exception in OnVrStatusChanged listener: " + ex.Message);
            }
        }
    }
}
