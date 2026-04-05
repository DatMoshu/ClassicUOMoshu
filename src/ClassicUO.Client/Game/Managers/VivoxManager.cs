// VivoxManager.cs — Singleton Vivox lifecycle manager for ClassicUO
// Manages SDK init, login, channel join, positional updates, PTT,
// per-player mute, speaking state tracking, and server config reception.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ClassicUO.Network.Vivox;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    [JsonSerializable(typeof(List<string>))]
    internal sealed partial class VivoxJsonContext : JsonSerializerContext { }

    /// <summary>
    /// Voice channel the player is currently transmitting on.
    /// </summary>
    public enum VoiceChannel
    {
        None,
        Proximity,
        Faction,
        Guild
    }

    internal sealed class VivoxManager
    {
        // ── Vivox Credentials (from developer dashboard) ──────────────────────
        // TODO: Move these to server-side token generation in ModernUO
        private const string ISSUER = "23381-uo_vi-15950";
        private const string SECRET = "wbBixwlgcDErP5F4M6oMwiRxAQ1L86Hd";
        private const string DOMAIN = "mtu1xp.vivox.com";
        private const string SERVER = "https://unity.vivox.com/appconfig/23381-uo_vi-15950";

        // The single proximity channel all players share
        private const string PROXIMITY_CHANNEL = "uoww-proximity";

        private VivoxClient _client;
        private bool _initFailed;
        private bool _isLoggingIn;
        private ushort _lastX;
        private ushort _lastY;
        private bool _firstPositionSent;
        private uint _positionLogCounter;

        // Throttle position updates
        private uint _lastPositionUpdateTick;
        private uint _positionUpdateIntervalMs = 100; // Server-configurable

        public bool IsInitialized => _client != null && !_initFailed;

        // ── Multi-Channel State ──────────────────────────────────────────────

        private string _factionChannelName;
        private string _guildChannelName;

        // ── PTT State ────────────────────────────────────────────────────────

        /// <summary>Current transmit channel (None = not transmitting).</summary>
        public VoiceChannel ActiveChannel { get; private set; } = VoiceChannel.None;

        /// <summary>Whether the mic is globally muted via M key toggle.</summary>
        public bool IsMicMuted { get; private set; }

        /// <summary>Fires when ActiveChannel or IsMicMuted changes.</summary>
        public event Action VoiceStateChanged;

        // ── Mute List ────────────────────────────────────────────────────────

        private readonly HashSet<string> _mutedParticipants = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, uint> _participantToSerial = new();
        private readonly Dictionary<uint, string> _serialToParticipant = new();

        // ── Speaking State ───────────────────────────────────────────────────

        private readonly Dictionary<string, bool> _speakingStates = new();
        private readonly Dictionary<string, double> _speakingEnergy = new();

        /// <summary>Fires when any participant's speaking state changes (serial, isSpeaking).</summary>
        public event Action<uint, bool> SpeakingStateChanged;

        // ── Server Config ────────────────────────────────────────────────────

        public int ProximityFullRange { get; private set; } = 15;
        public int ProximityMaxRange { get; private set; } = 25;
        public float DeadWhisperVolume { get; private set; } = 0.15f;
        public float TileToVivoxScale { get; private set; } = 1.0f;
        public bool ProximityOnTrammel { get; private set; }

        // ── Init / Shutdown ──────────────────────────────────────────────────

        public async void Initialize()
        {
            if (_client != null) return;

            try
            {
                var config = new VivoxClient.Config(ISSUER, SECRET, DOMAIN, SERVER);
                _client = new VivoxClient(config);

                _client.LoginStateChanged += OnLoginStateChanged;
                _client.ParticipantJoined += OnParticipantJoined;
                _client.ParticipantLeft += OnParticipantLeft;
                _client.SpeakingChanged += OnSpeakingChanged;

                await _client.InitializeAsync();
                Log.Trace("[VivoxManager] Vivox SDK ready.");

                LoadMuteList();
            }
            catch (DllNotFoundException)
            {
                Log.Warn("[VivoxManager] vivoxsdk.dll not found. Voice chat disabled.");
                _initFailed = true;
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Init failed: {ex.Message}");
                _initFailed = true;
            }
        }

        public void Shutdown()
        {
            if (_client != null)
            {
                SaveMuteList();
                _client.Dispose();
                _client = null;
                Log.Trace("[VivoxManager] Shutdown complete.");
            }
        }

        // ── Login / Logout ───────────────────────────────────────────────────

        public void OnPlayerEnterWorld(string characterName)
        {
            if (_client == null || _initFailed || _client.IsLoggedIn) return;
            if (_isLoggingIn) return;

            try
            {
                _isLoggingIn = true;
                string userId = SanitizeUserId(characterName);
                _client.Login(userId, characterName);
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Login failed: {ex.Message}");
                _isLoggingIn = false;
            }
        }

        public void OnPlayerLeaveWorld()
        {
            if (_client == null) return;

            try
            {
                SaveMuteList();
                _client.Logout();
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Logout error: {ex.Message}");
            }

            _isLoggingIn = false;
            _lastX = 0;
            _lastY = 0;
            ActiveChannel = VoiceChannel.None;
            _speakingStates.Clear();
            _participantToSerial.Clear();
            _serialToParticipant.Clear();
        }

        // ── Position Update ──────────────────────────────────────────────────

        public void UpdatePosition(ushort tileX, ushort tileY, uint currentTick)
        {
            if (_client == null || !_client.IsLoggedIn) return;

            if (currentTick - _lastPositionUpdateTick < _positionUpdateIntervalMs)
                return;

            if (tileX == _lastX && tileY == _lastY)
                return;

            _lastX = tileX;
            _lastY = tileY;
            _lastPositionUpdateTick = currentTick;

            try
            {
                _client.UpdatePosition(tileX, tileY);

                _positionLogCounter++;
                if (!_firstPositionSent || _positionLogCounter % 20 == 0)
                {
                    _firstPositionSent = true;
                    Log.Trace($"[VivoxManager] 3D pos: ({tileX}, {tileY})");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Position update failed: {ex.Message}");
            }
        }

        // ── PTT (Push-to-Talk) ───────────────────────────────────────────────

        /// <summary>Begin transmitting on the specified channel (called on key down).</summary>
        public void BeginTransmit(VoiceChannel channel)
        {
            if (_client == null || !_client.IsLoggedIn || IsMicMuted) return;

            ActiveChannel = channel;
            VoiceStateChanged?.Invoke();
            Log.Trace($"[VivoxManager] PTT begin: {channel}");
        }

        /// <summary>Stop transmitting (called on key up).</summary>
        public void EndTransmit()
        {
            if (ActiveChannel == VoiceChannel.None) return;

            ActiveChannel = VoiceChannel.None;
            VoiceStateChanged?.Invoke();
            Log.Trace("[VivoxManager] PTT end.");
        }

        /// <summary>Toggle global mic mute (M key).</summary>
        public void ToggleMicMute()
        {
            IsMicMuted = !IsMicMuted;

            if (IsMicMuted)
            {
                ActiveChannel = VoiceChannel.None;
            }

            VoiceStateChanged?.Invoke();
            Log.Trace($"[VivoxManager] Mic muted: {IsMicMuted}");
        }

        // ── Per-Player Mute ──────────────────────────────────────────────────

        /// <summary>Toggle mute for a player by their character name.</summary>
        public void ToggleMute(string characterName)
        {
            if (string.IsNullOrEmpty(characterName)) return;

            var sanitized = SanitizeUserId(characterName);

            if (_mutedParticipants.Contains(sanitized))
            {
                _mutedParticipants.Remove(sanitized);
                // Find their URI and unmute in Vivox
                if (_serialToParticipant.Count > 0)
                {
                    foreach (var (uri, _) in _speakingStates)
                    {
                        if (ExtractUserIdFromUri(uri) == sanitized)
                        {
                            _client?.SetParticipantMuteForMe(uri, false);
                            break;
                        }
                    }
                }

                Log.Trace($"[VivoxManager] Unmuted: {characterName}");
            }
            else
            {
                _mutedParticipants.Add(sanitized);
                // Find their URI and mute in Vivox
                foreach (var (uri, _) in _speakingStates)
                {
                    if (ExtractUserIdFromUri(uri) == sanitized)
                    {
                        _client?.SetParticipantMuteForMe(uri, true);
                        break;
                    }
                }

                Log.Trace($"[VivoxManager] Muted: {characterName}");
            }

            SaveMuteList();
        }

        /// <summary>Check if a player is muted by character name.</summary>
        public bool IsMuted(string characterName) =>
            !string.IsNullOrEmpty(characterName) && _mutedParticipants.Contains(SanitizeUserId(characterName));

        /// <summary>Check if a mobile serial is currently muted.</summary>
        public bool IsSerialMuted(uint serial)
        {
            if (_serialToParticipant.TryGetValue(serial, out var uri))
            {
                var userId = ExtractUserIdFromUri(uri);
                return _mutedParticipants.Contains(userId);
            }
            return false;
        }

        // ── Speaking State Queries ───────────────────────────────────────────

        /// <summary>Check if a mobile serial is currently speaking.</summary>
        public bool IsSerialSpeaking(uint serial)
        {
            if (_serialToParticipant.TryGetValue(serial, out var uri))
            {
                return _speakingStates.TryGetValue(uri, out var speaking) && speaking;
            }
            return false;
        }

        /// <summary>Get audio energy level for a speaking serial (0.0–1.0).</summary>
        public double GetSerialEnergy(uint serial)
        {
            if (_serialToParticipant.TryGetValue(serial, out var uri))
            {
                return _speakingEnergy.TryGetValue(uri, out var energy) ? energy : 0.0;
            }
            return 0.0;
        }

        // ── Server Config Reception ──────────────────────────────────────────

        /// <summary>
        /// Called by the client packet handler when receiving 0xBF sub-command 0x0100.
        /// </summary>
        public void ApplyServerConfig(int fullRange, int maxRange, int updateMs,
            float whisperVol, float scale, bool proxOnTrammel)
        {
            ProximityFullRange = fullRange;
            ProximityMaxRange = maxRange;
            _positionUpdateIntervalMs = (uint)updateMs;
            DeadWhisperVolume = whisperVol;
            TileToVivoxScale = scale;
            ProximityOnTrammel = proxOnTrammel;

            Log.Trace($"[VivoxManager] Server config: Full={fullRange} Max={maxRange} " +
                       $"Update={updateMs}ms Whisper={whisperVol:F2} Scale={scale:F1} Tram={proxOnTrammel}");
        }

        /// <summary>
        /// Called by the client packet handler when receiving 0xBF sub-command 0x0101.
        /// </summary>
        public void SetChannelInfo(string factionChannel, string guildChannel)
        {
            _factionChannelName = factionChannel;
            _guildChannelName = guildChannel;
            Log.Trace($"[VivoxManager] Channel info: faction='{factionChannel}' guild='{guildChannel}'");

            JoinConfiguredChannels();
        }

        private void JoinConfiguredChannels()
        {
            if (_client == null || !_client.IsLoggedIn) return;

            try
            {
                if (!string.IsNullOrEmpty(_factionChannelName))
                {
                    _client.JoinGroupChannel(_factionChannelName);
                    Log.Trace($"[VivoxManager] Joining faction channel: {_factionChannelName}");
                }

                if (!string.IsNullOrEmpty(_guildChannelName))
                {
                    _client.JoinGroupChannel(_guildChannelName);
                    Log.Trace($"[VivoxManager] Joining guild channel: {_guildChannelName}");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Failed to join channels: {ex.Message}");
            }
        }

        // ── Event Handlers ───────────────────────────────────────────────────

        private void OnLoginStateChanged(VxLoginState state)
        {
            Log.Trace($"[VivoxManager] Login state → {state}");

            if (state == VxLoginState.LoggedIn)
            {
                _isLoggingIn = false;
                try
                {
                    _client.JoinPositionalChannel(PROXIMITY_CHANNEL);
                    Log.Trace("[VivoxManager] Joined proximity channel.");
                }
                catch (Exception ex)
                {
                    Log.Warn($"[VivoxManager] Failed to join proximity channel: {ex.Message}");
                }
            }
            else if (state == VxLoginState.LoggedOut)
            {
                _isLoggingIn = false;
            }
        }

        private void OnParticipantJoined(string participantUri, string sessionHandle)
        {
            var userId = ExtractUserIdFromUri(participantUri);
            if (userId == null) return;

            // Try to map userId to a mobile serial by scanning nearby mobiles
            MapParticipantToSerial(userId, participantUri);

            // Apply mute if this player is on our mute list
            if (_mutedParticipants.Contains(userId))
            {
                _client?.SetParticipantMuteForMe(participantUri, true);
                Log.Trace($"[VivoxManager] Auto-muting participant: {userId}");
            }
        }

        private void OnParticipantLeft(string participantUri, string sessionHandle)
        {
            _speakingStates.Remove(participantUri);
            _speakingEnergy.Remove(participantUri);

            if (_participantToSerial.TryGetValue(participantUri, out var serial))
            {
                _participantToSerial.Remove(participantUri);
                _serialToParticipant.Remove(serial);
                SpeakingStateChanged?.Invoke(serial, false);
            }
        }

        private void OnSpeakingChanged(string participantUri, bool isSpeaking, double energy)
        {
            _speakingStates[participantUri] = isSpeaking;
            _speakingEnergy[participantUri] = energy;

            if (_participantToSerial.TryGetValue(participantUri, out var serial))
            {
                SpeakingStateChanged?.Invoke(serial, isSpeaking);
            }
        }

        // ── Participant → Serial Mapping ─────────────────────────────────────

        /// <summary>
        /// Register a mobile serial for a participant. Called from game code
        /// when a mobile enters range and we know their character name.
        /// </summary>
        public void RegisterMobileSerial(uint serial, string characterName)
        {
            var sanitized = SanitizeUserId(characterName);

            // Search existing participants for a match
            foreach (var (uri, _) in _speakingStates)
            {
                if (ExtractUserIdFromUri(uri) == sanitized)
                {
                    _participantToSerial[uri] = serial;
                    _serialToParticipant[serial] = uri;
                    return;
                }
            }
        }

        private void MapParticipantToSerial(string userId, string participantUri)
        {
            // This will be matched lazily when the game world updates
            // For now, store the speaking state so it's available when mapped
            if (!_speakingStates.ContainsKey(participantUri))
            {
                _speakingStates[participantUri] = false;
            }
        }

        // ── Mute List Persistence ────────────────────────────────────────────

        private static string MuteListPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClassicUO", "vivox_mutes.json");

        private void LoadMuteList()
        {
            try
            {
                var path = MuteListPath;
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize(json, VivoxJsonContext.Default.ListString);
                if (list != null)
                {
                    foreach (var name in list)
                    {
                        _mutedParticipants.Add(name);
                    }

                    Log.Trace($"[VivoxManager] Loaded {_mutedParticipants.Count} muted players.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Failed to load mute list: {ex.Message}");
            }
        }

        private void SaveMuteList()
        {
            try
            {
                var dir = Path.GetDirectoryName(MuteListPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(new List<string>(_mutedParticipants), VivoxJsonContext.Default.ListString);
                File.WriteAllText(MuteListPath, json);
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Failed to save mute list: {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string SanitizeUserId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "unknown";

            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
                else if (c == ' ' || c == '-')
                    sb.Append('_');
            }
            return sb.Length > 0 ? sb.ToString() : "unknown";
        }

        /// <summary>
        /// Extracts the userId from a Vivox participant URI.
        /// URI format: sip:.ISSUER.userId.@domain
        /// Account handle format: .ISSUER.userId.
        /// </summary>
        private static string ExtractUserIdFromUri(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;

            // Try to find the userId between the issuer and the trailing dot
            // Format: sip:.ISSUER.userId.@domain or .ISSUER.userId.
            var issuerPrefix = $".{ISSUER}.";
            var idx = uri.IndexOf(issuerPrefix, StringComparison.Ordinal);
            if (idx < 0) return null;

            var start = idx + issuerPrefix.Length;
            var end = uri.IndexOf('.', start);
            if (end < 0) end = uri.IndexOf('@', start);
            if (end < 0) end = uri.Length;

            return end > start ? uri.Substring(start, end - start) : null;
        }
    }
}
