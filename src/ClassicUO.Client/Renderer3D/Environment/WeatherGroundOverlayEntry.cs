// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012 Phase 4 pilot).

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// One row of the weather → ground overlay mapping table. <see cref="Mode"/> is the
    /// target ground-effect mode (Wet / Snow) and <see cref="StrengthMultiplier"/>
    /// scales the link strength when the weather is active.
    /// </summary>
    /// <remarks>
    /// A multiplier of 1.0 preserves pre-Phase-4 behavior verbatim; future content can
    /// dial down e.g. a "drizzle" entry to 0.4 without touching code.
    /// </remarks>
    internal readonly struct WeatherGroundOverlayEntry
    {
        public readonly GroundEffectMode Mode;
        public readonly float StrengthMultiplier;

        public WeatherGroundOverlayEntry(GroundEffectMode mode, float strengthMultiplier)
        {
            Mode = mode;
            StrengthMultiplier = strengthMultiplier;
        }
    }
}
