// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Owns the atmospheric color-lerp state — each frame, the active
    /// <see cref="IEnvironmentService.BackgroundColor"/> and
    /// <see cref="IEnvironmentService.FogColor"/> ease toward the target colors carried
    /// here at <see cref="AtmosphereLerpSpeed"/> (units/sec). BloodMoon-style pulsing is
    /// layered on top via <see cref="AtmospherePulseAmount"/> and
    /// <see cref="AtmospherePulseHz"/>.
    /// </summary>
    /// <remarks>
    /// Replaces direct reads/writes of <c>World3DRenderer.BackgroundColorTarget</c>,
    /// <c>FogColorTarget</c>, <c>AtmosphereLerpSpeed</c>, <c>AtmospherePulseAmount</c>,
    /// <c>AtmospherePulseHz</c>. The W3DR statics now delegate to this service.
    /// </remarks>
    public interface IAtmosphereService
    {
        Color BackgroundColorTarget { get; }
        Color FogColorTarget { get; }
        float AtmosphereLerpSpeed { get; }
        float AtmospherePulseAmount { get; }
        float AtmospherePulseHz { get; }

        void SetBackgroundColorTarget(Color color);
        void SetFogColorTarget(Color color);
        void SetAtmosphereLerpSpeed(float speed);
        void SetAtmospherePulseAmount(float amount);
        void SetAtmospherePulseHz(float hz);

        /// <summary>
        /// Advance the atmospheric lerp by <paramref name="dt"/> seconds. Reads + writes
        /// the active background and fog colors through <see cref="IEnvironmentService"/>.
        /// Per-frame; safe to call with dt == 0 (no-op).
        /// </summary>
        void Tick(float dt);
    }
}
