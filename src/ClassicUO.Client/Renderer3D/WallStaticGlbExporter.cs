// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — bake the radius static building into a single GLB scene.
//
// For every wall static within `RadiusTiles` of the player, this exporter:
//   1. Loads the source manifest GLB (via SharpGLTF).
//   2. Applies the WallMeshDrawer transform (yaw + non-uniform scale +
//      translate) to every vertex.
//   3. Adds the transformed primitives to a single SceneBuilder.
//
// Walls without a manifest entry get a magenta wireframe box so missing
// pieces are visually obvious in Blender.
//
// A paired sidecar JSON describes every static (graphic, world coords,
// resolved kind, archetype, glb file, transform) so visual issues can be
// cross-referenced against the classification pipeline.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class WallStaticGlbExporter
    {
        public static int RadiusTiles = 16;
        public static string LastError;
        public static string LastGlbPath;
        public static string LastJsonPath;

        // ---- coord convention (mirrors WallMeshDrawer) ----
        private const float TILE = LandMesh3D.TILE;     // 22f
        private const float ZS = LandMesh3D.Z_SCALE;    // 4f
        private const float MESH_SCALE_XZ = TILE;
        private const float DEFAULT_HEIGHT_WORLD = 80f;

        public static void ExportNearest()
        {
            var world = ClassicUO.Client.Game.UO.World;
            if (world?.Player == null || world.Map == null)
            {
                LastError = "no world / player / map";
                Console.WriteLine($"[3DCUO] WallStaticGlbExporter: {LastError}");
                return;
            }

            WallMeshRegistry.EnsureLoaded();

            int px = world.Player.X, py = world.Player.Y, pz = world.Player.Z;
            int minX = px - RadiusTiles, maxX = px + RadiusTiles;
            int minY = py - RadiusTiles, maxY = py + RadiusTiles;
            int minCX = minX >> 3, maxCX = maxX >> 3;
            int minCY = minY >> 3, maxCY = maxY >> 3;

            var scene = new SceneBuilder("WallStaticScene");
            var jsonStatics = new JsonArray();

            // One MeshBuilder per source GLB so SharpGLTF can dedupe materials.
            var meshBuilders =
                new Dictionary<string, MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty>>();

            // Magenta marker mesh for walls with no manifest entry.
            MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty> markerMb = null;

            int total = 0, baked = 0, marker = 0, skip = 0;

            for (int cy = minCY; cy <= maxCY; cy++)
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                Chunk chunk;
                try { chunk = world.Map.GetChunk2(cx, cy, load: true); }
                catch { continue; }
                if (chunk == null) continue;

                int chunkOriginX = chunk.X * 8, chunkOriginY = chunk.Y * 8;

                for (int ty = 0; ty < 8; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    int worldX = chunkOriginX + tx, worldY = chunkOriginY + ty;
                    if (worldX < minX || worldX > maxX || worldY < minY || worldY > maxY) continue;

                    for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                    {
                        if (obj.IsDestroyed) continue;
                        if (obj is not Static s) continue;

                        ref var data = ref ClassicUO.Client.Game.UO.FileManager.TileData.StaticData[s.Graphic];
                        var kind = MultiOrientationTable.Resolve(s.Graphic, ref data);
                        total++;

                        // Tile world position in mesh space (matches Multi3DRenderer).
                        float wx = s.X * TILE;
                        float wz = s.Y * TILE;
                        float wy = s.Z * ZS;
                        float h = data.Height > 0 ? data.Height * ZS : DEFAULT_HEIGHT_WORLD;

                        // Per-orientation anchor + yaw (mirrors WallMeshDrawer.Submit).
                        ResolveAnchor(kind, wx, wz, out float ax, out float az, out float yawRad);

                        string archetype = null, file = null;
                        if (WallMeshRegistry.TryGet(s.Graphic, out var entry))
                        {
                            archetype = entry.Archetype;
                            file = entry.File;
                        }

                        var sidecarRow = new JsonObject
                        {
                            ["graphic"] = $"0x{s.Graphic:X4}",
                            ["graphicId"] = (int)s.Graphic,
                            ["worldX"] = s.X,
                            ["worldY"] = s.Y,
                            ["worldZ"] = (int)s.Z,
                            ["meshAnchor"] = new JsonArray(ax, wy, az),
                            ["yawDeg"] = yawRad * 180f / MathF.PI,
                            ["targetHeight"] = h,
                            ["resolvedKind"] = kind.ToString(),
                            ["manifestArchetype"] = archetype,
                            ["glbFile"] = file,
                            ["isWall"] = data.IsWall,
                            ["isSurface"] = data.IsSurface,
                            ["isRoof"] = data.IsRoof,
                            ["isImpassable"] = data.IsImpassable,
                            ["height"] = (int)data.Height,
                        };

                        // Skip non-walls in the GLB (still recorded in JSON).
                        if (kind == TileOrientation.Floor || kind == TileOrientation.Roof
                            || kind == TileOrientation.Unknown)
                        {
                            sidecarRow["status"] = "skip";
                            jsonStatics.Add(sidecarRow);
                            skip++;
                            continue;
                        }

                        if (file != null)
                        {
                            string srcGlb = Path.Combine(WallMeshRegistry.MeshesDir, file);
                            if (!System.IO.File.Exists(srcGlb))
                            {
                                sidecarRow["status"] = "miss-file";
                                EmitMarkerBox(ref markerMb, ax, wy, az, h, kind);
                                jsonStatics.Add(sidecarRow);
                                marker++;
                                continue;
                            }

                            // World matrix matches WallMeshDrawer exactly.
                            float yScale = ComputeYScale(srcGlb, h);
                            var mWorld =
                                Matrix4x4.CreateScale(MESH_SCALE_XZ, yScale, MESH_SCALE_XZ) *
                                Matrix4x4.CreateRotationY(yawRad) *
                                Matrix4x4.CreateTranslation(ax, wy, az);

                            string mbKey = file;
                            if (!meshBuilders.TryGetValue(mbKey, out var mb))
                            {
                                mb = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty>(
                                    Path.GetFileNameWithoutExtension(file));
                                meshBuilders[mbKey] = mb;
                            }

                            try
                            {
                                BakeSourceGlbInto(mb, srcGlb, mWorld);
                                sidecarRow["status"] = "baked";
                                sidecarRow["yScale"] = yScale;
                                baked++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[3DCUO][static-export] {file} bake failed: {ex.Message}");
                                EmitMarkerBox(ref markerMb, ax, wy, az, h, kind);
                                sidecarRow["status"] = $"bake-failed:{ex.Message}";
                                marker++;
                            }
                        }
                        else
                        {
                            sidecarRow["status"] = "no-manifest";
                            EmitMarkerBox(ref markerMb, ax, wy, az, h, kind);
                            marker++;
                        }

                        jsonStatics.Add(sidecarRow);
                    }
                }
            }

            foreach (var mb in meshBuilders.Values)
                scene.AddRigidMesh(mb, Matrix4x4.Identity);
            if (markerMb != null)
                scene.AddRigidMesh(markerMb, Matrix4x4.Identity);

            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var outDir = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(outDir);
            string glbPath = Path.Combine(outDir, $"wallstatic-export-{stamp}.glb");
            string jsonPath = Path.Combine(outDir, $"wallstatic-export-{stamp}.json");

            try
            {
                var model = scene.ToGltf2();
                model.SaveGLB(glbPath);
                LastGlbPath = glbPath;
                long sz = new FileInfo(glbPath).Length;
                Console.WriteLine($"[3DCUO][static-export] GLB {glbPath} ({sz:N0} bytes) — baked={baked} marker={marker} skip={skip}/total={total}");
            }
            catch (Exception ex)
            {
                LastError = $"GLB save failed: {ex.Message}";
                Console.WriteLine($"[3DCUO][static-export] {LastError}");
            }

            try
            {
                var payload = new JsonObject
                {
                    ["timestamp"] = DateTime.Now.ToString("O"),
                    ["player"] = new JsonArray(px, py, pz),
                    ["radius"] = RadiusTiles,
                    ["meshesDir"] = WallMeshRegistry.MeshesDir,
                    ["coord"] = new JsonObject
                    {
                        ["TILE"] = TILE,
                        ["Z_SCALE"] = ZS,
                        ["mesh_axes"] = "Y up; X = uoX*TILE; Z = uoY*TILE; Y = uoZ*Z_SCALE",
                    },
                    ["counts"] = new JsonObject
                    {
                        ["total"] = total,
                        ["baked"] = baked,
                        ["marker"] = marker,
                        ["skip"] = skip,
                    },
                    ["statics"] = jsonStatics,
                };
                System.IO.File.WriteAllText(jsonPath,
                    payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                LastJsonPath = jsonPath;
                Console.WriteLine($"[3DCUO][static-export] JSON {jsonPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO][static-export] JSON save failed: {ex.Message}");
            }
        }

        private static void ResolveAnchor(TileOrientation kind, float wx, float wz,
            out float ax, out float az, out float yawRad)
        {
            switch (kind)
            {
                case TileOrientation.WallNorth:
                    ax = wx + TILE * 0.5f; az = wz; yawRad = MathF.PI / 2f; break;
                case TileOrientation.WallSouth:
                    ax = wx + TILE * 0.5f; az = wz + TILE; yawRad = MathF.PI / 2f; break;
                case TileOrientation.WallEast:
                    ax = wx + TILE; az = wz + TILE * 0.5f; yawRad = 0f; break;
                case TileOrientation.WallWest:
                    ax = wx; az = wz + TILE * 0.5f; yawRad = 0f; break;
                // Corner subtypes — center anchor + 90° steps. Mirrors
                // WallMeshDrawer.Submit so the exported scene matches the
                // runtime placement exactly.
                case TileOrientation.CornerNW:
                    ax = wx + TILE * 0.5f; az = wz + TILE * 0.5f; yawRad = 0f; break;
                case TileOrientation.CornerNE:
                    ax = wx + TILE * 0.5f; az = wz + TILE * 0.5f; yawRad = MathF.PI / 2f; break;
                case TileOrientation.CornerSE:
                    ax = wx + TILE * 0.5f; az = wz + TILE * 0.5f; yawRad = MathF.PI; break;
                case TileOrientation.CornerSW:
                    ax = wx + TILE * 0.5f; az = wz + TILE * 0.5f; yawRad = 3f * MathF.PI / 2f; break;
                default:
                    ax = wx + TILE * 0.5f; az = wz + TILE * 0.5f; yawRad = 0f; break;
            }
        }

        // Cache of source-GLB body heights so we don't re-load every wall.
        private static readonly Dictionary<string, float> _bodyHeightCache = new();

        private static float ComputeYScale(string srcGlb, float targetHeight)
        {
            if (!_bodyHeightCache.TryGetValue(srcGlb, out float bodyH))
            {
                try
                {
                    var settings = new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.Skip };
                    var root = ModelRoot.Load(srcGlb, settings);
                    float minY = float.MaxValue, maxY = float.MinValue;
                    foreach (var mesh in root.LogicalMeshes)
                    foreach (var prim in mesh.Primitives)
                    {
                        var pos = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
                        if (pos == null) continue;
                        for (int i = 0; i < pos.Count; i++)
                        {
                            float y = pos[i].Y;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                    bodyH = maxY > minY ? maxY - minY : 1f;
                }
                catch
                {
                    bodyH = 1f;
                }
                _bodyHeightCache[srcGlb] = bodyH;
            }
            return targetHeight / bodyH;
        }

        private static void BakeSourceGlbInto(
            MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty> mb,
            string srcGlb, Matrix4x4 worldXform)
        {
            var settings = new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.Skip };
            var root = ModelRoot.Load(srcGlb, settings);

            // Strip down to one shared per-source-GLB material so all primitives
            // collapse together. Texture embedding mirrors the runtime sampler.
            var mat = new MaterialBuilder(Path.GetFileNameWithoutExtension(srcGlb))
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithMetallicRoughness(0f, 1f);

            // Try to embed the first texture from the source.
            try
            {
                var firstTex = root.LogicalTextures.FirstOrDefault();
                if (firstTex?.PrimaryImage?.Content.Content.Length > 0)
                {
                    mat.WithChannelImage(KnownChannel.BaseColor, firstTex.PrimaryImage.Content.Content.ToArray());
                }
            }
            catch { /* best-effort */ }

            var prim = mb.UsePrimitive(mat);
            // Normal-transform = inverse-transpose of upper 3x3.
            Matrix4x4.Invert(worldXform, out var inv);
            var normalXform = Matrix4x4.Transpose(inv);

            foreach (var srcMesh in root.LogicalMeshes)
            foreach (var srcPrim in srcMesh.Primitives)
            {
                var positions = srcPrim.GetVertexAccessor("POSITION")?.AsVector3Array();
                if (positions == null) continue;
                var normals = srcPrim.GetVertexAccessor("NORMAL")?.AsVector3Array();
                var uvs = srcPrim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
                var indices = srcPrim.GetIndices();

                int vcount = positions.Count;
                var xv = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>[vcount];
                for (int i = 0; i < vcount; i++)
                {
                    var p = Vector3.Transform(positions[i], worldXform);
                    var n = normals != null
                        ? Vector3.Normalize(Vector3.TransformNormal(normals[i], normalXform))
                        : Vector3.UnitY;
                    var uv = uvs != null ? uvs[i] : Vector2.Zero;
                    xv[i] = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                        new VertexPositionNormal(p, n),
                        new VertexTexture1(uv));
                }

                if (indices != null)
                {
                    for (int i = 0; i + 2 < indices.Count; i += 3)
                        prim.AddTriangle(xv[(int)indices[i]], xv[(int)indices[i + 1]], xv[(int)indices[i + 2]]);
                }
                else
                {
                    for (int i = 0; i + 2 < vcount; i += 3)
                        prim.AddTriangle(xv[i], xv[i + 1], xv[i + 2]);
                }
            }
        }

        // Magenta unlit thin-box marker for missing/failed walls. Placed at the
        // tile edge that matches the resolved orientation so the *intended*
        // edge is visible even when no GLB is present.
        private static void EmitMarkerBox(
            ref MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty> mb,
            float ax, float wy, float az, float h, TileOrientation kind)
        {
            mb ??= new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexTexture1, VertexEmpty>("missing_walls");
            var mat = new MaterialBuilder("missing_marker")
                .WithDoubleSide(true)
                .WithUnlitShader()
                .WithBaseColor(new Vector4(1f, 0f, 1f, 1f));
            var prim = mb.UsePrimitive(mat);

            // Box dimensions: thin slab along correct axis. Default = N/S facing
            // (length along world X). Rotated by kind via the anchor positions.
            float thick = 1f;
            float length = TILE;
            // For E/W walls the length runs along world Z; the box is oriented
            // X = thickness, Z = length.
            bool ewLength = kind == TileOrientation.WallEast || kind == TileOrientation.WallWest;
            float dx = ewLength ? thick : length;
            float dz = ewLength ? length : thick;

            float x0 = ax - dx * 0.5f, x1 = ax + dx * 0.5f;
            float z0 = az - dz * 0.5f, z1 = az + dz * 0.5f;
            float y0 = wy, y1 = wy + h;

            // 8 corners
            Vector3 c000 = new(x0, y0, z0), c100 = new(x1, y0, z0),
                    c010 = new(x0, y1, z0), c110 = new(x1, y1, z0),
                    c001 = new(x0, y0, z1), c101 = new(x1, y0, z1),
                    c011 = new(x0, y1, z1), c111 = new(x1, y1, z1);

            void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                var va = MakeV(a, n);
                var vb = MakeV(b, n);
                var vc = MakeV(c, n);
                var vd = MakeV(d, n);
                prim.AddTriangle(va, vb, vc);
                prim.AddTriangle(va, vc, vd);
            }
            Q(c000, c100, c110, c010, -Vector3.UnitZ);
            Q(c001, c011, c111, c101, Vector3.UnitZ);
            Q(c000, c010, c011, c001, -Vector3.UnitX);
            Q(c100, c101, c111, c110, Vector3.UnitX);
            Q(c000, c001, c101, c100, -Vector3.UnitY);
            Q(c010, c110, c111, c011, Vector3.UnitY);
        }

        private static VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>
            MakeV(Vector3 p, Vector3 n) =>
            new(new VertexPositionNormal(p, n), new VertexTexture1(Vector2.Zero));
    }
}
