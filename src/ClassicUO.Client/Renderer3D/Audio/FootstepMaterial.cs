// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Audio domain (ADR-012).

namespace ClassicUO.Renderer.Audio
{
    /// <summary>
    /// Material classification for footstep audio. Bit-for-bit identical to the legacy
    /// <c>Renderer3D.FootstepMaterial</c> (locked by parity test).
    /// </summary>
    public enum FootstepMaterial
    {
        Auto = 0,
        Grass = 1,
        Stone = 2,
        Sand = 3,
        Wood = 4,
        Snow = 5,
        Gravel = 6,
        Water = 7,    // "Water or Mud"
        Metal = 8,
        Rug = 9,
    }

    /// <summary>Footwear kind used to pick "Shoe Step" vs "Bare Step" filename prefix.</summary>
    public enum FootwearKind
    {
        Bare = 0,
        Shoe = 1,
    }
}
