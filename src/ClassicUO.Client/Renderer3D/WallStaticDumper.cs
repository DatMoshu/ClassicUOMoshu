// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — radius-based static-building dump.
//
// WallMultiDumper covers player-deeded houses (Multi serials), but pre-built
// world buildings like Britain's Sweet Dreams Inn are made of map statics, not
// multi components. This dumper walks every Static within a square radius of
// the player and produces the same per-component status table so we can chase
// alignment + manifest coverage on those buildings too.
//
// Output: console + Logs/wallstatic-dump-<timestamp>.txt

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class WallStaticDumper
    {
        public static int DefaultRadius = 12;
        public static string LastReportPath;
        public static int LastComponentCount;
        public static int LastMeshedCount;
        public static int LastMissingMeshCount;

        public static void DumpNearest() => DumpNearest(DefaultRadius);

        public static void DumpNearest(int radius)
        {
            var world = ClassicUO.Client.Game.UO.World;
            var player = world?.Player;
            if (world == null || player == null || world.Map == null)
            {
                Console.WriteLine("[3DCUO] WallStaticDumper: no world/player/map.");
                return;
            }

            WallMeshRegistry.EnsureLoaded();

            int px = player.X, py = player.Y, pz = player.Z;
            int minX = px - radius, maxX = px + radius;
            int minY = py - radius, maxY = py + radius;
            int minCX = minX >> 3, maxCX = maxX >> 3;
            int minCY = minY >> 3, maxCY = maxY >> 3;

            // Render-state snapshot (matches WallMultiDumper). DARK status when
            // pipeline isn't active — same caveat applies.
            bool worldIs3D = RenderModeController.WorldIs3D;
            bool wallMeshOn = Multi3DRenderer.Use3DWallMeshes;
            bool multiOn = Multi3DRenderer.Enabled;
            bool renderActive = worldIs3D && wallMeshOn && multiOn;

            // What did the wall drawer actually instance last frame? Same key as
            // WallMultiDumper: (graphic, tile-cx, tile-cz).
            var rendered = new HashSet<(ushort g, int cx, int cz)>();
            foreach (var inst in WallMeshDrawer.LastInstances)
            {
                int cx = (int)Math.Round(inst.AnchorWorld.X / LandMesh3D.TILE);
                int cz = (int)Math.Round(inst.AnchorWorld.Z / LandMesh3D.TILE);
                rendered.Add((inst.Graphic, cx, cz));
            }

            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine($"# 3DCUO static-radius dump  {DateTime.Now:O}");
            sb.AppendLine($"# Player: ({px}, {py}, {pz})  radius={radius}  tile-bbox=({minX},{minY})..({maxX},{maxY})");
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

            int total = 0, meshed = 0, missing = 0;
            int corner = 0, cornerNW = 0, cornerNE = 0, cornerSW = 0, cornerSE = 0;
            int wallS = 0, wallE = 0, wallN = 0, wallW = 0, floor = 0, roof = 0, unknown = 0, other = 0;

            // Buffer rows so we can sort by (Z, Y, X) for readability.
            var rows = new List<(int z, int y, int x, string line, ushort graphic, TileOrientation kind, string archetype)>(512);

            for (int cy = minCY; cy <= maxCY; cy++)
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                Chunk chunk;
                try { chunk = world.Map.GetChunk2(cx, cy, load: true); }
                catch { continue; }
                if (chunk == null) continue;

                int chunkOriginX = chunk.X * 8;
                int chunkOriginY = chunk.Y * 8;

                for (int ty = 0; ty < 8; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    int worldX = chunkOriginX + tx;
                    int worldY = chunkOriginY + ty;
                    if (worldX < minX || worldX > maxX || worldY < minY || worldY > maxY) continue;

                    for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                    {
                        if (obj.IsDestroyed) continue;
                        if (obj is not Static s) continue;

                        ref var data = ref ClassicUO.Client.Game.UO.FileManager.TileData.StaticData[s.Graphic];
                        var kind = MultiOrientationTable.Resolve(s.Graphic, ref data);

                        string archetype = "(no manifest)";
                        string file = "-";
                        if (WallMeshRegistry.TryGet(s.Graphic, out var entry))
                        {
                            archetype = entry.Archetype ?? "(null)";
                            file = entry.File ?? "(null)";
                        }

                        bool didRender = rendered.Contains((s.Graphic, s.X, s.Y));
                        string status;
                        if (kind == TileOrientation.Floor || kind == TileOrientation.Roof)
                        {
                            status = " skip ";
                        }
                        else if (!renderActive)
                        {
                            status = " dark ";
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
                        int dx = s.X - px, dy = s.Y - py, dz = s.Z - pz;
                        string line =
                            $"{status}  0x{s.Graphic:X4}  rel({dx,3},{dy,3},{dz,3})  abs({s.X,5},{s.Y,5},{s.Z,3})  h={data.Height,2}  {kind,-12}   {archetype,-9}            {file,-22}  {flagStr}";

                        rows.Add((s.Z, s.Y, s.X, line, s.Graphic, kind, archetype));
                        total++;
                    }
                }
            }

            sb.AppendLine("status   graphic   rel(dx,dy,dz)    abs(X,Y,Z)         h    resolved-kind  manifest-archetype  glb-file              flags");
            foreach (var row in rows.OrderBy(r => r.z).ThenBy(r => r.y).ThenBy(r => r.x))
                sb.AppendLine(row.line);

            sb.AppendLine();
            sb.AppendLine($"# Components: {total}");
            sb.AppendLine($"# Resolved-kind tally: Corner={corner} (NW={cornerNW} NE={cornerNE} SW={cornerSW} SE={cornerSE}) WallS={wallS} WallE={wallE} WallN={wallN} WallW={wallW} Floor={floor} Roof={roof} Unknown={unknown} Other={other}");
            sb.AppendLine($"# Mesh status: meshed={meshed} MISS={missing} (3D-wall categories only)");

            // Failure-by-graphic summary so missing manifest entries surface fast.
            sb.AppendLine();
            if (!renderActive)
                sb.AppendLine("# === FAILURES BY GRAPHIC (skipped — pipeline was DARK; re-dump in Full 3D) ===");
            else
                sb.AppendLine("# === FAILURES BY GRAPHIC ===");
            sb.AppendLine("graphic  count-missing  resolved-kind  manifest-archetype  why");
            if (renderActive)
            {
                var byGraphic = rows
                    .Where(r => r.kind != TileOrientation.Floor && r.kind != TileOrientation.Roof)
                    .GroupBy(r => r.graphic)
                    .OrderByDescending(g => g.Count());
                foreach (var grp in byGraphic)
                {
                    int missCount = grp.Count(r => !rendered.Contains((r.graphic, r.x, r.y)));
                    if (missCount == 0) continue;
                    var first = grp.First();
                    string why = first.kind == TileOrientation.Unknown
                        ? "no orientation table entry → falls to Unknown placement"
                        : first.archetype == "(no manifest)"
                            ? "no GLB in WallMeshRegistry"
                            : "has manifest + orientation but didn't render — investigate";
                    sb.AppendLine($"0x{grp.Key:X4}   {missCount,12}    {first.kind,-12}   {first.archetype,-18}  {why}");
                }
            }

            string text = sb.ToString();
            Console.Write(text);

            LastComponentCount = total;
            LastMeshedCount = meshed;
            LastMissingMeshCount = missing;

            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                LastReportPath = Path.Combine(dir, $"wallstatic-dump-{stamp}.txt");
                File.WriteAllText(LastReportPath, text);
                Console.WriteLine($"[3DCUO] wall-static dump written to {LastReportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] wall-static dump file write FAILED: {ex.Message}");
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
    }
}
