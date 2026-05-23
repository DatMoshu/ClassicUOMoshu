// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Per-weather override record persisted to <c>weather-defaults.json</c>. Each weather
    /// kind may have one record pinning a subset of values that should beat the hardcoded
    /// defaults whenever <see cref="WeatherKind"/> is set. All fields are nullable so a
    /// record represents only the values the user explicitly pinned — unpinned fields fall
    /// through to code defaults.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy <c>WeatherDefaultsStore.ProfileDto</c>. JSON shape (property
    /// names) is preserved bit-for-bit so existing <c>weather-defaults.json</c> files load
    /// without migration. <see cref="System.Text.Json.JsonSerializer"/> serializes by
    /// property name, not by type name.
    /// </remarks>
    public class WeatherOverrideRecord
    {
        // ===== Wind =====
        public string Gust { get; set; }              // None/Steady/Variable/Storm
        public float? WindStrength { get; set; }
        public float? WindFrequency { get; set; }
        public float? GustChangeMin { get; set; }
        public float? GustChangeMax { get; set; }
        public float? GustStrengthMin { get; set; }
        public float? GustStrengthMax { get; set; }
        public float? GustDirRangeDeg { get; set; }
        public bool? WindLinkToWeather { get; set; }

        // ===== Atmosphere =====
        public int? BgR { get; set; }
        public int? BgG { get; set; }
        public int? BgB { get; set; }
        public int? FogR { get; set; }
        public int? FogG { get; set; }
        public int? FogB { get; set; }
        public float? AtmospherePulse { get; set; }

        // ===== Weather coverage =====
        public float? Intensity { get; set; }
        public float? Radius { get; set; }
        public float? Height { get; set; }

        // ===== Tree canopy =====
        public float? LeafSwayAmpDeg { get; set; }
        public bool? LeafSwayEnabled { get; set; }
        public bool? TreeSwayEnabled { get; set; }
        public int? LeafPlaneCount { get; set; }
        public float? LeafPlaneYawDeg { get; set; }
        public string LeafSwayMode { get; set; }    // Uniform/PerPlanePhase/FirstPlaneOnly
        public float? LeafSwayBobAmount { get; set; }
        public bool? LeafSwayPerTreePhase { get; set; }
        public float? LeafSwayPerTreeAmount { get; set; }
    }
}
