// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy MobileOutfitRegistry delegated to IMobileOutfitService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Note: the legacy MobileOutfit type was a top-level class in this namespace. The new
// MobileOutfit lives in ClassicUO.Renderer.Mobiles. Callers using `var` continue to
// work transparently; explicit `MobileOutfit` references in this namespace now resolve
// via the using alias below.

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
// Both the legacy facade and the new domain export a MobileOutfit. Alias the canonical
// (Mobiles) type so unqualified references in this namespace resolve cleanly.
using MobileOutfit = ClassicUO.Renderer.Mobiles.MobileOutfit;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IMobileOutfitService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class MobileOutfitRegistry
    {
        private static IMobileOutfitService Svc => Renderer3DHost.Services.MobileOutfit;

        public static int CachedCount => Svc.CachedCount;

        public static MobileOutfit GetOrPick(uint serial, string pack) => Svc.GetOrPick(serial, pack);
        public static void Drop(uint serial) => Svc.Drop(serial);
        public static void Clear() => Svc.Clear();
    }
}
