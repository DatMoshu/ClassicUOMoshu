// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Authoritative source of the externally-visible weather state — current
    /// <see cref="Type"/>, <see cref="Intensity"/>, particle wind components, and lifecycle
    /// triggers (<see cref="SetType"/>, <see cref="TriggerLightning"/>, <see cref="Configure"/>).
    /// </summary>
    /// <remarks>
    /// <para>This is a state-only service. The legacy weather simulation (particle spawning,
    /// lightning timer, tornado physics, profile application) currently lives in the legacy
    /// <c>Weather3DSystem</c> static class and reads state through this service; a future
    /// migration moves simulation here once <c>Particle3DSystem</c> migrates.</para>
    ///
    /// <para>Publishes <see cref="WeatherChangedEvent"/> on every <see cref="SetType"/>
    /// transition (including same-value calls — the legacy contract re-applies profile/textures
    /// on every SetType, so subscribers must too) and <see cref="LightningStruckEvent"/> on
    /// <see cref="TriggerLightning"/>.</para>
    /// </remarks>
    public interface IWeatherService
    {
        // ===== Read state =====

        WeatherKind Type { get; }
        float Intensity { get; }

        /// <summary>Horizontal particle drift (world units/sec²).</summary>
        float WindX { get; }
        float WindZ { get; }

        /// <summary>Cylinder radius around the player in which particles spawn.</summary>
        float Radius { get; }
        /// <summary>Spawn height above the player.</summary>
        float Height { get; }

        bool AutoLightning { get; }
        bool OcclusionCheckEnabled { get; }
        bool ApplyProfileOnSetType { get; }

        /// <summary>Minimum seconds between auto-lightning strikes (storm mode).</summary>
        float LightningEveryMin { get; }
        /// <summary>Maximum seconds between auto-lightning strikes (storm mode).</summary>
        float LightningEveryMax { get; }

        /// <summary>Player anchor in world space; updated each frame by <see cref="Configure"/>.</summary>
        Vector3 Anchor { get; }

        // ===== Mutate state =====

        /// <summary>
        /// Switch active weather. Publishes <see cref="WeatherChangedEvent"/> regardless of
        /// whether the value changed — the legacy contract is "SetType always re-applies" so
        /// subscribers (audio, profile application) must run on every call.
        /// </summary>
        void SetType(WeatherKind kind);

        void SetIntensity(float intensity);
        void SetWindX(float windX);
        void SetWindZ(float windZ);
        void SetRadius(float radius);
        void SetHeight(float height);
        void SetAutoLightning(bool autoLightning);
        void SetOcclusionCheckEnabled(bool enabled);
        void SetApplyProfileOnSetType(bool enabled);
        /// <summary>Set the lightning-cadence lower bound. Clamped to non-negative.</summary>
        void SetLightningEveryMin(float seconds);
        /// <summary>Set the lightning-cadence upper bound. Clamped to non-negative.</summary>
        void SetLightningEveryMax(float seconds);

        /// <summary>
        /// Update the player anchor used by the simulation for spawn positions and lightning
        /// strike origins. Called once per frame from GameScene before world rendering.
        /// </summary>
        void Configure(Vector3 anchorWorld);

        /// <summary>
        /// Fire a lightning strike. Publishes <see cref="LightningStruckEvent"/> with the
        /// current <see cref="Anchor"/> as origin. Subscribers handle the audible thunder,
        /// the screen-flash post-process, and any gameplay hook.
        /// </summary>
        void TriggerLightning();
    }
}
