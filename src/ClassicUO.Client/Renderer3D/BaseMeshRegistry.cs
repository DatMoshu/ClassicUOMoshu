// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — base mesh registry.
// Loads Data/base-mesh-models.json and resolves (species, slot) -> GLB path
// for the unclothed base body parts. Used as the fallback when an attachment
// slot has no equipment selected, mirroring Sidekick's "_BASE_" preference.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class BaseMeshRegistry
    {
        // species -> (slotDir -> relpath under Models/)
        private static Dictionary<string, Dictionary<string, string>> _bySpecies;
        private static string[] _speciesList = Array.Empty<string>();
        private static bool _loaded;
        public static string LastError;

        // (species|slotDir) -> loaded model. Owned here, separate from AttachmentRenderer's per-index cache.
        private static readonly Dictionary<string, SkinnedModelGlb> _cache = new();
        private static readonly HashSet<string> _failed = new();

        public static string[] AvailableSpecies => _speciesList;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                string path = ResolveJsonPath();
                if (path == null) { LastError = "base-mesh-models.json not found"; return; }
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                _bySpecies = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                if (root.TryGetProperty("species", out var sp))
                {
                    foreach (var speciesProp in sp.EnumerateObject())
                    {
                        var slotMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var slotProp in speciesProp.Value.EnumerateObject())
                            slotMap[slotProp.Name] = slotProp.Value.GetString();
                        _bySpecies[speciesProp.Name] = slotMap;
                    }
                }
                var list = new List<string>(_bySpecies.Keys);
                list.Sort(StringComparer.OrdinalIgnoreCase);
                _speciesList = list.ToArray();
                Console.WriteLine($"[3DCUO] BaseMeshRegistry loaded: species={_speciesList.Length} from {path}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO] BaseMeshRegistry load FAILED: {ex}");
            }
        }

        private static string ResolveJsonPath()
        {
            // Try several locations: build-output mirror, then repo Data folder.
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "Data", "base-mesh-models.json"),
                Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "Data", "base-mesh-models.json"),
                Path.Combine(baseDir, "..", "..", "..", "..", "Data", "base-mesh-models.json"),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return Path.GetFullPath(c);
            }
            return null;
        }

        public static bool TryGetRelativePath(string species, string slotDir, out string relPath)
        {
            EnsureLoaded();
            relPath = null;
            if (_bySpecies == null) return false;
            return _bySpecies.TryGetValue(species, out var slots)
                && slots.TryGetValue(slotDir, out relPath)
                && !string.IsNullOrEmpty(relPath);
        }

        public static SkinnedModelGlb EnsureModel(GraphicsDevice gd, string species, string slotDir, out string error)
        {
            error = null;
            string key = species + "|" + slotDir;
            if (_cache.TryGetValue(key, out var m) && m != null) return m;
            if (_failed.Contains(key)) { error = "previous load failed"; return null; }

            if (!TryGetRelativePath(species, slotDir, out var rel))
            {
                error = $"no base mesh for ({species}, {slotDir})";
                _failed.Add(key);
                return null;
            }
            string full = Path.Combine(AppContext.BaseDirectory, "Models", rel);
            if (!File.Exists(full))
            {
                error = $"glb missing: {full}";
                _failed.Add(key);
                return null;
            }
            try
            {
                var loaded = SkinnedModelGlb.Load(gd, full);
                loaded.ResetToBindPose();
                _cache[key] = loaded;
                Console.WriteLine($"[3DCUO] BaseMesh loaded [{species}/{slotDir}] {rel}: submeshes={loaded.Submeshes.Count}");
                return loaded;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _failed.Add(key);
                Console.WriteLine($"[3DCUO] BaseMesh load FAILED for {rel}: {ex}");
                return null;
            }
        }

        public static void Invalidate()
        {
            foreach (var m in _cache.Values) m?.Dispose();
            _cache.Clear();
            _failed.Clear();
        }
    }
}
