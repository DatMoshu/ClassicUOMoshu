// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.EnvRender
{
    /// <summary>
    /// Gump/admin contract for the world's empty-space rendering: background clear color,
    /// fog, and procedural sky. Replaces direct reads/writes of
    /// <c>World3DRenderer.{BackgroundColor, FogX, SkyX}</c>.
    /// </summary>
    /// <remarks>
    /// <para>This is a thin forwarding contract: state-of-record stays on the legacy
    /// <c>World3DRenderer</c> static class because the per-frame Draw pipeline reads these
    /// values in the hot path. Migrating the state requires migrating the Draw pipeline first.</para>
    ///
    /// <para>Color components are exposed as full <see cref="Color"/> values; gumps that edit
    /// individual R/G/B channels read the current Color, mutate, and write back via the
    /// matching <c>Set*Color</c> setter.</para>
    /// </remarks>
    public interface IEnvironmentService
    {
        // ----- Background / clear color -----

        Color BackgroundColor { get; }

        /// <summary>
        /// Effective clear color (background lerped with lightning-flash bias when applicable).
        /// Read-only: computed by the legacy renderer based on lightning state.
        /// </summary>
        Color GetEffectiveClearColor();

        void SetBackgroundColor(Color color);

        // ----- Fog -----

        bool FogEnabled { get; }
        float FogStart { get; }
        float FogEnd { get; }
        Color FogColor { get; }

        void SetFogEnabled(bool enabled);
        void SetFogStart(float start);
        void SetFogEnd(float end);
        void SetFogColor(Color color);

        // ----- Sky -----

        SkyboxMode SkyMode { get; }
        Color SkyTopColor { get; }
        Color SkyHorizonColor { get; }
        Color SkyBottomColor { get; }
        float SkyHorizonY { get; }

        void SetSkyMode(SkyboxMode mode);
        void SetSkyTopColor(Color color);
        void SetSkyHorizonColor(Color color);
        void SetSkyBottomColor(Color color);
        void SetSkyHorizonY(float y);
    }
}
