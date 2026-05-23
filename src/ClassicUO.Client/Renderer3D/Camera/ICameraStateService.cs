// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Read+mutate the active 3D camera's projection / orientation state. Replaces direct
    /// reads of <c>World3DRenderer.Camera.X</c> for gump and admin consumers.
    /// </summary>
    /// <remarks>
    /// <para>This is a thin forwarding contract: state-of-record stays on the legacy
    /// <c>Camera3D</c> instance hanging off <c>World3DRenderer.Camera</c> because the
    /// per-frame view+projection math (View, Projection, IsoViewProjection) is consumed by
    /// the renderer in the hot path.</para>
    ///
    /// <para>When the camera math migrates into the renderer service, this contract grows
    /// to own the state directly and the bridge is deleted.</para>
    /// </remarks>
    public interface ICameraStateService
    {
        // ----- Iso/orthographic state -----
        float PitchDegrees { get; }
        float YawDegrees { get; }
        float Zoom { get; }
        float OrthoWidthPixels { get; }
        float NearPlane { get; }
        float FarPlane { get; }

        void SetPitchDegrees(float degrees);
        void SetYawDegrees(float degrees);
        void SetZoom(float zoom);
        void SetOrthoWidthPixels(float pixels);
        void SetNearPlane(float near);
        void SetFarPlane(float far);

        // ----- Perspective state -----
        bool UsePerspective { get; }
        float FovDegrees { get; }
        float PerspectiveNear { get; }
        float PerspectiveFar { get; }
        float EyeDistance { get; }

        void SetUsePerspective(bool usePerspective);
        void SetFovDegrees(float degrees);
        void SetPerspectiveNear(float near);
        void SetPerspectiveFar(float far);
        void SetEyeDistance(float distance);

        /// <summary>Camera target world position. Read-only on the gump side; written
        /// by the renderer's per-frame target update.</summary>
        Vector3 Target { get; }
    }
}
