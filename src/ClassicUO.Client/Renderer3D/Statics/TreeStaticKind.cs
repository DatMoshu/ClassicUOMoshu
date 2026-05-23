// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Tree-static classification owned by the Statics domain. Replaces the legacy
    /// <c>TreeRegistryKind</c> enum bit-for-bit so casts round-trip during the transitional
    /// period (locked by parity test).
    /// </summary>
    public enum TreeStaticKind : byte
    {
        Unknown = 0,
        WholeTree = 1,
        TrunkOnly = 2,
        LeafOverlay = 3,
        Bush = 4,
    }
}
