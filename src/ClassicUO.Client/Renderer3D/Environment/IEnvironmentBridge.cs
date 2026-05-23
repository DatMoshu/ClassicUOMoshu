// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Gateway exposing legacy <c>World3DRenderer</c> background + fog + sky state. Lets
    /// <see cref="IEnvironmentService"/> stay free of the renderer's heavy Draw pipeline.
    /// </summary>
    /// <remarks>
    /// Production-side reads/writes the corresponding <c>World3DRenderer.X</c> static field
    /// on each access. When the sky+fog rendering migrates into a renderer service, this
    /// gateway is deleted and the service owns the state directly.
    /// </remarks>
    public interface IEnvironmentBridge
    {
        // ----- Background / clear color -----
        Color BackgroundColor { get; set; }
        Color GetEffectiveClearColor();

        // ----- Fog -----
        bool FogEnabled { get; set; }
        float FogStart { get; set; }
        float FogEnd { get; set; }
        Color FogColor { get; set; }

        // ----- Sky -----
        SkyboxMode SkyMode { get; set; }
        Color SkyTopColor { get; set; }
        Color SkyHorizonColor { get; set; }
        Color SkyBottomColor { get; set; }
        float SkyHorizonY { get; set; }
    }
}
