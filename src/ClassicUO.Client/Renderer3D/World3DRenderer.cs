// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 2: orchestrates the 3D world pass.
// Owns Camera3D, caches per-chunk LandMesh3D objects, builds them on demand,
// and draws them with a single shared BasicEffect.

using System;
using System.Collections.Generic;
using ClassicUO.Game.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class World3DRenderer
    {
        // ===== Master toggles =====
        public static bool Enabled = true;
        public static bool HideTestCube = true;
        // Master toggle: when true, GameScene skips drawing the 2D sprite world
        // (chunk meshes for land + statics + multis + mobiles + weather). Useful
        // for seeing the pure 3D pipeline without 2D bleed-through, and required
        // when UseIsoProjection=false so the perspective camera isn't clipped by
        // the 2D iso-aligned render.
        public static bool Disable2DWorld = false;
        // Master toggle: skip the legacy 2D darkness/lights overlay when in Full 3D
        // mode. The 2D lightmap (UseLights / UseAltLights in GameScene) was tuned for
        // the iso composite — it dims the perspective 3D world to night levels even
        // at noon, producing the "everything is dark" look. ON by default.
        public static bool Disable2DLightingIn3D = true;

        // ===== Render state tunables (exposed to debug gump) =====
        // OFF by default — enabling this without the Camera3D iso depth fix being live
        // makes the 3D pass read the 2D batcher's leftover depth values, causing
        // dropouts and "checker holes" in the heightmap. Safe to flip on once the
        // Renderer DLL is rebuilt with the iso-projection depth term.
        public static bool DepthTestEnabled = false;
        public static bool Wireframe = false;
        public static float MeshAlpha = 1.0f;
        public static float HeightExaggeration = 1.0f; // multiplies Y axis to amplify hills
        // When true, use the UO-iso projection (pixel-perfect alignment with 2D sprites).
        // When false, use a free 3D camera (LookAt + Ortho via Camera3D pitch/yaw).
        public static bool UseIsoProjection = true;

        // Manual camera offset from the player (in 3D world units, X/Y/Z).
        public static Vector3 CameraOffset = Vector3.Zero;

        // Multiplier on the 2D iso chunk-visibility radius. The base radius is tuned
        // for the iso camera; in 3D perspective/free-cam mode the visible footprint
        // is much larger and chunks past the base radius simply aren't loaded — the
        // user sees the cleared background past them. Crank this up to load more.
        // Cost is roughly quadratic in the multiplier (more chunks built + drawn).
        public static float RenderDistanceMultiplier = 1.0f;

        // Background color the GameScene clears to when Disable2DWorld is on. This
        // is what shows through wherever no 3D geometry covers a pixel — typically
        // the world horizon / off-map area. Default = pure black so the sky reads
        // as void until the procedural/cubemap skybox pass is wired in.
        public static Color BackgroundColor = Color.Black;

        // Lightning flash — set by Weather3DSystem.TriggerLightning() to ~0.85
        // and decayed each frame in SeasonCycleDriver.EaseLightningFlash. Used
        // wherever the clear color is applied: the effective clear color is
        // lerp(BackgroundColor, white, LightningFlashIntensity), giving a
        // brief screen wash that reads as a thunderclap illumination.
        public static float LightningFlashIntensity;
        public static Color GetEffectiveClearColor()
        {
            float t = LightningFlashIntensity;
            if (t <= 0f) return BackgroundColor;
            if (t > 1f) t = 1f;
            int r = (int)(BackgroundColor.R + (255 - BackgroundColor.R) * t);
            int g = (int)(BackgroundColor.G + (255 - BackgroundColor.G) * t);
            int b = (int)(BackgroundColor.B + (255 - BackgroundColor.B) * t);
            return new Color(r, g, b, 255);
        }

        // ===== Skybox (procedural gradient stub) =====
        // The dome behind the world. Pass One: vertical-gradient screen quad
        // drawn before chunk meshes when SkyboxEnabled is true (TopColor at
        // screen Y=0 → HorizonColor at screen Y=Height/2 → BottomColor at the
        // bottom). Pass Two (later): swap the gradient for a TextureCube
        // sampled by the inverse view direction. Both modes share these tunables.
        public enum SkyboxMode { Off = 0, Gradient = 1, Cubemap = 2 }
        public static SkyboxMode SkyMode = SkyboxMode.Off;
        public static Color SkyTopColor     = new Color(  8,  18,  48, 255); // deep night
        public static Color SkyHorizonColor = new Color( 70, 130, 180, 255); // dawn steel
        public static Color SkyBottomColor = new Color(   0,   0,   0, 255); // pure black floor
        // 0..1 — where on the screen the horizon line sits. 0.5 = mid-screen.
        public static float SkyHorizonY = 0.5f;

        // ===== Ground wet/snow overlay =====
        // Drawn as a second pass over the land mesh after the textured base
        // pass and before statics/multis/player so it tints ONLY the ground.
        //   Wet:  multiplicative dark-blue tint (rain darkens & cools the
        //         terrain — wood/dirt/stone all read wetter without changing
        //         the underlying texmap).
        //   Snow: alpha-blended white wash (snow accumulation on top faces;
        //         iso ground triangles all face roughly upward so this reads
        //         as snow cover even without a normal-based mask).
        public enum GroundEffect { None = 0, Wet = 1, Snow = 2 }
        public static GroundEffect GroundEffectMode = GroundEffect.None;
        // Target = where the slider / weather link wants to be (0..1).
        // Current = the value actually fed to the shader, eased toward Target each frame.
        // Slider writes into Target; the visual ramps in/out smoothly.
        public static float TargetGroundEffectIntensity = 0.7f;
        public static float GroundEffectIntensity      = 0.0f;
        public static float GroundEffectEaseSpeed      = 1.5f; // larger = snappier
        // Weather link — when true, GroundEffectMode + Target follow the active
        // Weather3DSystem.Type at LinkStrength (default 0.5). Disable to take
        // full manual control via the slider.
        // ON by default — the weather demo expects ground wet/snow to follow
        // the active Weather3DType automatically (Rain → wet, Snow → snowy)
        // without the user having to flip a checkbox first.
        public static bool  LinkToWeather              = true;
        public static float LinkStrength               = 0.7f;

        // Distance fog applied via BasicEffect. FogStart/FogEnd are in world units
        // (1 tile = 22 units along X/Z, 1 z-step = 4 units along Y). Color defaults
        // to BackgroundColor so the chunk edge fades into the sky cleanly.
        public static bool FogEnabled = false;
        public static float FogStart = 600f;
        public static float FogEnd = 1800f;
        public static Color FogColor = new Color(70, 130, 180, 255);

        // ===== Atmospheric color lerp =====
        // State migrated to IAtmosphereService (ADR-012 session 58). These statics now
        // delegate so existing writers (Weather3DSystem.SetType, one-shot effects)
        // keep working unchanged. Lerp body lives in AtmosphereService.Tick(dt).
        private static ClassicUO.Renderer.Environment.IAtmosphereService AtmoSvc
            => ClassicUO.Renderer.Core.Renderer3DHost.Services.Atmosphere;

        public static Color BackgroundColorTarget
        {
            get => AtmoSvc.BackgroundColorTarget;
            set => AtmoSvc.SetBackgroundColorTarget(value);
        }
        public static Color FogColorTarget
        {
            get => AtmoSvc.FogColorTarget;
            set => AtmoSvc.SetFogColorTarget(value);
        }
        public static float AtmosphereLerpSpeed
        {
            get => AtmoSvc.AtmosphereLerpSpeed;
            set => AtmoSvc.SetAtmosphereLerpSpeed(value);
        }
        public static float AtmospherePulseAmount
        {
            get => AtmoSvc.AtmospherePulseAmount;
            set => AtmoSvc.SetAtmospherePulseAmount(value);
        }
        public static float AtmospherePulseHz
        {
            get => AtmoSvc.AtmospherePulseHz;
            set => AtmoSvc.SetAtmospherePulseHz(value);
        }

        // AtmosphereService.Tick is driven by AtmospherePass.Execute (order 600) per the
        // session-64 switchover. The legacy TickAtmosphere(float) entrypoint and the
        // Particle3DSystem.Tick call site have been removed (lesson §S — no double-tick).

        // ===== Diagnostics =====
        public static bool VerboseLog = true;
        public static int LastVisibleChunks;
        public static int LastBuiltChunks;
        public static int LastDrawnChunks;
        public static Vector3 LastCameraTarget;
        public static Vector3 LastCameraEye;

        // Last-frame draw counters captured from LandMesh3D's manual instrumentation
        // (FNA's GraphicsDevice has no Metrics). Ground pass only — does not include
        // statics, multis, player, or attachments.
        public static int LastDrawCalls;
        public static int LastPrimitives;

        // Last view/projection used for the 3D pass — needed by MousePicker3D
        // to unproject a screen click without recomputing matrices.
        public static Matrix LastView;
        public static Matrix LastProjection;
        public static int LastViewportWidth;
        public static int LastViewportHeight;

        private static int _frameCount;

        public static readonly Camera3D Camera = new Camera3D();

        // Terrain GPU resources moved to ITerrainRenderResources (session 63, Phase 3).
        // Resolved through the service container so TerrainPass / GroundOverlayPass and this
        // transitional Draw() share one source of truth.
        private static ClassicUO.Renderer.Terrain.ITerrainRenderResources TerrainResources
            => ClassicUO.Renderer.Core.Renderer3DHost.Services.TerrainRenderResources;
        // Static3DRenderer reads this to draw the foliage overlay pass with the
        // same shader/tunables we use for the ground.
        public  static GroundOverlayEffect GroundOverlayEffect => TerrainResources.GroundOverlay;

        // ===== Foliage season (independent of GroundEffectMode) =====
        // Drives a separate per-leaf shader pass that tints canopies with
        // per-tree-random autumn colors. Designed to compose with wet/snow:
        // user can have "Wet ground + Fall trees" or "Snow + Fall trees" etc.
        public enum FoliageSeasonMode { None = 0, Fall = 1 }
        public static FoliageSeasonMode FoliageSeason = FoliageSeasonMode.None;
        public static float FoliageSeasonIntensity = 0.85f;  // 0..1
        // Mesh cache moved to ITerrainMeshCache (session 62, Phase 3). Resolved through the
        // service container so TerrainPass/GroundOverlayPass and this transitional Draw() share
        // one source of truth.
        private static ClassicUO.Renderer.Terrain.ITerrainMeshCache MeshCache
            => ClassicUO.Renderer.Core.Renderer3DHost.Services.TerrainMeshCache;

        // World is required for the inline-3D NPC pass (iterates world.Mobiles).
        // Safe to pass null — the NPC pass simply skips. Post-session-64 the four
        // visual passes (Terrain / GroundOverlay / StaticGeometry / Mobile) plus the
        // Atmosphere tick run through Renderer3DServices.Passes; this entrypoint keeps
        // global lighting/RT-mode guards, camera setup, inline-NPC draw, and the
        // diagnostic snapshot — none of which are pass-shaped yet.
        public static void Draw(
            GraphicsDevice gd,
            ClassicUO.Game.World world,
            IList<Chunk> visibleChunks,
            float playerUoX, float playerUoY, float playerUoZ,
            int playerFacing,
            int viewportWidth, int viewportHeight
        )
        {
            if (!Enabled || gd == null) return;

            // Phase-1 realtime lighting: advance the client-side sun clock and
            // push the resulting LightDir to every live TextureSkinnedEffect.
            // Cheap (one trig call + a small list scan) and safe to run even
            // when the toggle is OFF (returns the legacy hardcoded direction).
            // Runs BEFORE the MobileRT3D early-return so RT'd mobiles drawn
            // through that path still see the same sun.
            LightingState.Tick();
            TextureSkinnedEffect.BroadcastLightDirection(LightingState.CurrentLightDir());

            // RT'ed Mobiles mode trumps the standalone 3D world pass — when on,
            // the 2D world handles all sorting and mobiles ride in via per-mobile
            // RTs blitted by the 2D batcher.
            if (MobileRT3DRenderer.Enabled) return;
            // Reset per-frame draw counters that LandMesh3D increments.
            LandMesh3D.FrameDrawCalls = 0;
            LandMesh3D.FramePrimitives = 0;
            LastVisibleChunks = visibleChunks?.Count ?? 0;
            if (visibleChunks == null || visibleChunks.Count == 0)
            {
                LogPeriodic("no visible chunks");
                return;
            }

            EnsureResources(gd);

            // Camera target = player position in 3D world units (+ optional offset).
            var target = new Vector3(
                playerUoX * LandMesh3D.TILE,
                playerUoZ * LandMesh3D.Z_SCALE,
                playerUoY * LandMesh3D.TILE
            ) + CameraOffset;
            Camera.Target = target;
            // First/third-person/cinematic mode hook — may shift target, pitch,
            // yaw, and eye-override before we build the view matrix.
            CameraModeController.Apply(ref target);
            Camera.Target = target;
            LastCameraTarget = target;

            // Match the orthographic frustum to viewport pixels so 1 world unit
            // = 1 screen pixel at zoom=1. Same scale convention as the iso 2D world.
            Camera.OrthoWidthPixels = viewportWidth;
            float aspect = (float)viewportWidth / System.Math.Max(1, viewportHeight);

            Matrix view, proj;
            if (UseIsoProjection)
            {
                view = Matrix.Identity;
                proj = Camera.IsoViewProjection(viewportWidth, viewportHeight);
            }
            else
            {
                view = Camera.View;
                proj = Camera.Projection(aspect);
            }

            // ===== Phase 3 pipeline (session 64) =====
            // TerrainPass / GroundOverlayPass / StaticGeometryPass / MobilePass /
            // AtmospherePass execute via Renderer3DServices.Passes in canonical order.
            // Each pass owns its own GPU state save/restore.
            var services = ClassicUO.Renderer.Core.Renderer3DHost.Services;
            var frame = services.FrameClock.Current;
            var ctx = new ClassicUO.Renderer.Core.RenderPassContext(
                in frame, gd, Camera, world, visibleChunks,
                playerUoX, playerUoY, playerUoZ, playerFacing,
                viewportWidth, viewportHeight);
            services.Passes.Execute(in ctx);

            // Inline-3D NPC pass — stays here until SkinnedModelGlb is promoted out of
            // Player3DRenderer (playbook entry 39 escape-hatch). Mirrors the RT path's
            // composed-humanoid logic but draws straight into the 3D world (shared depth
            // buffer, world view/proj). Only runs in 3D modes — Classic2D handles NPCs
            // through MobileRT3DRenderer.RenderFrame.
            if (world != null && RenderModeController.MobilesIn3D)
            {
                DrawInlineNpcs(gd, world, view, proj);
            }

            // Snapshot last frame matrices (used by MousePicker3D) + draw counters.
            // FNA's GraphicsDevice does not expose Metrics, so the counts come from
            // LandMesh3D's manual instrumentation (covers ground + ground overlay).
            // Statics/multis/player passes are NOT counted yet.
            LastDrawCalls      = LandMesh3D.FrameDrawCalls;
            LastPrimitives     = LandMesh3D.FramePrimitives;
            LastView           = view;
            LastProjection     = proj;
            LastViewportWidth  = viewportWidth;
            LastViewportHeight = viewportHeight;

            _frameCount++;
            if (VerboseLog && (_frameCount % 300 == 1))
            {
                Console.WriteLine(
                    $"[3DCUO] frame={_frameCount} visible={LastVisibleChunks} built={LastBuiltChunks} drawn={LastDrawnChunks} target={target} pitch={Camera.PitchDegrees} yaw={Camera.YawDegrees} zoom={Camera.Zoom} ortho={Camera.OrthoWidthPixels}"
                );
            }
        }

        // ===== Inline-3D NPC pass =====
        // Iterates non-player human mobiles within MaxRender3DDistance and
        // draws each as a composed humanoid (pack outfit) directly into the
        // 3D world. Unlike the RT path this does NOT allocate per-mobile
        // RenderTarget2Ds — it draws straight into the same depth buffer as
        // walls/multis/statics, so occlusion is correct and free.
        //
        // The shared rig (Player3DRenderer.Model) is also used for every NPC.
        // We snapshot the player's pose + LastWorldMatrix once, swap each NPC's
        // pose + matrix in for its draw, then restore so the next frame's
        // player draw doesn't inherit the last NPC's state.
        public static int LastInlineNpcsDrawn;

        /// <summary>
        /// Per-frame delta for inline NPC animation. Reads from the renderer's
        /// authoritative <see cref="ClassicUO.Renderer.Core.IFrameClock"/> via
        /// <see cref="ClassicUO.Renderer.Core.Renderer3DHost"/>. Closes review finding #2
        /// (eliminates the third divergent clock source).
        /// </summary>
        private static float ConsumeInlineNpcDt()
        {
            return ClassicUO.Renderer.Core.Renderer3DHost.IsBound
                ? ClassicUO.Renderer.Core.Renderer3DHost.Services.FrameClock.Current.DeltaSeconds
                : 0f;
        }

        private static void DrawInlineNpcs(GraphicsDevice gd, ClassicUO.Game.World world, Matrix view, Matrix proj)
        {
            var player = world.Player;
            if (player == null) return;
            if (Player3DRenderer.Model == null) return;

            Mobile3DIndex.EnsureLoaded();
            Mobile3DAnimDriver.Tick();

            var savedWorldMatrix = Player3DRenderer.LastWorldMatrix;
            var savedPose = Mobile3DAnimDriver.SnapshotPose();
            int max = MobileRT3DRenderer.MaxRender3DDistance;
            float dt = ConsumeInlineNpcDt();

            int drawn = 0;
            foreach (var m in world.Mobiles.Values)
            {
                if (m == null || m == player || m.IsDestroyed) continue;
                if (m.Distance > max) continue;
                if (!m.IsHuman) continue;

                string typeName = Mobile3DIndex.ResolveTypeFromMobile(m);
                if (!Mobile3DIndex.TryGet(typeName, m.Graphic, out var entry)) continue;
                var outfit = MobileOutfitRegistry.GetOrPick(m.Serial, entry.Pack);
                if (outfit == null) continue;

                // Per-NPC anim — write this NPC's pose into the shared rig.
                Mobile3DAnimDriver.UpdateMotion(m);
                Mobile3DAnimDriver.ApplyNpcPose(m, dt);

                // Per-NPC world matrix — same composition Player3DRenderer.Draw
                // uses so attachments (which premultiply by LastWorldMatrix)
                // appear at the NPC's position with the NPC's facing.
                float wx = m.X * 22f;
                float wy = m.Z * 4f;
                float wz = m.Y * 22f;
                float autoYaw = Player3DRenderer.AutoYawFromFacing
                    ? Player3DRenderer.UoDirectionToYaw((int)(m.Direction & ClassicUO.Game.Data.Direction.Mask)) + Player3DRenderer.AutoYawOffsetDegrees
                    : 0f;
                float effectiveYaw = autoYaw + Player3DRenderer.ModelYawDegrees;
                Player3DRenderer.LastWorldMatrix =
                    Matrix.CreateScale(Player3DRenderer.ModelScale) *
                    Matrix.CreateRotationX(MathHelper.ToRadians(Player3DRenderer.ModelPitchDegrees)) *
                    Matrix.CreateRotationY(MathHelper.ToRadians(effectiveYaw)) *
                    Matrix.CreateRotationZ(MathHelper.ToRadians(Player3DRenderer.ModelRollDegrees)) *
                    Matrix.CreateTranslation(wx, wy + Player3DRenderer.ModelYOffset, wz);

                // Draw composed pack outfit. Body is NOT drawn here — pack
                // pieces (Torso/Arms/Legs/etc.) cover the base mesh.
                AttachmentRenderer.DrawWithOverrides(gd, view, proj, outfit);
                drawn++;
            }

            // Restore so the next frame's player draw starts from a clean slate.
            Player3DRenderer.LastWorldMatrix = savedWorldMatrix;
            Mobile3DAnimDriver.RestorePose(savedPose);
            LastInlineNpcsDrawn = drawn;
        }

        // LogPeriodic is called from early-return paths (no chunks visible), distinct from
        // the main Draw() flow that increments _frameCount. It used to share _frameCount,
        // double-counting frames and firing the log at the wrong cadence (review finding #3).
        // It now keeps its own counter so the cadence is honest in both paths.
        private static long _logPeriodicFrame;
        private static void LogPeriodic(string msg)
        {
            if (!VerboseLog) return;
            _logPeriodicFrame++;
            if (_logPeriodicFrame % 300 == 1)
            {
                Console.WriteLine($"[3DCUO] {msg}");
            }
        }

        public static void InvalidateAllMeshes() => MeshCache.Clear();

        // Terrain GPU resources (BasicEffect, RasterizerState, GroundOverlayEffect) are
        // allocated + tracked for shutdown disposal by TerrainRenderResources — see
        // session 63 lift. This method is now a thin shim that forwards to the service.
        private static void EnsureResources(GraphicsDevice gd)
            => TerrainResources.EnsureLoaded(gd);

        // Ground-effect ease + wet/snow overlay draw moved to GroundOverlayPass
        // (session 64 switchover). The pass owns SeasonCycleDriver.Tick() and reads
        // dt from the IFrameClock instead of sampling Environment.TickCount64.
    }
}
