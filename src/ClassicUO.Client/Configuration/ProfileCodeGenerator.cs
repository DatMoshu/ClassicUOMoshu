// SPDX-License-Identifier: BSD-2-Clause

// PP.A10 — Profile code generation
// -----------------------------------
// Produces a 48-character Base32 shareable code from the SHA-256 of the package bytes.
// Used to identify and share profiles server-side.
//
// Algorithm:
//   1. SHA-256(file_bytes)       → 32 bytes
//   2. Take first 30 bytes       → 240 bits
//   3. Base32-encode (RFC 4648)  → 48 chars (A-Z 2-7, no padding needed for 30 bytes)
//   4. Format as 6 groups of 8  → XXXXXXXX-XXXXXXXX-... (53 chars with dashes)
//
// The ContentHash in ProfileManifest uses Base32(SHA-256[0:20]) = 32 chars (shorter ID).
// The display code here uses 30 bytes → 48 chars for better collision resistance.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ClassicUO.Configuration
{
    internal static class ProfileCodeGenerator
    {
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>
        /// Computes a 48-character Base32 code from the SHA-256 of the given file.
        /// Returns an empty string if the file cannot be read.
        /// </summary>
        public static string ComputeFromFile(string filePath)
        {
            byte[] fileBytes;
            try   { fileBytes = File.ReadAllBytes(filePath); }
            catch { return string.Empty; }

            return ComputeFromBytes(fileBytes);
        }

        /// <summary>
        /// Computes a 48-character Base32 code from the SHA-256 of the given bytes.
        /// </summary>
        public static string ComputeFromBytes(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            // First 30 bytes → 48 Base32 chars (30 * 8 / 5 = 48, no remainder)
            return Base32Encode(hash.AsSpan(0, 30));
        }

        /// <summary>
        /// Formats a raw 48-char code as XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX.
        /// </summary>
        public static string Format(string rawCode)
        {
            if (rawCode.Length != 48) return rawCode;

            Span<char> buf = stackalloc char[53]; // 48 chars + 5 dashes
            for (int group = 0; group < 6; group++)
            {
                int srcOffset  = group * 8;
                int destOffset = group * 9;
                rawCode.AsSpan(srcOffset, 8).CopyTo(buf.Slice(destOffset, 8));
                if (group < 5) buf[destOffset + 8] = '-';
            }
            return new string(buf);
        }

        /// <summary>Strips dashes and uppercases a user-entered code for lookup.</summary>
        public static string Normalise(string input)
            => input?.Replace("-", "").Trim().ToUpperInvariant() ?? string.Empty;

        // ── Internals ──────────────────────────────────────────────────────────

        private static string Base32Encode(ReadOnlySpan<byte> data)
        {
            // Each 5 bytes → 8 Base32 chars. data.Length must be a multiple of 5.
            int outputLength = data.Length * 8 / 5;
            StringBuilder sb = new StringBuilder(outputLength);

            int buffer    = 0;
            int bitsLeft  = 0;

            foreach (byte b in data)
            {
                buffer   = (buffer << 8) | b;
                bitsLeft += 8;

                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
                }
            }

            return sb.ToString();
        }
    }
}
