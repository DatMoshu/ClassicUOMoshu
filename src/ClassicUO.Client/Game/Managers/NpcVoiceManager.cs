// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using ClassicUO.Utility.Logging;
using NAudio.Wave;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Plays pre-generated NPC voice line WAV files from Data/VoiceLines/.
    /// Triggered by server packet 0xBF / sub-command 0x0102.
    /// One line at a time — a new Play() call stops the previous line.
    /// </summary>
    internal sealed class NpcVoiceManager : IDisposable
    {
        public static readonly NpcVoiceManager Instance = new NpcVoiceManager();

        private WaveOutEvent _waveOut;
        private AudioFileReader _currentFile;
        private float _volume = 0.8f;
        private bool _disposed;

        /// <summary>NPC voice volume (0–1). Persists across calls.</summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                if (_waveOut != null)
                    _waveOut.Volume = _volume;
            }
        }

        /// <summary>
        /// Play a voice line.
        /// relPath: relative path as sent by the server, e.g. "vendor/Baker_male_idle_1.wav"
        /// Resolved against {exe}/Data/VoiceLines/.
        /// Silently skips if the file does not exist.
        /// </summary>
        public void Play(string relPath)
        {
            if (string.IsNullOrEmpty(relPath))
                return;

            string fullPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data", "VoiceLines",
                relPath.Replace('/', Path.DirectorySeparatorChar)
            );

            if (!File.Exists(fullPath))
            {
                Log.Warn($"[NpcVoice] Missing: {fullPath}");
                return;
            }

            Stop();

            try
            {
                _currentFile = new AudioFileReader(fullPath) { Volume = _volume };
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_currentFile);
                _waveOut.PlaybackStopped += (_, _) => Stop();
                _waveOut.Play();
                Log.Trace($"[NpcVoice] Playing: {relPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[NpcVoice] Playback failed for {relPath}: {ex.Message}");
                Stop();
            }
        }

        /// <summary>Stop and release the current audio file.</summary>
        public void Stop()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _currentFile?.Dispose();
            _currentFile = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
