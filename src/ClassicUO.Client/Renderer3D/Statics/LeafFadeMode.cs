// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Distant-leaf fade strategy. Mirrors legacy <c>Static3DRenderer.LeafFadeModeT</c>
    /// bit-for-bit; numeric values locked by xUnit parity theory.
    /// </summary>
    public enum LeafFadeMode
    {
        Cull = 0,
        Scale = 1,
        Fade = 2,
        ScaleThenFade = 3,
    }
}
