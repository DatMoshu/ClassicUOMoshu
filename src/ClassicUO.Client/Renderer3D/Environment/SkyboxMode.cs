// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Sky-rendering mode. Mirrors legacy <c>World3DRenderer.SkyboxMode</c> bit-for-bit;
    /// numeric values locked by an xUnit parity theory.
    /// </summary>
    public enum SkyboxMode
    {
        Off = 0,
        Gradient = 1,
        Cubemap = 2,
    }
}
