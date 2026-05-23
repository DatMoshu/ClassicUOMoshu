// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wiring ILeafSpawnSource to the legacy
// Static3DRenderer.LastTreeAnchors list.

using System.Collections.Generic;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="ILeafSpawnSource"/> backed by <c>Static3DRenderer.LastTreeAnchors</c>.
    /// Per-frame snapshot allocates a small list — this is acceptable because tree-anchor
    /// counts are small (typically &lt; 32 visible) and the allocation only fires when leaves
    /// are spawning. Future Static3DRenderer migration may expose a pooled snapshot path.
    /// </summary>
    internal sealed class LegacyLeafSpawnSource : ILeafSpawnSource
    {
        private readonly List<LeafSpawnAnchor> _scratch = new List<LeafSpawnAnchor>(32);

        public IReadOnlyList<LeafSpawnAnchor> GetVisibleAnchors()
        {
            _scratch.Clear();
            var src = Static3DRenderer.LastTreeAnchors;
            if (src == null) return _scratch;
            for (int i = 0; i < src.Count; i++)
            {
                var a = src[i];
                _scratch.Add(new LeafSpawnAnchor(a.Anchor, a.HalfWidth, a.HeightPx));
            }
            return _scratch;
        }
    }
}
