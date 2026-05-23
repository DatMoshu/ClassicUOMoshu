// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Abstraction over the leaf/tree texture cache that <see cref="TreeSeasonService"/>
    /// invalidates whenever its visual parameters change. Lets the service stay decoupled
    /// from the concrete <c>TreeTextureCache</c> static class.
    /// </summary>
    public interface ITreeSeasonCacheGateway
    {
        /// <summary>
        /// Drop every cached recolored texture. Called when a setter changes a value that
        /// affects pixel output (Season, SnowAmount, HueShift, SaturationBoost, etc.).
        /// </summary>
        void InvalidateAll();
    }
}
