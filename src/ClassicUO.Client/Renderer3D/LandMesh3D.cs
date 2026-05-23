// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Phase 2 (textured): per-chunk heightmap mesh, UO-textured.
// Each tile samples its UO terrain texmap (LandData[Graphic].TexID) — or the
// flat land Art tile when the land isn't "stretched" — exactly like the 2D
// renderer's DrawStretchedLand path. Tiles are grouped by Texture2D so the
// shared 2048x2048 atlas (Texmaps/Arts) collapses an entire chunk to a single
// draw call in the common case.
//
// Coordinate system (XNA Y-up world space, kept 1:1 with iso pixels):
//   3dWorld.X = uoTileX * 22
//   3dWorld.Y = uoZ     *  4
//   3dWorld.Z = uoTileY * 22
//
// Per-corner UVs match Batcher2D.DrawStretchedLand:
//   top    (worldX,   worldY)   -> (0,0)
//   right  (worldX+1, worldY)   -> (1,0)
//   left   (worldX,   worldY+1) -> (0,1)
//   bottom (worldX+1, worldY+1) -> (1,1)
// where (0..1) is mapped through the atlas sub-rect with the same half-pixel
// inset Batcher2D.CalculateHalfPixelUVs uses.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    // Position-only vertex (FNA doesn't ship Microsoft.Xna's VertexPosition).
    // 12 bytes per vertex — used by the ground overlay VBO since the shader
    // recomputes everything per-pixel from worldspace position.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct VertexPosition3D : IVertexType
    {
        public Vector3 Position;
        public VertexPosition3D(Vector3 p) { Position = p; }

        private static readonly VertexDeclaration s_decl = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0));

        public VertexDeclaration VertexDeclaration => s_decl;
    }

    internal sealed class LandMesh3D
    {
        public const float TILE = 22f;       // iso half-tile in pixels
        public const float Z_SCALE = 4f;     // pixels per UO Z

        // Per-frame draw counters reset by World3DRenderer at the top of Draw().
        // Reported by Stats3DGump. FNA's GraphicsDevice does not expose Metrics
        // so we count manually here at the only chokepoint that matters for the
        // ground pass (overlay counts add to FrameDrawCalls / FramePrimitives too).
        public static int FrameDrawCalls;
        public static int FramePrimitives;

        // One submesh per source Texture2D (typically just the Texmaps atlas page,
        // possibly an Arts page for non-stretched land).
        private sealed class Submesh
        {
            public Texture2D Texture;
            public VertexBuffer Vbo;
            public IndexBuffer Ibo;
            public int IndexCount;
            // Tiles in this submesh are wet (water/sea/lake/river). Lets the
            // draw pass tint them — e.g. blood-red during BloodMoon weather.
            public bool IsWater;
        }

        private readonly List<Submesh> _submeshes = new();
        private bool _hasGeometry;

        // Wet/snow overlay geometry — position-only VBO mirroring the base
        // mesh's triangles. The GroundOverlay shader (per-pixel, ps_3_0) does
        // all the work: worldspace noise sampling for patchiness, animated
        // puddle ripples, and per-pixel slope masking via cross(ddx, ddy)
        // of WorldPos (true triplanar-style `dot(N, up)` without normals in
        // the vertex format). When the shader fails to load, the overlay
        // simply doesn't render — base land still draws.
        private VertexBuffer _overlayVbo;
        private IndexBuffer _overlayIbo;
        private int _overlayVertCount;
        private int _overlayIndexCount;
        private bool _hasOverlay;

        public bool HasGeometry => _hasGeometry;
        public bool HasOverlay => _hasOverlay;

        // Reusable per-texture build buffers (cleared per Build call).
        private static readonly Dictionary<Texture2D, (List<VertexPositionColorTexture> verts, List<short> indices)>
            s_groups = new();
        // Separate water-tile build buffers — same texture can technically be
        // shared, but keeping them split lets the draw pass tint water without
        // affecting neighbouring earth/grass.
        private static readonly Dictionary<Texture2D, (List<VertexPositionColorTexture> verts, List<short> indices)>
            s_waterGroups = new();

        // Reusable overlay build buffers (cleared per Build call).
        private static readonly List<VertexPosition3D> s_overlayVerts = new(512);
        private static readonly List<short>          s_overlayIdx   = new(768);

        public void Build(Chunk chunk, GraphicsDevice gd)
        {
            // Reset existing GPU buffers.
            DisposeSubmeshes();
            DisposeOverlay();
            s_groups.Clear();
            s_waterGroups.Clear();
            s_overlayVerts.Clear();
            s_overlayIdx.Clear();

            int chunkOriginX = chunk.X * 8;
            int chunkOriginY = chunk.Y * 8;

            for (int ty = 0; ty < 8; ty++)
            for (int tx = 0; tx < 8; tx++)
            {
                Land land = null;
                for (var obj = chunk.GetHeadObject(tx, ty); obj != null; obj = obj.TNext)
                {
                    if (obj is Land l) { land = l; break; }
                }
                if (land == null || !land.AllowedToDraw)
                    continue;

                // Resolve texture + UV like LandView.Draw does. Track whether we
                // ended up on a diamond-shaped art tile (artLO) vs a square texmap
                // — diamond tiles need rotated UVs so we don't sample the
                // transparent/black corners.
                Texture2D tex;
                Rectangle uv;
                bool isDiamond;
                if (land.IsStretched)
                {
                    ref readonly var info = ref Client.Game.UO.Texmaps.GetTexmap(
                        Client.Game.UO.FileManager.TileData.LandData[land.Graphic].TexID
                    );
                    if (info.Texture != null)
                    {
                        tex = info.Texture;
                        uv = info.UV;
                        isDiamond = false;
                    }
                    else
                    {
                        ref readonly var artInfo = ref Client.Game.UO.Arts.GetLand(land.Graphic);
                        if (artInfo.Texture == null) continue;
                        tex = artInfo.Texture;
                        uv = artInfo.UV;
                        isDiamond = true;
                    }
                }
                else
                {
                    ref readonly var artInfo = ref Client.Game.UO.Arts.GetLand(land.Graphic);
                    if (artInfo.Texture == null) continue;
                    tex = artInfo.Texture;
                    uv = artInfo.UV;
                    isDiamond = true;
                }

                // Half-pixel UV inset (matches Batcher2D.CalculateHalfPixelUVs).
                float invW = 1f / tex.Width;
                float invH = 1f / tex.Height;
                float uX = (uv.X + 0.5f) * invW;
                float uY = (uv.Y + 0.5f) * invH;
                float uW = (uv.Width  - 1f) * invW;
                float uH = (uv.Height - 1f) * invH;

                // Per-corner UVs.
                //   Square texmap: corners map to texture corners (0,0)..(1,1).
                //   Diamond art:   corners map to diamond points: top=(0.5,0),
                //                  right=(1,0.5), left=(0,0.5), bottom=(0.5,1)
                //                  so we sample diamond-interior pixels only.
                float uTopX, uTopY, uRightX, uRightY, uLeftX, uLeftY, uBotX, uBotY;
                if (isDiamond)
                {
                    float midX = uX + uW * 0.5f;
                    float midY = uY + uH * 0.5f;
                    uTopX   = midX;       uTopY   = uY;
                    uRightX = uX + uW;    uRightY = midY;
                    uLeftX  = uX;         uLeftY  = midY;
                    uBotX   = midX;       uBotY   = uY + uH;
                }
                else
                {
                    uTopX   = uX;         uTopY   = uY;
                    uRightX = uX + uW;    uRightY = uY;
                    uLeftX  = uX;         uLeftY  = uY + uH;
                    uBotX   = uX + uW;    uBotY   = uY + uH;
                }

                int worldX = chunkOriginX + tx;
                int worldY = chunkOriginY + ty;

                float hTop, hRight, hLeft, hBottom;
                if (land.IsStretched)
                {
                    hTop    = land.YOffsets.Top;
                    hRight  = land.YOffsets.Right;
                    hLeft   = land.YOffsets.Left;
                    hBottom = land.YOffsets.Bottom;
                }
                else
                {
                    float h = land.Z * Z_SCALE;
                    hTop = hRight = hLeft = hBottom = h;
                }

                float wx = worldX * TILE;
                float wz = worldY * TILE;

                // Per-corner Lambert from ClassicUO's pre-smoothed vertex
                // normals (Land.CalculateNormal). Each corner samples 4 quad
                // face normals from its 12-tile neighbourhood and averages —
                // and adjacent tiles run the SAME math centred on the SAME
                // shared corner, so they produce IDENTICAL normals at the
                // seam. Vertex-colour interpolation then makes the lighting
                // gradient continuous across tile boundaries. This is what
                // the 2D IsometricWorld.fx shader does (lines 60-69, mapping
                // Lambert to [0.5,1.0]); we just replicate it on the CPU and
                // bake into the per-vertex Color channel that BasicEffect
                // multiplies into the texture.
                //
                // Basis remap: ClassicUO normals are in (X=east, Y=south,
                // Z=up); our 3D world is (X=east, Y=up, Z=south). So
                // 3DNormal = (cu.X, cu.Z, cu.Y).  Flat tiles' (0,0,1)
                // becomes (0,1,0) Y-up. Non-stretched tiles never had
                // ApplyStretch run, so their normals are zero — fall back
                // to straight up.
                Vector3 nT, nR, nL, nB;
                if (land.IsStretched)
                {
                    nT = new Vector3(land.NormalTop.X,    land.NormalTop.Z,    land.NormalTop.Y);
                    nR = new Vector3(land.NormalRight.X,  land.NormalRight.Z,  land.NormalRight.Y);
                    nL = new Vector3(land.NormalLeft.X,   land.NormalLeft.Z,   land.NormalLeft.Y);
                    nB = new Vector3(land.NormalBottom.X, land.NormalBottom.Z, land.NormalBottom.Y);
                }
                else
                {
                    nT = nR = nL = nB = Vector3.UnitY;
                }

                // Sun direction in 3D world space. Matches
                // LightingState.LegacyHardcodedDir and 2D IsometricWorld.fx's
                // LIGHT_DIRECTION constant, so flat tiles sit at exactly
                // cos(45°)/2 + 0.5 ≈ 0.854 brightness — visual parity with 2D.
                Vector3 sunDir = LightingState.CurrentLightDir();
                sunDir.Normalize();

                float litT = Math.Max(Vector3.Dot(nT, sunDir), 0f) * 0.5f + 0.5f;
                float litR = Math.Max(Vector3.Dot(nR, sunDir), 0f) * 0.5f + 0.5f;
                float litL = Math.Max(Vector3.Dot(nL, sunDir), 0f) * 0.5f + 0.5f;
                float litB = Math.Max(Vector3.Dot(nB, sunDir), 0f) * 0.5f + 0.5f;

                byte cT = (byte)MathHelper.Clamp((int)(litT * 255f), 0, 255);
                byte cR = (byte)MathHelper.Clamp((int)(litR * 255f), 0, 255);
                byte cL = (byte)MathHelper.Clamp((int)(litL * 255f), 0, 255);
                byte cB = (byte)MathHelper.Clamp((int)(litB * 255f), 0, 255);

                Color colorT = new Color(cT, cT, cT, (byte)255);
                Color colorR = new Color(cR, cR, cR, (byte)255);
                Color colorL = new Color(cL, cL, cL, (byte)255);
                Color colorB = new Color(cB, cB, cB, (byte)255);

                bool isWater = land.TileData.IsWet;
                var dict = isWater ? s_waterGroups : s_groups;
                if (!dict.TryGetValue(tex, out var group))
                {
                    group = (new List<VertexPositionColorTexture>(256),
                             new List<short>(384));
                    dict[tex] = group;
                }

                int v0 = group.verts.Count;

                // Corners: top, right, left, bottom (matches DrawStretchedLand).
                group.verts.Add(new VertexPositionColorTexture(
                    new Vector3(wx,        hTop,    wz),
                    colorT, new Vector2(uTopX,   uTopY)));
                group.verts.Add(new VertexPositionColorTexture(
                    new Vector3(wx + TILE, hRight,  wz),
                    colorR, new Vector2(uRightX, uRightY)));
                group.verts.Add(new VertexPositionColorTexture(
                    new Vector3(wx,        hLeft,   wz + TILE),
                    colorL, new Vector2(uLeftX,  uLeftY)));
                group.verts.Add(new VertexPositionColorTexture(
                    new Vector3(wx + TILE, hBottom, wz + TILE),
                    colorB, new Vector2(uBotX,   uBotY)));

                // Diagonal selection: split the quad along whichever diagonal
                // has the SMALLER height delta. The shared edge of both
                // triangles is the "fold-fixed" diagonal; the other diagonal
                // is the one that bends. We want the bend to follow the slope,
                // so we keep the flatter diagonal as the shared edge.
                //   default (left↔right shared): T1 = top,left,right ; T2 = right,left,bottom
                //   alt     (top↔bottom shared): T1 = top,left,bottom; T2 = top,bottom,right
                bool flipDiag = Math.Abs(hLeft - hRight) > Math.Abs(hTop - hBottom);
                if (!flipDiag)
                {
                    group.indices.Add((short)(v0 + 0));
                    group.indices.Add((short)(v0 + 2));
                    group.indices.Add((short)(v0 + 1));
                    group.indices.Add((short)(v0 + 1));
                    group.indices.Add((short)(v0 + 2));
                    group.indices.Add((short)(v0 + 3));
                }
                else
                {
                    group.indices.Add((short)(v0 + 0));
                    group.indices.Add((short)(v0 + 2));
                    group.indices.Add((short)(v0 + 3));
                    group.indices.Add((short)(v0 + 0));
                    group.indices.Add((short)(v0 + 3));
                    group.indices.Add((short)(v0 + 1));
                }

                // ===== Overlay geometry (position-only) =====
                // Shader does all the masking per-pixel from worldspace position.
                int ov0 = s_overlayVerts.Count;
                s_overlayVerts.Add(new VertexPosition3D(new Vector3(wx,        hTop,    wz)));
                s_overlayVerts.Add(new VertexPosition3D(new Vector3(wx + TILE, hRight,  wz)));
                s_overlayVerts.Add(new VertexPosition3D(new Vector3(wx,        hLeft,   wz + TILE)));
                s_overlayVerts.Add(new VertexPosition3D(new Vector3(wx + TILE, hBottom, wz + TILE)));
                if (!flipDiag)
                {
                    s_overlayIdx.Add((short)(ov0 + 0));
                    s_overlayIdx.Add((short)(ov0 + 2));
                    s_overlayIdx.Add((short)(ov0 + 1));
                    s_overlayIdx.Add((short)(ov0 + 1));
                    s_overlayIdx.Add((short)(ov0 + 2));
                    s_overlayIdx.Add((short)(ov0 + 3));
                }
                else
                {
                    s_overlayIdx.Add((short)(ov0 + 0));
                    s_overlayIdx.Add((short)(ov0 + 2));
                    s_overlayIdx.Add((short)(ov0 + 3));
                    s_overlayIdx.Add((short)(ov0 + 0));
                    s_overlayIdx.Add((short)(ov0 + 3));
                    s_overlayIdx.Add((short)(ov0 + 1));
                }
            }

            _hasGeometry = false;
            BuildSubmeshesFrom(s_groups, gd, isWater: false);
            BuildSubmeshesFrom(s_waterGroups, gd, isWater: true);
            s_groups.Clear();
            s_waterGroups.Clear();

            // Build overlay GPU buffers (position-only).
            if (s_overlayIdx.Count > 0 && s_overlayVerts.Count > 0)
            {
                _overlayVertCount  = s_overlayVerts.Count;
                _overlayIndexCount = s_overlayIdx.Count;

                _overlayVbo = new VertexBuffer(gd, typeof(VertexPosition3D),
                    _overlayVertCount, BufferUsage.WriteOnly);
                _overlayVbo.SetData(s_overlayVerts.ToArray(), 0, _overlayVertCount);

                _overlayIbo = new IndexBuffer(gd, IndexElementSize.SixteenBits,
                    _overlayIndexCount, BufferUsage.WriteOnly);
                _overlayIbo.SetData(s_overlayIdx.ToArray(), 0, _overlayIndexCount);

                _hasOverlay = true;
            }
            s_overlayVerts.Clear();
            s_overlayIdx.Clear();
        }

        public void Draw(GraphicsDevice gd, BasicEffect effect)
        {
            if (!_hasGeometry || _submeshes.Count == 0) return;

            // Caller is responsible for setting effect.View/Projection/World and
            // for enabling VertexColorEnabled + TextureEnabled. We just bind per
            // submesh texture and re-Apply the pass.
            //
            // BloodMoon weather tints water tiles blood-red via DiffuseColor.
            // BasicEffect computes  final = Texture * VertexColor * DiffuseColor,
            // so a saturated red Diffuse pulls the water reading toward blood
            // without rebuilding the chunk VBO each time weather changes.
            bool tintWater = Weather3DSystem.Type == Weather3DType.BloodMoon;
            Vector3 waterTint = new Vector3(1.6f, 0.10f, 0.10f); // red bias
            Vector3 prevDiffuse = effect.DiffuseColor;
            bool isTinted = false;

            foreach (var sub in _submeshes)
            {
                if (sub.IsWater && tintWater)
                {
                    if (!isTinted) { effect.DiffuseColor = waterTint; isTinted = true; }
                }
                else if (isTinted)
                {
                    effect.DiffuseColor = prevDiffuse;
                    isTinted = false;
                }

                effect.Texture = sub.Texture;
                gd.SetVertexBuffer(sub.Vbo);
                gd.Indices = sub.Ibo;

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    int prims = sub.IndexCount / 3;
                    gd.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        baseVertex: 0,
                        minVertexIndex: 0,
                        numVertices: sub.Vbo.VertexCount,
                        startIndex: 0,
                        primitiveCount: prims
                    );
                    FrameDrawCalls++;
                    FramePrimitives += prims;
                }
            }

            if (isTinted) effect.DiffuseColor = prevDiffuse;
        }

        // Build VBO/IBO for each non-empty group and append to _submeshes.
        private void BuildSubmeshesFrom(
            Dictionary<Texture2D, (List<VertexPositionColorTexture> verts, List<short> indices)> groups,
            GraphicsDevice gd,
            bool isWater)
        {
            foreach (var kv in groups)
            {
                var verts = kv.Value.verts;
                var indices = kv.Value.indices;
                if (verts.Count == 0 || indices.Count == 0) continue;

                var sub = new Submesh
                {
                    Texture = kv.Key,
                    Vbo = new VertexBuffer(gd, typeof(VertexPositionColorTexture), verts.Count, BufferUsage.WriteOnly),
                    Ibo = new IndexBuffer(gd, IndexElementSize.SixteenBits, indices.Count, BufferUsage.WriteOnly),
                    IndexCount = indices.Count,
                    IsWater = isWater,
                };
                sub.Vbo.SetData(verts.ToArray(), 0, verts.Count);
                sub.Ibo.SetData(indices.ToArray(), 0, indices.Count);
                _submeshes.Add(sub);
                _hasGeometry = true;
            }
        }

        // Draw the position-only overlay geometry. Caller selects the technique
        // on the GroundOverlay effect (Wet vs Snow) and provides world/view/proj.
        public void DrawGroundOverlay(GraphicsDevice gd, Effect effect)
        {
            if (!_hasOverlay || _overlayVbo == null || _overlayIbo == null) return;

            gd.SetVertexBuffer(_overlayVbo);
            gd.Indices = _overlayIbo;

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                int prims = _overlayIndexCount / 3;
                gd.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    baseVertex: 0,
                    minVertexIndex: 0,
                    numVertices: _overlayVertCount,
                    startIndex: 0,
                    primitiveCount: prims
                );
                FrameDrawCalls++;
                FramePrimitives += prims;
            }
        }

        public void Dispose()
        {
            DisposeSubmeshes();
            DisposeOverlay();
            _hasGeometry = false;
        }

        private void DisposeSubmeshes()
        {
            for (int i = 0; i < _submeshes.Count; i++)
            {
                _submeshes[i].Vbo?.Dispose();
                _submeshes[i].Ibo?.Dispose();
            }
            _submeshes.Clear();
        }

        private void DisposeOverlay()
        {
            _overlayVbo?.Dispose(); _overlayVbo = null;
            _overlayIbo?.Dispose(); _overlayIbo = null;
            _overlayVertCount = 0;
            _overlayIndexCount = 0;
            _hasOverlay = false;
        }
    }
}
