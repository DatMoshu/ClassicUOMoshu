// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — shared JSON helpers for Phase 4 production storage adapters.
// JsonDocument-based, AOT-clean per BLOCKER-05/-06 of the original code review.

using System;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Tiny static helpers for reading optional fields out of a parsed JSON element.
    /// Each helper falls back to a supplied default when the field is absent or the wrong
    /// kind — matches the per-property fallback semantics established by session 67's
    /// <c>FileWindServiceConfigStorage</c>.
    /// </summary>
    internal static class JsonConfigReader
    {
        public static float ReadFloat(JsonElement root, string name, float fallback)
            => root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.Number
                ? el.GetSingle()
                : fallback;

        public static int ReadInt(JsonElement root, string name, int fallback)
            => root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.Number
                ? el.GetInt32()
                : fallback;

        public static bool ReadBool(JsonElement root, string name, bool fallback)
            => root.TryGetProperty(name, out JsonElement el) && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
                ? el.GetBoolean()
                : fallback;

        public static string ReadString(JsonElement root, string name, string fallback)
            => root.TryGetProperty(name, out JsonElement el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : fallback;

        public static Vector3 ReadVector3(JsonElement root, string name, Vector3 fallback)
        {
            if (!root.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.Object)
                return fallback;
            return new Vector3(
                ReadFloat(el, "x", fallback.X),
                ReadFloat(el, "y", fallback.Y),
                ReadFloat(el, "z", fallback.Z));
        }

        public static TEnum ReadEnum<TEnum>(JsonElement root, string name, TEnum fallback) where TEnum : struct, Enum
        {
            if (!root.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.String)
                return fallback;
            return Enum.TryParse<TEnum>(el.GetString(), ignoreCase: true, out TEnum parsed) ? parsed : fallback;
        }
    }
}
