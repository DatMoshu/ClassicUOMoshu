// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Phase 3 pass (ADR-012 §7).

using System;
using System.Collections.Generic;
using ClassicUO.Game.Map;
using ClassicUO.Renderer.Renderer3D; // legacy SeasonCycleDriver + Weather3DSystem + W3DR ground-effect state
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Environment;
using ClassicUO.Renderer.Terrain;
using ClassicUO.Renderer.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Passes
{
    /// <summary>
    /// Order 250 — runs between <see cref="TerrainPass"/> (200) and
    /// <see cref="StaticGeometryPass"/> (300). Tints the heightmap with the per-pixel
    /// slope-masked wet / snow / fall shader. Ground-only by construction — running it
    /// after statics or mobiles would paint over them, which is why this pass lives
    /// below order 300 rather than at the legacy "Effects" 500 slot.
    /// </summary>
    /// <remarks>
    /// Owns the ground-effect ease tick (intensity ramp + weather-link mapping +
    /// per-frame <see cref="SeasonCycleDriver"/> drive). Pre-switchover this body lived
    /// in <c>World3DRenderer.DrawGroundEffectPass</c> / <c>UpdateGroundEffectEase</c>.
    /// Frame delta comes from <see cref="RenderPassContext.Frame"/> per the playbook's
    /// "no clock other than IFrameClock" rule.
    /// </remarks>
    internal sealed class GroundOverlayPass : IRenderPass
    {
        private readonly ITerrainMeshCache _cache;
        private readonly ITerrainRenderResources _resources;
        private readonly IWeatherGroundOverlayMap _weatherMap;
        private readonly IRenderQualityService _quality;

        public GroundOverlayPass(
            ITerrainMeshCache cache,
            ITerrainRenderResources resources,
            IWeatherGroundOverlayMap weatherMap,
            IRenderQualityService quality)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _weatherMap = weatherMap ?? throw new ArgumentNullException(nameof(weatherMap));
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
        }

        public string Name => "GroundOverlay";
        public int Order => RenderPassOrder.GroundOverlay;
        public bool IsEnabled => true;

        public void Execute(in RenderPassContext ctx)
        {
            if (ctx.Graphics is null || ctx.VisibleChunks is null) return;

            UpdateGroundEffectEase(ctx.Frame.DeltaSeconds, _weatherMap);

            if (World3DRenderer.GroundEffectMode == World3DRenderer.GroundEffect.None ||
                World3DRenderer.GroundEffectIntensity <= 0.001f)
                return;

            var overlay = _resources.GroundOverlay;
            if (overlay is null) return;

            var gd = ctx.Graphics;
            float intensity = MathHelper.Clamp(World3DRenderer.GroundEffectIntensity, 0f, 1f);

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

            var prevBlend = gd.BlendState;
            var prevDepth = gd.DepthStencilState;
            var prevSampler0 = gd.SamplerStates[0];
            var prevSampler1 = gd.SamplerStates[1];
            var prevSampler2 = gd.SamplerStates[2];

            // Layer onto existing ground depth; don't push it forward.
            gd.DepthStencilState = DepthStencilState.DepthRead;
            gd.BlendState = BlendState.AlphaBlend;
            // Wrap-linear so worldspace noise tiles cleanly across the terrain.
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            gd.SamplerStates[2] = SamplerState.LinearWrap;

            float exag = _quality.HeightExaggeration;
            overlay.World = exag != 1.0f
                ? Matrix.CreateScale(1f, exag, 1f)
                : Matrix.Identity;
            overlay.View = view;
            overlay.Projection = proj;
            overlay.Time = (float)ctx.Frame.TotalSeconds;
            overlay.Intensity = intensity;
            overlay.ApplyTunables();

            if (World3DRenderer.GroundEffectMode == World3DRenderer.GroundEffect.Wet) overlay.UseWet();
            else                                                                       overlay.UseSnow();

            IList<Chunk> visibleChunks = ctx.VisibleChunks;
            for (int idx = 0; idx < visibleChunks.Count; idx++)
            {
                var chunk = visibleChunks[idx];
                if (chunk is null) continue;
                if (_cache.TryGet(chunk, out var mesh) && mesh.HasOverlay)
                    mesh.DrawGroundOverlay(gd, overlay);
            }

            gd.BlendState = prevBlend;
            gd.DepthStencilState = prevDepth;
            gd.SamplerStates[0] = prevSampler0;
            gd.SamplerStates[1] = prevSampler1;
            gd.SamplerStates[2] = prevSampler2;
        }

        // Ease body lifted from World3DRenderer.UpdateGroundEffectEase (session 64). The
        // weather→{mode, multiplier} branch was a hardcoded if/else block; session 65's
        // Phase 4 pilot replaces it with a JSON-loaded lookup via
        // <see cref="IWeatherGroundOverlayMap"/>. Behavior matches the legacy mapping when
        // the default weather-ground-overlay.json is loaded; unknown weather kinds (or an
        // empty/missing config) fade the target intensity to 0 — same fallback as before.
        private static void UpdateGroundEffectEase(float rawDt, IWeatherGroundOverlayMap weatherMap)
        {
            SeasonCycleDriver.Tick();

            float dt = MathHelper.Clamp(rawDt, 0f, 0.1f);

            if (World3DRenderer.LinkToWeather)
            {
                // Legacy Weather3DType numeric values are parity-locked with WeatherKind
                // (see Atmosphere/WeatherKind.cs remarks); cast through int is safe.
                WeatherKind kind = (WeatherKind)(int)Weather3DSystem.Type;
                float wInt = MathHelper.Clamp(Weather3DSystem.Intensity, 0f, 1f);
                if (weatherMap.TryGet(kind, out WeatherGroundOverlayEntry rule))
                {
                    World3DRenderer.GroundEffectMode = ToLegacyMode(rule.Mode);
                    World3DRenderer.TargetGroundEffectIntensity =
                        World3DRenderer.LinkStrength * wInt * rule.StrengthMultiplier;
                }
                else
                {
                    // Fade out wet/snow but keep the mode so the fade-out visibly tracks
                    // the *current* effect, not a flash to None (legacy contract).
                    World3DRenderer.TargetGroundEffectIntensity = 0f;
                }
            }

            float alpha = 1f - MathF.Exp(-World3DRenderer.GroundEffectEaseSpeed * dt);
            World3DRenderer.GroundEffectIntensity = MathHelper.Lerp(
                World3DRenderer.GroundEffectIntensity,
                World3DRenderer.TargetGroundEffectIntensity, alpha);

            if (MathF.Abs(World3DRenderer.GroundEffectIntensity - World3DRenderer.TargetGroundEffectIntensity) < 0.001f)
                World3DRenderer.GroundEffectIntensity = World3DRenderer.TargetGroundEffectIntensity;
        }

        // Domain GroundEffectMode → legacy enum (parity-locked numeric values).
        private static World3DRenderer.GroundEffect ToLegacyMode(GroundEffectMode mode)
            => (World3DRenderer.GroundEffect)(int)mode;
    }
}
