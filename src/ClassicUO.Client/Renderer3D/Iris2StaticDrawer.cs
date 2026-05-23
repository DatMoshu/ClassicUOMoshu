// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Iris 2 static GLB drawer.
// Per-frame instance accumulator for Iris2StaticRegistry-loaded GLBs. Static3DRenderer
// calls Begin() at the start of a frame, Submit() for each Static whose graphic resolves
// to an Iris 2 mesh (replacing the billboard), and Draw() at the end to flush.
//
// Statics here are pivot-at-base, axis-aligned (no per-orientation yaw like walls);
// we just translate to (cx, wy, cz) where (cx, cz) is tile center and wy = s.Z * Z_SCALE.
// XZ scale = LandMesh3D.TILE so a 1-unit mesh footprint covers one UO tile.
// Y scale targets tile_height * Z_SCALE world units (matches sprite-quad height).

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class Iris2StaticDrawer
    {
        private const float MESH_SCALE_XZ = LandMesh3D.TILE;     // 22f
        private const float Z_SCALE       = LandMesh3D.Z_SCALE;  // 4f
        // Default Y target if tile_height is 0 — matches typical wall (20 * 4 = 80).
        private const float DEFAULT_HEIGHT_WORLD = 80f;

        public static bool Wireframe       = false;
        public static bool ShadedWireframe = false;
        public static Vector3 WireColor    = new Vector3(0.1f, 1f, 0.3f); // green to distinguish from wall red

        // Global axis-fix rotations applied to every Iris 2 instance, in degrees.
        // Iris 2 source meshes were authored Z-up in Ogre and exported via Blender;
        // many land on their side or face-up in our Y-up world. Rather than re-bake
        // 1,991 GLBs, the live-tweaker lets us find the right global fix here, then
        // bake it into a converter pass once the user is satisfied. Order: Pitch (X)
        // → Yaw (Y) → Roll (Z) → Scale → Translate.
        public static float GlobalPitchDeg = 0f; // rotation around X
        public static float GlobalYawDeg   = 0f; // rotation around Y
        public static float GlobalRollDeg  = 0f; // rotation around Z

        // Fine-step for the per-axis "+step" buttons. Toggle between 90° (cardinal)
        // and 15° (fine) so the same buttons can do both coarse axis swaps and tuning.
        public static float YawStepDeg = 90f;

        public static int LastInstanceCount;
        public static int LastTrianglesDrawn;
        public static int LastSubmeshesDrawn;

        public struct Instance
        {
            public SkinnedModelGlb Model;
            public Matrix World;
            public ushort Graphic;
        }

        private static readonly List<Instance> _instances = new(256);
        // Frame-scoped guard so Multi3DRenderer and Static3DRenderer can both
        // defensively call Begin() without one wiping the other's submissions.
        // First Begin() of a frame clears; subsequent Begins until Draw() are no-ops.
        private static bool _began;
        private static BasicEffect _effect;
        private static RasterizerState _wireRaster;
        private static RasterizerState _wireRasterBiased;

        public static void Begin()
        {
            if (_began) return; // idempotent within a frame
            _began = true;
            _instances.Clear();
            LastInstanceCount = 0;
            LastTrianglesDrawn = 0;
            LastSubmeshesDrawn = 0;
        }

        public static void Submit(SkinnedModelGlb model, ushort graphic, int sX, int sY, int sZ, int tileHeight)
        {
            if (model == null) return;

            float TILE = LandMesh3D.TILE;
            float cx = sX * TILE + TILE * 0.5f;
            float cz = sY * TILE + TILE * 0.5f;
            float wy = sZ * Z_SCALE;

            // Global rotation fix applied before scale so the model's own axes stay
            // consistent across uniform-XZ / non-uniform-Y scaling.
            Matrix rot =
                Matrix.CreateRotationX(MathHelper.ToRadians(GlobalPitchDeg)) *
                Matrix.CreateRotationY(MathHelper.ToRadians(GlobalYawDeg)) *
                Matrix.CreateRotationZ(MathHelper.ToRadians(GlobalRollDeg));

            // Rotate the local AABB to get the rotated bounds, then derive Y scale
            // and base-lift from that. Otherwise a 90° pitch swings the body half
            // off the ground because we'd be measuring with the wrong "up".
            var b = model.Bounds;
            Span<Vector3> corners = stackalloc Vector3[8]
            {
                new Vector3(b.Min.X, b.Min.Y, b.Min.Z),
                new Vector3(b.Max.X, b.Min.Y, b.Min.Z),
                new Vector3(b.Min.X, b.Max.Y, b.Min.Z),
                new Vector3(b.Max.X, b.Max.Y, b.Min.Z),
                new Vector3(b.Min.X, b.Min.Y, b.Max.Z),
                new Vector3(b.Max.X, b.Min.Y, b.Max.Z),
                new Vector3(b.Min.X, b.Max.Y, b.Max.Z),
                new Vector3(b.Max.X, b.Max.Y, b.Max.Z),
            };
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var rc = Vector3.Transform(corners[i], rot);
                if (rc.Y < minY) minY = rc.Y;
                if (rc.Y > maxY) maxY = rc.Y;
            }
            float rotatedHeight = maxY - minY;

            float targetHeightWorld = tileHeight > 0 ? tileHeight * Z_SCALE : DEFAULT_HEIGHT_WORLD;
            float yScale = rotatedHeight > 0.0001f
                ? targetHeightWorld / rotatedHeight
                : MESH_SCALE_XZ;
            float baseLift = -minY * yScale;

            var world =
                rot *
                Matrix.CreateScale(MESH_SCALE_XZ, yScale, MESH_SCALE_XZ) *
                Matrix.CreateTranslation(cx, wy + baseLift, cz);

            _instances.Add(new Instance { Model = model, World = world, Graphic = graphic });
        }

        public static void Draw(GraphicsDevice gd, Matrix view, Matrix projection)
        {
            LastInstanceCount = _instances.Count;
            _began = false; // close the frame regardless of whether we draw
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
            bool drawWire  = Wireframe || ShadedWireframe;

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

        private static void EnsureRasterStates()
        {
            if (_wireRaster == null)
            {
                _wireRaster = new RasterizerState { FillMode = FillMode.WireFrame, CullMode = CullMode.None };
            }
            if (_wireRasterBiased == null)
            {
                _wireRasterBiased = new RasterizerState
                {
                    FillMode = FillMode.WireFrame,
                    CullMode = CullMode.None,
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
