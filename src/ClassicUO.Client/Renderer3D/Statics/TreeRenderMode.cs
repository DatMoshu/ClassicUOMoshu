// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Tree render strategy. Mirrors legacy <c>Static3DRenderer.TreeRenderMode</c>
    /// bit-for-bit; numeric values locked by xUnit parity theory.
    /// </summary>
    public enum TreeRenderMode
    {
        OriginalBillboard = 0,
        CrossedPlanes3D = 1,
    }
}
