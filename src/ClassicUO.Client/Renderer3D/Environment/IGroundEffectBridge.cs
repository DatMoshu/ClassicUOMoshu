// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Gateway exposing legacy <c>World3DRenderer</c> ground-overlay state. Lets
    /// <see cref="IGroundEffectService"/> stay free of the renderer's heavy Draw pipeline.
    /// </summary>
    /// <remarks>
    /// Production-side reads/writes the corresponding <c>World3DRenderer.X</c> static field
    /// on each access. The per-frame ease (current intensity → target intensity) runs inside
    /// the legacy renderer; the service exposes both current and target.
    /// </remarks>
    public interface IGroundEffectBridge
    {
        GroundEffectMode Mode { get; set; }

        /// <summary>Current eased intensity (0..1). Read-only — the renderer drives this.</summary>
        float CurrentIntensity { get; }

        /// <summary>Target intensity (0..1). UI sets this; the legacy renderer eases toward it each frame.</summary>
        float TargetIntensity { get; set; }

        /// <summary>How fast CurrentIntensity eases toward TargetIntensity. Larger = snappier.</summary>
        float EaseSpeed { get; set; }

        /// <summary>When true, the active weather drives Mode + TargetIntensity automatically.</summary>
        bool LinkToWeather { get; set; }

        /// <summary>Weather-driven intensity multiplier (0..1).</summary>
        float LinkStrength { get; set; }
    }
}
