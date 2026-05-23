// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Per-graphic tree classification record. Mirrors the legacy
    /// <c>TreeRegistryEntry</c> field-for-field.
    /// </summary>
    public struct TreeStaticEntry
    {
        public TreeStaticKind Kind;
        public string Name;
        public string TreeType;

        /// <summary>Companion graphic ID for paired records, 0 if none.</summary>
        public int PairWith;

        /// <summary>Tile-data height for the static.</summary>
        public int TdHeight;

        /// <summary>True if the canopy drops in winter (deciduous); false for evergreens.</summary>
        public bool Deciduous;

        /// <summary>Peak-autumn HSV hue shift in degrees (negative = warmer).</summary>
        public float FallHueDeg;

        /// <summary>Peak-autumn saturation multiplier.</summary>
        public float FallSatBoost;
    }
}
