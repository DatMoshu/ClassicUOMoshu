// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
// Delegates to IMousePickService via Renderer3DHost. The GraphicsDevice parameter is
// retained for source-compat with the existing GameScene call site; it's unused now
// (the camera matrices come from IRenderCameraSource).

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.WorldEnv;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IMousePickService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class MousePicker3D
    {
        public static bool TryPickGroundTile(
            GraphicsDevice gd,
            int viewportX, int viewportY, int viewportW, int viewportH,
            int mouseX, int mouseY,
            float playerUoZ,
            out int tileX, out int tileY, out Vector3 worldHit)
        {
            _ = gd; // unused — kept for source-compat with the legacy call site.
            return Renderer3DHost.Services.MousePick.TryPickGroundTile(
                viewportX, viewportY, viewportW, viewportH,
                mouseX, mouseY, playerUoZ,
                out tileX, out tileY, out worldHit);
        }
    }
}
