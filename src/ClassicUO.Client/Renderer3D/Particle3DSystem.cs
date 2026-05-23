// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — 3D particle system with batched-quad rendering.
//
// Storage: SoA pools (Vector3 pos/vel/accel + scalar life/size + Color start/end).
// Rendering: every alive particle becomes one billboard quad written into a
// shared DynamicVertexBuffer; one indexed draw call submits all of them.
//
// This is the practical "GPU-instancing-style" optimization for FNA: single
// vertex buffer, single index buffer, one draw call, no per-particle state
// changes. True hardware instancing (DrawInstancedPrimitives + per-instance
// vertex stream + custom HLSL with SV_InstanceID) is the next step — gated
// on writing a custom particle.fx; that lift isn't justified at prototype
// stage when the batched path already drains thousands of particles per
// frame in one submit.
//
// Other optimizations applied:
//   - SoA layout (cache-friendly update loop).
//   - Free-slot scan with high-water mark so we don't iterate the whole pool
//     when only a few hundred particles are alive.
//   - 32-bit indices (8192 particles × 4 verts overflows the 16-bit limit).
//   - Discard-on-write SetData so the driver can hand us a fresh buffer
//     instead of stalling.
//   - Camera-space billboard math computed once per frame (right/up vectors)
//     and reused for every particle quad.
//   - Additive blend, depth-read but not depth-write, so particles never
//     poison the depth buffer for downstream passes.

using System;
using System.Diagnostics;
using ClassicUO.Renderer.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class Particle3DSystem
    {
        // ===== Public toggles (delegated to IParticleService since session 39) =====
        // These were raw `public static bool = …` fields; now delegating properties so the
        // gump-side IParticleService.SetEnabled / SetVerboseLog flow into the same store
        // that Tick/Draw read from.
        public static bool Enabled
        {
            get => Renderer3DHost.IsBound ? Renderer3DHost.Services.Particle.Enabled : _enabledSeed;
            set
            {
                if (Renderer3DHost.IsBound) Renderer3DHost.Services.Particle.SetEnabled(value);
                else                         _enabledSeed = value;
            }
        }
        public static bool VerboseLog
        {
            get => Renderer3DHost.IsBound ? Renderer3DHost.Services.Particle.VerboseLog : _verboseLogSeed;
            set
            {
                if (Renderer3DHost.IsBound) Renderer3DHost.Services.Particle.SetVerboseLog(value);
                else                         _verboseLogSeed = value;
            }
        }
        // Pre-host-binding seeds — written by any startup code that toggles these before
        // Renderer3DHost.Bind() runs. Read once by ParticleServiceConfig.Default if a
        // composition root cares; in practice no one does and these stay at their initial
        // values until the gump first writes through the service.
        private static bool _enabledSeed = true;
        private static bool _verboseLogSeed = false;

        // Cap chosen so cpu-vert array fits comfortably (8192 * 4 * 36B ≈ 1.1 MB).
        // Mirrored on ParticleService.MaxParticles — both constants must match.
        public const int MaxParticles = 8192;

        // ===== SoA storage — session 76: state moved to ParticleService =====
        // Accessor properties delegate to the service's internal arrays. Tick + Draw
        // (still on this class) read them via these properties; the IL emitted is
        // equivalent to direct array access since the service holds a single instance
        // bound at startup. After Tick + Draw migrate, these properties disappear.
        private static ClassicUO.Renderer.Effects.ParticleService Svc
        {
            get
            {
                // Resolve once and cache against the host's binding; if the host has been
                // re-bound (test isolation, hot reload) we re-fetch. The cast is safe because
                // Renderer3DServices only ever registers ParticleService for IParticleService.
                var live = (ClassicUO.Renderer.Effects.ParticleService)
                    ClassicUO.Renderer.Core.Renderer3DHost.Services.Particle;
                return live;
            }
        }

        private static Vector3[] _pos      => Svc.PosArr;
        private static Vector3[] _vel      => Svc.VelArr;
        private static Vector3[] _accel    => Svc.AccelArr;
        private static float[]   _life     => Svc.LifeArr;
        private static float[]   _maxLife  => Svc.MaxLifeArr;
        private static float[]   _size     => Svc.SizeArr;
        private static float[]   _sizeEnd  => Svc.SizeEndArr;
        private static Color[]   _colStart => Svc.ColStartArr;
        private static Color[]   _colEnd   => Svc.ColEndArr;
        private static byte[]    _flags    => Svc.FlagsArr;

        public const byte F_ALIVE        = 0x01;
        public const byte F_PINNED       = 0x02; // doesn't move (used by text formation)
        public const byte F_TRAIL        = 0x04; // emit trail sub-particles
        public const byte F_TEXTURED     = 0x08; // render with ParticleTexture (alpha-blend) instead of additive
        // Per-particle rotation flag — uses life * RotationSpeed as yaw around
        // the camera-forward axis. Cheap "twinkling/tumbling" for snowflakes.
        public const byte F_SPIN         = 0x10;
        // Render with RainTexture (additive, ParticleStreakAspect-tall quad).
        // Used for rain streaks. Mutually exclusive with F_TEXTURED.
        public const byte F_TEXTURED_ADD = 0x20;
        // Stretch the quad along the screen-up axis by ParticleStreakAspect.
        // Used for rain streaks regardless of texturing.
        public const byte F_STREAK       = 0x40;
        // Render with ParticleFlashTexture (additive) — independent of the
        // F_TEXTURED_ADD slot so weather streaks (rain) and one-shot bursts
        // (lightning ground flash, sky flash) don't fight for the same slot.
        public const byte F_FLASH        = 0x80;

        // Texture facade properties — session 78 moved storage to ParticleService.
        // External writers (Weather3DSystem, NukeShow, ParticleStringBuilder, etc.)
        // continue to assign through these statics; reads flow through the service.
        public static Texture2D ParticleTexture     { get => Svc.ParticleTexture;     set => Svc.ParticleTexture = value; }
        public static Texture2D ParticleRainTexture { get => Svc.ParticleRainTexture; set => Svc.ParticleRainTexture = value; }
        public static Texture2D ParticleFlashTexture{ get => Svc.ParticleFlashTexture;set => Svc.ParticleFlashTexture = value; }
        public static float ParticleStreakAspect    { get => Svc.ParticleStreakAspect;set => Svc.ParticleStreakAspect = value; }

        // Frame timing for the Weather3DSystem coordination call inside the legacy
        // Tick facade. Once weather migrates, this can be deleted.
        private static readonly Stopwatch _clock = Stopwatch.StartNew();
        private static double _lastSeconds;

        // ===== Diagnostics — delegated to ParticleService (session 76) =====
        public static int LastDrawnParticles { get => Svc.LastDrawnParticlesMutable; set => Svc.LastDrawnParticlesMutable = value; }
        public static int AliveParticles => Svc.AliveParticles;
        public static int HighWater      => Svc.HighWater;

        // ===== API =====

        /// <summary>
        /// Spawn a particle. Returns index, or -1 if pool is full. Session-76 facade —
        /// the body moved to <see cref="ClassicUO.Renderer.Effects.ParticleService.Spawn"/>.
        /// </summary>
        public static int Spawn(
            Vector3 pos, Vector3 vel, Vector3 accel,
            float life, float size, float sizeEnd,
            Color colorStart, Color colorEnd, byte flags = 0)
            => Svc.Spawn(pos, vel, accel, life, size, sizeEnd, colorStart, colorEnd, flags);

        /// <summary>Session-76 facade — body moved to <see cref="ClassicUO.Renderer.Effects.ParticleService.Clear"/>.</summary>
        public static void Clear() => Svc.Clear();

        /// <summary>
        /// Legacy per-frame entrypoint. Session 77: the particle lifecycle sweep + physics
        /// migrated to <see cref="ClassicUO.Renderer.Effects.ParticleService.Tick"/>
        /// which is now an <c>IFrameService</c> ticked automatically by
        /// <c>Renderer3DServices.Tick</c>. This facade is still called from
        /// <c>GameScene.Update</c> to drive <see cref="Weather3DSystem.Update"/> — the
        /// only cross-cutting body that hasn't migrated. Delete when weather migrates.
        /// </summary>
        public static void Tick()
        {
            double now = _clock.Elapsed.TotalSeconds;
            float dt = (float)(now - _lastSeconds);
            _lastSeconds = now;
            if (dt <= 0f) return;
            // Cap large dt (e.g. after a long pause) so velocities don't explode.
            if (dt > 0.1f) dt = 0.1f;

            // WindManager.Tick + LeafFallSystem.Tick removed — both are no-op facades;
            // their services tick themselves via Renderer3DServices.Tick (session 77).
            // The particle lifecycle sweep + physics moved to ParticleService.Tick.
            // FireworksShow.Update removed — IFireworksService is an IFrameService ticked
            // by Renderer3DServices.Tick. Legacy facade Update is a no-op.
            // NukeShow.Update removed — INukeShowService is an IFrameService ticked by
            // Renderer3DServices.Tick. Legacy facade Update is a no-op.
            // Weather sim is the last unmigrated cross-cutting body driven from this
            // legacy Tick. Once Weather3DSystem migrates, delete this entire method.
            Weather3DSystem.Update(dt);
        }

        /// <summary>
        /// Session-78 facade — Draw + EmitBatch + EnsureResources migrated to
        /// <see cref="ClassicUO.Renderer.Effects.ParticleService.Draw"/>. The legacy
        /// facade survives only to keep <c>GameScene.Update</c>'s call site compiling
        /// during the transitional window.
        /// </summary>
        public static void Draw(GraphicsDevice gd, Matrix view, Matrix proj)
            => Svc.Draw(gd, view, proj);
    }
}
