// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using System;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Production implementation of <see cref="IWindService"/>. Drives wind oscillation,
    /// gust auto-targeting, and publishes <see cref="WindUpdatedEvent"/> per frame for
    /// downstream consumers (weather particles, foliage sway, ember drift).
    /// </summary>
    /// <remarks>
    /// Allocation-free in the steady state. Reads timing exclusively from <see cref="FrameTickContext"/>;
    /// no <c>Environment.TickCount64</c> or <c>DateTime.UtcNow</c> access (closes review finding #2).
    /// </remarks>
    public sealed class WindService : IWindService, IFrameService
    {
        private readonly WindServiceConfig _config;
        private readonly IRendererEventBus _bus;
        private readonly Random _rng;

        // Mutable state
        private float _strength;
        private float _directionDeg;
        private float _frequency;
        private float _phase;
        private float _sample;
        private WindGustMode _gustMode;
        private bool _linkToWeather;
        private float _weatherWindStrength;
        private float _gustChangeMin;
        private float _gustChangeMax;
        private float _gustStrengthMin;
        private float _gustStrengthMax;
        private float _gustDirectionRangeDeg;
        private float _gustLerpSpeed;

        // Gust auto-target state
        private float _gustTimer;
        private float _strengthTarget;
        private float _directionTarget;
        private bool _hasStrengthTarget;
        private bool _hasDirectionTarget;

        public WindService(WindServiceConfig config, IRendererEventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _rng = new Random(_config.RandomSeed);

            _strength = MathHelper.Clamp(_config.InitialStrength, 0f, 1f);
            _directionDeg = WrapDegrees(_config.InitialDirectionDeg);
            _frequency = _config.BaseFrequencyHz;
            _gustMode = WindGustMode.None;
            _linkToWeather = _config.LinkToWeather;
            _weatherWindStrength = MathF.Max(0f, _config.WeatherParticleAdvection);
            _gustChangeMin = MathF.Max(0f, _config.GustChangeMin);
            _gustChangeMax = MathF.Max(0f, _config.GustChangeMax);
            _gustStrengthMin = MathHelper.Clamp(_config.GustStrengthMin, 0f, 1f);
            _gustStrengthMax = MathHelper.Clamp(_config.GustStrengthMax, 0f, 1f);
            _gustDirectionRangeDeg = MathF.Max(0f, _config.GustDirectionRangeDeg);
            _gustLerpSpeed = MathF.Max(0f, _config.GustLerpSpeed);
        }

        // ===== IWindService — read =====

        public float Strength => _strength;
        public float DirectionDeg => _directionDeg;
        public float Frequency => _frequency;
        public float Sample => _sample;
        public float Phase => _phase;
        public WindGustMode GustMode => _gustMode;
        public bool LinkToWeather => _linkToWeather;
        public float WeatherWindStrength => _weatherWindStrength;
        public float GustChangeMin => _gustChangeMin;
        public float GustChangeMax => _gustChangeMax;
        public float GustStrengthMin => _gustStrengthMin;
        public float GustStrengthMax => _gustStrengthMax;
        public float GustDirectionRangeDeg => _gustDirectionRangeDeg;
        public float GustLerpSpeed => _gustLerpSpeed;

        public Vector2 VectorXZ
        {
            get
            {
                float r = MathHelper.ToRadians(_directionDeg);
                return new Vector2(MathF.Cos(r), MathF.Sin(r)) * _strength;
            }
        }

        public float SampleAt(float phaseOffsetRad) => MathF.Sin(_phase + phaseOffsetRad);

        // ===== IWindService — mutate =====

        public void SetStrength(float strength) => _strength = MathHelper.Clamp(strength, 0f, 1f);

        public void SetDirectionDeg(float directionDeg) => _directionDeg = WrapDegrees(directionDeg);

        public void SetFrequency(float frequencyHz) => _frequency = MathF.Max(0f, frequencyHz);

        public void SetGustMode(WindGustMode mode)
        {
            _gustMode = mode;
            _hasStrengthTarget = false;
            _hasDirectionTarget = false;
            _gustTimer = 0f;
        }

        public void SetLinkToWeather(bool linkToWeather) => _linkToWeather = linkToWeather;

        public void SetWeatherWindStrength(float advectionPerSecondSquared)
            => _weatherWindStrength = MathF.Max(0f, advectionPerSecondSquared);

        public void SetGustChangeMin(float seconds) => _gustChangeMin = MathF.Max(0f, seconds);
        public void SetGustChangeMax(float seconds) => _gustChangeMax = MathF.Max(0f, seconds);
        public void SetGustStrengthMin(float strength) => _gustStrengthMin = MathHelper.Clamp(strength, 0f, 1f);
        public void SetGustStrengthMax(float strength) => _gustStrengthMax = MathHelper.Clamp(strength, 0f, 1f);
        public void SetGustDirectionRangeDeg(float degrees) => _gustDirectionRangeDeg = MathF.Max(0f, degrees);
        public void SetGustLerpSpeed(float speed) => _gustLerpSpeed = MathF.Max(0f, speed);

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            float dt = ctx.DeltaSeconds;

            AdvancePhase(dt);
            UpdateGusts(dt);
            PublishUpdate();
        }

        // ===== Internals =====

        private void AdvancePhase(float dt)
        {
            _phase += dt * _frequency * MathHelper.TwoPi;
            if (_phase > MathHelper.TwoPi) _phase -= MathHelper.TwoPi;
            _sample = MathF.Sin(_phase);
        }

        private void UpdateGusts(float dt)
        {
            bool autoGust = _gustMode == WindGustMode.Variable || _gustMode == WindGustMode.Storm;
            if (!autoGust)
            {
                _hasStrengthTarget = false;
                _hasDirectionTarget = false;
                return;
            }

            _gustTimer -= dt;
            if (_gustTimer <= 0f || (!_hasStrengthTarget && !_hasDirectionTarget))
                PickGustTarget();

            float k = MathF.Min(1f, dt * _gustLerpSpeed);

            if (_hasStrengthTarget)
                _strength += (_strengthTarget - _strength) * k;

            if (_hasDirectionTarget)
            {
                float diffRad = MathHelper.WrapAngle(MathHelper.ToRadians(_directionTarget - _directionDeg));
                _directionDeg = WrapDegrees(_directionDeg + MathHelper.ToDegrees(diffRad) * k);
            }
        }

        private void PickGustTarget()
        {
            float minStrength, maxStrength, minPeriod, maxPeriod;

            if (_gustMode == WindGustMode.Storm)
            {
                minStrength = MathF.Max(_gustStrengthMin, _config.StormStrengthFloor);
                maxStrength = MathF.Max(_gustStrengthMax, _config.StormStrengthCeiling);
                minPeriod = MathF.Max(_config.StormCadenceFloorSeconds, _gustChangeMin * _config.StormCadenceMultiplier);
                maxPeriod = MathF.Max(minPeriod + 0.5f, _gustChangeMax * _config.StormCadenceMultiplier);
            }
            else
            {
                minStrength = _gustStrengthMin;
                maxStrength = _gustStrengthMax;
                minPeriod = _gustChangeMin;
                maxPeriod = _gustChangeMax;
            }

            _strengthTarget = MathHelper.Clamp(
                minStrength + (float)_rng.NextDouble() * (maxStrength - minStrength), 0f, 1f);
            _hasStrengthTarget = true;

            float dirOffset = ((float)_rng.NextDouble() * 2f - 1f) * _gustDirectionRangeDeg;
            _directionTarget = WrapDegrees(_directionDeg + dirOffset);
            _hasDirectionTarget = true;

            _gustTimer = minPeriod + (float)_rng.NextDouble() * (maxPeriod - minPeriod);
        }

        private void PublishUpdate()
        {
            float r = MathHelper.ToRadians(_directionDeg);
            Vector2 vec = new Vector2(MathF.Cos(r), MathF.Sin(r)) * _strength;
            _bus.Publish(new WindUpdatedEvent(vec, _strength, _directionDeg, _sample));
        }

        private static float WrapDegrees(float deg)
        {
            // Normalise to [0, 360). MathF.IEEERemainder isn't ideal here since it returns
            // a signed remainder; manual wrap is clearer and branch-cheap for typical inputs.
            float d = deg % 360f;
            if (d < 0f) d += 360f;
            return d;
        }
    }
}
