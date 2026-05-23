// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Phase 3 pass (ADR-012 §7).

namespace ClassicUO.Renderer.Passes
{
    using ClassicUO.Renderer.Core;

    /// <summary>
    /// First-in-pipeline pass (<see cref="RenderPassOrder.Sky"/> = 100). Currently a
    /// documented no-op placeholder: the active sky in the 3D world is the
    /// <c>GraphicsDevice.Clear(BackgroundColor)</c> issued upstream of the pipeline by
    /// the GameScene draw flow. A real skybox / skydome implementation will move here
    /// in a follow-up session.
    /// </summary>
    /// <remarks>
    /// This pass exists so the seven-pass architecture documented in ADR-012 §7 has
    /// a concrete first member in the codebase, anchoring the
    /// <see cref="Renderer3D.Core.RenderPassPipeline"/> file layout.
    /// </remarks>
    internal sealed class SkyPass : IRenderPass
    {
        public string Name => "Sky";
        public int Order => RenderPassOrder.Sky;

        /// <summary>
        /// Always returns false — the pass is intentionally inert until a real sky
        /// renderer lands. Implementations that flip this to true must accompany the
        /// change with a pipeline-execute call from GameScene; see playbook lesson §S.
        /// </summary>
        public bool IsEnabled => false;

        public void Execute(in RenderPassContext ctx)
        {
            // Intentional no-op. See class summary.
        }
    }
}
