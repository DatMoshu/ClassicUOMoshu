// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World rendering domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Gateway exposing legacy <c>World3DRenderer</c> render-quality tunables. Read+write.
    /// State-of-record stays on the legacy class because the per-frame Draw() reads these
    /// in the hot path.
    /// </summary>
    public interface IRenderQualityBridge
    {
        bool MasterEnabled { get; set; }
        bool Wireframe { get; set; }
        bool DepthTestEnabled { get; set; }
        bool UseIsoProjection { get; set; }
        bool HideTestCube { get; set; }
        bool Disable2DWorld { get; set; }
        bool Disable2DLightingIn3D { get; set; }
        bool VerboseLog { get; set; }
        float MeshAlpha { get; set; }
        float HeightExaggeration { get; set; }
        float RenderDistanceMultiplier { get; set; }
        Vector3 CameraOffset { get; set; }
    }
}
