// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — export a UO multi (e.g. "Two Story Wood and Plaster House"
// 0x76) to a self-contained .glb file, using the same axis-aligned-quad pipeline
// as Multi3DRenderer plus the same orientation lookup table.
//
// Output coordinates match the in-engine convention so the .glb opens at the
// same scale as the runtime mesh:
//   X = component_offsetX * 22
//   Y = component_offsetZ *  4
//   Z = component_offsetY * 22
//
// Each unique source graphic becomes one PBR material whose base color texture
// is the cropped sprite (from the UO art atlas) saved as PNG and embedded in
// the .glb. Texture filtering is set to NEAREST so the export reads exactly
// like the in-engine point-sampled view.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;
using XColor = Microsoft.Xna.Framework.Color;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class MultiGlbExporter
    {
        private const float TILE = 22f;
        private const float Z_SCALE = 4f;
        private const float UNKNOWN_DEFAULT_HEIGHT = 20f;

        public static string LastError;
        public static string LastOutputPath;

        // When the orientation table doesn't have an entry for a wall graphic,
        // fall back to WallSouth so the exported mesh isn't missing every wood/
        // plaster wall in custom houses. The result will be wrong half the time
        // (east walls placed on the south edge) but it's better than nothing
        // until the JSON is extended. Use the "Dump components" button to print
        // the unmapped graphic IDs you need to add.
        public static bool FallbackUnknownWallsToSouth = true;

        public sealed class Result
        {
            public bool Success;
            public string Path;
            public int ComponentCount;
            public int Materials;
            public int Triangles;
            public int Skipped;
            public string Error;
        }

        public static Result Export(GraphicsDevice gd, uint multiId, string outputPath)
        {
            var r = new Result { Path = outputPath };
            try
            {
                var loader = Client.Game.UO.FileManager.Multis;
                var components = loader.GetMultis(multiId);
                if (components == null || components.Count == 0)
                {
                    r.Error = $"multi 0x{multiId:X4} has no components";
                    LastError = r.Error;
                    return r;
                }

                // Per-graphic material cache and per-graphic mesh primitive cache.
                var materials = new Dictionary<ushort, MaterialBuilder>();
                var meshes = new Dictionary<ushort, MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>>();

                int triCount = 0;
                int skipped = 0;

                foreach (var comp in components)
                {
                    if (!comp.IsVisible) { skipped++; continue; }
                    ushort graphic = comp.ID;
                    if (graphic == 0) { skipped++; continue; }

                    ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(graphic);
                    if (artInfo.Texture == null) { skipped++; continue; }

                    ref var data = ref Client.Game.UO.FileManager.TileData.StaticData[graphic];
                    var orient = MultiOrientationTable.Resolve(graphic, ref data);
                    if (orient == TileOrientation.Corner)
                    {
                        skipped++;
                        continue;
                    }
                    if (orient == TileOrientation.Unknown)
                    {
                        // Heuristic fallback for walls outside the JSON table —
                        // see comment on FallbackUnknownWallsToSouth.
                        if (FallbackUnknownWallsToSouth && data.IsWall)
                            orient = TileOrientation.WallSouth;
                        else
                        {
                            skipped++;
                            continue;
                        }
                    }

                    // Get-or-create material (unique sprite per graphic).
                    if (!materials.TryGetValue(graphic, out var mat))
                    {
                        var pngBytes = ExtractSpritePng(gd, artInfo.Texture, artInfo.UV);
                        if (pngBytes == null) { skipped++; continue; }
                        var image = ImageBuilder.From(new MemoryImage(pngBytes), $"tex_0x{graphic:X4}");
                        mat = new MaterialBuilder($"graphic_0x{graphic:X4}")
                            .WithMetallicRoughnessShader()
                            .WithBaseColor(image)
                            .WithAlpha(AlphaMode.MASK, 0.5f)
                            .WithDoubleSide(true);
                        // Make the metallic/roughness sensible for unlit-ish look.
                        mat.WithMetallicRoughness(0f, 1f);
                        materials[graphic] = mat;
                    }

                    if (!meshes.TryGetValue(graphic, out var mesh))
                    {
                        mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>(
                            $"mesh_0x{graphic:X4}");
                        meshes[graphic] = mesh;
                    }
                    var prim = mesh.UsePrimitive(mat);

                    // Component world position (relative to multi origin)
                    float wx = comp.X * TILE;
                    float wy = comp.Z * Z_SCALE;
                    float wz = comp.Y * TILE;
                    float h = data.Height > 0 ? data.Height * Z_SCALE : UNKNOWN_DEFAULT_HEIGHT;

                    Vector3 normal;
                    System.Numerics.Vector3 v0, v1, v2, v3;
                    switch (orient)
                    {
                        case TileOrientation.Floor:
                            // Horizontal quad on XZ plane facing +Y. Slight Y nudge
                            // matches the runtime so floors sit cleanly on land.
                            FloorVerts(wx, wy + 0.05f * Z_SCALE, wz, out v0, out v1, out v2, out v3);
                            normal = Vector3.Up;
                            break;
                        case TileOrientation.Roof:
                            FloorVerts(wx, wy + h, wz, out v0, out v1, out v2, out v3);
                            normal = Vector3.Up;
                            break;
                        case TileOrientation.WallSouth:
                            WallVerts(wx, wy, wz + TILE, TILE, 0f, h, out v0, out v1, out v2, out v3);
                            normal = new Vector3(0, 0, -1);
                            break;
                        case TileOrientation.WallNorth:
                            WallVerts(wx, wy, wz, TILE, 0f, h, out v0, out v1, out v2, out v3);
                            normal = new Vector3(0, 0, 1);
                            break;
                        case TileOrientation.WallEast:
                            WallVerts(wx + TILE, wy, wz, 0f, TILE, h, out v0, out v1, out v2, out v3);
                            normal = new Vector3(-1, 0, 0);
                            break;
                        case TileOrientation.WallWest:
                            WallVerts(wx, wy, wz, 0f, TILE, h, out v0, out v1, out v2, out v3);
                            normal = new Vector3(1, 0, 0);
                            break;
                        default:
                            skipped++;
                            continue;
                    }

                    var nrm = new System.Numerics.Vector3(normal.X, normal.Y, normal.Z);

                    var uTL = new System.Numerics.Vector2(0, 0);
                    var uTR = new System.Numerics.Vector2(1, 0);
                    var uBL = new System.Numerics.Vector2(0, 1);
                    var uBR = new System.Numerics.Vector2(1, 1);

                    // v0=TL, v1=TR, v2=BL, v3=BR (matches Multi3DRenderer.EmitQuad).
                    var TL = (new VertexPositionNormal(v0, nrm), new VertexTexture1(uTL));
                    var TR = (new VertexPositionNormal(v1, nrm), new VertexTexture1(uTR));
                    var BL = (new VertexPositionNormal(v2, nrm), new VertexTexture1(uBL));
                    var BR = (new VertexPositionNormal(v3, nrm), new VertexTexture1(uBR));

                    prim.AddTriangle(TL, TR, BL);
                    prim.AddTriangle(BL, TR, BR);
                    triCount += 2;
                }

                if (meshes.Count == 0)
                {
                    r.Error = "no exportable components (every component fell through to Unknown/Corner or had no texture)";
                    LastError = r.Error;
                    return r;
                }

                var scene = new SceneBuilder($"multi_0x{multiId:X4}");
                foreach (var kv in meshes)
                {
                    scene.AddRigidMesh(kv.Value, System.Numerics.Matrix4x4.Identity);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var model = scene.ToGltf2();
                model.SaveGLB(outputPath);

                r.Success = true;
                r.ComponentCount = components.Count;
                r.Materials = materials.Count;
                r.Triangles = triCount;
                r.Skipped = skipped;
                LastOutputPath = outputPath;
                LastError = null;
                return r;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                LastError = ex.ToString();
                Console.WriteLine($"[3DCUO] MultiGlbExporter failed: {ex}");
                return r;
            }
        }

        // Floor: 4 corners of the tile in world coords. Order: TL, TR, BL, BR
        // (north-west, north-east, south-west, south-east) — matches
        // Multi3DRenderer.EmitFloor.
        private static void FloorVerts(float wx, float wy, float wz,
            out System.Numerics.Vector3 v0,
            out System.Numerics.Vector3 v1,
            out System.Numerics.Vector3 v2,
            out System.Numerics.Vector3 v3)
        {
            v0 = new System.Numerics.Vector3(wx,        wy, wz);          // NW
            v1 = new System.Numerics.Vector3(wx + TILE, wy, wz);          // NE
            v2 = new System.Numerics.Vector3(wx,        wy, wz + TILE);   // SW
            v3 = new System.Numerics.Vector3(wx + TILE, wy, wz + TILE);   // SE
        }

        // Wall: vertical quad anchored at (wx, wy, wz), extending by (sideX, sideZ)
        // along the tile edge and by h vertically. Order: TL, TR, BL, BR.
        private static void WallVerts(
            float wx, float wy, float wz, float sideX, float sideZ, float h,
            out System.Numerics.Vector3 v0,
            out System.Numerics.Vector3 v1,
            out System.Numerics.Vector3 v2,
            out System.Numerics.Vector3 v3)
        {
            v2 = new System.Numerics.Vector3(wx,                 wy,     wz);                  // BL
            v3 = new System.Numerics.Vector3(wx + sideX,         wy,     wz + sideZ);          // BR
            v0 = new System.Numerics.Vector3(wx,                 wy + h, wz);                  // TL
            v1 = new System.Numerics.Vector3(wx + sideX,         wy + h, wz + sideZ);          // TR
        }

        // Crop the source atlas to the sprite's UV rect, encode as PNG, return bytes.
        private static byte[] ExtractSpritePng(GraphicsDevice gd, Texture2D src, Rectangle rect)
        {
            try
            {
                if (src == null || rect.Width <= 0 || rect.Height <= 0) return null;

                var pixels = new XColor[rect.Width * rect.Height];
                src.GetData(0, rect, pixels, 0, pixels.Length);

                using var sub = new Texture2D(gd, rect.Width, rect.Height);
                sub.SetData(pixels);

                using var ms = new MemoryStream();
                sub.SaveAsPng(ms, rect.Width, rect.Height);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] ExtractSpritePng({rect}): {ex.Message}");
                return null;
            }
        }
    }
}
