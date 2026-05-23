// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Leaf sway phase mode. Mirrors legacy <c>Static3DRenderer.LeafSwayModeT</c>
    /// bit-for-bit; numeric values locked by xUnit parity theory.
    /// </summary>
    public enum LeafSwayMode
    {
        Uniform = 0,
        PerPlanePhase = 1,
        FirstPlaneOnly = 2,
    }
}
