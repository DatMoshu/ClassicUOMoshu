// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World rendering domain (ADR-012).

using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="IRenderQualityService"/>.
    /// </summary>
    public sealed class RenderQualityService : IRenderQualityService
    {
        private readonly IRenderQualityBridge _bridge;

        public RenderQualityService(IRenderQualityBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool MasterEnabled => _bridge.MasterEnabled;
        public bool Wireframe => _bridge.Wireframe;
        public bool DepthTestEnabled => _bridge.DepthTestEnabled;
        public bool UseIsoProjection => _bridge.UseIsoProjection;
        public bool HideTestCube => _bridge.HideTestCube;
        public bool Disable2DWorld => _bridge.Disable2DWorld;
        public bool Disable2DLightingIn3D => _bridge.Disable2DLightingIn3D;
        public bool VerboseLog => _bridge.VerboseLog;
        public float MeshAlpha => _bridge.MeshAlpha;
        public float HeightExaggeration => _bridge.HeightExaggeration;
        public float RenderDistanceMultiplier => _bridge.RenderDistanceMultiplier;
        public Vector3 CameraOffset => _bridge.CameraOffset;

        public void SetMasterEnabled(bool value) => _bridge.MasterEnabled = value;
        public void SetWireframe(bool value) => _bridge.Wireframe = value;
        public void SetDepthTestEnabled(bool value) => _bridge.DepthTestEnabled = value;
        public void SetUseIsoProjection(bool value) => _bridge.UseIsoProjection = value;
        public void SetHideTestCube(bool value) => _bridge.HideTestCube = value;
        public void SetDisable2DWorld(bool value) => _bridge.Disable2DWorld = value;
        public void SetDisable2DLightingIn3D(bool value) => _bridge.Disable2DLightingIn3D = value;
        public void SetVerboseLog(bool value) => _bridge.VerboseLog = value;
        public void SetMeshAlpha(float value) => _bridge.MeshAlpha = value;
        public void SetHeightExaggeration(float value) => _bridge.HeightExaggeration = value;
        public void SetRenderDistanceMultiplier(float value) => _bridge.RenderDistanceMultiplier = value;
        public void SetCameraOffset(Vector3 value) => _bridge.CameraOffset = value;
    }
}
