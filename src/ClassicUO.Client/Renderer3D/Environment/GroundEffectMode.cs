// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Ground-overlay rendering mode (wet sheen, snow accumulation, off). Mirrors legacy
    /// <c>World3DRenderer.GroundEffect</c> bit-for-bit; numeric values locked by an xUnit
    /// parity theory.
    /// </summary>
    public enum GroundEffectMode
    {
        None = 0,
        Wet = 1,
        Snow = 2,
    }
}
