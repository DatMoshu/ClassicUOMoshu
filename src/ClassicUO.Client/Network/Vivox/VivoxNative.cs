// VivoxNative.cs — P/Invoke bindings for Vivox Core SDK (vivoxsdk.dll)
//
// SDK download: https://developer.vivox.com (requires free developer account)
// Docs: https://docs.vivox.com/v5/general/core/5_21_0/en-us/
//
// CONFIRMED via export table (555 exports, x64):
//   vx_initialize3, vx_uninitialize, vx_get_default_config3
//   vx_issue_request, vx_get_message, vx_destroy_message
//   vx_req_*_create factory functions all present
//   vx_get_message_type present
//
// NOTE: No field setter functions (vx_req_*_set_*) exported.
// Fields are written via Marshal.WriteIntPtr/WriteInt32 at known struct offsets.
// Offsets are derived from vxc.h (included in the SDK download).
// Estimated offsets are marked VERIFY-FROM-VXC.H — confirm after SDK download.
//
// Memory model: after vx_issue_request(), the SDK owns the request struct.
// Do NOT free it. The SDK will free it after processing.

using System;
using System.Runtime.InteropServices;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Network.Vivox;

// ─── Enums ────────────────────────────────────────────────────────────────────

public enum VxMessageType : int
{
    // Values confirmed from vx_get_message_type() return — verify against vxc.h in SDK download.
    // Old Vxc.h (MultiversePlatform): msg_request=1, msg_response=2, msg_event=3.
    // If ProcessMessage never matches, these need adjusting.
    Request  = 1,
    Response = 2,
    Event    = 3,
}

public enum VxEventType : int
{
    AccountLoginStateChange = 2,
    SessiongroupAdded       = 22,
    SessionAdded            = 24,
    SessionRemoved          = 25,
    ParticipantAdded        = 26,
    ParticipantRemoved      = 27,
    ParticipantUpdated      = 28,
}

public enum VxLoginState : int
{
    LoggedOut   = 0,
    LoggedIn    = 1,
    LoggingIn   = 2,
    LoggingOut  = 3,
    Resetting   = 4,
}

// Response subtype enum — values from vx_response_type in Vxc.h (SDK 5.27.1).
// ConnectorCreate live-validated; others confirmed from header.
public enum VxResponseType : int
{
    ConnectorCreate             = 1,
    AccountLogout               = 4,
    SessiongroupAddSession      = 8,
    SessionSet3dPosition        = 28,
    AccountAnonymousLogin       = 131,
}

// ─── Core API ─────────────────────────────────────────────────────────────────

public static class VivoxNative
{
    private const string DLL = "vivoxsdk";

    // vx_sdk_config_t is large. We allocate 16KB and let vx_get_default_config3 fill it.
    // Actual struct size is ~8KB but 16KB gives us a safe margin.
    public const int SDK_CONFIG_SIZE = 16384;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    // Fill a buffer with safe SDK defaults. Call BEFORE vx_initialize3.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_get_default_config3(byte[] config, nuint configSize);

    // Initialize the SDK. Call once at startup with a config filled by vx_get_default_config3.
    // NOTE: function is "vx_initialize3", not "vx_init" — confirmed from export table.
    [DllImport(DLL, EntryPoint = "vx_initialize3", CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_initialize3(byte[] config, nuint configSize);

    // Shut down the SDK. Call at clean exit.
    // NOTE: function is "vx_uninitialize", not "vx_uninit" — confirmed from export table.
    [DllImport(DLL, EntryPoint = "vx_uninitialize", CallingConvention = CallingConvention.Cdecl)]
    public static extern void vx_uninitialize();

    // Check if SDK is already initialized (useful to avoid double-init).
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_is_initialized(); // 1 if initialized

    // ── Message Pump ──────────────────────────────────────────────────────────

    // Poll for a pending message.
    // Returns 0 and sets *message to the message pointer when a message is available.
    // Returns non-zero (or sets *message = IntPtr.Zero) when queue is empty.
    // Caller owns the returned pointer — must call vx_destroy_message when done.
    //
    // Actual C signature: int vx_get_message(vx_message_base_t** message)
    // WRONG (old):  public static extern IntPtr vx_get_message();
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_get_message(out IntPtr message);

    // Free a message returned by vx_get_message.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void vx_destroy_message(IntPtr msg);

    // Get the VxMessageType of any message (request/response/event).
    // First field of every message struct — safe to read at offset 0.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_get_message_type(IntPtr msg); // returns VxMessageType

    // Issue a request. After this call the SDK owns the request struct — do NOT free it.
    // vx_issue_request and vx_issue_request2 are identical (same address in dumpbin).
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_issue_request(IntPtr req);

    // vx_issue_request3: newer variant confirmed from dumpbin (different address).
    // Takes base ptr + out requestCount. Docs examples use this for all connector/login calls.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_issue_request3(IntPtr req, out int requestCount);

    // ── Request Factory Functions ─────────────────────────────────────────────
    // Allocate and zero a request struct. Returns 0 on success, sets *req to pointer.
    // Set fields using Marshal helpers (see VivoxStructWriter), then call vx_issue_request.

    // CONFIRMED from dumpbin: exported as vx_req_connector_create_create (double _create)
    // This is step 1 after vx_initialize3 — must complete before login.
    [DllImport(DLL, EntryPoint = "vx_req_connector_create_create", CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_connector_create_create(out IntPtr req);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_account_anonymous_login_create(out IntPtr req);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_account_logout_create(out IntPtr req);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_sessiongroup_add_session_create(out IntPtr req);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_session_set_3d_position_create(out IntPtr req);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_session_set_participant_mute_for_me_create(out IntPtr req);

    // Set which session within a session group is the active TX (transmit) session.
    // Required after joining a channel — without this the mic stays disconnected
    // from the channel and remote participants hear silence.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_sessiongroup_set_tx_session_create(out IntPtr req);

    // Convenience: transmit to ALL joined sessions in a group simultaneously.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_sessiongroup_set_tx_all_sessions_create(out IntPtr req);

    // Convenience: stop transmitting to all sessions in a group (mic-off equivalent).
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern int vx_req_sessiongroup_set_tx_no_session_create(out IntPtr req);

    // ── Response/Event Field Readers ──────────────────────────────────────────
    // vx_get_message_type (above) works for all messages.
    // For response return codes and event fields: read directly from the struct
    // at known offsets using VivoxStructReader (see below).

    // Convenience: get error string for a return code.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr vx_get_error_string(int code); // returns const char*

    // Allocate a copy of a string using the Vivox SDK's own heap allocator.
    // MUST be used instead of Marshal.StringToHGlobalAnsi for strings written into
    // request structs. The SDK calls its own free() when destroying responses (which
    // embed the request pointer). Mixing allocators crashes vx_destroy_message.
    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr vx_strdup([MarshalAs(UnmanagedType.LPStr)] string s);
}

// ─── Struct Field Access ──────────────────────────────────────────────────────
//
// Vivox request structs are C structs allocated by _create functions.
// We write fields by computing byte offsets from vxc.h.
//
// IMPORTANT: ALL OFFSETS BELOW ARE ESTIMATED FOR x64 FROM vxc.h FIELD ORDER.
// Verify against your actual vxc.h after SDK download:
//   - Run a C++ program that prints offsetof(struct, field) for each
//   - Or read the header and trace layout manually
//
// x64 ABI rules used: sizeof(int)=4, sizeof(pointer)=8, natural alignment.
//
// vx_req_base_t layout (16 bytes):
//   int type         @ 0
//   int cookie_id    @ 4
//   char* vcookie    @ 8   (ptr, 8 bytes, aligned to 8)
//   TOTAL: 16 bytes

public static class VivoxStructWriter
{
    // Allocate a string in the Vivox SDK heap so vx_destroy_message can free it safely.
    // Do NOT use Marshal.StringToHGlobalAnsi — the SDK's free() will crash on those.
    private static IntPtr S(string s) => VivoxNative.vx_strdup(s);

    // ── vx_req_base_t layout (x64, confirmed from hex dump of response) ─────────
    //
    // vx_req_base_t contains a full vx_message_base_t (24 bytes), NOT just an int:
    //   vx_message_base_t message   @ 0   (24 bytes: int + 4pad + uint64 + uint64)
    //   vx_request_type type        @ 24  (int, 4 bytes)
    //   VX_COOKIE cookie            @ 28  (int, 4 bytes)
    //   void* vcookie               @ 32  (ptr, 8 bytes)
    //   TOTAL: 40 bytes
    //
    // Request-specific fields start at offset 40.

    // ── Connector Create Request ──────────────────────────────────────────────
    //
    // struct vx_req_connector_create (x64):
    //   vx_req_base_t base           @ 0   (40 bytes)
    //   char* connector_handle       @ 40  (your chosen handle name, e.g. "c1")
    //   char* acct_mgmt_server       @ 48  (Vivox server URL from dashboard)
    //   int    mode                  @ 56  (0 = normal, leave default)
    //   — more fields may follow; we only set what we need —
    //
    // VERIFY-FROM-VXC.H: check offsetof(vx_req_connector_create, acct_mgmt_server)

    public static void SetConnectorCreateFields(IntPtr req, string connectorHandle, string serverUrl)
    {
        Marshal.WriteIntPtr(req, 56, S(serverUrl));
        Marshal.WriteInt32(req, 64, 0); // ephemeral minimum_port
        Marshal.WriteInt32(req, 68, 0); // ephemeral maximum_port
        Marshal.WriteIntPtr(req, 168, S(connectorHandle));
    }

    // ── Anonymous Login Request ───────────────────────────────────────────────
    //
    // struct vx_req_account_anonymous_login (x64):
    //   vx_req_base_t base       @ 0    (40 bytes)
    //   char* connector_handle   @ 40
    //   char* displayname        @ 48
    //   char* account_handle     @ 112
    //   char* account_name       @ 120
    //   char* access_token       @ 128
    //
    // VERIFY-FROM-VXC.H: check offsetof(vx_req_account_anonymous_login, account_name)

    public static void SetLoginFields(
        IntPtr req,
        string connectorHandle,
        string acctName,
        string displayName,
        string token)
    {
        Marshal.WriteIntPtr(req, 48, S(connectorHandle)); // connector_handle
        Marshal.WriteIntPtr(req, 56, S(displayName));     // displayname
        // account_handle @ 120 (leave null)
        Marshal.WriteIntPtr(req, 128, S(acctName));       // acct_name
        Marshal.WriteIntPtr(req, 136, S(token));          // access_token
    }

    // ── Logout Request ────────────────────────────────────────────────────────
    //
    // struct vx_req_account_logout (x64, from VxcRequests.h):
    //   vx_req_base_t base       @ 0   (48 bytes)
    //   VX_HANDLE account_handle @ 48

    public static void SetLogoutFields(IntPtr req, string accountHandle)
    {
        Marshal.WriteIntPtr(req, 48, S(accountHandle));
    }

    // ── Add Session (Channel Join) Request ────────────────────────────────────
    //
    // struct vx_req_sessiongroup_add_session (x64):
    //   vx_req_base_t base              @ 0   (40 bytes)
    //   char* account_handle            @ 40
    //   char* sessiongroup_handle       @ 48
    //   char* uri                       @ 56
    //   char* name                      @ 64
    //   char* password                  @ 72
    //   int connect_audio               @ 80
    //   int connect_text                @ 84
    //   char* access_token              @ 88

    public static void SetAddSessionFields(
        IntPtr req,
        string accountHandle,
        string sessiongroupHandle,
        string channelUri,
        string accessToken,
        bool connectAudio = true)
    {
        Marshal.WriteIntPtr(req,  48, S(sessiongroupHandle));
        Marshal.WriteIntPtr(req,  56, S(channelUri));
        Marshal.WriteIntPtr(req,  64, IntPtr.Zero);    // name (optional, leave null)
        Marshal.WriteIntPtr(req,  72, IntPtr.Zero);    // password (empty channel)
        Marshal.WriteInt32 (req,  80, connectAudio ? 1 : 0);
        Marshal.WriteIntPtr(req, 112, S(accessToken));
        Marshal.WriteIntPtr(req, 120, S(accountHandle));
    }

    // ── Participant Mute For Me Request ─────────────────────────────────────
    //
    // struct vx_req_session_set_participant_mute_for_me (x64):
    //   vx_req_base_t base           @ 0   (40 bytes)
    //   char* session_handle         @ 40
    //   char* participant_uri        @ 48
    //   int mute                     @ 56  (1=mute, 0=unmute)
    //   int scope                    @ 60  (0=mute_scope_all)

    public static void SetParticipantMuteFields(IntPtr req, string sessionHandle, string participantUri, bool mute)
    {
        Marshal.WriteIntPtr(req, 40, S(sessionHandle));
        Marshal.WriteIntPtr(req, 48, S(participantUri));
        Marshal.WriteInt32(req, 56, mute ? 1 : 0);
        Marshal.WriteInt32(req, 60, 0); // mute_scope_all
    }

    // ── Set TX Session Request ──────────────────────────────────────────────
    //
    // struct vx_req_sessiongroup_set_tx_session (x64):
    //   vx_req_base_t base              @ 0   (40 bytes)
    //   char* sessiongroup_handle       @ 40
    //   char* session_handle            @ 48

    public static void SetTxSessionFields(IntPtr req, string sessionGroupHandle, string sessionHandle)
    {
        Marshal.WriteIntPtr(req, 40, S(sessionGroupHandle));
        Marshal.WriteIntPtr(req, 48, S(sessionHandle));
    }

    // ── Set TX All Sessions / Set TX No Session Request ─────────────────────
    //
    // Both share the same layout:
    //   vx_req_base_t base              @ 0   (40 bytes)
    //   char* sessiongroup_handle       @ 40

    public static void SetTxGroupHandleField(IntPtr req, string sessionGroupHandle)
    {
        Marshal.WriteIntPtr(req, 40, S(sessionGroupHandle));
    }

    // ── 3D Position Request ───────────────────────────────────────────────────
    //
    // struct vx_req_session_set_3d_position (x64, from VxcRequests.h):
    //   vx_req_base_t base               @ 0    (48 bytes)
    //   VX_HANDLE session_handle          @ 48
    //   double speaker_position[3]        @ 56   (3 × 8 = 24 bytes)
    //   double speaker_velocity[3]        @ 80
    //   double speaker_at_orientation[3]  @ 104
    //   double speaker_up_orientation[3]  @ 128
    //   double speaker_left_orientation[3]@ 152
    //   double listener_position[3]       @ 176
    //   double listener_velocity[3]       @ 200
    //   double listener_at_orientation[3] @ 224
    //   double listener_up_orientation[3] @ 248
    //   double listener_left_orientation[3]@ 272
    //   orientation_type type             @ 296
    //   req_disposition_type_t            @ 300

    // Cache the session handle string to avoid managed-side allocations,
    // but always vx_strdup a fresh native copy per request because the SDK
    // takes ownership of all strings inside request structs after vx_issue_request.
    private static string _cachedSessionHandleStr;

    public static void CacheSessionHandle(string sessionHandle)
    {
        _cachedSessionHandleStr = sessionHandle;
    }

    public static void SetPositionFields(IntPtr req, string sessionHandle, double x, double y, double z)
    {
        // Must allocate a fresh vx_strdup copy every call — the SDK frees
        // the previous request's strings when it processes the request.
        Marshal.WriteIntPtr(req, 48, S(sessionHandle));

        // Speaker position (x, y, z) — UO tile: x→X, y→Z, height=0
        WriteDouble(req,  56, x);   // speaker_position[0] = X
        WriteDouble(req,  64, y);   // speaker_position[1] = Y (height, 0 for flat world)
        WriteDouble(req,  72, z);   // speaker_position[2] = Z

        // Speaker orientation: forward (0,0,-1), up (0,1,0)
        WriteDouble(req, 104, 0.0); WriteDouble(req, 112, 0.0); WriteDouble(req, 120, -1.0);
        WriteDouble(req, 128, 0.0); WriteDouble(req, 136, 1.0); WriteDouble(req, 144, 0.0);

        // Listener = same position as speaker (self-hearing from own position)
        WriteDouble(req, 176, x);
        WriteDouble(req, 184, y);
        WriteDouble(req, 192, z);
        WriteDouble(req, 224, 0.0); WriteDouble(req, 232, 0.0); WriteDouble(req, 240, -1.0);
        WriteDouble(req, 248, 0.0); WriteDouble(req, 256, 1.0); WriteDouble(req, 264, 0.0);

        // No response needed for per-tick position updates
        Marshal.WriteInt32(req, 300, 1); // req_disposition_no_reply_required
    }

    private static void WriteDouble(IntPtr ptr, int offset, double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);
        Marshal.WriteInt64(ptr, offset, bits);
    }
}

// ─── Response/Event Readers ───────────────────────────────────────────────────
//
// vx_message_base_t layout (x64, from vxc.h):
//   int type                   @ 0    (VxMessageType — request/response/event)
//   [4 bytes padding]          @ 4
//   uint64 create_time_ms      @ 8
//   uint64 last_step_ms        @ 16
//   TOTAL: 24 bytes
//
// vx_resp_base_t layout (response messages):
//   vx_message_base_t base     @ 0    (24 bytes)
//   int subtype (resp type)    @ 24
//   int return_code            @ 28   (0 = success)
//   int status_code            @ 32
//   [4 bytes padding]          @ 36
//   char* status_string        @ 40
//   vx_req_base_t* request     @ 48
//
// vx_evt_base_t layout (event messages):
//   vx_message_base_t base     @ 0    (24 bytes)
//   int subtype (event type)   @ 24
//   TOTAL: 28 bytes
//
// vx_evt_account_login_state_change_t (extends evt_base, 28 bytes):
//   char* account_handle       @ 28   VERIFY-FROM-VXC.H
//   VxLoginState state         @ 36   VERIFY-FROM-VXC.H (after pointer at 28)
//
// vx_evt_session_added_t (extends evt_base, 28 bytes):
//   char* sessiongroup_handle  @ 28   VERIFY-FROM-VXC.H
//   char* session_handle       @ 36   VERIFY-FROM-VXC.H

public static class VivoxStructReader
{
    public static VxMessageType GetMessageType(IntPtr msg) =>
        (VxMessageType)Marshal.ReadInt32(msg, 0);

    // Subtype is at offset 24 (after 24-byte vx_message_base_t)
    public static int GetMessageSubtype(IntPtr msg) =>
        Marshal.ReadInt32(msg, 24);

    // For response messages (type == Response)
    public static int GetResponseReturnCode(IntPtr msg) =>
        Marshal.ReadInt32(msg, 28);

    public static int GetResponseStatusCode(IntPtr msg) =>
        Marshal.ReadInt32(msg, 32);

    public static string GetResponseStatusString(IntPtr msg)
    {
        IntPtr ptr = Marshal.ReadIntPtr(msg, 40);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
    }

    // For evt_account_login_state_change (subtype == AccountLoginStateChange)
    // vx_evt_account_login_state_change_t:
    // state          @ 40   (4 bytes)
    // account_handle @ 48   (8 bytes)
    public static VxLoginState GetLoginState(IntPtr msg) =>
        (VxLoginState)Marshal.ReadInt32(msg, 40);

    public static string GetEventAccountHandle(IntPtr msg)
    {
        IntPtr ptr = Marshal.ReadIntPtr(msg, 48);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
    }

    // For evt_session_added (subtype == SessionAdded)
    // vx_evt_session_added_t:
    //   vx_evt_base_t base   @ 0   (40 bytes total — matches login_state base,
    //                               which works at state@40 / account_handle@48)
    //   sessiongroup_handle  @ 40
    //   session_handle       @ 48
    //   uri                  @ 56
    // NOTE: prior values (32/40/48) were off by -8 and caused both session
    // handle mapping AND 3D position transmit to silently fail. Verified
    // against VivoxSpike/Core/VivoxNative.cs which uses (40/48/56) and works.
    public static string GetSessionGroupHandle(IntPtr msg)
    {
        IntPtr ptr = Marshal.ReadIntPtr(msg, 40);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
    }

    public static string GetSessionHandle(IntPtr msg)
    {
        IntPtr ptr = Marshal.ReadIntPtr(msg, 48);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
    }

    // For evt_participant_added / evt_participant_removed / evt_participant_updated
    // vx_evt_participant_updated_t (shifted +8 to match the corrected event base):
    //   vx_evt_base_t base           @ 0   (40 bytes)
    //   sessiongroup_handle          @ 40  (ptr)
    //   session_handle               @ 48  (ptr)
    //   participant_uri              @ 56  (ptr)
    //   is_moderator_muted           @ 64  (int)
    //   is_speaking                  @ 68  (int)
    //   volume                       @ 72  (int)
    //   [4 bytes padding]            @ 76
    //   energy                       @ 80  (double)

    public static string GetParticipantUri(IntPtr msg)
    {
        IntPtr ptr = Marshal.ReadIntPtr(msg, 56);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
    }

    public static string GetParticipantSessionHandle(IntPtr msg)
    {
        IntPtr ptr = Marshal.ReadIntPtr(msg, 48);
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
    }

    public static bool GetParticipantSpeaking(IntPtr msg) =>
        Marshal.ReadInt32(msg, 68) != 0;

    public static double GetParticipantEnergy(IntPtr msg)
    {
        long bits = Marshal.ReadInt64(msg, 80);
        return BitConverter.Int64BitsToDouble(bits);
    }

    // ── Diagnostic Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Dumps subtype + return/status codes for any response message.
    /// Use this to identify unknown subtypes when matching VxResponseType enum values.
    /// Expected output format: [Vivox] RESP subtype=5 rc=0 status=200 msg="OK"
    /// Cross-reference the subtype value with vx_resp_type enum in vxc.h.
    /// </summary>
    public static void DumpResponseSubtype(IntPtr msg)
    {
        int subtype    = GetMessageSubtype(msg);
        int rc         = GetResponseReturnCode(msg);
        int status     = GetResponseStatusCode(msg);
        string detail = GetResponseStatusString(msg);
        Log.Trace($"[Vivox][DIAG] RESP subtype={subtype} rc={rc} status={status} msg=\"{detail ?? "null"}\"");
    }

    /// <summary>
    /// Dumps a raw hex view of the first <paramref name="byteCount"/> bytes of a message.
    /// Useful for verifying struct offsets against vxc.h.
    /// </summary>
    public static void DumpRawBytes(IntPtr msg, int byteCount = 96)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[Vivox][DIAG] RAW @ 0x{msg:X} ({byteCount}b): ");
        for (int i = 0; i < byteCount; i += 4)
        {
            int v = Marshal.ReadInt32(msg, i);
            sb.Append($"+{i:D3}={v:X8} ");
        }
        Log.Trace(sb.ToString());
    }
}
