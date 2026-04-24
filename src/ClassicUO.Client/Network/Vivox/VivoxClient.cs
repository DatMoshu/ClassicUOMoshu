// VivoxClient.cs — Minimal Vivox Core SDK wrapper
// Ported from prototypes/VivoxSpike into ClassicUO.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Network.Vivox
{
    internal class VivoxClient : IDisposable
    {
        // ── Config ────────────────────────────────────────────────────────────
        public record Config(
            string Issuer,
            string SecretKey,
            string Domain,
            string Server
        );

        private readonly Config _config;
        private bool _initialized;

        private const string ConnectorHandle = "c1";
        private TaskCompletionSource<bool> _connectorTcs;

        private string _userId;
        private string _accountHandle;

        // Multi-session support: channelName → sessionHandle
        private readonly Dictionary<string, string> _sessionHandles = new();
        private string _proximitySessionHandle; // Shortcut for 3D position updates

        private CancellationTokenSource _pumpCts;

        public event Action<VxLoginState> LoginStateChanged;
        public event Action<string, string> ParticipantJoined;  // (participantUri, sessionHandle)
        public event Action<string, string> ParticipantLeft;    // (participantUri, sessionHandle)
        public event Action<string, bool, double> SpeakingChanged; // (participantUri, isSpeaking, energy)

        public bool IsLoggedIn => _accountHandle != null;

        public VivoxClient(Config config)
        {
            _config = config;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            var sdkConfig = new byte[VivoxNative.SDK_CONFIG_SIZE];
            int rc = VivoxNative.vx_get_default_config3(sdkConfig, (nuint)VivoxNative.SDK_CONFIG_SIZE);
            if (rc != 0) throw new VivoxException($"vx_get_default_config3 failed: rc={rc}");

            rc = VivoxNative.vx_initialize3(sdkConfig, (nuint)VivoxNative.SDK_CONFIG_SIZE);
            if (rc != 0) throw new VivoxException($"vx_initialize3 failed: rc={rc}");

            _initialized = true;
            Log.Trace("[Vivox] SDK initialized.");

            _connectorTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            VivoxNative.vx_req_connector_create_create(out IntPtr connReq);
            if (connReq == IntPtr.Zero)
                throw new VivoxException("vx_req_connector_create_create returned null.");

            VivoxStructWriter.SetConnectorCreateFields(connReq, ConnectorHandle, _config.Server);

            _pumpCts = new CancellationTokenSource();
            _ = Task.Run(() => MessagePump(_pumpCts.Token));

            rc = VivoxNative.vx_issue_request3(connReq, out int reqCount);
            if (rc != 0) throw new VivoxException($"Connector create request failed: rc={rc}");
            Log.Trace($"[Vivox] Connector create issued (handle='{ConnectorHandle}', server='{_config.Server}')");

            bool connectorOk = await _connectorTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (!connectorOk)
                throw new VivoxException("Connector create failed. Check Server URL and credentials.");

            Log.Trace("[Vivox] Connector ready.");
        }

        public void Shutdown()
        {
            _pumpCts?.Cancel();
            if (_initialized)
            {
                VivoxNative.vx_uninitialize();
                _initialized = false;
                Log.Trace("[Vivox] SDK shut down.");
            }
        }

        // ── Login ─────────────────────────────────────────────────────────────

        public void Login(string userId, string displayName)
        {
            EnsureInitialized();

            _userId        = userId;
            _accountHandle = $".{_config.Issuer}.{userId}.";

            string loginToken = VivoxToken.GenerateLoginToken(
                _config.Issuer, _config.SecretKey, userId, _config.Domain);

            Log.Trace($"[Vivox] Login request: acct='{_accountHandle}' display='{displayName}'");

            VivoxNative.vx_req_account_anonymous_login_create(out IntPtr req);
            VivoxStructWriter.SetLoginFields(req, ConnectorHandle, _accountHandle, displayName, loginToken);

            int rc = VivoxNative.vx_issue_request(req);
            if (rc != 0) throw new VivoxException($"Login request failed: rc={rc}");
            Log.Trace("[Vivox] Login request sent.");
        }

        public void Logout()
        {
            if (_accountHandle == null) return;

            try
            {
                VivoxNative.vx_req_account_logout_create(out IntPtr req);
                VivoxStructWriter.SetLogoutFields(req, _accountHandle);
                VivoxNative.vx_issue_request(req);
            }
            catch (Exception ex)
            {
                Log.Warn($"[Vivox] Logout error: {ex.Message}");
            }
            _accountHandle = null;
            _sessionHandles.Clear();
            _proximitySessionHandle = null;
            Log.Trace("[Vivox] Logged out.");
        }

        // ── Channel ───────────────────────────────────────────────────────────

        // Track pending channel joins so SessionAdded can map handle → channel name
        private readonly Dictionary<string, string> _pendingJoins = new(); // sgHandle → channelName

        // Positional channel 3D attenuation (tile units — UO tiles).
        // Without these, Vivox falls back to dashboard defaults (effectively no
        // attenuation) and every player in the channel is heard at full volume
        // regardless of distance. These values embed into the channel URI via
        // vx_get_positional_channel_uri — they are NOT a per-request setting.
        //
        //   MAX_RANGE          — tiles beyond this: silent. ~UO "say" range.
        //   CLAMPING_DISTANCE  — tiles within this: full volume.
        //   ROLLOFF            — attenuation curve steepness (1.0 = default).
        //   DISTANCE_MODEL     — 1 = inverse_distance_clamped (default curve).
        //
        // Bumping MAX_RANGE makes the world feel bigger but makes crowded
        // streets noisier; drop it for tighter prox chat.
        private const int    PROX_MAX_RANGE         = 18;
        private const int    PROX_CLAMPING_DISTANCE = 2;
        private const double PROX_ROLLOFF           = 1.1;
        private const int    PROX_DISTANCE_MODEL    = 1; // inverse_distance_clamped

        public void JoinPositionalChannel(string channelName)
        {
            EnsureLoggedIn();

            // Build the URI via the SDK so the 3D properties are encoded in the
            // exact format the server expects. A hand-built "sip:confctl-d-..."
            // string drops the attenuation params and gives infinite range.
            string channelUri;
            IntPtr uriPtr = VivoxNative.vx_get_positional_channel_uri(
                name:               channelName,
                realm:               _config.Domain,
                max_range:           PROX_MAX_RANGE,
                clamping_distance:   PROX_CLAMPING_DISTANCE,
                rolloff:             PROX_ROLLOFF,
                distance_model:      PROX_DISTANCE_MODEL,
                issuer:              _config.Issuer);
            if (uriPtr == IntPtr.Zero)
            {
                Log.Warn("[Vivox] vx_get_positional_channel_uri returned null — falling back to plain URI (no attenuation!)");
                channelUri = $"sip:confctl-d-{_config.Issuer}.{channelName}@{_config.Domain}";
            }
            else
            {
                channelUri = Marshal.PtrToStringAnsi(uriPtr);
                VivoxNative.vx_free(uriPtr);
            }

            // Token "to" claim MUST match the join URI exactly, including the
            // embedded 3D properties segment, or Vivox rejects the join.
            string joinToken = VivoxToken.GenerateJoinTokenForUri(
                _config.Issuer, _config.SecretKey, _userId, channelUri, _config.Domain);

            string sgHandle   = $"sg_{channelName}";

            _pendingJoins[sgHandle] = channelName;

            Log.Trace($"[Vivox] Joining positional channel: {channelUri}");
            VivoxNative.vx_req_sessiongroup_add_session_create(out IntPtr req);
            VivoxStructWriter.SetAddSessionFields(
                req,
                accountHandle:      _accountHandle,
                sessiongroupHandle: sgHandle,
                channelUri:         channelUri,
                accessToken:        joinToken,
                connectAudio:       true);

            int rc = VivoxNative.vx_issue_request(req);
            if (rc != 0) throw new VivoxException($"JoinChannel request failed: rc={rc}");
        }

        public void JoinGroupChannel(string channelName)
        {
            EnsureLoggedIn();

            string joinToken = VivoxToken.GenerateJoinToken(
                _config.Issuer, _config.SecretKey, _userId, channelName, _config.Domain);

            string channelUri = $"sip:confctl-g-{_config.Issuer}.{channelName}@{_config.Domain}";
            string sgHandle   = $"sg_{channelName}";

            _pendingJoins[sgHandle] = channelName;

            Log.Trace($"[Vivox] Joining group channel: {channelUri}");
            VivoxNative.vx_req_sessiongroup_add_session_create(out IntPtr req);
            VivoxStructWriter.SetAddSessionFields(
                req,
                accountHandle:      _accountHandle,
                sessiongroupHandle: sgHandle,
                channelUri:         channelUri,
                accessToken:        joinToken,
                connectAudio:       true);

            int rc = VivoxNative.vx_issue_request(req);
            if (rc != 0) throw new VivoxException($"JoinGroupChannel request failed: rc={rc}");
        }

        public void UpdatePosition(double tileX, double tileY)
        {
            if (_proximitySessionHandle == null) return;

            VivoxNative.vx_req_session_set_3d_position_create(out IntPtr req);
            VivoxStructWriter.SetPositionFields(req, _proximitySessionHandle, tileX, 0.0, tileY);

            VivoxNative.vx_issue_request(req);
        }

        // ── Per-Player Mute ──────────────────────────────────────────────────

        /// <summary>
        /// Mutes or unmutes a specific participant for the local user across all sessions.
        /// Uses vx_req_session_set_participant_mute_for_me.
        /// </summary>
        public void SetParticipantMuteForMe(string participantUri, bool mute)
        {
            foreach (var (_, sessionHandle) in _sessionHandles)
            {
                VivoxNative.vx_req_session_set_participant_mute_for_me_create(out IntPtr req);
                VivoxStructWriter.SetParticipantMuteFields(req, sessionHandle, participantUri, mute);

                int rc = VivoxNative.vx_issue_request(req);
                if (rc != 0)
                {
                    Log.Warn($"[Vivox] SetParticipantMuteForMe failed: rc={rc}");
                }
            }

            Log.Trace($"[Vivox] Participant {(mute ? "muted" : "unmuted")}: {participantUri}");
        }

        /// <summary>
        /// Gets the session handle for a specific channel, or null if not joined.
        /// </summary>
        public string GetSessionHandle(string channelName) =>
            _sessionHandles.TryGetValue(channelName, out var handle) ? handle : null;

        // ── Transmission Control ──────────────────────────────────────────────
        //
        // After joining a channel, the session is in "receive only" state until
        // it's explicitly designated as the active TX (transmit) session for the
        // session group. Without this call, your mic produces silence to the
        // channel and remote participants hear nothing.

        /// <summary>Set a single session as the transmit target for its session group.</summary>
        public void SetTransmitSession(string sessionGroupHandle, string sessionHandle)
        {
            if (string.IsNullOrEmpty(sessionGroupHandle) || string.IsNullOrEmpty(sessionHandle))
                return;

            VivoxNative.vx_req_sessiongroup_set_tx_session_create(out IntPtr req);
            if (req == IntPtr.Zero)
            {
                Log.Warn("[Vivox] vx_req_sessiongroup_set_tx_session_create returned null.");
                return;
            }

            VivoxStructWriter.SetTxSessionFields(req, sessionGroupHandle, sessionHandle);
            int rc = VivoxNative.vx_issue_request(req);
            if (rc != 0)
                Log.Warn($"[Vivox] set_tx_session request failed: rc={rc}");
            else
                Log.Trace($"[Vivox] TX session set: sg='{sessionGroupHandle}' session='{sessionHandle}'");
        }

        /// <summary>Transmit to all joined sessions in the group simultaneously.</summary>
        public void SetTransmitAllSessions(string sessionGroupHandle)
        {
            if (string.IsNullOrEmpty(sessionGroupHandle)) return;

            VivoxNative.vx_req_sessiongroup_set_tx_all_sessions_create(out IntPtr req);
            if (req == IntPtr.Zero) return;

            VivoxStructWriter.SetTxGroupHandleField(req, sessionGroupHandle);
            int rc = VivoxNative.vx_issue_request(req);
            if (rc != 0) Log.Warn($"[Vivox] set_tx_all_sessions failed: rc={rc}");
            else Log.Trace($"[Vivox] TX = all sessions in '{sessionGroupHandle}'");
        }

        /// <summary>Stop transmitting to any session in the group (mic-off).</summary>
        public void SetTransmitNoSession(string sessionGroupHandle)
        {
            if (string.IsNullOrEmpty(sessionGroupHandle)) return;

            VivoxNative.vx_req_sessiongroup_set_tx_no_session_create(out IntPtr req);
            if (req == IntPtr.Zero) return;

            VivoxStructWriter.SetTxGroupHandleField(req, sessionGroupHandle);
            int rc = VivoxNative.vx_issue_request(req);
            if (rc != 0) Log.Warn($"[Vivox] set_tx_no_session failed: rc={rc}");
            else Log.Trace($"[Vivox] TX = none in '{sessionGroupHandle}'");
        }

        /// <summary>
        /// Hard-mute the local capture device at the connector level. This stops
        /// the mic from recording entirely — unlike SetTransmitNoSession which
        /// only cuts the routing path. Use this as the real mute for PTT gating
        /// and F8, especially important when multiple clients run on one PC.
        /// </summary>
        public void SetLocalMicMuted(bool muted)
        {
            if (!_initialized) return;

            VivoxNative.vx_req_connector_mute_local_mic_create(out IntPtr req);
            if (req == IntPtr.Zero)
            {
                Log.Warn("[Vivox] vx_req_connector_mute_local_mic_create returned null.");
                return;
            }

            // One-shot probe: dump the request struct before and after the
            // writer the first N times we issue this request. This verifies
            // the SetMuteLocalMicFields offsets are correct against the
            // live SDK. Remove or gate this once confirmed.
            bool probe = _probeMuteRemaining > 0;
            if (probe)
            {
                VivoxStructWriter.DumpMuteRequestStruct(req, $"BEFORE muted={muted}");
            }

            VivoxStructWriter.SetMuteLocalMicFields(req, ConnectorHandle, muted);

            if (probe)
            {
                VivoxStructWriter.DumpMuteRequestStruct(req, $"AFTER  muted={muted}");
                _probeMuteRemaining--;
            }

            int rc = VivoxNative.vx_issue_request(req);
            if (rc != 0)
                Log.Warn($"[Vivox] connector_mute_local_mic submit failed: rc={rc}");
            else
                Log.Trace($"[Vivox] Capture device {(muted ? "MUTED" : "UNMUTED")} submitted (connector='{ConnectorHandle}', watch for subtype=61 response)");
        }

        // Number of remaining SetLocalMicMuted calls that should dump the
        // request struct for offset verification. Decremented each call.
        private int _probeMuteRemaining = 2;

        // ── Message Pump ──────────────────────────────────────────────────────

        private const int VX_GET_MESSAGE_AVAILABLE   =  0;
        private const int VX_GET_MESSAGE_FAILURE     =  1;
        private const int VX_GET_MESSAGE_NO_MESSAGE  = -1;

        private void MessagePump(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                int status;
                do
                {
                    status = VivoxNative.vx_get_message(out IntPtr msg);

                    if (status == VX_GET_MESSAGE_NO_MESSAGE || msg == IntPtr.Zero)
                        break;

                    if (status == VX_GET_MESSAGE_FAILURE)
                    {
                        Log.Warn("[Vivox] vx_get_message failure — SDK not initialized?");
                        break;
                    }

                    try
                    {
                        ProcessMessage(msg);
                    }
                    finally
                    {
                        VivoxNative.vx_destroy_message(msg);
                    }
                } while (status == VX_GET_MESSAGE_AVAILABLE);

                Thread.Sleep(10);
            }
        }

        private void ProcessMessage(IntPtr msg)
        {
            var msgType = VivoxStructReader.GetMessageType(msg);

            switch (msgType)
            {
                case VxMessageType.Response:
                    int subtype = VivoxStructReader.GetMessageSubtype(msg);
                    int rc      = VivoxStructReader.GetResponseReturnCode(msg);

                    if (subtype == (int)VxResponseType.ConnectorCreate)
                    {
                        if (rc == 0)
                        {
                            Log.Trace("[Vivox] Connector create: SUCCESS");
                            _connectorTcs?.TrySetResult(true);
                        }
                        else
                        {
                            int st  = VivoxStructReader.GetResponseStatusCode(msg);
                            string msg2 = VivoxStructReader.GetResponseStatusString(msg);
                            Log.Warn($"[Vivox] Connector create FAILED: rc={rc} status={st} detail='{msg2}'");
                            _connectorTcs?.TrySetResult(false);
                        }
                        break;
                    }

                    if (rc != 0)
                    {
                        int statusCode   = VivoxStructReader.GetResponseStatusCode(msg);
                        string statusStr = VivoxStructReader.GetResponseStatusString(msg);
                        Log.Warn($"[Vivox] Response error: subtype={subtype} rc={rc} status={statusCode} msg=\"{statusStr}\"");
                    }
                    else
                    {
                        // Call out the mute response specifically so we can
                        // correlate it with the probe dumps. subtype=61 =
                        // resp_connector_mute_local_mic (Vxc.h:564).
                        if (subtype == 61)
                            Log.Trace("[Vivox] Response OK: subtype=61 (connector_mute_local_mic applied)");
                        else
                            Log.Trace($"[Vivox] Response OK: subtype={subtype}");
                    }
                    break;

                case VxMessageType.Event:
                    int evtSubtype = VivoxStructReader.GetMessageSubtype(msg);
                    Log.Trace($"[Vivox] Event received (subtype={evtSubtype}).");

                    if (evtSubtype == (int)VxEventType.AccountLoginStateChange)
                    {
                        var state = VivoxStructReader.GetLoginState(msg);
                        Log.Trace($"[Vivox] Login state: {state}");

                        if (state == VxLoginState.LoggedIn)
                        {
                            string acctHandle = VivoxStructReader.GetEventAccountHandle(msg);
                            if (acctHandle != null) _accountHandle = acctHandle;
                        }

                        LoginStateChanged?.Invoke(state);
                    }

                    if (evtSubtype == (int)VxEventType.SessionAdded)
                    {
                        string handle = VivoxStructReader.GetSessionHandle(msg);
                        string sgHandle = VivoxStructReader.GetSessionGroupHandle(msg);
                        if (handle != null)
                        {
                            // Map channel name → session handle using pending joins
                            if (sgHandle != null && _pendingJoins.TryGetValue(sgHandle, out var channelName))
                            {
                                _sessionHandles[channelName] = handle;
                                _pendingJoins.Remove(sgHandle);

                                // First positional channel becomes the proximity handle
                                if (_proximitySessionHandle == null && sgHandle.Contains("proximity"))
                                {
                                    _proximitySessionHandle = handle;

                                    // Designate this session as the active TX target so the
                                    // mic actually transmits to the channel. Without this,
                                    // we receive audio but send silence.
                                    SetTransmitSession(sgHandle, handle);
                                }

                                Log.Trace($"[Vivox] Session added: channel='{channelName}' handle='{handle}'");
                            }
                            else
                            {
                                Log.Trace($"[Vivox] Session added (unmapped): handle='{handle}'");
                            }

                            VivoxStructWriter.CacheSessionHandle(handle);
                        }
                    }

                    if (evtSubtype == (int)VxEventType.ParticipantAdded)
                    {
                        string uri = VivoxStructReader.GetParticipantUri(msg);
                        string sessHandle = VivoxStructReader.GetParticipantSessionHandle(msg);
                        if (uri != null)
                        {
                            ParticipantJoined?.Invoke(uri, sessHandle);
                            Log.Trace($"[Vivox] Participant joined: {uri}");
                        }
                    }

                    if (evtSubtype == (int)VxEventType.ParticipantRemoved)
                    {
                        string uri = VivoxStructReader.GetParticipantUri(msg);
                        string sessHandle = VivoxStructReader.GetParticipantSessionHandle(msg);
                        if (uri != null)
                        {
                            ParticipantLeft?.Invoke(uri, sessHandle);
                            Log.Trace($"[Vivox] Participant left: {uri}");
                        }
                    }

                    if (evtSubtype == (int)VxEventType.ParticipantUpdated)
                    {
                        string uri = VivoxStructReader.GetParticipantUri(msg);
                        if (uri != null)
                        {
                            bool speaking = VivoxStructReader.GetParticipantSpeaking(msg);
                            double energy = VivoxStructReader.GetParticipantEnergy(msg);
                            SpeakingChanged?.Invoke(uri, speaking, energy);
                        }
                    }
                    break;

                default:
                    Log.Trace($"[Vivox] Unknown message type={msgType} (raw={(int)msgType})");
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void EnsureInitialized()
        {
            if (!_initialized) throw new VivoxException("SDK not initialized. Call Initialize() first.");
        }

        private void EnsureLoggedIn()
        {
            EnsureInitialized();
            if (_accountHandle == null) throw new VivoxException("Not logged in. Call Login() first.");
        }

        public void Dispose()
        {
            Logout();
            Shutdown();
        }
    }

    internal class VivoxException : Exception
    {
        public VivoxException(string message) : base(message) { }
    }
}
