// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wrapping XNA SoundEffect / SoundEffectInstance behind
// IAudioClipLibrary. Lives outside the Audio domain folder because it depends on the
// concrete XNA audio types.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Renderer.Audio;
using Microsoft.Xna.Framework.Audio;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IAudioClipLibrary"/>. Resolves relative paths against
    /// <see cref="AudioPathResolver.AudioRoot"/>, decodes WAV files via
    /// <see cref="SoundEffect.FromStream"/>, and caches the decoded buffers.
    /// </summary>
    /// <remarks>
    /// <para>One-shots play through a fixed-size ring-buffer pool of
    /// <see cref="SoundEffectInstance"/> slots. Without this, every <c>PlayOneShot</c>
    /// allocated a new instance that XNA/FNA never auto-disposed (contrary to the prior
    /// fire-and-forget comment on this class) — a thunder strike or nuke barrage would
    /// leak XAudio2 voices indefinitely. Slots are recycled in round-robin order;
    /// if all slots are still playing the oldest is evicted. POOL_SIZE = 16 matches the
    /// observed peak concurrent one-shots during a nuke show.</para>
    /// <para>Loops are owned by their caller (<see cref="IAudioLoopHandle"/>) and do
    /// not go through the pool — each loop has its own <see cref="SoundEffectInstance"/>
    /// disposed when the handle is disposed.</para>
    /// <para>Missing files are logged once and cached as null so subsequent calls
    /// short-circuit. Decoded <see cref="SoundEffect"/> instances are kept for the
    /// lifetime of the process — XNA holds the audio buffer in memory after first decode.</para>
    /// </remarks>
    internal sealed class LegacyAudioClipLibrary : IAudioClipLibrary, IDisposable
    {
        private const int POOL_SIZE = 16;

        private readonly Dictionary<string, SoundEffect> _cache =
            new Dictionary<string, SoundEffect>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SoundEffectInstance[] _oneShotPool = new SoundEffectInstance[POOL_SIZE];
        private int _poolNext;
        private readonly bool _verboseLog;

        public LegacyAudioClipLibrary(bool verboseLog = false)
        {
            _verboseLog = verboseLog;
        }

        public void PlayOneShot(string relativePath, float volume, float pitch)
        {
            SoundEffect fx = LoadFx(relativePath);
            if (fx == null) return;
            try
            {
                int slot = FindReusableSlot();
                // SoundEffectInstance can't be rebound to a different SoundEffect, so
                // dispose the existing slot occupant and create fresh whenever we recycle.
                try { _oneShotPool[slot]?.Stop(); _oneShotPool[slot]?.Dispose(); } catch { /* device gone */ }

                SoundEffectInstance inst = fx.CreateInstance();
                _oneShotPool[slot] = inst;
                _poolNext = (slot + 1) % POOL_SIZE;

                inst.Volume = Clamp01(volume);
                inst.Pitch = Clamp(pitch, -1f, 1f);
                inst.Play();
            }
            catch (Exception ex)
            {
                if (_verboseLog)
                    Console.WriteLine($"[WeatherAudio] one-shot play failed for {relativePath}: {ex.Message}");
            }
        }

        public IAudioLoopHandle StartLoop(string relativePath, float initialVolume)
        {
            SoundEffect fx = LoadFx(relativePath);
            if (fx == null) return null;
            try
            {
                SoundEffectInstance inst = fx.CreateInstance();
                inst.IsLooped = true;
                inst.Volume = Clamp01(initialVolume);
                inst.Play();
                if (_verboseLog)
                    Console.WriteLine($"[WeatherAudio] loop start: {relativePath}");
                return new XnaLoopHandle(inst);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WeatherAudio] loop start failed for {relativePath}: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _oneShotPool.Length; i++)
            {
                SoundEffectInstance inst = _oneShotPool[i];
                if (inst == null) continue;
                try { inst.Stop(); inst.Dispose(); } catch { /* ignored on shutdown */ }
                _oneShotPool[i] = null;
            }
            foreach (KeyValuePair<string, SoundEffect> kvp in _cache)
            {
                try { kvp.Value?.Dispose(); } catch { /* ignored on shutdown */ }
            }
            _cache.Clear();
        }

        // Walk the pool from the round-robin cursor; first stopped/free/disposed slot wins.
        // If all slots are still playing (rare — burst of >POOL_SIZE simultaneous one-shots)
        // we evict the cursor position (oldest by round-robin).
        private int FindReusableSlot()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                int idx = (_poolNext + i) % POOL_SIZE;
                SoundEffectInstance candidate = _oneShotPool[idx];
                if (candidate == null || candidate.IsDisposed || candidate.State == SoundState.Stopped)
                    return idx;
            }
            return _poolNext;
        }

        private SoundEffect LoadFx(string rel)
        {
            if (_cache.TryGetValue(rel, out SoundEffect fx)) return fx;

            string root = AudioPathResolver.AudioRoot;
            if (root == null) { _cache[rel] = null; return null; }
            string full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                if (_missingLogged.Add(rel))
                    Console.WriteLine($"[WeatherAudio] missing audio file: {full}");
                _cache[rel] = null;
                return null;
            }
            try
            {
                using FileStream fs = File.OpenRead(full);
                fx = SoundEffect.FromStream(fs);
                _cache[rel] = fx;
                if (_verboseLog)
                    Console.WriteLine($"[WeatherAudio] loaded {rel} ({fx.Duration.TotalSeconds:0.0}s)");
                return fx;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WeatherAudio] load failed for {rel}: {ex.Message}");
                _cache[rel] = null;
                return null;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Clamp(float v, float lo, float hi)
            => v < lo ? lo : (v > hi ? hi : v);

        private sealed class XnaLoopHandle : IAudioLoopHandle
        {
            private SoundEffectInstance _inst;

            public XnaLoopHandle(SoundEffectInstance inst) { _inst = inst; }

            public void SetVolume(float volume)
            {
                if (_inst == null) return;
                try { _inst.Volume = Clamp01(volume); } catch { /* device gone */ }
            }

            public void Dispose()
            {
                if (_inst == null) return;
                try { _inst.Stop(); _inst.Dispose(); } catch { /* device gone */ }
                _inst = null;
            }
        }
    }
}
