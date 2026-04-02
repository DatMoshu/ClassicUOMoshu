// VivoxClient.cs — Minimal Vivox Core SDK wrapper
// Ported from prototypes/VivoxSpike into ClassicUO.

using System;
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
        private string _sessionHandle;

        private CancellationTokenSource _pumpCts;

        public event Action<VxLoginState> LoginStateChanged;
        public event Action<string> ParticipantJoined;
        public event Action<string> ParticipantLeft;
        public event Action<string, bool, double> SpeakingChanged;

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
            _sessionHandle = null;
            Log.Trace("[Vivox] Logged out.");
        }

        // ── Channel ───────────────────────────────────────────────────────────

        public void JoinPositionalChannel(string channelName)
        {
            EnsureLoggedIn();

            string joinToken = VivoxToken.GeneratePositionalJoinToken(
                _config.Issuer, _config.SecretKey, _userId, channelName, _config.Domain);

            string channelUri = $"sip:confctl-d-{_config.Issuer}.{channelName}@{_config.Domain}";
            string sgHandle   = $"sg_{channelName}";

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
            if (_sessionHandle == null) return;

            VivoxNative.vx_req_session_set_3d_position_create(out IntPtr req);
            VivoxStructWriter.SetPositionFields(req, _sessionHandle, tileX, 0.0, tileY);

            VivoxNative.vx_issue_request(req);
        }

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
                        if (handle != null)
                        {
                            _sessionHandle = handle;
                            VivoxStructWriter.CacheSessionHandle(handle);
                            Log.Trace($"[Vivox] Session added: handle='{handle}'");
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
