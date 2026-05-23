// SPDX-License-Identifier: BSD-2-Clause
//
// NPC voice line playback for the 3DCUO client. Triggered by server packet
// 0xBF / sub-command 0x0102 (one mp3 path per packet, see
// OutgoingNPCVoicePackets on the server).
//
// Audio path: decode mp3 → 16-bit PCM with NAudio's managed Mp3FileReader,
// wrap in an FNA SoundEffect, play via SoundEffectInstance. This is the
// same path CrowdMimicManager uses and it deliberately avoids NAudio's
// MediaFoundation/COM playback (AudioFileReader + WaveOutEvent), which
// fails with `NotSupported_COM` for .mp3 on background threads / when
// MediaFoundation hasn't been Startup()'d.
//
// One line at a time — a new Play() call stops the previous line.
//
// Decoded SoundEffects are cached by relative path with a small LRU cap so
// the same greeting/idle/quest_hook clip doesn't re-decode on every replay.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Configuration;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework.Audio;
using NAudio.Wave;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Plays pre-generated NPC voice line mp3 files from Data/voicelines/.
    /// </summary>
    internal sealed class NpcVoiceManager : IDisposable
    {
        public static readonly NpcVoiceManager Instance = new NpcVoiceManager();

        // Cache of decoded SoundEffects keyed by relative path. Bounded so a
        // long session doesn't accumulate GBs in RAM.
        private const int MaxCacheEntries = 128;
        private readonly Dictionary<string, SoundEffect> _cache =
            new Dictionary<string, SoundEffect>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> _lru = new LinkedList<string>();

        private SoundEffectInstance _current;
        private float _volume = 0.8f;
        private float _narratorVolume = 0.9f;
        private bool _disposed;

        // Anti-spam / cooldown bookkeeping. All times are UTC tick counts.
        private long _lastGlobalPlayTicks;
        private readonly Dictionary<uint, long> _lastPerNpcTicks = new Dictionary<uint, long>();
        private readonly System.Random _rng = new System.Random();

        // Serial.MinusOne (server-side) — special sender id meaning "this is a
        // narrator line; route to NarratorVolume and bypass the gates".
        public const uint NarratorSerial = 0xFFFFFFFFu;

        /// <summary>NPC voice volume (0–1). Persists across calls.</summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                if (_current != null && _currentIsNarrator == false)
                    _current.Volume = _volume;
            }
        }

        /// <summary>Narrator volume (0–1). Separate bus from <see cref="Volume"/>.</summary>
        public float NarratorVolume
        {
            get => _narratorVolume;
            set
            {
                _narratorVolume = Math.Clamp(value, 0f, 1f);
                if (_current != null && _currentIsNarrator)
                    _current.Volume = _narratorVolume;
            }
        }

        // Tracks whether the currently-playing clip came in on the narrator
        // serial — used by the Volume / NarratorVolume setters so live changes
        // route to the right bus.
        private bool _currentIsNarrator;

        /// <summary>Sync volume + history cap from the active player profile.</summary>
        public void ApplyProfileSettings(Profile p)
        {
            if (p == null) return;
            Volume = Math.Clamp(p.NpcVoiceVolume, 0, 100) / 100f;
            NarratorVolume = Math.Clamp(p.NarratorVolume, 0, 100) / 100f;
            VoiceHistoryManager.Instance.MaxEntries = Math.Max(1, p.NpcVoiceHistoryLength);
        }

        /// <summary>
        /// Play a voice line.
        /// relPath: relative path as sent by the server, e.g.
        ///   "audio/npc/personas/&lt;key&gt;/greeting_0_xxx.mp3"
        /// Resolved against {exe}/Data/voicelines/.
        /// Returns the clip's duration in milliseconds, or 0 if the file was
        /// missing / failed to decode. Callers that need to time anything to
        /// the audio (e.g. TalkingHeadGump frame cycling) consume this value;
        /// regular call sites can ignore it.
        /// </summary>
        public int Play(string relPath) => Play(relPath, 0, "?", bypassGates: false);

        /// <summary>
        /// Full-fidelity overload. <paramref name="npcSerial"/> drives the
        /// narrator detection (<see cref="NarratorSerial"/>) and per-NPC
        /// cooldown gating; <paramref name="npcName"/> is recorded in the
        /// replay history. Set <paramref name="bypassGates"/> when the player
        /// explicitly asked for the line (e.g. via -rv).
        /// </summary>
        public int Play(string relPath, uint npcSerial, string npcName = "?", bool bypassGates = false)
        {
            if (string.IsNullOrEmpty(relPath))
                return 0;

            bool isNarrator = npcSerial == NarratorSerial;
            string displayName = isNarrator ? "Narrator" : (npcName ?? "?");

            // Always record into the history ring, even if we end up gating.
            // The player can still recall a missed line via -rv.
            VoiceHistoryManager.Instance.Record(npcSerial, displayName, relPath);

            string fullPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data", "voicelines",
                relPath.Replace('/', Path.DirectorySeparatorChar)
            );

            if (!File.Exists(fullPath))
            {
                Log.Warn($"[NpcVoice] Missing: {fullPath}");
                return 0;
            }

            // Narrator playback or explicit-replay bypass the rate-limit gates;
            // regular NPC adjacency lines are subject to the player's settings.
            if (!isNarrator && !bypassGates && !PassesGates(npcSerial))
            {
                return 0;
            }

            SoundEffect clip;
            try
            {
                clip = GetOrLoad(relPath, fullPath);
            }
            catch (Exception ex)
            {
                Log.Error($"[NpcVoice] Decode failed for {relPath}: {ex.Message}");
                return 0;
            }

            Stop();

            try
            {
                _current = clip.CreateInstance();
                _currentIsNarrator = isNarrator;
                _current.Volume = isNarrator ? _narratorVolume : _volume;
                _current.Play();
                int durationMs = (int)clip.Duration.TotalMilliseconds;
                Log.Trace($"[NpcVoice] Playing: {relPath} ({durationMs} ms) src={(isNarrator ? "narrator" : npcSerial.ToString("X8"))}");

                long nowTicks = DateTime.UtcNow.Ticks;
                _lastGlobalPlayTicks = nowTicks;
                if (!isNarrator && npcSerial != 0)
                {
                    _lastPerNpcTicks[npcSerial] = nowTicks;
                }
                return durationMs;
            }
            catch (Exception ex)
            {
                Log.Error($"[NpcVoice] Playback failed for {relPath}: {ex.Message}");
                Stop();
                return 0;
            }
        }

        private bool PassesGates(uint npcSerial)
        {
            // Resolve current Profile lazily; if no profile yet (login screen),
            // accept by default.
            var profile = ProfileManager.CurrentProfile;
            if (profile == null) return true;

            // Frequency probability check.
            int freq = Math.Clamp(profile.NpcVoiceFrequency, 0, 100);
            if (freq <= 0) return false;
            if (freq < 100 && _rng.Next(100) >= freq) return false;

            long nowTicks = DateTime.UtcNow.Ticks;

            // Global anti-spam.
            int minGlobalSec = Math.Max(0, profile.NpcVoiceMinSecGlobal);
            if (minGlobalSec > 0 && _lastGlobalPlayTicks > 0)
            {
                var sinceGlobal = TimeSpan.FromTicks(nowTicks - _lastGlobalPlayTicks);
                if (sinceGlobal.TotalSeconds < minGlobalSec) return false;
            }

            // Per-NPC cooldown (only when we know the source serial).
            int minPerNpcSec = Math.Max(0, profile.NpcVoiceMinSecPerNpc);
            if (minPerNpcSec > 0 && npcSerial != 0 &&
                _lastPerNpcTicks.TryGetValue(npcSerial, out var lastTicks))
            {
                var sincePerNpc = TimeSpan.FromTicks(nowTicks - lastTicks);
                if (sincePerNpc.TotalSeconds < minPerNpcSec) return false;
            }

            return true;
        }

        /// <summary>Stop and release the current SoundEffectInstance. Cache survives.</summary>
        public void Stop()
        {
            var inst = _current;
            _current = null;
            if (inst != null)
            {
                try { inst.Stop(); } catch { }
                try { inst.Dispose(); } catch { }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();

            foreach (var s in _cache.Values)
            {
                try { s.Dispose(); } catch { }
            }
            _cache.Clear();
            _lru.Clear();
        }

        // ─── Decoded-SoundEffect cache ────────────────────────────────────

        private SoundEffect GetOrLoad(string relPath, string fullPath)
        {
            if (_cache.TryGetValue(relPath, out var hit))
            {
                // Move to MRU.
                _lru.Remove(relPath);
                _lru.AddFirst(relPath);
                return hit;
            }

            var fresh = DecodeMp3ToSoundEffect(fullPath);
            _cache[relPath] = fresh;
            _lru.AddFirst(relPath);

            // Evict LRU if over cap.
            while (_lru.Count > MaxCacheEntries)
            {
                var last = _lru.Last;
                if (last == null) break;
                _lru.RemoveLast();
                if (_cache.TryGetValue(last.Value, out var stale))
                {
                    _cache.Remove(last.Value);
                    try { stale.Dispose(); } catch { }
                }
            }
            return fresh;
        }

        /// <summary>
        /// Managed mp3 → 16-bit PCM → FNA SoundEffect. No MediaFoundation,
        /// safe to call from any thread.
        /// </summary>
        private static SoundEffect DecodeMp3ToSoundEffect(string path)
        {
            using var mp3 = new Mp3FileReader(path);
            var sampleProvider = mp3.ToSampleProvider();
            int channels = sampleProvider.WaveFormat.Channels;
            int sampleRate = sampleProvider.WaveFormat.SampleRate;

            const int blockSamples = 4096;
            var floatBuf = new float[blockSamples];
            using var ms = new MemoryStream();
            int read;
            while ((read = sampleProvider.Read(floatBuf, 0, floatBuf.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    float f = floatBuf[i];
                    if (f > 1f) f = 1f; else if (f < -1f) f = -1f;
                    short s = (short)(f * short.MaxValue);
                    ms.WriteByte((byte)(s & 0xFF));
                    ms.WriteByte((byte)((s >> 8) & 0xFF));
                }
            }

            byte[] pcm = ms.ToArray();
            return new SoundEffect(
                pcm,
                sampleRate,
                channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
        }
    }
}
