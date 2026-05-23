// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy World3DRenderer render-quality tunables to IRenderQualityBridge.
// Replaced when render-state moves into a dedicated render service.

using ClassicUO.Renderer.WorldEnv;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyRenderQualityBridge : IRenderQualityBridge
    {
        public bool MasterEnabled
        {
            get => World3DRenderer.Enabled;
            set => World3DRenderer.Enabled = value;
        }

        public bool Wireframe
        {
            get => World3DRenderer.Wireframe;
            set => World3DRenderer.Wireframe = value;
        }

        public bool DepthTestEnabled
        {
            get => World3DRenderer.DepthTestEnabled;
            set => World3DRenderer.DepthTestEnabled = value;
        }

        public bool UseIsoProjection
        {
            get => World3DRenderer.UseIsoProjection;
            set => World3DRenderer.UseIsoProjection = value;
        }

        public bool HideTestCube
        {
            get => World3DRenderer.HideTestCube;
            set => World3DRenderer.HideTestCube = value;
        }

        public bool Disable2DWorld
        {
            get => World3DRenderer.Disable2DWorld;
            set => World3DRenderer.Disable2DWorld = value;
        }

        public bool Disable2DLightingIn3D
        {
            get => World3DRenderer.Disable2DLightingIn3D;
            set => World3DRenderer.Disable2DLightingIn3D = value;
        }

        public bool VerboseLog
        {
            get => World3DRenderer.VerboseLog;
            set => World3DRenderer.VerboseLog = value;
        }

        public float MeshAlpha
        {
            get => World3DRenderer.MeshAlpha;
            set => World3DRenderer.MeshAlpha = value;
        }

        public float HeightExaggeration
        {
            get => World3DRenderer.HeightExaggeration;
            set => World3DRenderer.HeightExaggeration = value;
        }

        public float RenderDistanceMultiplier
        {
            get => World3DRenderer.RenderDistanceMultiplier;
            set => World3DRenderer.RenderDistanceMultiplier = value;
        }

        public Vector3 CameraOffset
        {
            get => World3DRenderer.CameraOffset;
            set => World3DRenderer.CameraOffset = value;
        }
    }
}
