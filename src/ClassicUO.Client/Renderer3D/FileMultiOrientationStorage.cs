// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Loads the hand-authored + auto-derived orientation tables from JSON and merges
// them, hand-authored entries overriding auto entries on collision.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class FileMultiOrientationStorage : IMultiOrientationStorage
    {
        public const string DefaultHandPath = "Data/3D/multi-tile-orientations.json";
        public const string DefaultAutoPath = "Data/3D/multi-tile-orientations.auto.json";

        private readonly string _handPath;
        private readonly string _autoPath;

        public FileMultiOrientationStorage()
            : this(DefaultHandPath, DefaultAutoPath) { }

        public FileMultiOrientationStorage(string handPath, string autoPath)
        {
            _handPath = handPath;
            _autoPath = autoPath;
        }

        public MultiOrientationLoadResult Load()
        {
            var table = new Dictionary<ushort, WallTileOrientation>(4096);
            string lastError = null;

            // Auto first so the hand-authored table can override on top.
            int autoCount = LoadInto(_autoPath, table, "auto", ref lastError);
            int handCount = LoadInto(_handPath, table, "hand", ref lastError);

            int merged = table.Count;
            Console.WriteLine(
                $"[3DCUO] MultiOrientationTable: hand={handCount} + auto={autoCount} -> {merged} merged entries");

            return new MultiOrientationLoadResult(
                success: lastError == null,
                errorMessage: lastError,
                handCount: handCount,
                autoCount: autoCount,
                table: table);
        }

        private static int LoadInto(
            string relativePath,
            Dictionary<ushort, WallTileOrientation> table,
            string label,
            ref string lastError)
        {
            string path = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (!File.Exists(path))
            {
                if (label == "hand")
                {
                    lastError = $"orientation table not found at {path}";
                    Console.WriteLine($"[3DCUO] {lastError}");
                }
                return 0;
            }
            try
            {
                using var fs = File.OpenRead(path);
                using var doc = JsonDocument.Parse(fs);
                int loaded = 0;
                if (doc.RootElement.TryGetProperty("tiles", out JsonElement tilesEl) &&
                    tilesEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty entry in tilesEl.EnumerateObject())
                    {
                        if (!TryParseGraphic(entry.Name, out ushort id))
                            continue;
                        if (entry.Value.ValueKind != JsonValueKind.String)
                            continue;
                        if (!Enum.TryParse(entry.Value.GetString(), ignoreCase: true,
                                           out WallTileOrientation kind))
                            continue;
                        table[id] = kind; // last-write-wins; we order calls in Load().
                        loaded++;
                    }
                }
                return loaded;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Console.WriteLine($"[3DCUO] MultiOrientationTable {label} load failed: {ex.Message}");
                return 0;
            }
        }

        private static bool TryParseGraphic(string s, out ushort id)
        {
            id = 0;
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(t.AsSpan(2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out id);
            }
            return ushort.TryParse(t, out id);
        }
    }
}
