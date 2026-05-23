// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System.Collections.Generic;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Persistence abstraction for <see cref="TreeStaticRegistryService"/>. Production-side
    /// reads <c>tree-statics.json</c>; tests pre-seed an in-memory dictionary.
    /// </summary>
    public interface ITreeStaticRegistryStorage
    {
        /// <summary>Diagnostic — path of the most recently loaded source. Null until first Load.</summary>
        string LastSource { get; }

        /// <summary>Last error message from a failed Load, or null if all Loads succeeded.</summary>
        string LastError { get; }

        /// <summary>
        /// Read entries from storage into the supplied dictionary. The destination is
        /// cleared by the caller before this method runs. Returns true on success
        /// (including the "file not present" case → leaves destination empty and returns
        /// false with <see cref="LastError"/> populated).
        /// </summary>
        bool TryLoad(IDictionary<ushort, TreeStaticEntry> destination);
    }
}
