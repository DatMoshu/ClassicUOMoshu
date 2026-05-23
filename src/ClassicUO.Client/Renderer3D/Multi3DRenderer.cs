// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 4: render multis (houses/boats/structures) as
// axis-aligned textured quads in 3D space.
//
// For each Multi GameObject in the visible chunks:
//   - Floors  -> horizontal quad on the XZ plane at world Y = uoZ * 4.
//   - Walls   -> vertical quad along the tile edge implied by facing
//                (South/East/North/West). Height = TileData.Height * 4 px.
//   - Roofs   -> flat horizontal quad at the top of the tile (no slope yet).
//   - Corners -> skipped in v1 (filled visually by adjacent walls).
//   - Unknown -> magenta debug quad so missing JSON entries are obvious.
//
// Quads are batched per Texture2D; one DrawUserPrimitives call per texture.
// Depth test/write is enabled locally for this pass so walls occlude each other.
//
// World mapping (matches LandMesh3D / Player3DRenderer):
//   world.X = uoTileX * 22
//   world.Y = uoZ     *  4
//   world.Z = uoTileY * 22

using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class Multi3DRenderer
    {
        // Default OFF — opt-in via debug gump. With BillboardAllStatics=false and
        // Multi3D enabled, every Static gets treated as wall/floor geometry, which
        // produces a "forest of pillars" inside towns. The "Pure 3D" macro button
        // in the debug gump turns both Multi3D + Static3D + BillboardAllStatics on.
        public static bool Enabled = false;
        public static bool VerboseLog = true;
        public static bool ShowFallbackForUnknownWalls = true;
        // When true, walls whose graphic is in WallMeshRegistry render as 3D GLB
        // meshes instead of flat sprite quads. Default OFF — opt-in via Debug3D
        // gump. Walls outside the registry keep falling through to sprite quads.
        public static bool Use3DWallMeshes = true;
        // When true, roof tiles whose graphic ID is registered in
        // RoofMeshRegistry render as pitched 3D meshes with a per-family atlas
        // instead of a flat horizontal quad. Falls back to EmitFloor when the
        // tag is missing or the manifest has no entry, so this is safe to leave
        // on even before the Blender pipeline ships any meshes.
        public static bool Use3DRoofMeshes = true;
        // Diagnostics
        public static int LastRoofMeshCount;
        // When true, attempt to "square up" iso-drawn UO art on axis-aligned
        // quads by trimming the 4 transparent corners of the source sprite to
        // the bounding box of opaque pixels (heuristic; actual deskew would
        // need per-graphic art metadata). Off by default — current behavior is
        // to map the full source rect 1:1 to the quad.
        public static bool DeskewMultiTexture = false;
        // PROTOTYPE DEBUG: force a fixed-size quad at the player so we can confirm
        // the pipeline draws *anything* in 3D space. Toggle off once multis appear.
        public static bool ForceTestQuadAtPlayer = false;
        public static float TestQuadPlayerUoX = 0f;
        public static float TestQuadPlayerUoY = 0f;
        public static float TestQuadPlayerUoZ = 0f;

        // Diagnostics
        public static int LastMultiSeen;
        public static int LastStaticSeen;
        public static int LastQuadCount;
        public static int LastTextureCount;
        public static int LastUnknownCount;
        public static int LastSkippedCount;
        public static int LastWallMeshCount;
        private static int _frameCount;
        // Set at the top of Draw() so AddTileQuad can lazily load wall GLBs
        // through WallMeshRegistry without us having to thread gd through every
        // helper. Cleared at end of Draw().
        private static GraphicsDevice _frameGd;

        private const float TILE = LandMesh3D.TILE;     // 22f
        private const float Z_SCALE = LandMesh3D.Z_SCALE; // 4f
        private const float UNKNOWN_DEFAULT_HEIGHT = 20f; // when TileData.Height == 0

        private static BasicEffect _effect;
        private static Texture2D _fallbackTexture;
        private static RasterizerState _wireframeState;

        private static RasterizerState GetWireframeState()
        {
            return _wireframeState ??= new RasterizerState
            {
                FillMode = FillMode.WireFrame,
                CullMode = CullMode.None
            };
        }

        // Re-used scratch storage for batching: one list of verts per texture.
        private static readonly Dictionary<Texture2D, List<VertexPositionTexture>> _batches = new();
        private static readonly List<Texture2D> _batchKeys = new();

        // Classic UO transparent-roof: when the player walks under a roof or
        // upper floor, the 2D pipeline's _maxZ is set so the 2D path culls
        // tiles at obj.Z >= _maxZ. We mirror that signal in the 3D draw so
        // the camera can see the player when standing inside a building.
        public static bool HideAbovePlayerZ = true;

        // Snow accumulation overlay on roof top faces. When the active weather
        // is Snow / Blizzard, each rendered roof tile gets an extra white-tinted
        // quad emitted just above its top face, using a cached snow-white clone
        // of the source atlas (RGB forced to white, alpha preserved) so the
        // snow exactly matches the roof's painted shape. Toggle in WeatherAdminGump.
        public static bool RoofSnowOverlay = true;
        // Cache of atlas → snow-tinted atlas. One Texture2D per source atlas;
        // weather-bound for the lifetime of the renderer (prototype scope).
        private static readonly Dictionary<Texture2D, Texture2D> _snowTexCache = new();
        private static int _frameMaxZ = sbyte.MaxValue;
        // Public read so Static3DRenderer can pick up the same per-frame value
        // without re-querying GameScene.
        public static int LastFrameMaxZ => _frameMaxZ;

        public static void Draw(
            GraphicsDevice gd,
            IList<Chunk> visibleChunks,
            Matrix view,
            Matrix projection
        )
        {
            if (!Enabled || gd == null) return;
            if (visibleChunks == null || visibleChunks.Count == 0) return;

            EnsureResources(gd);

            // Reset batches (keep allocated lists for reuse).
            foreach (var kv in _batches) kv.Value.Clear();
            _batchKeys.Clear();

            _frameGd = gd;
            if (Use3DWallMeshes) WallMeshDrawer.Begin();
            if (Use3DRoofMeshes) RoofMeshDrawer.Begin();
            // Iris 2 drawer is shared with Static3DRenderer. Begin is idempotent
            // within the frame, so calling here for the override path is safe even
            // when Static3DRenderer also calls it.
            if (Static3DRenderer.Use3DIris2Statics || Static3DRenderer.Iris2OverrideAll)
                Iris2StaticDrawer.Begin();

            // Capture the GameScene's _maxZ once per frame. UpdateMaxDrawZ
            // already runs every frame in the 2D draw path, so reading it
            // here is free.
            if (HideAbovePlayerZ)
            {
                var scene = ClassicUO.Client.Game.GetScene<ClassicUO.Game.Scenes.GameScene>();
                _frameMaxZ = scene?.MaxZRender ?? sbyte.MaxValue;
            }
            else
            {
                _frameMaxZ = sbyte.MaxValue;
            }

            int unknown = 0, skipped = 0, multiSeen = 0, staticSeen = 0;

            for (int ci = 0; ci < visibleChunks.Count; ci++)
            {
                var chunk = visibleChunks[ci];
                if (chunk == null) continue;

                for (int ty = 0; ty < 8; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                    {
                        if (obj.IsDestroyed) continue;

                        // Multi components (player-deeded houses) and Static items
                        // (pre-built world houses, terrain decoration) both feed the
                        // same orientation pipeline. Static decoration that doesn't
                        // resolve to a known kind is filtered inside AddTileQuad.
                        if (obj is Multi m)
                        {
                            multiSeen++;
                            if (!m.AllowedToDraw) continue;
                            // Player-placed houses set CHMOF flags (FLOOR / FIXTURE /
                            // VALIDATED_PLACE / DONT_REMOVE etc.) on every component,
                            // so a "skip if State != 0" filter would drop the entire
                            // building. Mirror MultiView.cs:43-59 (the legacy 2D path)
                            // and only skip components explicitly flagged as
                            // non-renderable or in-progress preview placements.
                            if ((m.State & (CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_IGNORE_IN_RENDER
                                          | CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_PREVIEW)) != 0)
                                continue;
                            // 100% Iris preview: replace the multi-quad path with the
                            // matching Iris 2 GLB whenever the registry has a hit.
                            if (Static3DRenderer.Iris2OverrideAll &&
                                Iris2StaticRegistry.TryGet(m.Graphic, out var i2m))
                            {
                                var mdl = Iris2StaticRegistry.EnsureModel(gd, m.Graphic, out _);
                                if (mdl != null)
                                {
                                    Iris2StaticDrawer.Submit(mdl, m.Graphic, m.X, m.Y, m.Z, i2m.TileHeight);
                                    continue;
                                }
                            }
                            AddTileQuad(m, ref m.ItemData, ref unknown, ref skipped);
                        }
                        else if (obj is Static s)
                        {
                            staticSeen++;
                            if (!s.AllowedToDraw) continue;
                            // 100% Iris preview also wins over walls. Falls through to
                            // the wall path if the registry has nothing for this graphic.
                            if (Static3DRenderer.Iris2OverrideAll &&
                                Iris2StaticRegistry.TryGet(s.Graphic, out var i2s))
                            {
                                var mdl = Iris2StaticRegistry.EnsureModel(gd, s.Graphic, out _);
                                if (mdl != null)
                                {
                                    Iris2StaticDrawer.Submit(mdl, s.Graphic, s.X, s.Y, s.Z, i2s.TileHeight);
                                    continue;
                                }
                            }
                            // Wall-mesh override comes first so castle/town walls
                            // (which are Static, not Multi) participate in the
                            // 3D-mesh swap even when BillboardAllStatics is on.
                            if (Use3DWallMeshes &&
                                WallMeshRegistry.TryGet(s.Graphic, out _))
                            {
                                AddTileQuad(s, ref s.ItemData, ref unknown, ref skipped);
                                continue;
                            }
                            // Statics are otherwise handled by Static3DRenderer
                            // (billboards) when that path is enabled, to avoid
                            // them being mis-classified as wall/floor geometry.
                            if (Static3DRenderer.BillboardAllStatics) continue;
                            AddTileQuad(s, ref s.ItemData, ref unknown, ref skipped);
                        }
                    }
                }
            }
            LastMultiSeen = multiSeen;
            LastStaticSeen = staticSeen;

            // Debug pipeline-sanity quad at the player position. Shows up regardless
            // of multi data so we can tell if the issue is rendering vs data.
            if (ForceTestQuadAtPlayer)
            {
                EnsureResources(gd);
                float wx = TestQuadPlayerUoX * TILE;
                float wz = TestQuadPlayerUoY * TILE;
                float wy = TestQuadPlayerUoZ * Z_SCALE;
                // Tall, narrow billboard-ish quad facing south, 44px wide x 80px tall.
                EmitWallSouth(_fallbackTexture, wx, wy, wz, 80f, 0, 0, 1, 1);
            }

            // Render state
            var prevDepth = gd.DepthStencilState;
            var prevRaster = gd.RasterizerState;
            var prevBlend = gd.BlendState;
            var prevSampler = gd.SamplerStates[0];

            gd.DepthStencilState = DepthStencilState.Default; // depth on for this pass
            gd.RasterizerState = World3DRenderer.Wireframe
                ? GetWireframeState()
                : RasterizerState.CullNone;                    // both sides; UO art has only one face
            gd.BlendState = BlendState.AlphaBlend;             // sprite alpha
            gd.SamplerStates[0] = SamplerState.PointClamp;     // crisp UO pixels

            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            _effect.TextureEnabled = true;
            _effect.VertexColorEnabled = false;
            _effect.LightingEnabled = false;

            int totalQuads = 0;
            for (int ki = 0; ki < _batchKeys.Count; ki++)
            {
                var tex = _batchKeys[ki];
                var verts = _batches[tex];
                if (verts.Count == 0) continue;

                _effect.Texture = tex;

                int triCount = verts.Count / 3;
                var arr = verts.ToArray(); // proto: simple, fine for now

                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, triCount);
                }

                totalQuads += verts.Count / 6;
            }

            gd.DepthStencilState = prevDepth;
            gd.RasterizerState = prevRaster;
            gd.BlendState = prevBlend;
            gd.SamplerStates[0] = prevSampler;

            LastQuadCount = totalQuads;
            LastTextureCount = _batchKeys.Count;
            LastUnknownCount = unknown;
            LastSkippedCount = skipped;

            // Flush any 3D wall-mesh instances accumulated during this frame.
            // This runs after the sprite-quad batches so meshes share the same
            // depth buffer state we just left set up. WallMeshDrawer pushes/pops
            // its own renderstates internally.
            if (Use3DWallMeshes)
            {
                WallMeshDrawer.Draw(gd, view, projection);
                LastWallMeshCount = WallMeshDrawer.LastInstanceCount;
            }
            else
            {
                LastWallMeshCount = 0;
            }
            if (Use3DRoofMeshes)
            {
                RoofMeshDrawer.Draw(gd, view, projection);
                LastRoofMeshCount = RoofMeshDrawer.LastInstanceCount;
            }
            else
            {
                LastRoofMeshCount = 0;
            }
            // Iris 2 fallback flush. Normally Static3DRenderer.Draw flushes the
            // shared drawer at end of frame; if it's disabled we have to do it
            // here or instances accumulate forever and never paint.
            if ((Static3DRenderer.Use3DIris2Statics || Static3DRenderer.Iris2OverrideAll) &&
                !Static3DRenderer.Enabled)
            {
                Iris2StaticDrawer.Draw(gd, view, projection);
            }
            _frameGd = null;

            _frameCount++;
            if (VerboseLog && (_frameCount <= 5 || _frameCount % 300 == 1))
            {
                Console.WriteLine(
                    $"[3DCUO] Multi3D frame={_frameCount} chunks={visibleChunks.Count} multisSeen={LastMultiSeen} staticsSeen={LastStaticSeen} quads={LastQuadCount} tex={LastTextureCount} wallMeshes={LastWallMeshCount} roofMeshes={LastRoofMeshCount} unknown={LastUnknownCount} skipped={LastSkippedCount} testQuad={ForceTestQuadAtPlayer}"
                );
            }
        }

        private static bool AddTileQuad(GameObject obj, ref StaticTiles data, ref int unknown, ref int skipped)
        {
            ref readonly var artInfo = ref Client.Game.UO.Arts.GetArt(obj.Graphic);
            if (artInfo.Texture == null)
            {
                skipped++;
                return false;
            }

            // Classic transparent-roof: skip components at or above the
            // player's _maxZ so the camera can see the player when they walk
            // inside a building. Same signal the 2D pipeline already uses.
            if (obj.Z >= _frameMaxZ)
            {
                skipped++;
                return false;
            }

            var kind = MultiOrientationTable.Resolve(obj.Graphic, ref data);

            // Per-instance refinement: corners can't be classified by graphic
            // ID alone (the same corner piece is the NW or SE corner of
            // different buildings depending on placement). Probe cardinal
            // neighbours and narrow `Corner` to CornerNW/NE/SW/SE.
            kind = WallNeighborClassifier.Refine(
                ClassicUO.Client.Game.UO.World, obj.Graphic, obj.X, obj.Y, obj.Z, kind);

            // Tile world position
            float wx = obj.X * TILE;
            float wz = obj.Y * TILE;
            float wy = obj.Z * Z_SCALE;

            // Wall height in world Y units. Height==0 (e.g. roofs without thickness)
            // gets a sensible default so the quad has visible extent.
            float h = data.Height > 0 ? data.Height * Z_SCALE : UNKNOWN_DEFAULT_HEIGHT;

            // UV rect (normalized) in art atlas
            var tex = artInfo.Texture;
            float uvX = artInfo.UV.X / (float)tex.Width;
            float uvY = artInfo.UV.Y / (float)tex.Height;
            float uvW = artInfo.UV.Width / (float)tex.Width;
            float uvH = artInfo.UV.Height / (float)tex.Height;

            // Apply per-graphic override (UV transform + height tweak + visibility).
            if (!MultiUvOverrides.ShouldDraw(obj.Graphic)) { skipped++; return false; }
            MultiUvOverrides.Apply(obj.Graphic, ref uvX, ref uvY, ref uvW, ref uvH, ref h);

            // 3D wall-mesh substitution. If the graphic has a manifest entry and
            // the model loads, hand off to WallMeshDrawer with the post-override
            // height and skip the sprite path. Walls outside the registry fall
            // through to the existing quad path.
            if (Use3DWallMeshes && _frameGd != null &&
                WallMeshRegistry.TryGet(obj.Graphic, out _))
            {
                var model = WallMeshRegistry.EnsureModel(_frameGd, obj.Graphic, out _);
                if (model != null)
                {
                    WallMeshDrawer.Submit(model, obj.Graphic, kind, wx, wy, wz, h);
                    return true;
                }
                // load failed → fall through to sprite quad fallback
            }

            switch (kind)
            {
                case TileOrientation.Floor:
                    EmitFloor(tex, wx, wy + 0.05f * Z_SCALE, wz, uvX, uvY, uvW, uvH);
                    return true;

                case TileOrientation.Roof:
                {
                    // Try the 3D roof-mesh path first: tagged graphic + manifest
                    // entry + GLB loads → pitched mesh with family atlas. Snow
                    // overlay re-uses the same mesh with the snow-tinted atlas.
                    // Falls back to the legacy flat-quad EmitFloor when any of
                    // those steps fail, so unsupported roof families keep working.
                    if (Use3DRoofMeshes && _frameGd != null &&
                        RoofMeshRegistry.TryResolve(obj.Graphic, out var roofTag, out var roofEntry) &&
                        !string.IsNullOrEmpty(roofEntry.MeshFile))
                    {
                        var model = RoofMeshRegistry.EnsureModel(_frameGd, roofEntry.MeshFile, out _);
                        var atlas = RoofMeshRegistry.EnsureAtlas(_frameGd, roofEntry.AtlasFile);
                        if (model != null)
                        {
                            RoofMeshDrawer.Submit(
                                model, atlas, obj.Graphic, roofTag.Archetype,
                                wx, wy, wz, wy + h);
                            if (RoofSnowOverlay && IsSnowWeatherActive() && atlas != null)
                            {
                                var snowAtlas = GetSnowTinted(_frameGd, atlas);
                                if (snowAtlas != null)
                                {
                                    var last = RoofMeshDrawer.LastInstances[RoofMeshDrawer.LastInstances.Count - 1];
                                    RoofMeshDrawer.SubmitSnowOverlay(in last, snowAtlas, 0.25f);
                                }
                            }
                            return true;
                        }
                        // mesh load failed → fall through to flat-quad fallback
                    }

                    // Legacy fallback: flat horizontal quad at top of tile.
                    EmitFloor(tex, wx, wy + h, wz, uvX, uvY, uvW, uvH);
                    if (RoofSnowOverlay && _frameGd != null && IsSnowWeatherActive())
                    {
                        var snowTex = GetSnowTinted(_frameGd, tex);
                        if (snowTex != null)
                            EmitFloor(snowTex, wx, wy + h + 0.25f, wz, uvX, uvY, uvW, uvH);
                    }
                    return true;
                }

                case TileOrientation.WallSouth:
                    EmitWallSouth(tex, wx, wy, wz, h, uvX, uvY, uvW, uvH);
                    return true;
                case TileOrientation.WallEast:
                    EmitWallEast(tex, wx, wy, wz, h, uvX, uvY, uvW, uvH);
                    return true;
                case TileOrientation.WallNorth:
                    EmitWallNorth(tex, wx, wy, wz, h, uvX, uvY, uvW, uvH);
                    return true;
                case TileOrientation.WallWest:
                    EmitWallWest(tex, wx, wy, wz, h, uvX, uvY, uvW, uvH);
                    return true;

                case TileOrientation.Corner:
                case TileOrientation.CornerNW:
                case TileOrientation.CornerNE:
                case TileOrientation.CornerSW:
                case TileOrientation.CornerSE:
                    // Sprite-quad fallback for corners without a manifest GLB.
                    // Manifest-backed corners go through WallMeshDrawer above
                    // with proper subtype rotation; this path skips since
                    // adjacent walls visually fill the gap on the 2D quads.
                    skipped++;
                    return false;

                default: // Unknown
                    if (data.IsWall && ShowFallbackForUnknownWalls)
                    {
                        unknown++;
                        // Magenta-tinted south-facing fallback so missing JSON entries
                        // jump out visually. Use the actual tile texture so we can
                        // identify which graphic ID needs a mapping.
                        EmitWallSouth(_fallbackTexture, wx, wy, wz, h, 0, 0, 1, 1);
                    }
                    else
                    {
                        skipped++;
                    }
                    return false;
            }
        }

        // =================================================================
        // Quad emitters. Vertex order per quad: TL, TR, BL, BL, TR, BR
        // (TriangleList, no index buffer). UV mapping is uniform: TL=(uMin,vMin),
        // TR=(uMax,vMin), BL=(uMin,vMax), BR=(uMax,vMax).
        // =================================================================

        private static void EmitFloor(Texture2D tex, float wx, float wy, float wz,
            float uvX, float uvY, float uvW, float uvH)
        {
            // Horizontal quad on XZ plane at height wy. Looking down +Y.
            //   NW (wx,         wy, wz       )
            //   NE (wx+TILE,    wy, wz       )
            //   SW (wx,         wy, wz+TILE  )
            //   SE (wx+TILE,    wy, wz+TILE  )
            var nw = new Vector3(wx,        wy, wz);
            var ne = new Vector3(wx + TILE, wy, wz);
            var sw = new Vector3(wx,        wy, wz + TILE);
            var se = new Vector3(wx + TILE, wy, wz + TILE);
            EmitQuad(tex, nw, ne, sw, se, uvX, uvY, uvW, uvH);
        }

        private static void EmitWallSouth(Texture2D tex, float wx, float wy, float wz, float h,
            float uvX, float uvY, float uvW, float uvH)
        {
            // South edge of tile: world Z = wz + TILE. Wall plane parallel to XY plane.
            float zEdge = wz + TILE;
            var bl = new Vector3(wx,        wy,     zEdge);
            var br = new Vector3(wx + TILE, wy,     zEdge);
            var tl = new Vector3(wx,        wy + h, zEdge);
            var tr = new Vector3(wx + TILE, wy + h, zEdge);
            EmitQuad(tex, tl, tr, bl, br, uvX, uvY, uvW, uvH);
        }

        private static void EmitWallNorth(Texture2D tex, float wx, float wy, float wz, float h,
            float uvX, float uvY, float uvW, float uvH)
        {
            // North edge: world Z = wz.
            float zEdge = wz;
            var bl = new Vector3(wx,        wy,     zEdge);
            var br = new Vector3(wx + TILE, wy,     zEdge);
            var tl = new Vector3(wx,        wy + h, zEdge);
            var tr = new Vector3(wx + TILE, wy + h, zEdge);
            EmitQuad(tex, tl, tr, bl, br, uvX, uvY, uvW, uvH);
        }

        private static void EmitWallEast(Texture2D tex, float wx, float wy, float wz, float h,
            float uvX, float uvY, float uvW, float uvH)
        {
            // East edge: world X = wx + TILE. Wall plane parallel to YZ.
            float xEdge = wx + TILE;
            var bl = new Vector3(xEdge, wy,     wz);
            var br = new Vector3(xEdge, wy,     wz + TILE);
            var tl = new Vector3(xEdge, wy + h, wz);
            var tr = new Vector3(xEdge, wy + h, wz + TILE);
            EmitQuad(tex, tl, tr, bl, br, uvX, uvY, uvW, uvH);
        }

        private static void EmitWallWest(Texture2D tex, float wx, float wy, float wz, float h,
            float uvX, float uvY, float uvW, float uvH)
        {
            // West edge: world X = wx.
            float xEdge = wx;
            var bl = new Vector3(xEdge, wy,     wz);
            var br = new Vector3(xEdge, wy,     wz + TILE);
            var tl = new Vector3(xEdge, wy + h, wz);
            var tr = new Vector3(xEdge, wy + h, wz + TILE);
            EmitQuad(tex, tl, tr, bl, br, uvX, uvY, uvW, uvH);
        }

        private static void EmitQuad(Texture2D tex,
            Vector3 tl, Vector3 tr, Vector3 bl, Vector3 br,
            float uvX, float uvY, float uvW, float uvH)
        {
            var list = GetBatch(tex);
            float uMin = uvX, vMin = uvY;
            float uMax = uvX + uvW, vMax = uvY + uvH;

            // Tri 1: TL, TR, BL
            list.Add(new VertexPositionTexture(tl, new Vector2(uMin, vMin)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            // Tri 2: BL, TR, BR
            list.Add(new VertexPositionTexture(bl, new Vector2(uMin, vMax)));
            list.Add(new VertexPositionTexture(tr, new Vector2(uMax, vMin)));
            list.Add(new VertexPositionTexture(br, new Vector2(uMax, vMax)));
        }

        private static List<VertexPositionTexture> GetBatch(Texture2D tex)
        {
            if (!_batches.TryGetValue(tex, out var list))
            {
                list = new List<VertexPositionTexture>(64);
                _batches[tex] = list;
                _batchKeys.Add(tex);
            }
            else if (list.Count == 0)
            {
                // First use this frame — register key.
                if (!_batchKeys.Contains(tex))
                    _batchKeys.Add(tex);
            }
            return list;
        }

        private static bool IsSnowWeatherActive()
        {
            var t = Weather3DSystem.Type;
            return t == Weather3DType.Snow || t == Weather3DType.Blizzard;
        }

        // Return a cached white-RGB / alpha-preserved clone of `src`. We copy
        // the entire atlas once (UV coords from the original draw call still
        // index into the same pixel positions in the clone). On GetData failure
        // (some RT-like surfaces), return null so the caller skips the overlay.
        private static Texture2D GetSnowTinted(GraphicsDevice gd, Texture2D src)
        {
            if (src == null) return null;
            if (_snowTexCache.TryGetValue(src, out var cached)) return cached;
            try
            {
                int n = src.Width * src.Height;
                var data = new Color[n];
                src.GetData(data);
                for (int i = 0; i < n; i++)
                {
                    byte a = data[i].A;
                    data[i] = new Color((byte)255, (byte)255, (byte)255, a);
                }
                var clone = new Texture2D(gd, src.Width, src.Height);
                clone.SetData(data);
                _snowTexCache[src] = clone;
                return clone;
            }
            catch
            {
                _snowTexCache[src] = null; // poison so we don't retry every frame
                return null;
            }
        }

        private static void EnsureResources(GraphicsDevice gd)
        {
            if (_effect == null)
            {
                _effect = new BasicEffect(gd);
            }
            if (_fallbackTexture == null)
            {
                _fallbackTexture = new Texture2D(gd, 2, 2);
                var pixels = new Color[] { Color.Magenta, Color.Black, Color.Black, Color.Magenta };
                _fallbackTexture.SetData(pixels);
            }
        }
    }
}
