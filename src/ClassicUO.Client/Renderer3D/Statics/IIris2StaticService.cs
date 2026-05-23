// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Read-only registry of Iris 2 static-graphic GLB entries by ushort graphic ID.
    /// Loads <c>Data/iris2-static-registry.json</c> on first access and caches the table.
    /// </summary>
    /// <remarks>
    /// This service handles the registry-table state only. GPU model loading + caching
    /// (the legacy <c>EnsureModel</c>) stays in the legacy facade until
    /// <c>Multi3DRenderer</c> / <c>Static3DRenderer</c> migrate.
    /// </remarks>
    public interface IIris2StaticService
    {
        int LoadedEntryCount { get; }
        string LastError { get; }
        string RegistryPath { get; }
        string RepoRoot { get; }

        /// <summary>Force load on first access; idempotent.</summary>
        void EnsureLoaded();

        /// <summary>Reset internal state; next <see cref="EnsureLoaded"/> reloads.</summary>
        void Invalidate();

        /// <summary>Lookup entry by graphic ID. Returns false when not present.</summary>
        bool TryGet(ushort graphic, out Iris2StaticEntry entry);
    }
}
