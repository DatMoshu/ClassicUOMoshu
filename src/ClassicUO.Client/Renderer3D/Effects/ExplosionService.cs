// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using System;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Production implementation of <see cref="IExplosionService"/>. Fixed-size struct
    /// pool, allocation-free in the steady state.
    /// </summary>
    public sealed class ExplosionService : IExplosionService, IFrameService
    {
        private readonly ExplosionServiceConfig _config;
        private readonly Event[] _events;
        private bool _enabled;

        public ExplosionService(ExplosionServiceConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            int max = _config.MaxEvents;
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(config), "MaxEvents must be >= 1");
            _events = new Event[max];
            _enabled = _config.InitialEnabled;
        }

        // ===== IExplosionService =====

        public bool Enabled => _enabled;

        public int LiveEvents
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _events.Length; i++) if (_events[i].Alive) n++;
                return n;
            }
        }

        public void SetEnabled(bool enabled) => _enabled = enabled;

        public void Add(float centerX, float centerZ, float radius, float strength)
        {
            if (!_enabled) return;

            int slot = FindSpawnSlot();
            _events[slot] = new Event
            {
                Cx = centerX,
                Cz = centerZ,
                Radius = MathF.Max(_config.MinEventRadius, radius),
                Strength = MathF.Max(_config.MinEventStrength, strength),
                Age = 0f,
                Alive = true,
            };
        }

        public void Clear()
        {
            for (int i = 0; i < _events.Length; i++) _events[i].Alive = false;
        }

        public bool Query(float wx, float wz, out float bendX, out float bendZ, out bool leavesHidden)
        {
            bendX = 0f;
            bendZ = 0f;
            leavesHidden = false;
            if (!_enabled) return false;

            bool any = false;
            for (int i = 0; i < _events.Length; i++)
            {
                ref Event e = ref _events[i];
                if (!e.Alive) continue;
                any |= AccumulateInfluence(in e, wx, wz, ref bendX, ref bendZ, ref leavesHidden);
            }
            return any;
        }

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            if (!_enabled) return;
            float maxAge = MathF.Max(_config.BendDurationSeconds, _config.LeavesHiddenSeconds);
            float dt = ctx.DeltaSeconds;
            for (int i = 0; i < _events.Length; i++)
            {
                if (!_events[i].Alive) continue;
                _events[i].Age += dt;
                if (_events[i].Age > maxAge) _events[i].Alive = false;
            }
        }

        // ===== Internals =====

        private int FindSpawnSlot()
        {
            int oldestIdx = 0;
            float oldestAge = -1f;
            for (int i = 0; i < _events.Length; i++)
            {
                if (!_events[i].Alive) return i;
                if (_events[i].Age > oldestAge)
                {
                    oldestAge = _events[i].Age;
                    oldestIdx = i;
                }
            }
            return oldestIdx;
        }

        private bool AccumulateInfluence(
            in Event e,
            float wx, float wz,
            ref float bendX, ref float bendZ, ref bool leavesHidden)
        {
            float dx = wx - e.Cx;
            float dz = wz - e.Cz;
            float dist2 = dx * dx + dz * dz;
            float r2 = e.Radius * e.Radius;
            if (dist2 > r2) return false;

            float dist = MathF.Sqrt(dist2);
            float distFrac = dist / e.Radius;
            if (distFrac < _config.CoreFalloffFraction) distFrac = _config.CoreFalloffFraction;
            float falloff = 1f - distFrac;

            // Leaves-hidden window — flag persists across all events; first hit wins.
            if (e.Age <= _config.LeavesHiddenSeconds) leavesHidden = true;

            // Bend envelope: fast attack ramp, exponential decay tail.
            if (e.Age <= _config.BendDurationSeconds)
            {
                float env = ComputeBendEnvelope(e.Age, _config.BendAttackSeconds, _config.BendDecayRate);
                float invDist = (dist > _config.ZeroDirectionDistance) ? (1f / dist) : 0f;
                float magnitude = _config.BendStrengthPx * e.Strength * falloff * env;
                bendX += dx * invDist * magnitude;
                bendZ += dz * invDist * magnitude;
            }

            return true;
        }

        /// <summary>
        /// Bend envelope: linear ramp 0→1 over the attack window, then exponential decay
        /// using <paramref name="decayRate"/> as the rate constant. Public-static for tests.
        /// </summary>
        public static float ComputeBendEnvelope(float age, float attackSeconds, float decayRate)
        {
            if (age < attackSeconds)
                return attackSeconds <= 0f ? 1f : (age / attackSeconds);
            return MathF.Exp(-(age - attackSeconds) * decayRate);
        }

        private struct Event
        {
            public float Cx, Cz;
            public float Radius;
            public float Strength;
            public float Age;
            public bool Alive;
        }
    }
}
