// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — roof mesh drawer.
//
// Per-frame instance accumulator for RoofMeshRegistry-loaded GLBs. Mirrors
// WallMeshDrawer's solid+wireframe pass structure. Differences vs walls:
//
//   - Anchor is the tile center (not an edge midpoint).
//   - World matrix applies yaw only (pitch is baked into the canonical mesh).
//   - The mesh is canonical-per-shape (one of: roof_slope, roof_ridge,
//     roof_dent, roof_tpiece, roof_xpiece, roof_extra) and the per-archetype
//     orientation is recovered by RoofArchetypeMath.YawRadians.
//   - Atlas texture is *family-wide*, not per-mesh. We override the GLB's
//     embedded texture with the family atlas at draw time.
//   - Snow overlay re-submits the same instance with snow-tinted atlas.
//
// Mesh axis convention (matches roof manifest spec):
//   local +Y = up (slope rises toward +Y)
//   local +Z = the "downhill" cardinal of a SlopeS canonical mesh
//   local +X = perpendicular to slope direction
//   mesh AABB: x,z span [-0.5..+0.5] (one tile), y span [0..0.5] for a 26.57° pitch
//
// Y scale is taken from TileData.Height like walls, but for roofs this
// usually evaluates to a small number (roof tiles are thin slabs); the
// canonical mesh already encodes the correct slope so we keep Y scale = 1
// and just translate in Y by `wy + h`.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class RoofMeshDrawer
    {
        private const float MESH_SCALE_XZ = LandMesh3D.TILE; // 22f, one tile

        // Render-style toggles (driven by Debug3DGump, parallels WallMeshDrawer).
        public static bool Wireframe = false;
        public static bool ShadedWireframe = false;
        public static Vector3 WireColor = new Vector3(1f, 1f, 0f); // yellow, distinct from walls

        // Diagnostics
        public static int LastInstanceCount;
        public static int LastTrianglesDrawn;
        public static int LastSubmeshesDrawn;

        public struct Instance
        {
            public SkinnedModelGlb Model;
            public Texture2D AtlasOverride; // null = use mesh's embedded texture
            public Matrix World;
            public ushort Graphic;
            public RoofArchetype Archetype;
            public Vector3 AnchorWorld;
        }

        public static IReadOnlyList<Instance> LastInstances => _instances;

        private static readonly List<Instance> _instances = new(256);
        private static BasicEffect _effect;
        private static RasterizerState _wireRaster;
        private static RasterizerState _wireRasterBiased;

        public static void Begin()
        {
            _instances.Clear();
            LastInstanceCount = 0;
            LastTrianglesDrawn = 0;
            LastSubmeshesDrawn = 0;
        }

        public static void Submit(
            SkinnedModelGlb model,
            Texture2D atlas,
            ushort graphic,
            RoofArchetype archetype,
            float wx, float wy, float wz,
            float topOfTileWorldY)
        {
            if (model == null) return;
            float TILE = LandMesh3D.TILE;
            float ax = wx + TILE * 0.5f;
            float az = wz + TILE * 0.5f;
            float yaw = RoofArchetypeMath.YawRadians(archetype);

            var world =
                Matrix.CreateRotationY(yaw) *
                Matrix.CreateScale(MESH_SCALE_XZ, MESH_SCALE_XZ, MESH_SCALE_XZ) *
                Matrix.CreateTranslation(ax, topOfTileWorldY, az);

            _instances.Add(new Instance
            {
                Model = model,
                AtlasOverride = atlas,
                World = world,
                Graphic = graphic,
                Archetype = archetype,
                AnchorWorld = new Vector3(ax, topOfTileWorldY, az),
            });
        }

        // Submit a snow overlay instance: same world matrix as `baseInst`
        // shifted slightly along world +Y (good-enough approximation of the
        // surface normal for a 26.57° slope at prototype scope; flake noise
        // textures hide the small parallax).
        public static void SubmitSnowOverlay(in Instance baseInst, Texture2D snowAtlas, float yOffsetWorld)
        {
            if (snowAtlas == null) return;
            var world = baseInst.World * Matrix.CreateTranslation(0f, yOffsetWorld, 0f);
            _instances.Add(new Instance
            {
                Model = baseInst.Model,
                AtlasOverride = snowAtlas,
                World = world,
                Graphic = baseInst.Graphic,
                Archetype = baseInst.Archetype,
                AnchorWorld = baseInst.AnchorWorld + new Vector3(0f, yOffsetWorld, 0f),
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
            gd.SamplerStates[0] = SamplerState.LinearClamp;

            _effect.View = view;
            _effect.Projection = projection;

            bool drawSolid = !Wireframe;
            bool drawWire = Wireframe || ShadedWireframe;
            int submeshes = 0;
            int tris = 0;

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
                        var tex = inst.AtlasOverride ?? sub.Texture;
                        _effect.Texture = tex;
                        _effect.TextureEnabled = tex != null;
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
                        if (!drawSolid) { submeshes++; tris += sub.IndexCount / 3; }
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

        // GPU resources here are tracked by the renderer's IDisposableRegistry so they
        // release at shutdown (review finding #5). The Track helper is a no-op when the
        // host hasn't been bound yet (defensive — should not happen in production paths).
        private static T TrackIfBound<T>(T resource) where T : System.IDisposable
        {
            if (resource is not null && ClassicUO.Renderer.Core.Renderer3DHost.IsBound)
                ClassicUO.Renderer.Core.Renderer3DHost.Services.Disposables.Track(resource);
            return resource;
        }

        private static void EnsureRasterStates()
        {
            if (_wireRaster == null)
            {
                _wireRaster = TrackIfBound(new RasterizerState { FillMode = FillMode.WireFrame, CullMode = CullMode.None });
            }
            if (_wireRasterBiased == null)
            {
                _wireRasterBiased = TrackIfBound(new RasterizerState
                {
                    FillMode = FillMode.WireFrame,
                    CullMode = CullMode.None,
                    DepthBias = -0.0001f,
                    SlopeScaleDepthBias = -1f,
                });
            }
        }

        private static void EnsureEffect(GraphicsDevice gd)
        {
            if (_effect != null) return;
            _effect = TrackIfBound(new BasicEffect(gd)
            {
                LightingEnabled = false,
                VertexColorEnabled = false,
                TextureEnabled = true,
            });
        }
    }
}
