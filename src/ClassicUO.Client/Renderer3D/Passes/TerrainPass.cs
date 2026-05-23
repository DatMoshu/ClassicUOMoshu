// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Phase 3 pass (ADR-012 §7).

using System;
using System.Collections.Generic;
using ClassicUO.Game.Map;
using ClassicUO.Renderer.Renderer3D; // legacy Multi3DRenderer/Static3DRenderer/Player3DRenderer toggles (still raw statics)
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.EnvRender;
using ClassicUO.Renderer.Terrain;
using ClassicUO.Renderer.WorldEnv;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Passes
{
    /// <summary>
    /// Order 200. Builds dirty chunks and draws the heightmap with the shared
    /// <see cref="BasicEffect"/>. Owns the per-chunk land-mesh cache via
    /// <see cref="ITerrainMeshCache"/> (session 62 extraction) and the heightmap
    /// <see cref="BasicEffect"/> / wireframe <see cref="RasterizerState"/> via
    /// <see cref="ITerrainRenderResources"/> (session 63 extraction).
    /// </summary>
    /// <remarks>
    /// Body lifted verbatim from <c>World3DRenderer.Draw</c> at the session-64 switchover.
    /// Session 70 tightened pass→W3DR static reads: render quality tunables now flow
    /// through <see cref="IRenderQualityService"/>, fog params through
    /// <see cref="IEnvironmentService"/>. Down-stream pass enables
    /// (<c>Multi3DRenderer.Enabled</c>, <c>Static3DRenderer.Enabled</c>,
    /// <c>Player3DRenderer.Enabled</c>) are still raw legacy reads — they migrate when
    /// those subsystems finish migration.
    /// </remarks>
    internal sealed class TerrainPass : IRenderPass
    {
        private readonly ITerrainMeshCache _cache;
        private readonly ITerrainRenderResources _resources;
        private readonly IRenderQualityService _quality;
        private readonly IEnvironmentService _environment;

        public TerrainPass(
            ITerrainMeshCache cache,
            ITerrainRenderResources resources,
            IRenderQualityService quality,
            IEnvironmentService environment)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public string Name => "Terrain";
        public int Order => RenderPassOrder.Terrain;
        public bool IsEnabled => true;

        public void Execute(in RenderPassContext ctx)
        {
            if (ctx.Graphics is null || ctx.VisibleChunks is null) return;
            if (ctx.VisibleChunks.Count == 0) return;

            var gd = ctx.Graphics;
            IList<Chunk> visibleChunks = ctx.VisibleChunks;

            // Build any visible chunks that don't have a mesh yet, or whose data has
            // changed (IsDirty3D), or whose cached mesh is empty (chunk wasn't loaded
            // the first time we saw it — retry until we get geometry).
            int builtThisFrame = 0;
            for (int i = 0; i < visibleChunks.Count; i++)
            {
                if (_cache.BuildIfDirty(visibleChunks[i], gd, out _))
                    builtThisFrame++;
            }
            World3DRenderer.LastBuiltChunks = builtThisFrame;

            // Compute view/proj matching the shared camera convention.
            Matrix view, proj;
            if (_quality.UseIsoProjection)
            {
                view = Matrix.Identity;
                proj = ctx.Camera.IsoViewProjection(ctx.ViewportWidth, ctx.ViewportHeight);
            }
            else
            {
                float aspect = (float)ctx.ViewportWidth / System.Math.Max(1, ctx.ViewportHeight);
                view = ctx.Camera.View;
                proj = ctx.Camera.Projection(aspect);
            }

            // Save GPU state — restored at end of pass.
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;
            var prevSampler = gd.SamplerStates[0];

            // Force depth on whenever a downstream pass needs occlusion, even when the
            // "Depth test" master toggle is off. Otherwise the heightmap skips depth
            // writes and the player model draws on top of walls / statics. Downstream
            // pass enables (Multi3D/Static3D/Player3D) are still raw legacy statics —
            // they migrate when those subsystems do.
            bool needDepth = _quality.DepthTestEnabled
                || Multi3DRenderer.Enabled
                || Static3DRenderer.Enabled
                || Player3DRenderer.Enabled;
            gd.DepthStencilState = needDepth ? DepthStencilState.Default : DepthStencilState.None;
            gd.RasterizerState = _quality.Wireframe ? _resources.Wireframe : RasterizerState.CullNone;
            gd.BlendState = _quality.MeshAlpha < 0.999f ? BlendState.AlphaBlend : BlendState.Opaque;
            // Pixel-art sampling so UO terrain texmaps stay crisp like the 2D client.
            gd.SamplerStates[0] = SamplerState.PointClamp;

            var effect = _resources.Effect;
            float exag = _quality.HeightExaggeration;
            effect.World = exag != 1.0f
                ? Matrix.CreateScale(1f, exag, 1f)
                : Matrix.Identity;
            effect.View = view;
            effect.Projection = proj;
            effect.VertexColorEnabled = true;
            effect.LightingEnabled = false;
            effect.TextureEnabled = true; // LandMesh3D binds the per-submesh texture
            effect.Alpha = _quality.MeshAlpha;

            effect.FogEnabled = _environment.FogEnabled;
            if (_environment.FogEnabled)
            {
                effect.FogStart = _environment.FogStart;
                effect.FogEnd = _environment.FogEnd;
                effect.FogColor = _environment.FogColor.ToVector3();
            }

            int drawn = 0;
            for (int i = 0; i < visibleChunks.Count; i++)
            {
                var chunk = visibleChunks[i];
                if (chunk is null) continue;
                if (_cache.TryGet(chunk, out var mesh) && mesh.HasGeometry)
                {
                    mesh.Draw(gd, effect);
                    drawn++;
                }
            }
            World3DRenderer.LastDrawnChunks = drawn;

            // Restore GPU state for downstream passes.
            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
            gd.SamplerStates[0] = prevSampler;
        }
    }
}
