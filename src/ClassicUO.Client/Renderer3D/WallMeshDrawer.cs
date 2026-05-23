// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — wall mesh drawer.
// Per-frame instance accumulator for WallMeshRegistry-loaded GLBs. Multi3DRenderer
// calls Begin() at the start of a frame, Submit() per wall component (replacing the
// flat sprite-quad path), and Draw() at the end to flush every instance.
//
// Mesh axis convention (current export 2026-05-02_221419+, manifest declares
// "+Y up (glTF default)" — Blender exported with Z-up→Y-up conversion):
//   local +X = wall thickness (~0.4 m)
//   local +Y = wall height    (~2.83 m, base at Y=0)
//   local +Z = wall length    (1.0 m = 1 UO tile, centered: -0.5 .. +0.5)
// No axis-fix rotation needed — height already lands on world +Y, length on
// world +Z. Per-orientation Y rotation swings length onto the correct tile edge.
// (Older 150440 export was Blender-native Z-up and required RotX(-90°); the new
// pipeline includes the Y-up conversion so we drop that step.)

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class WallMeshDrawer
    {
        // XZ scale = world units per tile. Y scale is derived per-instance from
        // TileData.Height so GLB walls match the sprite-quad path exactly:
        //   target world height = data.Height * Z_SCALE  (4 wu per UO Z unit)
        //   Y scale = target / mesh body height (read from GLB Bounds AABB)
        // Old hard-coded MESH_SCALE_Y=30 over-scaled every wall by ~6%, accumulating
        // gaps and causing second-story walls to poke through the roof.
        private const float MESH_SCALE_XZ = LandMesh3D.TILE; // 22f

        // Render-style toggles (driven by Debug3DGump). Wireframe wins over
        // ShadedWireframe when both are on (pure-wireframe = simpler debug view).
        public static bool Wireframe = false;
        public static bool ShadedWireframe = false;
        public static Vector3 WireColor = new Vector3(1f, 0f, 0f); // red

        // Corner-subtype yaw rotations in degrees. Live-tweakable from the
        // wall mesh gump because the canonical corner GLB's L-quadrant depends
        // on Blender's Y-up axis-conversion direction (different exporters
        // produce mirrored output) — empirically iterated rather than derived.
        // Defaults reflect the user's "NE looks correct" observation: NE = 90°,
        // others rotated 90° clockwise from there.
        public static float CornerYawNW_Deg = 180f;
        public static float CornerYawNE_Deg = 90f;
        public static float CornerYawSE_Deg = 0f;
        public static float CornerYawSW_Deg = 270f;

        // Diagnostics
        public static int LastInstanceCount;
        public static int LastTrianglesDrawn;
        public static int LastSubmeshesDrawn;

        public struct Instance
        {
            public SkinnedModelGlb Model;
            public Matrix World;
            public ushort Graphic;
            public TileOrientation Kind;
            public Vector3 AnchorWorld; // (ax, wy, az) before scale/rot
        }

        // Public so the dumper can iterate the last-frame submission.
        public static IReadOnlyList<Instance> LastInstances => _instances;

        private static readonly List<Instance> _instances = new(256);
        private static BasicEffect _effect;
        private static RasterizerState _wireRaster;
        // Slight depth bias so the wireframe overlay sits in front of the
        // textured fill pass without z-fighting on flat wall surfaces.
        private static RasterizerState _wireRasterBiased;

        public static void Begin()
        {
            _instances.Clear();
            LastInstanceCount = 0;
            LastTrianglesDrawn = 0;
            LastSubmeshesDrawn = 0;
        }

        // Default target height when no TileData is available. Matches the UO
        // standard wall (data.Height=20) * Z_SCALE=4 = 80 world units.
        private const float DEFAULT_HEIGHT_WORLD = 80f;

        public static void Submit(
            SkinnedModelGlb model,
            TileOrientation kind,
            float wx, float wy, float wz)
        {
            Submit(model, 0, kind, wx, wy, wz, DEFAULT_HEIGHT_WORLD);
        }

        public static void Submit(
            SkinnedModelGlb model,
            ushort graphic,
            TileOrientation kind,
            float wx, float wy, float wz)
        {
            Submit(model, graphic, kind, wx, wy, wz, DEFAULT_HEIGHT_WORLD);
        }

        public static void Submit(
            SkinnedModelGlb model,
            ushort graphic,
            TileOrientation kind,
            float wx, float wy, float wz,
            float targetHeightWorld)
        {
            if (model == null) return;

            // Tile-relative anchor based on resolved orientation. Tile occupies
            // world X = [wx..wx+TILE], Z = [wz..wz+TILE]. EmitWall* placements
            // (Multi3DRenderer.cs:311-357) define which edge each kind sits on.
            float TILE = LandMesh3D.TILE;
            float ax, az;       // anchor in world space (mesh local origin lands here)
            float yawRadians;   // additional Y rotation after the Z-up→Y-up fix

            switch (kind)
            {
                case TileOrientation.WallNorth:
                    ax = wx + TILE * 0.5f;
                    az = wz;
                    yawRadians = MathHelper.PiOver2;     // swing length (mesh +Z) onto world X
                    break;
                case TileOrientation.WallSouth:
                    ax = wx + TILE * 0.5f;
                    az = wz + TILE;
                    yawRadians = MathHelper.PiOver2;
                    break;
                case TileOrientation.WallEast:
                    ax = wx + TILE;
                    az = wz + TILE * 0.5f;
                    yawRadians = 0f;                     // mesh length already along world Z
                    break;
                case TileOrientation.WallWest:
                    ax = wx;
                    az = wz + TILE * 0.5f;
                    yawRadians = 0f;
                    break;
                // Corner subtypes — center anchor, per-quadrant yaw. Source
                // GLB's canonical L sits in the NW quadrant; rotate clockwise
                // by 90° per cardinal step. WallNeighborClassifier picks the
                // subtype from cardinal-neighbour wall presence.
                case TileOrientation.CornerNW:
                    ax = wx + TILE * 0.5f;
                    az = wz + TILE * 0.5f;
                    yawRadians = MathHelper.ToRadians(CornerYawNW_Deg);
                    break;
                case TileOrientation.CornerNE:
                    ax = wx + TILE * 0.5f;
                    az = wz + TILE * 0.5f;
                    yawRadians = MathHelper.ToRadians(CornerYawNE_Deg);
                    break;
                case TileOrientation.CornerSE:
                    ax = wx + TILE * 0.5f;
                    az = wz + TILE * 0.5f;
                    yawRadians = MathHelper.ToRadians(CornerYawSE_Deg);
                    break;
                case TileOrientation.CornerSW:
                    ax = wx + TILE * 0.5f;
                    az = wz + TILE * 0.5f;
                    yawRadians = MathHelper.ToRadians(CornerYawSW_Deg);
                    break;
                case TileOrientation.Corner:
                case TileOrientation.Floor:
                case TileOrientation.Roof:
                case TileOrientation.Unknown:
                default:
                    // Place ambiguous corner/pillar/unknown at tile center, no extra yaw.
                    ax = wx + TILE * 0.5f;
                    az = wz + TILE * 0.5f;
                    yawRadians = 0f;
                    break;
            }

            // World matrix: RotY(yaw) * NonUniformScale * Translate.
            // GLB is exported +Y-up so height is already on world +Y and length
            // is on world +Z. Y scale is computed from the mesh AABB so the
            // mesh top lands at exactly targetHeightWorld (== TileData.Height
            // * Z_SCALE), matching the sprite-quad height path 1:1.
            float bodyHeight = model.Bounds.Max.Y - model.Bounds.Min.Y;
            float yScale = bodyHeight > 0.0001f
                ? targetHeightWorld / bodyHeight
                : MESH_SCALE_XZ;

            var world =
                Matrix.CreateRotationY(yawRadians) *
                Matrix.CreateScale(MESH_SCALE_XZ, yScale, MESH_SCALE_XZ) *
                Matrix.CreateTranslation(ax, wy, az);

            _instances.Add(new Instance
            {
                Model = model,
                World = world,
                Graphic = graphic,
                Kind = kind,
                AnchorWorld = new Vector3(ax, wy, az),
            });
        }

        public static void Draw(GraphicsDevice gd, Matrix view, Matrix projection)
        {
            LastInstanceCount = _instances.Count;
            if (gd == null || _instances.Count == 0) return;

            EnsureEffect(gd);
            EnsureRasterStates();

            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;
            var prevSampler = gd.SamplerStates[0];

            gd.DepthStencilState = DepthStencilState.Default;
            gd.BlendState = BlendState.AlphaBlend;
            gd.SamplerStates[0] = SamplerState.LinearClamp; // GLB textures are baked, not pixel-art

            _effect.View = view;
            _effect.Projection = projection;

            // Pure-wireframe mode wins. Otherwise: solid (optional) + wire overlay.
            bool drawSolid = !Wireframe;          // skip fill in pure-wireframe
            bool drawWire = Wireframe || ShadedWireframe;

            int submeshes = 0;
            int tris = 0;

            // ── Pass 1: solid textured fill ───────────────────────────────
            if (drawSolid)
            {
                gd.RasterizerState = RasterizerState.CullNone;
                for (int i = 0; i < _instances.Count; i++)
                {
                    var inst = _instances[i];
                    _effect.World = inst.World;
                    foreach (var sub in inst.Model.Submeshes)
                    {
                        if (!sub.Visible) continue;
                        _effect.Texture = sub.Texture;
                        _effect.TextureEnabled = sub.Texture != null;
                        _effect.DiffuseColor = new Vector3(sub.BaseColorFactor.X, sub.BaseColorFactor.Y, sub.BaseColorFactor.Z);
                        _effect.Alpha = sub.BaseColorFactor.W;

                        gd.SetVertexBuffer(sub.Vertices);
                        gd.Indices = sub.Indices;
                        foreach (var pass in _effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.DrawIndexedPrimitives(
                                PrimitiveType.TriangleList, 0, 0,
                                sub.Vertices.VertexCount, 0, sub.IndexCount / 3);
                        }
                        submeshes++;
                        tris += sub.IndexCount / 3;
                    }
                }
            }

            // ── Pass 2: red wireframe overlay (or sole pass) ──────────────
            if (drawWire)
            {
                gd.RasterizerState = drawSolid ? _wireRasterBiased : _wireRaster;
                for (int i = 0; i < _instances.Count; i++)
                {
                    var inst = _instances[i];
                    _effect.World = inst.World;
                    _effect.TextureEnabled = false;
                    _effect.Texture = null;
                    _effect.DiffuseColor = WireColor;
                    _effect.Alpha = 1f;

                    foreach (var sub in inst.Model.Submeshes)
                    {
                        if (!sub.Visible) continue;
                        gd.SetVertexBuffer(sub.Vertices);
                        gd.Indices = sub.Indices;
                        foreach (var pass in _effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.DrawIndexedPrimitives(
                                PrimitiveType.TriangleList, 0, 0,
                                sub.Vertices.VertexCount, 0, sub.IndexCount / 3);
                        }
                        if (!drawSolid)
                        {
                            submeshes++;
                            tris += sub.IndexCount / 3;
                        }
                    }
                }
            }

            LastSubmeshesDrawn = submeshes;
            LastTrianglesDrawn = tris;

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
            gd.SamplerStates[0] = prevSampler;
        }

        private static void EnsureRasterStates()
        {
            if (_wireRaster == null)
            {
                _wireRaster = new RasterizerState
                {
                    FillMode = FillMode.WireFrame,
                    CullMode = CullMode.None,
                };
            }
            if (_wireRasterBiased == null)
            {
                _wireRasterBiased = new RasterizerState
                {
                    FillMode = FillMode.WireFrame,
                    CullMode = CullMode.None,
                    // Pull the wire pass slightly toward the camera so it draws
                    // on top of the solid fill without z-fighting.
                    DepthBias = -0.0001f,
                    SlopeScaleDepthBias = -1f,
                };
            }
        }

        private static void EnsureEffect(GraphicsDevice gd)
        {
            if (_effect != null) return;
            _effect = new BasicEffect(gd)
            {
                LightingEnabled = false,
                VertexColorEnabled = false,
                TextureEnabled = true,
            };
        }
    }
}
