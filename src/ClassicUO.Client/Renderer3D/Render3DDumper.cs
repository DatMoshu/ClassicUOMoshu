// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — single home for "dump current state to console".
// Replaces Debug3DGump.BuildDumpString after the launcher refactor.

using System.Text;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class Render3DDumper
    {
        public static string BuildDumpString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== [3DCUO] DEBUG VALUES ====================");
            sb.AppendLine($"  World3D.Enabled            = {World3DRenderer.Enabled}");
            sb.AppendLine($"  World3D.UseIsoProjection   = {World3DRenderer.UseIsoProjection}");
            sb.AppendLine($"  World3D.DepthTestEnabled   = {World3DRenderer.DepthTestEnabled}");
            sb.AppendLine($"  World3D.Wireframe          = {World3DRenderer.Wireframe}");
            sb.AppendLine($"  World3D.MeshAlpha          = {World3DRenderer.MeshAlpha:F3}");
            sb.AppendLine($"  World3D.HeightExaggeration = {World3DRenderer.HeightExaggeration:F3}");
            sb.AppendLine($"  World3D.Disable2DWorld     = {World3DRenderer.Disable2DWorld}");
            sb.AppendLine($"  World3D.HideTestCube       = {World3DRenderer.HideTestCube}");
            sb.AppendLine($"  World3D.CameraOffset       = {World3DRenderer.CameraOffset}");
            sb.AppendLine($"  Camera.PitchDegrees        = {World3DRenderer.Camera.PitchDegrees:F2}");
            sb.AppendLine($"  Camera.YawDegrees          = {World3DRenderer.Camera.YawDegrees:F2}");
            sb.AppendLine($"  Camera.Zoom                = {World3DRenderer.Camera.Zoom:F3}");
            sb.AppendLine($"  MobileRT.Enabled           = {MobileRT3DRenderer.Enabled}");
            sb.AppendLine($"  MobileRT.RTWidth/Height    = {MobileRT3DRenderer.RTWidth} x {MobileRT3DRenderer.RTHeight}  (logical)");
            sb.AppendLine($"  MobileRT.SuperSample       = x{System.Math.Clamp(MobileRT3DRenderer.SuperSample, 1, 4)}");
            sb.AppendLine($"  MobileRT.FootMargin        = {MobileRT3DRenderer.FootMarginFromBottom}");
            sb.AppendLine($"  MobileRT.RTYAnchorOffset   = {MobileRT3DRenderer.RTYAnchorOffset}");
            sb.AppendLine($"  Player3D.Enabled           = {Player3DRenderer.Enabled}");
            sb.AppendLine($"  Player3D.ModelScale        = {Player3DRenderer.ModelScale:F2}");
            sb.AppendLine($"  Player3D.PitchDeg          = {Player3DRenderer.ModelPitchDegrees:F2}");
            sb.AppendLine($"  Player3D.YawDeg            = {Player3DRenderer.ModelYawDegrees:F2}");
            sb.AppendLine($"  Player3D.RollDeg           = {Player3DRenderer.ModelRollDegrees:F2}");
            sb.AppendLine($"  Player3D.YOffset           = {Player3DRenderer.ModelYOffset:F2}");
            sb.AppendLine($"  Player3D.AnimIndex         = {Player3DRenderer.AnimIndex}");
            sb.AppendLine($"  Player3D.AnimSpeed         = {Player3DRenderer.AnimSpeed:F2}");
            sb.AppendLine($"  Multi3D.Enabled            = {Multi3DRenderer.Enabled}");
            sb.AppendLine($"  Static3D.Enabled           = {Static3DRenderer.Enabled}");
            sb.AppendLine($"  Static3D.AlphaCutoff       = {Static3DRenderer.AlphaCutoff}");
            sb.Append(    "==============================================================");
            return sb.ToString();
        }

        public static void DumpAll()
        {
            System.Console.WriteLine(BuildDumpString());
        }
    }
}
