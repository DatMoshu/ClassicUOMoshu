// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012 Phase 4 pilot).

using System.Collections.Generic;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Loaded weather → ground overlay map. Either side may be absent on parse failure;
    /// <see cref="WeatherGroundOverlayMap"/> degrades to an empty table (effectively a
    /// no-op pass) rather than crashing the renderer.
    /// </summary>
    internal readonly struct WeatherGroundOverlayMapLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string ConfigPath;
        public readonly IReadOnlyDictionary<WeatherKind, WeatherGroundOverlayEntry> Mappings;

        public WeatherGroundOverlayMapLoadResult(
            bool success, string errorMessage, string configPath,
            IReadOnlyDictionary<WeatherKind, WeatherGroundOverlayEntry> mappings)
        {
            Success = success;
            ErrorMessage = errorMessage;
            ConfigPath = configPath;
            Mappings = mappings;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="WeatherGroundOverlayMap"/>. Production reads
    /// <c>Data/renderer3d/weather-ground-overlay.json</c>; tests pre-seed an in-memory dict.
    /// Mirrors the storage-gateway pattern established by session-19 Iris2 + session-65 Roof.
    /// </summary>
    internal interface IWeatherGroundOverlayMapStorage
    {
        WeatherGroundOverlayMapLoadResult Load();
    }
}
