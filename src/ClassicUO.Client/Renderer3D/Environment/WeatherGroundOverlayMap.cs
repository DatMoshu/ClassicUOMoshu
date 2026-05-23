// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012 Phase 4 pilot).

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Production implementation of <see cref="IWeatherGroundOverlayMap"/>. Pure-state
    /// (no events, no per-frame tick). First <see cref="EnsureLoaded"/> call delegates
    /// to the injected <see cref="IWeatherGroundOverlayMapStorage"/>.
    /// </summary>
    internal sealed class WeatherGroundOverlayMap : IWeatherGroundOverlayMap
    {
        private static readonly Dictionary<WeatherKind, WeatherGroundOverlayEntry> EmptyTable = new();

        private readonly IWeatherGroundOverlayMapStorage _storage;
        private bool _loaded;
        private IReadOnlyDictionary<WeatherKind, WeatherGroundOverlayEntry> _mappings = EmptyTable;
        private string _lastError;
        private string _configPath;

        public WeatherGroundOverlayMap(IWeatherGroundOverlayMapStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public int LoadedMappingCount => _mappings?.Count ?? 0;
        public string LastError => _lastError;
        public string ConfigPath => _configPath;

        public void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            WeatherGroundOverlayMapLoadResult r = _storage.Load();
            _configPath = r.ConfigPath;
            if (!r.Success)
            {
                _lastError = r.ErrorMessage;
                _mappings = EmptyTable;
                return;
            }
            _lastError = null;
            _mappings = r.Mappings ?? EmptyTable;
        }

        public bool TryGet(WeatherKind weather, out WeatherGroundOverlayEntry entry)
        {
            EnsureLoaded();
            return _mappings.TryGetValue(weather, out entry);
        }
    }
}
