// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).
//
// D-Day style nuke barrage orchestrator. Each detonation emits seven layered particle
// systems (flash, fireball, shockwave ring, smoke ring, mushroom stem/cap, ground dust),
// triggers an ExplosionForce blast event for tree bend + leaves-off, and lights a
// FireSpread patch for the burning crater.

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Audio;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Production implementation of <see cref="INukeShowService"/>. Allocation-free in the
    /// steady state apart from the small <c>List&lt;Vector3&gt;</c> of barrage sites which
    /// is reused across triggers.
    /// </summary>
    public sealed class NukeShowService : INukeShowService, IFrameService
    {
        private readonly NukeShowServiceConfig _config;
        private readonly IParticleSpawner _spawner;
        private readonly IExplosionService _explosion;
        private readonly IFireService _fire;
        private readonly IAudioClipLibrary _audio;
        private readonly IUOWorldSoundPlayer _uoSound;
        private readonly Random _rng;

        // Tunable mutable state (UI-bound).
        private bool _enabled;
        private bool _verboseLog;
        private int _barrageCount;
        private float _barrageRadius;
        private float _stagger;
        private float _singleDistance;
        private float _nukeScale;
        private float _blastRadius;
        private bool _playDDayAudio;

        // Show runtime state.
        private Vector3 _anchor;
        private float _t;
        private int _detonatedCount;
        private float _endAt;
        private readonly List<Vector3> _sites = new(capacity: 16);

        public NukeShowService(
            NukeShowServiceConfig config,
            IParticleSpawner spawner,
            IExplosionService explosion,
            IFireService fire,
            IAudioClipLibrary audio,
            IUOWorldSoundPlayer uoSound)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _explosion = explosion ?? throw new ArgumentNullException(nameof(explosion));
            _fire = fire ?? throw new ArgumentNullException(nameof(fire));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _uoSound = uoSound ?? throw new ArgumentNullException(nameof(uoSound));

            _enabled = _config.InitialEnabled;
            _verboseLog = _config.InitialVerboseLog;
            _barrageCount = _config.InitialBarrageCount;
            _barrageRadius = _config.InitialBarrageRadius;
            _stagger = _config.InitialStagger;
            _singleDistance = _config.InitialSingleDistance;
            _nukeScale = _config.InitialNukeScale;
            _blastRadius = _config.InitialBlastRadius;
            _playDDayAudio = _config.InitialPlayDDayAudio;
            _rng = new Random(_config.RandomSeed);
        }

        // ===== INukeShowService — read =====

        public bool Enabled => _enabled;
        public bool VerboseLog => _verboseLog;
        public bool IsRunning => _enabled;
        public int BarrageCount => _barrageCount;
        public float BarrageRadius => _barrageRadius;
        public float Stagger => _stagger;
        public float SingleDistance => _singleDistance;
        public float NukeScale => _nukeScale;
        public float BlastRadius => _blastRadius;
        public bool PlayDDayAudio => _playDDayAudio;
        public int RemainingDetonations => Math.Max(0, _sites.Count - _detonatedCount);
        public float CurrentTime => _t;
        public Vector3 Anchor => _anchor;

        // ===== INukeShowService — mutate =====

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetVerboseLog(bool verbose) => _verboseLog = verbose;
        public void SetBarrageCount(int count) => _barrageCount = Math.Max(1, count);
        public void SetBarrageRadius(float radius) => _barrageRadius = MathF.Max(0f, radius);
        public void SetStagger(float seconds) => _stagger = MathF.Max(0.01f, seconds);
        public void SetNukeScale(float scale) => _nukeScale = MathF.Max(0.01f, scale);
        public void SetBlastRadius(float radius) => _blastRadius = MathF.Max(0f, radius);
        public void SetPlayDDayAudio(bool play) => _playDDayAudio = play;

        public void Configure(Vector3 anchorWorld) => _anchor = anchorWorld;

        public void TriggerSingle()
        {
            BeginTrigger();
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            Vector3 site = _anchor + new Vector3(
                MathF.Sin(ang) * _singleDistance, 0f,
                MathF.Cos(ang) * _singleDistance);
            _sites.Add(site);
            _endAt = _stagger + _config.TailSeconds;
            _enabled = true;
        }

        public void TriggerBarrage()
        {
            BeginTrigger();
            int n = Math.Max(1, _barrageCount);
            for (int i = 0; i < n; i++)
                _sites.Add(PickBarrageSite(i, n));
            _endAt = n * _stagger + _config.TailSeconds;
            _enabled = true;

            if (_playDDayAudio)
                _audio.PlayOneShot(_config.DDayAudioPath, _config.DDayVolume, pitch: 0f);
        }

        public void Stop()
        {
            _enabled = false;
            _t = 0f;
            _detonatedCount = 0;
            _sites.Clear();
        }

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            if (!_enabled) return;
            float dt = ctx.DeltaSeconds;
            if (dt > _config.MaxDeltaSeconds) dt = _config.MaxDeltaSeconds;
            _t += dt;

            while (_detonatedCount < _sites.Count && _detonatedCount * _stagger <= _t)
            {
                Detonate(_sites[_detonatedCount]);
                _detonatedCount++;
            }

            if (_t >= _endAt)
            {
                _enabled = false;
                _detonatedCount = 0;
                _sites.Clear();
            }
        }

        // ===== Internals — trigger/detonate =====

        private void BeginTrigger()
        {
            _t = 0f;
            _detonatedCount = 0;
            _sites.Clear();
            _spawner.EnsureFlashTexture(_config.FlashTextureName);
        }

        private Vector3 PickBarrageSite(int i, int n)
        {
            // Even angular spacing + jitter so the ring isn't perfect.
            float baseAng = (float)i / n * MathHelper.TwoPi;
            float jitter = ((float)_rng.NextDouble() - 0.5f) * (MathHelper.TwoPi / n) * 0.4f;
            float ang = baseAng + jitter;
            float r = _barrageRadius * (0.85f + (float)_rng.NextDouble() * 0.3f);
            return _anchor + new Vector3(MathF.Sin(ang) * r, 0f, MathF.Cos(ang) * r);
        }

        private void Detonate(Vector3 ground)
        {
            // Distance-mixed UO explosion sound at the detonation tile.
            // Tile size is 22 world-units (LandMesh3D.TILE) — kept here as a constant so
            // the service stays decoupled from the legacy class.
            const int TILE = 22;
            int tx = (int)(ground.X / TILE);
            int ty = (int)(ground.Z / TILE);
            try { _uoSound.PlaySoundAtTile(_config.UoExplosionSoundId, tx, ty); }
            catch { /* audio is best-effort */ }

            EmitFlash(ground);
            EmitFireball(ground);
            EmitShockwaveRing(ground);
            EmitSmokeRing(ground);
            EmitMushroomStem(ground);
            EmitMushroomCap(ground);
            EmitGroundDust(ground);

            float r = _blastRadius * MathF.Sqrt(_nukeScale);
            _explosion.Add(ground.X, ground.Z, r, _nukeScale);
            _fire.Ignite(ground, 30f * MathF.Sqrt(_nukeScale), 12f);
        }

        // ===== Internals — emit per-layer =====

        private void EmitFlash(Vector3 ground)
        {
            float s = _nukeScale;
            // Big core flash — soft disc, very bright, very short.
            _spawner.Spawn(
                ground + new Vector3(0f, 40f * s, 0f),
                Vector3.Zero, Vector3.Zero,
                lifetimeSeconds: 0.30f,
                sizeStart: 360f * s, sizeEnd: 80f * s,
                colorStart: new Color(255, 255, 255),
                colorEnd: new Color(255, 220, 120, (byte)0),
                flags: ParticleFlags.Flash);
            // Wider, softer secondary that lingers a beat longer.
            _spawner.Spawn(
                ground + new Vector3(0f, 80f * s, 0f),
                Vector3.Zero, Vector3.Zero,
                lifetimeSeconds: 0.55f,
                sizeStart: 520f * s, sizeEnd: 200f * s,
                colorStart: new Color(255, 240, 180, (byte)200),
                colorEnd: new Color(255, 120, 60, (byte)0),
                flags: ParticleFlags.Flash);
        }

        private void EmitFireball(Vector3 ground)
        {
            float s = _nukeScale;
            int N = (int)(120 * MathF.Sqrt(s));
            Vector3 origin = ground + new Vector3(0f, 35f * s, 0f);
            for (int i = 0; i < N; i++)
                EmitFireballEmber(origin, s, i);
        }

        private void EmitFireballEmber(Vector3 origin, float s, int i)
        {
            Vector3 dir = RandUnitSphere();
            if (dir.Y < 0f) dir.Y = -dir.Y * 0.35f; // bias up
            float speed = 110f * s * (0.55f + (float)_rng.NextDouble() * 0.7f);
            float life = 1.6f + (float)_rng.NextDouble() * 1.2f;
            Color hot = (i & 3) switch
            {
                0 => new Color(255, 255, 220),
                1 => new Color(255, 220, 140),
                2 => new Color(255, 180, 90),
                _ => new Color(255, 140, 60),
            };
            Color cool = new Color(80, 30, 15, (byte)0);

            _spawner.Spawn(origin, dir * speed,
                acceleration: new Vector3(0f, 25f * s, 0f),
                lifetimeSeconds: life,
                sizeStart: 14f * s, sizeEnd: 2f * s,
                colorStart: hot, colorEnd: cool,
                flags: ParticleFlags.Trail);
        }

        private void EmitShockwaveRing(Vector3 ground)
        {
            float s = _nukeScale;
            int N = (int)(140 * MathF.Sqrt(s));
            Vector3 origin = ground + new Vector3(0f, 6f, 0f);
            float speed = 480f * s;

            // Bright leading edge — fast bright dots racing outward.
            for (int i = 0; i < N; i++)
                EmitShockwaveLeadingDot(origin, s, speed, i, N);

            // Slower follow-up ring at half speed for "pressure wave" thickness.
            for (int i = 0; i < N / 2; i++)
                EmitShockwaveFollowDot(origin, s, speed);
        }

        private void EmitShockwaveLeadingDot(Vector3 origin, float s, float speed, int i, int N)
        {
            float ang = (float)i / N * MathHelper.TwoPi + (float)_rng.NextDouble() * 0.04f;
            Vector3 dir = new Vector3(MathF.Cos(ang), 0.03f, MathF.Sin(ang));
            _spawner.Spawn(origin, dir * speed,
                acceleration: new Vector3(0f, -60f, 0f),
                lifetimeSeconds: 1.0f, sizeStart: 10f * s, sizeEnd: 2f * s,
                colorStart: new Color(255, 250, 220),
                colorEnd: new Color(220, 160, 80, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitShockwaveFollowDot(Vector3 origin, float s, float speed)
        {
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            Vector3 dir = new Vector3(MathF.Cos(ang), 0.02f, MathF.Sin(ang));
            _spawner.Spawn(origin, dir * (speed * 0.55f),
                acceleration: new Vector3(0f, -30f, 0f),
                lifetimeSeconds: 1.6f, sizeStart: 14f * s, sizeEnd: 4f * s,
                colorStart: new Color(255, 200, 130),
                colorEnd: new Color(140, 80, 40, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitSmokeRing(Vector3 ground)
        {
            float s = _nukeScale;
            int N = (int)(110 * MathF.Sqrt(s));
            Vector3 origin = ground + new Vector3(0f, 8f * s, 0f);
            float speed = 180f * s;
            for (int i = 0; i < N; i++)
                EmitSmokeRingDot(origin, s, speed, i, N);
        }

        private void EmitSmokeRingDot(Vector3 origin, float s, float speed, int i, int N)
        {
            float ang = (float)i / N * MathHelper.TwoPi + (float)_rng.NextDouble() * 0.10f;
            Vector3 dir = new Vector3(MathF.Cos(ang), 0.20f, MathF.Sin(ang));
            Vector3 vel = dir * (speed * (0.7f + (float)_rng.NextDouble() * 0.6f));
            _spawner.Spawn(origin, vel,
                acceleration: new Vector3(0f, -10f, 0f),
                lifetimeSeconds: 3.5f + (float)_rng.NextDouble() * 1.2f,
                sizeStart: 24f * s, sizeEnd: 90f * s,
                colorStart: new Color(160, 140, 120, (byte)160),
                colorEnd: new Color(30, 25, 22, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitMushroomStem(Vector3 ground)
        {
            float s = _nukeScale;
            int N = (int)(60 * MathF.Sqrt(s));
            for (int i = 0; i < N; i++)
                EmitMushroomStemPuff(ground, s, i, N);
        }

        private void EmitMushroomStemPuff(Vector3 ground, float s, int i, int N)
        {
            float t01 = (float)i / N;
            Vector3 pos = ground + new Vector3(
                ((float)_rng.NextDouble() - 0.5f) * 35f * s,
                (20f + t01 * 220f) * s,
                ((float)_rng.NextDouble() - 0.5f) * 35f * s);
            Vector3 vel = new Vector3(
                ((float)_rng.NextDouble() - 0.5f) * 12f * s,
                (35f + (float)_rng.NextDouble() * 25f) * s,
                ((float)_rng.NextDouble() - 0.5f) * 12f * s);
            Color hot = ((i & 1) == 0)
                ? new Color(180, 80, 50)
                : new Color(120, 90, 80);
            Color cool = new Color(20, 10, 10, (byte)0);
            _spawner.Spawn(pos, vel,
                acceleration: new Vector3(0f, -3f, 0f),
                lifetimeSeconds: 3.6f + (float)_rng.NextDouble() * 1.2f,
                sizeStart: 34f * s, sizeEnd: 14f * s,
                colorStart: hot, colorEnd: cool,
                flags: ParticleFlags.None);
        }

        private void EmitMushroomCap(Vector3 ground)
        {
            float s = _nukeScale;
            int N = (int)(130 * MathF.Sqrt(s));
            float apex = 290f * s;
            Vector3 capCenter = ground + new Vector3(0f, apex, 0f);
            for (int i = 0; i < N; i++)
                EmitMushroomCapPuff(capCenter, s, i);
        }

        private void EmitMushroomCapPuff(Vector3 capCenter, float s, int i)
        {
            Vector3 dir = RandUnitSphere();
            // Bias to upper hemisphere + outward in horizontal plane → flattened toroid.
            dir.Y = Math.Abs(dir.Y) * 0.6f + 0.05f;
            float horizMag = MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z);
            if (horizMag > 0.001f) { dir.X *= 1.4f; dir.Z *= 1.4f; }
            float speed = 60f * s * (0.65f + (float)_rng.NextDouble() * 0.6f);
            Color hot = (i & 3) switch
            {
                0 => new Color(220, 150, 100),
                1 => new Color(180, 110, 70),
                2 => new Color(130, 80, 60),
                _ => new Color(90, 60, 50),
            };
            Vector3 jitter = new Vector3(
                ((float)_rng.NextDouble() - 0.5f) * 40f * s,
                ((float)_rng.NextDouble() - 0.5f) * 25f * s,
                ((float)_rng.NextDouble() - 0.5f) * 40f * s);
            _spawner.Spawn(capCenter + jitter, dir * speed,
                acceleration: new Vector3(0f, -6f, 0f),
                lifetimeSeconds: 4.5f + (float)_rng.NextDouble() * 1.5f,
                sizeStart: 50f * s, sizeEnd: 18f * s,
                colorStart: hot, colorEnd: new Color(15, 10, 10, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitGroundDust(Vector3 ground)
        {
            float s = _nukeScale;
            int N = (int)(90 * MathF.Sqrt(s));
            for (int i = 0; i < N; i++)
                EmitGroundDustPuff(ground, s);
        }

        private void EmitGroundDustPuff(Vector3 ground, float s)
        {
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            float r0 = (30f + (float)_rng.NextDouble() * 40f) * s;
            Vector3 pos = ground + new Vector3(
                MathF.Cos(ang) * r0,
                8f + (float)_rng.NextDouble() * 10f,
                MathF.Sin(ang) * r0);
            Vector3 vel = new Vector3(
                MathF.Cos(ang) * (140f + (float)_rng.NextDouble() * 80f) * s,
                (10f + (float)_rng.NextDouble() * 18f) * s,
                MathF.Sin(ang) * (140f + (float)_rng.NextDouble() * 80f) * s);
            _spawner.Spawn(pos, vel,
                acceleration: new Vector3(0f, -10f, 0f),
                lifetimeSeconds: 2.6f + (float)_rng.NextDouble() * 1.0f,
                sizeStart: 36f * s, sizeEnd: 6f * s,
                colorStart: new Color(170, 140, 100),
                colorEnd: new Color(80, 60, 40, (byte)0),
                flags: ParticleFlags.None);
        }

        // Marsaglia rejection-sample on the unit sphere. Public-static for tests.
        public static Vector3 RandUnitSphere(Random rng)
        {
            double x, y, z, sq;
            do
            {
                x = rng.NextDouble() * 2 - 1;
                y = rng.NextDouble() * 2 - 1;
                z = rng.NextDouble() * 2 - 1;
                sq = x * x + y * y + z * z;
            } while (sq > 1.0 || sq < 0.0001);
            float inv = 1f / (float)Math.Sqrt(sq);
            return new Vector3((float)x * inv, (float)y * inv, (float)z * inv);
        }

        private Vector3 RandUnitSphere() => RandUnitSphere(_rng);
    }
}
