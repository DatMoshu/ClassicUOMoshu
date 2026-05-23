// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO migration smoke test — renders a baked .umesh file above the local
// player's head using BasicEffect. Proves the SharpGLTF-free runtime loader
// (UMeshLoader) round-trips real Synty content correctly.
//
// Coordinate convention matches World3DRenderer (TILE=22, Z_SCALE=4, Y up).
// Toggle via Debug3DGump or set HeadMeshRenderer.Enabled directly.

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>Smoke-test renderer that draws a single .umesh above the player's head.</summary>
    internal static class HeadMeshRenderer
    {
        public static bool Enabled = false;

        // Path resolved against AppContext.BaseDirectory (i.e. the published binary's
        // working dir). Copied there by the CopyUMeshes MSBuild target.
        public static string MeshPath = "Data/test-meshes/elven_hat.umesh";

        // Scale converts mesh units → world units. Synty Sidekick parts are in
        // ~Mixamo scale; matches Player3DRenderer.ModelScale default.
        public static float Scale = 60f;

        // Vertical offset above the player position, in world units. Player
        // pivot sits on the ground; head is roughly +Y=110 at scale 60. Add a
        // little headroom so the hat clearly floats above.
        public static float HeadHeight = 130f;

        // Spin slowly around Y so we can see all sides of the hat. Set to 0
        // for static placement once we've verified normals/UVs.
        public static float SpinDegPerSec = 30f;

        // Pitch correction. Synty hats are authored Z-up; our world is Y-up,
        // so rotate -90° around X to stand the hat upright.
        public static float PitchDegrees = -90f;

        // Diagnostics surfaced to Debug3DGump.
        public static string LastError;
        public static bool Loaded => _mesh != null;
        public static int VertexCount => _mesh?.VertexCount ?? 0;
        public static int PrimitiveCount => _mesh?.PrimitiveCount ?? 0;

        private static UMesh _mesh;
        private static BasicEffect _effect;
        private static string _loadedPath;
        private static bool _loadFailed;
        private static float _spinTimeSec;
        private static long _lastTicks;

        /// <summary>Force a reload on next draw (e.g. after MeshPath changes).</summary>
        public static void Invalidate()
        {
            _mesh?.Dispose();
            _mesh = null;
            _loadedPath = null;
            _loadFailed = false;
            LastError = null;
        }

        public static void Draw(
            GraphicsDevice gd,
            float playerUoX, float playerUoY, float playerUoZ,
            int viewportWidth, int viewportHeight)
        {
            if (!Enabled || gd == null) return;

            // Lazy load (and reload on path change).
            if (_mesh == null && !_loadFailed)
            {
                try
                {
                    string resolved = ResolvePath(MeshPath);
                    _mesh = UMeshLoader.Load(gd, resolved);
                    _loadedPath = resolved;
                    LastError = null;
                }
                catch (Exception ex)
                {
                    _loadFailed = true;
                    LastError = $"{ex.GetType().Name}: {ex.Message}";
                    Console.WriteLine($"[HeadMesh] load failed: {LastError}");
                    return;
                }
            }
            if (_mesh == null) return;

            EnsureEffect(gd);

            // Advance spin timer (wall-clock; smoke test only — production renderer
            // takes dt from the scene per code-review MAJOR-09).
            long now = DateTime.UtcNow.Ticks;
            if (_lastTicks != 0)
            {
                float dt = (float)((now - _lastTicks) / 10_000_000.0);
                if (dt > 0 && dt < 0.5f) _spinTimeSec += dt;
            }
            _lastTicks = now;

            // World matrix — pitch correction, scale, spin around Y, translate
            // to player position + head height. Order: localTransform → worldPos.
            float spinRad = MathHelper.ToRadians((SpinDegPerSec * _spinTimeSec) % 360f);
            float pitchRad = MathHelper.ToRadians(PitchDegrees);

            Matrix world =
                Matrix.CreateRotationX(pitchRad) *
                Matrix.CreateScale(Scale) *
                Matrix.CreateRotationY(spinRad) *
                Matrix.CreateTranslation(
                    playerUoX * LandMesh3D.TILE,
                    playerUoZ * LandMesh3D.Z_SCALE + HeadHeight,
                    playerUoY * LandMesh3D.TILE);

            // View/projection — match World3DRenderer's camera.
            Matrix view, proj;
            if (World3DRenderer.UseIsoProjection)
            {
                view = Matrix.Identity;
                proj = World3DRenderer.Camera.IsoViewProjection(viewportWidth, viewportHeight);
            }
            else
            {
                view = World3DRenderer.Camera.View;
                float aspect = (float)viewportWidth / Math.Max(1, viewportHeight);
                proj = World3DRenderer.Camera.Projection(aspect);
            }

            _effect.World = world;
            _effect.View = view;
            _effect.Projection = proj;
            _effect.TextureEnabled = _mesh.Texture != null;
            _effect.Texture = _mesh.Texture;
            _effect.VertexColorEnabled = false;
            _effect.LightingEnabled = false;
            _effect.Alpha = 1f;
            _effect.FogEnabled = false;

            // Save + restore device state so we don't leak settings into the next
            // 2D pass (matches World3DRenderer's pattern).
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;
            var prevSampler = gd.SamplerStates[0];

            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.BlendState = BlendState.Opaque;
            gd.SamplerStates[0] = SamplerState.LinearClamp;

            gd.SetVertexBuffer(_mesh.VertexBuffer);
            gd.Indices = _mesh.IndexBuffer;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    baseVertex: 0,
                    minVertexIndex: 0,
                    numVertices: _mesh.VertexCount,
                    startIndex: 0,
                    primitiveCount: _mesh.PrimitiveCount);
            }

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
            gd.SamplerStates[0] = prevSampler;
        }

        private static void EnsureEffect(GraphicsDevice gd)
        {
            if (_effect == null) _effect = new BasicEffect(gd);
        }

        private static string ResolvePath(string relOrAbs)
        {
            if (Path.IsPathRooted(relOrAbs) && File.Exists(relOrAbs)) return relOrAbs;
            string baseDir = AppContext.BaseDirectory;
            string candidate = Path.Combine(baseDir, relOrAbs);
            if (File.Exists(candidate)) return candidate;
            // Working-dir fallback — useful when running from VS (cwd != bin dir).
            string cwdCandidate = Path.GetFullPath(relOrAbs);
            if (File.Exists(cwdCandidate)) return cwdCandidate;
            throw new FileNotFoundException(
                $"umesh not found: tried '{candidate}' and '{cwdCandidate}'", relOrAbs);
        }
    }
}
