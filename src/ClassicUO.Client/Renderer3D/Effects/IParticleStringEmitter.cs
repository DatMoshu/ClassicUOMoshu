// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Emits text as a constellation of pinned particles. Used by
    /// <see cref="FireworksService"/> for the "WE DID IT!" climax. Production-side wraps
    /// the legacy <c>ParticleStringBuilder</c> with its glyph-layout cache; tests use a
    /// recording fake.
    /// </summary>
    /// <remarks>
    /// Implementations may cache the glyph layout across calls and only rebuild when the
    /// text or cell-size changes. Callers that change those parameters mid-emit should
    /// call <see cref="InvalidateLayout"/>.
    /// </remarks>
    public interface IParticleStringEmitter
    {
        /// <summary>
        /// Configure and emit one frame's worth of pinned particles for <paramref name="text"/>
        /// centred at <paramref name="origin"/>. Implementations may apply layout caching keyed
        /// on (text, cellSize). Alignment is always centre.
        /// </summary>
        void EmitText(
            string text,
            Vector3 origin,
            Color colorStart,
            Color colorEnd,
            float lifetimeSeconds,
            float cellSize,
            float sizeStart,
            float sizeEnd);

        /// <summary>Force re-layout on the next <see cref="EmitText"/> call.</summary>
        void InvalidateLayout();
    }
}
