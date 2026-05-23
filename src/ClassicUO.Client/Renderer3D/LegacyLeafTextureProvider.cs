// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wiring ILeafTextureProvider to LeafTextureFactory.

using ClassicUO.Renderer.World;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="ILeafTextureProvider"/> backed by
    /// <see cref="LeafTextureFactory.Get(GraphicsDevice)"/>.
    /// </summary>
    internal sealed class LegacyLeafTextureProvider : ILeafTextureProvider
    {
        public Texture2D GetTexture(GraphicsDevice device) => LeafTextureFactory.Get(device);
    }
}
