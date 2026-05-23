// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Provides the current frame's visible-tree spawn anchors to <see cref="LeafFallService"/>.
    /// Production-side wraps <c>Static3DRenderer.LastTreeAnchors</c>; tests use a fake.
    /// </summary>
    public interface ILeafSpawnSource
    {
        /// <summary>
        /// Snapshot of visible tree anchors this frame. Empty list = no trees visible
        /// (the service falls back to a player-anchored spawn disc).
        /// </summary>
        IReadOnlyList<LeafSpawnAnchor> GetVisibleAnchors();
    }
}
