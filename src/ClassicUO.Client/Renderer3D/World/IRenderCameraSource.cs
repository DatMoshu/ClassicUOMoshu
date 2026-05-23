// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D World gateway (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Gateway exposing the most recently captured camera matrices and viewport
    /// dimensions used by the 3D world renderer. The picker — and any future
    /// systems that need to relate screen-space coordinates to world-space — read
    /// camera state through this gateway rather than reaching into a concrete
    /// renderer's static fields.
    /// </summary>
    public interface IRenderCameraSource
    {
        /// <summary>
        /// Try to obtain the last frame's view+projection and viewport dimensions.
        /// Returns <c>false</c> if no 3D frame has been drawn yet (matrices invalid).
        /// </summary>
        bool TryGetLastFrame(
            out Matrix view,
            out Matrix projection,
            out int viewportWidth,
            out int viewportHeight);
    }
}
