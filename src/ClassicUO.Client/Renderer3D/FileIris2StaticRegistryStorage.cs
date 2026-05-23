// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IIris2StaticRegistryStorage backed by
// Data/iris2-static-registry.json. Walks up from AppContext.BaseDirectory looking for the
// repo root (a directory containing both Data/iris2-static-registry.json AND Data/iris2-glb/).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Statics;
// Both namespaces declare an Iris2StaticEntry struct during the transitional period.
// All references inside this file are fully qualified to ClassicUO.Renderer.Statics.Iris2StaticEntry.

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IIris2StaticRegistryStorage"/>. JSON shape preserved
    /// bit-for-bit from the legacy <c>Iris2StaticRegistry.EnsureLoaded</c> parser.
    /// </summary>
    internal sealed class FileIris2StaticRegistryStorage : IIris2StaticRegistryStorage
    {
        private const int MaxWalkUpHops = 12;

        public Iris2StaticRegistryLoadResult Load()
        {
            try
            {
                string path = ResolveRegistryPath(out string repoRoot);
                if (path == null || !File.Exists(path))
                {
                    return new Iris2StaticRegistryLoadResult(
                        success: false,
                        error: "iris2-static-registry.json not found",
                        registryPath: null, repoRoot: null, entries: null);
                }

                using FileStream fs = File.OpenRead(path);
                using JsonDocument doc = JsonDocument.Parse(fs);
                JsonElement root = doc.RootElement;
                if (!root.TryGetProperty("registry", out JsonElement registry) ||
                    registry.ValueKind != JsonValueKind.Object)
                {
                    return new Iris2StaticRegistryLoadResult(
                        success: false,
                        error: "missing 'registry' object in iris2-static-registry.json",
                        registryPath: path, repoRoot: repoRoot, entries: null);
                }

                Dictionary<ushort, ClassicUO.Renderer.Statics.Iris2StaticEntry> table = ParseEntries(registry);
                Console.WriteLine($"[3DCUO] Iris2StaticRegistry loaded: {table.Count} graphics from {path} (repoRoot={repoRoot})");
                return new Iris2StaticRegistryLoadResult(
                    success: true, error: null,
                    registryPath: path, repoRoot: repoRoot, entries: table);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] Iris2StaticRegistry load FAILED: {ex}");
                return new Iris2StaticRegistryLoadResult(
                    success: false, error: ex.Message,
                    registryPath: null, repoRoot: null, entries: null);
            }
        }

        private static Dictionary<ushort, ClassicUO.Renderer.Statics.Iris2StaticEntry> ParseEntries(JsonElement registry)
        {
            var table = new Dictionary<ushort, ClassicUO.Renderer.Statics.Iris2StaticEntry>(2048);
            foreach (JsonProperty prop in registry.EnumerateObject())
            {
                if (!TryParseHexKey(prop.Name, out ushort graphic)) continue;
                if (TryParseEntry(prop.Value, out ClassicUO.Renderer.Statics.Iris2StaticEntry entry))
                    table[graphic] = entry;
            }
            return table;
        }

        private static bool TryParseHexKey(string keyHex, out ushort graphic)
        {
            if (keyHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                keyHex = keyHex.Substring(2);
            return ushort.TryParse(keyHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out graphic);
        }

        private static bool TryParseEntry(JsonElement v, out ClassicUO.Renderer.Statics.Iris2StaticEntry entry)
        {
            entry = default;
            string glb = v.TryGetProperty("primary_glb", out JsonElement gEl) ? gEl.GetString() : null;
            if (string.IsNullOrEmpty(glb)) return false;

            string meshId = v.TryGetProperty("primary_mesh_id", out JsonElement mEl) ? mEl.GetString() : null;
            string name = v.TryGetProperty("static_name", out JsonElement nEl) ? nEl.GetString() : null;
            int th = 0;
            if (v.TryGetProperty("static_dimensions", out JsonElement dimEl) &&
                dimEl.ValueKind == JsonValueKind.Object &&
                dimEl.TryGetProperty("tile_height", out JsonElement thEl) &&
                thEl.ValueKind == JsonValueKind.Number)
            {
                th = thEl.GetInt32();
            }

            entry = new ClassicUO.Renderer.Statics.Iris2StaticEntry
            {
                GlbRelative = glb,
                MeshId = meshId ?? "",
                StaticName = name ?? "",
                TileHeight = th,
            };
            return true;
        }

        // Walk up from AppContext.BaseDirectory until we find a directory that contains
        // BOTH Data/iris2-static-registry.json AND Data/iris2-glb/. Both are required so
        // we pin the repo root, not the build-output dir (which has the JSON but no GLBs).
        private static string ResolveRegistryPath(out string repoRoot)
        {
            repoRoot = null;
            string baseDir = AppContext.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(baseDir);

            for (int hops = 0; hops < MaxWalkUpHops && dir != null; hops++, dir = dir.Parent)
            {
                string regCand = Path.Combine(dir.FullName, "Data", "iris2-static-registry.json");
                string glbDir = Path.Combine(dir.FullName, "Data", "iris2-glb");
                if (File.Exists(regCand) && Directory.Exists(glbDir))
                {
                    repoRoot = dir.FullName;
                    return regCand;
                }
            }

            // Fallback: registry-only (no GLBs). Loading meshes will fail but the table
            // is still useful for diagnostics.
            string copied = Path.Combine(baseDir, "Data", "iris2-static-registry.json");
            if (File.Exists(copied))
            {
                repoRoot = baseDir;
                return copied;
            }
            return null;
        }
    }
}
