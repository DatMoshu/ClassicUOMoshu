// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Gateway exposing legacy <c>World3DRenderer</c> foliage-season state. The shader pass
    /// itself is consumed by Static3DRenderer hot-path code; this gateway only exposes the
    /// gump-mutable surface.
    /// </summary>
    public interface IFoliageSeasonBridge
    {
        FoliageSeasonMode Mode { get; set; }
        float Intensity { get; set; }
    }
}
