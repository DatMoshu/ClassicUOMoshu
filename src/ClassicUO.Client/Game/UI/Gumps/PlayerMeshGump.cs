// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — player mesh tuning split out from Debug3DGump.
// Owns the PLAYER MESH section: enabled / pose / blend / scale /
// pitch / yaw / roll / Y-offset / anim index / anim speed sliders,
// the anim quick-fire row, and the model status panel.
//
// MIGRATION (ADR-012 §6): all gump-tunable Player3DRenderer fields go through
// IPlayer3DService. The Model object (SkinnedModelGlb) is internal-sealed in the
// renderer; the status label still reads it directly until that type is promoted.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PlayerMeshGump : Gump
    {
        public static PlayerMeshGump Instance;

        private readonly IPlayer3DService _player;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 460;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;
        private const int LABEL_W   = 150;

        private static class BtnId
        {
            public const int Reload     = 1;
            public const int AnimIdle   = 10;
            public const int AnimRun    = 11;
            public const int AnimHit    = 12;
            public const int AnimAttack = 13;
        }

        private Label _modelStatusLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        public PlayerMeshGump(World world)
            : this(world, Renderer3DHost.Services.Player3D) { }

        public PlayerMeshGump(World world, IPlayer3DService player) : base(world, 0, 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;
            Instance = this;

            int y = Debug3DStyle.BuildShell(this, W, "PLAYER MESH",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Toggles ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "MESH TOGGLES", y);
            y = AddTwoColCheck(
                "Enabled", _player.Enabled, v => _player.SetEnabled(v),
                "Position marker", _player.DrawPositionMarker,
                    v => _player.SetDrawPositionMarker(v),
                y);
            y = AddTwoColCheck(
                "Test cube (P1)", Test3DRenderer.Enabled, v => Test3DRenderer.Enabled = v,
                "T-pose only", _player.TPoseOnly, v => _player.SetTPoseOnly(v),
                y);
            y = AddTwoColCheck(
                "Static idle (freeze frame)", _player.StaticIdle,
                    v => _player.SetStaticIdle(v),
                "Single GLB (all anims)", _player.UseSingleGlb, v =>
                {
                    _player.SetUseSingleGlb(v);
                    _player.InvalidateAll();
                },
                y);
            y = AddTwoColCheck(
                "Auto-hide hair/beard under hat", _player.AutoHideHairBeardWhenHat,
                    v => _player.SetAutoHideHairBeardWhenHat(v),
                "", false, null,
                y);
            y = AddSlider("Idle frame time (x100s)", 0, 500,
                (int)(_player.StaticIdleTimeSec * 100), y,
                v => _player.SetStaticIdleTimeSec(v / 100f));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Transform sliders ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "TRANSFORM", y);
            y = AddSlider("Blend (ms)", 0, 1000, (int)(_player.BlendDurationSec * 1000), y,
                v => _player.SetBlendDurationSec(v / 1000f));
            y = AddSlider("Scale (x10)", 1, 1000, (int)(_player.ModelScale * 10), y,
                v => _player.SetModelScale(v / 10f));
            y = AddSlider("Pitch (deg)", -180, 180, (int)_player.ModelPitchDegrees, y,
                v => _player.SetModelPitchDegrees(v));
            y = AddSlider("Yaw (deg)", 0, 359, (int)_player.ModelYawDegrees, y,
                v => _player.SetModelYawDegrees(v));
            y = AddSlider("Roll (deg)", -180, 180, (int)_player.ModelRollDegrees, y,
                v => _player.SetModelRollDegrees(v));
            y = AddSlider("Y offset", -500, 500, (int)_player.ModelYOffset, y,
                v => _player.SetModelYOffset(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Animation ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ANIMATION", y);
            y = AddSlider("Anim index", 0, 8, _player.AnimIndex, y,
                v => _player.SetAnimIndex(v));
            y = AddSlider("Anim speed (x10)", 0, 30, (int)(_player.AnimSpeed * 10), y,
                v => _player.SetAnimSpeed(v / 10f));
            int qW = (innerW - 30) / 4;
            Add(new NiceButton(contentX + 0*(qW+10), y, qW, 22, ButtonAction.Activate, "Idle")   { ButtonParameter = BtnId.AnimIdle });
            Add(new NiceButton(contentX + 1*(qW+10), y, qW, 22, ButtonAction.Activate, "Run")    { ButtonParameter = BtnId.AnimRun });
            Add(new NiceButton(contentX + 2*(qW+10), y, qW, 22, ButtonAction.Activate, "Hit!")   { ButtonParameter = BtnId.AnimHit });
            Add(new NiceButton(contentX + 3*(qW+10), y, qW, 22, ButtonAction.Activate, "Attack!"){ ButtonParameter = BtnId.AnimAttack });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Reload models")
                { ButtonParameter = BtnId.Reload });
            y += ROW_H + Debug3DStyle.SECTION_GAP;

            // ---------- Status ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "MODEL STATUS", y);
            _modelStatusLabel = new Label("(model status)", true, Debug3DStyle.HUE_LABEL_WHITE,
                innerW, 4) { X = contentX, Y = y };
            Add(_modelStatusLabel);
            y += ROW_H * 4 + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 280;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); return;
                case BtnId.Reload: _player.InvalidateAll(); break;
                case BtnId.AnimIdle:
                    _player.SetAutoStateFromMovement(false);
                    _player.SetBaselineState(PlayerAnimState.Idle);
                    break;
                case BtnId.AnimRun:
                    _player.SetAutoStateFromMovement(false);
                    _player.SetBaselineState(PlayerAnimState.Run);
                    break;
                case BtnId.AnimHit:    _player.TriggerOneShot(PlayerAnimState.Hit, 1.2f);    break;
                case BtnId.AnimAttack: _player.TriggerOneShot(PlayerAnimState.Attack, 1.2f); break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (_modelStatusLabel == null) return;

            // Model is internal-sealed SkinnedModelGlb — read direct off legacy until promoted.
            var m = Player3DRenderer.Model;
            if (m != null)
            {
                string anim = (m.Animations?.Length ?? 0) > 0
                    ? m.Animations[Math.Clamp(m.ActiveAnim, 0, m.Animations.Length - 1)].Name : "(none)";
                _modelStatusLabel.Text =
                    $"state   {_player.CurrentState}  (auto={_player.AutoStateFromMovement})\n" +
                    $"model   joints {m.JointNodeIndex.Length}  submeshes {m.Submeshes.Count}\n" +
                    $"clip    '{anim}'  t={m.AnimTime:F2}s";
                _modelStatusLabel.Hue = Debug3DStyle.HUE_OK;
            }
            else if (!string.IsNullOrEmpty(_player.LastError))
            {
                _modelStatusLabel.Text = $"model   FAILED\n{_player.LastError}";
                _modelStatusLabel.Hue = Debug3DStyle.HUE_WARN;
            }
            else
            {
                _modelStatusLabel.Text = "model   not loaded yet";
                _modelStatusLabel.Hue = Debug3DStyle.HUE_LABEL_WHITE;
            }
        }

        private int AddTwoColCheck(
            string label1, bool init1, Action<bool> on1,
            string label2, bool init2, Action<bool> on2,
            int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddTwoColCheck(this, contentX, innerW, y,
                label1, init1, on1, label2, init2, on2);
        }

        private int AddSlider(string label, int min, int max, int value, int y, Action<int> onChange)
        {
            int contentX = INNER_PAD + 10;
            int rowW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSlider(this, contentX, rowW, LABEL_W,
                label, min, max, value, y, onChange);
        }
    }
}
