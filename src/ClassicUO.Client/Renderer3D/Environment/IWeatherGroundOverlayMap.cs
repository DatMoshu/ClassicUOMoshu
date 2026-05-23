// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012 Phase 4 pilot).

using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Read-only lookup of weather → ground overlay rules. Loaded once at startup from
    /// <c>Data/renderer3d/weather-ground-overlay.json</c>; consulted each frame by
    /// <see cref="ClassicUO.Renderer.Passes.GroundOverlayPass"/> in place of the legacy
    /// hardcoded if/else block.
    /// </summary>
    /// <remarks>
    /// First data-driven coupling in ADR-012 Phase 4. The other three couplings catalogued
    /// in ADR-012 §3 (wind-profiles, weather-audio, season-profiles) follow this pattern.
    /// </remarks>
    internal interface IWeatherGroundOverlayMap
    {
        int LoadedMappingCount { get; }
        string LastError { get; }
        string ConfigPath { get; }

        /// <summary>Force load on first access; idempotent.</summary>
        void EnsureLoaded();

        /// <summary>
        /// Resolve the overlay entry for a weather kind. Returns false when the kind has
        /// no mapping — caller eases the target intensity to zero (no-overlay fade).
        /// </summary>
        bool TryGet(WeatherKind weather, out WeatherGroundOverlayEntry entry);
    }
}
