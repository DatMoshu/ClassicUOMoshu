// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter for IRoofRegistryStorage. JSON shape preserved
// bit-for-bit from the legacy RoofMeshRegistry.EnsureTagsLoaded / EnsureManifestLoaded
// parsers (session 65 hybrid migration).

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClassicUO.Renderer.Statics;
// All RoofTileTag / RoofMeshEntry references are fully qualified to the domain types —
// using-aliases don't bind across an enclosing namespace member of the same name.

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IRoofRegistryStorage"/>. Loads both data sources:
    /// the tag table from <c>Data/3D/multi-tile-roof-archetypes.auto.json</c> (relative to
    /// the build output) and the family|canonical mesh manifest from the external
    /// <see cref="ExternalMeshesRoot"/> tree (versioned via <c>latest.txt</c>).
    /// </summary>
    /// <remarks>
    /// The external meshes root, version override, and meshes sub-path mirror the legacy
    /// <c>RoofMeshRegistry</c> public-static defaults verbatim. Future Phase 4 work moves
    /// these into <c>renderer-3d-config.json</c>; for now they stay as raw fields.
    /// </remarks>
    internal sealed class FileRoofRegistryStorage : IRoofRegistryStorage
    {
        public string TagDataPath { get; set; } = "Data/3D/multi-tile-roof-archetypes.auto.json";
        public string ExternalMeshesRoot { get; set; } = @"D:\UOMassExport\Roof\meshes";
        public string VersionOverride { get; set; }
        public string MeshesSubPath { get; set; } = "Roof/meshes";

        public RoofRegistryLoadResult Load()
        {
            (var tags, string tagErr, string tagPath) = LoadTags();
            (var entries, string manErr, string meshDir, string version) = LoadManifest();

            return new RoofRegistryLoadResult(
                tagsSuccess: tagErr is null,
                manifestSuccess: manErr is null,
                tagLoadError: tagErr,
                manifestLoadError: manErr,
                tagDataPath: tagPath,
                meshesDir: meshDir,
                resolvedVersion: version,
                tagsByGraphic: tags,
                entriesByFamilyMesh: entries);
        }

        private (IReadOnlyDictionary<ushort, ClassicUO.Renderer.Statics.RoofTileTag> table, string error, string path) LoadTags()
        {
            var table = new Dictionary<ushort, ClassicUO.Renderer.Statics.RoofTileTag>(512);
            string path = Path.Combine(AppContext.BaseDirectory, TagDataPath);
            if (!File.Exists(path))
            {
                string err = $"tag table missing: {path}";
                Console.WriteLine($"[3DCUO] RoofRegistry: {err}");
                return (table, err, path);
            }
            try
            {
                using FileStream fs = File.OpenRead(path);
                using JsonDocument doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("tiles", out JsonElement tiles) &&
                    tiles.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty entry in tiles.EnumerateObject())
                    {
                        if (!TryParseGraphic(entry.Name, out ushort gid)) continue;
                        JsonElement v = entry.Value;
                        if (v.ValueKind != JsonValueKind.Object) continue;
                        string family = v.TryGetProperty("family", out JsonElement fEl) ? fEl.GetString() : null;
                        string famName = v.TryGetProperty("family_name", out JsonElement fnEl) ? fnEl.GetString() : null;
                        string aStr = v.TryGetProperty("archetype", out JsonElement aEl) ? aEl.GetString() : null;
                        if (string.IsNullOrEmpty(family) || string.IsNullOrEmpty(aStr)) continue;
                        if (!Enum.TryParse(aStr, ignoreCase: true, out RoofArchetype archetype)) continue;
                        table[gid] = new ClassicUO.Renderer.Statics.RoofTileTag(family, famName, archetype);
                    }
                }
                Console.WriteLine($"[3DCUO] RoofRegistry tags loaded: count={table.Count}");
                return (table, null, path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] RoofRegistry tag load FAILED: {ex}");
                return (table, ex.Message, path);
            }
        }

        private (IReadOnlyDictionary<string, ClassicUO.Renderer.Statics.RoofMeshEntry> table, string error, string meshDir, string version) LoadManifest()
        {
            var table = new Dictionary<string, ClassicUO.Renderer.Statics.RoofMeshEntry>(64);
            string meshesDir = ResolveMeshesDir(out string version);
            if (meshesDir == null)
            {
                string err = $"roof meshes dir not found ({MeshesSubPath})";
                Console.WriteLine($"[3DCUO] RoofRegistry: {err} -- runtime will fall back to flat-quad path");
                return (table, err, null, null);
            }
            string manifestPath = Path.Combine(meshesDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                string err = $"manifest.json missing under {meshesDir}";
                Console.WriteLine($"[3DCUO] RoofRegistry: {err}");
                return (table, err, meshesDir, version);
            }
            try
            {
                using FileStream fs = File.OpenRead(manifestPath);
                using JsonDocument doc = JsonDocument.Parse(fs);
                if (doc.RootElement.TryGetProperty("families", out JsonElement families) &&
                    families.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement fam in families.EnumerateArray())
                    {
                        string familyKey = fam.TryGetProperty("family", out JsonElement fkEl) ? fkEl.GetString() : null;
                        string atlas = fam.TryGetProperty("atlas", out JsonElement atEl) ? atEl.GetString() : null;
                        if (string.IsNullOrEmpty(familyKey)) continue;
                        if (!fam.TryGetProperty("meshes", out JsonElement meshes) ||
                            meshes.ValueKind != JsonValueKind.Object) continue;
                        foreach (JsonProperty m in meshes.EnumerateObject())
                        {
                            string canonical = m.Name;
                            string file = m.Value.ValueKind == JsonValueKind.String ? m.Value.GetString() : null;
                            if (string.IsNullOrEmpty(file)) continue;
                            table[$"{familyKey}|{canonical}"] = new ClassicUO.Renderer.Statics.RoofMeshEntry(file, atlas);
                        }
                    }
                }
                Console.WriteLine($"[3DCUO] RoofRegistry manifest loaded: count={table.Count} from {manifestPath}");
                return (table, null, meshesDir, version);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] RoofRegistry manifest load FAILED: {ex}");
                return (table, ex.Message, meshesDir, version);
            }
        }

        private string ResolveMeshesDir(out string resolvedVersion)
        {
            resolvedVersion = null;
            if (!string.IsNullOrEmpty(ExternalMeshesRoot) && Directory.Exists(ExternalMeshesRoot))
            {
                string version = VersionOverride;
                if (string.IsNullOrEmpty(version))
                {
                    string latestPath = Path.Combine(ExternalMeshesRoot, "latest.txt");
                    if (File.Exists(latestPath))
                    {
                        try { version = File.ReadAllText(latestPath).Trim(); } catch { version = null; }
                    }
                }
                if (!string.IsNullOrEmpty(version))
                {
                    string ext = Path.Combine(ExternalMeshesRoot, version);
                    if (Directory.Exists(ext) && File.Exists(Path.Combine(ext, "manifest.json")))
                    {
                        resolvedVersion = version;
                        return Path.GetFullPath(ext);
                    }
                }
                if (File.Exists(Path.Combine(ExternalMeshesRoot, "manifest.json")))
                {
                    resolvedVersion = "(root)";
                    return Path.GetFullPath(ExternalMeshesRoot);
                }
            }

            string baseDir = AppContext.BaseDirectory;
            string sideloaded = Path.Combine(baseDir, "Data", MeshesSubPath);
            if (Directory.Exists(sideloaded))
            {
                resolvedVersion = "(repo) " + MeshesSubPath;
                return Path.GetFullPath(sideloaded);
            }
            DirectoryInfo dir = new DirectoryInfo(baseDir);
            for (int hops = 0; hops < 10 && dir != null; hops++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Data", MeshesSubPath);
                if (Directory.Exists(candidate))
                {
                    resolvedVersion = "(repo) " + MeshesSubPath;
                    return Path.GetFullPath(candidate);
                }
            }
            return null;
        }

        private static bool TryParseGraphic(string s, out ushort id)
        {
            id = 0;
            if (string.IsNullOrEmpty(s)) return false;
            string t = s.Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(
                    t.AsSpan(2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out id);
            }
            return ushort.TryParse(t, out id);
        }
    }
}
