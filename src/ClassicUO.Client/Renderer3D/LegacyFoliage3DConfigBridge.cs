// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy Static3DRenderer tree/leaf foliage statics to IFoliage3DConfigBridge.

using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyFoliage3DConfigBridge : IFoliage3DConfigBridge
    {
        // ----- Tree -----
        public TreeRenderMode TreeMode
        {
            get => (TreeRenderMode)(int)Static3DRenderer.TreeMode;
            set => Static3DRenderer.TreeMode = (Static3DRenderer.TreeRenderMode)(int)value;
        }
        public float TreeYJitter
        {
            get => Static3DRenderer.TreeYJitter;
            set => Static3DRenderer.TreeYJitter = value;
        }
        public float TreeZBiasMagnitude
        {
            get => Static3DRenderer.TreeZBiasMagnitude;
            set => Static3DRenderer.TreeZBiasMagnitude = value;
        }
        public bool UseTreeDepthBias
        {
            get => Static3DRenderer.UseTreeDepthBias;
            set => Static3DRenderer.UseTreeDepthBias = value;
        }
        public bool SortTreesBackToFront
        {
            get => Static3DRenderer.SortTreesBackToFront;
            set => Static3DRenderer.SortTreesBackToFront = value;
        }
        public float GroundDecalLift
        {
            get => Static3DRenderer.GroundDecalLift;
            set => Static3DRenderer.GroundDecalLift = value;
        }

        // ----- Leaf-plane -----
        public int LeafPlaneCount
        {
            get => Static3DRenderer.LeafPlaneCount;
            set => Static3DRenderer.LeafPlaneCount = value;
        }
        public float LeafPlaneYawDeg
        {
            get => Static3DRenderer.LeafPlaneYawDeg;
            set => Static3DRenderer.LeafPlaneYawDeg = value;
        }
        public bool LeafPlaneWindEnabled
        {
            get => Static3DRenderer.LeafPlaneWindEnabled;
            set => Static3DRenderer.LeafPlaneWindEnabled = value;
        }
        public float LeafPlaneWindAmpDeg
        {
            get => Static3DRenderer.LeafPlaneWindAmpDeg;
            set => Static3DRenderer.LeafPlaneWindAmpDeg = value;
        }

        // ----- Leaf sway -----
        public LeafSwayMode LeafSwayMode
        {
            get => (LeafSwayMode)(int)Static3DRenderer.LeafSwayMode;
            set => Static3DRenderer.LeafSwayMode = (Static3DRenderer.LeafSwayModeT)(int)value;
        }
        public float LeafSwayPhasePerPlane
        {
            get => Static3DRenderer.LeafSwayPhasePerPlane;
            set => Static3DRenderer.LeafSwayPhasePerPlane = value;
        }
        public float LeafSwayBobAmount
        {
            get => Static3DRenderer.LeafSwayBobAmount;
            set => Static3DRenderer.LeafSwayBobAmount = value;
        }
        public bool LeafSwaySmoothstep
        {
            get => Static3DRenderer.LeafSwaySmoothstep;
            set => Static3DRenderer.LeafSwaySmoothstep = value;
        }
        public bool LeafSwayPerTreePhase
        {
            get => Static3DRenderer.LeafSwayPerTreePhase;
            set => Static3DRenderer.LeafSwayPerTreePhase = value;
        }
        public float LeafSwayPerTreeAmount
        {
            get => Static3DRenderer.LeafSwayPerTreeAmount;
            set => Static3DRenderer.LeafSwayPerTreeAmount = value;
        }

        // ----- Overlay / presence -----
        public bool ApplyOverlayToFoliage
        {
            get => Static3DRenderer.ApplyOverlayToFoliage;
            set => Static3DRenderer.ApplyOverlayToFoliage = value;
        }
        public bool ApplyOverlayToTrunks
        {
            get => Static3DRenderer.ApplyOverlayToTrunks;
            set => Static3DRenderer.ApplyOverlayToTrunks = value;
        }
        public bool ForceWholeTreeAsLeafOverlay
        {
            get => Static3DRenderer.ForceWholeTreeAsLeafOverlay;
            set => Static3DRenderer.ForceWholeTreeAsLeafOverlay = value;
        }
        public float LeafPresence
        {
            get => Static3DRenderer.LeafPresence;
            set => Static3DRenderer.LeafPresence = value;
        }
        public LeafFadeMode LeafFadeMode
        {
            get => (LeafFadeMode)(int)Static3DRenderer.LeafFadeMode;
            set => Static3DRenderer.LeafFadeMode = (Static3DRenderer.LeafFadeModeT)(int)value;
        }

        // ----- Drop-leaves -----
        public bool DropLeavesWorldwide
        {
            get => Static3DRenderer.DropLeavesWorldwide;
            set => Static3DRenderer.DropLeavesWorldwide = value;
        }
        public bool DropLeavesNearby
        {
            get => Static3DRenderer.DropLeavesNearby;
            set => Static3DRenderer.DropLeavesNearby = value;
        }
        public int DropLeavesRadius
        {
            get => Static3DRenderer.DropLeavesRadius;
            set => Static3DRenderer.DropLeavesRadius = value;
        }
    }
}
