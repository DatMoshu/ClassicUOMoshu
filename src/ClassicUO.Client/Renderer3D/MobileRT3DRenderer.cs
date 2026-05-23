// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — RT'ed Mobiles mode.
// When Enabled, each mobile (currently: player only) is rendered as a 3D skinned
// model into its own pooled RenderTarget2D. The RT is then submitted to the 2D
// UltimaBatcher2D as a regular sprite at the mobile's screen anchor with the
// existing 2D `depth` value. This makes 3D mobiles sort correctly against
// statics, walls, items and other mobiles via the original 2D batcher.
//
// This mode TRUMPS all other 3D toggles: World3DRenderer.Draw early-returns
// when this is on, and the GameScene 2D-suppression / depth-clear paths skip
// their 3D-aware branches.

using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer.Mobiles; // MobileOutfit (migrated to Mobiles domain in ADR-012)
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using World = ClassicUO.Game.World;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class MobileRT3DRenderer
    {
        // Master toggle. When true, all other 3D world toggles are suppressed
        // and mobiles render through the 2D batcher via per-mobile RTs.
        public static bool Enabled = false;

        // NPC sub-toggle — proxies the cross-mode RenderModeController.MobilesIn3D
        // flag so existing call sites (WorldRenderGump, MobileView, dump tools)
        // keep compiling. The flag does NOT force-enable the RT master toggle
        // anymore — RenderModeController owns whether the RT path runs at all,
        // based on the active render mode. In 3D modes (Iso3D / Full3D) the
        // inline NPC pass in World3DRenderer reads the same flag.
        public static bool Render3DMobiles
        {
            get => RenderModeController.MobilesIn3D;
            set => RenderModeController.SetMobilesIn3D(value);
        }
        public static bool RenderVendorsAs3D
        {
            get => Render3DMobiles;
            set => Render3DMobiles = value;
        }

        // Cap fill cost: only render NPCs within this many tiles. Default keeps
        // the budget at ~30 RTs in a busy town. Tunable via the debug gump.
        public static int MaxRender3DDistance = 12;

        // Logical RT dimensions, in screen pixels. The on-screen blit always
        // uses these. Actual RT pixel dims = RTWidth*SuperSample × RTHeight*SuperSample.
        // 192x192 comfortably holds a Mixamo-scaled humanoid and leaves padding
        // for limbs swung wide. Bump if models clip the edges.
        public static int RTWidth = 192;
        public static int RTHeight = 192;

        // Supersampling factor — render the model into an N× larger RT and blit
        // it back down at the logical size. Higher = sharper / better-AA model
        // when downsampled by the world batcher. Cost = O(N²) fill rate per frame.
        // 1 = native (no SS). Clamped to 1..4. Camera Zoom is set to SuperSample
        // inside RenderMobile so iso framing (foot margin, anchor) is invariant.
        public static int SuperSample = 1;

        // Vertical screen-space nudge applied to the RT blit. Positive moves
        // the whole model down on screen.
        public static int RTYAnchorOffset = 0;

        // When true, the original 2D player sprite is drawn IN ADDITION to the
        // 3D RT. Useful for A/B comparison: the 2D sprite has a tight bounding
        // box and never gets overlapped by land tiles. Off by default so the
        // 3D model fully replaces the 2D sprite.
        public static bool Show2DPlayerSprite = false;

        // Pixels between the model's projected feet and the RT bottom edge.
        // Pushes the rendered model toward the bottom of the RT so that the
        // RT's blit footprint barely extends south of the player — which
        // matters because land tiles south of the player legitimately draw
        // on top of the RT's lower portion.
        public static int FootMarginFromBottom = 16;

        // Surface format / depth format for the per-mobile RTs.
        // Depth24 because the 3D draw inside the RT does its own depth test
        // (skin self-occlusion, future multi-mobile occlusion).
        private const SurfaceFormat RT_COLOR = SurfaceFormat.Color;
        private const DepthFormat RT_DEPTH = DepthFormat.Depth24;

        private sealed class PoolEntry
        {
            public RenderTarget2D Rt;
            public int LastUsedFrame;
        }

        private static readonly Dictionary<uint, PoolEntry> _pool = new();
        private static readonly Camera3D _camera = new Camera3D();
        private static int _frameCount;
        private const int EVICT_AFTER_FRAMES = 600; // ~10s at 60fps

        // Per-frame entry point — call ONCE per frame BEFORE the 2D batcher
        // binds WorldRenderTarget. Renders all 3D mobiles into their pooled
        // RTs so Mobile.Draw can later blit the cached textures without any
        // mid-batch render-target swap. (Mid-batch RT swaps cause already-
        // committed chunk-mesh pixels — land + multis drawn via DrawDirectIndexed
        // — to be discarded when WorldRenderTarget is rebound on RenderTargetUsage.
        // DiscardContents.)
        public static void RenderFrame(World world, GraphicsDevice gd)
        {
            if (!Enabled || world == null || gd == null) return;
            _frameCount++;

            var player = world.Player;
            if (player != null)
                RenderMobile(player, gd);

            if (Render3DMobiles)
            {
                NpcAnimTick();
                Mobile3DIndex.EnsureLoaded();
                foreach (var m in world.Mobiles.Values)
                {
                    if (m == null || m == player || m.IsDestroyed) continue;
                    if (m.Distance > MaxRender3DDistance) continue;
                    if (!m.IsHuman) continue;

                    string typeName = Mobile3DIndex.ResolveTypeFromMobile(m);
                    if (!Mobile3DIndex.TryGet(typeName, m.Graphic, out var entry))
                        continue; // 2D sprite remains
                    var outfit = MobileOutfitRegistry.GetOrPick(m.Serial, entry.Pack);
                    if (outfit == null) continue;
                    RenderNpc(m, gd, outfit);
                }
            }

            EvictStaleEntries();
        }

        // Returns the pooled RT for a mobile that was rendered this frame.
        // Returns null if the mobile wasn't rendered (e.g. RT mode off, or
        // mobile isn't in the 3D-mobile set).
        public static RenderTarget2D GetCached(Mobile mobile)
        {
            if (!Enabled || mobile == null) return null;
            return _pool.TryGetValue(mobile.Serial, out var e) ? e.Rt : null;
        }

        private static void RenderMobile(Mobile mobile, GraphicsDevice gd)
        {
            var entry = GetOrCreate(gd, mobile.Serial);
            entry.LastUsedFrame = _frameCount;

            // ===== Save current RT bindings =====
            // The 2D batcher has WorldRenderTarget bound and queued sprites in
            // CPU memory; switching RTs here is safe as long as we restore the
            // binding before End() flushes. We don't issue any batcher.Draw calls
            // between the save/restore.
            var prevTargets = gd.GetRenderTargets();

            gd.SetRenderTarget(entry.Rt);
            gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);

            // ===== Build per-mobile iso projection =====
            // Target = mobile's UO world position in 3D world units, BIASED UP
            // in world Y so the model's feet project to (halfH + bias) within
            // the RT — i.e. near the bottom edge instead of the center. This
            // keeps the RT's transparent footprint from extending south of the
            // player onto the next land tile.
            float wx = mobile.X * 22f;
            float wy = mobile.Z * 4f;
            float wz = mobile.Y * 22f;
            // halfH is in LOGICAL pixels — Zoom=SuperSample below collapses
            // the supersampled RT back to logical world coverage so foot
            // margin / Y anchor are invariant under SuperSample changes.
            int halfH = RTHeight / 2;
            float yBias = halfH - FootMarginFromBottom; // world units; iso 1:1 with logical screen px at Zoom=SuperSample
            _camera.Target = new Vector3(wx, wy + yBias, wz);
            int ss = System.Math.Clamp(SuperSample, 1, 4);
            _camera.Zoom = ss;

            Matrix view = Matrix.Identity;
            Matrix proj = _camera.IsoViewProjection(entry.Rt.Width, entry.Rt.Height);

            // ===== Draw the model =====
            // Reuse Player3DRenderer for the player. It owns model loading, anim
            // timing, facing-from-direction, and submesh draw. We force its
            // Enabled=true for the duration of this draw in case the user has
            // toggled it off independently — RT mode trumps that flag.
            if (mobile == mobile.World?.Player)
            {
                bool prevEnabled = Player3DRenderer.Enabled;
                Player3DRenderer.Enabled = true;
                try
                {
                    // IMPORTANT: pass INTEGER tile position here, matching the
                    // RT camera target (which also uses integer-tile world coords).
                    // The 2D batcher composites this RT later at the screen anchor
                    // that already includes Offset interpolation, so adding
                    // interpolation here too would double-apply it and the model
                    // would drift ahead each step then snap back at tile boundary.
                    // Motion-detection state still ticks via the 0.5s grace
                    // timeout, which spans the gap between integer-tile updates.
                    Player3DRenderer.Draw(
                        gd,
                        mobile.X, mobile.Y, mobile.Z,
                        (int)(mobile.Direction & Direction.Mask),
                        view, proj
                    );

                    // Attachments (hat / face / back / hips / shoulders) — must run
                    // INSIDE the RT block so they render into the same per-mobile RT
                    // and blit to screen with the player. The skinning palette uses
                    // Player3DRenderer.Model.WorldMatrices set during the call above.
                    AttachmentRenderer.Draw(gd, view, proj);
                }
                finally
                {
                    Player3DRenderer.Enabled = prevEnabled;
                }
            }
            // TODO: non-player mobiles need per-mobile model + anim state. Out
            // of scope for this RT-mode prototype; structure is here to extend.

            // ===== Restore prior RT binding =====
            if (prevTargets == null || prevTargets.Length == 0)
                gd.SetRenderTarget(null);
            else
                gd.SetRenderTargets(prevTargets);
        }

        // ===== NPC composed-humanoid path =====
        // Per-NPC anim/motion state has been lifted into Mobile3DAnimDriver so
        // the inline-3D NPC pass (World3DRenderer) shares the same dt clock and
        // walk/idle bookkeeping. NpcAnimTick + NpcDeltaTime are thin shims kept
        // for diff hygiene; both delegate to the shared driver.
        private static long _npcLastTickTicks;
        private static void NpcAnimTick() => Mobile3DAnimDriver.Tick();

        private static void RenderNpc(Mobile mobile, GraphicsDevice gd, MobileOutfit outfit)
        {
            var entry = GetOrCreate(gd, mobile.Serial);
            entry.LastUsedFrame = _frameCount;

            var prevTargets = gd.GetRenderTargets();
            gd.SetRenderTarget(entry.Rt);
            gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);

            // Iso camera math — same convention as RenderMobile.
            float wx = mobile.X * 22f;
            float wy = mobile.Z * 4f;
            float wz = mobile.Y * 22f;
            int halfH = RTHeight / 2;
            float yBias = halfH - FootMarginFromBottom;
            _camera.Target = new Vector3(wx, wy + yBias, wz);
            int ss = System.Math.Clamp(SuperSample, 1, 4);
            _camera.Zoom = ss;

            Matrix view = Matrix.Identity;
            Matrix proj = _camera.IsoViewProjection(entry.Rt.Width, entry.Rt.Height);

            // Per-NPC anim/motion via the shared Mobile3DAnimDriver. Snapshot
            // the player's current pose, swap in this NPC's pose, draw the
            // composed outfit through AttachmentRenderer, then restore — so
            // the next caller (player draw, next NPC) doesn't inherit our pose.
            var model = Player3DRenderer.Model;
            if (model != null && model.Animations != null && model.Animations.Length > 0)
            {
                Mobile3DAnimDriver.UpdateMotion(mobile);
                var snap = Mobile3DAnimDriver.SnapshotPose();
                Mobile3DAnimDriver.ApplyNpcPose(mobile, NpcDeltaTime());

                // Compose pack outfit through AttachmentRenderer; the player's
                // base skinned body is NOT drawn here — pack parts cover the body.
                AttachmentRenderer.DrawWithOverrides(gd, view, proj, outfit);

                Mobile3DAnimDriver.RestorePose(snap);
            }

            if (prevTargets == null || prevTargets.Length == 0)
                gd.SetRenderTarget(null);
            else
                gd.SetRenderTargets(prevTargets);
        }

        private static float NpcDeltaTime()
        {
            long now = System.DateTime.UtcNow.Ticks;
            float dt = _npcLastTickTicks == 0 ? 0f : (float)((now - _npcLastTickTicks) / 10_000_000.0);
            _npcLastTickTicks = now;
            return dt;
        }

        // Vendor variant — renders a non-player human mobile via the shared
        // Vendor3DRenderer model instead of Player3DRenderer. Same iso camera
        // setup as RenderMobile so the RT blits with the same anchor math.
        // Kept for back-compat / debug A/B; no longer reached from RenderFrame.
        private static void RenderVendor(Mobile mobile, GraphicsDevice gd)
        {
            var entry = GetOrCreate(gd, mobile.Serial);
            entry.LastUsedFrame = _frameCount;

            var prevTargets = gd.GetRenderTargets();
            gd.SetRenderTarget(entry.Rt);
            gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);

            float wx = mobile.X * 22f;
            float wy = mobile.Z * 4f;
            float wz = mobile.Y * 22f;
            int halfH = RTHeight / 2;
            float yBias = halfH - FootMarginFromBottom;
            _camera.Target = new Vector3(wx, wy + yBias, wz);
            int ss = System.Math.Clamp(SuperSample, 1, 4);
            _camera.Zoom = ss;

            Matrix view = Matrix.Identity;
            Matrix proj = _camera.IsoViewProjection(entry.Rt.Width, entry.Rt.Height);

            Vendor3DRenderer.Draw(gd, mobile, view, proj);

            if (prevTargets == null || prevTargets.Length == 0)
                gd.SetRenderTarget(null);
            else
                gd.SetRenderTargets(prevTargets);
        }

        // Called from RenderFrame to evict pool entries unused for a while.
        // Cheap — runs every 256 frames at most.
        private static void EvictStaleEntries()
        {
            if ((_frameCount & 0xFF) != 0) return;

            List<uint> dead = null;
            foreach (var kvp in _pool)
            {
                if (_frameCount - kvp.Value.LastUsedFrame > EVICT_AFTER_FRAMES)
                {
                    dead ??= new List<uint>();
                    dead.Add(kvp.Key);
                }
            }
            if (dead == null) return;
            foreach (var key in dead)
            {
                if (_pool.TryGetValue(key, out var e))
                {
                    e.Rt?.Dispose();
                    _pool.Remove(key);
                }
                Vendor3DRenderer.DropMobile(key);
                Mobile3DAnimDriver.Drop(key);
                MobileOutfitRegistry.Drop(key);
            }
        }

        public static void DisposeAll()
        {
            foreach (var e in _pool.Values) e.Rt?.Dispose();
            _pool.Clear();
        }

        private static PoolEntry GetOrCreate(GraphicsDevice gd, uint key)
        {
            int ss = System.Math.Clamp(SuperSample, 1, 4);
            int wantW = RTWidth * ss;
            int wantH = RTHeight * ss;
            if (_pool.TryGetValue(key, out var e))
            {
                // Re-create if the user changed the RT size or SuperSample at runtime.
                if (e.Rt == null || e.Rt.Width != wantW || e.Rt.Height != wantH)
                {
                    e.Rt?.Dispose();
                    e.Rt = AllocRt(gd);
                }
                return e;
            }
            e = new PoolEntry { Rt = AllocRt(gd) };
            _pool[key] = e;
            return e;
        }

        private static RenderTarget2D AllocRt(GraphicsDevice gd)
        {
            int ss = System.Math.Clamp(SuperSample, 1, 4);
            return new RenderTarget2D(
                gd,
                RTWidth * ss,
                RTHeight * ss,
                mipMap: false,
                preferredFormat: RT_COLOR,
                preferredDepthFormat: RT_DEPTH,
                preferredMultiSampleCount: 0,
                usage: RenderTargetUsage.DiscardContents
            );
        }
    }
}
