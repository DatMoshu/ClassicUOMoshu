// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gateway exposing legacy <c>Static3DRenderer</c> tree/leaf foliage tunables. Read+write.
    /// State-of-record stays on the legacy class because per-frame Static3DRenderer.Draw()
    /// reads them in the hot path.
    /// </summary>
    public interface IFoliage3DConfigBridge
    {
        // ----- Tree config -----
        TreeRenderMode TreeMode { get; set; }
        float TreeYJitter { get; set; }
        float TreeZBiasMagnitude { get; set; }
        bool UseTreeDepthBias { get; set; }
        bool SortTreesBackToFront { get; set; }
        float GroundDecalLift { get; set; }

        // ----- Leaf-plane config -----
        int LeafPlaneCount { get; set; }
        float LeafPlaneYawDeg { get; set; }
        bool LeafPlaneWindEnabled { get; set; }
        float LeafPlaneWindAmpDeg { get; set; }

        // ----- Leaf sway config -----
        LeafSwayMode LeafSwayMode { get; set; }
        float LeafSwayPhasePerPlane { get; set; }
        float LeafSwayBobAmount { get; set; }
        bool LeafSwaySmoothstep { get; set; }
        bool LeafSwayPerTreePhase { get; set; }
        float LeafSwayPerTreeAmount { get; set; }

        // ----- Overlay / leaf presence -----
        bool ApplyOverlayToFoliage { get; set; }
        bool ApplyOverlayToTrunks { get; set; }
        bool ForceWholeTreeAsLeafOverlay { get; set; }
        float LeafPresence { get; set; }
        LeafFadeMode LeafFadeMode { get; set; }

        // ----- Drop-leaves -----
        bool DropLeavesWorldwide { get; set; }
        bool DropLeavesNearby { get; set; }
        int DropLeavesRadius { get; set; }
    }
}
