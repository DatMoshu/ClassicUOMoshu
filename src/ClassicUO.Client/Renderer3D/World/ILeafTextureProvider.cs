// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Provides the leaf-quad texture to <see cref="LeafFallService"/>'s draw path.
    /// Production-side wraps <c>LeafTextureFactory.Get</c>; tests do not exercise the
    /// draw path so a null-returning fake suffices.
    /// </summary>
    public interface ILeafTextureProvider
    {
        /// <summary>Return the leaf texture for the supplied device, or null if unavailable.</summary>
        Texture2D GetTexture(GraphicsDevice device);
    }
}
