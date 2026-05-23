// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Foliage seasonal-tint mode. Mirrors legacy <c>World3DRenderer.FoliageSeasonMode</c>
    /// bit-for-bit; numeric values locked by xUnit parity theory.
    /// </summary>
    public enum FoliageSeasonMode
    {
        None = 0,
        Fall = 1,
    }
}
