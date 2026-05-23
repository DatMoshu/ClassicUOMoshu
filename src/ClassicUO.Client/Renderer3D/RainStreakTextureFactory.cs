// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — procedural rain droplet/streak texture for the particle
// system. Vertical white streak with a bright punctate head (the droplet)
// and a soft trailing tail. Drawn additive so several streaks compose into
// a believable downpour rather than a row of opaque dashes.

using System;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class RainStreakTextureFactory
    {
        private const int W = 16;
        private const int H = 64;
        private static Texture2D _cached;

        public static Texture2D Get(GraphicsDevice gd)
        {
            if (_cached != null) return _cached;
            _cached = Build(gd);
            return _cached;
        }

        private static Texture2D Build(GraphicsDevice gd)
        {
            var pixels = new uint[W * H];

            for (int y = 0; y < H; y++)
            {
                // v = 0 at top, 1 at bottom of the texture.
                float v = y / (float)(H - 1);

                // Vertical envelope (0..1):
                //   head:  bright disc centred at v=0.85 (the droplet itself)
                //   tail:  long linear ramp from v=0 (faint) to v=0.85 (full)
                float head = MathF.Max(0f, 1f - MathF.Abs(v - 0.85f) / 0.15f);
                head *= head;
                float tail = (v < 0.85f) ? (v / 0.85f) * 0.55f : 0f;
                float vert = MathF.Max(head, tail);
                if (vert <= 0.01f) continue;

                // Horizontal envelope (normalized 0..1 across the texture).
                // Core is wider at the head (the droplet bulges) and pinches
                // at the tail.
                float halfWidthNorm = 0.20f + 0.30f * head;
                for (int x = 0; x < W; x++)
                {
                    float u = (x - (W - 1) * 0.5f) / ((W - 1) * 0.5f); // -1..+1
                    float dn = MathF.Abs(u) / halfWidthNorm;            // 0 at centre, 1 at edge of core
                    if (dn >= 1f) { pixels[y * W + x] = 0; continue; }
                    float horiz = 1f - dn;
                    horiz = horiz * horiz;                              // soft falloff

                    float a = vert * horiz;
                    if (a <= 0.02f) { pixels[y * W + x] = 0; continue; }

                    byte alpha = (byte)Math.Clamp((int)(a * 255f), 0, 255);
                    // Bright cool-white. Additive blend means RGB is what
                    // composites; alpha gates fragment write.
                    byte rch = 230;
                    byte gch = 240;
                    byte bch = 255;
                    pixels[y * W + x] =
                        (uint)alpha << 24 | (uint)bch << 16 | (uint)gch << 8 | rch;
                }
            }

            var tex = new Texture2D(gd, W, H, false, SurfaceFormat.Color);
            tex.SetData(pixels);
            return tex;
        }
    }
}
