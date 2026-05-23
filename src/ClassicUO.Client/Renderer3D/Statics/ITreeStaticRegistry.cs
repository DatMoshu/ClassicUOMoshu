// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Per-graphic tree-static lookup. Replaces the legacy <c>TreeStaticRegistry</c>
    /// static class. Read-mostly: <see cref="Load"/> populates the table; <see cref="TryGet"/>
    /// + <see cref="IsDeciduous"/> are the hot-path queries.
    /// </summary>
    public interface ITreeStaticRegistry
    {
        int Count { get; }
        string LastSource { get; }
        string LastError { get; }

        /// <summary>Reload the table from storage. Returns true on success.</summary>
        bool Load();

        /// <summary>Lookup a graphic's entry. Returns false when not in the table.</summary>
        bool TryGet(ushort graphic, out TreeStaticEntry entry);

        /// <summary>
        /// True iff <paramref name="graphic"/> is in the table AND tagged deciduous (drops
        /// leaves). Evergreens and unknowns return false so callers do not skip emitting the canopy.
        /// </summary>
        bool IsDeciduous(ushort graphic);
    }
}
