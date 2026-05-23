// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 1: prove the 3D pipeline.
// Renders a single colored cube into the world render target using FNA's
// BasicEffect, with the cube positioned at the screen pixel coordinates of a
// chosen world tile so it composites with the sprite world.
//
// This is throwaway scaffolding. When Phase 2 starts, the iso "fake 3D" math
// here is replaced with a real Camera3D + heightmap mesh.

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace ClassicUO.Renderer.Renderer3D
{
    public static class Test3DRenderer
    {
        // Set to true at runtime (or hardcode) to render the test cube.
        public static bool Enabled = true;

        // Diagnostic: when true, also draws an unmissable magenta full-viewport
        // overlay so we can confirm Draw() is being called at all. Turn off once
        // the cube is visible.
        public static bool DiagnosticOverlay = false;

        private static int _drawCallCount;

        // World tile to render the cube at. Default is "wherever the player is".
        // Set to a fixed tile to verify projection.
        public static int WorldTileX;
        public static int WorldTileY;
        public static int WorldTileZ;
        public static bool FollowPlayer = true;

        private static BasicEffect _effect;
        private static VertexBuffer _vbo;
        private static IndexBuffer _ibo;
        private static bool _ready;

        private const int CUBE_HEIGHT_PIXELS = 44; // one tile tall, in iso pixels

        public static void Draw(
            GraphicsDevice gd,
            int playerScreenX,
            int playerScreenY,
            int viewportWidth,
            int viewportHeight,
            Matrix camera2DTransform
        )
        {
            if (!Enabled || gd == null) return;

            EnsureResources(gd);

            // Fail-loud diagnostic: log first call, then every 600 frames (~10 sec @ 60fps).
            if (_drawCallCount == 0)
            {
                Console.WriteLine("[3DCUO] Test3DRenderer.Draw() FIRST CALL -- pipeline is hot.");
            }
            if ((_drawCallCount % 600) == 0)
            {
                Console.WriteLine($"[3DCUO] Draw call #{_drawCallCount} -- player screen=({playerScreenX},{playerScreenY}) viewport=({viewportWidth}x{viewportHeight})");
            }
            _drawCallCount++;

            // Diagnostic overlay: bright magenta quad covering top-left 200x200 of the
            // world render target. Drawn with identity matrices straight to clip space
            // so projection math can't be at fault. If you don't see this, Draw() isn't
            // being called -- you're running the wrong binary.
            if (DiagnosticOverlay)
            {
                DrawDiagnosticOverlay(gd);
            }

            // Step 1: where on screen do we want the cube to appear?
            // For the first test we just slap it on top of the player.
            // (Iso world->screen uses the player's pre-camera pixel position;
            //  Camera2DTransform scales/pans that into the viewport.)
            var screenCenter = new Vector2(playerScreenX, playerScreenY);

            // Step 2: build a render-target-space ortho projection.
            // World render target is viewportWidth x viewportHeight, top-left origin.
            var projection = Matrix.CreateOrthographicOffCenter(
                0, viewportWidth, viewportHeight, 0, -1024f, 1024f
            );

            // Step 3: world matrix places the cube at the screen position.
            // Cube is built in unit-cube local space (-0.5..0.5 each axis); scale to
            // pixel size first, then translate.
            var world =
                Matrix.CreateScale(CUBE_HEIGHT_PIXELS) *
                Matrix.CreateRotationY(0.5f) *      // small rotation for 3D-ness
                Matrix.CreateRotationX(-0.4f) *
                Matrix.CreateTranslation(screenCenter.X, screenCenter.Y - CUBE_HEIGHT_PIXELS, 0);

            // We want the cube to behave like a 3D object in the *2D camera* transform,
            // so multiply the world matrix by the camera's 2D affine transform.
            world *= camera2DTransform;

            _effect.World = world;
            _effect.View = Matrix.Identity;
            _effect.Projection = projection;
            _effect.VertexColorEnabled = true;
            _effect.LightingEnabled = false;
            _effect.TextureEnabled = false;

            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;

            // Phase 1 diagnostic: depth-test OFF so the cube can't lose to the
            // sprite world's depth values. We verify positioning first; correct
            // depth alignment is a Phase 2/4 concern (sprites write depth in their
            // own scheme that our 3D cube doesn't match yet).
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;    // see all faces during proto
            gd.BlendState = BlendState.Opaque;

            gd.SetVertexBuffer(_vbo);
            gd.Indices = _ibo;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 8, 0, 12);
            }

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
        }

        private static VertexBuffer _diagVbo;

        private static void DrawDiagnosticOverlay(GraphicsDevice gd)
        {
            if (_diagVbo == null)
            {
                // Quad in clip space: covers top-left quadrant of the screen.
                // Clip-space x=[-1, 0], y=[0, 1] is upper-left in default GL/D3D.
                var quad = new VertexPositionColor[6];
                var c = Color.Magenta;
                quad[0] = new VertexPositionColor(new Vector3(-1f,  0f, 0f), c);
                quad[1] = new VertexPositionColor(new Vector3( 0f,  0f, 0f), c);
                quad[2] = new VertexPositionColor(new Vector3(-1f,  1f, 0f), c);
                quad[3] = new VertexPositionColor(new Vector3( 0f,  0f, 0f), c);
                quad[4] = new VertexPositionColor(new Vector3( 0f,  1f, 0f), c);
                quad[5] = new VertexPositionColor(new Vector3(-1f,  1f, 0f), c);
                _diagVbo = new VertexBuffer(gd, typeof(VertexPositionColor), 6, BufferUsage.WriteOnly);
                _diagVbo.SetData(quad);
            }

            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;

            gd.DepthStencilState = DepthStencilState.None;  // ignore depth, force visible
            gd.RasterizerState = RasterizerState.CullNone;
            gd.BlendState = BlendState.Opaque;

            _effect.World = Matrix.Identity;
            _effect.View = Matrix.Identity;
            _effect.Projection = Matrix.Identity;
            _effect.VertexColorEnabled = true;
            _effect.LightingEnabled = false;
            _effect.TextureEnabled = false;

            gd.SetVertexBuffer(_diagVbo);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);
            }

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
        }

        private static void EnsureResources(GraphicsDevice gd)
        {
            if (_ready) return;

            _effect = new BasicEffect(gd);

            // Unit cube (8 verts), colored per-vertex so faces are distinguishable.
            var verts = new VertexPositionColor[8];
            verts[0] = new VertexPositionColor(new Vector3(-0.5f, -0.5f, -0.5f), Color.Red);
            verts[1] = new VertexPositionColor(new Vector3( 0.5f, -0.5f, -0.5f), Color.Green);
            verts[2] = new VertexPositionColor(new Vector3( 0.5f,  0.5f, -0.5f), Color.Blue);
            verts[3] = new VertexPositionColor(new Vector3(-0.5f,  0.5f, -0.5f), Color.Yellow);
            verts[4] = new VertexPositionColor(new Vector3(-0.5f, -0.5f,  0.5f), Color.Magenta);
            verts[5] = new VertexPositionColor(new Vector3( 0.5f, -0.5f,  0.5f), Color.Cyan);
            verts[6] = new VertexPositionColor(new Vector3( 0.5f,  0.5f,  0.5f), Color.White);
            verts[7] = new VertexPositionColor(new Vector3(-0.5f,  0.5f,  0.5f), Color.Orange);

            // 12 triangles, 36 indices
            var idx = new short[]
            {
                0,1,2, 0,2,3,   // back
                4,6,5, 4,7,6,   // front
                0,4,5, 0,5,1,   // bottom
                3,2,6, 3,6,7,   // top
                0,3,7, 0,7,4,   // left
                1,5,6, 1,6,2    // right
            };

            _vbo = new VertexBuffer(gd, typeof(VertexPositionColor), verts.Length, BufferUsage.WriteOnly);
            _vbo.SetData(verts);

            _ibo = new IndexBuffer(gd, IndexElementSize.SixteenBits, idx.Length, BufferUsage.WriteOnly);
            _ibo.SetData(idx);

            _ready = true;
        }
    }
}
