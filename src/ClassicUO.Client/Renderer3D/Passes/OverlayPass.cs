// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Phase 3 pass (ADR-012 §7).

namespace ClassicUO.Renderer.Passes
{
    using ClassicUO.Renderer.Core;

    /// <summary>
    /// Last-in-pipeline pass (<see cref="RenderPassOrder.Overlay"/> = 700). Reserved for
    /// HUD-on-3D and debug overlays drawn after the world is composited. Currently a
    /// documented no-op placeholder — there is no overlay drawing in
    /// <c>World3DRenderer.Draw</c> today; the slot is held so future overlay work has a
    /// well-typed home.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="SkyPass"/>: the seven-pass architecture (ADR-012 §7) requires
    /// every slot to have a concrete pass even when no logic lives there yet.
    /// </remarks>
    internal sealed class OverlayPass : IRenderPass
    {
        public string Name => "Overlay";
        public int Order => RenderPassOrder.Overlay;
        public bool IsEnabled => false;

        public void Execute(in RenderPassContext ctx)
        {
            // Intentional no-op. See class summary.
        }
    }
}
