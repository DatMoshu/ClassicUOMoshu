// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — fake-crowd ambience by overlapping random voicelines.
//
// Spawns N concurrent "voices", each loops forever picking a random clip from
// a folder, plays it, jitters a short pause, picks another. After `DurationSec`
// every voice stops. Volumes/pans are randomized per voice and per playback so
// the mix blurs into a babble. Driven by `[crowd` chat command + `[crowdgump`
// admin tuning UI.
//
// Audio path: decode mp3 → 16-bit PCM with NAudio's managed Mp3FileReader, wrap
// in an FNA SoundEffect, play instances via SoundEffectInstance. Matches the
// engine's own audio path (see AudioManager) and avoids NAudio's MF/COM
// playback path, which fails with NotSupported_COM on background threads.
//
// Pile-up control: a global "next-allowed-start" tick gate ensures no two
// voices can start within `MinIntervoiceGapMs` of each other. Without it, with
// short clips and small per-voice gaps, voices end up firing in clumps.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Audio;
using NAudio.Wave;

namespace ClassicUO.Game.Managers
{
    internal static class CrowdMimicManager
    {
        // ---- Tunables (mutated live by CrowdGump) ----
        public static int DurationSec = 30;
        public static int VoiceCount = 5;
        public static int GapMinMs = 600;          // per-voice min pause between clips
        public static int GapMaxMs = 2400;         // per-voice max pause between clips
        public static int MinIntervoiceGapMs = 450;// global min spacing between any two clip starts
        public static int InitialStaggerMs = 2000; // 0..N initial random delay per voice
        public static float VolumeMin = 0.30f;
        public static float VolumeMax = 0.75f;
        public static string FolderRel = "Data/voicelines/JarJar";

        public const int DefaultDurationSec = 30;
        public const int DefaultVoiceCount = 5;

        private static CancellationTokenSource _cts;
        private static int _activeVoices;
        private static SoundEffect[] _clips;

        private static readonly object _gateLock = new object();
        private static long _nextAllowedStartTicks; // DateTime.UtcNow.Ticks gate

        public static bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
        public static int ActiveVoices => _activeVoices;
        public static int LoadedClips => _clips?.Length ?? 0;

        public static string Start()
            => Start(FolderRel, DurationSec, VoiceCount);

        public static string Start(string folderRel, int durationSec, int voiceCount)
        {
            // One crowd at a time — restart if already running.
            Stop();

            FolderRel = folderRel;
            DurationSec = durationSec;
            VoiceCount = voiceCount;

            string folder = Path.Combine(AppContext.BaseDirectory, folderRel);
            if (!Directory.Exists(folder))
                return $"folder not found: {folder}";

            string[] files = Directory.GetFiles(folder, "*.mp3", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
                return $"no .mp3 files in {folder}";

            // Decode every clip once on the calling (game) thread. Caching as
            // SoundEffect lets background voice threads play instances without
            // touching any decoder/COM-bound API.
            var loaded = new List<SoundEffect>(files.Length);
            int failed = 0;
            foreach (string path in files)
            {
                try
                {
                    loaded.Add(LoadMp3AsSoundEffect(path));
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"[3DCUO][crowd] failed to decode '{Path.GetFileName(path)}': {ex.Message}");
                }
            }
            if (loaded.Count == 0)
                return $"no decodable .mp3 files in {folder} ({failed} failed)";

            _clips = loaded.ToArray();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var rng = new Random();
            _nextAllowedStartTicks = DateTime.UtcNow.Ticks;

            for (int i = 0; i < voiceCount; i++)
            {
                int seed = rng.Next();
                Interlocked.Increment(ref _activeVoices);
                Task.Run(() => VoiceLoop(seed, token), token)
                    .ContinueWith(_ => Interlocked.Decrement(ref _activeVoices));
            }

            // Auto-stop after duration. Doesn't block caller.
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(TimeSpan.FromSeconds(durationSec), token); }
                catch (TaskCanceledException) { }
                Stop();
            });

            string failMsg = failed > 0 ? $", {failed} undecodable" : "";
            return $"crowd: {voiceCount} voices over {durationSec}s from {folderRel} ({_clips.Length} clips{failMsg})";
        }

        public static void Stop()
        {
            var cts = _cts;
            if (cts != null)
            {
                _cts = null;
                try { cts.Cancel(); } catch { }
                try { cts.Dispose(); } catch { }
            }

            var clips = _clips;
            _clips = null;
            if (clips != null)
            {
                foreach (var s in clips)
                {
                    try { s.Dispose(); } catch { }
                }
            }
        }

        private static void VoiceLoop(int seed, CancellationToken ct)
        {
            var rng = new Random(seed);

            int initStagger = Math.Max(0, InitialStaggerMs);
            try { Thread.Sleep(rng.Next(0, initStagger == 0 ? 1 : initStagger)); } catch { }

            while (!ct.IsCancellationRequested)
            {
                var clips = _clips;
                if (clips == null || clips.Length == 0) return;

                // Global throttle: wait until the inter-voice gate opens. Each
                // voice grabs the gate and pushes it forward by MinIntervoiceGapMs.
                if (!WaitForGate(ct)) return;

                SoundEffect clip = clips[rng.Next(clips.Length)];
                float vMin = Math.Clamp(VolumeMin, 0f, 1f);
                float vMax = Math.Clamp(VolumeMax, 0f, 1f);
                if (vMax < vMin) vMax = vMin;
                float volume = vMin + (float)rng.NextDouble() * (vMax - vMin);
                float pan    = ((float)rng.NextDouble() * 2f) - 1f;

                SoundEffectInstance instance = null;
                try
                {
                    instance = clip.CreateInstance();
                    instance.Volume = volume;
                    instance.Pan = pan;
                    instance.Play();

                    while (instance.State == SoundState.Playing)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            try { instance.Stop(); } catch { }
                            return;
                        }
                        Thread.Sleep(50);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[3DCUO][crowd] voice {seed} playback error: {ex.Message}");
                    try { Thread.Sleep(500); } catch { }
                }
                finally
                {
                    try { instance?.Dispose(); } catch { }
                }

                if (ct.IsCancellationRequested) return;

                // Per-voice jitter so voices drift apart over time.
                int gMin = Math.Max(0, GapMinMs);
                int gMax = Math.Max(gMin + 1, GapMaxMs);
                int gapMs = rng.Next(gMin, gMax);
                try { Thread.Sleep(gapMs); } catch { }
            }
        }

        // Returns false if cancelled while waiting.
        private static bool WaitForGate(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                long nowTicks = DateTime.UtcNow.Ticks;
                long waitMs;
                lock (_gateLock)
                {
                    long gateMs = (_nextAllowedStartTicks - nowTicks) / TimeSpan.TicksPerMillisecond;
                    if (gateMs <= 0)
                    {
                        // We get to fire now — push the gate forward.
                        int spacing = Math.Max(0, MinIntervoiceGapMs);
                        _nextAllowedStartTicks = nowTicks + spacing * TimeSpan.TicksPerMillisecond;
                        return true;
                    }
                    waitMs = gateMs;
                }
                // Sleep outside the lock. Cap per-iteration sleep so cancel is responsive.
                int sleep = (int)Math.Min(waitMs, 100);
                if (sleep <= 0) sleep = 1;
                try { Thread.Sleep(sleep); } catch { return false; }
            }
            return false;
        }

        // Fully managed mp3 → 16-bit PCM decode (no Media Foundation / no COM).
        // FNA's SoundEffect requires 16-bit PCM, mono or stereo, at 8..48 kHz.
        private static SoundEffect LoadMp3AsSoundEffect(string path)
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
