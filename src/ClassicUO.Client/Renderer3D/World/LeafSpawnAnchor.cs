// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Single visible-tree spawn target consumed by <see cref="ILeafFallService"/>.
    /// Mirrors the legacy <c>Static3DRenderer.TreeAnchor</c> shape but is owned by the
    /// new domain so the leaf service stays decoupled from the unmigrated
    /// <c>Static3DRenderer</c>.
    /// </summary>
    public readonly struct LeafSpawnAnchor
    {
        /// <summary>World-space tree base position.</summary>
        public readonly Vector3 Anchor;
        /// <summary>Half-width of the canopy in world units; controls spawn jitter radius.</summary>
        public readonly float HalfWidth;
        /// <summary>Total canopy height in world units; spawn Y range is [Anchor.Y + 0.25h, Anchor.Y + h].</summary>
        public readonly float HeightPx;

        public LeafSpawnAnchor(Vector3 anchor, float halfWidth, float heightPx)
        {
            Anchor = anchor;
            HalfWidth = halfWidth;
            HeightPx = heightPx;
        }
    }
}
