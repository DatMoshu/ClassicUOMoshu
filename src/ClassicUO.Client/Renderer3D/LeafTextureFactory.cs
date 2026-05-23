// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — procedural single-leaf texture for the leaf-fall particle
// system. White / light-grey leaf shape so per-particle vertex color (driven
// by SeasonCycleDriver phase) can tint it into spring green, summer green,
// autumn yellow/orange/red, etc. Alpha-cut feathered edge keeps the silhouette
// soft at small sizes.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class LeafTextureFactory
    {
        private const int W = 32;
        private const int H = 48;
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
            float cx = (W - 1) * 0.5f;

            for (int y = 0; y < H; y++)
            {
                // v = 0 top, 1 bottom. Stem at the bottom, leaf body fills the top 75%.
                float v = y / (float)(H - 1);

                // Body region (0..0.75 v) = leaf blade. Below = stem.
                float bodyV = v / 0.75f;
                if (bodyV > 1.05f)
                {
                    // STEM strip — narrow vertical line near center.
                    for (int x = 0; x < W; x++)
                    {
                        float dx = MathF.Abs(x - cx) / 1.5f; // ~3px wide stem
                        if (dx > 1f) { pixels[y * W + x] = 0; continue; }
                        float a = 1f - dx;
                        a *= MathF.Max(0f, 1f - (v - 0.85f) / 0.15f); // taper to 0 at v=1
                        if (a <= 0.05f) { pixels[y * W + x] = 0; continue; }
                        byte alpha = (byte)Math.Clamp((int)(a * 255f), 0, 255);
                        pixels[y * W + x] =
                            (uint)alpha << 24 | (uint)200 << 16 | (uint)200 << 8 | 200;
                    }
                    continue;
                }

                // BLADE — pointed-oval shape: widest in the middle, pointed at
                // both top and bottom. Half-width as a function of bodyV.
                float bv = MathHelper.Clamp(bodyV, 0f, 1f);
                // Width envelope: sin(bv*π) gives 0..1..0 from top to base.
                // Skew slightly so the widest point sits a bit below center
                // (more leaf-like than a perfect oval).
                float skewed = MathHelper.Clamp(bv < 0.55f ? bv / 0.55f : (1f - bv) / 0.45f, 0f, 1f);
                float halfWNorm = MathF.Sqrt(skewed) * 0.85f; // 0..0.85

                for (int x = 0; x < W; x++)
                {
                    float u = (x - cx) / cx; // -1..+1
                    float dn = MathF.Abs(u) / MathF.Max(0.05f, halfWNorm);
                    if (dn >= 1f) { pixels[y * W + x] = 0; continue; }

                    // Soft falloff at edges, full white in the middle.
                    float a = 1f - dn * dn;

                    // Central vein — slight darkening.
                    float vein = MathF.Max(0f, 1f - MathF.Abs(u) / 0.06f);
                    byte rch = (byte)(255 - (int)(vein * 30f));
                    byte gch = (byte)(255 - (int)(vein * 30f));
                    byte bch = (byte)(255 - (int)(vein * 30f));

                    if (a <= 0.04f) { pixels[y * W + x] = 0; continue; }
                    byte alpha = (byte)Math.Clamp((int)(a * 255f), 0, 255);
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
