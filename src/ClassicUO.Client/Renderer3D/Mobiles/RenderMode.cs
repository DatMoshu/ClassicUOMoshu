// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Render mode. Mirrors legacy <c>RenderMode</c> bit-for-bit; numeric values locked
    /// by xUnit parity theory.
    /// </summary>
    public enum RenderMode
    {
        Classic2D = 0,
        Iso3D = 1,
        Full3D = 2,
    }
}
