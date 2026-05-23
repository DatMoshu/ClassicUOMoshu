// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy AmbientMotes3D delegated to IAmbientMotesService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * CommandManager — `[motes` and `[motepalette` console commands toggle Enabled,
//     adjust TargetAlive/Radius, call SetPalette.
//   * GameScene.DrawCustom (3DCUO inline pass) — calls Configure(anchor) and Tick(dt);
//     Tick is now a no-op (service ticks itself via Renderer3DServices.Tick).

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IAmbientMotesService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class AmbientMotes3D
    {
        private static IAmbientMotesService Svc => Renderer3DHost.Services.AmbientMotes;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        public static int TargetAlive
        {
            get => Svc.TargetAlive;
            set => Svc.SetTargetAlive(value);
        }

        public static float Radius
        {
            get => Svc.Radius;
            set => Svc.SetRadius(value);
        }

        // Legacy fields preserved as direct mirrors. They are no-longer authoritative —
        // the service uses its config values. Mutations here are silently ignored in the
        // transitional window. Remove in Phase 3 after every consumer migrates.
        public static float MinHeight = 8f;
        public static float MaxHeight = 140f;
        public static float SpawnRatePerSecond = 28f;
        public static float DriftUp = 12f;
        public static float SwayHorizontalMax = 22f;
        public static float LifetimeMin = 6f;
        public static float LifetimeMax = 11f;
        public static Color ColorStart = new Color(220, 255, 130, (byte)255);
        public static Color ColorEnd = new Color(80, 160, 60, (byte)0);
        public static float SizeStart = 5f;
        public static float SizeEnd = 1f;
        public static bool UseSoftGlow = true;

        public static void Configure(Vector3 anchorWorld) => Svc.Configure(anchorWorld);

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (<c>GameScene.DrawCustom</c>) compile.
        /// </summary>
        public static void Tick(float dt) { /* no-op */ }

        public static void SetPalette(string name) => Svc.SetPalette(name);
    }
}
