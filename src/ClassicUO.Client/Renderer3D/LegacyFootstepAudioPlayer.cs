// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IFootstepAudioPlayer. Wraps the legacy
// Ovani-pack file resolver + 8-slot ring-buffer pool that prevents XAudio2 voice leaks
// when steps fire 2-3x per second.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Renderer.Audio;
using Microsoft.Xna.Framework.Audio;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IFootstepAudioPlayer"/>. Resolves relative paths against
    /// <see cref="AudioPathResolver.AudioRoot"/>, decodes WAV files via
    /// <see cref="SoundEffect.FromStream"/>, caches decoded buffers, and recycles
    /// <see cref="SoundEffectInstance"/> through an 8-slot ring buffer.
    /// </summary>
    internal sealed class LegacyFootstepAudioPlayer : IFootstepAudioPlayer
    {
        private const int POOL_SIZE = 8;

        private readonly Dictionary<string, SoundEffect> _cache =
            new Dictionary<string, SoundEffect>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SoundEffectInstance[] _pool = new SoundEffectInstance[POOL_SIZE];
        private int _poolNext;

        public bool Exists(string relativePath)
        {
            string full = ResolveFullPath(relativePath);
            return full != null && File.Exists(full);
        }

        public bool Play(string relativePath, float volume, float pitch)
        {
            string full = ResolveFullPath(relativePath);
            if (full == null) return false;

            SoundEffect fx = LoadFx(full);
            if (fx == null) return false;

            try
            {
                int slot = FindReusableSlot();
                // SoundEffectInstance can't be rebound to a different SoundEffect, so
                // dispose the existing slot occupant and create fresh whenever we recycle.
                try { _pool[slot]?.Stop(); _pool[slot]?.Dispose(); } catch { /* ignore */ }
                SoundEffectInstance inst = fx.CreateInstance();
                _pool[slot] = inst;
                _poolNext = (slot + 1) % POOL_SIZE;

                inst.Volume = volume < 0f ? 0f : (volume > 1f ? 1f : volume);
                inst.Pitch = pitch < -1f ? -1f : (pitch > 1f ? 1f : pitch);
                inst.Play();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Walk the pool from the round-robin cursor; first stopped/free slot wins.
        // If all slots are still playing (rare), evict the cursor position (oldest).
        private int FindReusableSlot()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                int idx = (_poolNext + i) % POOL_SIZE;
                SoundEffectInstance candidate = _pool[idx];
                if (candidate == null || candidate.IsDisposed || candidate.State == SoundState.Stopped)
                    return idx;
            }
            return _poolNext;
        }

        private static string ResolveFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            string root = AudioPathResolver.AudioRoot;
            if (root == null) return null;
            return Path.Combine(root, relativePath);
        }

        private SoundEffect LoadFx(string fullPath)
        {
            if (_cache.TryGetValue(fullPath, out SoundEffect fx)) return fx;
            if (!File.Exists(fullPath))
            {
                if (_missingLogged.Add(fullPath))
                    Console.WriteLine($"[FootstepAudio] missing: {fullPath}");
                _cache[fullPath] = null;
                return null;
            }
            try
            {
                using FileStream fs = File.OpenRead(fullPath);
                fx = SoundEffect.FromStream(fs);
                _cache[fullPath] = fx;
                return fx;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FootstepAudio] load failed for {fullPath}: {ex.Message}");
                _cache[fullPath] = null;
                return null;
            }
        }
    }
}
