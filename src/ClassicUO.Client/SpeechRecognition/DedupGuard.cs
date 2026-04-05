// SPDX-License-Identifier: BSD-2-Clause

using System;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Prevents the same voice command from firing twice within a configurable window.
    /// Thread-safe via volatile fields — safe for use from audio callback and game threads.
    /// </summary>
    internal sealed class DedupGuard
    {
        private readonly long _windowMs;
        private volatile string _lastCommand = string.Empty;
        private long _lastTickMs;

        public DedupGuard(long windowMs = 2000)
        {
            _windowMs = windowMs;
        }

        /// <summary>Returns true if this command was already executed within the dedup window.</summary>
        public bool IsDuplicate(string text)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return string.Equals(text, _lastCommand, StringComparison.OrdinalIgnoreCase)
                && nowMs - _lastTickMs < _windowMs;
        }

        /// <summary>Record a command execution for future dedup checks.</summary>
        public void RecordExecuted(string text)
        {
            _lastCommand = text;
            _lastTickMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>Check and record in one call. Returns true if duplicate (should skip).</summary>
        public bool CheckAndRecord(string text)
        {
            if (IsDuplicate(text)) return true;
            RecordExecuted(text);
            return false;
        }

        /// <summary>Returns true if any command was recorded within the dedup window.</summary>
        public bool FiredRecently()
        {
            if (string.IsNullOrEmpty(_lastCommand)) return false;
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return nowMs - _lastTickMs < _windowMs;
        }
    }
}

