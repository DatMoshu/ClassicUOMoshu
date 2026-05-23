// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Camera domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Camera
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="ICameraStateService"/>. Every read+write
    /// flows through the supplied <see cref="ICameraStateBridge"/>. Production-side wraps
    /// the legacy <c>World3DRenderer.Camera</c> instance.
    /// </summary>
    public sealed class CameraStateService : ICameraStateService
    {
        private readonly ICameraStateBridge _bridge;

        public CameraStateService(ICameraStateBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        // ----- Iso/orthographic state -----

        public float PitchDegrees => _bridge.PitchDegrees;
        public float YawDegrees => _bridge.YawDegrees;
        public float Zoom => _bridge.Zoom;
        public float OrthoWidthPixels => _bridge.OrthoWidthPixels;
        public float NearPlane => _bridge.NearPlane;
        public float FarPlane => _bridge.FarPlane;

        public void SetPitchDegrees(float degrees) => _bridge.PitchDegrees = degrees;
        public void SetYawDegrees(float degrees) => _bridge.YawDegrees = degrees;
        public void SetZoom(float zoom) => _bridge.Zoom = zoom;
        public void SetOrthoWidthPixels(float pixels) => _bridge.OrthoWidthPixels = pixels;
        public void SetNearPlane(float near) => _bridge.NearPlane = near;
        public void SetFarPlane(float far) => _bridge.FarPlane = far;

        // ----- Perspective state -----

        public bool UsePerspective => _bridge.UsePerspective;
        public float FovDegrees => _bridge.FovDegrees;
        public float PerspectiveNear => _bridge.PerspectiveNear;
        public float PerspectiveFar => _bridge.PerspectiveFar;
        public float EyeDistance => _bridge.EyeDistance;

        public void SetUsePerspective(bool usePerspective) => _bridge.UsePerspective = usePerspective;
        public void SetFovDegrees(float degrees) => _bridge.FovDegrees = degrees;
        public void SetPerspectiveNear(float near) => _bridge.PerspectiveNear = near;
        public void SetPerspectiveFar(float far) => _bridge.PerspectiveFar = far;
        public void SetEyeDistance(float distance) => _bridge.EyeDistance = distance;

        public Microsoft.Xna.Framework.Vector3 Target => _bridge.Target;
    }
}
