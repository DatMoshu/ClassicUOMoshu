// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — single-source classification for Static GameObjects.
//
// Categorizes each static into a StaticKind so Static3DRenderer can choose
// between cylindrical billboard (trees, foliage), flat ground decal (sticks,
// rocks, water edges), or fall through. Uses TileData flags + StaticFilters.

using System.Runtime.CompilerServices;
using ClassicUO.Assets;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Renderer.Renderer3D
{
    internal enum StaticKind : byte
    {
        Tree,         // Tall foliage / explicit tree (no trunk/leaf split known)
        TreeTrunk,    // tree.txt flag=0 — base/stump; single fixed plane
        TreeLeaf,     // tree.txt flag=1 — canopy/leaves; crossed planes + seasonal recolor
        Foliage,      // Bushes / vines (IsFoliage, short) — billboard, no bias
        GroundDecal,  // Sticks, rocks, vegetation tufts — flat quad on ground
        WaterEdge,    // IsWet — flat quad, slight Y lift
        Wall,         // IsWall — Multi3D handles; safety net only
        Roof,         // IsRoof — Multi3D handles; safety net only
        Surface,      // Walkable surface — flat quad
        Background,   // IsBackground — flat quad on ground
        Other,        // Unclassified — keep current billboard behavior

        // ---- Registry-driven kinds (tree-statics.json) ----
        // Whole-tree sprite (trunk + canopy baked together). MUST render as a
        // single billboard — crossing it duplicates the trunk and produces the
        // "radiating trunks" artifact this rework exists to fix.
        WholeTree,
        // Small canopy-only overlay (the "leaves" / "needles" companion static
        // that sits next to a WholeTree in UO). This is where crossed planes,
        // seasonal recolor, and DropLeaves actually belong.
        LeafOverlay
    }

    internal static class StaticClassifier
    {
        // Heights are tiledata 0..255. UO tile is 22 iso pixels wide; "tall"
        // foliage like trees are usually 30-60. Below ~10 is ground-clutter.
        private const int TALL_FOLIAGE_MIN_HEIGHT = 10;
        private const int GROUND_DECAL_MAX_HEIGHT = 8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StaticKind Classify(Static s)
        {
            return Classify(s.Graphic, in s.ItemData);
        }

        // Toggle: when false, fall back to the legacy tree.txt-only path so
        // the user can A/B compare against the registry.
        public static bool UseTreeStaticRegistry = true;

        public static StaticKind Classify(ushort graphic, in StaticTiles td)
        {
            // 0. Per-graphic tree registry (tree-statics.json) — preferred.
            //    Distinguishes WholeTree (single billboard) from LeafOverlay
            //    (crossed planes / seasonal). Tree.txt's 0/1 flag conflates
            //    these and is the root cause of the "radiating trunk" bug.
            if (UseTreeStaticRegistry && TreeStaticRegistry.TryGet(graphic, out var entry))
            {
                switch (entry.Kind)
                {
                    case ClassicUO.Renderer.Statics.TreeStaticKind.WholeTree:   return StaticKind.WholeTree;
                    case ClassicUO.Renderer.Statics.TreeStaticKind.LeafOverlay: return StaticKind.LeafOverlay;
                    case ClassicUO.Renderer.Statics.TreeStaticKind.TrunkOnly:   return StaticKind.TreeTrunk;
                    case ClassicUO.Renderer.Statics.TreeStaticKind.Bush:        return StaticKind.Foliage;
                }
            }

            // 1. Explicit tree list (tree.txt) — split by per-graphic flag.
            if (StaticFilters.IsTreeLeaf(graphic))
                return StaticKind.TreeLeaf;
            if (StaticFilters.IsTreeTrunk(graphic))
                return StaticKind.TreeTrunk;
            if (StaticFilters.IsTree(graphic, out _))
                return StaticKind.Tree;

            // 2. Walls / roofs — should be handled by Multi3D, fall back here.
            if (td.IsWall) return StaticKind.Wall;
            if (td.IsRoof) return StaticKind.Roof;

            // 3. Water — shoreline / waves / lake tiles.
            if (td.IsWet) return StaticKind.WaterEdge;

            // 4. Tall foliage = tree-like (branches, leaves with foliage flag).
            if (td.IsFoliage && td.Height >= TALL_FOLIAGE_MIN_HEIGHT)
                return StaticKind.Tree;

            // 5. Short foliage = bushes / vines.
            if (td.IsFoliage)
                return StaticKind.Foliage;

            // 6. Vegetation list (grass tufts) and rock list (small boulders).
            if (StaticFilters.IsVegetation(graphic) || StaticFilters.IsRock(graphic))
                return StaticKind.GroundDecal;

            // 7. Tiny background/surface props = sticks, pebbles, debris.
            if (td.Height <= GROUND_DECAL_MAX_HEIGHT && (td.IsBackground || td.IsSurface))
                return StaticKind.GroundDecal;

            // 8. Walkable surface, e.g. floor tiles, planks.
            if (td.IsSurface) return StaticKind.Surface;

            // 9. Background-flagged but not classified above.
            if (td.IsBackground) return StaticKind.Background;

            return StaticKind.Other;
        }

        // True iff this kind should render as a flat XZ ground quad rather than
        // a camera-facing billboard.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsGroundDecalKind(StaticKind k)
        {
            return k == StaticKind.GroundDecal
                || k == StaticKind.WaterEdge
                || k == StaticKind.Surface
                || k == StaticKind.Background;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsTreeKind(StaticKind k)
        {
            return k == StaticKind.Tree
                || k == StaticKind.TreeTrunk
                || k == StaticKind.TreeLeaf
                || k == StaticKind.WholeTree
                || k == StaticKind.LeafOverlay;
        }
    }
}
