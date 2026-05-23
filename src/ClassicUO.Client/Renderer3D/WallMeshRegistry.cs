// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — wall-piece GLB registry.
// Loads Data/Wallpieces/meshes/<stamp>/manifest.json and resolves
//   ushort graphic -> { glb path, archetype }
// for the wall-set produced by the Blender pipeline (UO Mass Export → wallpieces).
// Mirrors BaseMeshRegistry; reuses SkinnedModelGlb.Load for non-skinned meshes.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    internal struct WallMeshEntry
    {
        public string File;        // "wall_0087.glb"
        public string Archetype;   // "wall_NS" | "wall_EW" | "corner" | "pillar"
    }

    internal static class WallMeshRegistry
    {
        // Resolution order:
        //   1. ExternalMeshesRoot/<latest.txt-version>/   ← Blender pipeline output
        //   2. ExternalMeshesRoot/<version>/              ← if VersionOverride set
        //   3. Data/Wallpieces/meshes/<MeshesSubPath>/    ← repo-local fallback
        // External root is checked first so the running client always pulls the
        // freshest export without copying 3000+ GLBs into the repo.
        public static string ExternalMeshesRoot = @"D:\UOMassExport\Wall\Wallpieces\meshes";
        public static string VersionOverride = null;          // e.g. "2026-05-02_221419"
        public static string MeshesSubPath = "Wallpieces/meshes/2026-05-02_150440";

        public static string LastError;
        public static int LoadedEntryCount;
        public static string ResolvedVersion; // for diagnostics

        private static Dictionary<ushort, WallMeshEntry> _byGraphic;
        private static string _meshesDir;
        private static bool _loaded;

        // Cache of loaded models keyed by graphic.
        private static readonly Dictionary<ushort, SkinnedModelGlb> _cache = new();
        private static readonly HashSet<ushort> _failed = new();

        public static int CacheCount => _cache.Count;
        public static int FailedCount => _failed.Count;
        public static string MeshesDir => _meshesDir;

        public struct EntryStatus
        {
            public ushort Graphic;
            public string File;
            public string Archetype;
            public bool IsCached;
            public bool HasFailed;
            public string FullPath;
            public bool FileExists;
        }

        public static IEnumerable<EntryStatus> AllEntryStatuses()
        {
            EnsureLoaded();
            if (_byGraphic == null) yield break;
            foreach (var kv in _byGraphic)
            {
                string full = _meshesDir != null ? Path.Combine(_meshesDir, kv.Value.File) : null;
                yield return new EntryStatus
                {
                    Graphic = kv.Key,
                    File = kv.Value.File,
                    Archetype = kv.Value.Archetype,
                    IsCached = _cache.ContainsKey(kv.Key),
                    HasFailed = _failed.Contains(kv.Key),
                    FullPath = full,
                    FileExists = full != null && File.Exists(full),
                };
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                _meshesDir = ResolveMeshesDir();
                if (_meshesDir == null)
                {
                    LastError = $"wallpieces meshes dir not found ({MeshesSubPath})";
                    Console.WriteLine($"[3DCUO] WallMeshRegistry: {LastError}");
                    _byGraphic = new Dictionary<ushort, WallMeshEntry>();
                    return;
                }

                string manifestPath = Path.Combine(_meshesDir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    LastError = $"manifest.json missing under {_meshesDir}";
                    Console.WriteLine($"[3DCUO] WallMeshRegistry: {LastError}");
                    _byGraphic = new Dictionary<ushort, WallMeshEntry>();
                    return;
                }

                var json = File.ReadAllText(manifestPath);
                using var doc = JsonDocument.Parse(json);

                var table = new Dictionary<ushort, WallMeshEntry>(128);
                if (doc.RootElement.TryGetProperty("meshes", out var meshes) &&
                    meshes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in meshes.EnumerateArray())
                    {
                        if (!m.TryGetProperty("sprite_id", out var sidEl)) continue;
                        var sid = sidEl.GetString();
                        if (string.IsNullOrEmpty(sid)) continue;

                        // sprite_id is hex graphic ID, zero-padded to 4 chars
                        // (e.g. "0087" → 0x0087 → matches MultiOrientationTable).
                        if (!ushort.TryParse(sid, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var graphic))
                            continue;

                        var entry = new WallMeshEntry
                        {
                            File = m.TryGetProperty("file", out var fEl) ? fEl.GetString() : null,
                            Archetype = m.TryGetProperty("archetype", out var aEl) ? aEl.GetString() : null,
                        };
                        if (string.IsNullOrEmpty(entry.File)) continue;

                        table[graphic] = entry;
                    }
                }

                _byGraphic = table;
                LoadedEntryCount = table.Count;
                Console.WriteLine($"[3DCUO] WallMeshRegistry loaded: count={LoadedEntryCount} from {manifestPath}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine($"[3DCUO] WallMeshRegistry load FAILED: {ex}");
                _byGraphic = new Dictionary<ushort, WallMeshEntry>();
            }
        }

        public static bool TryGet(ushort graphic, out WallMeshEntry entry)
        {
            EnsureLoaded();
            entry = default;
            return _byGraphic != null && _byGraphic.TryGetValue(graphic, out entry);
        }

        public static SkinnedModelGlb EnsureModel(GraphicsDevice gd, ushort graphic, out string error)
        {
            error = null;
            EnsureLoaded();
            if (_cache.TryGetValue(graphic, out var m) && m != null) return m;
            if (_failed.Contains(graphic)) { error = "previous load failed"; return null; }

            if (_byGraphic == null || !_byGraphic.TryGetValue(graphic, out var entry))
            {
                error = $"no manifest entry for graphic 0x{graphic:X4}";
                _failed.Add(graphic);
                return null;
            }
            string full = Path.Combine(_meshesDir, entry.File);
            if (!File.Exists(full))
            {
                error = $"glb missing: {full}";
                _failed.Add(graphic);
                return null;
            }
            try
            {
                var loaded = SkinnedModelGlb.Load(gd, full);
                loaded.ResetToBindPose();
                _cache[graphic] = loaded;
                Console.WriteLine($"[3DCUO] WallMesh loaded [0x{graphic:X4}] {entry.File}: submeshes={loaded.Submeshes.Count}");
                return loaded;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _failed.Add(graphic);
                Console.WriteLine($"[3DCUO] WallMesh load FAILED for {entry.File}: {ex}");
                return null;
            }
        }

        public static void Invalidate()
        {
            foreach (var v in _cache.Values) v?.Dispose();
            _cache.Clear();
            _failed.Clear();
            _loaded = false;
            _byGraphic = null;
            _meshesDir = null;
        }

        private static string ResolveMeshesDir()
        {
            // 1) External Blender pipeline root (latest.txt or VersionOverride).
            if (!string.IsNullOrEmpty(ExternalMeshesRoot) && Directory.Exists(ExternalMeshesRoot))
            {
                string version = VersionOverride;
                if (string.IsNullOrEmpty(version))
                {
                    string latestPath = Path.Combine(ExternalMeshesRoot, "latest.txt");
                    if (File.Exists(latestPath))
                    {
                        try { version = File.ReadAllText(latestPath).Trim(); }
                        catch { version = null; }
                    }
                }
                if (!string.IsNullOrEmpty(version))
                {
                    string ext = Path.Combine(ExternalMeshesRoot, version);
                    if (Directory.Exists(ext) && File.Exists(Path.Combine(ext, "manifest.json")))
                    {
                        ResolvedVersion = version;
                        Console.WriteLine($"[3DCUO] WallMeshRegistry using external version '{version}' at {ext}");
                        return Path.GetFullPath(ext);
                    }
                }
            }

            // 2) Repo-local fallback: Data/<MeshesSubPath>.
            string baseDir = AppContext.BaseDirectory;
            string sideloaded = Path.Combine(baseDir, "Data", MeshesSubPath);
            if (Directory.Exists(sideloaded)) { ResolvedVersion = "(repo) " + MeshesSubPath; return Path.GetFullPath(sideloaded); }

            var dir = new DirectoryInfo(baseDir);
            for (int hops = 0; hops < 10 && dir != null; hops++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "Data", MeshesSubPath);
                if (Directory.Exists(candidate))
                { ResolvedVersion = "(repo) " + MeshesSubPath; return Path.GetFullPath(candidate); }
            }
            return null;
        }
    }
}
