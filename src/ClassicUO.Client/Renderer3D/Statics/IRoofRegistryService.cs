// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Read-only registry of roof-tile tags and the family|canonical -> mesh manifest.
    /// Loads on first access; manifest absence is non-fatal (renderer falls back to the
    /// legacy flat-quad EmitFloor path).
    /// </summary>
    /// <remarks>
    /// This service handles registry-table state only. GPU model + atlas caching stays in
    /// the legacy <c>RoofMeshRegistry</c> facade until <c>Multi3DRenderer</c> migrates —
    /// same hybrid pattern as session-19 <c>IIris2StaticService</c>.
    /// </remarks>
    internal interface IRoofRegistryService
    {
        int LoadedTagCount { get; }
        int LoadedManifestCount { get; }
        string TagLoadError { get; }
        string ManifestLoadError { get; }
        string TagDataPath { get; }
        string MeshesDir { get; }
        string ResolvedVersion { get; }

        /// <summary>Force load on first access; idempotent.</summary>
        void EnsureLoaded();

        /// <summary>Reset internal state; next <see cref="EnsureLoaded"/> reloads.</summary>
        void Invalidate();

        /// <summary>
        /// Look up the tag and manifest entry for a roof graphic. Returns false when the
        /// graphic has no tag entry, OR the tag is present but the manifest has no GLB for
        /// the resolved family|canonical pair (e.g. atlas pipeline hasn't baked yet).
        /// </summary>
        bool TryResolve(ushort graphic, out RoofTileTag tag, out RoofMeshEntry entry);
    }
}
