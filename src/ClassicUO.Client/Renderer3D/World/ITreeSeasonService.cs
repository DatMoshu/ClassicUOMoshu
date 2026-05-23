// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Authoritative source of seasonal tree-recoloring parameters and snow coverage.
    /// Replaces the legacy <c>TreeSeasonManager</c> static class. Setters that affect pixel
    /// output (<see cref="Season"/>, <see cref="SnowAmount"/>, <see cref="HueShiftDeg"/>,
    /// etc.) invalidate the texture cache via the gateway so the next frame rebuilds.
    /// </summary>
    /// <remarks>
    /// Subscribes to <see cref="SeasonChangedEvent"/> for diagnostic / future tracing use,
    /// but does NOT use the event to drive its own season — its season classification has
    /// different boundaries than <see cref="ISeasonService"/> (legacy quirk preserved).
    /// </remarks>
    public interface ITreeSeasonService
    {
        // ===== Read state =====

        bool Enabled { get; }
        TreeSeasonKind Season { get; }
        float YearProgress { get; }
        float SnowAmount { get; }
        float SnowLineFrac { get; }
        float HueShiftDeg { get; }
        float SaturationBoost { get; }
        bool AutoFromYear { get; }
        float FallColorSharpness { get; }

        // ===== Mutate state — every setter that affects pixel output invalidates the cache =====

        void SetEnabled(bool enabled);
        void SetSeason(TreeSeasonKind season);

        /// <summary>Set continuous year progress in [0,1]. Clamped. When <see cref="AutoFromYear"/>
        /// is true, also recomputes the derived season + snow + hue + saturation curves.</summary>
        void SetYearProgress(float yearProgress);

        void SetSnowAmount(float amount);
        void SetSnowLineFrac(float frac);
        void SetHueShiftDeg(float degrees);
        void SetSaturationBoost(float boost);
        void SetAutoFromYear(bool autoFromYear);
        void SetFallColorSharpness(float sharpness);

        /// <summary>Snap year-progress to a season's centre and replay the auto curve.</summary>
        void SnapToSeason(TreeSeasonKind season);

        /// <summary>
        /// Quantised cache key — small slider movements share a texture so the cache doesn't
        /// churn when the user drags a knob. Same packing as the legacy
        /// <c>TreeSeasonManager.QuantisedCacheToken</c>.
        /// </summary>
        int QuantisedCacheToken();
    }
}
