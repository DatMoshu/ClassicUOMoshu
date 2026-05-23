// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Authoritative source of the particle system's runtime toggles + diagnostic counters.
    /// Replaces the public-static surface of the legacy <c>Particle3DSystem</c> class for
    /// admin and diagnostic callers (gumps, dev tools).
    /// </summary>
    /// <remarks>
    /// <para>This service owns <see cref="Enabled"/> and <see cref="VerboseLog"/> as
    /// runtime-mutable state seeded from <see cref="ParticleServiceConfig"/>. The legacy
    /// <c>Particle3DSystem.Enabled</c> / <c>VerboseLog</c> static properties now delegate
    /// to this service, so a UI write here gates the legacy simulator's hot Tick/Draw
    /// paths in the same frame.</para>
    ///
    /// <para>Diagnostic counters (<see cref="AliveParticles"/>, <see cref="LastDrawnParticles"/>,
    /// <see cref="HighWater"/>, <see cref="MaxParticles"/>) and <see cref="Clear"/> are
    /// owned by <see cref="ClassicUO.Renderer.Effects.ParticleService"/> directly
    /// since session 76 — the legacy <c>IParticleSimulator</c> gateway was deleted when
    /// the SoA state migrated. GPU rendering (Tick + Draw) still lives on the legacy
    /// <c>Particle3DSystem</c> static; those migrate in a follow-up session.</para>
    /// </remarks>
    public interface IParticleService
    {
        // ===== Runtime-mutable toggles =====

        bool Enabled { get; }
        bool VerboseLog { get; }

        void SetEnabled(bool enabled);
        void SetVerboseLog(bool verbose);

        // ===== Diagnostic counters (read-through) =====

        int AliveParticles { get; }
        int LastDrawnParticles { get; }
        int HighWater { get; }
        int MaxParticles { get; }

        // ===== Admin =====

        /// <summary>Drop every alive particle. Idempotent.</summary>
        void Clear();
    }
}
