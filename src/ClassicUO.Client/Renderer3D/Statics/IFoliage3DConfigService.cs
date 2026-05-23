// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gump/admin contract for the 3D static-renderer's tree/leaf foliage tunables.
    /// Replaces direct reads/writes of <c>Static3DRenderer.{TreeMode, LeafSwayMode,
    /// LeafPresence, ...}</c> consumed by Trees3DSettingsGump.
    /// </summary>
    // TODO Phase 2 (ISP): 24 members across 5 logical sub-groups (Tree / LeafPlane /
    // LeafSway / Overlay-Presence / DropLeaves). Split when Phase 1 migration is
    // complete on every consumer; the sub-group comment blocks below are the cut lines.
    public interface IFoliage3DConfigService
    {
        // ----- Tree config -----
        TreeRenderMode TreeMode { get; }
        float TreeYJitter { get; }
        float TreeZBiasMagnitude { get; }
        bool UseTreeDepthBias { get; }
        bool SortTreesBackToFront { get; }
        float GroundDecalLift { get; }

        void SetTreeMode(TreeRenderMode value);
        void SetTreeYJitter(float value);
        void SetTreeZBiasMagnitude(float value);
        void SetUseTreeDepthBias(bool value);
        void SetSortTreesBackToFront(bool value);
        void SetGroundDecalLift(float value);

        // ----- Leaf-plane config -----
        int LeafPlaneCount { get; }
        float LeafPlaneYawDeg { get; }
        bool LeafPlaneWindEnabled { get; }
        float LeafPlaneWindAmpDeg { get; }

        void SetLeafPlaneCount(int value);
        void SetLeafPlaneYawDeg(float value);
        void SetLeafPlaneWindEnabled(bool value);
        void SetLeafPlaneWindAmpDeg(float value);

        // ----- Leaf sway config -----
        LeafSwayMode LeafSwayMode { get; }
        float LeafSwayPhasePerPlane { get; }
        float LeafSwayBobAmount { get; }
        bool LeafSwaySmoothstep { get; }
        bool LeafSwayPerTreePhase { get; }
        float LeafSwayPerTreeAmount { get; }

        void SetLeafSwayMode(LeafSwayMode value);
        void SetLeafSwayPhasePerPlane(float value);
        void SetLeafSwayBobAmount(float value);
        void SetLeafSwaySmoothstep(bool value);
        void SetLeafSwayPerTreePhase(bool value);
        void SetLeafSwayPerTreeAmount(float value);

        // ----- Overlay / leaf presence -----
        bool ApplyOverlayToFoliage { get; }
        bool ApplyOverlayToTrunks { get; }
        bool ForceWholeTreeAsLeafOverlay { get; }
        float LeafPresence { get; }
        LeafFadeMode LeafFadeMode { get; }

        void SetApplyOverlayToFoliage(bool value);
        void SetApplyOverlayToTrunks(bool value);
        void SetForceWholeTreeAsLeafOverlay(bool value);
        void SetLeafPresence(float value);
        void SetLeafFadeMode(LeafFadeMode value);

        // ----- Drop-leaves -----
        bool DropLeavesWorldwide { get; }
        bool DropLeavesNearby { get; }
        int DropLeavesRadius { get; }

        void SetDropLeavesWorldwide(bool value);
        void SetDropLeavesNearby(bool value);
        void SetDropLeavesRadius(int value);
    }
}
