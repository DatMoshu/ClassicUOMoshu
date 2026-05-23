// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using System;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Production implementation of <see cref="IAmbientMotesService"/>. Tops up the
    /// global particle population each tick toward <see cref="AmbientMotesServiceConfig.TargetAlive"/>
    /// at <see cref="AmbientMotesServiceConfig.SpawnRatePerSecond"/>.
    /// </summary>
    public sealed class AmbientMotesService : IAmbientMotesService, IFrameService
    {
        private readonly AmbientMotesServiceConfig _config;
        private readonly IParticleSpawner _spawner;
        private readonly Random _rng;

        // Mutable state
        private bool _enabled;
        private int _targetAlive;
        private float _radius;
        private AmbientMotesPalette _palette;

        // Bookkeeping
        private Vector3 _anchor;
        private float _spawnAccumulator;

        public AmbientMotesService(AmbientMotesServiceConfig config, IParticleSpawner spawner)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));

            _rng = new Random(_config.RandomSeed);
            _enabled = _config.InitialEnabled;
            _targetAlive = _config.TargetAlive;
            _radius = MathF.Max(0f, _config.Radius);

            _palette = AmbientMotesPaletteLibrary.TryGet(_config.InitialPalette, out AmbientMotesPalette p)
                ? p
                : AmbientMotesPaletteLibrary.Default;
        }

        // ===== IAmbientMotesService =====

        public bool Enabled => _enabled;
        public int TargetAlive => _targetAlive;
        public float Radius => _radius;
        public AmbientMotesPalette CurrentPalette => _palette;

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetTargetAlive(int target) => _targetAlive = Math.Max(0, target);
        public void SetRadius(float radius) => _radius = MathF.Max(0f, radius);

        public void SetPalette(string name)
        {
            if (AmbientMotesPaletteLibrary.TryGet(name, out AmbientMotesPalette p))
                _palette = p;
        }

        public void SetPalette(AmbientMotesPalette palette) => _palette = palette;

        public void Configure(Vector3 anchorWorld) => _anchor = anchorWorld;

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            if (!_enabled) return;

            float dt = ctx.DeltaSeconds;
            if (dt > _config.MaxDeltaSeconds) dt = _config.MaxDeltaSeconds;

            int deficit = _targetAlive - _spawner.AliveParticles;
            if (deficit <= 0) { _spawnAccumulator = 0f; return; }

            _spawnAccumulator += _config.SpawnRatePerSecond * dt;
            int n = (int)_spawnAccumulator;
            if (n <= 0) return;
            _spawnAccumulator -= n;
            if (n > deficit) n = deficit;
            if (n > _config.PerTickSpawnCap) n = _config.PerTickSpawnCap;

            ParticleFlags flags = _config.UseSoftGlow ? ParticleFlags.TexturedAdd : ParticleFlags.None;
            for (int i = 0; i < n; i++)
                SpawnOne(flags);
        }

        // ===== Internals =====

        private void SpawnOne(ParticleFlags flags)
        {
            // Random point in the cylinder around the anchor.
            float r = _radius * MathF.Sqrt((float)_rng.NextDouble());
            float a = (float)_rng.NextDouble() * MathHelper.TwoPi;
            float dx = MathF.Cos(a) * r;
            float dz = MathF.Sin(a) * r;
            float dy = _config.MinHeight + (float)_rng.NextDouble() * (_config.MaxHeight - _config.MinHeight);
            Vector3 pos = _anchor + new Vector3(dx, dy, dz);

            // Slow up-drift + small horizontal sway, baked into initial velocity.
            float swayX = ((float)_rng.NextDouble() * 2f - 1f) * _config.SwayHorizontalMax;
            float swayZ = ((float)_rng.NextDouble() * 2f - 1f) * _config.SwayHorizontalMax;
            Vector3 vel = new Vector3(swayX, _config.DriftUp, swayZ);
            // Slight gravity pulling toward zero vertical velocity so motes settle.
            Vector3 accel = new Vector3(0f, -_config.DriftUp * 0.4f, 0f);

            float life = _config.LifetimeMin + (float)_rng.NextDouble() * (_config.LifetimeMax - _config.LifetimeMin);

            // Per-mote brightness variance so the cluster doesn't look uniform.
            float bri = 0.6f + (float)_rng.NextDouble() * 0.4f;
            Color cs = new Color(
                (byte)(_palette.Start.R * bri),
                (byte)(_palette.Start.G * bri),
                (byte)(_palette.Start.B * bri),
                (byte)(_palette.Start.A * bri));

            _spawner.Spawn(pos, vel, accel,
                lifetimeSeconds: life,
                sizeStart: _config.SizeStart,
                sizeEnd: _config.SizeEnd,
                colorStart: cs,
                colorEnd: _palette.End,
                flags: flags);
        }
    }
}
