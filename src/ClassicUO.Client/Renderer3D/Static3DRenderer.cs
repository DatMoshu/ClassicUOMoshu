// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 5: render Static GameObjects as depth-aware billboards.
//
// Each static (tree, rock, sign, plant, decoration, etc.) becomes one camera-
// facing textured quad anchored at the bottom-center of the tile, sized 1:1
// with the underlying art's pixel dimensions (1 world unit = 1 iso pixel,
// matching LandMesh3D's coordinate convention).
//
// Billboards are cylindrical (rotate around world Y only) so trees stay
// vertical. Right vector = the iso projection's screen-X in world space:
// world (1, 0, -1) / √2 — derived from UO's iso math
// (screen.x = (uoX - uoY) * 22). Up vector = world (0, 1, 0).
//
// Depth is read AND written using AlphaTestEffect with reference alpha 128
// so transparent foliage edges don't poison the depth buffer (alpha-blend
// would cause back-to-front sort artifacts between overlapping trees).
//
// Quads are batched per Texture2D — almost everything lives on the same
// 2048×2048 Arts atlas, so the chunk usually collapses to one draw call.

using System;
using System.Collections.Generic;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class Static3DRenderer
    {
        // Default OFF — opt-in via debug gump. When enabled in tandem with the
        // 2D world, every Static (rocks, trees, walls, decoration) renders twice
        // (2D + 3D billboard), creating visual noise. The "Pure 3D" macro button
        // in the debug gump enables this together with Disable2DWorld.
        public static bool Enabled = false;
        public static bool VerboseLog = true;
        // Iris 2 GLB statics: when true, any Static whose graphic resolves through
        // Iris2StaticRegistry replaces its billboard with the registered GLB.
        // Walls remain owned by WallMeshRegistry — iris2 only fills the gap.
        // GPLv3-tainted reference assets, debug-only — never ship.
        public static bool Use3DIris2Statics = false;
        // "100% Iris" preview: when true, Iris 2 wins over walls AND multi components
        // wherever the registry has a hit. Anything not in the registry still falls
        // back to its normal renderer (walls / multi quads / billboards). Implies
        // Use3DIris2Statics. Pure visual preview — GPLv3 boundary unchanged.
        public static bool Iris2OverrideAll = false;
        public static int LastIris2Meshes;
        // When true, Multi3DRenderer skips Static objects so they go through
        // this billboard path instead of being treated as wall/floor geometry.
        public static bool BillboardAllStatics = false;
        // Pixels with alpha >= AlphaCutoff are drawn (and write depth). Lower
        // values keep more foliage visible at the cost of slightly soft edges
        // in the depth buffer. UO art is mostly fully-opaque or fully-transparent
        // so a low cutoff (1) recovers stylized leaves that would otherwise vanish.
        public static int AlphaCutoff = 1;

        // When true, route every Static through StaticClassifier and choose
        // billboard vs flat ground-decal per kind. When false, every Static
        // billboards (legacy behavior) — useful for A/B comparison.
        public static bool ClassifyStatics = true;

        // Billboard a curated allow-list of Item graphics (server-spawned items
        // like Barracks 0x0062 and FactionStone 0xEDC) using the same path as
        // Static billboards. The chunk traversal normally skips Items because
        // the 2D renderer handles them, but in 3D mode the player camera can
        // pitch above them and they would otherwise be invisible.
        public static bool BillboardItems = true;
        public static readonly System.Collections.Generic.HashSet<ushort> BillboardItemAllowList = new()
        {
            0x0062, // Barracks placeholder graphic
            0xEDC,  // Built-in FactionStone
        };
        public static int LastBillboardedItems;

        // Lift ground-decal quads above the land mesh by this many world Y units
        // so they don't Z-fight the heightmap.
        public static float GroundDecalLift = 0.2f;

        // Per-tile camera-right offset for trees, in world units, to keep
        // overlapping tree billboards from fighting at the same depth. Invisible
        // along the camera-right axis (no parallax along the view direction).
        public static float TreeZBiasMagnitude = 0.4f;

        // ---- 3D tree rendering ----
        // OriginalBillboard: legacy single cylindrical billboard (camera-facing).
        // CrossedPlanes3D: same UO sprite duplicated into N quads at fixed world
        // yaws so the silhouette reads as a 3D tree from any camera angle. A
        // single camera-facing "leaf overlay" quad showing the upper portion of
        // the sprite is drawn last so the canopy always reads correctly.
        public enum TreeRenderMode { OriginalBillboard, CrossedPlanes3D }
        public static TreeRenderMode TreeMode = TreeRenderMode.OriginalBillboard;

        // CrossedPlanes3D tunables.
        // Trees in UO are TWO statics — a TreeTrunk (single plane, no
        // duplication) and a TreeLeaf (the canopy sprite, duplicated as
        // crossed planes for 3D volume). Leaf textures may be swapped for a
        // recolored season-aware Texture2D via TreeTextureCache.
        // 4 leaf planes by default — fuller canopy when 3D mode is engaged.
        // The slider still allows 1..4; 4 just looks correct out of the box.
        public static int   LeafPlaneCount  = 4;   // 1..4 extra leaf planes
        public static float LeafPlaneYawDeg = 60f; // step between leaf planes

        // ---- Wind sway (controlled from WindGump) ----
        //   Uniform           — all leaf planes share one yaw offset; the
        //                        canopy leans together as one combined mesh.
        //   PerPlanePhase     — each plane uses its own sin(phase + i*offset)
        //                        so the planes sway independently.
        //   FirstPlaneOnly    — only the front plane (plane 0) sways; the
        //                        back planes stay anchored. Reads as wind-
        //                        ruffled fronds in front of a stable canopy.
        public enum LeafSwayModeT { Uniform, PerPlanePhase, FirstPlaneOnly }
        public static LeafSwayModeT LeafSwayMode    = LeafSwayModeT.Uniform;
        public static bool  LeafPlaneWindEnabled    = false;
        public static float LeafPlaneWindAmpDeg     = 4f;       // sub-degree precision via x10 slider
        public static float LeafSwayPhasePerPlane   = 0.9f;     // radians per plane in PerPlanePhase mode
        public static float LeafSwayBobAmount       = 0f;       // world-Y bob amplitude (0 = off)
        public static bool  LeafSwaySmoothstep      = false;    // smoothstep(sample) for slower turnaround at extremes
        // Per-tree phase jitter so neighbouring trees don't sway in lockstep
        // (looks more natural than one synchronized wave across the forest).
        public static bool  LeafSwayPerTreePhase    = true;
        public static float LeafSwayPerTreeAmount   = 1.7f;     // radians of jitter per hash unit

        // Z-fighting helpers (apply to both modes).
        // Sub-pixel deterministic Y nudge so neighbouring trees don't share the
        // same world-Y plane. World units (very small).
        public static float TreeYJitter         = 0.05f;
        // Apply a slight rasterizer DepthBias when drawing the static batch so
        // tree fragments win against ground decals at the same depth.
        public static bool  UseTreeDepthBias    = true;
        // Sort visible chunks back-to-front by camera distance before emission
        // so overlapping trees / crossed planes draw in deterministic order.
        public static bool  SortTreesBackToFront = true;

        // Diagnostics
        public static int LastStaticSeen;
        public static int LastBillboards;
        public static int LastGroundDecals;
        public static int LastTextures;
        public static int LastSkipped;
        private static int _frameCount;

        // Last visible-chunks list captured during Draw — used by debug dumps so
        // they iterate the same set the renderer just rendered.
        public static IList<Chunk> LastVisibleChunks;

        private const float TILE = LandMesh3D.TILE;       // 22f
        private const float Z_SCALE = LandMesh3D.Z_SCALE; // 4f

        // Camera-facing right axis (cylindrical: Y-locked). Recomputed from the
        // view matrix each frame in Draw(); fallback to iso right if degenerate.
        private static readonly Vector3 ISO_RIGHT_FALLBACK = Vector3.Normalize(new Vector3(1f, 0f, -1f));
        private static readonly Vector3 BILLBOARD_UP = Vector3.UnitY;
        private static Vector3 _billboardRight = ISO_RIGHT_FALLBACK;

        private static AlphaTestEffect _effect;
        private static RasterizerState _wireframeState;
        private static RasterizerState _depthBiasState;

        private static RasterizerState GetWireframeState()
        {
            return _wireframeState ??= new RasterizerState
            {
                FillMode = FillMode.WireFrame,
                CullMode = CullMode.None
            };
        }

        private static RasterizerState GetDepthBiasState()
        {
            // Slight negative bias pulls tree fragments toward the camera so
            // they win the depth test against coplanar ground decals.
            return _depthBiasState ??= new RasterizerState
            {
                CullMode = CullMode.None,
                DepthBias = -1e-5f,
                SlopeScaleDepthBias = -1f
            };
        }

        // Reusable per-texture batches (Position + Texture; no vertex color in v1).
        private static readonly Dictionary<Texture2D, List<VertexPositionTexture>> _batches = new();
        private static readonly List<Texture2D> _batchKeys = new();

        // Parallel batches for water-edge / wet statics. Drawn separately so
        // weather effects (BloodMoon → blood-red water) can tint them via
        // effect.DiffuseColor without affecting the rest of the static pass.
        private static readonly Dictionary<Texture2D, List<VertexPositionTexture>> _waterBatches = new();
        private static readonly List<Texture2D> _waterBatchKeys = new();

        // Fade batches: leaf quads whose alpha < 1 are routed here so they
        // can be drawn with vertex color + alpha blend (depth read, no write).
        // The base alpha-test batch can't render gradual fade (it's binary
        // pass/fail), so faded leaves need their own pass.
        private static readonly Dictionary<Texture2D, List<VertexPositionColorTexture>> _leafFadeBatches = new();
        private static readonly List<Texture2D> _leafFadeKeys = new();

        // Ground-decal batches: flat statics that lie on the ground (grass tufts,
        // rocks, sticks, water-edge tiles). Carry a per-vertex Color so we can
        // bake the same Lambert lighting that LandMesh3D applies to the land
        // mesh under them — without this, decals render at full brightness
        // while land sits at ~0.85 (cos(45°)/2 + 0.5), producing a visible
        // brightness mismatch where the decal connects to the land mesh.
        // Split water vs non-water so weather (BloodMoon → blood-red water)
        // can still tint the wet ones via DiffuseColor.
        private static readonly Dictionary<Texture2D, List<VertexPositionColorTexture>> _groundDecalBatches = new();
        private static readonly List<Texture2D> _groundDecalKeys = new();
        private static readonly Dictionary<Texture2D, List<VertexPositionColorTexture>> _waterDecalBatches = new();
        private static readonly List<Texture2D> _waterDecalKeys = new();

        // ===== Foliage overlay (wet/snow/fall on leaves and trunks) =====
        // Parallel batches that mirror only the leaf and trunk vertices from
        // the base pass. These are drawn AFTER the base alpha-test pass with
        // the GroundOverlay shader's foliage techniques to apply the same
        // wet/snow effect that the ground gets — masked by the leaf/trunk
        // texture's alpha so we don't paint transparent corners.
        // Same Texture2D key as _batches so the foliage shader can sample the
        // correct atlas alpha per draw.
        private static readonly Dictionary<Texture2D, List<VertexPositionTexture>> _leafOverlayBatches  = new();
        private static readonly Dictionary<Texture2D, List<VertexPositionTexture>> _trunkOverlayBatches = new();
        private static readonly List<Texture2D> _leafOverlayKeys  = new();
        private static readonly List<Texture2D> _trunkOverlayKeys = new();

        // UI toggles — exposed in WeatherAdminGump.
        public static bool ApplyOverlayToFoliage = true;  // leaves get wet/snow/fall
        public static bool ApplyOverlayToTrunks  = true;  // trunks get wet (snow on bare trunks looks unnatural)

        // "Drop leaves" — defoliate trees (skip emitting TreeLeaf statics).
        // Used to simulate late-fall / winter bare branches. Two scopes:
        //   Worldwide: every tree everywhere.
        //   Nearby:    only trees within DropLeavesRadius tiles of the player.
        // Implemented as an early-return in EmitBillboard / EmitCrossedLeaves
        // when kind == TreeLeaf (or generic Tree).
        public static bool DropLeavesWorldwide = false;
        public static bool DropLeavesNearby    = false;
        public static int  DropLeavesRadius    = 16;     // tiles

        // Debug: render every WholeTree as if it were a LeafOverlay (force
        // crossed planes). Useful for proving the crossed-plane path still
        // works once trees stop going through it by default.
        public static bool ForceWholeTreeAsLeafOverlay = false;

        // ---- Leaf presence animation ----
        // Smooth replacement for the old DropLeavesWorldwide hard cull.
        // SeasonCycleDriver writes this 0..1 each frame: 1 = full canopy,
        // 0 = bare branches. Below MIN_PRESENCE the LeafOverlay is skipped
        // entirely (avoids a degenerate sub-pixel quad). Evergreens override
        // to 1 via the registry so needles stay full year-round.
        public static float LeafPresence = 1f;

        // How the canopy disappears as presence falls from 1 → 0:
        //   Cull          — hard cut: full canopy until presence < 1, then gone (legacy).
        //   Scale         — shrink toward center; alpha stays 1 (current default).
        //   Fade          — keep full size, fade alpha 1 → 0 with presence.
        //   ScaleThenFade — shrink linearly across 1.0..0.0; alpha stays 1 above
        //                   presence 0.5, then fades 1 → 0 across 0.5..0.0 so the
        //                   shrinking quad doesn't end as a sharp pop.
        public enum LeafFadeModeT { Cull, Scale, Fade, ScaleThenFade }
        public static LeafFadeModeT LeafFadeMode = LeafFadeModeT.Scale;

        // Backwards-compat shims (old toggles map to the enum). Reads always
        // reflect current mode; writes adjust the mode.
        public static bool UseLeafScaleAnim
        {
            get => LeafFadeMode == LeafFadeModeT.Scale || LeafFadeMode == LeafFadeModeT.ScaleThenFade;
            set => LeafFadeMode = value
                ? (LeafFadeMode == LeafFadeModeT.Fade ? LeafFadeModeT.ScaleThenFade : LeafFadeModeT.Scale)
                : (LeafFadeMode == LeafFadeModeT.ScaleThenFade ? LeafFadeModeT.Fade : LeafFadeModeT.Cull);
        }
        public static bool UseLeafFadeAnim
        {
            get => LeafFadeMode == LeafFadeModeT.Fade || LeafFadeMode == LeafFadeModeT.ScaleThenFade;
            set => LeafFadeMode = value
                ? (LeafFadeMode == LeafFadeModeT.Scale ? LeafFadeModeT.ScaleThenFade : LeafFadeModeT.Fade)
                : (LeafFadeMode == LeafFadeModeT.ScaleThenFade ? LeafFadeModeT.Scale : LeafFadeModeT.Cull);
        }

        // Quads with effective presence below this are skipped (sub-pixel).
        private const float LEAF_MIN_PRESENCE = 0.02f;

        // (scale, alpha) for the current LeafFadeMode at this presence.
        public static (float scale, float alpha) ResolveLeafPresence(float pres)
        {
            switch (LeafFadeMode)
            {
                case LeafFadeModeT.Cull:          return (pres >= 1f ? 1f : 0f, 1f);
                case LeafFadeModeT.Scale:         return (pres, 1f);
                case LeafFadeModeT.Fade:          return (1f, pres);
                case LeafFadeModeT.ScaleThenFade:
                {
                    float a = pres >= 0.5f ? 1f : (pres * 2f);
                    return (pres, a);
                }
                default: return (pres, 1f);
            }
        }
        // Per-frame counters by registry kind so the gump can display what
        // the registry is actually classifying in the visible chunks.
        public static int LastWholeTrees;
        public static int LastLeafOverlays;
        public static int LastLeafFadeQuads;

        // Per-frame snapshot of visible tree canopy anchors so other systems
        // (LeafFallSystem) can spawn particles from real trees instead of a
        // generic cylinder around the player. Each entry = (canopy bottom-
        // anchor in world units, canopy height in world units, canopy half-
        // width in world units). Reset at the top of Draw, appended to inside
        // EmitCrossedLeaves / EmitBillboard for tree-classified statics.
        public readonly struct TreeAnchor
        {
            public readonly Vector3 Anchor;     // bottom-center of canopy quad
            public readonly float   HeightPx;   // canopy quad height (world units)
            public readonly float   HalfWidth;  // canopy quad half-width
            public TreeAnchor(Vector3 a, float h, float w) { Anchor = a; HeightPx = h; HalfWidth = w; }
        }
        private const int TREE_ANCHOR_CAP = 512;
        public static readonly List<TreeAnchor> LastTreeAnchors = new(TREE_ANCHOR_CAP);
        // Player tile position pushed in by World3DRenderer each frame so the
        // radius check works without reaching back into the global World object.
        public static int  PlayerTileX;
        public static int  PlayerTileY;

        public static void Draw(
            GraphicsDevice gd,
            IList<Chunk> visibleChunks,
            Matrix view,
            Matrix projection
        )
        {
            if (!Enabled || gd == null) return;
            LastVisibleChunks = visibleChunks;
            if (visibleChunks == null || visibleChunks.Count == 0) return;

            EnsureResources(gd);
            TreeTextureCache.SetDevice(gd);

            // Cylindrical billboard: derive the world-space right axis from the
            // camera by inverting the view matrix and zeroing Y so quads stay
            // upright while their face turns with camera yaw.
            Matrix invView = Matrix.Invert(view);
            Vector3 camRight = invView.Right;
            camRight.Y = 0f;
            float rLen2 = camRight.LengthSquared();
            _billboardRight = rLen2 > 1e-6f
                ? camRight / MathF.Sqrt(rLen2)
                : ISO_RIGHT_FALLBACK; // looking straight down — keep last-good axis

            // Reset batches (keep capacity).
            for (int i = 0; i < _batchKeys.Count; i++)
                _batches[_batchKeys[i]].Clear();
            _batchKeys.Clear();
            for (int i = 0; i < _waterBatchKeys.Count; i++)
                _waterBatches[_waterBatchKeys[i]].Clear();
            _waterBatchKeys.Clear();
            for (int i = 0; i < _groundDecalKeys.Count; i++)
                _groundDecalBatches[_groundDecalKeys[i]].Clear();
            _groundDecalKeys.Clear();
            for (int i = 0; i < _waterDecalKeys.Count; i++)
                _waterDecalBatches[_waterDecalKeys[i]].Clear();
            _waterDecalKeys.Clear();

            // Reset foliage overlay batches the same way.
            for (int i = 0; i < _leafOverlayKeys.Count; i++)
                _leafOverlayBatches[_leafOverlayKeys[i]].Clear();
            _leafOverlayKeys.Clear();
            for (int i = 0; i < _trunkOverlayKeys.Count; i++)
                _trunkOverlayBatches[_trunkOverlayKeys[i]].Clear();
            _trunkOverlayKeys.Clear();

            // Reset leaf fade batches.
            for (int i = 0; i < _leafFadeKeys.Count; i++)
                _leafFadeBatches[_leafFadeKeys[i]].Clear();
            _leafFadeKeys.Clear();

            // Reset visible-tree anchor snapshot.
            LastTreeAnchors.Clear();

            // Iris 2 mesh per-frame instance buffer. Begin is idempotent within a
            // frame, so Multi3DRenderer may already have called it.
            if (Use3DIris2Statics || Iris2OverrideAll) Iris2StaticDrawer.Begin();

            int seen = 0, skipped = 0, decals = 0, wholeTrees = 0, leafOverlays = 0;

            // Optional back-to-front chunk sort. Cheap (visible chunks count is
            // small) and removes draw-order ties between overlapping trees.
            IList<Chunk> chunks = visibleChunks;
            if (SortTreesBackToFront)
            {
                Vector3 camPos = invView.Translation;
                var sorted = new List<Chunk>(visibleChunks.Count);
                for (int i = 0; i < visibleChunks.Count; i++)
                    if (visibleChunks[i] != null) sorted.Add(visibleChunks[i]);
                sorted.Sort((a, b) =>
                {
                    float ax = (a.X * 8 + 4) * TILE;
                    float az = (a.Y * 8 + 4) * TILE;
                    float bx = (b.X * 8 + 4) * TILE;
                    float bz = (b.Y * 8 + 4) * TILE;
                    float da = (ax - camPos.X) * (ax - camPos.X) + (az - camPos.Z) * (az - camPos.Z);
                    float db = (bx - camPos.X) * (bx - camPos.X) + (bz - camPos.Z) * (bz - camPos.Z);
                    return db.CompareTo(da); // farther first
                });
                chunks = sorted;
            }

            for (int ci = 0; ci < chunks.Count; ci++)
            {
                var chunk = chunks[ci];
                if (chunk == null) continue;

                for (int ty = 0; ty < 8; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                    {
                        if (obj.IsDestroyed) continue;
                        if (obj is not Static s) continue;
                        seen++;
                        if (!s.AllowedToDraw) { skipped++; continue; }

                        // Classic transparent-roof: re-use the per-frame
                        // _maxZ that Multi3DRenderer captured this frame so
                        // tree canopies and overhead foliage hide when the
                        // player walks under them.
                        if (Multi3DRenderer.HideAbovePlayerZ &&
                            s.Z >= Multi3DRenderer.LastFrameMaxZ)
                        {
                            skipped++;
                            continue;
                        }

                        // Iris 2 wins over walls when override is on. Multi3DRenderer
                        // already submitted the same instance to the iris2 drawer,
                        // so we just skip the billboard here. The drawer's frame-guard
                        // means double-Begin from both renderers is harmless.
                        if (Iris2OverrideAll && Iris2StaticRegistry.TryGet(s.Graphic, out _))
                        {
                            skipped++;
                            continue;
                        }

                        // If this static is a wall-mesh candidate that will be
                        // rendered as a 3D GLB by Multi3DRenderer/WallMeshDrawer,
                        // skip the billboard so we don't draw both.
                        if (Multi3DRenderer.Use3DWallMeshes &&
                            WallMeshRegistry.TryGet(s.Graphic, out _))
                        {
                            skipped++;
                            continue;
                        }

                        // Iris 2 GLB static path. Walls win first (above); iris2
                        // only fires when the wall registry has nothing for this
                        // graphic. We submit to Iris2StaticDrawer and skip the
                        // billboard so we don't draw both.
                        if (Use3DIris2Statics && Iris2StaticRegistry.TryGet(s.Graphic, out var i2Entry))
                        {
                            var i2Model = Iris2StaticRegistry.EnsureModel(gd, s.Graphic, out _);
                            if (i2Model != null)
                            {
                                Iris2StaticDrawer.Submit(i2Model, s.Graphic, s.X, s.Y, s.Z, i2Entry.TileHeight);
                                skipped++; // not a billboard but tallied as "not emitted as quad"
                                continue;
                            }
                        }

                        bool emitted;
                        if (ClassifyStatics)
                        {
                            var kind = StaticClassifier.Classify(s);
                            if (kind == StaticKind.WholeTree)        wholeTrees++;
                            else if (kind == StaticKind.LeafOverlay) leafOverlays++;
                            if (StaticClassifier.IsGroundDecalKind(kind))
                            {
                                emitted = EmitGroundQuad(s);
                                if (emitted) decals++;
                            }
                            else
                            {
                                emitted = EmitBillboard(s, kind);
                            }
                        }
                        else
                        {
                            emitted = EmitBillboard(s, StaticKind.Other);
                        }
                        if (!emitted) skipped++;
                    }
                }
            }
            LastStaticSeen = seen;
            LastSkipped = skipped;
            LastGroundDecals = decals;
            LastWholeTrees = wholeTrees;
            LastLeafOverlays = leafOverlays;

            // ---- Allow-listed Item billboards (Barracks, FactionStone, ...) ----
            int billboardedItems = 0;
            if (BillboardItems && BillboardItemAllowList.Count > 0)
            {
                for (int ci = 0; ci < chunks.Count; ci++)
                {
                    var chunk = chunks[ci];
                    if (chunk == null) continue;
                    for (int ty = 0; ty < 8; ty++)
                    for (int tx = 0; tx < 8; tx++)
                    {
                        for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                        {
                            if (obj.IsDestroyed) continue;
                            if (obj is not ClassicUO.Game.GameObjects.Item it) continue;
                            if (!BillboardItemAllowList.Contains(it.Graphic)) continue;
                            if (EmitItemBillboard(it)) billboardedItems++;
                        }
                    }
                }
            }
            LastBillboardedItems = billboardedItems;

            // Render state — depth on, alpha test (no blend) for crisp foliage edges.
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;
            var prevSampler = gd.SamplerStates[0];

            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = World3DRenderer.Wireframe
                ? GetWireframeState()
                : (UseTreeDepthBias ? GetDepthBiasState() : RasterizerState.CullNone);
            gd.BlendState = BlendState.Opaque; // alpha-tested in shader, no blending
            gd.SamplerStates[0] = SamplerState.PointClamp;

            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            _effect.AlphaFunction = CompareFunction.GreaterEqual;
            _effect.ReferenceAlpha = AlphaCutoff;
            _effect.VertexColorEnabled = false;

            int billboards = 0;
            for (int ki = 0; ki < _batchKeys.Count; ki++)
            {
                var tex = _batchKeys[ki];
                var verts = _batches[tex];
                if (verts.Count == 0) continue;

                _effect.Texture = tex;
                int triCount = verts.Count / 3;
                var arr = verts.ToArray(); // proto: simple

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, triCount);
                }

                billboards += verts.Count / 6;
            }

            // ----- Water-static pass (BloodMoon tint) -----
            // Same alpha-test config as the main pass; the only difference is
            // an optional DiffuseColor red bias when BloodMoon is active so
            // shoreline tiles read as blood-red. Restored after the pass.
            if (_waterBatchKeys.Count > 0)
            {
                bool tint = Weather3DSystem.Type == Weather3DType.BloodMoon;
                Vector3 prevDiffuse = _effect.DiffuseColor;
                if (tint) _effect.DiffuseColor = new Vector3(1.6f, 0.10f, 0.10f);
                for (int ki = 0; ki < _waterBatchKeys.Count; ki++)
                {
                    var tex = _waterBatchKeys[ki];
                    var verts = _waterBatches[tex];
                    if (verts.Count == 0) continue;
                    _effect.Texture = tex;
                    int triCount = verts.Count / 3;
                    var arr = verts.ToArray();
                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        gd.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, triCount);
                    }
                    billboards += verts.Count / 6;
                }
                if (tint) _effect.DiffuseColor = prevDiffuse;
            }

            // ----- Ground-decal pass (Lambert-lit) -----
            // Flat statics that lie on the ground (grass tufts, rocks, sticks,
            // shoreline tiles) carry a per-vertex Lambert color sampled from
            // the underlying Land tile, so the decal's brightness matches the
            // land mesh it sits on. Without this pass the decals would render
            // at full 255 brightness and pop visibly against ~217 land. Same
            // depth-and-alpha-test config as the main pass; the only delta is
            // VertexColorEnabled=true so the AlphaTestEffect multiplies our
            // baked Lambert into the texture sample.
            if (_groundDecalKeys.Count > 0)
            {
                _effect.VertexColorEnabled = true;
                for (int ki = 0; ki < _groundDecalKeys.Count; ki++)
                {
                    var tex = _groundDecalKeys[ki];
                    var verts = _groundDecalBatches[tex];
                    if (verts.Count == 0) continue;
                    _effect.Texture = tex;
                    int triCount = verts.Count / 3;
                    var arr = verts.ToArray();
                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        gd.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, triCount);
                    }
                    billboards += verts.Count / 6;
                }
                _effect.VertexColorEnabled = false;
            }

            // Same for water-edge ground decals — separate pass so BloodMoon
            // weather can tint them blood-red independent of the rest.
            if (_waterDecalKeys.Count > 0)
            {
                bool tintBM = Weather3DSystem.Type == Weather3DType.BloodMoon;
                Vector3 prevDiffuse = _effect.DiffuseColor;
                if (tintBM) _effect.DiffuseColor = new Vector3(1.6f, 0.10f, 0.10f);
                _effect.VertexColorEnabled = true;
                for (int ki = 0; ki < _waterDecalKeys.Count; ki++)
                {
                    var tex = _waterDecalKeys[ki];
                    var verts = _waterDecalBatches[tex];
                    if (verts.Count == 0) continue;
                    _effect.Texture = tex;
                    int triCount = verts.Count / 3;
                    var arr = verts.ToArray();
                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        gd.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, triCount);
                    }
                    billboards += verts.Count / 6;
                }
                _effect.VertexColorEnabled = false;
                if (tintBM) _effect.DiffuseColor = prevDiffuse;
            }

            // ----- Faded leaf pass -----
            // Drawn AFTER the alpha-test pass so opaque leaves win the depth
            // test against any half-transparent versions. DepthRead-only so
            // these don't poison the buffer for downstream passes. Vertex
            // color α modulates the texture α; with low ReferenceAlpha the
            // alpha test only kills fully-empty pixels and the rest blend.
            int leafFadeQuads = 0;
            if (_leafFadeKeys.Count > 0)
            {
                gd.DepthStencilState = DepthStencilState.DepthRead;
                gd.BlendState        = BlendState.NonPremultiplied;
                _effect.VertexColorEnabled = true;
                _effect.AlphaFunction = CompareFunction.Greater;
                _effect.ReferenceAlpha = 0;
                for (int ki = 0; ki < _leafFadeKeys.Count; ki++)
                {
                    var tex = _leafFadeKeys[ki];
                    var verts = _leafFadeBatches[tex];
                    if (verts.Count == 0) continue;
                    _effect.Texture = tex;
                    int triCount = verts.Count / 3;
                    var arr = verts.ToArray();
                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        gd.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, triCount);
                    }
                    leafFadeQuads += verts.Count / 6;
                }
                // Restore for next frame.
                _effect.VertexColorEnabled = false;
                _effect.AlphaFunction = CompareFunction.GreaterEqual;
                _effect.ReferenceAlpha = AlphaCutoff;
            }
            LastLeafFadeQuads = leafFadeQuads;

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
            gd.SamplerStates[0] = prevSampler;

            // Iris 2 GLB statics — flush per-frame instances. Drawer pushes/pops
            // its own render state so this slots in cleanly between passes.
            if (Use3DIris2Statics || Iris2OverrideAll)
            {
                Iris2StaticDrawer.Draw(gd, view, projection);
                LastIris2Meshes = Iris2StaticDrawer.LastInstanceCount;
            }
            else
            {
                LastIris2Meshes = 0;
            }

            // Foliage wet/snow overlay — runs after the base alpha-test pass
            // so leaf/trunk depth is established. Uses the GroundOverlay shader.
            DrawFoliageOverlay(gd, view, projection);

            LastBillboards = billboards;
            LastTextures = _batchKeys.Count;

            _frameCount++;
            if (VerboseLog && (_frameCount <= 5 || _frameCount % 300 == 1))
            {
                Console.WriteLine(
                    $"[3DCUO] Static3D frame={_frameCount} chunks={visibleChunks.Count} seen={LastStaticSeen} billboards={LastBillboards} decals={LastGroundDecals} tex={LastTextures} skipped={LastSkipped}"
                );
            }
        }

        // True if we should skip emitting this leaf static because the
        // "drop leaves" toggle is active for its location. Evergreens
        // (cedar, pine, cypress, yew per tree-statics.json) never drop.
        private static bool ShouldDropLeaf(Static s)
        {
            // Registry override — keep evergreen needles year-round.
            // (Even evergreens lose leaves to a nuke — explosion check below
            // intentionally runs before this early-out.)
            // Explosion force — trees within blast radius go bare for ~10s.
            // Evergreens included: a fireball doesn't care about needle vs leaf.
            if (ExplosionForceSystem.QueryTile(s.X, s.Y, out _, out _, out bool leavesGone)
                && leavesGone)
                return true;

            if (TreeStaticRegistry.TryGet(s.Graphic, out var entry) && !entry.Deciduous)
                return false;

            if (DropLeavesWorldwide) return true;
            if (DropLeavesNearby)
            {
                int dx = s.X - PlayerTileX;
                int dy = s.Y - PlayerTileY;
                if (dx * dx + dy * dy <= DropLeavesRadius * DropLeavesRadius)
                    return true;
            }
            return false;
        }

        private static bool EmitBillboard(Static s, StaticKind kind)
        {
            // Debug override: force WholeTree through the leaf-overlay path
            // (crossed planes). Useful for verifying the crossed-plane code
            // still works after WholeTrees stopped going through it.
            if (ForceWholeTreeAsLeafOverlay && kind == StaticKind.WholeTree)
                kind = StaticKind.LeafOverlay;

            // Defoliation — drop the leaf billboard so only the trunk shows.
            // Generic Tree (single-static, no trunk/leaf split) is also dropped
            // since otherwise we'd hide the whole tree silhouette and a bare
            // patch of ground reads worse than nothing.
            // WholeTree is NEVER dropped — that would erase the tree entirely
            // and leave a bare patch of ground. Only LeafOverlay (the small
            // canopy companion static) responds to defoliation.
            if ((kind == StaticKind.TreeLeaf || kind == StaticKind.Tree || kind == StaticKind.LeafOverlay)
                && ShouldDropLeaf(s))
                return false;

            // WholeTree (registry): single fixed-yaw billboard, never crossed.
            // The sprite already contains both trunk and canopy — duplicating
            // it at offset yaws produces the radiating-trunk artifact this
            // rework exists to fix.
            if (kind == StaticKind.WholeTree)
                return EmitFixedTrunkPlane(s);

            // LeafOverlay (registry): the small canopy overlay companion. This
            // is where crossed planes + seasonal recolor + DropLeaves *should*
            // act. In CrossedPlanes3D mode use the multi-plane emitter; in
            // OriginalBillboard mode treat it as a TreeLeaf so ResolveDrawTexture
            // applies the season-recolored texture.
            if (kind == StaticKind.LeafOverlay)
            {
                if (TreeMode == TreeRenderMode.CrossedPlanes3D)
                    return EmitCrossedLeaves(s);
                kind = StaticKind.TreeLeaf;
            }

            // TreeLeaf in CrossedPlanes3D mode → multi-plane canopy emitter.
            if (kind == StaticKind.TreeLeaf && TreeMode == TreeRenderMode.CrossedPlanes3D)
                return EmitCrossedLeaves(s);

            // TreeTrunk → single fixed-yaw plane (never billboards, never duplicates).
            if (kind == StaticKind.TreeTrunk)
                return EmitFixedTrunkPlane(s);

            // Generic Tree (graphic not split into trunk/leaves) keeps legacy
            // crossed-tree behaviour for backward compatibility.
            if (kind == StaticKind.Tree && TreeMode == TreeRenderMode.CrossedPlanes3D)
                return EmitCrossedTreeLegacy(s);

            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(s.Graphic);
            if (artInfo.Texture == null) return false;
            if (artInfo.UV.Width <= 0 || artInfo.UV.Height <= 0) return false;

            // Anchor at bottom-center of the tile, raised by the static's Z.
            float cx = s.X * TILE + TILE * 0.5f;
            float cz = s.Y * TILE + TILE * 0.5f;
            float wy = s.Z * Z_SCALE;

            bool isTreeKind = StaticClassifier.IsTreeKind(kind);

            // Tree Z-bias: deterministic per-tile nudge along the camera-right
            // axis so adjacent trees never share the same camera-space depth.
            if (isTreeKind && TreeZBiasMagnitude > 0f)
            {
                int h = (s.X * 31 + s.Y * 17) % 7; // 0..6
                float biasUnits = (h - 3) * TreeZBiasMagnitude;
                cx += _billboardRight.X * biasUnits;
                cz += _billboardRight.Z * biasUnits;
            }

            // Sub-pixel Y nudge so trees on the same row don't share an exact
            // Y plane (still-coplanar trees can z-fight even with X/Z bias).
            if (isTreeKind && TreeYJitter > 0f)
            {
                int hy = (s.X * 13 + s.Y * 7) % 5; // 0..4
                wy += hy * TreeYJitter;
            }

            float wPx = artInfo.UV.Width;
            float hPx = artInfo.UV.Height;

            // Per-static leaf-presence scale (LeafOverlay/TreeLeaf only).
            // Evergreens override to 1 via the registry. Below MIN_PRESENCE
            // the canopy is sub-pixel — skip emission.
            // origHPx tracks the unscaled height so we can lift the anchor by
            // half the difference and scale around the canopy's CENTER instead
            // of its bottom (otherwise leaves "drain into the ground").
            float origHPx = hPx;
            float leafScale = 1f;
            if (kind == StaticKind.TreeLeaf || kind == StaticKind.LeafOverlay)
            {
                float pres = LeafPresence;
                if (TreeStaticRegistry.TryGet(s.Graphic, out var regE) && !regE.Deciduous)
                    pres = 1f;
                else if (TreeDefoliationStagger.Enabled)
                    pres = TreeDefoliationStagger.Sample(s.X, s.Y, s.Graphic, pres);
                if (UseLeafScaleAnim)
                {
                    if (pres <= LEAF_MIN_PRESENCE) return false;
                    wPx *= pres;
                    hPx *= pres;
                    leafScale = pres;
                }
                else if (pres < 1f)
                {
                    return false;   // legacy hard cull
                }
            }

            // Bottom-center anchor; build the quad in cylindrical-billboard space.
            // Lift anchor for shrinking leaves so the canopy stays centered
            // on its rest position (no settling-into-ground artifact).
            float leafLift = (1f - leafScale) * origHPx * 0.5f;
            Vector3 right = _billboardRight * (wPx * 0.5f);
            Vector3 up = BILLBOARD_UP * hPx;
            Vector3 anchor = new Vector3(cx, wy + leafLift, cz);

            Vector3 bl = anchor - right;
            Vector3 br = anchor + right;
            Vector3 tl = bl + up;
            Vector3 tr = br + up;

            // ===== Explosion-force tree bend =====
            // Tilt the canopy by displacing the TOP two verts horizontally away
            // from any nearby explosion. Bottom stays planted so the trunk
            // visually pivots from the base — quick to read, no math beyond a
            // top-vert add. Skip on non-tree statics (trunks bend negligibly
            // in real life and bending bushes/rocks looks weird).
            if (isTreeKind &&
                ExplosionForceSystem.Query(cx, cz, out float bendX, out float bendZ, out _))
            {
                Vector3 bend = new Vector3(bendX, 0f, bendZ);
                tl += bend;
                tr += bend;
            }

            // Texture + UV resolution: leaf statics may be substituted with a
            // season-aware recolored texture (whole texture, full 0..1 UVs);
            // everything else uses the atlas sub-region.
            ResolveDrawTexture(s, kind, in artInfo,
                out var tex, out float uMin, out float vMin, out float uMax, out float vMax);

            var list = GetBatch(tex);
            // Tri 1: TL, TR, BL
            list.Add(new VertexPositionTexture(tl, new Vector2(uMin, vMin)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            // Tri 2: BL, TR, BR
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(br, new Vector2(uMax, vMax)));

            // Mirror leaf/trunk billboards into the foliage overlay batches so
            // the wet/snow/fall shader passes paint them. Generic Tree (kind
            // not split into trunk/leaf) is treated as a leaf for the overlay
            // so single-sprite trees still get the seasonal/weather effect.
            if (ShouldDrawFoliageOverlay())
            {
                if ((kind == StaticKind.TreeLeaf || kind == StaticKind.Tree) && ApplyOverlayToFoliage)
                    AppendOverlayQuad(GetLeafOverlayBatch(tex), tl, tr, bl, br, uMin, vMin, uMax, vMax);
                else if (kind == StaticKind.TreeTrunk && ApplyOverlayToTrunks)
                    AppendOverlayQuad(GetTrunkOverlayBatch(tex), tl, tr, bl, br, uMin, vMin, uMax, vMax);
            }
            return true;
        }

        // Pick the texture + UV rect to draw with. Substitutes a runtime
        // recolored leaf texture when the static is a TreeLeaf and seasonal
        // recoloring is enabled; falls back to the atlas region otherwise.
        // Per-tile exposure-to-sky gates snow application: a leaf static
        // under a roof gets the season hue but no snow accumulation.
        private static void ResolveDrawTexture(
            Static s, StaticKind kind,
            in ClassicUO.Renderer.SpriteInfo artInfo,
            out Texture2D tex, out float uMin, out float vMin, out float uMax, out float vMax)
        {
            if (kind == StaticKind.TreeLeaf)
            {
                bool exposed = IsStaticExposedToSky(s);
                var recolored = TreeTextureCache.Get(s.Graphic, applySnow: exposed);
                if (recolored != null)
                {
                    tex = recolored;
                    float invW = 1f / recolored.Width;
                    float invH = 1f / recolored.Height;
                    uMin = 0.5f * invW;
                    vMin = 0.5f * invH;
                    uMax = 1f - 0.5f * invW;
                    vMax = 1f - 0.5f * invH;
                    return;
                }
            }

            tex = artInfo.Texture;
            float aInvW = 1f / tex.Width;
            float aInvH = 1f / tex.Height;
            uMin = (artInfo.UV.X + 0.5f) * aInvW;
            vMin = (artInfo.UV.Y + 0.5f) * aInvH;
            uMax = uMin + (artInfo.UV.Width  - 1f) * aInvW;
            vMax = vMin + (artInfo.UV.Height - 1f) * aInvH;
        }

        // True when no IsRoof static covers the tile this static sits on.
        // Looks up the chunk via the static's owning world.
        private static bool IsStaticExposedToSky(Static s)
        {
            try
            {
                var map = s.World?.Map;
                if (map == null) return true;
                var chunk = map.GetChunk(s.X >> 3, s.Y >> 3);
                if (chunk == null) return true;
                return chunk.IsTileExposedToSky(s.X & 7, s.Y & 7);
            }
            catch { return true; }
        }

        // TreeTrunk: one fixed-yaw quad anchored at the tile, no billboard,
        // no duplication. Z-bias and Y-jitter still applied for z-fighting.
        private static bool EmitFixedTrunkPlane(Static s)
        {
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(s.Graphic);
            if (artInfo.Texture == null) return false;
            if (artInfo.UV.Width <= 0 || artInfo.UV.Height <= 0) return false;

            float cx = s.X * TILE + TILE * 0.5f;
            float cz = s.Y * TILE + TILE * 0.5f;
            float wy = s.Z * Z_SCALE;
            if (TreeYJitter > 0f)
            {
                int hy = (s.X * 13 + s.Y * 7) % 5;
                wy += hy * TreeYJitter;
            }

            float wPx = artInfo.UV.Width;
            float hPx = artInfo.UV.Height;

            int hyaw = (s.X * 17 + s.Y * 31) & 0xFF;
            float yaw = hyaw * (MathF.PI * 2f / 256f);
            Vector3 right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw)) * (wPx * 0.5f);
            Vector3 up    = BILLBOARD_UP * hPx;
            Vector3 anchor = new Vector3(cx, wy, cz);
            Vector3 bl = anchor - right;
            Vector3 br = anchor + right;
            Vector3 tl = bl + up;
            Vector3 tr = br + up;

            // Tree-bend from explosion force (whole-tree path mirrors EmitBillboard).
            if (ExplosionForceSystem.Query(cx, cz, out float bendX, out float bendZ, out _))
            {
                Vector3 bend = new Vector3(bendX, 0f, bendZ);
                tl += bend;
                tr += bend;
            }

            var tex = artInfo.Texture;
            float invW = 1f / tex.Width;
            float invH = 1f / tex.Height;
            float uMin = (artInfo.UV.X + 0.5f) * invW;
            float vMin = (artInfo.UV.Y + 0.5f) * invH;
            float uMax = uMin + (artInfo.UV.Width  - 1f) * invW;
            float vMax = vMin + (artInfo.UV.Height - 1f) * invH;

            var list = GetBatch(tex);
            list.Add(new VertexPositionTexture(tl, new Vector2(uMin, vMin)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(br, new Vector2(uMax, vMax)));

            // Mirror into the trunk overlay batch so the wet shader can paint it.
            if (ApplyOverlayToTrunks && ShouldDrawFoliageOverlay())
                AppendOverlayQuad(GetTrunkOverlayBatch(tex),
                    tl, tr, bl, br, uMin, vMin, uMax, vMax);
            return true;
        }

        // EmitCrossedLeaves — TreeLeaf static rendered as 1 fixed-yaw plane +
        // (LeafPlaneCount) duplicate planes at offset yaws. Whole sprite is
        // canopy (the trunk lives in a separate TreeTrunk static), so no UV
        // cropping needed. Uses the season-recolored texture if available.
        private static bool EmitCrossedLeaves(Static s)
        {
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(s.Graphic);
            if (artInfo.Texture == null) return false;
            if (artInfo.UV.Width <= 0 || artInfo.UV.Height <= 0) return false;

            float cx = s.X * TILE + TILE * 0.5f;
            float cz = s.Y * TILE + TILE * 0.5f;
            float wy = s.Z * Z_SCALE;
            if (TreeYJitter > 0f)
            {
                int hy = (s.X * 13 + s.Y * 7) % 5;
                wy += hy * TreeYJitter;
            }

            float wPx = artInfo.UV.Width;
            float hPx = artInfo.UV.Height;

            ResolveDrawTexture(s, StaticKind.TreeLeaf, in artInfo,
                out var tex, out float uMin, out float vMin, out float uMax, out float vMax);

            // Per-static effective leaf presence — evergreens override to 1.
            // ResolveLeafPresence picks (geometric scale, vertex alpha) from
            // the active LeafFadeMode (Cull/Scale/Fade/ScaleThenFade).
            float pres = LeafPresence;
            if (TreeStaticRegistry.TryGet(s.Graphic, out var regE) && !regE.Deciduous)
                pres = 1f;
            else if (TreeDefoliationStagger.Enabled)
                pres = TreeDefoliationStagger.Sample(s.X, s.Y, s.Graphic, pres);
            var (presScale, presAlpha) = ResolveLeafPresence(pres);
            if (presScale <= LEAF_MIN_PRESENCE) return false;
            if (presAlpha <= LEAF_MIN_PRESENCE) return false;

            int hyaw = (s.X * 17 + s.Y * 31) & 0xFF;
            float baseYaw = hyaw * (MathF.PI * 2f / 256f);

            // Per-tree phase jitter (radians) — same hash as baseYaw, scaled
            // by user knob. Decorrelates the global wave so the forest doesn't
            // sway in lockstep.
            float treePhase = (LeafPlaneWindEnabled && LeafSwayPerTreePhase)
                ? hyaw * (LeafSwayPerTreeAmount / 256f) * MathHelper.TwoPi
                : 0f;

            // Maximum yaw offset (radians) for plane 0 / uniform mode.
            float maxYawRad = MathHelper.ToRadians(LeafPlaneWindAmpDeg * WindManager.Strength);

            // Sample helper: optionally smoothstep the sin (-1..1 → eased curve)
            // so motion lingers slightly at the extremes instead of accelerating
            // through them. Reduces the "snap" feeling at the zero-crossing.
            float SwayAt(float planeIdx)
            {
                float phaseOffset = treePhase + (LeafSwayMode == LeafSwayModeT.PerPlanePhase
                    ? planeIdx * LeafSwayPhasePerPlane : 0f);
                float s2 = WindManager.SampleAt(phaseOffset);
                if (LeafSwaySmoothstep)
                {
                    // Map sin's -1..1 to a softer cubic; preserves sign.
                    float a = MathF.Abs(s2);
                    float e = a * a * (3f - 2f * a);
                    s2 = (s2 < 0f) ? -e : e;
                }
                return s2;
            }

            // Plane-0 yaw lean used for Uniform/FirstPlaneOnly modes.
            float plane0YawOffset = LeafPlaneWindEnabled ? SwayAt(0f) * maxYawRad : 0f;

            // Optional vertical bob (Y translation in world units), shared
            // across all planes regardless of mode. Driven by the same
            // sample so the bob and yaw move together.
            float bobY = (LeafPlaneWindEnabled && LeafSwayBobAmount > 0f)
                ? SwayAt(0.7853981f /* π/4 phase */) * LeafSwayBobAmount * WindManager.Strength
                : 0f;

            // Scale canopy AROUND ITS GEOMETRIC CENTER (not the bottom).
            // Without the lift, a bottom-anchored quad would collapse downward
            // into the trunk/ground as pres shrinks — leaves "drain into the
            // ground". Lifting the anchor by half the lost height keeps the
            // shrinking canopy centered on its rest position.
            float lift = (1f - presScale) * hPx * 0.5f;
            var anchor = new Vector3(cx, wy + lift + bobY, cz);
            var up     = BILLBOARD_UP * hPx * presScale;
            float halfW = wPx * 0.5f * presScale;

            // Snapshot this canopy for the LeafFallSystem so falling leaves
            // spawn from real trees rather than a generic cylinder. Capped so
            // dense forests don't blow out the list each frame.
            if (LastTreeAnchors.Count < TREE_ANCHOR_CAP)
                LastTreeAnchors.Add(new TreeAnchor(anchor, hPx * presScale, halfW));

            // Faded leaves go to a separate batch drawn with vertex-color alpha
            // blend (no overlay paint — wet/snow on a half-faded canopy looks
            // wrong). Fully-opaque leaves stay on the fast alpha-test path.
            bool faded = presAlpha < 0.999f;
            var list = faded ? null : GetBatch(tex);
            var fadeList = faded ? GetLeafFadeBatch(tex) : null;
            var overlayList = (!faded && ApplyOverlayToFoliage && ShouldDrawFoliageOverlay())
                ? GetLeafOverlayBatch(tex) : null;

            // Helper: combine baseYaw + step + per-plane wind offset.
            float YawForPlane(int planeIdx, float baseYawArg, float stepRad)
            {
                int sign = (planeIdx % 2 == 1) ? +1 : -1;
                int mag  = (planeIdx + 1) / 2;
                float stepYaw = sign * mag * stepRad;

                if (!LeafPlaneWindEnabled)
                    return baseYawArg + stepYaw;

                switch (LeafSwayMode)
                {
                    case LeafSwayModeT.Uniform:
                        return baseYawArg + stepYaw + plane0YawOffset;
                    case LeafSwayModeT.PerPlanePhase:
                        return baseYawArg + stepYaw + SwayAt(planeIdx) * maxYawRad;
                    case LeafSwayModeT.FirstPlaneOnly:
                        return baseYawArg + stepYaw + (planeIdx == 0 ? plane0YawOffset : 0f);
                    default:
                        return baseYawArg + stepYaw;
                }
            }

            // Plane 0: base.
            float yaw0 = YawForPlane(0, baseYaw, 0f);
            if (faded) EmitOnePlaneFaded(fadeList, anchor, up, halfW, yaw0, uMin, vMin, uMax, vMax, presAlpha);
            else       EmitOnePlane(list, anchor, up, halfW, yaw0, uMin, vMin, uMax, vMax, overlayList);

            // Planes 1..N: distributed +step, -step, +2step, -2step around base.
            int extraPlanes = Math.Clamp(LeafPlaneCount, 0, 4);
            float yawStepRad = MathHelper.ToRadians(LeafPlaneYawDeg);
            for (int i = 0; i < extraPlanes; i++)
            {
                float yawN = YawForPlane(i + 1, baseYaw, yawStepRad);
                if (faded) EmitOnePlaneFaded(fadeList, anchor, up, halfW, yawN, uMin, vMin, uMax, vMax, presAlpha);
                else       EmitOnePlane(list, anchor, up, halfW, yawN, uMin, vMin, uMax, vMax, overlayList);
            }

            return true;
        }

        private static void EmitOnePlane(
            List<VertexPositionTexture> list,
            Vector3 anchor, Vector3 up, float halfW, float yaw,
            float uMin, float vMin, float uMax, float vMax,
            List<VertexPositionTexture> overlayList = null)
        {
            Vector3 right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw)) * halfW;
            Vector3 bl = anchor - right;
            Vector3 br = anchor + right;
            Vector3 tl = bl + up;
            Vector3 tr = br + up;
            list.Add(new VertexPositionTexture(tl, new Vector2(uMin, vMin)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(br, new Vector2(uMax, vMax)));

            if (overlayList != null)
                AppendOverlayQuad(overlayList, tl, tr, bl, br, uMin, vMin, uMax, vMax);
        }

        // Faded variant: same quad but with vertex color (white * alpha) so
        // the fade pass can blend the leaf out smoothly. The overlay list is
        // skipped — wet/snow shouldn't paint a half-faded canopy.
        private static void EmitOnePlaneFaded(
            List<VertexPositionColorTexture> list,
            Vector3 anchor, Vector3 up, float halfW, float yaw,
            float uMin, float vMin, float uMax, float vMax,
            float alpha)
        {
            Vector3 right = new Vector3(MathF.Cos(yaw), 0f, MathF.Sin(yaw)) * halfW;
            Vector3 bl = anchor - right;
            Vector3 br = anchor + right;
            Vector3 tl = bl + up;
            Vector3 tr = br + up;
            byte a = (byte)Math.Clamp((int)(alpha * 255f), 0, 255);
            // Vertex RGB stays white so the leaf colour isn't darkened — only
            // the alpha modulates. NonPremultiplied blend state will then do
            // src.a * texture.a fade against the destination.
            var c = new Color((byte)255, (byte)255, (byte)255, a);
            list.Add(new VertexPositionColorTexture(tl, c, new Vector2(uMin, vMin)));
            list.Add(new VertexPositionColorTexture(tr, c, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionColorTexture(bl, c, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionColorTexture(bl, c, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionColorTexture(tr, c, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionColorTexture(br, c, new Vector2(uMax, vMax)));
        }

        // Legacy EmitCrossedTree path for generic StaticKind.Tree (graphic not
        // present in tree.txt, so we don't know if it's trunk or leaves). One
        // fixed-yaw plane only — no extra duplicated planes (would risk the
        // radiating-trunk artefact for unknown statics).
        private static bool EmitCrossedTreeLegacy(Static s)
        {
            return EmitFixedTrunkPlane(s);
        }

        // Flat XZ quad for sticks, rocks, vegetation tufts, water-edge tiles —
        // anything that should lay on the ground rather than face the camera.
        // Quad size = art pixel bounds, centered on the tile, lifted slightly to
        // avoid Z-fighting with the heightmap.
        private static bool EmitGroundQuad(Static s)
        {
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(s.Graphic);
            if (artInfo.Texture == null) return false;
            if (artInfo.UV.Width <= 0 || artInfo.UV.Height <= 0) return false;

            float cx = s.X * TILE + TILE * 0.5f;
            float cz = s.Y * TILE + TILE * 0.5f;
            float wy = s.Z * Z_SCALE + GroundDecalLift;

            // UO art convention: a full-tile sprite is drawn iso-projected on a
            // 44px-wide texture, with the diamond corners at the texture edge
            // midpoints (texture corners are transparent). World tiles are
            // TILE=22 wide, and a TILE×TILE axis-aligned XZ quad iso-projects to
            // a 44×44 screen-diamond — exactly the texture content's footprint.
            // Adjacent tiles tile edge-to-edge with no overlap (no Z-fight).
            // Smaller arts (rocks/sticks/decals) keep proportional size.
            const float ART_TO_WORLD = TILE / 44f; // 0.5
            float wWorld = artInfo.UV.Width  * ART_TO_WORLD;
            float hWorld = artInfo.UV.Height * ART_TO_WORLD;

            float halfW = wWorld * 0.5f;
            float halfH = hWorld * 0.5f;

            // World axes: +X east, +Z south (matches LandMesh3D convention).
            Vector3 bl = new Vector3(cx - halfW, wy, cz + halfH);
            Vector3 br = new Vector3(cx + halfW, wy, cz + halfH);
            Vector3 tl = new Vector3(cx - halfW, wy, cz - halfH);
            Vector3 tr = new Vector3(cx + halfW, wy, cz - halfH);

            // Diamond UV mapping: the texture's content is a diamond with tips
            // at the texture's edge midpoints. After iso projection, our world
            // quad becomes a screen-diamond whose tips are at the world quad's
            // CORNERS. So we map the texture's diamond-tip pixels to the world
            // quad's corners (NOT to the texture's corners, which are
            // transparent — using square UVs would render only a tiny inner
            // square, leaving gaps between adjacent tiles).
            var tex = artInfo.Texture;
            float invW = 1f / tex.Width;
            float invH = 1f / tex.Height;
            float uMid = (artInfo.UV.X + artInfo.UV.Width  * 0.5f) * invW;
            float vMid = (artInfo.UV.Y + artInfo.UV.Height * 0.5f) * invH;
            float uLeft  = (artInfo.UV.X + 0.5f) * invW;
            float uRight = (artInfo.UV.X + artInfo.UV.Width  - 0.5f) * invW;
            float vTop    = (artInfo.UV.Y + 0.5f) * invH;
            float vBottom = (artInfo.UV.Y + artInfo.UV.Height - 0.5f) * invH;
            Vector2 uvN = new Vector2(uMid,   vTop);
            Vector2 uvE = new Vector2(uRight, vMid);
            Vector2 uvS = new Vector2(uMid,   vBottom);
            Vector2 uvW = new Vector2(uLeft,  vMid);

            // World corner → screen-diamond tip mapping (iso projection):
            //   tl (min X, min Z) → screen NORTH tip → diamond N
            //   tr (max X, min Z) → screen EAST  tip → diamond E
            //   br (max X, max Z) → screen SOUTH tip → diamond S
            //   bl (min X, max Z) → screen WEST  tip → diamond W
            // Match the underlying land mesh's Lambert lighting at this
            // tile so the decal blends in. LandMesh3D applies a per-corner
            // dot(normal, sunDir) * 0.5 + 0.5, mapped to vertex colour.
            // We use the SAME formula here, sampled at the underlying Land
            // tile (a single tile-average value applied to the whole decal
            // — fine for one-tile decals; large multi-tile decals could be
            // upgraded to per-corner lookups if it ever matters visually).
            // Without this, decals render at full brightness (255) while
            // the land mesh sits at ~217, producing a visible step where a
            // static-flat-grass meets the land tile mesh.
            Color decalColor = ComputeGroundDecalLambert(s);

            // Wet statics (shoreline, lake/sea tiles) go to the water batch so
            // BloodMoon weather can tint them blood-red independently.
            var list = s.ItemData.IsWet ? GetWaterDecalBatch(tex) : GetGroundDecalBatch(tex);
            // Tri 1: tl(N), tr(E), bl(W)
            list.Add(new VertexPositionColorTexture(tl, decalColor, uvN));
            list.Add(new VertexPositionColorTexture(tr, decalColor, uvE));
            list.Add(new VertexPositionColorTexture(bl, decalColor, uvW));
            // Tri 2: bl(W), tr(E), br(S)
            list.Add(new VertexPositionColorTexture(bl, decalColor, uvW));
            list.Add(new VertexPositionColorTexture(tr, decalColor, uvE));
            list.Add(new VertexPositionColorTexture(br, decalColor, uvS));
            return true;
        }

        // Look up the Land tile under this static and compute its Lambert
        // brightness using LandMesh3D's lighting formula (the same one the
        // 2D IsometricWorld.fx LAND mode uses — dot(N, L) * 0.5 + 0.5,
        // L = LightingState.CurrentLightDir()).  We average the four corner
        // normals to get a single tile-representative brightness; flat tiles
        // land at exactly cos(45°)/2 + 0.5 ≈ 0.854, matching the land mesh.
        // Falls back to flat-tile brightness when the Land isn't stretched
        // (no normals computed) or can't be located.
        private static Color ComputeGroundDecalLambert(Static s)
        {
            Vector3 sunDir = LightingState.CurrentLightDir();
            float sunLen2 = sunDir.LengthSquared();
            if (sunLen2 > 1e-6f) sunDir /= MathF.Sqrt(sunLen2);
            else                 sunDir = Vector3.UnitY;

            Vector3 n = Vector3.UnitY;

            var land = FindLandAt(s);
            if (land != null && land.IsStretched)
            {
                // Average the 4 per-corner normals (ClassicUO basis: X=east,
                // Y=south, Z=up).  Then swap Y↔Z to convert to our 3D basis
                // (X=east, Y=up, Z=south) — same remap LandMesh3D uses.
                Vector3 cuAvg = (land.NormalTop + land.NormalRight
                               + land.NormalLeft + land.NormalBottom) * 0.25f;
                Vector3 mapped = new Vector3(cuAvg.X, cuAvg.Z, cuAvg.Y);
                float mLen2 = mapped.LengthSquared();
                if (mLen2 > 1e-6f) n = mapped / MathF.Sqrt(mLen2);
            }

            float lambert = MathF.Max(Vector3.Dot(n, sunDir), 0f);
            float lit = MathHelper.Clamp(lambert * 0.5f + 0.5f, 0f, 1.15f);
            byte cb = (byte)MathHelper.Clamp((int)(lit * 255f), 0, 255);
            return new Color(cb, cb, cb, (byte)255);
        }

        private static ClassicUO.Game.GameObjects.Land FindLandAt(Static s)
        {
            var map = s.World?.Map;
            if (map == null) return null;
            var chunk = map.GetChunk(s.X, s.Y, false);
            if (chunk == null) return null;
            for (var obj = chunk.GetHeadObject(s.X & 7, s.Y & 7); obj != null; obj = obj.TNext)
            {
                if (obj is ClassicUO.Game.GameObjects.Land l) return l;
            }
            return null;
        }

        private static List<VertexPositionColorTexture> GetGroundDecalBatch(Texture2D tex)
        {
            if (!_groundDecalBatches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionColorTexture>(128);
                _groundDecalBatches[tex] = list;
                _groundDecalKeys.Add(tex);
            }
            else if (list.Count == 0 && !_groundDecalKeys.Contains(tex))
            {
                _groundDecalKeys.Add(tex);
            }
            return list;
        }

        private static List<VertexPositionColorTexture> GetWaterDecalBatch(Texture2D tex)
        {
            if (!_waterDecalBatches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionColorTexture>(64);
                _waterDecalBatches[tex] = list;
                _waterDecalKeys.Add(tex);
            }
            else if (list.Count == 0 && !_waterDecalKeys.Contains(tex))
            {
                _waterDecalKeys.Add(tex);
            }
            return list;
        }

        private static List<VertexPositionTexture> GetWaterBatch(Texture2D tex)
        {
            if (!_waterBatches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionTexture>(128);
                _waterBatches[tex] = list;
                _waterBatchKeys.Add(tex);
            }
            else if (list.Count == 0 && !_waterBatchKeys.Contains(tex))
            {
                _waterBatchKeys.Add(tex);
            }
            return list;
        }

        // Plain camera-facing billboard for an arbitrary world Item. No tree
        // jitter, no z-bias, no kind classification — items in the allow-list
        // are typically singletons (one Barracks, one FactionStone) so the
        // tree-overlap heuristics aren't needed.
        private static bool EmitItemBillboard(ClassicUO.Game.GameObjects.Item item)
        {
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(item.Graphic);
            if (artInfo.Texture == null) return false;
            if (artInfo.UV.Width <= 0 || artInfo.UV.Height <= 0) return false;

            float cx = item.X * TILE + TILE * 0.5f;
            float cz = item.Y * TILE + TILE * 0.5f;
            float wy = item.Z * Z_SCALE;

            float wPx = artInfo.UV.Width;
            float hPx = artInfo.UV.Height;

            Vector3 right = _billboardRight * (wPx * 0.5f);
            Vector3 up = BILLBOARD_UP * hPx;
            Vector3 anchor = new Vector3(cx, wy, cz);

            Vector3 bl = anchor - right;
            Vector3 br = anchor + right;
            Vector3 tl = bl + up;
            Vector3 tr = br + up;

            var tex = artInfo.Texture;
            float invW = 1f / tex.Width;
            float invH = 1f / tex.Height;
            float uMin = (artInfo.UV.X + 0.5f) * invW;
            float vMin = (artInfo.UV.Y + 0.5f) * invH;
            float uMax = uMin + (artInfo.UV.Width  - 1f) * invW;
            float vMax = vMin + (artInfo.UV.Height - 1f) * invH;

            var list = GetBatch(tex);
            list.Add(new VertexPositionTexture(tl, new Vector2(uMin, vMin)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(br, new Vector2(uMax, vMax)));
            return true;
        }

        private static List<VertexPositionTexture> GetBatch(Texture2D tex)
        {
            if (!_batches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionTexture>(256);
                _batches[tex] = list;
                _batchKeys.Add(tex);
            }
            else if (list.Count == 0 && !_batchKeys.Contains(tex))
            {
                _batchKeys.Add(tex);
            }
            return list;
        }

        private static List<VertexPositionColorTexture> GetLeafFadeBatch(Texture2D tex)
        {
            if (!_leafFadeBatches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionColorTexture>(256);
                _leafFadeBatches[tex] = list;
                _leafFadeKeys.Add(tex);
            }
            else if (list.Count == 0 && !_leafFadeKeys.Contains(tex))
            {
                _leafFadeKeys.Add(tex);
            }
            return list;
        }

        // Same shape as GetBatch but for the leaf overlay batches. Pulled into
        // a helper so EmitCrossedLeaves / EmitCrossedTreeLegacy / EmitFixedTrunkPlane
        // can fan out to it without duplicating the dictionary bookkeeping.
        private static List<VertexPositionTexture> GetLeafOverlayBatch(Texture2D tex)
        {
            if (!_leafOverlayBatches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionTexture>(256);
                _leafOverlayBatches[tex] = list;
                _leafOverlayKeys.Add(tex);
            }
            else if (list.Count == 0 && !_leafOverlayKeys.Contains(tex))
            {
                _leafOverlayKeys.Add(tex);
            }
            return list;
        }

        private static List<VertexPositionTexture> GetTrunkOverlayBatch(Texture2D tex)
        {
            if (!_trunkOverlayBatches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionTexture>(256);
                _trunkOverlayBatches[tex] = list;
                _trunkOverlayKeys.Add(tex);
            }
            else if (list.Count == 0 && !_trunkOverlayKeys.Contains(tex))
            {
                _trunkOverlayKeys.Add(tex);
            }
            return list;
        }

        // Append a quad's 6 vertices to an overlay batch (mirrors the same
        // tri-strip layout the base emitters use).
        private static void AppendOverlayQuad(
            List<VertexPositionTexture> list,
            Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br,
            float uMin, float vMin, float uMax, float vMax)
        {
            list.Add(new VertexPositionTexture(tl, new Vector2(uMin, vMin)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(br, new Vector2(uMax, vMax)));
        }

        // Returns true if there's any work to do for the foliage overlay this frame.
        // Fall season counts too — leaves still need to be re-tinted even if no
        // wet/snow is active, so the leaf overlay batch must be populated.
        private static bool ShouldDrawFoliageOverlay()
        {
            bool wetOrSnow = World3DRenderer.GroundEffectMode != World3DRenderer.GroundEffect.None
                          && World3DRenderer.GroundEffectIntensity > 0.001f;
            bool fall = World3DRenderer.FoliageSeason != World3DRenderer.FoliageSeasonMode.None
                     && World3DRenderer.FoliageSeasonIntensity > 0.001f;
            return (wetOrSnow || fall) && (ApplyOverlayToFoliage || ApplyOverlayToTrunks);
        }

        // Iterate a per-texture batch set and draw each batch with the
        // currently-selected technique on `fx`. BaseAlphaTex is rebound per
        // batch so the foliage shader can sample the right cutout.
        private static void DrawOverlayBatchSet(
            GraphicsDevice gd, GroundOverlayEffect fx,
            List<Texture2D> keys,
            Dictionary<Texture2D, List<VertexPositionTexture>> batches)
        {
            for (int ki = 0; ki < keys.Count; ki++)
            {
                var tex = keys[ki];
                var verts = batches[tex];
                if (verts.Count == 0) continue;
                fx.BaseAlphaTex = tex;
                int triCount = verts.Count / 3;
                var arr = verts.ToArray();
                foreach (var pass in fx.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, triCount);
                }
            }
        }

        // Draw the wet/snow overlay over leaf and (for wet) trunk geometry,
        // using the GroundOverlay shader's foliage techniques. Called from Draw()
        // after the base alpha-test pass writes the leaf depth.
        private static void DrawFoliageOverlay(GraphicsDevice gd, Matrix view, Matrix proj)
        {
            var fx = World3DRenderer.GroundOverlayEffect;
            if (fx == null) return;
            if (!ShouldDrawFoliageOverlay()) return;

            float intensity = MathHelper.Clamp(World3DRenderer.GroundEffectIntensity, 0f, 1f);
            float fallI = MathHelper.Clamp(World3DRenderer.FoliageSeasonIntensity, 0f, 1f);
            var mode = World3DRenderer.GroundEffectMode;
            var season = World3DRenderer.FoliageSeason;
            bool wetSnowActive = mode != World3DRenderer.GroundEffect.None && intensity > 0.001f;
            bool fallActive    = season == World3DRenderer.FoliageSeasonMode.Fall && fallI > 0.001f;

            var prevBlend   = gd.BlendState;
            var prevDepth   = gd.DepthStencilState;
            var prevRaster  = gd.RasterizerState;
            var prevSampler0 = gd.SamplerStates[0];
            var prevSampler1 = gd.SamplerStates[1];
            var prevSampler2 = gd.SamplerStates[2];
            var prevSampler3 = gd.SamplerStates[3];

            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.DepthRead;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.SamplerStates[0] = SamplerState.LinearWrap;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            gd.SamplerStates[2] = SamplerState.LinearWrap;
            gd.SamplerStates[3] = SamplerState.PointClamp; // base alpha — pixel-art

            fx.World = Matrix.Identity;
            fx.View = view;
            fx.Projection = proj;
            fx.Time = (float)(Environment.TickCount64 * 0.001);
            fx.ApplyTunables();

            // Pass 1 — Fall foliage (leaves only). Drawn FIRST so any subsequent
            // wet/snow pass blends on top of the autumn-colored canopy (matches
            // real life: snow lands on red leaves, not the other way around).
            if (fallActive && ApplyOverlayToFoliage)
            {
                fx.Intensity = fallI;
                fx.UseFallFoliage();
                DrawOverlayBatchSet(gd, fx, _leafOverlayKeys, _leafOverlayBatches);
            }

            // Pass 2 — wet/snow on leaves.
            if (wetSnowActive && ApplyOverlayToFoliage)
            {
                fx.Intensity = intensity;
                if (mode == World3DRenderer.GroundEffect.Wet) fx.UseWetFoliage();
                else                                          fx.UseSnowFoliage();
                DrawOverlayBatchSet(gd, fx, _leafOverlayKeys, _leafOverlayBatches);
            }

            // Pass 3 — wet/snow on trunks. Wet darkens the bark; snow paints
            // a snow-line on top of the trunk that matches the canopy's snow.
            if (wetSnowActive && ApplyOverlayToTrunks)
            {
                if (mode == World3DRenderer.GroundEffect.Wet)
                {
                    fx.Intensity = intensity;
                    fx.UseWetFoliage();
                    DrawOverlayBatchSet(gd, fx, _trunkOverlayKeys, _trunkOverlayBatches);
                }
                else if (mode == World3DRenderer.GroundEffect.Snow)
                {
                    fx.Intensity = intensity;
                    fx.UseSnowFoliage();
                    DrawOverlayBatchSet(gd, fx, _trunkOverlayKeys, _trunkOverlayBatches);
                }
            }

            gd.BlendState = prevBlend;
            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.SamplerStates[0] = prevSampler0;
            gd.SamplerStates[1] = prevSampler1;
            gd.SamplerStates[2] = prevSampler2;
            gd.SamplerStates[3] = prevSampler3;
        }

        private static void EnsureResources(GraphicsDevice gd)
        {
            if (_effect == null)
            {
                _effect = new AlphaTestEffect(gd);
            }
        }
    }
}
