// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Plays UO classic sound IDs (the engine's <c>0x0207</c> "explosion" etc.) at a tile
    /// coordinate, with the engine's distance-mixed pan/volume calculation. Production-side
    /// wraps <c>Client.Game.Audio.PlaySoundWithDistance</c>; tests use a no-op fake.
    /// </summary>
    public interface IUOWorldSoundPlayer
    {
        /// <summary>Play <paramref name="uoSoundId"/> at the given tile coordinate.</summary>
        void PlaySoundAtTile(int uoSoundId, int tileX, int tileY);
    }
}
