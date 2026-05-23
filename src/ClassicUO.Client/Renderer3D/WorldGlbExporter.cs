// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — exports a textured ground heightmap to a glTF binary
// (.glb) for inspection in Blender / glTF viewers.
//
// Uses SharpGLTF.Toolkit's fluent SceneBuilder/MeshBuilder API so we don't
// hand-roll the glb file format.
//
// Output sampler is forced to NEAREST/NEAREST so Blender shows pixel-art
// UO textures correctly (no atlas-bleed checker artifacts from bilinear
// filtering across the unpadded CUO atlas).
//
// Bound by a tile-radius around the player; a full Felucca facet is
// ~30M tiles which would not fit in one glb.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;
using World = ClassicUO.Game.World;
using Microsoft.Xna.Framework.Graphics;

using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using XnaTexture2D = Microsoft.Xna.Framework.Graphics.Texture2D;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class WorldGlbExporter
    {
        // Radius in TILES around the player. 256 → 512x512 area.
        public static int RadiusTiles = 256;
        public static bool IncludeTextures = true;
        public static bool IncludeStatics  = true;
        public static bool IncludeMultis   = true;

        // Full-world export tunables. Defaults bound to a large square around
        // the player rather than a full Felucca facet — at 22f/tile the entire
        // 6144x4096 map is too much geometry for one Blender import session.
        public static int FullWorldRadiusTiles = 2048;
        public static int FullWorldBlockTiles  = 256;

        // When true, multi/static graphics that resolve to a WallMeshRegistry
        // entry skip the crossed-billboard quad and record a placement entry
        // in the sidecar manifest. The companion Blender script imports those
        // GLBs at their world transforms so the result is real geometry, not
        // billboards.
        public static bool ExternalizeWallMeshes = true;
        // Pixel→world scale for static billboards (TILE / 44px screen tile).
        // ART_TO_WORLD = 0.5; matches LandMesh3D.TILE=22 vs UO 44px tile width.
        public const float ART_TO_WORLD = 0.5f;
        public static string LastError;
        public static string LastOutputPath;

        // Vertex format: Position + UV (no normal/color — flat unlit).
        // SharpGLTF's stock VertexPositionTexture works.
        private static readonly VertexBuilder<VertexPosition, VertexTexture1, VertexEmpty>[]
            _vertexCornerScratch = new VertexBuilder<VertexPosition, VertexTexture1, VertexEmpty>[4];

        public static string Export(World world, GraphicsDevice gd, string outPath)
        {
            LastError = null; LastOutputPath = null;

            if (world?.Player == null || world.Map == null)
            { LastError = "no world / no player"; return null; }

            int pcx = world.Player.X;
            int pcy = world.Player.Y;
            int minTX = Math.Max(0, pcx - RadiusTiles), maxTX = pcx + RadiusTiles;
            int minTY = Math.Max(0, pcy - RadiusTiles), maxTY = pcy + RadiusTiles;

            var placements = new JsonArray();
            var result = ExportRegion(world, gd, outPath, minTX, maxTX, minTY, maxTY, placements);
            if (result == null) return null;

            try
            {
                string jsonPath = Path.ChangeExtension(outPath, ".json");
                WriteSidecarJson(world, jsonPath, minTX, maxTX, minTY, maxTY,
                    result.Value.tiles, result.Value.statics, result.Value.multis, placements);
                Console.WriteLine($"[3DCUO][export] sidecar JSON: {jsonPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO][export] sidecar JSON FAILED: {ex.Message}");
            }

            LastOutputPath = outPath;
            return outPath;
        }

        // Exports the entire reachable world (bounded by FullWorldRadiusTiles
        // around the player) as a grid of FullWorldBlockTiles-sized GLB files
        // under outDir. Writes a master manifest.json with one entry per block
        // (origin, file) plus all wall-mesh placements. The companion Blender
        // script reads this manifest and arranges every chunk + mesh in-place.
        public static string ExportFullWorld(World world, GraphicsDevice gd, string outDir)
        {
            LastError = null; LastOutputPath = null;

            if (world?.Player == null || world.Map == null)
            { LastError = "no world / no player"; return null; }

            try { Directory.CreateDirectory(outDir); }
            catch (Exception ex) { LastError = $"mkdir failed: {ex.Message}"; return null; }

            int pcx = world.Player.X, pcy = world.Player.Y;
            int worldMinX = Math.Max(0, pcx - FullWorldRadiusTiles);
            int worldMinY = Math.Max(0, pcy - FullWorldRadiusTiles);
            int worldMaxX = pcx + FullWorldRadiusTiles;
            int worldMaxY = pcy + FullWorldRadiusTiles;

            int block = Math.Max(64, FullWorldBlockTiles);
            int blocksX = (worldMaxX - worldMinX + block) / block;
            int blocksY = (worldMaxY - worldMinY + block) / block;

            Console.WriteLine($"[3DCUO][export-world] bounds tiles {worldMinX}..{worldMaxX} x {worldMinY}..{worldMaxY}, block={block}, grid={blocksX}x{blocksY}");

            var blockArr = new JsonArray();
            var placements = new JsonArray();
            int totalTiles = 0, totalStatics = 0, totalMultis = 0, blocksWritten = 0;

            for (int by = 0; by < blocksY; by++)
            for (int bx = 0; bx < blocksX; bx++)
            {
                int minTX = worldMinX + bx * block;
                int minTY = worldMinY + by * block;
                int maxTX = Math.Min(worldMaxX, minTX + block - 1);
                int maxTY = Math.Min(worldMaxY, minTY + block - 1);

                string fileName = $"block_{bx:D3}_{by:D3}.glb";
                string outPath = Path.Combine(outDir, fileName);

                var r = ExportRegion(world, gd, outPath, minTX, maxTX, minTY, maxTY, placements);
                if (r == null)
                {
                    Console.WriteLine($"[3DCUO][export-world] block {bx},{by} skipped: {LastError}");
                    LastError = null;
                    continue;
                }

                totalTiles += r.Value.tiles;
                totalStatics += r.Value.statics;
                totalMultis += r.Value.multis;
                blocksWritten++;

                blockArr.Add(new JsonObject
                {
                    ["bx"] = bx,
                    ["by"] = by,
                    ["file"] = fileName,
                    ["minTileX"] = minTX,
                    ["maxTileX"] = maxTX,
                    ["minTileY"] = minTY,
                    ["maxTileY"] = maxTY,
                    ["meshOriginX"] = minTX * LandMesh3D.TILE,
                    ["meshOriginZ"] = minTY * LandMesh3D.TILE,
                    ["tilesEmitted"] = r.Value.tiles,
                    ["staticsEmitted"] = r.Value.statics,
                    ["multisEmitted"] = r.Value.multis,
                });
            }

            var master = new JsonObject
            {
                ["timestamp"] = DateTime.Now.ToString("O"),
                ["coordConvention"] = new JsonObject
                {
                    ["TILE"] = LandMesh3D.TILE,
                    ["Z_SCALE"] = LandMesh3D.Z_SCALE,
                    ["meshFromUO"] = "meshX = X*TILE, meshY = Z*Z_SCALE, meshZ = Y*TILE (Y-up)"
                },
                ["bounds"] = new JsonObject
                {
                    ["minTileX"] = worldMinX,
                    ["maxTileX"] = worldMaxX,
                    ["minTileY"] = worldMinY,
                    ["maxTileY"] = worldMaxY,
                    ["blockTiles"] = block,
                    ["blocksX"] = blocksX,
                    ["blocksY"] = blocksY
                },
                ["player"] = new JsonObject
                {
                    ["x"] = world.Player.X,
                    ["y"] = world.Player.Y,
                    ["z"] = world.Player.Z
                },
                ["wallMeshesDir"] = WallMeshRegistry.MeshesDir,
                ["totals"] = new JsonObject
                {
                    ["blocksWritten"] = blocksWritten,
                    ["tiles"] = totalTiles,
                    ["statics"] = totalStatics,
                    ["multis"] = totalMultis,
                    ["meshPlacements"] = placements.Count,
                },
                ["blocks"] = blockArr,
                ["meshPlacements"] = placements
            };

            string manifestPath = Path.Combine(outDir, "manifest.json");
            File.WriteAllText(manifestPath,
                master.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine($"[3DCUO][export-world] wrote {blocksWritten} blocks, {totalTiles} tiles, {totalStatics} statics, {totalMultis} multis (billboard), {placements.Count} mesh placements -> {manifestPath}");

            LastOutputPath = manifestPath;
            return manifestPath;
        }

        // Build + save one bounded region. Caller decides the bounds and the
        // output path, plus a shared placements JsonArray that gets appended
        // for every wall-mesh-backed multi/static encountered. Returns null
        // and sets LastError when nothing was emitted (e.g. unloaded chunks).
        private static (int tiles, int statics, int multis)? ExportRegion(
            World world, GraphicsDevice gd, string outPath,
            int minTX, int maxTX, int minTY, int maxTY,
            JsonArray placements)
        {
            int minCX = minTX >> 3, minCY = minTY >> 3;
            int maxCX = maxTX >> 3, maxCY = maxTY >> 3;

            var meshBuilders =
                new Dictionary<XnaTexture2D, MeshBuilder<MaterialBuilder, VertexPosition, VertexTexture1, VertexEmpty>>();
            int tilesEmitted = BuildGeometry(world, minTX, maxTX, minTY, maxTY, minCX, maxCX, minCY, maxCY, meshBuilders);
            if (tilesEmitted == 0) { LastError = "no tiles emitted"; return null; }

            int staticsEmitted = 0, multisEmitted = 0;
            if (IncludeStatics || IncludeMultis)
            {
                BuildStaticGeometry(world, minTX, maxTX, minTY, maxTY, minCX, maxCX, minCY, maxCY,
                    meshBuilders, placements, out staticsEmitted, out multisEmitted);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");

                var scene = new SceneBuilder("UO World");
                foreach (var (tex, mb) in meshBuilders)
                    scene.AddRigidMesh(mb, Matrix4x4.Identity);

                var model = scene.ToGltf2();
                ForceNearestSamplers(model);
                model.SaveGLB(outPath);

                long sz = new FileInfo(outPath).Length;
                Console.WriteLine($"[3DCUO][export] wrote {outPath} ({sz:N0} bytes) — {tilesEmitted} tiles, {staticsEmitted} statics, {multisEmitted} multis");

                return (tilesEmitted, staticsEmitted, multisEmitted);
            }
            catch (Exception ex)
            {
                LastError = $"save failed: {ex.Message}";
                Console.WriteLine($"[3DCUO][export] {LastError}");
                return null;
            }
        }

        private static int BuildGeometry(
            World world,
            int minTX, int maxTX, int minTY, int maxTY,
            int minCX, int maxCX, int minCY, int maxCY,
            Dictionary<XnaTexture2D, MeshBuilder<MaterialBuilder, VertexPosition, VertexTexture1, VertexEmpty>> meshes)
        {
            const float TILE = LandMesh3D.TILE;
            const float ZS   = LandMesh3D.Z_SCALE;
            int emitted = 0;

            // Cache materials per texture so we only embed each PNG once.
            var materials = new Dictionary<XnaTexture2D, MaterialBuilder>();

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
                    if (worldX < minTX || worldX > maxTX || worldY < minTY || worldY > maxTY) continue;

                    Land land = null;
                    for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                        if (obj is Land l) { land = l; break; }
                    if (land == null) continue;

                    if (!ResolveTexture(land, out var tex, out var uv, out bool isDiamond)) continue;

                    // Half-pixel UV inset (matches Batcher2D.CalculateHalfPixelUVs).
                    float invW = 1f / tex.Width, invH = 1f / tex.Height;
                    float uX = (uv.X + 0.5f) * invW;
                    float uY = (uv.Y + 0.5f) * invH;
                    float uW = (uv.Width  - 1f) * invW;
                    float uH = (uv.Height - 1f) * invH;

                    // Per-corner UVs. Diamond art tiles must rotate UVs so the
                    // mesh corners land on diamond points, not the transparent
                    // square corners.
                    float uTopX, uTopY, uRightX, uRightY, uLeftX, uLeftY, uBotX, uBotY;
                    if (isDiamond)
                    {
                        float midX = uX + uW * 0.5f, midY = uY + uH * 0.5f;
                        uTopX = midX;    uTopY = uY;
                        uRightX = uX + uW; uRightY = midY;
                        uLeftX = uX;     uLeftY = midY;
                        uBotX = midX;    uBotY = uY + uH;
                    }
                    else
                    {
                        uTopX = uX;        uTopY = uY;
                        uRightX = uX + uW; uRightY = uY;
                        uLeftX = uX;       uLeftY = uY + uH;
                        uBotX = uX + uW;   uBotY = uY + uH;
                    }

                    float hT, hR, hL, hB;
                    if (land.IsStretched)
                    {
                        hT = land.YOffsets.Top; hR = land.YOffsets.Right;
                        hL = land.YOffsets.Left; hB = land.YOffsets.Bottom;
                    }
                    else { float h = land.Z * ZS; hT = hR = hL = hB = h; }

                    float wx = worldX * TILE, wz = worldY * TILE;

                    // Get/create the material for this texture.
                    if (!materials.TryGetValue(tex, out var mat))
                    {
                        mat = BuildMaterial(tex, materials.Count);
                        materials[tex] = mat;
                    }

                    // Get/create the mesh-builder for this texture.
                    if (!meshes.TryGetValue(tex, out var mb))
                    {
                        mb = new MeshBuilder<MaterialBuilder, VertexPosition, VertexTexture1, VertexEmpty>(
                            $"ground_{meshes.Count}");
                        meshes[tex] = mb;
                    }

                    var prim = mb.UsePrimitive(mat);

                    // Build vertices: top, right, left, bottom (diamond UVs when needed).
                    var vTop    = NewV(wx,        hT, wz,        uTopX,   uTopY);
                    var vRight  = NewV(wx + TILE, hR, wz,        uRightX, uRightY);
                    var vLeft   = NewV(wx,        hL, wz + TILE, uLeftX,  uLeftY);
                    var vBottom = NewV(wx + TILE, hB, wz + TILE, uBotX,   uBotY);

                    // Triangles: (top,left,right) (right,left,bottom) — face up.
                    prim.AddTriangle(vTop, vLeft, vRight);
                    prim.AddTriangle(vRight, vLeft, vBottom);
                    emitted++;
                }
            }

            return emitted;
        }

        // Statics + multis: emit two crossed upright billboard quads per tile-object,
        // textured with the art sprite. Anchor: bottom-center sits on the static's
        // (X,Y,Z) tile in mesh space (Y-up). Width/height come from the art's UV
        // box, scaled by ART_TO_WORLD so screen-px ↔ world units stay consistent
        // with the ground tiles.
        private static void BuildStaticGeometry(
            World world,
            int minTX, int maxTX, int minTY, int maxTY,
            int minCX, int maxCX, int minCY, int maxCY,
            Dictionary<XnaTexture2D, MeshBuilder<MaterialBuilder, VertexPosition, VertexTexture1, VertexEmpty>> meshes,
            JsonArray placements,
            out int staticsOut, out int multisOut)
        {
            const float TILE = LandMesh3D.TILE;
            const float ZS   = LandMesh3D.Z_SCALE;
            staticsOut = 0; multisOut = 0;

            if (ExternalizeWallMeshes) WallMeshRegistry.EnsureLoaded();

            var materials = new Dictionary<XnaTexture2D, MaterialBuilder>();

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
                    if (worldX < minTX || worldX > maxTX || worldY < minTY || worldY > maxTY) continue;

                    for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                    {
                        if (obj == null || obj.IsDestroyed) continue;

                        ushort graphic;
                        bool isMulti;
                        if (obj is Static s)        { if (!IncludeStatics) continue; graphic = s.Graphic; isMulti = false; }
                        else if (obj is Multi m)    { if (!IncludeMultis)  continue; graphic = m.Graphic; isMulti = true;  }
                        else continue;

                        // If a real wall-mesh GLB exists for this graphic, record a
                        // placement (graphic, world tile, mesh-space transform, glb file)
                        // for the Blender script to import — and skip the billboard quad.
                        if (ExternalizeWallMeshes && placements != null &&
                            WallMeshRegistry.TryGet(graphic, out var meshEntry))
                        {
                            placements.Add(new JsonObject
                            {
                                ["graphic"] = $"0x{graphic:X4}",
                                ["graphicId"] = (int)graphic,
                                ["isMulti"] = isMulti,
                                ["worldX"] = obj.X,
                                ["worldY"] = obj.Y,
                                ["worldZ"] = (int)obj.Z,
                                ["meshX"] = (worldX + 0.5f) * TILE,
                                ["meshY"] = obj.Z * ZS,
                                ["meshZ"] = (worldY + 0.5f) * TILE,
                                ["archetype"] = meshEntry.Archetype,
                                ["glbFile"] = meshEntry.File,
                            });
                            if (isMulti) multisOut++; else staticsOut++;
                            continue;
                        }

                        ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(graphic);
                        var tex = artInfo.Texture;
                        if (tex == null) continue;
                        var uv = artInfo.UV;
                        if (uv.Width <= 0 || uv.Height <= 0) continue;

                        float invW = 1f / tex.Width, invH = 1f / tex.Height;
                        float uX = (uv.X + 0.5f) * invW;
                        float uY = (uv.Y + 0.5f) * invH;
                        float uW = (uv.Width  - 1f) * invW;
                        float uH = (uv.Height - 1f) * invH;

                        // Material per texture (shared with ground pass via local cache).
                        if (!materials.TryGetValue(tex, out var mat))
                        {
                            mat = BuildMaterial(tex, materials.Count);
                            materials[tex] = mat;
                        }
                        if (!meshes.TryGetValue(tex, out var mb))
                        {
                            mb = new MeshBuilder<MaterialBuilder, VertexPosition, VertexTexture1, VertexEmpty>(
                                $"{(isMulti ? "multi" : "static")}_{meshes.Count}");
                            meshes[tex] = mb;
                        }
                        var prim = mb.UsePrimitive(mat);

                        float w = uv.Width  * ART_TO_WORLD;
                        float h = uv.Height * ART_TO_WORLD;

                        // Mesh-space anchor: tile center, with bottom of quad at Z*ZS.
                        float cxw = (worldX + 0.5f) * TILE;
                        float czw = (worldY + 0.5f) * TILE;
                        float yBot = obj.Z * ZS;
                        float yTop = yBot + h;
                        float halfW = w * 0.5f;

                        // Quad 1 — runs along X axis (faces ±Z).
                        var a1 = NewV(cxw - halfW, yTop, czw, uX,        uY);
                        var b1 = NewV(cxw + halfW, yTop, czw, uX + uW,   uY);
                        var c1 = NewV(cxw - halfW, yBot, czw, uX,        uY + uH);
                        var d1 = NewV(cxw + halfW, yBot, czw, uX + uW,   uY + uH);
                        prim.AddTriangle(a1, c1, b1);
                        prim.AddTriangle(b1, c1, d1);

                        // Quad 2 — runs along Z axis (faces ±X). Crossed billboard
                        // so the static reads from any camera angle in Blender.
                        var a2 = NewV(cxw, yTop, czw - halfW, uX,        uY);
                        var b2 = NewV(cxw, yTop, czw + halfW, uX + uW,   uY);
                        var c2 = NewV(cxw, yBot, czw - halfW, uX,        uY + uH);
                        var d2 = NewV(cxw, yBot, czw + halfW, uX + uW,   uY + uH);
                        prim.AddTriangle(a2, c2, b2);
                        prim.AddTriangle(b2, c2, d2);

                        if (isMulti) multisOut++; else staticsOut++;
                    }
                }
            }
        }

        private static bool ResolveTexture(Land land, out XnaTexture2D tex, out XnaRectangle uv, out bool isDiamond)
        {
            if (land.IsStretched)
            {
                ref readonly var info = ref Client.Game.UO.Texmaps.GetTexmap(
                    Client.Game.UO.FileManager.TileData.LandData[land.Graphic].TexID);
                if (info.Texture != null) { tex = info.Texture; uv = info.UV; isDiamond = false; return true; }
            }
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetLand(land.Graphic);
            if (artInfo.Texture != null) { tex = artInfo.Texture; uv = artInfo.UV; isDiamond = true; return true; }
            tex = null; uv = default; isDiamond = false; return false;
        }

        private static VertexBuilder<VertexPosition, VertexTexture1, VertexEmpty>
            NewV(float x, float y, float z, float u, float v)
        {
            return new VertexBuilder<VertexPosition, VertexTexture1, VertexEmpty>(
                new VertexPosition(new Vector3(x, y, z)),
                new VertexTexture1(new Vector2(u, v)));
        }

        private static MaterialBuilder BuildMaterial(XnaTexture2D tex, int idx)
        {
            // MASK alpha: UO PNGs come out RGBA with index-0 keyed transparent.
            // OPAQUE (the default) ignores alpha and Blender shows black backgrounds
            // on every billboard quad and on the diamond corners of ground tiles.
            var mat = new MaterialBuilder($"mat_{idx}")
                .WithDoubleSide(true)
                .WithAlpha(SharpGLTF.Materials.AlphaMode.MASK, 0.5f)
                .WithMetallicRoughnessShader()
                .WithMetallicRoughness(0.0f, 1.0f);

            if (IncludeTextures && tex != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    tex.SaveAsPng(ms, tex.Width, tex.Height);
                    var pngBytes = ms.ToArray();
                    mat.WithChannelImage(KnownChannel.BaseColor, pngBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[3DCUO][export] tex {idx} embed failed: {ex.Message}");
                }
            }
            return mat;
        }

        private static void WriteSidecarJson(World world,
            string jsonPath,
            int minTX, int maxTX, int minTY, int maxTY,
            int tilesEmitted, int staticsEmitted, int multisEmitted,
            JsonArray placements)
        {
            int minCX = minTX >> 3, minCY = minTY >> 3;
            int maxCX = maxTX >> 3, maxCY = maxTY >> 3;
            const float TILE = LandMesh3D.TILE;
            const float ZS = LandMesh3D.Z_SCALE;
            WallMeshRegistry.EnsureLoaded();

            var statics = new JsonArray();
            var multis  = new JsonArray();
            for (int cy = minCY; cy <= maxCY; cy++)
            for (int cx = minCX; cx <= maxCX; cx++)
            {
                Chunk chunk;
                try { chunk = world.Map.GetChunk2(cx, cy, load: false); }
                catch { continue; }
                if (chunk == null) continue;

                int chunkOriginX = chunk.X * 8;
                int chunkOriginY = chunk.Y * 8;

                for (int ty = 0; ty < 8; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    int worldX = chunkOriginX + tx;
                    int worldY = chunkOriginY + ty;
                    if (worldX < minTX || worldX > maxTX || worldY < minTY || worldY > maxTY) continue;

                    for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                    {
                        if (obj == null || obj.IsDestroyed) continue;

                        ushort gfx;
                        bool isMulti;
                        if (obj is Static so)     { gfx = so.Graphic; isMulti = false; }
                        else if (obj is Multi mo) { gfx = mo.Graphic; isMulti = true;  }
                        else continue;

                        ref var data = ref Client.Game.UO.FileManager.TileData.StaticData[gfx];
                        var kind = MultiOrientationTable.Resolve(gfx, ref data);

                        string archetype = null, file = null;
                        if (WallMeshRegistry.TryGet(gfx, out var entry))
                        {
                            archetype = entry.Archetype;
                            file = entry.File;
                        }

                        var entryJson = new JsonObject
                        {
                            ["graphic"] = $"0x{gfx:X4}",
                            ["graphicId"] = (int)gfx,
                            ["worldX"] = obj.X,
                            ["worldY"] = obj.Y,
                            ["worldZ"] = (int)obj.Z,
                            ["meshX"] = obj.X * TILE,
                            ["meshY"] = obj.Z * ZS,
                            ["meshZ"] = obj.Y * TILE,
                            ["resolvedKind"] = kind.ToString(),
                            ["manifestArchetype"] = archetype,
                            ["glbFile"] = file,
                            ["height"] = (int)data.Height,
                            ["isWall"] = data.IsWall,
                            ["isSurface"] = data.IsSurface,
                            ["isRoof"] = data.IsRoof,
                            ["isImpassable"] = data.IsImpassable
                        };
                        if (isMulti) multis.Add(entryJson); else statics.Add(entryJson);
                    }
                }
            }

            var payload = new JsonObject
            {
                ["timestamp"] = DateTime.Now.ToString("O"),
                ["bounds"] = new JsonObject
                {
                    ["minTileX"] = minTX,
                    ["maxTileX"] = maxTX,
                    ["minTileY"] = minTY,
                    ["maxTileY"] = maxTY
                },
                ["coordConvention"] = new JsonObject
                {
                    ["TILE"] = TILE,
                    ["Z_SCALE"] = ZS,
                    ["meshFromUO"] = "meshX = X*TILE, meshY = Z*Z_SCALE, meshZ = Y*TILE (Y-up)"
                },
                ["player"] = new JsonObject
                {
                    ["x"] = world.Player.X,
                    ["y"] = world.Player.Y,
                    ["z"] = world.Player.Z
                },
                ["meshesDir"] = WallMeshRegistry.MeshesDir,
                ["terrainTilesEmitted"] = tilesEmitted,
                ["staticsEmitted"] = staticsEmitted,
                ["multisEmitted"] = multisEmitted,
                ["staticCount"] = statics.Count,
                ["multiCount"] = multis.Count,
                ["meshPlacementCount"] = placements?.Count ?? 0,
                ["statics"] = statics,
                ["multis"] = multis,
                ["meshPlacements"] = placements ?? new JsonArray()
            };

            File.WriteAllText(jsonPath,
                payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void ForceNearestSamplers(ModelRoot model)
        {
            // Set every texture's sampler to NEAREST/NEAREST (no mipmap, no filter).
            // CUO's atlases pack tiles with no padding; bilinear filtering at sub-rect
            // edges samples into neighboring cells, producing checker-like bleed.
            var sampler = model.UseTextureSampler(
                TextureWrapMode.CLAMP_TO_EDGE,
                TextureWrapMode.CLAMP_TO_EDGE,
                TextureMipMapFilter.NEAREST,
                TextureInterpolationFilter.NEAREST);
            foreach (var t in model.LogicalTextures)
            {
                t.Sampler = sampler;
            }
        }
    }
}
