// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Domain-owned attachment-slot identifier. Bit-for-bit identical to the legacy
    /// <c>ClassicUO.Renderer.Renderer3D.AttachSlotKind</c> (locked by parity test).
    /// Cast through <c>(int)</c> to convert between the two during the transitional period.
    /// </summary>
    public enum OutfitSlot
    {
        // ── Bone-attached pieces (rigid: skinned to a Synty *Attach socket) ──
        Head = 0,
        Face = 1,
        Back = 2,
        HipFront = 3,
        HipBack = 4,
        HipLeft = 5,
        HipRight = 6,
        ShoulderLeft = 7,
        ShoulderRight = 8,

        // ── Skinned body parts (deform with full body animation) ──
        Torso = 9,
        HipsMesh = 10,
        ArmUpperL = 11,
        ArmUpperR = 12,
        ArmLowerL = 13,
        ArmLowerR = 14,
        HandL = 15,
        HandR = 16,
        LegL = 17,
        LegR = 18,
        FootL = 19,
        FootR = 20,
        Hair = 21,
        EarL = 22,
        EarR = 23,
        EyebrowL = 24,
        EyebrowR = 25,
        FaceCheek = 26,
        Nose = 27,
        Teeth = 28,
    }
}
