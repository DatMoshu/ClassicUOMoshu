// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Loaded Iris 2 registry payload: the entries by graphic ID, the repository root path
    /// (used to resolve <see cref="Iris2StaticEntry.GlbRelative"/> later), and the registry
    /// JSON's resolved file path (diagnostic).
    /// </summary>
    public readonly struct Iris2StaticRegistryLoadResult
    {
        public readonly bool Success;
        public readonly string ErrorMessage;
        public readonly string RegistryPath;
        public readonly string RepoRoot;
        public readonly IReadOnlyDictionary<ushort, Iris2StaticEntry> Entries;

        public Iris2StaticRegistryLoadResult(
            bool success, string error, string registryPath, string repoRoot,
            IReadOnlyDictionary<ushort, Iris2StaticEntry> entries)
        {
            Success = success;
            ErrorMessage = error;
            RegistryPath = registryPath;
            RepoRoot = repoRoot;
            Entries = entries;
        }
    }

    /// <summary>
    /// Persistence abstraction for <see cref="Iris2StaticService"/>. Production-side reads
    /// <c>Data/iris2-static-registry.json</c>; tests pre-seed an in-memory dictionary.
    /// </summary>
    public interface IIris2StaticRegistryStorage
    {
        /// <summary>
        /// Resolve and parse the registry. Returns a result struct with Success=false plus
        /// an error message when the file is missing or malformed; Success=true with
        /// (possibly empty) entries when load succeeds.
        /// </summary>
        Iris2StaticRegistryLoadResult Load();
    }
}
