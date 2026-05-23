// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

using System;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Production implementation of <see cref="IWeatherAudioService"/>. Subscribes to two
    /// events: <see cref="WeatherChangedEvent"/> (triggers crossfade) and
    /// <see cref="LightningStruckEvent"/> (plays a thunder one-shot). The Tick advances
    /// crossfade volumes; no per-frame polling of weather state.
    /// </summary>
    public sealed class WeatherAudioService : IWeatherAudioService, IFrameService, IDisposable
    {
        private readonly WeatherAudioServiceConfig _config;
        private readonly IRendererEventBus _bus;
        private readonly IAudioClipLibrary _library;
        private readonly Random _rng;

        private readonly IDisposable _weatherSubscription;
        private readonly IDisposable _lightningSubscription;

        private bool _enabled;
        private float _ambientVolume;
        private float _thunderVolume;
        private bool _verboseLog;
        private WeatherKind _activeType;

        // Crossfade state — _outgoing fades from full → 0 while _incoming fades 0 → full.
        private IAudioLoopHandle _outgoing;
        private IAudioLoopHandle _incoming;
        private float _crossfadeT;

        public WeatherAudioService(
            WeatherAudioServiceConfig config,
            IRendererEventBus bus,
            IAudioClipLibrary library)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _library = library ?? throw new ArgumentNullException(nameof(library));

            _enabled = _config.InitialEnabled;
            _ambientVolume = Clamp01(_config.InitialAmbientVolume);
            _thunderVolume = Clamp01(_config.InitialThunderVolume);
            _verboseLog = _config.VerboseLog;
            _activeType = WeatherKind.None;
            _rng = new Random(_config.RandomSeed);

            _weatherSubscription = bus.Subscribe<WeatherChangedEvent>(OnWeatherChanged);
            _lightningSubscription = bus.Subscribe<LightningStruckEvent>(OnLightningStruck);
        }

        public void Dispose()
        {
            _weatherSubscription?.Dispose();
            _lightningSubscription?.Dispose();
            StopAll();
        }

        // ===== Event handlers =====

        private void OnWeatherChanged(WeatherChangedEvent evt)
        {
            if (!_enabled) { StopAll(); return; }
            // Same-type re-publish (legacy contract: SetType always re-applies) is a no-op
            // for audio — there's no point restarting a loop that's already playing.
            if (evt.Current == _activeType) return;
            StartCrossfade(evt.Current);
            _activeType = evt.Current;
        }

        private void OnLightningStruck(LightningStruckEvent evt)
        {
            if (!_enabled || _config.ThunderOneShots.Count == 0) return;
            string rel = _config.ThunderOneShots[_rng.Next(_config.ThunderOneShots.Count)];
            float pitch = ((float)_rng.NextDouble() - 0.5f) * 2f * _config.ThunderPitchJitter;
            _library.PlayOneShot(rel, _thunderVolume, pitch);
        }

        // ===== IWeatherAudioService =====

        public bool Enabled => _enabled;
        public float AmbientVolume => _ambientVolume;
        public float ThunderVolume => _thunderVolume;
        public float CrossfadeSeconds => _config.CrossfadeSeconds;
        public bool VerboseLog => _verboseLog;
        public WeatherKind ActiveType => _activeType;

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled) StopAll();
        }

        public void SetAmbientVolume(float volume) => _ambientVolume = Clamp01(volume);
        public void SetThunderVolume(float volume) => _thunderVolume = Clamp01(volume);
        public void SetVerboseLog(bool verbose) => _verboseLog = verbose;

        public void StopAll()
        {
            _outgoing?.Dispose();
            _incoming?.Dispose();
            _outgoing = null;
            _incoming = null;
            _activeType = WeatherKind.None;
        }

        public void Refresh()
        {
            WeatherKind t = _activeType;
            _activeType = WeatherKind.None;
            if (_enabled) StartCrossfade(t);
        }

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            if (_incoming == null && _outgoing == null) return;

            float period = MathF.Max(0.05f, _config.CrossfadeSeconds);
            _crossfadeT += ctx.DeltaSeconds / period;
            if (_crossfadeT > 1f) _crossfadeT = 1f;

            if (_incoming != null)
                _incoming.SetVolume(Clamp01(_crossfadeT) * _ambientVolume);

            if (_outgoing != null)
            {
                _outgoing.SetVolume(Clamp01(1f - _crossfadeT) * _ambientVolume);
                if (_crossfadeT >= 1f)
                {
                    _outgoing.Dispose();
                    _outgoing = null;
                }
            }
        }

        // ===== Internals =====

        private void StartCrossfade(WeatherKind to)
        {
            // Promote the previous outgoing (already nearly faded) and demote the incoming
            // to outgoing so we have a continuous fade path even if the user thrashes types.
            _outgoing?.Dispose();
            _outgoing = _incoming;
            _incoming = null;
            _crossfadeT = 0f;

            if (!_config.AmbientLoops.TryGetValue(to, out string rel))
            {
                // No ambient bed for this kind (None / Snow / BloodMoon / etc.) — fade out only.
                return;
            }

            _incoming = _library.StartLoop(rel, 0f);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
