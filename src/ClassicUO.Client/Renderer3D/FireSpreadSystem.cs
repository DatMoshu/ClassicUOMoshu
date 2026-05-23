// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy FireSpreadSystem delegated to IFireService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * Particle3DSystem.Tick — calls Update(dt) (now no-op; service ticks itself).
//   * NukeShow / Weather3DSystem — call Ignite(...) for visual effects.
//   * NukeGump — reads LiveFires for HUD; calls Clear() / Ignite() from buttons.

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IFireService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class FireSpreadSystem
    {
        private static IFireService Svc => Renderer3DHost.Services.Fire;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        // Legacy diagnostic mirrors. Service config holds the authoritative values now;
        // mutations here are silently ignored. Remove in Phase 3.
        public static bool VerboseLog;
        public static float DefaultRadius = 35f;
        public static float DefaultLifetime = 9f;
        public static float EmberRatePerSec = 90f;
        public static float SmokeRatePerSec = 18f;

        public static int LiveFires => Svc.LiveFires;

        public static void Ignite(Vector3 worldGround) => Svc.Ignite(worldGround);
        public static void Ignite(Vector3 worldGround, float radius, float lifetime)
            => Svc.Ignite(worldGround, radius, lifetime);

        public static void Clear() => Svc.Clear();

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (<c>Particle3DSystem.Tick</c>) compile.
        /// </summary>
        public static void Update(float dt) { /* no-op */ }
    }
}
