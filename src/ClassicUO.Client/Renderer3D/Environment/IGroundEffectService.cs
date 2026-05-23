// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Gump/admin contract for the world's ground-overlay rendering (wet sheen, snow
    /// accumulation). Replaces direct reads/writes of
    /// <c>World3DRenderer.{GroundEffectMode, GroundEffectIntensity, TargetGroundEffectIntensity,
    /// GroundEffectEaseSpeed, LinkToWeather, LinkStrength}</c>.
    /// </summary>
    /// <remarks>
    /// State-of-record stays on the legacy <c>World3DRenderer</c> static class because the
    /// per-frame ground pass + UpdateGroundEffectEase live there. Migrating the state
    /// requires migrating the Draw pipeline first.
    /// </remarks>
    public interface IGroundEffectService
    {
        // ----- Read state -----

        GroundEffectMode Mode { get; }

        /// <summary>Current eased intensity (0..1). Renderer drives this; gumps read for status display.</summary>
        float CurrentIntensity { get; }

        float TargetIntensity { get; }
        float EaseSpeed { get; }
        bool LinkToWeather { get; }
        float LinkStrength { get; }

        // ----- Mutate state -----

        void SetMode(GroundEffectMode mode);
        void SetTargetIntensity(float intensity);
        void SetEaseSpeed(float speed);
        void SetLinkToWeather(bool link);
        void SetLinkStrength(float strength);
    }
}
