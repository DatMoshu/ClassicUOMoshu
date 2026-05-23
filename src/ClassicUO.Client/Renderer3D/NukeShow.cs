// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy NukeShow delegated to INukeShowService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.
//
// Callers (deletion blockers):
//   * Particle3DSystem.Tick — calls Update(dt) (now no-op; service ticks itself).
//   * GameScene.DrawCustom — calls Configure(anchor) once per frame.
//   * NukeGump — reads/writes the tunable surface and calls TriggerSingle/Barrage/Stop.
//   * LegacySeasonHostBridge — sets Enabled and calls TriggerNukeBarrage.

using System;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Effects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use INukeShowService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class NukeShow
    {
        private static INukeShowService Svc => Renderer3DHost.Services.NukeShow;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        public static bool VerboseLog
        {
            get => Svc.VerboseLog;
            set => Svc.SetVerboseLog(value);
        }

        public static int BarrageCount
        {
            get => Svc.BarrageCount;
            set => Svc.SetBarrageCount(value);
        }

        public static float BarrageRadius
        {
            get => Svc.BarrageRadius;
            set => Svc.SetBarrageRadius(value);
        }

        public static float Stagger
        {
            get => Svc.Stagger;
            set => Svc.SetStagger(value);
        }

        public static float SingleDistance => Svc.SingleDistance;

        // Legacy mirror — service config holds the authoritative value.
        public static int UoExplosionSound = 0x0207;

        public static float NukeScale
        {
            get => Svc.NukeScale;
            set => Svc.SetNukeScale(value);
        }

        public static float BlastRadius
        {
            get => Svc.BlastRadius;
            set => Svc.SetBlastRadius(value);
        }

        // Legacy mirror — service config holds the authoritative value.
        public static string DDayAudioFile = "dday.wav";

        public static bool PlayDDayAudio
        {
            get => Svc.PlayDDayAudio;
            set => Svc.SetPlayDDayAudio(value);
        }

        public const string FlashTextureName = "halo";

        public static int RemainingDetonations => Svc.RemainingDetonations;
        public static bool IsRunning => Svc.IsRunning;
        public static float CurrentTime => Svc.CurrentTime;

        public static void Configure(Vector3 anchorWorld) => Svc.Configure(anchorWorld);
        public static Vector3 GetAnchor() => Svc.Anchor;

        public static void TriggerSingle() => Svc.TriggerSingle();
        public static void TriggerBarrage() => Svc.TriggerBarrage();
        public static void Stop() => Svc.Stop();

        /// <summary>
        /// Legacy entry point. The service ticks itself via <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (<c>Particle3DSystem.Tick</c>) compile.
        /// </summary>
        public static void Update(float dt) { /* no-op */ }
    }
}
