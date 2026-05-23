// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).
//
// Fire-patch simulator: each patch lives for `Lifetime` seconds, emitting embers and
// smoke through IParticleSpawner. Horizontal drift comes from the cached
// WindUpdatedEvent vector — no direct coupling to IWindService.

using System;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Production implementation of <see cref="IFireService"/>. Fixed-size struct array
    /// for the patch pool — zero per-frame allocation. Allocation-free in the steady state.
    /// </summary>
    public sealed class FireService : IFireService, IFrameService, IDisposable
    {
        private readonly FireServiceConfig _config;
        private readonly IParticleSpawner _spawner;
        private readonly Random _rng;
        private readonly IDisposable _windSubscription;

        // Patch pool — fixed size from config.MaxFires.
        private readonly Fire[] _fires;

        // Cached wind vector from the most recent WindUpdatedEvent.
        private Vector2 _windXZ;

        // Service state
        private bool _enabled;

        public FireService(FireServiceConfig config, IRendererEventBus bus, IParticleSpawner spawner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (bus is null) throw new ArgumentNullException(nameof(bus));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));

            int max = _config.MaxFires;
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(config), "MaxFires must be >= 1");

            _fires = new Fire[max];
            _rng = new Random(_config.RandomSeed);
            _enabled = _config.InitialEnabled;

            _windSubscription = bus.Subscribe<WindUpdatedEvent>(OnWindUpdated);
        }

        public void Dispose() => _windSubscription?.Dispose();

        private void OnWindUpdated(WindUpdatedEvent evt) => _windXZ = evt.VectorXZ;

        // ===== IFireService =====

        public bool Enabled => _enabled;

        public int LiveFires
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _fires.Length; i++) if (_fires[i].Alive) n++;
                return n;
            }
        }

        public void SetEnabled(bool enabled) => _enabled = enabled;

        public void Ignite(Vector3 worldGround)
            => Ignite(worldGround, _config.DefaultRadius, _config.DefaultLifetime);

        public void Ignite(Vector3 worldGround, float radius, float lifetime)
        {
            if (!_enabled) return;

            int slot = FindSpawnSlot();
            _fires[slot] = new Fire
            {
                Pos = worldGround,
                Radius = MathF.Max(_config.MinIgniteRadius, radius),
                Age = 0f,
                Lifetime = MathF.Max(_config.MinIgniteLifetime, lifetime),
                EmberAccum = 0f,
                SmokeAccum = 0f,
                Alive = true,
            };
        }

        public void Clear()
        {
            for (int i = 0; i < _fires.Length; i++) _fires[i].Alive = false;
        }

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            if (!_enabled) return;

            float dt = ctx.DeltaSeconds;
            if (dt > _config.MaxDeltaSeconds) dt = _config.MaxDeltaSeconds;

            for (int i = 0; i < _fires.Length; i++)
                AdvanceFire(ref _fires[i], dt);
        }

        // ===== Internals =====

        private int FindSpawnSlot()
        {
            // Prefer empty slot; if pool is full, recycle the oldest fire.
            int oldestIdx = 0;
            float oldestAge = -1f;
            for (int i = 0; i < _fires.Length; i++)
            {
                if (!_fires[i].Alive) return i;
                if (_fires[i].Age > oldestAge)
                {
                    oldestAge = _fires[i].Age;
                    oldestIdx = i;
                }
            }
            return oldestIdx;
        }

        private void AdvanceFire(ref Fire f, float dt)
        {
            if (!f.Alive) return;

            f.Age += dt;
            if (f.Age >= f.Lifetime) { f.Alive = false; return; }

            float intensity = ComputeIntensity(f.Age, f.Lifetime);

            f.EmberAccum += _config.EmberRatePerSec * intensity * dt;
            f.SmokeAccum += _config.SmokeRatePerSec * intensity * dt;

            while (f.EmberAccum >= 1f) { SpawnEmber(in f); f.EmberAccum -= 1f; }
            while (f.SmokeAccum >= 1f) { SpawnSmoke(in f); f.SmokeAccum -= 1f; }
        }

        /// <summary>
        /// Emit-rate envelope. Ramps 0→1 over the first 30% of lifetime, then linearly
        /// tapers back to 0 over the remaining 70%. Public-static for direct test exercise.
        /// </summary>
        public static float ComputeIntensity(float age, float lifetime)
        {
            if (lifetime <= 0f) return 0f;
            float t01 = age / lifetime;
            float v = (t01 < 0.3f) ? (t01 / 0.3f) : (1f - (t01 - 0.3f) / 0.7f);
            return v < 0f ? 0f : v;
        }

        private void SpawnEmber(in Fire f)
        {
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            float r = MathF.Sqrt((float)_rng.NextDouble()) * f.Radius;
            Vector3 origin = f.Pos + new Vector3(MathF.Cos(ang) * r, 1f, MathF.Sin(ang) * r);

            Vector2 wind = _windXZ * _config.WindAdvectionScale;
            Vector3 vel = new Vector3(
                wind.X * 0.25f + ((float)_rng.NextDouble() - 0.5f) * 18f,
                30f + (float)_rng.NextDouble() * 35f,
                wind.Y * 0.25f + ((float)_rng.NextDouble() - 0.5f) * 18f);

            // Hot core → orange → fade to dark.
            int variant = _rng.Next(4);
            Color hot = variant switch
            {
                0 => new Color(255, 240, 180),
                1 => new Color(255, 200, 110),
                2 => new Color(255, 150, 60),
                _ => new Color(255, 110, 40),
            };
            Color cool = new Color(60, 20, 8, (byte)0);
            float life = 0.55f + (float)_rng.NextDouble() * 0.6f;

            // Untextured additive — F_TRAIL gives the eye sub-flicker.
            _spawner.Spawn(origin, vel,
                acceleration: new Vector3(0f, 4f, 0f),
                lifetimeSeconds: life,
                sizeStart: 4f, sizeEnd: 1f,
                colorStart: hot, colorEnd: cool,
                flags: ParticleFlags.Trail);
        }

        private void SpawnSmoke(in Fire f)
        {
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            float r = (float)_rng.NextDouble() * f.Radius * 0.6f;
            Vector3 origin = f.Pos + new Vector3(MathF.Cos(ang) * r, 6f, MathF.Sin(ang) * r);

            Vector2 windS = _windXZ * _config.WindAdvectionScale;
            Vector3 vel = new Vector3(
                windS.X * 1.1f + ((float)_rng.NextDouble() - 0.5f) * 6f,
                14f + (float)_rng.NextDouble() * 10f,
                windS.Y * 1.1f + ((float)_rng.NextDouble() - 0.5f) * 6f);

            // Greyed-down, low alpha so multiple puffs read as a column without
            // saturating to white in additive blend.
            Color hot = new Color(80, 70, 65, (byte)90);
            Color cool = new Color(20, 18, 16, (byte)0);
            float life = 1.6f + (float)_rng.NextDouble() * 1.2f;

            _spawner.Spawn(origin, vel,
                acceleration: new Vector3(0f, 2f, 0f),
                lifetimeSeconds: life,
                sizeStart: 12f, sizeEnd: 36f,
                colorStart: hot, colorEnd: cool,
                flags: ParticleFlags.None);
        }

        // Internal record — packed in a fixed-size array for zero-allocation simulation.
        private struct Fire
        {
            public Vector3 Pos;
            public float Radius;
            public float Age;
            public float Lifetime;
            public float EmberAccum;
            public float SmokeAccum;
            public bool Alive;
        }
    }
}
