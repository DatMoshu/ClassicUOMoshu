// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy TreeStaticRegistry delegated to ITreeStaticRegistry via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3. The legacy enum and struct (TreeRegistryKind /
// TreeRegistryEntry) are kept as [Obsolete] aliases over the new domain types; callers
// using `var` get the new types transparently.

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Legacy classification enum aliased to <see cref="TreeStaticKind"/>. Bit-for-bit
    /// identical values; casts round-trip.
    /// </summary>
    [Obsolete("Use ClassicUO.Renderer.Statics.TreeStaticKind. Will be removed in ADR-012 Phase 3.")]
    internal enum TreeRegistryKind : byte
    {
        Unknown = 0,
        WholeTree = 1,
        TrunkOnly = 2,
        LeafOverlay = 3,
        Bush = 4,
    }

    /// <summary>
    /// Backwards-compatible facade over <see cref="ITreeStaticRegistry"/>. <see cref="TryGet"/>
    /// returns the new <see cref="TreeStaticEntry"/> directly — call sites using <c>var</c>
    /// continue to compile.
    /// </summary>
    [Obsolete("Use ITreeStaticRegistry via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class TreeStaticRegistry
    {
        private static ITreeStaticRegistry Svc => Renderer3DHost.Services.TreeStaticRegistry;

        public static int Count => Svc.Count;
        public static string LastSource => Svc.LastSource;
        public static string LastError => Svc.LastError;

        public static bool Load() => Svc.Load();
        public static bool TryGet(ushort graphic, out TreeStaticEntry entry) => Svc.TryGet(graphic, out entry);
        public static bool IsDeciduous(ushort graphic) => Svc.IsDeciduous(graphic);
    }
}
