// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Effects domain (ADR-012).

namespace ClassicUO.Renderer.Effects
{
    /// <summary>
    /// Tunable parameters for <see cref="ParticleService"/>. Mirrors the prior hardcoded
    /// defaults on the legacy <c>Particle3DSystem</c> static class.
    /// </summary>
    public sealed class ParticleServiceConfig
    {
        /// <summary>Master enable toggle initial value.</summary>
        public bool InitialEnabled { get; init; } = true;

        /// <summary>Diagnostic verbose-log initial value.</summary>
        public bool InitialVerboseLog { get; init; } = false;

        public static ParticleServiceConfig Default => new();
    }
}
