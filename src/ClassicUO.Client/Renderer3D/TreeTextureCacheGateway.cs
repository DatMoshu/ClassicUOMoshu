// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wiring ITreeSeasonCacheGateway to the legacy
// TreeTextureCache static class.

using ClassicUO.Renderer.World;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="ITreeSeasonCacheGateway"/> backed by <see cref="TreeTextureCache"/>.
    /// </summary>
    internal sealed class TreeTextureCacheGateway : ITreeSeasonCacheGateway
    {
        public void InvalidateAll() => TreeTextureCache.InvalidateAll();
    }
}
