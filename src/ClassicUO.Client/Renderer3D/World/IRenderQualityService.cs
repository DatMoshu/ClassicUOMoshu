// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World rendering domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.World
{
    /// <summary>
    /// Gump/admin contract for the 3D world's render-quality tunables. Replaces direct
    /// reads/writes of <c>World3DRenderer.{Enabled, Wireframe, MeshAlpha, ...}</c>.
    /// </summary>
    // TODO Phase 2 (ISP): could split debug flags (Wireframe, HideTestCube, VerboseLog,
    // Disable2D*) from quality tunables (MeshAlpha, HeightExaggeration, RenderDistance*).
    // Defer until consumers naturally separate.
    public interface IRenderQualityService
    {
        bool MasterEnabled { get; }
        bool Wireframe { get; }
        bool DepthTestEnabled { get; }
        bool UseIsoProjection { get; }
        bool HideTestCube { get; }
        bool Disable2DWorld { get; }
        bool Disable2DLightingIn3D { get; }
        bool VerboseLog { get; }
        float MeshAlpha { get; }
        float HeightExaggeration { get; }
        float RenderDistanceMultiplier { get; }
        Vector3 CameraOffset { get; }

        void SetMasterEnabled(bool value);
        void SetWireframe(bool value);
        void SetDepthTestEnabled(bool value);
        void SetUseIsoProjection(bool value);
        void SetHideTestCube(bool value);
        void SetDisable2DWorld(bool value);
        void SetDisable2DLightingIn3D(bool value);
        void SetVerboseLog(bool value);
        void SetMeshAlpha(float value);
        void SetHeightExaggeration(float value);
        void SetRenderDistanceMultiplier(float value);
        void SetCameraOffset(Vector3 value);
    }
}
