// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IWallNeighborClassifier configuration (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Configuration for <see cref="IWallNeighborClassifier"/>.
    /// </summary>
    public readonly struct WallNeighborClassifierConfig
    {
        /// <summary>
        /// Z band, in UO units, around the corner tile's Z. Cardinal neighbours whose
        /// |Z - cornerZ| exceeds this are ignored.
        /// </summary>
        /// <remarks>
        /// Sweet Dreams Inn has a stone foundation course (Z=18) below the plaster
        /// ground story (Z≈20-40); ±4 covers the typical 3-5-Z thickness lip without
        /// letting the foundation row pollute the upstairs analysis.
        /// </remarks>
        public readonly int ZTolerance;

        /// <summary>Master switch — when false, <c>Refine</c> is a pass-through.</summary>
        public readonly bool Enabled;

        public WallNeighborClassifierConfig(int zTolerance, bool enabled)
        {
            ZTolerance = zTolerance;
            Enabled = enabled;
        }

        /// <summary>Defaults that match the legacy <c>WallNeighborClassifier</c> bit-for-bit.</summary>
        public static WallNeighborClassifierConfig Default => new(zTolerance: 4, enabled: true);
    }
}
