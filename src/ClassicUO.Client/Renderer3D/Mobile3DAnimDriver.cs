// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy Mobile3DAnimDriver delegated to IMobileAnimService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.

using System;
using ClassicUO.Game.GameObjects;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
// Legacy facade had a nested ModelPoseSnapshot; alias the new domain type so existing
// callers compile against the same name.
using ModelPoseSnapshot = ClassicUO.Renderer.Mobiles.ModelPoseSnapshot;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IMobileAnimService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class Mobile3DAnimDriver
    {
        private static IMobileAnimService Svc => Renderer3DHost.Services.MobileAnim;

        public static int CachedCount => Svc.CachedCount;
        public static float NowSec => Svc.NowSec;

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (World3DRenderer, MobileRT3DRenderer) compile.
        /// </summary>
        public static void Tick() { /* no-op */ }

        public static void Drop(uint serial) => Svc.Drop(serial);
        public static void Clear() => Svc.Clear();

        /// <summary>Mobile-typed convenience. New code should call <see cref="IMobileAnimService.UpdateMotion"/> directly.</summary>
        public static void UpdateMotion(Mobile m)
        {
            if (m == null) return;
            Svc.UpdateMotion(m.Serial, m.X, m.Y, m.Z);
        }

        public static ModelPoseSnapshot SnapshotPose() => Svc.SnapshotPose();
        public static void RestorePose(ModelPoseSnapshot s) => Svc.RestorePose(s);

        public static void ApplyNpcPose(Mobile m, float dt)
        {
            if (m == null) return;
            Svc.ApplyNpcPose(m.Serial, dt);
        }

        public static bool IsWalking(uint serial) => Svc.IsWalking(serial);
    }
}
