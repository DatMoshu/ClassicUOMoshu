// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Gateway exposing the legacy <c>Camera3D</c> mutable state. Lets
    /// <see cref="ICameraStateService"/> stay free of the legacy assembly while the per-frame
    /// view+projection math + <c>World3DRenderer.Camera</c> instance ownership remain there.
    /// </summary>
    /// <remarks>
    /// Production-side reads/writes <c>World3DRenderer.Camera.X</c> on each access — the
    /// camera instance is reassignable on the legacy class so callers always see current state.
    /// </remarks>
    public interface ICameraStateBridge
    {
        // ----- Iso/orthographic state -----
        float PitchDegrees { get; set; }
        float YawDegrees { get; set; }
        float Zoom { get; set; }
        float OrthoWidthPixels { get; set; }
        float NearPlane { get; set; }
        float FarPlane { get; set; }

        // ----- Perspective state -----
        bool UsePerspective { get; set; }
        float FovDegrees { get; set; }
        float PerspectiveNear { get; set; }
        float PerspectiveFar { get; set; }
        float EyeDistance { get; set; }

        /// <summary>Camera target world position. Read-only on the gump side; written
        /// by the renderer's per-frame target update.</summary>
        Vector3 Target { get; }
    }
}
