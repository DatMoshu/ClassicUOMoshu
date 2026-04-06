// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.SpeechRecognition.Diagnostics;
using ClassicUO.SpeechRecognition.Engines;
using ClassicUO.SpeechRecognition.Interfaces;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Monitors microphone input continuously — even while TTS is playing —
    /// and cancels audio output the moment the user starts speaking.
    ///
    /// This enables natural conversational interruption:
    ///   Avatar is mid-sentence → user speaks → Avatar stops → listens.
    ///
    /// Uses a dedicated low-latency VAD instance separate from the main AudioPipeline
    /// so TTS playback doesn't interfere with the detection window.
    /// </summary>
    internal sealed class BargeInController : IDisposable
    {
        private readonly IVadEngine _vad;
        private readonly TtsPlaybackManager _playback;
        private readonly GenerativeSpeech _generativeSpeech;

        // Must exceed this many consecutive "speech" frames before barge-in triggers
        // (~200ms at 32ms/frame = 6 frames)
        private int _confirmedFrames;
        private const int ConfirmFrames = 6;

        private bool _bargeInArmed; // true when TTS is playing
        private bool _disposed;

        /// <summary>Fired when the user successfully interrupts TTS playback.</summary>
        public event EventHandler BargeInDetected;

        public BargeInController(
            IVadEngine vad,
            TtsPlaybackManager playback,
            GenerativeSpeech generativeSpeech)
        {
            _vad = vad ?? throw new ArgumentNullException(nameof(vad));
            _playback = playback ?? throw new ArgumentNullException(nameof(playback));
            _generativeSpeech = generativeSpeech ?? throw new ArgumentNullException(nameof(generativeSpeech));

            _playback.PlaybackComplete += OnPlaybackComplete;
        }

        /// <summary>
        /// Arm barge-in detection. Call when TTS playback starts.
        /// The controller will now actively watch for user speech.
        /// </summary>
        public void Arm()
        {
            _confirmedFrames = 0;
            _bargeInArmed = true;
            _vad.Reset();
        }

        /// <summary>
        /// Process a 32ms audio frame while TTS is playing.
        /// Call from the same NAudio callback that feeds the main pipeline.
        /// This runs on the NAudio thread — must be fast.
        /// </summary>
        public void ProcessFrame(float[] frame)
        {
            if (!_bargeInArmed || !Settings.GlobalSettings.BargeInEnabled) return;

            float prob = _vad.ProcessFrame(frame);

            if (prob >= 0.5f)
            {
                _confirmedFrames++;
                if (_confirmedFrames >= ConfirmFrames)
                    TriggerBargeIn();
            }
            else
            {
                _confirmedFrames = 0;
            }
        }

        private void TriggerBargeIn()
        {
            if (!_bargeInArmed) return;
            _bargeInArmed = false;
            _confirmedFrames = 0;

            // Cancel TTS output immediately
            _generativeSpeech?.Cancel();
            _playback.Cancel();

            BargeInDetected?.Invoke(this, EventArgs.Empty);
            SpeechLog.Info(SpeechLogChannel.Voice, "Barge-in detected — interrupting Avatar.");
        }

        private void OnPlaybackComplete(object sender, EventArgs e)
        {
            // Disarm when TTS finishes naturally (no interruption needed)
            _bargeInArmed = false;
            _confirmedFrames = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _vad?.Dispose();
        }
    }
}
