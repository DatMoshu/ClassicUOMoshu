// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — captures a markdown dump of all 3D-renderer state plus a
// PNG screenshot of the current frame, so an AI assistant can review what
// the player is seeing without needing live access to the running client.
//
// Output: prototypes/3DCUO/dumps/3D_<timestamp>/{info.md, screenshot.png}

using System;
using System.IO;
using System.Text;
using ClassicUO.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using World = ClassicUO.Game.World;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class InfoDumper
    {
        public static string LastDumpDir;
        public static string LastError;

        // Path resolution: try repo-style "prototypes/3DCUO/dumps", else
        // bin-relative "../../../dumps".
        private static string ResolveDumpRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "..", "..", "..", "dumps"),
                Path.Combine(baseDir, "dumps"),
            };
            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                Directory.CreateDirectory(full);
                return full;
            }
            return Path.Combine(baseDir, "dumps");
        }

        public static string Dump(GraphicsDevice gd, World world)
        {
            try
            {
                string root = ResolveDumpRoot();
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string dir = Path.Combine(root, $"3D_{ts}");
                Directory.CreateDirectory(dir);

                // ---- info.md ----
                string md = BuildMarkdown(gd, world);
                File.WriteAllText(Path.Combine(dir, "info.md"), md);

                // ---- screenshot ----
                try { CaptureScreenshot(gd, Path.Combine(dir, "screenshot.png")); }
                catch (Exception ex) { Console.WriteLine($"[3DCUO][dump] screenshot failed: {ex.Message}"); }

                LastDumpDir = dir;
                Console.WriteLine($"[3DCUO][dump] wrote dump to {dir}");
                return dir;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO][dump] {ex}");
                return null;
            }
        }

        private static void CaptureScreenshot(GraphicsDevice gd, string outPath)
        {
            int w = gd.PresentationParameters.BackBufferWidth;
            int h = gd.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            gd.GetBackBufferData(data);
            using var tex = new Texture2D(gd, w, h, false, SurfaceFormat.Color);
            tex.SetData(data);
            using var fs = File.Create(outPath);
            tex.SaveAsPng(fs, w, h);
        }

        private static string BuildMarkdown(GraphicsDevice gd, World world)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 3DCUO Runtime Dump");
            sb.AppendLine();
            sb.AppendLine($"- Generated: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            sb.AppendLine($"- Build: `{AppContext.BaseDirectory}`");
            sb.AppendLine();

            sb.AppendLine("## World / Player");
            if (world?.Player != null)
            {
                var p = world.Player;
                sb.AppendLine($"- Map: `{world.MapIndex}`");
                sb.AppendLine($"- Player tile: `({p.X}, {p.Y}, {p.Z})`");
                sb.AppendLine($"- Direction: `{p.Direction}`  Offset: `({p.Offset.X}, {p.Offset.Y}, {p.Offset.Z})`");
            }
            else sb.AppendLine("- (no player)");
            sb.AppendLine();

            sb.AppendLine("## World3DRenderer");
            sb.AppendLine($"- Heightmap enabled: `{World3DRenderer.Enabled}`  Iso projection: `{World3DRenderer.UseIsoProjection}`");
            sb.AppendLine($"- Depth test: `{World3DRenderer.DepthTestEnabled}`  Wireframe: `{World3DRenderer.Wireframe}`  Hide test cube: `{World3DRenderer.HideTestCube}`");
            sb.AppendLine($"- Verbose log: `{World3DRenderer.VerboseLog}`  Mesh alpha: `{World3DRenderer.MeshAlpha:F2}`  Height exag: `{World3DRenderer.HeightExaggeration:F2}`");
            sb.AppendLine($"- Last frame: `vis={World3DRenderer.LastVisibleChunks}` `built={World3DRenderer.LastBuiltChunks}` `drawn={World3DRenderer.LastDrawnChunks}`");
            sb.AppendLine($"- Camera target: `{World3DRenderer.LastCameraTarget}`");
            sb.AppendLine($"- Camera offset: `{World3DRenderer.CameraOffset}`");
            sb.AppendLine();

            sb.AppendLine("## Camera3D");
            var cam = World3DRenderer.Camera;
            sb.AppendLine($"- Pitch: `{cam.PitchDegrees:F1}°`  Yaw: `{cam.YawDegrees:F1}°`  Zoom: `{cam.Zoom:F2}`");
            sb.AppendLine();

            sb.AppendLine("## Player3DRenderer");
            sb.AppendLine($"- Enabled: `{Player3DRenderer.Enabled}`");
            sb.AppendLine($"- MobileRT enabled: `{MobileRT3DRenderer.Enabled}`  (when on, RT pass calls Player3DRenderer.Draw instead of World3DRenderer)");
            sb.AppendLine($"- Current state: `{Player3DRenderer.CurrentState}`  AutoStateFromMovement: `{Player3DRenderer.AutoStateFromMovement}`  Baseline: `{Player3DRenderer.BaselineState}`");
            sb.AppendLine($"- MotionStopTimeoutSec: `{Player3DRenderer.MotionStopTimeoutSec:F2}s`");
            sb.AppendLine($"- Diagnostics: `Draw calls={Player3DRenderer.CallCount}  state changes={Player3DRenderer.TotalStateChanges}  motion frames={Player3DRenderer.TotalMotionFrames}  last delta={Player3DRenderer.LastFrameDelta:F4} tiles`");
            sb.AppendLine($"- ForceWhiteMaterial: `{Player3DRenderer.ForceWhiteMaterial}`  TPoseOnly: `{Player3DRenderer.TPoseOnly}`");
            sb.AppendLine($"- Scale: `{Player3DRenderer.ModelScale:F1}`  Pitch: `{Player3DRenderer.ModelPitchDegrees:F0}°`  Yaw: `{Player3DRenderer.ModelYawDegrees:F0}°`  Roll: `{Player3DRenderer.ModelRollDegrees:F0}°`");
            sb.AppendLine($"- AutoYawFromFacing: `{Player3DRenderer.AutoYawFromFacing}`  AutoYawOffset: `{Player3DRenderer.AutoYawOffsetDegrees:F0}°`  YOffset: `{Player3DRenderer.ModelYOffset:F0}`");
            sb.AppendLine($"- Anim speed: `{Player3DRenderer.AnimSpeed:F2}x`  Hide submesh match: `'{Player3DRenderer.HideSubmeshNameMatch}'`");
            sb.AppendLine();

            sb.AppendLine("### State paths");
            foreach (var kv in Player3DRenderer.StatePaths)
            {
                string full = Path.Combine(AppContext.BaseDirectory, "Models", kv.Value);
                bool exists = File.Exists(full);
                sb.AppendLine($"- `{kv.Key}` → `{kv.Value}` {(exists ? "✓" : "MISSING")}");
            }
            sb.AppendLine();

            sb.AppendLine("### Active model");
            var m = Player3DRenderer.Model;
            if (m != null)
            {
                sb.AppendLine($"- Joints: `{m.JointNodeIndex.Length}`  Submeshes: `{m.Submeshes.Count}`  Anims: `{m.Animations.Length}`");
                sb.AppendLine($"- Bounds: min=`{Fmt(m.Bounds.Min)}` max=`{Fmt(m.Bounds.Max)}`");
                sb.AppendLine($"- Active anim: `[{m.ActiveAnim}]`  t=`{m.AnimTime:F2}s`");
                sb.AppendLine();
                sb.AppendLine("| # | Mesh | Material | Verts | Idx | Tex | Visible | Factor |");
                sb.AppendLine("|---|------|----------|-------|-----|-----|---------|--------|");
                for (int i = 0; i < m.Submeshes.Count; i++)
                {
                    var s = m.Submeshes[i];
                    sb.AppendLine($"| {i} | `{s.MeshName}` | `{s.MaterialName}` | {s.Vertices.VertexCount} | {s.IndexCount} | {(s.Texture != null ? "yes" : "no")} | {s.Visible} | ({s.BaseColorFactor.X:F2},{s.BaseColorFactor.Y:F2},{s.BaseColorFactor.Z:F2}) |");
                }
                sb.AppendLine();
                sb.AppendLine("### Animations");
                for (int i = 0; i < m.Animations.Length; i++)
                {
                    var a = m.Animations[i];
                    sb.AppendLine($"- `[{i}]` `'{a.Name}'`  duration=`{a.Duration:F2}s`  channels=`{a.Channels.Count}`");
                }
            }
            else
            {
                sb.AppendLine($"- Model not loaded. LastError: `{Player3DRenderer.LastError ?? "(none)"}`");
            }
            sb.AppendLine();

            sb.AppendLine("### Recent state-change log (oldest → newest)");
            var log = Player3DRenderer.RecentStateLog();
            if (log.Count == 0) sb.AppendLine("- (no state changes yet)");
            else
            {
                sb.AppendLine();
                sb.AppendLine("| t (sec) | from → to | UO pos | Δ pos | sec since last motion | moving? |");
                sb.AppendLine("|---------|-----------|--------|-------|-----------------------|---------|");
                foreach (var e in log)
                {
                    sb.AppendLine($"| {e.TimeSec,7:F2} | {e.From} → {e.To} | ({e.UoX:F1},{e.UoY:F1},{e.UoZ:F1}) | {e.DeltaSinceLastSample:F4} | {e.TimeSinceLastMotion:F3} | {e.IsMoving} |");
                }
            }
            sb.AppendLine();

            sb.AppendLine("## GraphicsDevice");
            sb.AppendLine($"- BackBuffer: `{gd.PresentationParameters.BackBufferWidth} x {gd.PresentationParameters.BackBufferHeight}`");
            sb.AppendLine($"- Adapter: `{GraphicsAdapter.DefaultAdapter?.Description}`");
            sb.AppendLine();

            sb.AppendLine("## Notes for AI reader");
            sb.AppendLine("- This is the 3DCUO prototype — see `prototypes/3DCUO/README.md` for goals and phase status.");
            sb.AppendLine("- Coordinate convention: `worldX = uoX * 22`, `worldY = uoZ * 4`, `worldZ = uoY * 22`.");
            sb.AppendLine("- A magenta wireframe box marker drawn at the player's 3D position is normal (debug aid).");

            return sb.ToString();
        }

        private static string Fmt(Vector3 v) => $"({v.X:F1}, {v.Y:F1}, {v.Z:F1})";
    }
}
