// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Coarse weather kind known to <see cref="ISeasonHostBridge"/>. Mirrors the legacy
    /// <c>Weather3DType</c> enum but is owned by the new domain so the season service
    /// stays decoupled from the not-yet-migrated Weather3DSystem.
    /// </summary>
    public enum SeasonWeatherKind
    {
        None = 0,
        Rain = 1,
        Snow = 2,
        Storm = 3,
        Fog = 4,
        Sandstorm = 5,
        Tornado = 6,
        Embers = 7,
        BloodMoon = 8,
        Blizzard = 9,
    }

    /// <summary>Ground material modes that the season service drives.</summary>
    public enum SeasonGroundMode
    {
        None = 0,
        Wet = 1,
        Snow = 2,
    }

    /// <summary>Foliage shader-tint modes that the season service drives.</summary>
    public enum SeasonFoliageMode
    {
        None = 0,
        Fall = 1,
    }

    /// <summary>
    /// Abstraction over the cross-system state mutations <see cref="SeasonService"/> performs
    /// on legacy static singletons (<c>TreeSeasonManager</c>, <c>World3DRenderer</c>,
    /// <c>Weather3DSystem</c>, <c>Static3DRenderer</c>, <c>TreeDefoliationStagger</c>,
    /// <c>NukeShow</c>, <c>WeatherDefaultsStore</c>). Lets the season service stay decoupled
    /// from those subsystems for testability and lets each downstream migration replace one
    /// bridge method at a time with a direct service call.
    /// </summary>
    public interface ISeasonHostBridge
    {
        // ===== Tree season manager =====
        void SetTreeSeasonState(bool enabled, bool autoFromYear, float yearProgress);

        // ===== World3DRenderer (ground + foliage + atmosphere) =====
        void SetGroundEffect(SeasonGroundMode mode, float targetIntensity);
        void SetFoliageSeason(SeasonFoliageMode mode, float intensity);
        void SetWeatherLinkSuspended(bool suspended);
        float LightningFlashIntensity { get; set; }

        // ===== Weather3DSystem =====
        SeasonWeatherKind GetWeatherType();
        float GetWeatherIntensity();
        void SetWeatherType(SeasonWeatherKind type);
        void SetWeatherIntensity(float intensity);
        void TriggerLightning();

        // ===== Static3DRenderer (tree canopy state) =====
        float LeafPlaneWindAmpDeg { get; set; }
        bool LeafPlaneWindEnabled { get; set; }
        bool DropLeavesWorldwide { get; set; }
        float LeafPresence { get; set; }

        // ===== TreeDefoliationStagger =====
        bool DefoliationStaggerEnabled { get; }
        void ConfigureDefoliationStagger(float winterT);

        // ===== NukeShow =====
        void EnableNukeShow();
        void TriggerNukeBarrage();

        // ===== WeatherDefaultsStore =====
        /// <summary>Per-weather sway-enable override; null when nothing is pinned for this type.</summary>
        bool? GetTreeSwayOverride(SeasonWeatherKind type);
    }
}
