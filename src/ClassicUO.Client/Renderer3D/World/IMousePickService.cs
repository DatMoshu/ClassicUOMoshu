// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D World service (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Converts a screen-space mouse position into UO tile coordinates by unprojecting
    /// through the camera's last view+projection and intersecting a horizontal ground
    /// plane at the player's Z. Replaces the legacy iso unprojection in
    /// <c>Camera.MouseToWorldPosition()</c> for any non-iso (perspective / free-fly /
    /// cinematic) 3D camera mode.
    /// </summary>
    public interface IMousePickService
    {
        /// <summary>
        /// Try to pick the ground tile under the supplied mouse coordinate.
        /// Returns <c>false</c> if the camera matrices are not yet available, the
        /// pick ray is parallel to the plane, or the plane is behind the camera.
        /// </summary>
        /// <param name="viewportX">Viewport origin X in screen pixels.</param>
        /// <param name="viewportY">Viewport origin Y in screen pixels.</param>
        /// <param name="viewportWidth">Viewport width in screen pixels.</param>
        /// <param name="viewportHeight">Viewport height in screen pixels.</param>
        /// <param name="mouseX">Mouse X in screen pixels (top-left origin).</param>
        /// <param name="mouseY">Mouse Y in screen pixels (top-left origin).</param>
        /// <param name="playerUoZ">Player Z in UO units; defines the ground plane height.</param>
        /// <param name="tileX">Out: UO tile X under the cursor.</param>
        /// <param name="tileY">Out: UO tile Y under the cursor.</param>
        /// <param name="worldHit">Out: world-space intersection point.</param>
        bool TryPickGroundTile(
            int viewportX, int viewportY, int viewportWidth, int viewportHeight,
            int mouseX, int mouseY,
            float playerUoZ,
            out int tileX, out int tileY, out Vector3 worldHit);
    }
}
