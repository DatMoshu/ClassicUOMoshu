// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy World3DRenderer.Camera (Camera3D instance) to ICameraStateBridge.
// Reads the current World3DRenderer.Camera reference on each access — the legacy
// class can reassign Camera at runtime, so caching the reference would go stale.
// Replaced when the camera math + state migrate into a renderer service.

using ClassicUO.Renderer.Camera;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyCameraStateBridge : ICameraStateBridge
    {
        // ----- Iso/orthographic state -----

        public float PitchDegrees
        {
            get => World3DRenderer.Camera.PitchDegrees;
            set => World3DRenderer.Camera.PitchDegrees = value;
        }

        public float YawDegrees
        {
            get => World3DRenderer.Camera.YawDegrees;
            set => World3DRenderer.Camera.YawDegrees = value;
        }

        public float Zoom
        {
            get => World3DRenderer.Camera.Zoom;
            set => World3DRenderer.Camera.Zoom = value;
        }

        public float OrthoWidthPixels
        {
            get => World3DRenderer.Camera.OrthoWidthPixels;
            set => World3DRenderer.Camera.OrthoWidthPixels = value;
        }

        public float NearPlane
        {
            get => World3DRenderer.Camera.NearPlane;
            set => World3DRenderer.Camera.NearPlane = value;
        }

        public float FarPlane
        {
            get => World3DRenderer.Camera.FarPlane;
            set => World3DRenderer.Camera.FarPlane = value;
        }

        // ----- Perspective state -----

        public bool UsePerspective
        {
            get => World3DRenderer.Camera.UsePerspective;
            set => World3DRenderer.Camera.UsePerspective = value;
        }

        public float FovDegrees
        {
            get => World3DRenderer.Camera.FovDegrees;
            set => World3DRenderer.Camera.FovDegrees = value;
        }

        public float PerspectiveNear
        {
            get => World3DRenderer.Camera.PerspectiveNear;
            set => World3DRenderer.Camera.PerspectiveNear = value;
        }

        public float PerspectiveFar
        {
            get => World3DRenderer.Camera.PerspectiveFar;
            set => World3DRenderer.Camera.PerspectiveFar = value;
        }

        public float EyeDistance
        {
            get => World3DRenderer.Camera.EyeDistance;
            set => World3DRenderer.Camera.EyeDistance = value;
        }

        public Microsoft.Xna.Framework.Vector3 Target => World3DRenderer.Camera.Target;
    }
}
