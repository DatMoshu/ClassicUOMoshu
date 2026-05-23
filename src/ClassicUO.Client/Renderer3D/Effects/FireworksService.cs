// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using System;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Production implementation of <see cref="IFireworksService"/>. Hand-authored 9-event
    /// timeline drives <see cref="IParticleSpawner"/> for bursts; climax text reuses an
    /// <see cref="IParticleStringEmitter"/> for the cached glyph layout.
    /// </summary>
    public sealed class FireworksService : IFireworksService, IFrameService
    {
        private readonly FireworksServiceConfig _config;
        private readonly IParticleSpawner _spawner;
        private readonly IParticleStringEmitter _stringEmitter;
        private readonly Random _rng;
        private readonly Event[] _events;

        private bool _enabled;
        private bool _loop;
        private string _climaxText;
        private Vector3 _anchor;
        private float _t;
        private int _stage;
        private float _climaxEmitTimer;
        private string _climaxBuiltFor;
        private int _twinklePhase;

        public FireworksService(
            FireworksServiceConfig config,
            IParticleSpawner spawner,
            IParticleStringEmitter stringEmitter)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _stringEmitter = stringEmitter ?? throw new ArgumentNullException(nameof(stringEmitter));

            _enabled = _config.InitialEnabled;
            _loop = _config.InitialLoop;
            _climaxText = _config.InitialClimaxText ?? string.Empty;
            _rng = new Random(_config.RandomSeed);
            _events = BuildSchedule();
        }

        // ===== IFireworksService =====

        public bool Enabled => _enabled;
        public bool Loop => _loop;
        public bool IsRunning => _enabled && _t < _config.ShowDurationSeconds;
        public float CurrentTime => _t;
        public string ClimaxText => _climaxText;

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetLoop(bool loop) => _loop = loop;

        public void SetClimaxText(string text)
        {
            _climaxText = text ?? string.Empty;
            _stringEmitter.InvalidateLayout();
            _climaxBuiltFor = null;
        }

        public void Configure(Vector3 anchorWorld) => _anchor = anchorWorld;

        public void Trigger()
        {
            _enabled = true;
            _t = 0f;
            _stage = 0;
            _climaxEmitTimer = 0f;
        }

        public void Stop()
        {
            _enabled = false;
            _t = 0f;
            _stage = 0;
        }

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            if (!_enabled) return;

            float dt = ctx.DeltaSeconds;
            if (dt > _config.MaxDeltaSeconds) dt = _config.MaxDeltaSeconds;

            float prev = _t;
            _t += dt;

            // Fire scheduled events whose threshold we just crossed.
            while (_stage < _events.Length && _events[_stage].Time <= _t)
            {
                _events[_stage].Run(this, _anchor);
                _stage++;
            }

            // Climax phase: continuously re-emit the text constellation.
            if (_t >= _config.ClimaxStartSeconds && _t <= _config.ClimaxEndSeconds)
                TickClimax(dt);

            // End / loop.
            if (prev < _config.ShowDurationSeconds && _t >= _config.ShowDurationSeconds)
                HandleShowEnd();
        }

        private void HandleShowEnd()
        {
            if (_loop)
            {
                _t = 0f;
                _stage = 0;
                _climaxEmitTimer = 0f;
            }
            else
            {
                _enabled = false;
            }
        }

        private void TickClimax(float dt)
        {
            _climaxEmitTimer -= dt;
            if (_climaxEmitTimer > 0f) return;
            EmitClimaxText();
            _climaxEmitTimer = _config.ClimaxEmitIntervalSeconds;
        }

        private void EmitClimaxText()
        {
            Vector3 origin = _anchor + new Vector3(0f, _config.ClimaxYOffset, _config.ClimaxZOffset);

            // The string emitter caches its glyph layout — this _climaxBuiltFor latch is a
            // belt-and-braces: only call InvalidateLayout when the text actually changed.
            if (_climaxBuiltFor != _climaxText)
            {
                _stringEmitter.InvalidateLayout();
                _climaxBuiltFor = _climaxText;
            }

            _twinklePhase = (_twinklePhase + 1) & 3;
            Color hot = _twinklePhase switch
            {
                0 => new Color(255, 240, 180),
                1 => new Color(255, 200, 120),
                2 => new Color(255, 255, 255),
                _ => new Color(255, 220, 100),
            };
            Color cool = new Color(hot.R, hot.G, hot.B, (byte)0);

            _stringEmitter.EmitText(
                text: _climaxText,
                origin: origin,
                colorStart: hot, colorEnd: cool,
                lifetimeSeconds: _config.ClimaxLifetimeSeconds,
                cellSize: _config.ClimaxCellSize,
                sizeStart: _config.ClimaxSizeStart,
                sizeEnd: _config.ClimaxSizeEnd);
        }

        // ===== Schedule =====

        private enum BurstPattern { Sphere, Willow, Ring, BigFinale }

        private readonly struct Event
        {
            public readonly float Time;
            public readonly Action<FireworksService, Vector3> Run;
            public Event(float t, Action<FireworksService, Vector3> r) { Time = t; Run = r; }
        }

        // Hand-authored timeline. Mirrors legacy FireworksShow._events bit-for-bit.
        private static Event[] BuildSchedule() => new Event[]
        {
            new Event(0.0f,  (s, a) => s.Launch(a, new Vector3(-100f, 0f, -50f),  BurstPattern.Sphere,    new Color(255, 60, 60))),
            new Event(1.5f,  (s, a) => s.Launch(a, new Vector3( 60f, 0f, -120f),  BurstPattern.Willow,    new Color(255, 200, 80))),
            new Event(3.0f,  (s, a) => s.Launch(a, new Vector3(-60f, 0f,  120f),  BurstPattern.Ring,      new Color(80, 160, 255))),
            new Event(4.5f,  (s, a) => s.Launch(a, new Vector3( 130f, 0f,  80f),  BurstPattern.Sphere,    new Color(80, 255, 120))),
            new Event(6.0f,  (s, a) => s.Launch(a, new Vector3(-150f, 0f, -80f),  BurstPattern.Sphere,    new Color(255, 90, 220))),
            new Event(6.4f,  (s, a) => s.Launch(a, new Vector3(  0f, 0f, -160f),  BurstPattern.Ring,      new Color(255, 255, 120))),
            new Event(6.8f,  (s, a) => s.Launch(a, new Vector3( 150f, 0f, -80f),  BurstPattern.Willow,    new Color(255, 140, 60))),
            new Event(8.0f,  (s, a) => s.Launch(a, new Vector3(  0f, 0f,   0f),   BurstPattern.BigFinale, new Color(255, 240, 200), apexHeight: 720f)),
            new Event(10.0f, (s, a) => { /* climax handled by Tick directly */ }),
        };

        // ===== Bursts =====

        private void Launch(Vector3 anchor, Vector3 offset, BurstPattern pattern, Color baseColor, float apexHeight = 480f)
        {
            Vector3 ground = anchor + offset;
            Vector3 apex = ground + new Vector3(0f, apexHeight, 0f);

            // Rocket trail — single fast-rising particle that fades.
            _spawner.Spawn(
                ground + new Vector3(0f, 8f, 0f),
                new Vector3(0f, apexHeight / 1.2f, 0f),
                new Vector3(0f, -100f, 0f),
                lifetimeSeconds: 1.2f, sizeStart: 8f, sizeEnd: 2f,
                colorStart: Color.White,
                colorEnd: new Color(baseColor.R, baseColor.G, baseColor.B, (byte)0),
                flags: ParticleFlags.Trail);

            EmitBurst(apex, pattern, baseColor);
        }

        private void EmitBurst(Vector3 center, BurstPattern pattern, Color baseColor)
        {
            switch (pattern)
            {
                case BurstPattern.Sphere:
                    EmitSphere(center, baseColor, particles: 90, speed: 140f, life: 1.6f);
                    break;
                case BurstPattern.Willow:
                    EmitWillow(center, baseColor, particles: 80, speed: 100f, life: 2.4f);
                    break;
                case BurstPattern.Ring:
                    EmitRing(center, baseColor, particles: 60, speed: 160f, life: 1.4f);
                    break;
                case BurstPattern.BigFinale:
                    EmitSphere(center, new Color(255, 255, 200), particles: 140, speed: 180f, life: 2.0f);
                    EmitSphere(center, new Color(255, 120, 40),  particles: 100, speed: 110f, life: 1.6f);
                    EmitRing(center,   new Color(255, 255, 255), particles: 80,  speed: 240f, life: 1.0f);
                    break;
            }
        }

        private void EmitSphere(Vector3 center, Color color, int particles, float speed, float life)
        {
            Color fade = new Color(color.R, color.G, color.B, (byte)0);
            for (int i = 0; i < particles; i++)
            {
                Vector3 dir = RandUnitSphere();
                float vmag = speed * (0.7f + (float)_rng.NextDouble() * 0.5f);
                _spawner.Spawn(
                    center, dir * vmag,
                    acceleration: new Vector3(0f, -120f, 0f),
                    lifetimeSeconds: life * (0.7f + (float)_rng.NextDouble() * 0.5f),
                    sizeStart: 6f, sizeEnd: 1f,
                    colorStart: Color.White, colorEnd: fade,
                    flags: ParticleFlags.Trail);
            }
        }

        private void EmitWillow(Vector3 center, Color color, int particles, float speed, float life)
        {
            // Slower, drooping — strong gravity, color biased to base hue.
            Color fade = new Color(color.R, color.G, color.B, (byte)0);
            for (int i = 0; i < particles; i++)
            {
                Vector3 dir = RandUnitSphere();
                if (dir.Y < 0) dir.Y = -dir.Y * 0.4f; // restrict to upper hemisphere
                float vmag = speed * (0.6f + (float)_rng.NextDouble() * 0.5f);
                _spawner.Spawn(
                    center, dir * vmag,
                    acceleration: new Vector3(0f, -260f, 0f), // strong gravity = droop
                    lifetimeSeconds: life,
                    sizeStart: 7f, sizeEnd: 0.5f,
                    colorStart: color, colorEnd: fade,
                    flags: ParticleFlags.Trail);
            }
        }

        private void EmitRing(Vector3 center, Color color, int particles, float speed, float life)
        {
            Color fade = new Color(color.R, color.G, color.B, (byte)0);
            for (int i = 0; i < particles; i++)
            {
                float ang = (float)i / particles * MathHelper.TwoPi;
                Vector3 dir = new Vector3(MathF.Cos(ang), 0.05f, MathF.Sin(ang));
                _spawner.Spawn(
                    center, dir * speed,
                    acceleration: new Vector3(0f, -80f, 0f),
                    lifetimeSeconds: life,
                    sizeStart: 5f, sizeEnd: 1f,
                    colorStart: Color.White, colorEnd: fade,
                    flags: ParticleFlags.None);
            }
        }

        // Marsaglia rejection sample on the unit sphere. Public-static for tests.
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
