// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using System;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Production implementation of <see cref="ILightingService"/>. Drives the day/night
    /// auto-cycle, sun direction calculation per Formula F2 (realtime-lighting-3d.md §F2),
    /// and profile persistence.
    /// </summary>
    /// <remarks>
    /// <para>Allocation-free in the steady state. Reads timing exclusively from
    /// <see cref="FrameTickContext"/>; closes review finding #2 for this subsystem.</para>
    ///
    /// <para>Coordinate convention: +X east, +Y up, +Z south (XNA/MonoGame). Sanity:
    /// at t=6 → pure east (sunrise); at t=12 → mostly up + south; at t=18 → pure west.</para>
    /// </remarks>
    public sealed class LightingService : ILightingService, IFrameService
    {
        private readonly LightingServiceConfig _config;
        private readonly IRendererEventBus _bus;
        private readonly ILightingProfileGateway _profile;

        private bool _enabled;
        private bool _autoCycle;
        private float _timeOfDay;
        private float _cyclePeriodSeconds;
        private bool _profileLoaded;

        // Cached so we publish a SunDirChangedEvent only when the dir actually changes
        // beyond a small tolerance — keeps subscribers (shader uniforms) from churning.
        private Vector3 _lastPublishedDir;
        private bool _hasPublished;
        private const float DirChangeEpsilon = 0.0001f;

        public LightingService(
            LightingServiceConfig config,
            IRendererEventBus bus,
            ILightingProfileGateway profile)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            _enabled = _config.InitialEnabled;
            _autoCycle = _config.InitialAutoCycle;
            _timeOfDay = MathHelper.Clamp(_config.InitialTimeOfDay, 0f, 24f);
            _cyclePeriodSeconds = MathF.Max(1f, _config.InitialCyclePeriodSeconds);
        }

        // ===== ILightingService — read =====

        public bool Enabled => _enabled;
        public bool AutoCycle => _autoCycle;
        public float TimeOfDay => _timeOfDay;
        public float CyclePeriodSeconds => _cyclePeriodSeconds;

        public Vector3 CurrentLightDir
            => _enabled ? ComputeSunDir(_timeOfDay) : _config.LegacyHardcodedDir;

        public Vector3 ComputeSunDir(float timeOfDay)
        {
            // Formula F2 from realtime-lighting-3d.md §F2.
            //   azimuth   = (t / 24) * 2π - π/2        (east at dawn, west at dusk)
            //   elevation = sin((t - 6) / 12 * π) * MaxElevation
            float az = (timeOfDay / 24f) * MathHelper.TwoPi - MathHelper.PiOver2;
            float el = MathF.Sin((timeOfDay - 6f) / 12f * MathHelper.Pi) * _config.MaxElevation;
            return Vector3.Normalize(new Vector3(MathF.Cos(az), MathF.Sin(el), MathF.Sin(az)));
        }

        // ===== ILightingService — mutate =====

        public void SetEnabled(bool enabled) => _enabled = enabled;
        public void SetAutoCycle(bool autoCycle) => _autoCycle = autoCycle;

        public void SetTimeOfDay(float timeOfDay)
        {
            float t = timeOfDay % 24f;
            if (t < 0f) t += 24f;
            _timeOfDay = t;
        }

        public void SetCyclePeriodSeconds(float seconds)
        {
            _cyclePeriodSeconds = MathF.Max(1f, seconds);
        }

        // ===== Persistence =====

        public void SaveToProfile()
        {
            if (!_profile.HasActiveProfile) return;
            _profile.WriteEnabled(_enabled);
            _profile.WriteAutoCycle(_autoCycle);
            _profile.WriteTimeOfDay(_timeOfDay);
            _profile.WriteCyclePeriodSeconds(_cyclePeriodSeconds);
        }

        public void OnProfileLoaded() => _profileLoaded = false;

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            EnsureProfileLoaded();

            if (_enabled && _autoCycle && _cyclePeriodSeconds > 0.001f)
            {
                float deltaTOD = (ctx.DeltaSeconds / _cyclePeriodSeconds) * 24f;
                float t = _timeOfDay + deltaTOD;
                while (t >= 24f) t -= 24f;
                while (t < 0f) t += 24f;
                _timeOfDay = t;
            }

            PublishIfChanged();
        }

        // ===== Internals =====

        private void EnsureProfileLoaded()
        {
            if (_profileLoaded || !_profile.HasActiveProfile) return;
            _enabled = _profile.ReadEnabled();
            _autoCycle = _profile.ReadAutoCycle();
            _timeOfDay = MathHelper.Clamp(_profile.ReadTimeOfDay(), 0f, 24f);
            _cyclePeriodSeconds = MathF.Max(1f, _profile.ReadCyclePeriodSeconds());
            _profileLoaded = true;
        }

        private void PublishIfChanged()
        {
            Vector3 dir = CurrentLightDir;
            if (_hasPublished && Vector3.DistanceSquared(dir, _lastPublishedDir) < DirChangeEpsilon * DirChangeEpsilon)
                return;
            _lastPublishedDir = dir;
            _hasPublished = true;
            _bus.Publish(new SunDirChangedEvent(dir, _timeOfDay, _enabled));
        }
    }
}
