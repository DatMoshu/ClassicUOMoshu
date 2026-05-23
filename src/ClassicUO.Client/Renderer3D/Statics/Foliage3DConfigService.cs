// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>Pure-delegation implementation of <see cref="IFoliage3DConfigService"/>.</summary>
    public sealed class Foliage3DConfigService : IFoliage3DConfigService
    {
        private readonly IFoliage3DConfigBridge _bridge;

        public Foliage3DConfigService(IFoliage3DConfigBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        // ----- Tree -----
        public TreeRenderMode TreeMode => _bridge.TreeMode;
        public float TreeYJitter => _bridge.TreeYJitter;
        public float TreeZBiasMagnitude => _bridge.TreeZBiasMagnitude;
        public bool UseTreeDepthBias => _bridge.UseTreeDepthBias;
        public bool SortTreesBackToFront => _bridge.SortTreesBackToFront;
        public float GroundDecalLift => _bridge.GroundDecalLift;

        public void SetTreeMode(TreeRenderMode value) => _bridge.TreeMode = value;
        public void SetTreeYJitter(float value) => _bridge.TreeYJitter = value;
        public void SetTreeZBiasMagnitude(float value) => _bridge.TreeZBiasMagnitude = value;
        public void SetUseTreeDepthBias(bool value) => _bridge.UseTreeDepthBias = value;
        public void SetSortTreesBackToFront(bool value) => _bridge.SortTreesBackToFront = value;
        public void SetGroundDecalLift(float value) => _bridge.GroundDecalLift = value;

        // ----- Leaf-plane -----
        public int LeafPlaneCount => _bridge.LeafPlaneCount;
        public float LeafPlaneYawDeg => _bridge.LeafPlaneYawDeg;
        public bool LeafPlaneWindEnabled => _bridge.LeafPlaneWindEnabled;
        public float LeafPlaneWindAmpDeg => _bridge.LeafPlaneWindAmpDeg;

        public void SetLeafPlaneCount(int value) => _bridge.LeafPlaneCount = value;
        public void SetLeafPlaneYawDeg(float value) => _bridge.LeafPlaneYawDeg = value;
        public void SetLeafPlaneWindEnabled(bool value) => _bridge.LeafPlaneWindEnabled = value;
        public void SetLeafPlaneWindAmpDeg(float value) => _bridge.LeafPlaneWindAmpDeg = value;

        // ----- Leaf sway -----
        public LeafSwayMode LeafSwayMode => _bridge.LeafSwayMode;
        public float LeafSwayPhasePerPlane => _bridge.LeafSwayPhasePerPlane;
        public float LeafSwayBobAmount => _bridge.LeafSwayBobAmount;
        public bool LeafSwaySmoothstep => _bridge.LeafSwaySmoothstep;
        public bool LeafSwayPerTreePhase => _bridge.LeafSwayPerTreePhase;
        public float LeafSwayPerTreeAmount => _bridge.LeafSwayPerTreeAmount;

        public void SetLeafSwayMode(LeafSwayMode value) => _bridge.LeafSwayMode = value;
        public void SetLeafSwayPhasePerPlane(float value) => _bridge.LeafSwayPhasePerPlane = value;
        public void SetLeafSwayBobAmount(float value) => _bridge.LeafSwayBobAmount = value;
        public void SetLeafSwaySmoothstep(bool value) => _bridge.LeafSwaySmoothstep = value;
        public void SetLeafSwayPerTreePhase(bool value) => _bridge.LeafSwayPerTreePhase = value;
        public void SetLeafSwayPerTreeAmount(float value) => _bridge.LeafSwayPerTreeAmount = value;

        // ----- Overlay / presence -----
        public bool ApplyOverlayToFoliage => _bridge.ApplyOverlayToFoliage;
        public bool ApplyOverlayToTrunks => _bridge.ApplyOverlayToTrunks;
        public bool ForceWholeTreeAsLeafOverlay => _bridge.ForceWholeTreeAsLeafOverlay;
        public float LeafPresence => _bridge.LeafPresence;
        public LeafFadeMode LeafFadeMode => _bridge.LeafFadeMode;

        public void SetApplyOverlayToFoliage(bool value) => _bridge.ApplyOverlayToFoliage = value;
        public void SetApplyOverlayToTrunks(bool value) => _bridge.ApplyOverlayToTrunks = value;
        public void SetForceWholeTreeAsLeafOverlay(bool value) => _bridge.ForceWholeTreeAsLeafOverlay = value;
        public void SetLeafPresence(float value) => _bridge.LeafPresence = value;
        public void SetLeafFadeMode(LeafFadeMode value) => _bridge.LeafFadeMode = value;

        // ----- Drop-leaves -----
        public bool DropLeavesWorldwide => _bridge.DropLeavesWorldwide;
        public bool DropLeavesNearby => _bridge.DropLeavesNearby;
        public int DropLeavesRadius => _bridge.DropLeavesRadius;

        public void SetDropLeavesWorldwide(bool value) => _bridge.DropLeavesWorldwide = value;
        public void SetDropLeavesNearby(bool value) => _bridge.DropLeavesNearby = value;
        public void SetDropLeavesRadius(int value) => _bridge.DropLeavesRadius = value;
    }
}
