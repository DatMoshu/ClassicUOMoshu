// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).
//
// Per-buff timer dictionary keyed by ulong (the buff source's stable identifier);
// each timer counts down independently so two Fire-archetype buffs each fire on their
// own schedule.

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Production implementation of <see cref="IBuffParticleService"/>. 13 emit archetypes
    /// (Fire/Ice/Holy/Curse/Poison/Lightning/Stat/Defense/Stealth/FormShift/Wind/Debuff/Default)
    /// dispatched per active buff at archetype-specific intervals.
    /// </summary>
    public sealed class BuffParticleService : IBuffParticleService, IFrameService
    {
        private readonly BuffParticleServiceConfig _config;
        private readonly IParticleSpawner _spawner;
        private readonly IActiveBuffSource _buffSource;
        private readonly IRenderModeGate _renderGate;
        private readonly Random _rng;
        private readonly Dictionary<ulong, float> _timers = new();
        private readonly HashSet<ulong> _seenThisTick = new();
        private readonly List<ulong> _evictionScratch = new();
        private readonly bool[] _archetypeEnabled = new bool[14]; // index by (int)BuffArchetype

        private bool _enabled;
        private bool _require3DMode;
        private Vector3 _anchor;
        private int _lastTickEmissions;
        private int _lastActiveCount;

        public BuffParticleService(
            BuffParticleServiceConfig config,
            IParticleSpawner spawner,
            IActiveBuffSource buffSource,
            IRenderModeGate renderGate)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _buffSource = buffSource ?? throw new ArgumentNullException(nameof(buffSource));
            _renderGate = renderGate ?? throw new ArgumentNullException(nameof(renderGate));

            _enabled = _config.InitialEnabled;
            _require3DMode = _config.InitialRequire3DMode;
            _rng = new Random(_config.RandomSeed);
            for (int i = 0; i < _archetypeEnabled.Length; i++) _archetypeEnabled[i] = true;
        }

        // ===== IBuffParticleService =====

        public bool Enabled => _enabled;
        public bool Require3DMode => _require3DMode;
        public int LastTickEmissions => _lastTickEmissions;
        public int LastActiveCount => _lastActiveCount;

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetRequire3DMode(bool require) => _require3DMode = require;

        public void SetArchetypeEnabled(BuffArchetype archetype, bool enabled)
            => _archetypeEnabled[(int)archetype] = enabled;

        public bool IsArchetypeEnabled(BuffArchetype archetype) => _archetypeEnabled[(int)archetype];

        public void Configure(Vector3 anchorWorld) => _anchor = anchorWorld;

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            _lastTickEmissions = 0;
            _lastActiveCount = 0;
            if (!_enabled) return;
            if (_require3DMode && _renderGate.Is2DOnly) return;

            IReadOnlyCollection<ActiveBuffEntry> buffs = _buffSource.GetActiveBuffs();
            if (buffs is null || buffs.Count == 0)
            {
                if (_timers.Count > 0) _timers.Clear();
                return;
            }
            _lastActiveCount = buffs.Count;

            float dt = ctx.DeltaSeconds;
            if (dt > _config.MaxDeltaSeconds) dt = _config.MaxDeltaSeconds;

            _seenThisTick.Clear();
            foreach (ActiveBuffEntry b in buffs)
            {
                _seenThisTick.Add(b.Key);
                if (b.Archetype == BuffArchetype.None) continue;
                if (!_archetypeEnabled[(int)b.Archetype]) continue;

                if (!_timers.TryGetValue(b.Key, out float t)) t = 0f;
                t -= dt;
                if (t <= 0f)
                {
                    EmitArchetype(b.Archetype, _anchor);
                    _lastTickEmissions++;
                    t = IntervalFor(b.Archetype);
                }
                _timers[b.Key] = t;
            }

            EvictDroppedBuffs();
        }

        // ===== Internals =====

        private void EvictDroppedBuffs()
        {
            if (_timers.Count <= _seenThisTick.Count) return;
            _evictionScratch.Clear();
            foreach (KeyValuePair<ulong, float> kv in _timers)
                if (!_seenThisTick.Contains(kv.Key)) _evictionScratch.Add(kv.Key);
            for (int i = 0; i < _evictionScratch.Count; i++)
                _timers.Remove(_evictionScratch[i]);
        }

        private float IntervalFor(BuffArchetype arc) => arc switch
        {
            BuffArchetype.Fire => _config.IntervalFire,
            BuffArchetype.Ice => _config.IntervalIce,
            BuffArchetype.Holy => _config.IntervalHoly,
            BuffArchetype.Curse => _config.IntervalCurse,
            BuffArchetype.Poison => _config.IntervalPoison,
            BuffArchetype.Lightning => _config.IntervalLightning,
            BuffArchetype.Stat => _config.IntervalStat,
            BuffArchetype.Defense => _config.IntervalDefense,
            BuffArchetype.Stealth => _config.IntervalStealth,
            BuffArchetype.FormShift => _config.IntervalFormShift,
            BuffArchetype.Wind => _config.IntervalWind,
            BuffArchetype.Debuff => _config.IntervalDebuff,
            _ => _config.IntervalDefault,
        };

        private void EmitArchetype(BuffArchetype arc, Vector3 anchor)
        {
            switch (arc)
            {
                case BuffArchetype.Fire:      EmitFire(anchor);      break;
                case BuffArchetype.Ice:       EmitIce(anchor);       break;
                case BuffArchetype.Holy:      EmitHoly(anchor);      break;
                case BuffArchetype.Curse:     EmitCurse(anchor);     break;
                case BuffArchetype.Poison:    EmitPoison(anchor);    break;
                case BuffArchetype.Lightning: EmitLightning(anchor); break;
                case BuffArchetype.Stat:      EmitStat(anchor);      break;
                case BuffArchetype.Defense:   EmitDefense(anchor);   break;
                case BuffArchetype.Stealth:   EmitStealth(anchor);   break;
                case BuffArchetype.FormShift: EmitFormShift(anchor); break;
                case BuffArchetype.Wind:      EmitWind(anchor);      break;
                case BuffArchetype.Debuff:    EmitDebuff(anchor);    break;
                default:                      EmitDefault(anchor);   break;
            }
        }

        private Vector3 RandRing(float radius, float yLow, float yHigh)
        {
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            float r = radius * (0.7f + 0.3f * (float)_rng.NextDouble());
            float y = MathHelper.Lerp(yLow, yHigh, (float)_rng.NextDouble());
            return new Vector3(MathF.Cos(ang) * r, y, MathF.Sin(ang) * r);
        }

        // ===== Per-archetype emit (preserved verbatim from legacy BuffParticleEffects) =====

        private void EmitFire(Vector3 anchor)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = anchor + RandRing(_config.AuraRadius, _config.BodyLow, _config.BodyMid);
                _spawner.Spawn(pos,
                    velocity: new Vector3(0f, 60f + (float)_rng.NextDouble() * 30f, 0f),
                    acceleration: new Vector3(0f, 30f, 0f),
                    lifetimeSeconds: 0.8f, sizeStart: 7f, sizeEnd: 0f,
                    colorStart: new Color(255, 220, 60),
                    colorEnd: new Color(160, 30, 0, (byte)0),
                    flags: ParticleFlags.Trail);
            }
        }

        private void EmitIce(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius, _config.BodyTop - 4, _config.BodyTop + 6);
            _spawner.Spawn(pos,
                velocity: new Vector3(0f, -30f, 0f),
                acceleration: new Vector3(0f, -15f, 0f),
                lifetimeSeconds: 1.6f, sizeStart: 5f, sizeEnd: 1f,
                colorStart: new Color(180, 230, 255),
                colorEnd: new Color(60, 120, 200, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitHoly(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius, _config.BodyLow, _config.BodyTop);
            _spawner.Spawn(pos,
                velocity: new Vector3(0f, 20f + (float)_rng.NextDouble() * 30f, 0f),
                acceleration: Vector3.Zero,
                lifetimeSeconds: 1.4f, sizeStart: 6f, sizeEnd: 1f,
                colorStart: new Color(255, 240, 160),
                colorEnd: new Color(255, 200, 80, (byte)0),
                flags: ParticleFlags.Trail);
        }

        private void EmitCurse(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius * 0.8f, _config.BodyTop, _config.BodyTop + 12);
            _spawner.Spawn(pos,
                velocity: new Vector3(((float)_rng.NextDouble() - 0.5f) * 20f, -25f, ((float)_rng.NextDouble() - 0.5f) * 20f),
                acceleration: Vector3.Zero,
                lifetimeSeconds: 1.8f, sizeStart: 12f, sizeEnd: 18f,
                colorStart: new Color(100, 20, 120, (byte)180),
                colorEnd: new Color(30, 0, 60, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitPoison(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius, _config.BodyLow, _config.BodyMid);
            _spawner.Spawn(pos,
                velocity: new Vector3(0f, 25f + (float)_rng.NextDouble() * 20f, 0f),
                acceleration: new Vector3(0f, 5f, 0f),
                lifetimeSeconds: 1.3f, sizeStart: 6f, sizeEnd: 12f,
                colorStart: new Color(80, 220, 40, (byte)200),
                colorEnd: new Color(30, 120, 20, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitLightning(Vector3 anchor)
        {
            float baseY = _config.BodyTop;
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = anchor + new Vector3(
                    ((float)_rng.NextDouble() - 0.5f) * 6f,
                    baseY + i * 12f,
                    ((float)_rng.NextDouble() - 0.5f) * 6f);
                _spawner.Spawn(pos,
                    velocity: Vector3.Zero, acceleration: Vector3.Zero,
                    lifetimeSeconds: 0.18f, sizeStart: 14f, sizeEnd: 4f,
                    colorStart: new Color(255, 255, 200),
                    colorEnd: new Color(180, 200, 255, (byte)0),
                    flags: ParticleFlags.None);
            }
        }

        private void EmitStat(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius + 4f, _config.BodyMid - 4, _config.BodyMid + 8);
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            _spawner.Spawn(pos,
                velocity: new Vector3(-MathF.Sin(ang) * 30f, 5f, MathF.Cos(ang) * 30f),
                acceleration: Vector3.Zero,
                lifetimeSeconds: 1.2f, sizeStart: 4f, sizeEnd: 0f,
                colorStart: new Color(220, 230, 255),
                colorEnd: new Color(160, 180, 220, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitDefense(Vector3 anchor)
        {
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            for (int i = 0; i < 4; i++)
            {
                float a = ang + i * MathHelper.PiOver2;
                Vector3 pos = anchor + new Vector3(
                    MathF.Cos(a) * (_config.AuraRadius + 6f),
                    _config.BodyMid,
                    MathF.Sin(a) * (_config.AuraRadius + 6f));
                _spawner.Spawn(pos,
                    velocity: Vector3.Zero, acceleration: Vector3.Zero,
                    lifetimeSeconds: 0.7f, sizeStart: 8f, sizeEnd: 14f,
                    colorStart: new Color(120, 200, 255, (byte)180),
                    colorEnd: new Color(40, 100, 200, (byte)0),
                    flags: ParticleFlags.None);
            }
        }

        private void EmitStealth(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius * 1.3f, _config.BodyLow - 4, _config.BodyLow + 6);
            _spawner.Spawn(pos,
                velocity: new Vector3(((float)_rng.NextDouble() - 0.5f) * 8f, 2f, ((float)_rng.NextDouble() - 0.5f) * 8f),
                acceleration: Vector3.Zero,
                lifetimeSeconds: 1.6f, sizeStart: 14f, sizeEnd: 22f,
                colorStart: new Color(30, 30, 60, (byte)160),
                colorEnd: new Color(0, 0, 0, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitFormShift(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius, _config.BodyLow, _config.BodyTop);
            bool green = (_rng.Next() & 1) == 0;
            Color start = green ? new Color(120, 220, 80) : new Color(180, 80, 220);
            Color end = green ? new Color(30, 120, 20, (byte)0) : new Color(60, 10, 100, (byte)0);
            _spawner.Spawn(pos,
                velocity: new Vector3(0f, 12f, 0f),
                acceleration: Vector3.Zero,
                lifetimeSeconds: 1.5f, sizeStart: 8f, sizeEnd: 16f,
                colorStart: start, colorEnd: end,
                flags: ParticleFlags.None);
        }

        private void EmitWind(Vector3 anchor)
        {
            float ang = (float)_rng.NextDouble() * MathHelper.TwoPi;
            Vector3 dir = new Vector3(MathF.Cos(ang), 0f, MathF.Sin(ang));
            Vector3 pos = anchor + dir * _config.AuraRadius + new Vector3(0f, _config.BodyMid, 0f);
            _spawner.Spawn(pos,
                velocity: dir * 220f, acceleration: Vector3.Zero,
                lifetimeSeconds: 0.35f, sizeStart: 6f, sizeEnd: 1f,
                colorStart: new Color(240, 250, 255),
                colorEnd: new Color(180, 200, 240, (byte)0),
                flags: ParticleFlags.Trail);
        }

        private void EmitDebuff(Vector3 anchor)
        {
            Vector3 pos = anchor + RandRing(_config.AuraRadius, _config.BodyTop, _config.BodyTop + 8);
            _spawner.Spawn(pos,
                velocity: new Vector3(0f, -20f, 0f),
                acceleration: Vector3.Zero,
                lifetimeSeconds: 1.5f, sizeStart: 6f, sizeEnd: 12f,
                colorStart: new Color(160, 160, 160, (byte)180),
                colorEnd: new Color(60, 60, 60, (byte)0),
                flags: ParticleFlags.None);
        }

        private void EmitDefault(Vector3 anchor)
        {
            Vector3 pos = anchor + new Vector3(0f, _config.BodyTop + 6f, 0f);
            _spawner.Spawn(pos,
                velocity: Vector3.Zero, acceleration: Vector3.Zero,
                lifetimeSeconds: 0.4f, sizeStart: 6f, sizeEnd: 1f,
                colorStart: new Color(220, 220, 220),
                colorEnd: new Color(120, 120, 120, (byte)0),
                flags: ParticleFlags.None);
        }
    }
}
