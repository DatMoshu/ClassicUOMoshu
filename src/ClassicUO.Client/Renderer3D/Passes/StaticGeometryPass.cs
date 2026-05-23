// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Phase 3 pass (ADR-012 §7).

using System;
using ClassicUO.Renderer.Renderer3D; // legacy Multi3DRenderer + Static3DRenderer
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.WorldEnv; // IRenderQualityService
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Passes
{
    /// <summary>
    /// Third pass (<see cref="RenderPassOrder.StaticGeometry"/> = 300). Draws multis
    /// (houses / structures) before statics (trees / rocks / billboards), so depth-on
    /// multis can occlude statics where applicable. Wraps the legacy
    /// <c>Multi3DRenderer.Draw</c> + <c>Static3DRenderer.Draw</c> entrypoints; their
    /// internal toggles are gated by <see cref="IMulti3DConfigService"/> and
    /// <see cref="IStatic3DConfigService"/> respectively, so this pass doesn't query
    /// them — it just forwards.
    /// </summary>
    /// <remarks>
    /// Placeholder per playbook lesson §S — <see cref="IsEnabled"/> returns <c>false</c>
    /// while <c>World3DRenderer.Draw</c> still owns the live call. The Execute body is
    /// implemented in full so switchover is a one-line flip plus a corresponding
    /// deletion in <c>World3DRenderer.Draw</c> (no logic changes during the switchover).
    /// </remarks>
    internal sealed class StaticGeometryPass : IRenderPass
    {
        private readonly IRenderQualityService _quality;

        public StaticGeometryPass(IRenderQualityService quality)
        {
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
        }

        public string Name => "StaticGeometry";
        public int Order => RenderPassOrder.StaticGeometry;
        public bool IsEnabled => true;

        public void Execute(in RenderPassContext ctx)
        {
            if (ctx.Graphics is null || ctx.Camera is null || ctx.VisibleChunks is null)
                return;

            Matrix view, proj;
            if (_quality.UseIsoProjection)
            {
                view = Matrix.Identity;
                proj = ctx.Camera.IsoViewProjection(ctx.ViewportWidth, ctx.ViewportHeight);
            }
            else
            {
                float aspect = (float)ctx.ViewportWidth / System.Math.Max(1, ctx.ViewportHeight);
                view = ctx.Camera.View;
                proj = ctx.Camera.Projection(aspect);
            }

            // Multi pass — houses / structures. Player tile coords push into the
            // legacy statics that the multi shader uses for its test-quad placement.
            Multi3DRenderer.TestQuadPlayerUoX = ctx.PlayerUoX;
            Multi3DRenderer.TestQuadPlayerUoY = ctx.PlayerUoY;
            Multi3DRenderer.TestQuadPlayerUoZ = ctx.PlayerUoZ;
            Multi3DRenderer.Draw(ctx.Graphics, ctx.VisibleChunks, view, proj);

            // Static pass — trees / rocks / decorations as depth-aware billboards.
            // Player tile coords feed the "drop leaves nearby" radius check.
            Static3DRenderer.PlayerTileX = (int)ctx.PlayerUoX;
            Static3DRenderer.PlayerTileY = (int)ctx.PlayerUoY;
            Static3DRenderer.Draw(ctx.Graphics, ctx.VisibleChunks, view, proj);
        }
    }
}
