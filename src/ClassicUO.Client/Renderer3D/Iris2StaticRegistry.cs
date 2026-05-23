// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL HYBRID FACADE (ADR-012 §6).
//
// Registry-table state is delegated to IIris2StaticService via Renderer3DHost.
// GPU model caching (EnsureModel + Invalidate) stays local because SkinnedModelGlb is
// owned by the legacy 3D rendering path; it migrates with Multi3DRenderer / Static3DRenderer.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;
using Microsoft.Xna.Framework.Graphics;
using LegacyEntry = ClassicUO.Renderer.Statics.Iris2StaticEntry;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Legacy entry-record type aliased to the new domain type. Existing call sites that
    /// use <c>var</c> continue to work; explicit <c>Iris2StaticEntry</c> references resolve
    /// to the same struct.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Statics.Iris2StaticEntry. Will be removed in ADR-012 Phase 3.")]
    internal struct Iris2StaticEntry
    {
        public string GlbRelative;
        public string MeshId;
        public string StaticName;
        public int TileHeight;
    }

    [Obsolete("Use IIris2StaticService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class Iris2StaticRegistry
    {
        private static IIris2StaticService Svc => Renderer3DHost.Services.Iris2Static;

        // ===== Legacy diagnostic surface (delegated) =====

        public static string LastError => Svc.LastError;
        public static int LoadedEntryCount => Svc.LoadedEntryCount;
        public static string ResolvedRegistryPath => Svc.RegistryPath;
        public static string ResolvedRepoRoot => Svc.RepoRoot;

        public static int CacheCount => _cache.Count;
        public static int FailedCount => _failed.Count;

        // ===== Registry-table API (delegated) =====

        public static void EnsureLoaded() => Svc.EnsureLoaded();

        public static bool TryGet(ushort graphic, out Iris2StaticEntry entry)
        {
            if (Svc.TryGet(graphic, out LegacyEntry domainEntry))
            {
                entry = new Iris2StaticEntry
                {
                    GlbRelative = domainEntry.GlbRelative,
                    MeshId = domainEntry.MeshId,
                    StaticName = domainEntry.StaticName,
                    TileHeight = domainEntry.TileHeight,
                };
                return true;
            }
            entry = default;
            return false;
        }

        // ===== GPU model cache (stays in facade until Multi3D/Static3D migrate) =====

        private static readonly Dictionary<ushort, SkinnedModelGlb> _cache = new();
        private static readonly HashSet<ushort> _failed = new();

        public static SkinnedModelGlb EnsureModel(GraphicsDevice gd, ushort graphic, out string error)
        {
            error = null;
            Svc.EnsureLoaded();

            if (_cache.TryGetValue(graphic, out SkinnedModelGlb m) && m != null) return m;
            if (_failed.Contains(graphic)) { error = "previous load failed"; return null; }

            if (!Svc.TryGet(graphic, out LegacyEntry entry))
            {
                error = $"no iris2 entry for 0x{graphic:X4}";
                _failed.Add(graphic);
                return null;
            }

            string full = ResolveGlbPath(entry.GlbRelative, Svc.RepoRoot);
            if (full == null || !File.Exists(full))
            {
                error = $"glb missing: {entry.GlbRelative}";
                _failed.Add(graphic);
                return null;
            }

            try
            {
                SkinnedModelGlb loaded = SkinnedModelGlb.Load(gd, full);
                loaded.ResetToBindPose();
                _cache[graphic] = loaded;
                Console.WriteLine($"[3DCUO] Iris2 mesh loaded [0x{graphic:X4}] {entry.MeshId} ({entry.StaticName}): submeshes={loaded.Submeshes.Count}");
                return loaded;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _failed.Add(graphic);
                Console.WriteLine($"[3DCUO] Iris2 mesh load FAILED for {entry.GlbRelative}: {ex}");
                return null;
            }
        }

        public static void Invalidate()
        {
            foreach (SkinnedModelGlb v in _cache.Values) v?.Dispose();
            _cache.Clear();
            _failed.Clear();
            Svc.Invalidate();
        }

        private static string ResolveGlbPath(string relative, string repoRoot)
        {
            if (string.IsNullOrEmpty(relative) || string.IsNullOrEmpty(repoRoot)) return null;
            string norm = relative.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(repoRoot, norm));
        }
    }
}
