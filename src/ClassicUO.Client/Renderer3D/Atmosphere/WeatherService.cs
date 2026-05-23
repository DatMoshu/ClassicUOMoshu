// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using System;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// State-only implementation of <see cref="IWeatherService"/>. Holds the externally
    /// visible weather state and publishes events on transitions; does NOT own the weather
    /// simulation (that lives in legacy <c>Weather3DSystem</c> until a future migration).
    /// </summary>
    /// <remarks>
    /// Allocation-free in the steady state. Not an <see cref="IFrameService"/> — there is no
    /// per-frame state to advance at this layer; the simulation that runs each frame is in
    /// the legacy class and reads from this service.
    /// </remarks>
    public sealed class WeatherService : IWeatherService
    {
        private readonly WeatherServiceConfig _config;
        private readonly IRendererEventBus _bus;

        private WeatherKind _type;
        private float _intensity;
        private float _windX;
        private float _windZ;
        private float _radius;
        private float _height;
        private bool _autoLightning;
        private bool _occlusionCheckEnabled;
        private bool _applyProfileOnSetType;
        private float _lightningEveryMin;
        private float _lightningEveryMax;
        private Vector3 _anchor;

        public WeatherService(WeatherServiceConfig config, IRendererEventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));

            _type = _config.InitialType;
            _intensity = MathHelper.Clamp(_config.InitialIntensity, 0f, 1f);
            _radius = MathF.Max(0f, _config.Radius);
            _height = MathF.Max(0f, _config.Height);
            _autoLightning = _config.InitialAutoLightning;
            _occlusionCheckEnabled = _config.InitialOcclusionCheckEnabled;
            _applyProfileOnSetType = _config.InitialApplyProfileOnSetType;
            _lightningEveryMin = MathF.Max(0f, _config.LightningEveryMin);
            _lightningEveryMax = MathF.Max(0f, _config.LightningEveryMax);
        }

        // ===== IWeatherService — read =====

        public WeatherKind Type => _type;
        public float Intensity => _intensity;
        public float WindX => _windX;
        public float WindZ => _windZ;
        public float Radius => _radius;
        public float Height => _height;
        public bool AutoLightning => _autoLightning;
        public bool OcclusionCheckEnabled => _occlusionCheckEnabled;
        public bool ApplyProfileOnSetType => _applyProfileOnSetType;
        public float LightningEveryMin => _lightningEveryMin;
        public float LightningEveryMax => _lightningEveryMax;
        public Vector3 Anchor => _anchor;

        // ===== IWeatherService — mutate =====

        public void SetType(WeatherKind kind)
        {
            // Legacy contract: SetType always re-applies (textures, profile, particle
            // texture binding) regardless of whether the value changed. Subscribers must
            // run on every call, so we publish unconditionally and do not gate on equality.
            WeatherKind previous = _type;
            _type = kind;
            _bus.Publish(new WeatherChangedEvent(kind, previous));
        }

        public void SetIntensity(float intensity) => _intensity = MathHelper.Clamp(intensity, 0f, 1f);
        public void SetWindX(float windX) => _windX = windX;
        public void SetWindZ(float windZ) => _windZ = windZ;
        public void SetRadius(float radius) => _radius = MathF.Max(0f, radius);
        public void SetHeight(float height) => _height = MathF.Max(0f, height);
        public void SetAutoLightning(bool autoLightning) => _autoLightning = autoLightning;
        public void SetOcclusionCheckEnabled(bool enabled) => _occlusionCheckEnabled = enabled;
        public void SetApplyProfileOnSetType(bool enabled) => _applyProfileOnSetType = enabled;
        public void SetLightningEveryMin(float seconds) => _lightningEveryMin = MathF.Max(0f, seconds);
        public void SetLightningEveryMax(float seconds) => _lightningEveryMax = MathF.Max(0f, seconds);

        public void Configure(Vector3 anchorWorld) => _anchor = anchorWorld;

        public void TriggerLightning()
        {
            _bus.Publish(new LightningStruckEvent(_anchor));
        }
    }
}
