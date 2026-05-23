// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>Pure-delegation implementation of <see cref="IPlayer3DService"/>.</summary>
    public sealed class Player3DService : IPlayer3DService
    {
        private readonly IPlayer3DBridge _bridge;

        public Player3DService(IPlayer3DBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool Enabled => _bridge.Enabled;
        public bool UseSingleGlb => _bridge.UseSingleGlb;
        public string ModelPath => _bridge.ModelPath;
        public string LastError => _bridge.LastError;

        public float ModelScale => _bridge.ModelScale;
        public float ModelPitchDegrees => _bridge.ModelPitchDegrees;
        public float ModelYawDegrees => _bridge.ModelYawDegrees;
        public float ModelRollDegrees => _bridge.ModelRollDegrees;
        public float ModelYOffset => _bridge.ModelYOffset;

        public int AnimIndex => _bridge.AnimIndex;
        public float AnimSpeed => _bridge.AnimSpeed;
        public float BlendDurationSec => _bridge.BlendDurationSec;
        public PlayerAnimState BaselineState => _bridge.BaselineState;
        public PlayerAnimState CurrentState => _bridge.CurrentState;
        public bool AutoStateFromMovement => _bridge.AutoStateFromMovement;
        public bool StaticIdle => _bridge.StaticIdle;
        public float StaticIdleTimeSec => _bridge.StaticIdleTimeSec;
        public bool TPoseOnly => _bridge.TPoseOnly;

        public bool ForceWhiteMaterial => _bridge.ForceWhiteMaterial;
        public bool SrgbOutput => _bridge.SrgbOutput;
        public bool MobilesWireframe => _bridge.MobilesWireframe;
        public int CullMode => _bridge.CullMode;
        public bool DrawPositionMarker => _bridge.DrawPositionMarker;
        public bool AutoHideHairBeardWhenHat => _bridge.AutoHideHairBeardWhenHat;
        public string HideSubmeshNameMatch => _bridge.HideSubmeshNameMatch;

        public void SetEnabled(bool value) => _bridge.Enabled = value;
        public void SetUseSingleGlb(bool value) => _bridge.UseSingleGlb = value;

        public void SetModelScale(float value) => _bridge.ModelScale = value;
        public void SetModelPitchDegrees(float value) => _bridge.ModelPitchDegrees = value;
        public void SetModelYawDegrees(float value) => _bridge.ModelYawDegrees = value;
        public void SetModelRollDegrees(float value) => _bridge.ModelRollDegrees = value;
        public void SetModelYOffset(float value) => _bridge.ModelYOffset = value;

        public void SetAnimIndex(int value) => _bridge.AnimIndex = value;
        public void SetAnimSpeed(float value) => _bridge.AnimSpeed = value;
        public void SetBlendDurationSec(float value) => _bridge.BlendDurationSec = value;
        public void SetBaselineState(PlayerAnimState value) => _bridge.BaselineState = value;
        public void SetAutoStateFromMovement(bool value) => _bridge.AutoStateFromMovement = value;
        public void SetStaticIdle(bool value) => _bridge.StaticIdle = value;
        public void SetStaticIdleTimeSec(float value) => _bridge.StaticIdleTimeSec = value;
        public void SetTPoseOnly(bool value) => _bridge.TPoseOnly = value;

        public void SetForceWhiteMaterial(bool value) => _bridge.ForceWhiteMaterial = value;
        public void SetSrgbOutput(bool value) => _bridge.SrgbOutput = value;
        public void SetMobilesWireframe(bool value) => _bridge.MobilesWireframe = value;
        public void SetCullMode(int value) => _bridge.CullMode = value;
        public void SetDrawPositionMarker(bool value) => _bridge.DrawPositionMarker = value;
        public void SetAutoHideHairBeardWhenHat(bool value) => _bridge.AutoHideHairBeardWhenHat = value;
        public void SetHideSubmeshNameMatch(string value) => _bridge.HideSubmeshNameMatch = value;

        public void InvalidateAll() => _bridge.InvalidateAll();
        public void TriggerOneShot(PlayerAnimState state, float durationSec = 0f) => _bridge.TriggerOneShot(state, durationSec);
    }
}
