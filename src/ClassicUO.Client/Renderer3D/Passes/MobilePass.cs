// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Phase 3 pass (ADR-012 §7).

using System;
using ClassicUO.Renderer.Renderer3D; // legacy Player3DRenderer + AttachmentRenderer + CameraModeController
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.World; // IRenderQualityService
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Passes
{
    /// <summary>
    /// Fourth pass (<see cref="RenderPassOrder.Mobile"/> = 400). Draws the player
    /// skinned mesh + attachments after static geometry, before effects. Inline-NPC
    /// drawing (the per-NPC pose-swap loop currently in <c>World3DRenderer.DrawInlineNpcs</c>)
    /// is NOT yet hosted here — it depends on the <c>SkinnedModelGlb</c> internal-sealed
    /// type held by <c>Player3DRenderer.Model</c>; the body moves into this pass when
    /// that type is promoted (playbook entry 39 escape-hatch note).
    /// </summary>
    /// <remarks>
    /// Placeholder per playbook lesson §S — <see cref="IsEnabled"/> returns <c>false</c>
    /// while <c>World3DRenderer.Draw</c> still owns the live call. Execute body is the
    /// "player + attachments" portion; pipeline.Execute switchover deletes the equivalent
    /// block from <c>World3DRenderer.Draw</c> and lifts <c>DrawInlineNpcs</c> here.
    /// </remarks>
    internal sealed class MobilePass : IRenderPass
    {
        private readonly IRenderQualityService _quality;

        public MobilePass(IRenderQualityService quality)
        {
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
        }

        public string Name => "Mobile";
        public int Order => RenderPassOrder.Mobile;
        public bool IsEnabled => true;

        public void Execute(in RenderPassContext ctx)
        {
            if (ctx.Graphics is null || ctx.Camera is null) return;
            if (CameraModeController.ShouldHidePlayerThisFrame) return;

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

            Player3DRenderer.Draw(
                ctx.Graphics, ctx.PlayerUoX, ctx.PlayerUoY, ctx.PlayerUoZ,
                ctx.PlayerFacing, view, proj);

            // Attachment pass — MUST run after Player3DRenderer so per-bone animated
            // matrices on Player3DRenderer.Model are up to date before AttachmentRenderer
            // reads them.
            AttachmentRenderer.Draw(ctx.Graphics, view, proj);

            // Inline-NPC draw stays in World3DRenderer.DrawInlineNpcs for now — it
            // touches Player3DRenderer.Model (internal-sealed SkinnedModelGlb).
            // Move here once Model is promoted (playbook entry 39).
        }
    }
}
