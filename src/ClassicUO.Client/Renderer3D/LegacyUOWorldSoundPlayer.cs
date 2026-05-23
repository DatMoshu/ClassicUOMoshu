// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IUOWorldSoundPlayer backed by the legacy
// Client.Game.Audio.PlaySoundWithDistance call.

using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IUOWorldSoundPlayer"/>. Failure is best-effort: missing audio
    /// engine is silently swallowed so a crash in audio does not prevent game logic.
    /// </summary>
    internal sealed class LegacyUOWorldSoundPlayer : IUOWorldSoundPlayer
    {
        public void PlaySoundAtTile(int uoSoundId, int tileX, int tileY)
        {
            try
            {
                ClassicUO.Client.Game.Audio?.PlaySoundWithDistance(
                    ClassicUO.Client.Game.UO?.World, (ushort)uoSoundId, tileX, tileY);
            }
            catch { /* audio is best-effort */ }
        }
    }
}
