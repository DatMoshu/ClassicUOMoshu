// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy LeafFallSystem delegated to ILeafFallService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.

using System;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Backwards-compatible facade over <see cref="ClassicUO.Renderer.WorldEnv.ILeafFallService"/>.
    /// </summary>
    [Obsolete("Use ILeafFallService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class LeafFallSystem
    {
        private static ClassicUO.Renderer.WorldEnv.ILeafFallService Service
            => Renderer3DHost.Services.LeafFall;

        public static bool Enabled
        {
            get => Service.Enabled;
            set => Service.SetEnabled(value);
        }

        public static bool UseManualSeason
        {
            get => Service.UseManualSeason;
            set => Service.SetUseManualSeason(value);
        }

        public static float ManualSeasonProgress
        {
            get => Service.ManualSeasonProgress;
            set => Service.SetManualSeasonProgress(value);
        }

        public static int LastDrawnLeaves => Service.LastDrawnLeaves;
        public static float LastSpawnRate => Service.LastSpawnRate;
        public static int AliveLeaves => Service.AliveLeaves;

        // Legacy public tunables — moved to LeafFallServiceConfig. Kept as fields so any
        // straggler callers compile; mutations are no-ops in this transitional window.
        public static float MaxSpawnPerSecond = 30f;
        public static float SpawnRadius = 480f;
        public static float SpawnHeight = 220f;

        public static void Configure(Vector3 playerAnchorWorld) => Service.Configure(playerAnchorWorld);

        public static void Draw(GraphicsDevice gd, Matrix view, Matrix proj)
            => Service.Draw(gd, view, proj);

        public static void Clear() => Service.Clear();

        /// <summary>
        /// Legacy entry point. The service is ticked by <see cref="Renderer3DServices.Tick"/>;
        /// this is a no-op kept so straggler callers (Particle3DSystem.Tick) compile.
        /// </summary>
        public static void Tick() { /* no-op */ }

        /// <summary>Pure-math helper preserved for any external caller (none currently known).</summary>
        public static float SpawnRateAt(float y)
            => ClassicUO.Renderer.WorldEnv.LeafFallService.SpawnRateAt(y);
    }
}
