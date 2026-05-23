// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — bitmap-font glyph table and layout helpers used by
// Particle3DSystem to render arbitrary strings as constellations of particles
// in 3D world space.
//
// Glyph format: 7 rows × 5 columns. Each row is the lower 5 bits of an int
// (bit 4 = leftmost column, bit 0 = rightmost). Set bits = lit pixels.
//
// Coverage: A–Z, 0–9, space, ! ? . , ' - : / + (sufficient for hand-typed
// celebratory text and short messages).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class Particle3DText
    {
        public const int GLYPH_W = 5;
        public const int GLYPH_H = 7;
        public const int KERN = 1;

        // ===== Glyph table =====
        // Letters
        private static readonly int[] G_A = { 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 };
        private static readonly int[] G_B = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110 };
        private static readonly int[] G_C = { 0b01111, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b01111 };
        private static readonly int[] G_D = { 0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110 };
        private static readonly int[] G_E = { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111 };
        private static readonly int[] G_F = { 0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000 };
        private static readonly int[] G_G = { 0b01111, 0b10000, 0b10000, 0b10011, 0b10001, 0b10001, 0b01111 };
        private static readonly int[] G_H = { 0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001 };
        private static readonly int[] G_I = { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111 };
        private static readonly int[] G_J = { 0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100 };
        private static readonly int[] G_K = { 0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001 };
        private static readonly int[] G_L = { 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111 };
        private static readonly int[] G_M = { 0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001 };
        private static readonly int[] G_N = { 0b10001, 0b11001, 0b10101, 0b10101, 0b10101, 0b10011, 0b10001 };
        private static readonly int[] G_O = { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 };
        private static readonly int[] G_P = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000 };
        private static readonly int[] G_Q = { 0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101 };
        private static readonly int[] G_R = { 0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001 };
        private static readonly int[] G_S = { 0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110 };
        private static readonly int[] G_T = { 0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100 };
        private static readonly int[] G_U = { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 };
        private static readonly int[] G_V = { 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100 };
        private static readonly int[] G_W = { 0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b10101, 0b01010 };
        private static readonly int[] G_X = { 0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001 };
        private static readonly int[] G_Y = { 0b10001, 0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100 };
        private static readonly int[] G_Z = { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111 };

        // Digits
        private static readonly int[] G_0 = { 0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110 };
        private static readonly int[] G_1 = { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 };
        private static readonly int[] G_2 = { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111 };
        private static readonly int[] G_3 = { 0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110 };
        private static readonly int[] G_4 = { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 };
        private static readonly int[] G_5 = { 0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110 };
        private static readonly int[] G_6 = { 0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 };
        private static readonly int[] G_7 = { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 };
        private static readonly int[] G_8 = { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 };
        private static readonly int[] G_9 = { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100 };

        // Punctuation
        private static readonly int[] G_BANG     = { 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100 };
        private static readonly int[] G_QUESTION = { 0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b00000, 0b00100 };
        private static readonly int[] G_PERIOD   = { 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b01100, 0b01100 };
        private static readonly int[] G_COMMA    = { 0b00000, 0b00000, 0b00000, 0b00000, 0b01100, 0b01100, 0b00100 };
        private static readonly int[] G_APOST    = { 0b00100, 0b00100, 0b00100, 0b00000, 0b00000, 0b00000, 0b00000 };
        private static readonly int[] G_HYPHEN   = { 0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000 };
        private static readonly int[] G_COLON    = { 0b00000, 0b01100, 0b01100, 0b00000, 0b01100, 0b01100, 0b00000 };
        private static readonly int[] G_SLASH    = { 0b00001, 0b00010, 0b00010, 0b00100, 0b01000, 0b01000, 0b10000 };
        private static readonly int[] G_PLUS     = { 0b00000, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0b00000 };
        private static readonly int[] G_SPACE    = { 0, 0, 0, 0, 0, 0, 0 };

        // Fallback for unsupported chars: thin vertical bar, so layout still
        // shows where the unknown glyph lives without throwing.
        private static readonly int[] G_UNKNOWN  = { 0b01110, 0b01010, 0b01010, 0b01010, 0b01010, 0b01010, 0b01110 };

        private static int[] Glyph(char c)
        {
            // Normalise to uppercase ASCII for the letter range; lowercase
            // shares the uppercase glyph (no separate descenders in 5x7).
            if (c >= 'a' && c <= 'z') c = (char)(c - 32);
            return c switch
            {
                'A' => G_A, 'B' => G_B, 'C' => G_C, 'D' => G_D, 'E' => G_E,
                'F' => G_F, 'G' => G_G, 'H' => G_H, 'I' => G_I, 'J' => G_J,
                'K' => G_K, 'L' => G_L, 'M' => G_M, 'N' => G_N, 'O' => G_O,
                'P' => G_P, 'Q' => G_Q, 'R' => G_R, 'S' => G_S, 'T' => G_T,
                'U' => G_U, 'V' => G_V, 'W' => G_W, 'X' => G_X, 'Y' => G_Y,
                'Z' => G_Z,
                '0' => G_0, '1' => G_1, '2' => G_2, '3' => G_3, '4' => G_4,
                '5' => G_5, '6' => G_6, '7' => G_7, '8' => G_8, '9' => G_9,
                '!' => G_BANG, '?' => G_QUESTION, '.' => G_PERIOD, ',' => G_COMMA,
                '\'' => G_APOST, '-' => G_HYPHEN, ':' => G_COLON, '/' => G_SLASH,
                '+' => G_PLUS,
                ' ' => G_SPACE,
                _ => G_UNKNOWN,
            };
        }

        /// Returns world-space points for every lit pixel in <paramref name="text"/>.
        /// The text plane is XY (constant Z); Y grows UP; <paramref name="origin"/>
        /// is the LEFT-CENTER of the laid-out text.
        public static Vector3[] Layout(string text, Vector3 origin, float cellSize)
        {
            int count = 0;
            for (int gi = 0; gi < text.Length; gi++)
            {
                var g = Glyph(text[gi]);
                for (int row = 0; row < GLYPH_H; row++)
                {
                    int bits = g[row];
                    for (int col = 0; col < GLYPH_W; col++)
                    {
                        if (((bits >> (GLYPH_W - 1 - col)) & 1) != 0) count++;
                    }
                }
            }

            var output = new Vector3[count];
            int idx = 0;
            float xCursor = 0f;
            for (int gi = 0; gi < text.Length; gi++)
            {
                var g = Glyph(text[gi]);
                for (int row = 0; row < GLYPH_H; row++)
                {
                    int bits = g[row];
                    for (int col = 0; col < GLYPH_W; col++)
                    {
                        if (((bits >> (GLYPH_W - 1 - col)) & 1) != 0)
                        {
                            float x = (xCursor + col) * cellSize;
                            float y = ((GLYPH_H - 1) * 0.5f - row) * cellSize;
                            output[idx++] = origin + new Vector3(x, y, 0f);
                        }
                    }
                }
                xCursor += GLYPH_W + KERN;
            }
            return output;
        }

        /// Total horizontal width (world units) the laid-out text will occupy.
        public static float MeasureWidth(string text, float cellSize)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            int cols = text.Length * (GLYPH_W + KERN) - KERN;
            return cols * cellSize;
        }
    }
}
