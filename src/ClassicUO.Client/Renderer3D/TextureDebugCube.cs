// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — diagnostic textured cube rendered with FNA's stock
// BasicEffect, sampling the FIRST loaded attachment texture. Used to isolate
// whether the "white meshes" bug is in our custom TextureSkinnedEffect /
// shader, or in something deeper (FNA/GL state, driver, texture upload).
//
// If this cube shows real palette colors but the modular meshes don't,
// the bug is 100% in our custom shader. THROWAWAY DIAGNOSTIC.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    public static class TextureDebugCube
    {
        public static bool Enabled = false;

        // World-space anchor near the player so it's always in view.
        // Player3DRenderer uses pixel-space world coords. RT-mode camera is
        // tightly framed (192x192 RT, +/-96 in iso Y around target). Putting
        // the cube at +60 above feet keeps it inside the RT view, sized to
        // half the player's height so it can't be missed.
        public static Vector3 WorldOffset = new Vector3(0f, 60f, 0f);
        public static float CubeSize = 80f;
        private static int _debugFrameCounter;

        private static BasicEffect _effect;
        private static VertexBuffer _vb;
        private static IndexBuffer  _ib;
        private static int _indexCount;
        private static Texture2D _fallbackTex;

        // Procedural test texture: 8x8 pattern with red/green/blue/yellow
        // quadrants + a black/white checker overlay. ANY visible pattern on
        // the cube proves BasicEffect can sample textures correctly.
        private static Texture2D EnsureFallbackTexture(GraphicsDevice gd)
        {
            if (_fallbackTex != null) return _fallbackTex;
            const int N = 8;
            var data = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color quad = (x < N/2 && y < N/2) ? Color.Red
                           : (x >= N/2 && y < N/2) ? Color.Lime
                           : (x < N/2 && y >= N/2) ? Color.Blue
                           : Color.Yellow;
                bool checker = ((x ^ y) & 1) == 0;
                data[y * N + x] = checker ? quad : new Color((byte)(quad.R/3), (byte)(quad.G/3), (byte)(quad.B/3));
            }
            _fallbackTex = new Texture2D(gd, N, N, false, SurfaceFormat.Color);
            _fallbackTex.SetData(data);
            return _fallbackTex;
        }

        private static void EnsureGeometry(GraphicsDevice gd)
        {
            if (_effect != null) return;

            _effect = new BasicEffect(gd)
            {
                TextureEnabled  = true,
                LightingEnabled = false,
                VertexColorEnabled = false,
            };

            // Build a unit cube centered at origin. Each face has its own UVs
            // covering the full texture (0..1, 0..1).
            float s = 0.5f;
            VertexPositionNormalTexture[] verts = new VertexPositionNormalTexture[24];
            int[] idxList = new int[36];
            int v = 0, ix = 0;

            // Helper to push a quad: 4 verts + 6 indices.
            void PushFace(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
            {
                int b = v;
                verts[v++] = new VertexPositionNormalTexture(p0, n, new Vector2(0, 0));
                verts[v++] = new VertexPositionNormalTexture(p1, n, new Vector2(1, 0));
                verts[v++] = new VertexPositionNormalTexture(p2, n, new Vector2(1, 1));
                verts[v++] = new VertexPositionNormalTexture(p3, n, new Vector2(0, 1));
                idxList[ix++] = b + 0; idxList[ix++] = b + 1; idxList[ix++] = b + 2;
                idxList[ix++] = b + 0; idxList[ix++] = b + 2; idxList[ix++] = b + 3;
            }

            // +Z (front)
            PushFace(new Vector3(-s,-s, s), new Vector3( s,-s, s), new Vector3( s, s, s), new Vector3(-s, s, s), Vector3.UnitZ);
            // -Z (back)
            PushFace(new Vector3( s,-s,-s), new Vector3(-s,-s,-s), new Vector3(-s, s,-s), new Vector3( s, s,-s), -Vector3.UnitZ);
            // +X (right)
            PushFace(new Vector3( s,-s, s), new Vector3( s,-s,-s), new Vector3( s, s,-s), new Vector3( s, s, s), Vector3.UnitX);
            // -X (left)
            PushFace(new Vector3(-s,-s,-s), new Vector3(-s,-s, s), new Vector3(-s, s, s), new Vector3(-s, s,-s), -Vector3.UnitX);
            // +Y (top)
            PushFace(new Vector3(-s, s, s), new Vector3( s, s, s), new Vector3( s, s,-s), new Vector3(-s, s,-s), Vector3.UnitY);
            // -Y (bottom)
            PushFace(new Vector3(-s,-s,-s), new Vector3( s,-s,-s), new Vector3( s,-s, s), new Vector3(-s,-s, s), -Vector3.UnitY);

            _vb = new VertexBuffer(gd, VertexPositionNormalTexture.VertexDeclaration, verts.Length, BufferUsage.WriteOnly);
            _vb.SetData(verts);
            _ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idxList.Length, BufferUsage.WriteOnly);
            _ib.SetData(idxList);
            _indexCount = idxList.Length;
        }

        public static void Draw(GraphicsDevice gd, Matrix view, Matrix projection,
                                Vector3 worldCenter, Texture2D texture)
        {
            if (!Enabled) return;

            // Periodic log so we can confirm this draw is firing AT ALL.
            // BUILD-MARKER suffix lets us verify a fresh build is running:
            // "v3-fallback" means the EnsureFallbackTexture path is active.
            if ((++_debugFrameCounter % 60) == 1)
            {
                Console.WriteLine($"[3DCUO][debugcube v3-fallback] firing at worldCenter=({worldCenter.X:F0},{worldCenter.Y:F0},{worldCenter.Z:F0}) " +
                    $"+offset=({WorldOffset.X:F0},{WorldOffset.Y:F0},{WorldOffset.Z:F0}) size={CubeSize} tex={(texture == null ? "null→fallback8x8" : $"{texture.Width}x{texture.Height}")}");
            }

            // Even with no texture, render a flat-shaded cube so we know the
            // mesh is positioned correctly. (BasicEffect needs TextureEnabled=false
            // for the un-textured path.)

            EnsureGeometry(gd);

            var prevDepth  = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend  = gd.BlendState;
            var prevSamp   = gd.SamplerStates[0];

            // No depth testing — guarantees the diagnostic cube is visible
            // on top of the player and terrain regardless of camera setup.
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState   = RasterizerState.CullNone;  // see all faces from any angle
            gd.BlendState        = BlendState.Opaque;
            gd.SamplerStates[0]  = SamplerState.PointWrap;

            float scale = CubeSize;
            _effect.World      = Matrix.CreateScale(scale) * Matrix.CreateTranslation(worldCenter + WorldOffset);
            _effect.View       = view;
            _effect.Projection = projection;
            // Prefer real attachment texture; fall back to procedural test
            // pattern so we ALWAYS test BasicEffect's texture sampling.
            var actualTex = texture ?? EnsureFallbackTexture(gd);
            _effect.Texture = actualTex;
            _effect.TextureEnabled = true;
            _effect.DiffuseColor = Vector3.One;

            gd.SetVertexBuffer(_vb);
            gd.Indices = _ib;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _vb.VertexCount, 0, _indexCount / 3);
            }

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState   = prevRaster;
            gd.BlendState        = prevBlend;
            gd.SamplerStates[0]  = prevSamp;
        }

        // Pull the first non-null Submesh.Texture from any loaded attachment.
        public static Texture2D ResolveAttachmentTexture()
        {
            foreach (var slot in AttachmentRenderer.Slots)
            {
                foreach (var kv in slot.Cache)
                {
                    var mdl = kv.Value;
                    if (mdl == null) continue;
                    foreach (var s in mdl.Submeshes)
                        if (s.Texture != null) return s.Texture;
                }
            }
            return null;
        }
    }
}
