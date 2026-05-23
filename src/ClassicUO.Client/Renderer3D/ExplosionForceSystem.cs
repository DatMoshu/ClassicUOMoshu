// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy ExplosionForceSystem delegated to IExplosionService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * Particle3DSystem.Tick — calls Update(dt) (now no-op; service ticks itself).
//   * NukeShow — calls Add(...) for blast events.
//   * Static3DRenderer — calls Query(...) and QueryTile(...) per-tree per-frame.
//   * NukeGump — reads LiveEvents for HUD; calls Clear() from buttons.

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IExplosionService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class ExplosionForceSystem
    {
        private static IExplosionService Svc => Renderer3DHost.Services.Explosion;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        // Legacy diagnostic mirrors. Service config holds the authoritative values now.
        public static float BendAttackS = 0.18f;
        public static float BendDuration = 1.6f;
        public static float LeavesHiddenS = 10f;
        public static float BendStrengthPx = 28f;
        public static float CoreFalloffFrac = 0.15f;

        public static int LiveEvents => Svc.LiveEvents;

        public static void Add(float centerX, float centerZ, float radius, float strength)
            => Svc.Add(centerX, centerZ, radius, strength);

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (<c>Particle3DSystem.Tick</c>) compile.
        /// </summary>
        public static void Update(float dt) { /* no-op */ }

        public static void Clear() => Svc.Clear();

        public static bool Query(float wx, float wz, out float bendX, out float bendZ, out bool leavesHidden)
            => Svc.Query(wx, wz, out bendX, out bendZ, out leavesHidden);

        /// <summary>
        /// Tile-coord convenience overload. Stays in the facade because <see cref="LandMesh3D.TILE"/>
        /// lives in the legacy namespace; new code should call <see cref="Query"/> directly with
        /// world coordinates the caller has already computed.
        /// </summary>
        public static bool QueryTile(int tileX, int tileY,
            out float bendX, out float bendZ, out bool leavesHidden)
        {
            float wx = tileX * LandMesh3D.TILE + LandMesh3D.TILE * 0.5f;
            float wz = tileY * LandMesh3D.TILE + LandMesh3D.TILE * 0.5f;
            return Svc.Query(wx, wz, out bendX, out bendZ, out leavesHidden);
        }
    }
}
