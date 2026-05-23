// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy FireworksShow delegated to IFireworksService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * Particle3DSystem.Tick — calls Update(dt) (now no-op; service ticks itself).
//   * GameScene.DrawCustom — calls Configure(anchor) once per frame.
//   * FireworksGump — toggles Enabled / Loop, sets ClimaxText, triggers/stops.

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IFireworksService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class FireworksShow
    {
        private static IFireworksService Svc => Renderer3DHost.Services.Fireworks;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        public static bool Loop
        {
            get => Svc.Loop;
            set => Svc.SetLoop(value);
        }

        public static string ClimaxText
        {
            get => Svc.ClimaxText;
            set => Svc.SetClimaxText(value);
        }

        public static float CurrentTime => Svc.CurrentTime;
        public static bool IsRunning => Svc.IsRunning;

        public static void Configure(Vector3 anchorWorld) => Svc.Configure(anchorWorld);
        public static void Trigger() => Svc.Trigger();
        public static void Stop() => Svc.Stop();

        /// <summary>
        /// Legacy hook called by FireworksGump after the user edits ClimaxText. The new
        /// service's <see cref="IFireworksService.SetClimaxText"/> already invalidates the
        /// emitter's cache, so this entry point is a no-op kept only for compile compatibility.
        /// </summary>
        public static void OnClimaxTextChanged() { /* SetClimaxText already invalidated */ }

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (<c>Particle3DSystem.Tick</c>) compile.
        /// </summary>
        public static void Update(float dt) { /* no-op */ }
    }
}
