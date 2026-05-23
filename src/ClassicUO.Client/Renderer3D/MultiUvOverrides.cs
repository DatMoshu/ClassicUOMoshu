// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — per-graphic UV / height overrides for multi rendering.
// Lets a human iterate visually on a single tile, save the result, and have
// the override applied for every instance of that graphic going forward.
//
// File format: JSON keyed by graphic ID (decimal). Stored alongside the
// binary so it ships with the build:
//   {bin}/Data/multi_uv_overrides.json

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class MultiUvOverride
    {
        public float OffsetU { get; set; } = 0f;       // shift in normalized atlas UV
        public float OffsetV { get; set; } = 0f;
        public float ScaleU  { get; set; } = 1f;       // 1.0 = full source rect
        public float ScaleV  { get; set; } = 1f;
        public bool  FlipU   { get; set; } = false;
        public bool  FlipV   { get; set; } = false;
        public float HeightScale  { get; set; } = 1f;  // wall height multiplier
        public float HeightOffset { get; set; } = 0f;  // additive height in pixels
        public bool  Visible { get; set; } = true;     // false = skip rendering this graphic

        public bool IsDefault =>
            OffsetU == 0 && OffsetV == 0 &&
            ScaleU == 1f && ScaleV == 1f &&
            !FlipU && !FlipV &&
            HeightScale == 1f && HeightOffset == 0f &&
            Visible;
    }

    internal static class MultiUvOverrides
    {
        private static readonly Dictionary<ushort, MultiUvOverride> _overrides = new();
        private static bool _loaded;
        public static string LastError;

        private static string ResolvePath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Data", "multi_uv_overrides.json");
        }

        public static IReadOnlyDictionary<ushort, MultiUvOverride> All
        {
            get { EnsureLoaded(); return _overrides; }
        }

        public static MultiUvOverride Get(ushort graphic)
        {
            EnsureLoaded();
            _overrides.TryGetValue(graphic, out var o);
            return o;
        }

        public static MultiUvOverride GetOrCreate(ushort graphic)
        {
            EnsureLoaded();
            if (!_overrides.TryGetValue(graphic, out var o))
            {
                o = new MultiUvOverride();
                _overrides[graphic] = o;
            }
            return o;
        }

        public static void Remove(ushort graphic)
        {
            EnsureLoaded();
            _overrides.Remove(graphic);
        }

        // Apply the override to the resolved UV/height. Caller passes ref values.
        // If no override exists or the override is the identity, nothing changes.
        public static bool Apply(ushort graphic,
            ref float uvX, ref float uvY, ref float uvW, ref float uvH,
            ref float height)
        {
            EnsureLoaded();
            if (!_overrides.TryGetValue(graphic, out var o)) return false;

            if (!o.Visible) return false;     // caller treats false-return as "skip if !visible"

            // Apply scale + offset against the inner UV rect.
            float oldW = uvW, oldH = uvH;
            uvW = oldW * o.ScaleU;
            uvH = oldH * o.ScaleV;
            // Center-anchor scale, then add offset.
            uvX += (oldW - uvW) * 0.5f + o.OffsetU * oldW;
            uvY += (oldH - uvH) * 0.5f + o.OffsetV * oldH;

            if (o.FlipU) { uvX += uvW; uvW = -uvW; }
            if (o.FlipV) { uvY += uvH; uvH = -uvH; }

            height = height * o.HeightScale + o.HeightOffset;
            return true;
        }

        public static bool ShouldDraw(ushort graphic)
        {
            EnsureLoaded();
            if (_overrides.TryGetValue(graphic, out var o)) return o.Visible;
            return true;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            Load();
        }

        public static void Load()
        {
            try
            {
                string path = ResolvePath();
                if (!File.Exists(path)) { _overrides.Clear(); return; }
                var json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, MultiUvOverride>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _overrides.Clear();
                if (dict != null)
                {
                    foreach (var (k, v) in dict)
                    {
                        if (ushort.TryParse(k, out var g))
                            _overrides[g] = v;
                        else if (k.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                                 ushort.TryParse(k.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var g2))
                            _overrides[g2] = v;
                    }
                }
                LastError = null;
                Console.WriteLine($"[3DCUO][uv] loaded {_overrides.Count} overrides from {path}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO][uv] load failed: {ex}");
            }
        }

        public static bool Save()
        {
            try
            {
                string path = ResolvePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                // Output keys as hex for readability; filter out identity overrides.
                var dict = new Dictionary<string, MultiUvOverride>();
                foreach (var (g, o) in _overrides)
                {
                    if (o.IsDefault) continue;
                    dict[$"0x{g:X4}"] = o;
                }
                var json = JsonSerializer.Serialize(dict,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                Console.WriteLine($"[3DCUO][uv] saved {dict.Count} overrides to {path}");
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO][uv] save failed: {ex}");
                return false;
            }
        }
    }
}
