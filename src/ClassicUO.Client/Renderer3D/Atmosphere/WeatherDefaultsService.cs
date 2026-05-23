// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Production implementation of <see cref="IWeatherDefaultsService"/>. State-only service
    /// (no per-frame tick). Persistence is delegated to <see cref="IWeatherDefaultsStorage"/>;
    /// legacy renderer state access goes through <see cref="IWeatherDefaultsHost"/>.
    /// </summary>
    public sealed class WeatherDefaultsService : IWeatherDefaultsService
    {
        private readonly IWeatherDefaultsStorage _storage;
        private readonly IWeatherDefaultsHost _host;
        private readonly IWindService _wind;
        private readonly IWeatherService _weather;

        private readonly Dictionary<WeatherKind, WeatherOverrideRecord> _overrides = new();
        private readonly ReadOnlyDictView _readonlyView;

        public WeatherDefaultsService(
            IWeatherDefaultsStorage storage,
            IWeatherDefaultsHost host,
            IWindService wind,
            IWeatherService weather)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _wind = wind ?? throw new ArgumentNullException(nameof(wind));
            _weather = weather ?? throw new ArgumentNullException(nameof(weather));
            _readonlyView = new ReadOnlyDictView(_overrides);
        }

        // ===== Read =====

        public IReadOnlyDictionary<WeatherKind, WeatherOverrideRecord> Overrides => _readonlyView;
        public int Count => _overrides.Count;
        public string LastSavedPath => _storage.LastSavedPath;
        public string LastError => _storage.LastError;

        // ===== Override CRUD =====

        public bool TryGetOverride(WeatherKind kind, out WeatherOverrideRecord record)
            => _overrides.TryGetValue(kind, out record);

        public void SetOverride(WeatherKind kind, WeatherOverrideRecord record)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));
            _overrides[kind] = record;
        }

        public WeatherOverrideRecord GetOrCreateOverride(WeatherKind kind)
        {
            if (!_overrides.TryGetValue(kind, out WeatherOverrideRecord rec))
            {
                rec = new WeatherOverrideRecord();
                _overrides[kind] = rec;
            }
            return rec;
        }

        public void Reset(WeatherKind kind) => _overrides.Remove(kind);
        public void ResetAll() => _overrides.Clear();

        // ===== Lookup helpers =====

        public bool? GetTreeSwayEnabled(WeatherKind kind)
            => _overrides.TryGetValue(kind, out WeatherOverrideRecord rec) ? rec.TreeSwayEnabled : null;

        // ===== Lifecycle =====

        public void CaptureCurrent(WeatherKind kind)
        {
            WeatherDefaultsLegacyState legacy = _host.ReadLegacyState();
            WeatherOverrideRecord record = new WeatherOverrideRecord
            {
                // Wind (from IWindService)
                Gust = _wind.GustMode.ToString(),
                WindStrength = _wind.Strength,
                WindFrequency = _wind.Frequency,
                WindLinkToWeather = true, // legacy default — IWindService doesn't surface this

                // Weather (from IWeatherService)
                Intensity = _weather.Intensity,
                Radius = _weather.Radius,
                Height = _weather.Height,

                // Leaf canopy (from legacy host)
                LeafSwayAmpDeg = legacy.LeafPlaneWindAmpDeg,
                LeafSwayEnabled = legacy.LeafPlaneWindEnabled,
                TreeSwayEnabled = legacy.LeafPlaneWindEnabled,
                LeafPlaneCount = legacy.LeafPlaneCount,
                LeafPlaneYawDeg = legacy.LeafPlaneYawDeg,
                LeafSwayMode = legacy.LeafSwayMode,
                LeafSwayBobAmount = legacy.LeafSwayBobAmount,
                LeafSwayPerTreePhase = legacy.LeafSwayPerTreePhase,
                LeafSwayPerTreeAmount = legacy.LeafSwayPerTreeAmount,

                // Atmosphere (from legacy host)
                BgR = legacy.BackgroundColor.R,
                BgG = legacy.BackgroundColor.G,
                BgB = legacy.BackgroundColor.B,
                FogR = legacy.FogColor.R,
                FogG = legacy.FogColor.G,
                FogB = legacy.FogColor.B,
                AtmospherePulse = legacy.AtmospherePulseAmount,
            };
            _overrides[kind] = record;
        }

        public void ApplyRuntimeOverrides(WeatherKind kind)
        {
            if (!_overrides.TryGetValue(kind, out WeatherOverrideRecord rec)) return;

            // Weather (service-side)
            if (rec.Intensity.HasValue) _weather.SetIntensity(rec.Intensity.Value);
            if (rec.Radius.HasValue) _weather.SetRadius(rec.Radius.Value);
            if (rec.Height.HasValue) _weather.SetHeight(rec.Height.Value);

            // Leaf canopy (legacy-side via host)
            _host.ApplyLeafCanopyOverrides(
                rec.LeafSwayEnabled,
                rec.LeafPlaneCount,
                rec.LeafPlaneYawDeg,
                rec.LeafSwayBobAmount,
                rec.LeafSwayPerTreePhase,
                rec.LeafSwayPerTreeAmount,
                rec.LeafSwayMode);
        }

        public bool Load()
        {
            _overrides.Clear();
            return _storage.TryLoad(_overrides);
        }

        public bool Save() => _storage.Save(_overrides);

        // ===== ReadOnlyDictView wrapper =====
        // Avoids per-call allocation by returning a single wrapper instance from the
        // Overrides property. The wrapper forwards every IReadOnlyDictionary call back
        // to the underlying mutable dict.
        private sealed class ReadOnlyDictView : IReadOnlyDictionary<WeatherKind, WeatherOverrideRecord>
        {
            private readonly Dictionary<WeatherKind, WeatherOverrideRecord> _src;
            public ReadOnlyDictView(Dictionary<WeatherKind, WeatherOverrideRecord> src) { _src = src; }

            public WeatherOverrideRecord this[WeatherKind key] => _src[key];
            public IEnumerable<WeatherKind> Keys => _src.Keys;
            public IEnumerable<WeatherOverrideRecord> Values => _src.Values;
            public int Count => _src.Count;
            public bool ContainsKey(WeatherKind key) => _src.ContainsKey(key);
            public bool TryGetValue(WeatherKind key, out WeatherOverrideRecord value) => _src.TryGetValue(key, out value);
            public IEnumerator<KeyValuePair<WeatherKind, WeatherOverrideRecord>> GetEnumerator() => _src.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _src.GetEnumerator();
        }
    }
}
