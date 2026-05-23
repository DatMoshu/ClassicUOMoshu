// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — procedural snowflake texture for the particle system.
//
// Generates a 64x64 RGBA texture of a 6-armed snowflake (white-on-transparent)
// at first request and caches it. No network / asset dependency — the shape is
// drawn analytically from a polar function and softened with a radial alpha
// falloff so it reads as a fluffy flake rather than a sharp star.

using System;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class SnowflakeTextureFactory
    {
        private const int SIZE = 64;
        private static Texture2D _cached;

        public static Texture2D Get(GraphicsDevice gd)
        {
            if (_cached != null) return _cached;
            _cached = Build(gd);
            return _cached;
        }

        private static Texture2D Build(GraphicsDevice gd)
        {
            var pixels = new uint[SIZE * SIZE];
            float cx = (SIZE - 1) * 0.5f;
            float cy = (SIZE - 1) * 0.5f;
            float maxR = SIZE * 0.5f;

            // Six-fold symmetric snowflake: arms drawn as a polar mask m(θ),
            // multiplied by a radial alpha falloff so the flake fades into
            // transparency at the edge rather than ending in a hard ring.
            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = MathF.Sqrt(dx * dx + dy * dy) / maxR; // 0..1+
                    if (r > 1f) { pixels[y * SIZE + x] = 0; continue; }

                    float theta = MathF.Atan2(dy, dx);

                    // Fold angle into a single 60° wedge so we only design one arm.
                    float wedge = MathF.PI / 3f;
                    float t = theta;
                    while (t < 0f) t += wedge;
                    while (t >= wedge) t -= wedge;
                    // Distance from the closest arm centerline (0 at center, wedge/2 at edge).
                    float dArm = MathF.Abs(t - wedge * 0.5f);

                    // Main arm: thin radial band whose thickness shrinks with r
                    // (looks like a needle). Thickness is in radians.
                    float armThick = 0.10f + 0.18f * (1f - r);
                    float armMask = MathF.Max(0f, 1f - dArm / armThick);

                    // Side branches: two perpendicular ticks at r ≈ 0.45 and 0.75.
                    float branchMask = 0f;
                    branchMask += BranchTick(r, dArm, 0.45f, 0.06f, 0.20f);
                    branchMask += BranchTick(r, dArm, 0.72f, 0.05f, 0.14f);

                    // Bright hexagonal core (small disc near the center).
                    float coreMask = MathF.Max(0f, 1f - r / 0.18f);
                    coreMask *= coreMask;

                    float mask = MathF.Min(1f, armMask + branchMask + coreMask);

                    // Radial alpha falloff so the flake feathers into transparency.
                    float falloff = 1f - SmoothStep(0.55f, 1f, r);
                    float a = mask * falloff;
                    if (a <= 0.01f) { pixels[y * SIZE + x] = 0; continue; }

                    byte alpha = (byte)Math.Clamp((int)(a * 255f), 0, 255);
                    // Slight cool tint at edges, near-white at center.
                    byte rch = (byte)Math.Clamp((int)(235 + 20 * (1f - r)), 0, 255);
                    byte gch = (byte)Math.Clamp((int)(240 + 15 * (1f - r)), 0, 255);
                    byte bch = 255;
                    // Premultiplied? No — XNA SpriteBatch defaults to premultiplied
                    // but here we draw with a custom AlphaBlend state in the
                    // particle path, so straight alpha works. Pack RGBA = ABGR
                    // little-endian for SurfaceFormat.Color.
                    pixels[y * SIZE + x] =
                        (uint)alpha << 24 | (uint)bch << 16 | (uint)gch << 8 | rch;
                }
            }

            var tex = new Texture2D(gd, SIZE, SIZE, false, SurfaceFormat.Color);
            tex.SetData(pixels);
            return tex;
        }

        private static float BranchTick(float r, float dArm, float at, float radialWidth, float angularWidth)
        {
            float dr = MathF.Abs(r - at);
            if (dr > radialWidth) return 0f;
            float radial = 1f - dr / radialWidth;
            float ang    = MathF.Max(0f, 1f - dArm / angularWidth);
            return radial * ang;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
