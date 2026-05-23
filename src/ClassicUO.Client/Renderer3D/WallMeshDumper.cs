// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — wall-mesh candidate scanner.
// Walks the most-recent set of visible chunks and reports every wall-piece
// graphic that has a manifest entry in WallMeshRegistry. Helps the human find
// in-world locations where the 3D wall meshes will activate.
//
// Output: console + a sidecar file under Logs/ for after-run review. Per-graphic
// counts, sample world coordinates, and the player's current tile so the human
// can navigate to the nearest hit.

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
    internal static class WallMeshDumper
    {
        public static string LastReportPath;
        public static int LastHitCount;
        public static int LastUniqueGraphics;

        private const int MaxSamplesPerGraphic = 5;

        public static void Dump()
        {
            WallMeshRegistry.EnsureLoaded();

            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine($"# 3DCUO wall-mesh candidate dump  {DateTime.Now:O}");
            sb.AppendLine($"# WallMeshRegistry: count={WallMeshRegistry.LoadedEntryCount} (cache={WallMeshRegistry.CacheCount} failed={WallMeshRegistry.FailedCount})");
            sb.AppendLine($"# resolved version: {WallMeshRegistry.ResolvedVersion ?? "(none)"}");
            sb.AppendLine($"# meshes dir: {WallMeshRegistry.MeshesDir ?? "(null)"}");
            sb.AppendLine($"# RenderMode={RenderModeController.Mode}  WorldIs3D={RenderModeController.WorldIs3D}");
            sb.AppendLine($"# Use3DWallMeshes={Multi3DRenderer.Use3DWallMeshes}  Multi3D.Enabled={Multi3DRenderer.Enabled}");
            sb.AppendLine($"# Multi3D last frame: multisSeen={Multi3DRenderer.LastMultiSeen} staticsSeen={Multi3DRenderer.LastStaticSeen} quads={Multi3DRenderer.LastQuadCount} wallMeshes={Multi3DRenderer.LastWallMeshCount}");
            sb.AppendLine($"# WallMeshDrawer last frame: instances={WallMeshDrawer.LastInstanceCount} submeshes={WallMeshDrawer.LastSubmeshesDrawn} tris={WallMeshDrawer.LastTrianglesDrawn}  wireframe={WallMeshDrawer.Wireframe} shadedWire={WallMeshDrawer.ShadedWireframe}");
            if (!RenderModeController.WorldIs3D)
            {
                sb.AppendLine("#");
                sb.AppendLine("# >>> NOT IN FULL 3D MODE. Multi3DRenderer.Draw is not being called.");
                sb.AppendLine("# >>> Click 'Pure 3D ISO' or 'Pers 3D' in QUICK MODE to enter Full 3D.");
                sb.AppendLine("#");
            }
            else if (Multi3DRenderer.LastMultiSeen + Multi3DRenderer.LastStaticSeen == 0)
            {
                sb.AppendLine("# >>> WARNING: Multi3DRenderer hasn't seen anything yet. Move the camera or wait one frame.");
            }

            // Player position (so user can navigate)
            var player = ClassicUO.Client.Game.UO.World.Player;
            int px = player?.X ?? 0;
            int py = player?.Y ?? 0;
            int pz = player?.Z ?? 0;
            sb.AppendLine($"# Player: X={px} Y={py} Z={pz}");

            // Use the visible-chunks snapshot Static3DRenderer caches each frame.
            var chunks = Static3DRenderer.LastVisibleChunks;
            if (chunks == null || chunks.Count == 0)
            {
                sb.AppendLine("# WARNING: no visible chunks cached. Enable Multi3D and/or Static3D to populate.");
            }

            // hits: graphic -> { count, samples (up to MaxSamplesPerGraphic) }
            var hits = new Dictionary<ushort, (int Count, List<(int X, int Y, int Z, int Dist)> Samples)>();
            // allWalls: every IsWall-flagged static/multi in view, manifest or not.
            var allWalls = new Dictionary<ushort, (int Count, int NearestDist, int NX, int NY, int NZ)>();

            int total = 0;
            if (chunks != null)
            {
                for (int ci = 0; ci < chunks.Count; ci++)
                {
                    var chunk = chunks[ci];
                    if (chunk == null) continue;

                    for (int ty = 0; ty < 8; ty++)
                    for (int tx = 0; tx < 8; tx++)
                    {
                        for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                        {
                            if (obj.IsDestroyed) continue;
                            if (!(obj is Multi || obj is Static)) continue;

                            ushort g = obj.Graphic;

                            // Track wall-flagged graphics regardless of manifest
                            // membership so the human can see which graphic IDs
                            // need new GLBs.
                            int dxAll = obj.X - px, dyAll = obj.Y - py;
                            int distAll = (int)Math.Round(Math.Sqrt(dxAll * dxAll + dyAll * dyAll));
                            ref var data = ref ClassicUO.Client.Game.UO.FileManager.TileData.StaticData[g];
                            if (data.IsWall)
                            {
                                if (allWalls.TryGetValue(g, out var aw))
                                {
                                    aw.Count++;
                                    if (distAll < aw.NearestDist)
                                    { aw.NearestDist = distAll; aw.NX = obj.X; aw.NY = obj.Y; aw.NZ = obj.Z; }
                                    allWalls[g] = aw;
                                }
                                else
                                {
                                    allWalls[g] = (1, distAll, obj.X, obj.Y, obj.Z);
                                }
                            }

                            if (!WallMeshRegistry.TryGet(g, out _)) continue;

                            total++;
                            int dx = obj.X - px;
                            int dy = obj.Y - py;
                            int dist = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));

                            if (!hits.TryGetValue(g, out var bucket))
                            {
                                bucket = (0, new List<(int, int, int, int)>(MaxSamplesPerGraphic));
                                hits[g] = bucket;
                            }
                            bucket.Count++;
                            if (bucket.Samples.Count < MaxSamplesPerGraphic)
                            {
                                bucket.Samples.Add((obj.X, obj.Y, obj.Z, dist));
                                bucket.Samples.Sort((a, b) => a.Dist.CompareTo(b.Dist));
                            }
                            else
                            {
                                // Replace the farthest sample if this one is closer.
                                int worstIdx = 0;
                                int worstDist = bucket.Samples[0].Dist;
                                for (int i = 1; i < bucket.Samples.Count; i++)
                                    if (bucket.Samples[i].Dist > worstDist) { worstIdx = i; worstDist = bucket.Samples[i].Dist; }
                                if (dist < worstDist)
                                {
                                    bucket.Samples[worstIdx] = (obj.X, obj.Y, obj.Z, dist);
                                    bucket.Samples.Sort((a, b) => a.Dist.CompareTo(b.Dist));
                                }
                            }
                            hits[g] = bucket;
                        }
                    }
                }
            }

            LastHitCount = total;
            LastUniqueGraphics = hits.Count;

            sb.AppendLine($"# total wall-mesh candidates in view: {total} across {hits.Count} unique graphics");
            sb.AppendLine();

            if (hits.Count == 0)
            {
                sb.AppendLine("(no candidates found in visible chunks — try moving to a town with stone-wall");
                sb.AppendLine(" buildings, e.g. Britain, Trinsic, Vesper, or Yew. The manifest covers");
                sb.AppendLine($" graphic IDs 0x0087..0x0143 — classic UO wall sets.)");
            }
            else
            {
                // Sort by closest sample, then by count desc.
                var ordered = hits.OrderBy(kv =>
                    kv.Value.Samples.Count == 0 ? int.MaxValue : kv.Value.Samples[0].Dist);

                sb.AppendLine("graphic   count   archetype   nearest samples (X, Y, Z, dist-tiles)");
                foreach (var kv in ordered)
                {
                    WallMeshRegistry.TryGet(kv.Key, out var entry);
                    var samples = string.Join("  ",
                        kv.Value.Samples.Select(s => $"({s.X},{s.Y},{s.Z},d={s.Dist})"));
                    sb.AppendLine(
                        $"0x{kv.Key:X4}    {kv.Value.Count,5}   {entry.Archetype,-9}   {samples}");
                }

                // Closest single hit overall — most useful "go here" pointer.
                var closest = hits
                    .Where(kv => kv.Value.Samples.Count > 0)
                    .OrderBy(kv => kv.Value.Samples[0].Dist)
                    .First();
                var c = closest.Value.Samples[0];
                sb.AppendLine();
                sb.AppendLine($"# CLOSEST: graphic 0x{closest.Key:X4} at ({c.X},{c.Y},{c.Z}) — {c.Dist} tiles from player");
            }

            // ── All wall-flagged statics in view, manifest or not ──────────
            // This is the "make these next" list. Each row marks ✓ if it's
            // already covered by WallMeshRegistry, ✗ if it needs a new GLB.
            sb.AppendLine();
            sb.AppendLine($"# ALL WALL-FLAGGED STATICS IN VIEW: {allWalls.Count} unique graphics");
            sb.AppendLine("# (✓ = in manifest, ✗ = needs new GLB)");
            sb.AppendLine();
            sb.AppendLine("status  graphic   count   nearest  sample (X,Y,Z)");
            int covered = 0, missing = 0;
            foreach (var kv in allWalls.OrderBy(kv => kv.Value.NearestDist))
            {
                bool inManifest = WallMeshRegistry.TryGet(kv.Key, out _);
                if (inManifest) covered++; else missing++;
                string mark = inManifest ? "  ✓   " : "  ✗   ";
                sb.AppendLine(
                    $"{mark}  0x{kv.Key:X4}    {kv.Value.Count,5}   d={kv.Value.NearestDist,4}   ({kv.Value.NX},{kv.Value.NY},{kv.Value.NZ})");
            }
            sb.AppendLine();
            sb.AppendLine($"# coverage: {covered} graphics covered / {allWalls.Count} total ({missing} graphics need GLBs)");

            // ── Family-range summary ───────────────────────────────────────
            // Bucket graphics by 16-id ranges so contiguous wall-set families
            // are obvious. Each row: range, total count, manifest-coverage %.
            // ── Per-entry registry status ──────────────────────────────────
            // For every manifest entry: whether the GLB exists on disk, whether
            // it's been loaded (cached), and whether it failed to load.
            sb.AppendLine();
            sb.AppendLine($"# REGISTRY ENTRIES ({WallMeshRegistry.LoadedEntryCount}):");
            sb.AppendLine($"# meshes_dir = {WallMeshRegistry.MeshesDir ?? "(null)"}");
            sb.AppendLine($"# cache={WallMeshRegistry.CacheCount} failed={WallMeshRegistry.FailedCount}");
            sb.AppendLine();
            int sCached = 0, sFailed = 0, sUntouched = 0, sMissingFile = 0;
            var problems = new List<string>();
            foreach (var es in WallMeshRegistry.AllEntryStatuses().OrderBy(e => e.Graphic))
            {
                if (!es.FileExists)
                {
                    sMissingFile++;
                    problems.Add($"FILE-MISSING  0x{es.Graphic:X4}    {es.Archetype,-9}   {es.File}");
                }
                else if (es.IsCached) sCached++;
                else if (es.HasFailed)
                {
                    sFailed++;
                    problems.Add($"LOAD-FAILED   0x{es.Graphic:X4}    {es.Archetype,-9}   {es.File}");
                }
                else sUntouched++;
            }
            sb.AppendLine($"# entry totals: loaded={sCached} failed={sFailed} untouched={sUntouched} file-missing={sMissingFile}");
            // Only print rows that need attention; with 3000+ entries the full
            // table would dominate the report.
            if (problems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"# problem entries ({problems.Count}):");
                sb.AppendLine("status        graphic   archetype   file");
                foreach (var line in problems.Take(80)) sb.AppendLine(line);
                if (problems.Count > 80) sb.AppendLine($"... and {problems.Count - 80} more");
            }

            // ── Per-instance positions for last frame ─────────────────────
            sb.AppendLine();
            sb.AppendLine($"# WALL MESH INSTANCES THIS FRAME: {WallMeshDrawer.LastInstances.Count}");
            if (WallMeshDrawer.LastInstances.Count > 0)
            {
                sb.AppendLine("graphic   kind         anchor (world X, Y, Z)");
                foreach (var inst in WallMeshDrawer.LastInstances.Take(40))
                {
                    sb.AppendLine($"0x{inst.Graphic:X4}    {inst.Kind,-12}  ({inst.AnchorWorld.X:F1}, {inst.AnchorWorld.Y:F1}, {inst.AnchorWorld.Z:F1})");
                }
                if (WallMeshDrawer.LastInstances.Count > 40)
                    sb.AppendLine($"... and {WallMeshDrawer.LastInstances.Count - 40} more");
            }

            sb.AppendLine();
            sb.AppendLine("# WALL FAMILY RANGES (16-id buckets):");
            sb.AppendLine("range            count   covered   missing");
            var buckets = allWalls
                .GroupBy(kv => (ushort)(kv.Key & 0xFFF0))
                .OrderBy(g => g.Key);
            foreach (var b in buckets)
            {
                int bCount = b.Sum(kv => kv.Value.Count);
                int bCovered = b.Where(kv => WallMeshRegistry.TryGet(kv.Key, out _)).Sum(kv => kv.Value.Count);
                int bMissing = bCount - bCovered;
                sb.AppendLine($"0x{b.Key:X4}-0x{b.Key + 0xF:X4}  {bCount,5}   {bCovered,5}     {bMissing,5}");
            }

            string text = sb.ToString();
            Console.Write(text);

            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                LastReportPath = Path.Combine(dir, $"wallmesh-dump-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(LastReportPath, text);
                Console.WriteLine($"[3DCUO] wallmesh dump written to {LastReportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] wallmesh dump file write FAILED: {ex.Message}");
            }
        }
    }
}
