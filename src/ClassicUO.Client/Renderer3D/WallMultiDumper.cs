// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — single-multi diagnostic dump.
//
// Picks the house nearest the player, walks every Multi component, and
// reports the full pipeline state for that one structure:
//   - graphic + multi-relative offset
//   - TileData flags
//   - resolved TileOrientation (from MultiOrientationTable + fallback)
//   - manifest entry (file + archetype) from WallMeshRegistry
//   - whether the runtime actually rendered the component this frame
//     (cross-referenced against WallMeshDrawer.LastInstances)
//
// Output: console + Logs/wallmulti-dump-<timestamp>.txt
//
// The point: when we're iterating to "100% perfect" on a chosen multi
// (e.g. 0x0077 Two-Story Wood and Plaster House), this is the report we
// look at to find missed pieces, wrong rotations, and mesh gaps.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClassicUO.Game.GameObjects;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class WallMultiDumper
    {
        public static string LastReportPath;
        public static string LastJsonReportPath;
        public static int LastComponentCount;
        public static int LastMeshedCount;
        public static int LastMissingMeshCount;

        public static void DumpNearest()
        {
            var world = ClassicUO.Client.Game.UO.World;
            var player = world?.Player;
            if (world == null || player == null)
            {
                Console.WriteLine("[3DCUO] WallMultiDumper: no world/player.");
                return;
            }

            // Pick the nearest house: smallest Manhattan distance from the
            // player to the house's anchor item.
            House nearest = null;
            int nearestDist = int.MaxValue;
            uint nearestSerial = 0;
            int anchorX = 0, anchorY = 0, anchorZ = 0;
            ushort anchorGraphic = 0;
            foreach (var h in world.HouseManager.Houses)
            {
                var item = world.Items.Get(h.Serial);
                if (item == null) continue;
                int d = Math.Abs(item.X - player.X) + Math.Abs(item.Y - player.Y);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = h;
                    nearestSerial = h.Serial;
                    anchorX = item.X;
                    anchorY = item.Y;
                    anchorZ = item.Z;
                    anchorGraphic = item.Graphic;
                }
            }

            if (nearest == null)
            {
                Console.WriteLine("[3DCUO] WallMultiDumper: no houses tracked. Stand inside or next to a multi (player house, public bank, etc.) and retry.");
                return;
            }

            Dump(nearest, nearestSerial, anchorX, anchorY, anchorZ, anchorGraphic);
        }

        private static void Dump(House house, uint serial, int ax, int ay, int az, ushort anchorGraphic)
        {
            WallMeshRegistry.EnsureLoaded();
            // MultiOrientationTable lazy-loads on first Resolve() call below.

            // Build a quick lookup of "did we render this graphic at this exact
            // world tile last frame?" from WallMeshDrawer's instance list.
            // Anchor world coords (mesh space): wx + TILE*0.5, wy, az + TILE*0.5
            // for non-edge kinds; for edge kinds, the +TILE half-offsets vary.
            // For the dump we just match by (graphic, world cell rounded).
            var rendered = new HashSet<(ushort g, int cx, int cz)>();
            foreach (var inst in WallMeshDrawer.LastInstances)
            {
                int cx = (int)Math.Round(inst.AnchorWorld.X / LandMesh3D.TILE);
                int cz = (int)Math.Round(inst.AnchorWorld.Z / LandMesh3D.TILE);
                rendered.Add((inst.Graphic, cx, cz));
            }

            // Capture render-pipeline state up front so we can suppress false
            // MISSes when the user ran the dump without Full 3D enabled.
            bool worldIs3D = RenderModeController.WorldIs3D;
            bool wallMeshOn = Multi3DRenderer.Use3DWallMeshes;
            bool multiOn = Multi3DRenderer.Enabled;
            bool renderActive = worldIs3D && wallMeshOn && multiOn;

            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine($"# 3DCUO single-multi dump  {DateTime.Now:O}");
            sb.AppendLine($"# Player: ({player_X(ClassicUO.Client.Game.UO.World.Player)}, {player_Y(ClassicUO.Client.Game.UO.World.Player)}, {player_Z(ClassicUO.Client.Game.UO.World.Player)})");
            sb.AppendLine($"# House serial: 0x{serial:X8}  anchor item: graphic 0x{anchorGraphic:X4} at ({ax},{ay},{az})");
            sb.AppendLine($"# Bounds: {house.Bounds}");
            sb.AppendLine($"# Components: {house.Components.Count}");
            sb.AppendLine($"# RenderMode={RenderModeController.Mode} WorldIs3D={worldIs3D}  Multi3D.Enabled={multiOn}  Use3DWallMeshes={wallMeshOn}");
            sb.AppendLine($"# WallMeshDrawer last frame: instances={WallMeshDrawer.LastInstanceCount}");
            if (!renderActive)
            {
                sb.AppendLine();
                sb.AppendLine("# >>> 3D wall pipeline NOT ACTIVE last frame.");
                sb.AppendLine("# >>> Per-component status will report DARK (no render data) instead of");
                sb.AppendLine("# >>> ok/MISS. To get true mesh-status, enable Full 3D + Walls:3D Meshes,");
                sb.AppendLine("# >>> orbit the camera once so the wall pass runs, then re-dump.");
            }
            sb.AppendLine();

            int meshed = 0;
            int missing = 0;
            int corner = 0, cornerNW = 0, cornerNE = 0, cornerSW = 0, cornerSE = 0;
            int wallS = 0, wallE = 0, wallN = 0, wallW = 0, floor = 0, roof = 0, unknown = 0, other = 0;

            // Parallel JSON collection — same data the text dump captures, in a
            // structured form the multi-author skill's `analyze` mode can ingest.
            // See design/Core/Architecture/multi-system/04-multi-authoring-agent-spec.md.
            var jsonComponents = new List<Dictionary<string, object>>(house.Components.Count);

            sb.AppendLine("status   graphic  rel(dx,dy,dz)  abs(X,Y,Z)         resolved-kind  manifest-archetype  glb-file              flags");
            foreach (var comp in house.Components.OrderBy(c => c.MultiOffsetZ).ThenBy(c => c.MultiOffsetY).ThenBy(c => c.MultiOffsetX))
            {
                if (comp.IsDestroyed) continue;
                ref var data = ref ClassicUO.Client.Game.UO.FileManager.TileData.StaticData[comp.Graphic];
                var kind = MultiOrientationTable.Resolve(comp.Graphic, ref data);

                string archetype = "(no manifest)";
                string file = "-";
                if (WallMeshRegistry.TryGet(comp.Graphic, out var entry))
                {
                    archetype = entry.Archetype ?? "(null)";
                    file = entry.File ?? "(null)";
                }

                bool didRender = rendered.Contains((comp.Graphic, comp.X, comp.Y));
                string status;
                if (kind == TileOrientation.Floor || kind == TileOrientation.Roof)
                {
                    status = " skip ";  // not a wall, expected to skip 3D wall path
                }
                else if (!renderActive)
                {
                    status = " dark ";  // pipeline off, can't tell ok vs MISS
                }
                else if (didRender)
                {
                    status = "  ok  ";
                    meshed++;
                }
                else
                {
                    status = " MISS ";
                    missing++;
                }

                switch (kind)
                {
                    case TileOrientation.Corner:    corner++; break;
                    case TileOrientation.CornerNW:  cornerNW++; break;
                    case TileOrientation.CornerNE:  cornerNE++; break;
                    case TileOrientation.CornerSW:  cornerSW++; break;
                    case TileOrientation.CornerSE:  cornerSE++; break;
                    case TileOrientation.WallSouth: wallS++; break;
                    case TileOrientation.WallEast:  wallE++; break;
                    case TileOrientation.WallNorth: wallN++; break;
                    case TileOrientation.WallWest:  wallW++; break;
                    case TileOrientation.Floor:     floor++; break;
                    case TileOrientation.Roof:      roof++; break;
                    case TileOrientation.Unknown:   unknown++; break;
                    default: other++; break;
                }

                string flagStr = TileFlagString(ref data);
                sb.AppendLine(
                    $"{status}  0x{comp.Graphic:X4}    ({comp.MultiOffsetX,3},{comp.MultiOffsetY,3},{comp.MultiOffsetZ,3})  ({comp.X,5},{comp.Y,5},{comp.Z,3})    {kind,-12}   {archetype,-9}            {file,-22}  {flagStr}");

                jsonComponents.Add(new Dictionary<string, object>
                {
                    ["graphic"] = $"0x{comp.Graphic:X4}",
                    ["graphicId"] = (int)comp.Graphic,
                    ["offsetX"] = (int)comp.MultiOffsetX,
                    ["offsetY"] = (int)comp.MultiOffsetY,
                    ["offsetZ"] = (int)comp.MultiOffsetZ,
                    ["worldX"] = comp.X,
                    ["worldY"] = comp.Y,
                    ["worldZ"] = (int)comp.Z,
                    ["resolvedKind"] = kind.ToString(),
                    ["manifestArchetype"] = archetype,
                    ["glbFile"] = file,
                    ["status"] = status.Trim(),
                    ["flags"] = flagStr,
                    ["isWall"] = data.IsWall,
                    ["isSurface"] = data.IsSurface,
                    ["isRoof"] = data.IsRoof,
                    ["isImpassable"] = data.IsImpassable,
                    ["height"] = (int)data.Height
                });
            }

            sb.AppendLine();
            sb.AppendLine($"# Resolved-kind tally: Corner={corner} (NW={cornerNW} NE={cornerNE} SW={cornerSW} SE={cornerSE}) WallS={wallS} WallE={wallE} WallN={wallN} WallW={wallW} Floor={floor} Roof={roof} Unknown={unknown} Other={other}");
            sb.AppendLine($"# Mesh status: meshed={meshed} MISS={missing} (3D-wall categories only)");

            LastComponentCount = house.Components.Count;
            LastMeshedCount = meshed;
            LastMissingMeshCount = missing;

            // Per-graphic summary: which graphic IDs failed to render?
            sb.AppendLine();
            if (!renderActive)
                sb.AppendLine("# === FAILURES BY GRAPHIC (skipped — pipeline was DARK; re-dump in Full 3D) ===");
            else
                sb.AppendLine("# === FAILURES BY GRAPHIC ===");
            sb.AppendLine("graphic  count-missing  resolved-kind  manifest-archetype  why");
            var byGraphic = house.Components
                .Where(c => !c.IsDestroyed)
                .GroupBy(c => c.Graphic)
                .OrderByDescending(g => g.Count());
            foreach (var grp in byGraphic)
            {
                ref var data = ref ClassicUO.Client.Game.UO.FileManager.TileData.StaticData[grp.Key];
                var kind = MultiOrientationTable.Resolve(grp.Key, ref data);
                if (kind == TileOrientation.Floor || kind == TileOrientation.Roof) continue;
                if (!renderActive) continue;  // can't classify failures meaningfully

                int missCount = grp.Count(c => !rendered.Contains((c.Graphic, c.X, c.Y)));
                if (missCount == 0) continue;

                string archetype = "(no manifest)";
                if (WallMeshRegistry.TryGet(grp.Key, out var entry))
                    archetype = entry.Archetype ?? "(null)";

                string why;
                if (kind == TileOrientation.Unknown)
                    why = "no orientation table entry → falls to Unknown placement";
                else if (archetype == "(no manifest)")
                    why = "no GLB in WallMeshRegistry";
                else
                    why = "has manifest + orientation but didn't render — investigate";

                sb.AppendLine($"0x{grp.Key:X4}   {missCount,12}    {kind,-12}   {archetype,-18}  {why}");
            }

            string text = sb.ToString();
            Console.Write(text);

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                LastReportPath = Path.Combine(dir, $"wallmulti-dump-{stamp}.txt");
                File.WriteAllText(LastReportPath, text);
                Console.WriteLine($"[3DCUO] wall-multi dump written to {LastReportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] wall-multi dump file write FAILED: {ex.Message}");
            }

            // JSON sidecar — structured payload for the multi-author skill's
            // analyze mode. Same data, machine-readable shape.
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                LastJsonReportPath = Path.Combine(dir, $"wallmulti-dump-{stamp}.json");

                var payload = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.Now.ToString("O"),
                    ["houseSerial"] = $"0x{serial:X8}",
                    ["anchor"] = new Dictionary<string, object>
                    {
                        ["graphic"] = $"0x{anchorGraphic:X4}",
                        ["x"] = ax, ["y"] = ay, ["z"] = az
                    },
                    ["bounds"] = house.Bounds.ToString(),
                    ["componentCount"] = house.Components.Count,
                    ["renderState"] = new Dictionary<string, object>
                    {
                        ["worldIs3D"] = worldIs3D,
                        ["multi3DEnabled"] = multiOn,
                        ["use3DWallMeshes"] = wallMeshOn,
                        ["renderActive"] = renderActive,
                        ["lastWallMeshInstances"] = WallMeshDrawer.LastInstanceCount
                    },
                    ["tally"] = new Dictionary<string, int>
                    {
                        ["corner"] = corner, ["wallSouth"] = wallS, ["wallEast"] = wallE,
                        ["wallNorth"] = wallN, ["wallWest"] = wallW,
                        ["floor"] = floor, ["roof"] = roof,
                        ["unknown"] = unknown, ["other"] = other,
                        ["meshed"] = meshed, ["missing"] = missing
                    },
                    ["components"] = jsonComponents
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(LastJsonReportPath, json);
                Console.WriteLine($"[3DCUO] wall-multi JSON sidecar written to {LastJsonReportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] wall-multi JSON sidecar FAILED: {ex.Message}");
            }
        }

        private static string TileFlagString(ref ClassicUO.Assets.StaticTiles data)
        {
            var parts = new List<string>(4);
            if (data.IsWall) parts.Add("Wall");
            if (data.IsSurface) parts.Add("Surface");
            if (data.IsRoof) parts.Add("Roof");
            if (data.IsImpassable) parts.Add("Impass");
            return parts.Count == 0 ? "-" : string.Join("|", parts);
        }

        private static int player_X(Mobile p) => p?.X ?? 0;
        private static int player_Y(Mobile p) => p?.Y ?? 0;
        private static int player_Z(Mobile p) => p?.Z ?? 0;
    }
}
