// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Gump/admin contract for the player's 3D mesh + animation. Replaces direct reads/writes
    /// of <c>Player3DRenderer.{Enabled, ModelScale, AnimIndex, ...}</c> consumed by
    /// PlayerMeshGump, ModelInfoGump, PlayerMaterialGump, MaterialDebugGump, MobileRender3DGump,
    /// and Npc3DDebugGump.
    /// </summary>
    /// <remarks>
    /// <c>Model</c> (the renderer-internal <c>SkinnedModelGlb</c>) and <c>LastWorldMatrix</c>
    /// are intentionally NOT exposed — they leak deep mesh internals. Diagnostic gumps that
    /// need them read the legacy static directly until those types are promoted to public.
    /// </remarks>
    // TODO Phase 2 (ISP): split into IPlayerTransformService / IPlayerAnimService /
    // IPlayerMaterialService once Phase 1 surface migration is complete on every consumer.
    public interface IPlayer3DService
    {
        bool Enabled { get; }
        bool UseSingleGlb { get; }
        string ModelPath { get; }
        string LastError { get; }

        float ModelScale { get; }
        float ModelPitchDegrees { get; }
        float ModelYawDegrees { get; }
        float ModelRollDegrees { get; }
        float ModelYOffset { get; }

        int AnimIndex { get; }
        float AnimSpeed { get; }
        float BlendDurationSec { get; }
        PlayerAnimState BaselineState { get; }
        PlayerAnimState CurrentState { get; }
        bool AutoStateFromMovement { get; }
        bool StaticIdle { get; }
        float StaticIdleTimeSec { get; }
        bool TPoseOnly { get; }

        bool ForceWhiteMaterial { get; }
        bool SrgbOutput { get; }
        bool MobilesWireframe { get; }
        int CullMode { get; }
        bool DrawPositionMarker { get; }
        bool AutoHideHairBeardWhenHat { get; }
        string HideSubmeshNameMatch { get; }

        void SetEnabled(bool value);
        void SetUseSingleGlb(bool value);

        void SetModelScale(float value);
        void SetModelPitchDegrees(float value);
        void SetModelYawDegrees(float value);
        void SetModelRollDegrees(float value);
        void SetModelYOffset(float value);

        void SetAnimIndex(int value);
        void SetAnimSpeed(float value);
        void SetBlendDurationSec(float value);
        void SetBaselineState(PlayerAnimState value);
        void SetAutoStateFromMovement(bool value);
        void SetStaticIdle(bool value);
        void SetStaticIdleTimeSec(float value);
        void SetTPoseOnly(bool value);

        void SetForceWhiteMaterial(bool value);
        void SetSrgbOutput(bool value);
        void SetMobilesWireframe(bool value);
        void SetCullMode(int value);
        void SetDrawPositionMarker(bool value);
        void SetAutoHideHairBeardWhenHat(bool value);
        void SetHideSubmeshNameMatch(string value);

        void InvalidateAll();
        void TriggerOneShot(PlayerAnimState state, float durationSec = 0f);
    }
}
