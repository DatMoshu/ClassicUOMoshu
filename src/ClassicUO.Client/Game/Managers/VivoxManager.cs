// VivoxManager.cs — Singleton Vivox lifecycle manager for ClassicUO
// Manages SDK init, login, channel join, positional updates, and shutdown.

using System;
using System.Threading.Tasks;
using ClassicUO.Network.Vivox;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
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

        // Throttle position updates — no need to send every tick
        private uint _lastPositionUpdateTick;
        private const uint POSITION_UPDATE_INTERVAL_MS = 100; // ~10 updates/sec

        public bool IsInitialized => _client != null && !_initFailed;

        // ── Init / Shutdown ───────────────────────────────────────────────────

        public async void Initialize()
        {
            if (_client != null) return;

            try
            {
                var config = new VivoxClient.Config(ISSUER, SECRET, DOMAIN, SERVER);
                _client = new VivoxClient(config);

                _client.LoginStateChanged += OnLoginStateChanged;

                await _client.InitializeAsync();
                Log.Trace("[VivoxManager] Vivox SDK ready.");
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
                _client.Dispose();
                _client = null;
                Log.Trace("[VivoxManager] Shutdown complete.");
            }
        }

        // ── Login / Logout ────────────────────────────────────────────────────

        /// <summary>
        /// Called when the player enters the game world (CreatePlayer).
        /// Uses the character name as both userId and displayName.
        /// </summary>
        public void OnPlayerEnterWorld(string characterName)
        {
            if (_client == null || _initFailed || _client.IsLoggedIn) return;
            if (_isLoggingIn) return;

            try
            {
                _isLoggingIn = true;
                // Sanitize character name for Vivox (lowercase, no spaces)
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
                _client.Logout();
            }
            catch (Exception ex)
            {
                Log.Warn($"[VivoxManager] Logout error: {ex.Message}");
            }
            _isLoggingIn = false;
            _lastX = 0;
            _lastY = 0;
        }

        // ── Positional Update ─────────────────────────────────────────────────

        /// <summary>
        /// Called every game tick from World.Update() with the player's current tile.
        /// Throttled to ~10 updates/sec and only sends when position changes.
        /// </summary>
        public void UpdatePosition(ushort tileX, ushort tileY, uint currentTick)
        {
            if (_client == null || !_client.IsLoggedIn) return;

            // Throttle: skip if not enough time has passed
            if (currentTick - _lastPositionUpdateTick < POSITION_UPDATE_INTERVAL_MS)
                return;

            // Skip if position hasn't changed
            if (tileX == _lastX && tileY == _lastY)
                return;

            _lastX = tileX;
            _lastY = tileY;
            _lastPositionUpdateTick = currentTick;

            try
            {
                _client.UpdatePosition(tileX, tileY);

                _positionLogCounter++;
                // Log every ~2 seconds (every 20th update at 10/sec)
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

        // ── Event Handlers ────────────────────────────────────────────────────

        private void OnLoginStateChanged(VxLoginState state)
        {
            Log.Trace($"[VivoxManager] Login state → {state}");

            if (state == VxLoginState.LoggedIn)
            {
                _isLoggingIn = false;
                // Auto-join proximity channel after login succeeds
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

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string SanitizeUserId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "unknown";

            // Vivox user IDs: lowercase, alphanumeric + underscores
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
    }
}
