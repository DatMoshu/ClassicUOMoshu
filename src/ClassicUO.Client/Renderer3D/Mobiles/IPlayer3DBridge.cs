// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Gateway exposing legacy <c>Player3DRenderer</c> mesh + animation tunables. Read+write.
    /// State-of-record stays on the legacy class because per-frame Draw/animation reads them
    /// in the hot path. <c>Model</c> (internal-sealed <c>SkinnedModelGlb</c>) and
    /// <c>LastWorldMatrix</c> are NOT exposed — gumps that inspect mesh structure read them
    /// directly off the legacy class until SkinnedModelGlb is promoted to public.
    /// </summary>
    public interface IPlayer3DBridge
    {
        // ----- Master toggle + mesh source -----
        bool Enabled { get; set; }
        bool UseSingleGlb { get; set; }
        string ModelPath { get; }     // read-only — set by renderer load path
        string LastError { get; }     // read-only — set by renderer load path

        // ----- Transform -----
        float ModelScale { get; set; }
        float ModelPitchDegrees { get; set; }
        float ModelYawDegrees { get; set; }
        float ModelRollDegrees { get; set; }
        float ModelYOffset { get; set; }

        // ----- Animation -----
        int AnimIndex { get; set; }
        float AnimSpeed { get; set; }
        float BlendDurationSec { get; set; }
        PlayerAnimState BaselineState { get; set; }
        PlayerAnimState CurrentState { get; }    // read-only — driven by renderer
        bool AutoStateFromMovement { get; set; }
        bool StaticIdle { get; set; }
        float StaticIdleTimeSec { get; set; }
        bool TPoseOnly { get; set; }

        // ----- Render flags -----
        bool ForceWhiteMaterial { get; set; }
        bool SrgbOutput { get; set; }
        bool MobilesWireframe { get; set; }
        int CullMode { get; set; }
        bool DrawPositionMarker { get; set; }
        bool AutoHideHairBeardWhenHat { get; set; }
        string HideSubmeshNameMatch { get; set; }

        // ----- Methods -----
        void InvalidateAll();
        void TriggerOneShot(PlayerAnimState state, float durationSec);
    }
}
