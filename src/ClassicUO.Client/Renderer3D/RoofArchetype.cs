// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — roof archetype taxonomy.
//
// UO roof families (see server/ModernUO/Distribution/Data/Components/roof.txt)
// each ship 17 piece archetypes. Mirrors the wall N/E/S/W + corner taxonomy,
// but every archetype is *pitched* (not vertical) — the mesh's "up" face is the
// painted slope.
//
// Naming convention follows roof.txt column headers:
//
//   slopes   N / E / S / W            -- single-pitch slabs facing each cardinal
//   ridges   NSCrosspiece / EWCrosspiece  -- two-slope ridge caps
//   dents    N / E / S / W Dent       -- hip corners (inverted: two slopes meet)
//   tees     N / E / S / W TPiece     -- T junctions (three slopes)
//   x-piece  XPiece                   -- 4-way center / pyramid peak
//   extra    Extra                    -- gable end caps / chimney etc.
//
// Pitch is fixed at the iso-natural angle (atan(1/2) ≈ 26.565°) for v1 so the
// painted shingles in the source sprite line up with mesh edges and the iso
// parallelogram UV becomes a clean axis-aligned rectangle in mesh-local space.
// Per-family override knobs live in RoofMeshDrawer.

namespace ClassicUO.Renderer.Renderer3D
{
    internal enum RoofArchetype
    {
        Unknown = 0,

        // Single-pitch slopes. Painted slope direction faces the named cardinal.
        SlopeN, SlopeE, SlopeS, SlopeW,

        // Ridge caps (two slopes meeting at a horizontal ridge line).
        RidgeNS, RidgeEW,

        // Hip / dent corners. roof.txt calls them "dents"; geometrically they
        // are inverted ridges where two single-pitch slopes converge into a
        // diagonal valley/hip line. Named for the cardinal *into* which the
        // hip points.
        DentN, DentE, DentS, DentW,

        // T-junction pieces (three slopes meeting).
        TPieceN, TPieceE, TPieceS, TPieceW,

        // 4-way crossing / pyramid peak.
        XPiece,

        // Extra: gable end caps, chimney bases, family-specific oddities.
        Extra,
    }

    internal static class RoofArchetypeMath
    {
        // UO iso natural pitch: tan(theta) = 1/2 (one Z step per two world
        // units of horizontal). Matches how shingles are painted.
        public const float ISO_PITCH_RADIANS = 0.46364760900080615f; // atan(0.5)

        // Yaw (Y-axis rotation, radians) to apply to a canonical mesh authored
        // facing +Z so its painted slope aims at the named cardinal.
        // World convention (matches LandMesh3D / Multi3DRenderer):
        //   +X = East, +Z = South, +Y = up.
        public static float YawRadians(RoofArchetype a)
        {
            // Canonical mesh: SlopeS authored facing +Z (south). Other slopes
            // are 90°/180°/270° rotations of the same canonical slab.
            return a switch
            {
                RoofArchetype.SlopeS    =>  0f,
                RoofArchetype.SlopeW    =>  Microsoft.Xna.Framework.MathHelper.PiOver2,
                RoofArchetype.SlopeN    =>  Microsoft.Xna.Framework.MathHelper.Pi,
                RoofArchetype.SlopeE    =>  Microsoft.Xna.Framework.MathHelper.Pi + Microsoft.Xna.Framework.MathHelper.PiOver2,

                RoofArchetype.RidgeEW   =>  0f,
                RoofArchetype.RidgeNS   =>  Microsoft.Xna.Framework.MathHelper.PiOver2,

                RoofArchetype.DentS     =>  0f,
                RoofArchetype.DentW     =>  Microsoft.Xna.Framework.MathHelper.PiOver2,
                RoofArchetype.DentN     =>  Microsoft.Xna.Framework.MathHelper.Pi,
                RoofArchetype.DentE     =>  Microsoft.Xna.Framework.MathHelper.Pi + Microsoft.Xna.Framework.MathHelper.PiOver2,

                RoofArchetype.TPieceS   =>  0f,
                RoofArchetype.TPieceW   =>  Microsoft.Xna.Framework.MathHelper.PiOver2,
                RoofArchetype.TPieceN   =>  Microsoft.Xna.Framework.MathHelper.Pi,
                RoofArchetype.TPieceE   =>  Microsoft.Xna.Framework.MathHelper.Pi + Microsoft.Xna.Framework.MathHelper.PiOver2,

                _ => 0f,
            };
        }

        // Each archetype maps to one canonical mesh; many archetypes share a
        // mesh and differ only by yaw. This collapses the 17-way enum to the
        // 6 unique mesh shapes the Blender pipeline must produce.
        public static string CanonicalMeshName(RoofArchetype a) => a switch
        {
            RoofArchetype.SlopeN or RoofArchetype.SlopeE
                or RoofArchetype.SlopeS or RoofArchetype.SlopeW => "roof_slope",

            RoofArchetype.RidgeNS or RoofArchetype.RidgeEW => "roof_ridge",

            RoofArchetype.DentN or RoofArchetype.DentE
                or RoofArchetype.DentS or RoofArchetype.DentW => "roof_dent",

            RoofArchetype.TPieceN or RoofArchetype.TPieceE
                or RoofArchetype.TPieceS or RoofArchetype.TPieceW => "roof_tpiece",

            RoofArchetype.XPiece => "roof_xpiece",
            RoofArchetype.Extra  => "roof_extra",

            _ => null,
        };
    }
}
