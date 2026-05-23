// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL HYBRID FACADE (ADR-012 §6).
//
// Registry-table state delegates to IRoofRegistryService via Renderer3DHost.
// GPU caches (model + atlas) stay local because SkinnedModelGlb + Texture2D are owned
// by the legacy 3D rendering path; they migrate with Multi3DRenderer / Static3DRenderer.
//
// Mirrors the session-19 Iris2StaticRegistry hybrid migration pattern.

using System;
using System.Collections.Generic;
using System.IO;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;
using Microsoft.Xna.Framework.Graphics;
using DomainTileTag = ClassicUO.Renderer.Statics.RoofTileTag;
using DomainMeshEntry = ClassicUO.Renderer.Statics.RoofMeshEntry;

#nullable disable

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Legacy tag-record alias. The legacy struct fields stay live for source-compat
    /// with <c>Multi3DRenderer</c>'s <c>roofTag.Family / Archetype</c> reads; values
    /// originate from the new domain struct via <see cref="TryResolve"/>.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Statics.RoofTileTag. Will be removed in ADR-012 Phase 3.")]
    internal struct RoofTileTag
    {
        public string Family;
        public string FamilyName;
        public RoofArchetype Archetype;
    }

    /// <summary>
    /// Legacy mesh-entry alias. Same purpose as <see cref="RoofTileTag"/>.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Statics.RoofMeshEntry. Will be removed in ADR-012 Phase 3.")]
    internal struct RoofMeshEntry
    {
        public string MeshFile;
        public string AtlasFile;
    }

    [Obsolete("Use IRoofRegistryService via Renderer3DServices for data lookups; GPU caches stay here until Multi3DRenderer migrates. Will be removed in ADR-012 Phase 3.")]
    internal static class RoofMeshRegistry
    {
        private static IRoofRegistryService Svc => Renderer3DHost.Services.RoofRegistry;

        // ===== Legacy diagnostic surface (delegated) =====

        public static int LoadedTagCount => Svc.LoadedTagCount;
        public static int LoadedManifestCount => Svc.LoadedManifestCount;
        public static string TagLoadError => Svc.TagLoadError;
        public static string ManifestLoadError => Svc.ManifestLoadError;
        public static string ResolvedVersion => Svc.ResolvedVersion;
        public static string MeshesDir => Svc.MeshesDir;

        public static int CacheCount => _meshCache.Count;
        public static int FailedCount => _failed.Count;

        // Read-only legacy config knobs preserved so external diagnostic gumps that grep
        // for these names still compile. Writes are no-ops; the production-side defaults
        // live in FileRoofRegistryStorage (instance fields) per ADR-012 §3 data-driven goal.
        public static string TagDataPath => Svc.TagDataPath;
        public static string ExternalMeshesRoot => null; // configured on FileRoofRegistryStorage
        public static string VersionOverride => null;
        public static string MeshesSubPath => "Roof/meshes";

        // ===== Registry-table API (delegated) =====

        public static void EnsureLoaded() => Svc.EnsureLoaded();

        public static bool TryResolve(ushort graphic, out RoofTileTag tag, out RoofMeshEntry entry)
        {
            tag = default;
            entry = default;
            if (!Svc.TryResolve(graphic, out DomainTileTag domainTag, out DomainMeshEntry domainEntry))
                return false;

            tag = new RoofTileTag
            {
                Family = domainTag.Family,
                FamilyName = domainTag.FamilyName,
                Archetype = domainTag.Archetype,
            };
            entry = new RoofMeshEntry
            {
                MeshFile = domainEntry.MeshFile,
                AtlasFile = domainEntry.AtlasFile,
            };
            return true;
        }

        // ===== GPU caches (stay in facade until Multi3DRenderer migrates) =====

        private static readonly Dictionary<string, SkinnedModelGlb> _meshCache = new();
        private static readonly Dictionary<string, Texture2D> _atlasCache = new();
        private static readonly HashSet<string> _failed = new();

        public static SkinnedModelGlb EnsureModel(GraphicsDevice gd, string meshFile, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(meshFile)) { error = "empty mesh file"; return null; }
            if (_meshCache.TryGetValue(meshFile, out SkinnedModelGlb cached) && cached != null) return cached;
            if (_failed.Contains(meshFile)) { error = "previous load failed"; return null; }

            string meshesDir = Svc.MeshesDir;
            string full = Path.Combine(meshesDir ?? "", meshFile);
            if (!File.Exists(full)) { error = $"glb missing: {full}"; _failed.Add(meshFile); return null; }
            try
            {
                SkinnedModelGlb loaded = SkinnedModelGlb.Load(gd, full);
                loaded.ResetToBindPose();
                _meshCache[meshFile] = loaded;
                Console.WriteLine($"[3DCUO] RoofMesh loaded {meshFile}: submeshes={loaded.Submeshes.Count}");
                return loaded;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _failed.Add(meshFile);
                Console.WriteLine($"[3DCUO] RoofMesh load FAILED for {meshFile}: {ex}");
                return null;
            }
        }

        public static Texture2D EnsureAtlas(GraphicsDevice gd, string atlasFile)
        {
            if (string.IsNullOrEmpty(atlasFile)) return null;
            if (_atlasCache.TryGetValue(atlasFile, out Texture2D cached) && cached != null) return cached;
            string meshesDir = Svc.MeshesDir;
            string full = Path.Combine(meshesDir ?? "", atlasFile);
            if (!File.Exists(full)) return null;
            try
            {
                using FileStream fs = File.OpenRead(full);
                Texture2D tex = Texture2D.FromStream(gd, fs);
                _atlasCache[atlasFile] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] RoofMesh atlas load FAILED for {atlasFile}: {ex}");
                return null;
            }
        }

        public static void Invalidate()
        {
            foreach (SkinnedModelGlb v in _meshCache.Values) v?.Dispose();
            foreach (Texture2D t in _atlasCache.Values) t?.Dispose();
            _meshCache.Clear();
            _atlasCache.Clear();
            _failed.Clear();
            Svc.Invalidate();
        }
    }
}
