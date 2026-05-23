// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wrapping the legacy ParticleStringBuilder behind
// IParticleStringEmitter. Owns one builder instance whose glyph-layout cache is reused
// across calls — important for per-tick climax emission.

using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IParticleStringEmitter"/> backed by
    /// <see cref="ParticleStringBuilder"/>. The builder caches its glyph layout, so per-tick
    /// emit calls are cheap when text + cell size don't change.
    /// </summary>
    internal sealed class LegacyParticleStringEmitter : IParticleStringEmitter
    {
        private readonly ParticleStringBuilder _builder = new();

        public void EmitText(
            string text,
            Vector3 origin,
            Color colorStart,
            Color colorEnd,
            float lifetimeSeconds,
            float cellSize,
            float sizeStart,
            float sizeEnd)
        {
            _builder
                .Text(text ?? string.Empty)
                .CellSize(cellSize)
                .Align(TextAlign.Center)
                .Pinned()
                .Size(sizeStart, sizeEnd)
                .Life(lifetimeSeconds)
                .Colors(colorStart, colorEnd)
                .At(origin)
                .Emit();
        }

        public void InvalidateLayout() => _builder.Invalidate();
    }
}
