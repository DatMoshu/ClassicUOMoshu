// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — wall-mesh mapping audit.
// Pairs every wall instance submitted to WallMeshDrawer last frame with:
//   - its manifest entry (file + archetype) from WallMeshRegistry
//   - its resolved TileOrientation from MultiOrientationTable
//   - its TileData flags (IsWall/IsSurface/IsRoof + raw flags)
// Flags rows where archetype and orientation disagree (e.g. archetype="wall_NS"
// but renderer placed it as WallEast) — those are the mismapped graphic→GLB
// pairings the human needs to fix in the manifest or override table.
//
// Output: console + Logs/wallmapping-dump-<timestamp>.txt sidecar.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClassicUO.Assets;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class WallMappingDumper
    {
        public static string LastReportPath;
        public static int LastInstanceCount;
        public static int LastMismatchCount;
        public static int LastUniqueMismatchGraphics;

        public static void Dump()
        {
            WallMeshRegistry.EnsureLoaded();

            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine($"# 3DCUO wall mapping audit  {DateTime.Now:O}");
            sb.AppendLine($"# meshes dir: {WallMeshRegistry.MeshesDir ?? "(null)"}");
            sb.AppendLine($"# manifest entries={WallMeshRegistry.LoadedEntryCount}  cached={WallMeshRegistry.CacheCount}");
            sb.AppendLine($"# WallMeshDrawer last frame: instances={WallMeshDrawer.LastInstanceCount}");

            var player = ClassicUO.Client.Game.UO.World.Player;
            if (player != null)
                sb.AppendLine($"# Player: X={player.X} Y={player.Y} Z={player.Z}");

            var instances = WallMeshDrawer.LastInstances;
            LastInstanceCount = instances.Count;

            if (instances.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("# No wall-mesh instances rendered last frame. Enable Multi3D + Walls:3D Meshes,");
                sb.AppendLine("# face a wall in the world, then click 'Dump Wall Mapping' again.");
                FlushReport(sb);
                return;
            }

            // ── Per-instance rows ─────────────────────────────────────────
            sb.AppendLine();
            sb.AppendLine("# PER-INSTANCE MAPPING (archetype = what the GLB is, kind = how renderer placed it)");
            sb.AppendLine("status   graphic   archetype   kind          file                    anchor (X,Y,Z)   flags");

            // Group: graphic → list of (kind, archetype, file)
            var perGraphic = new Dictionary<ushort, (string Archetype, string File, Dictionary<TileOrientation, int> KindCounts, int TotalCount, bool IsWallFlag)>();

            int mismatch = 0;
            foreach (var inst in instances)
            {
                ushort g = inst.Graphic;
                string archetype = "(none)";
                string file = "(none)";
                if (WallMeshRegistry.TryGet(g, out var entry))
                {
                    archetype = entry.Archetype ?? "(null)";
                    file = entry.File ?? "(null)";
                }

                ref var data = ref ClassicUO.Client.Game.UO.FileManager.TileData.StaticData[g];
                bool agree = ArchetypeMatchesKind(archetype, inst.Kind);
                if (!agree) mismatch++;

                string flagStr = TileFlagsString(ref data);
                string mark = agree ? "  ok   " : "  ✗MM  ";

                sb.AppendLine(
                    $"{mark} 0x{g:X4}    {archetype,-9}   {inst.Kind,-12}  {file,-22}  ({inst.AnchorWorld.X:F0},{inst.AnchorWorld.Y:F0},{inst.AnchorWorld.Z:F0})   {flagStr}");

                if (!perGraphic.TryGetValue(g, out var pg))
                {
                    pg = (archetype, file, new Dictionary<TileOrientation, int>(), 0, data.IsWall);
                    perGraphic[g] = pg;
                }
                if (!pg.KindCounts.ContainsKey(inst.Kind)) pg.KindCounts[inst.Kind] = 0;
                pg.KindCounts[inst.Kind]++;
                pg.TotalCount++;
                perGraphic[g] = pg;
            }

            LastMismatchCount = mismatch;

            // ── Per-graphic summary ───────────────────────────────────────
            sb.AppendLine();
            sb.AppendLine($"# PER-GRAPHIC SUMMARY: {perGraphic.Count} unique graphics, {mismatch} mismatch instances");
            sb.AppendLine("graphic   archetype   isWall  kind-distribution                          file");
            int uniqueMismatchGraphics = 0;
            foreach (var kv in perGraphic.OrderBy(p => p.Key))
            {
                var pg = kv.Value;
                bool anyMismatch = pg.KindCounts.Keys.Any(k => !ArchetypeMatchesKind(pg.Archetype, k));
                if (anyMismatch) uniqueMismatchGraphics++;
                string kinds = string.Join(" ",
                    pg.KindCounts.OrderByDescending(p => p.Value)
                        .Select(p => $"{p.Key}={p.Value}"));
                string mark = anyMismatch ? " ✗ " : "   ";
                sb.AppendLine(
                    $"{mark}0x{kv.Key:X4}   {pg.Archetype,-9}   {(pg.IsWallFlag ? "yes" : "no "),-3}   {kinds,-42}  {pg.File}");
            }
            LastUniqueMismatchGraphics = uniqueMismatchGraphics;

            // ── Suggested manifest fixes ──────────────────────────────────
            // For each mismatched graphic, suggest which archetype best fits the
            // observed orientation distribution. Author can paste these into the
            // override JSON or fix the Blender export tagging.
            sb.AppendLine();
            sb.AppendLine("# SUGGESTED FIXES (graphics where archetype disagrees with renderer placement):");
            sb.AppendLine("graphic   current      suggested    based-on");
            foreach (var kv in perGraphic.OrderBy(p => p.Key))
            {
                var pg = kv.Value;
                if (!pg.KindCounts.Keys.Any(k => !ArchetypeMatchesKind(pg.Archetype, k))) continue;

                var dominant = pg.KindCounts.OrderByDescending(p => p.Value).First().Key;
                string suggested = SuggestedArchetypeFor(dominant);
                sb.AppendLine(
                    $" 0x{kv.Key:X4}   {pg.Archetype,-9}    {suggested,-9}    dominant kind={dominant}  total={pg.TotalCount}");
            }

            sb.AppendLine();
            sb.AppendLine($"# totals: instances={instances.Count}  mismatches={mismatch}  mismapped-graphics={uniqueMismatchGraphics}");

            FlushReport(sb);
        }

        // wall_NS  → length runs north-south → renderer places as WallEast/WallWest (length along world Z)
        // wall_EW  → length runs east-west   → renderer places as WallNorth/WallSouth (length along world X)
        // corner / pillar → any non-edge placement
        private static bool ArchetypeMatchesKind(string archetype, TileOrientation kind)
        {
            if (string.IsNullOrEmpty(archetype)) return false;
            switch (archetype)
            {
                case "wall_NS":
                    return kind == TileOrientation.WallEast || kind == TileOrientation.WallWest;
                case "wall_EW":
                    return kind == TileOrientation.WallNorth || kind == TileOrientation.WallSouth;
                case "corner":
                case "pillar":
                    return kind == TileOrientation.Corner ||
                           kind == TileOrientation.Unknown ||
                           kind == TileOrientation.Floor ||
                           kind == TileOrientation.Roof;
                default:
                    return false;
            }
        }

        private static string SuggestedArchetypeFor(TileOrientation kind)
        {
            switch (kind)
            {
                case TileOrientation.WallEast:
                case TileOrientation.WallWest:
                    return "wall_NS";
                case TileOrientation.WallNorth:
                case TileOrientation.WallSouth:
                    return "wall_EW";
                case TileOrientation.Corner:    return "corner";
                case TileOrientation.CornerNW:  return "corner_nw";
                case TileOrientation.CornerNE:  return "corner_ne";
                case TileOrientation.CornerSW:  return "corner_sw";
                case TileOrientation.CornerSE:  return "corner_se";
                default:
                    return "pillar";
            }
        }

        private static string TileFlagsString(ref StaticTiles data)
        {
            var parts = new List<string>(4);
            if (data.IsWall) parts.Add("Wall");
            if (data.IsSurface) parts.Add("Surface");
            if (data.IsRoof) parts.Add("Roof");
            if (data.IsImpassable) parts.Add("Impass");
            return parts.Count == 0 ? "-" : string.Join("|", parts);
        }

        private static void FlushReport(StringBuilder sb)
        {
            string text = sb.ToString();
            Console.Write(text);
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                LastReportPath = Path.Combine(dir, $"wallmapping-dump-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(LastReportPath, text);
                Console.WriteLine($"[3DCUO] wall mapping dump written to {LastReportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] wall mapping dump file write FAILED: {ex.Message}");
            }
        }
    }
}
