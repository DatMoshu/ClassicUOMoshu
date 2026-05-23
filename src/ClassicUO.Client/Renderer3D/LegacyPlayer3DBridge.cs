// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy Player3DRenderer statics to IPlayer3DBridge.

using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyPlayer3DBridge : IPlayer3DBridge
    {
        public bool Enabled
        {
            get => Player3DRenderer.Enabled;
            set => Player3DRenderer.Enabled = value;
        }
        public bool UseSingleGlb
        {
            get => Player3DRenderer.UseSingleGlb;
            set => Player3DRenderer.UseSingleGlb = value;
        }
        public string ModelPath => Player3DRenderer.ModelPath;
        public string LastError => Player3DRenderer.LastError;

        public float ModelScale
        {
            get => Player3DRenderer.ModelScale;
            set => Player3DRenderer.ModelScale = value;
        }
        public float ModelPitchDegrees
        {
            get => Player3DRenderer.ModelPitchDegrees;
            set => Player3DRenderer.ModelPitchDegrees = value;
        }
        public float ModelYawDegrees
        {
            get => Player3DRenderer.ModelYawDegrees;
            set => Player3DRenderer.ModelYawDegrees = value;
        }
        public float ModelRollDegrees
        {
            get => Player3DRenderer.ModelRollDegrees;
            set => Player3DRenderer.ModelRollDegrees = value;
        }
        public float ModelYOffset
        {
            get => Player3DRenderer.ModelYOffset;
            set => Player3DRenderer.ModelYOffset = value;
        }

        public int AnimIndex
        {
            get => Player3DRenderer.AnimIndex;
            set => Player3DRenderer.AnimIndex = value;
        }
        public float AnimSpeed
        {
            get => Player3DRenderer.AnimSpeed;
            set => Player3DRenderer.AnimSpeed = value;
        }
        public float BlendDurationSec
        {
            get => Player3DRenderer.BlendDurationSec;
            set => Player3DRenderer.BlendDurationSec = value;
        }
        public PlayerAnimState BaselineState
        {
            get => (PlayerAnimState)(int)Player3DRenderer.BaselineState;
            set => Player3DRenderer.BaselineState = (AnimState)(int)value;
        }
        public PlayerAnimState CurrentState => (PlayerAnimState)(int)Player3DRenderer.CurrentState;
        public bool AutoStateFromMovement
        {
            get => Player3DRenderer.AutoStateFromMovement;
            set => Player3DRenderer.AutoStateFromMovement = value;
        }
        public bool StaticIdle
        {
            get => Player3DRenderer.StaticIdle;
            set => Player3DRenderer.StaticIdle = value;
        }
        public float StaticIdleTimeSec
        {
            get => Player3DRenderer.StaticIdleTimeSec;
            set => Player3DRenderer.StaticIdleTimeSec = value;
        }
        public bool TPoseOnly
        {
            get => Player3DRenderer.TPoseOnly;
            set => Player3DRenderer.TPoseOnly = value;
        }

        public bool ForceWhiteMaterial
        {
            get => Player3DRenderer.ForceWhiteMaterial;
            set => Player3DRenderer.ForceWhiteMaterial = value;
        }
        public bool SrgbOutput
        {
            get => Player3DRenderer.SrgbOutput;
            set => Player3DRenderer.SrgbOutput = value;
        }
        public bool MobilesWireframe
        {
            get => Player3DRenderer.MobilesWireframe;
            set => Player3DRenderer.MobilesWireframe = value;
        }
        public int CullMode
        {
            get => Player3DRenderer.CullMode;
            set => Player3DRenderer.CullMode = value;
        }
        public bool DrawPositionMarker
        {
            get => Player3DRenderer.DrawPositionMarker;
            set => Player3DRenderer.DrawPositionMarker = value;
        }
        public bool AutoHideHairBeardWhenHat
        {
            get => Player3DRenderer.AutoHideHairBeardWhenHat;
            set => Player3DRenderer.AutoHideHairBeardWhenHat = value;
        }
        public string HideSubmeshNameMatch
        {
            get => Player3DRenderer.HideSubmeshNameMatch;
            set => Player3DRenderer.HideSubmeshNameMatch = value;
        }

        public void InvalidateAll() => Player3DRenderer.InvalidateAll();
        public void TriggerOneShot(PlayerAnimState state, float durationSec)
            => Player3DRenderer.TriggerOneShot((AnimState)(int)state, durationSec);
    }
}
