// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — per-graphic recolored leaf texture cache.
// Reads the source UO sprite for a leaf static, applies snow + seasonal
// hue/saturation transforms (driven by TreeSeasonManager), and returns a
// runtime Texture2D that both 2D and 3D static draw paths can substitute
// for the original atlas region.
//
// Pixel format from ArtLoader is ABGR8888 packed into a uint
// (LE: byte0=R, byte1=G, byte2=B, byte3=A).

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class TreeTextureCache
    {
        private readonly struct Key : IEquatable<Key>
        {
            public readonly ushort Graphic;
            public readonly int    Token;
            public readonly bool   ApplySnow;
            public Key(ushort g, int t, bool snow) { Graphic = g; Token = t; ApplySnow = snow; }
            public bool Equals(Key o) => Graphic == o.Graphic && Token == o.Token && ApplySnow == o.ApplySnow;
            public override bool Equals(object obj) => obj is Key k && Equals(k);
            public override int GetHashCode() => ((Graphic * 397) ^ Token) ^ (ApplySnow ? 1 : 0);
        }

        private static readonly Dictionary<Key, Texture2D> _cache = new();
        private static GraphicsDevice _gd;

        // Pixels with greenness >= this threshold get hue/sat treatment.
        // Below it, the pixel is treated as "twig/branch" and only receives
        // snow (not hue shift) so brown branches stay brown across seasons.
        public static int GreenThreshold = 8;

        public static int CachedCount => _cache.Count;

        public static void SetDevice(GraphicsDevice gd) => _gd = gd;

        public static void InvalidateAll()
        {
            foreach (var t in _cache.Values) t?.Dispose();
            _cache.Clear();
        }

        // Returns the recolored texture for the given leaf-static graphic.
        // Returns null when seasonal recoloring is disabled (caller should
        // fall through to the original atlas texture) or when the source
        // sprite is empty. `applySnow` lets the caller opt out of snow per
        // tile (e.g. tiles covered by a roof) while still getting hue + sat.
        public static Texture2D Get(ushort graphic, bool applySnow = true)
        {
            if (!TreeSeasonManager.Enabled) return null;
            // Lazy-init device so the 2D path (which doesn't touch
            // Static3DRenderer.Draw) still gets a recolored texture.
            if (_gd == null) _gd = Client.Game.GraphicsDevice;
            if (_gd == null) return null;

            var key = new Key(graphic, TreeSeasonManager.QuantisedCacheToken(), applySnow);
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var loader = Client.Game.UO.FileManager.Arts;
            var src = loader.GetArt((uint)(graphic + 0x4000));
            if (src.Pixels.IsEmpty || src.Width <= 0 || src.Height <= 0)
            {
                _cache[key] = null;
                return null;
            }

            int w = src.Width, h = src.Height;
            var data = new uint[w * h];

            float snow      = applySnow ? TreeSeasonManager.SnowAmount : 0f;
            float snowLine  = TreeSeasonManager.SnowLineFrac;
            float hueDeg    = TreeSeasonManager.HueShiftDeg;
            float satBoost  = TreeSeasonManager.SaturationBoost;

            // Per-species fall hue override (tree-statics.json). Lets oaks
            // tint red, walnuts yellow, willows gold, evergreens stay green.
            // Blended in across YearProgress 0.50..0.65 (autumn ramp), held
            // at peak through 0.65..0.72, faded back out 0.72..0.78. Outside
            // autumn we leave hueDeg/satBoost at the global TreeSeasonManager
            // values (winter desat, spring green-shift, summer neutral).
            if (TreeStaticRegistry.TryGet(graphic, out var regEntry))
            {
                float yp = TreeSeasonManager.YearProgress;
                float autumnT = 0f;
                // Color stays at full fall hue right through the canopy
                // shrink-to-bare in winter, so leaves don't flash green on
                // their way to disappearing. Reset only happens during the
                // bare window (0.95..wrap..0.05) so spring's regrow starts
                // visibly green — the swap is invisible because LeafPresence
                // is 0 across that whole stretch.
                if      (yp < 0.05f) autumnT = 1f;                          // tail of winter (no leaves anyway)
                else if (yp < 0.50f) autumnT = 0f;                          // spring + summer green
                else if (yp < 0.65f) autumnT = (yp - 0.50f) / 0.15f;        // ramp 0→1
                else                 autumnT = 1f;                          // hold through scale-out + winter

                if (autumnT > 0f)
                {
                    // FallColorSharpness reshapes the 0..1 curve. 1 = linear
                    // smoothstep, >1 = colors snap on faster, <1 = creep.
                    float sharp = Math.Max(0.1f, TreeSeasonManager.FallColorSharpness);
                    autumnT = (float)Math.Pow(autumnT, 1f / sharp);
                    // Override (don't add) so evergreens (FallHueDeg=0) stay
                    // green even when global TreeSeasonManager.HueShiftDeg
                    // pushes everything else toward orange.
                    hueDeg   = regEntry.FallHueDeg * autumnT;
                    satBoost = 1f + (regEntry.FallSatBoost - 1f) * autumnT;
                }
            }

            int   greenT    = GreenThreshold;
            float invH      = 1f / h;

            // White colour packed in ABGR8888 (alpha 0xFF, RGB 0xFF).
            const uint WHITE_RGBA = 0xFFFFFFFFu;

            for (int i = 0; i < data.Length; i++)
            {
                uint p = src.Pixels[i];
                uint a = p >> 24;
                if (a == 0)
                {
                    data[i] = 0;
                    continue;
                }

                int r = (int)(p & 0xFF);
                int g = (int)((p >> 8) & 0xFF);
                int b = (int)((p >> 16) & 0xFF);

                bool isLeaf = (g - Math.Max(r, b)) >= greenT;

                // Hue + sat shift (leaf pixels only).
                if (isLeaf && (hueDeg != 0f || satBoost != 1f))
                {
                    RgbToHsv(r, g, b, out float hh, out float ss, out float vv);
                    hh = (hh + hueDeg) % 360f;
                    if (hh < 0f) hh += 360f;
                    ss = Math.Clamp(ss * satBoost, 0f, 1f);
                    HsvToRgb(hh, ss, vv, out r, out g, out b);
                }

                // Snow: V from 0 (top) to 1 (bottom). Top of canopy whitens
                // first; below snowLine no snow.
                if (snow > 0f)
                {
                    int y = i / w;
                    float v = y * invH;
                    if (v < snowLine)
                    {
                        float snowy = (1f - v / snowLine) * snow;
                        if (snowy > 0f)
                        {
                            r = LerpByte(r, 0xFF, snowy);
                            g = LerpByte(g, 0xFF, snowy);
                            b = LerpByte(b, 0xFF, snowy);
                        }
                    }
                }

                data[i] = ((uint)a << 24)
                        | ((uint)(b & 0xFF) << 16)
                        | ((uint)(g & 0xFF) << 8)
                        |  (uint)(r & 0xFF);

                _ = WHITE_RGBA; // touch to silence unused-warning if removed later
            }

            var tex = new Texture2D(_gd, w, h, false, SurfaceFormat.Color);
            tex.SetData(data);
            _cache[key] = tex;
            return tex;
        }

        private static int LerpByte(int a, int b, float t)
            => Math.Clamp((int)(a + (b - a) * t), 0, 255);

        // Standard RGB(0..255) <-> HSV(h:0..360, s:0..1, v:0..1).
        private static void RgbToHsv(int r, int g, int b, out float h, out float s, out float v)
        {
            float rf = r / 255f, gf = g / 255f, bf = b / 255f;
            float max = Math.Max(rf, Math.Max(gf, bf));
            float min = Math.Min(rf, Math.Min(gf, bf));
            v = max;
            float d = max - min;
            s = max <= 0f ? 0f : d / max;
            if (d <= 0f) { h = 0f; return; }
            if (max == rf)      h = 60f * (((gf - bf) / d) % 6f);
            else if (max == gf) h = 60f * (((bf - rf) / d) + 2f);
            else                h = 60f * (((rf - gf) / d) + 4f);
            if (h < 0f) h += 360f;
        }

        private static void HsvToRgb(float h, float s, float v, out int r, out int g, out int b)
        {
            float c = v * s;
            float hh = (h % 360f) / 60f;
            float x = c * (1f - Math.Abs((hh % 2f) - 1f));
            float rf = 0, gf = 0, bf = 0;
            if      (hh >= 0 && hh < 1) { rf = c; gf = x; }
            else if (hh < 2)            { rf = x; gf = c; }
            else if (hh < 3)            { gf = c; bf = x; }
            else if (hh < 4)            { gf = x; bf = c; }
            else if (hh < 5)            { rf = x; bf = c; }
            else                        { rf = c; bf = x; }
            float m = v - c;
            r = Math.Clamp((int)((rf + m) * 255f + 0.5f), 0, 255);
            g = Math.Clamp((int)((gf + m) * 255f + 0.5f), 0, 255);
            b = Math.Clamp((int)((bf + m) * 255f + 0.5f), 0, 255);
        }
    }
}
