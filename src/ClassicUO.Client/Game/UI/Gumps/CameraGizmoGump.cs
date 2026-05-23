// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — compact camera gizmo with arrow buttons, view presets,
// FreeFly toggle, and Go-To-Player. Works alongside the bigger CameraGump
// which still owns the slider tuning.
//
// MIGRATION (ADR-012 §6 / playbook §Q): two-service injection —
// ICameraModeService (12 touch points) + ICameraStateService (yaw/pitch/zoom
// arrow buttons + ApplyPreset writes). World3DRenderer.LastCameraTarget stays legacy.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Camera;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.World;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class CameraGizmoGump : Gump
    {
        public static CameraGizmoGump Instance;

        private readonly ICameraModeService _camMode;
        private readonly ICameraStateService _camState;
        private readonly IRenderDiagnosticsService _diag;

        private const int W = 240;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;

        // Button IDs (avoid Debug3DStyle reserved range).
        private const int BTN_YAW_LEFT   = 100;
        private const int BTN_YAW_RIGHT  = 101;
        private const int BTN_PITCH_UP   = 102;
        private const int BTN_PITCH_DOWN = 103;
        private const int BTN_RECENTER   = 104;
        private const int BTN_ZOOM_IN    = 110;
        private const int BTN_ZOOM_OUT   = 111;
        private const int BTN_VIEW_FRONT = 200;
        private const int BTN_VIEW_BACK  = 201;
        private const int BTN_VIEW_LEFT  = 202;
        private const int BTN_VIEW_RIGHT = 203;
        private const int BTN_VIEW_TOP   = 204;
        private const int BTN_VIEW_BOTTOM = 205;
        private const int BTN_VIEW_ISO   = 206;
        private const int BTN_TOGGLE_FREEFLY = 300;
        private const int BTN_GOTO_PLAYER    = 301;
        private const int BTN_RESET_CAMERA   = 302;

        // Sprint-13 feature toggles
        private const int BTN_TOGGLE_LOOKAHEAD        = 400;
        private const int BTN_TOGGLE_GROUND_COLLIDE   = 401;
        private const int BTN_TOGGLE_ROTATE_PLAYER    = 402;
        private const int BTN_LOOKAHEAD_DIST_INC      = 403;
        private const int BTN_LOOKAHEAD_DIST_DEC      = 404;
        private const int BTN_GROUND_MARGIN_INC       = 405;
        private const int BTN_GROUND_MARGIN_DEC       = 406;

        private const float STEP_DEG  = 5f;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private NiceButton _freeFlyBtn;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        public CameraGizmoGump(World world)
            : this(world, Renderer3DHost.Services.CameraMode, Renderer3DHost.Services.CameraState,
                   Renderer3DHost.Services.RenderDiagnostics) { }

        public CameraGizmoGump(World world, ICameraModeService camMode, ICameraStateService camState,
                               IRenderDiagnosticsService diag)
            : base(world, 0, 0)
        {
            _camMode = camMode ?? throw new ArgumentNullException(nameof(camMode));
            _camState = camState ?? throw new ArgumentNullException(nameof(camState));
            _diag = diag ?? throw new ArgumentNullException(nameof(diag));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "CAMERA  GIZMO",
                out _outerBg, out _innerBg, out _borders);
            int contentX = Debug3DStyle.GetContentX();
            int innerW   = Debug3DStyle.GetContentWidth(W);

            // ---------- D-pad ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "ROTATE  (5° / click)", y);
            int padCenterX = contentX + innerW / 2 - 14;
            int padTopY    = y;
            int btn = 28;
            Add(new NiceButton(padCenterX, padTopY, btn, btn, ButtonAction.Activate, "^") { ButtonParameter = BTN_PITCH_UP });
            Add(new NiceButton(padCenterX - btn - 2, padTopY + btn + 2, btn, btn, ButtonAction.Activate, "<") { ButtonParameter = BTN_YAW_LEFT });
            Add(new NiceButton(padCenterX,           padTopY + btn + 2, btn, btn, ButtonAction.Activate, "o") { ButtonParameter = BTN_RECENTER });
            Add(new NiceButton(padCenterX + btn + 2, padTopY + btn + 2, btn, btn, ButtonAction.Activate, ">") { ButtonParameter = BTN_YAW_RIGHT });
            Add(new NiceButton(padCenterX,           padTopY + (btn + 2) * 2, btn, btn, ButtonAction.Activate, "v") { ButtonParameter = BTN_PITCH_DOWN });
            y = padTopY + (btn + 2) * 3 + SECTION_GAP;

            // ---------- Zoom ----------
            int halfW = (innerW - 6) / 2;
            Add(new NiceButton(contentX,             y, halfW, 22, ButtonAction.Activate, "Zoom -") { ButtonParameter = BTN_ZOOM_OUT });
            Add(new NiceButton(contentX + halfW + 6, y, halfW, 22, ButtonAction.Activate, "Zoom +") { ButtonParameter = BTN_ZOOM_IN });
            y += ROW_H + SECTION_GAP;

            // ---------- View presets ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "VIEW PRESETS", y);
            Add(new NiceButton(contentX,             y, halfW, 22, ButtonAction.Activate, "Front") { ButtonParameter = BTN_VIEW_FRONT });
            Add(new NiceButton(contentX + halfW + 6, y, halfW, 22, ButtonAction.Activate, "Back")  { ButtonParameter = BTN_VIEW_BACK });
            y += ROW_H + 2;
            Add(new NiceButton(contentX,             y, halfW, 22, ButtonAction.Activate, "Left")  { ButtonParameter = BTN_VIEW_LEFT });
            Add(new NiceButton(contentX + halfW + 6, y, halfW, 22, ButtonAction.Activate, "Right") { ButtonParameter = BTN_VIEW_RIGHT });
            y += ROW_H + 2;
            Add(new NiceButton(contentX,             y, halfW, 22, ButtonAction.Activate, "Top")    { ButtonParameter = BTN_VIEW_TOP });
            Add(new NiceButton(contentX + halfW + 6, y, halfW, 22, ButtonAction.Activate, "Bottom") { ButtonParameter = BTN_VIEW_BOTTOM });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Iso (default)") { ButtonParameter = BTN_VIEW_ISO });
            y += ROW_H + SECTION_GAP;

            // ---------- Modes ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "MODE", y);
            _freeFlyBtn = new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate,
                FreeFlyButtonLabel()) { ButtonParameter = BTN_TOGGLE_FREEFLY };
            Add(_freeFlyBtn);
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Go To Player")
                { ButtonParameter = BTN_GOTO_PLAYER });
            y += ROW_H + 2;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Reset Camera")
                { ButtonParameter = BTN_RESET_CAMERA });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 30;
            Y = 380;
            WantUpdateSize = false;
        }

        public override void Update()
        {
            base.Update();
            // Keep the FreeFly button label in sync with the controller state
            // (mode may be changed from elsewhere — keys, other gumps).
            if (_freeFlyBtn != null && _freeFlyBtn.TextLabel != null)
            {
                _freeFlyBtn.TextLabel.Text = FreeFlyButtonLabel();
            }
        }

        // Non-static so it can read injected _camMode.
        private string FreeFlyButtonLabel()
        {
            return _camMode.CurrentMode == CameraMode.FreeFly
                ? "FreeFly: ON  (click to leave)"
                : "FreeFly: OFF (click to enter)";
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE:
                    Dispose();
                    return;

                case BTN_YAW_LEFT:   _camState.SetYawDegrees(WrapYaw(_camState.YawDegrees - STEP_DEG)); return;
                case BTN_YAW_RIGHT:  _camState.SetYawDegrees(WrapYaw(_camState.YawDegrees + STEP_DEG)); return;
                case BTN_PITCH_UP:   _camState.SetPitchDegrees(ClampPitch(_camState.PitchDegrees + STEP_DEG)); return;
                case BTN_PITCH_DOWN: _camState.SetPitchDegrees(ClampPitch(_camState.PitchDegrees - STEP_DEG)); return;
                case BTN_RECENTER:
                    if (_camMode.CurrentMode == CameraMode.FreeFly)
                        _camMode.GoToPlayer(_diag.LastCameraTarget);
                    return;

                case BTN_ZOOM_IN:  _camState.SetZoom(System.Math.Min(5f,  _camState.Zoom * 1.1f)); return;
                case BTN_ZOOM_OUT: _camState.SetZoom(System.Math.Max(0.1f, _camState.Zoom / 1.1f)); return;

                case BTN_VIEW_FRONT:  ApplyPreset(0f,   5f);  return;
                case BTN_VIEW_BACK:   ApplyPreset(180f, 5f);  return;
                case BTN_VIEW_LEFT:   ApplyPreset(90f,  5f);  return;
                case BTN_VIEW_RIGHT:  ApplyPreset(270f, 5f);  return;
                case BTN_VIEW_TOP:    ApplyPreset(0f,   89f); return;
                case BTN_VIEW_BOTTOM: ApplyPreset(0f,   1f);  return;
                case BTN_VIEW_ISO:    ApplyPreset(45f,  30f); return;

                case BTN_TOGGLE_FREEFLY:
                    if (_camMode.CurrentMode == CameraMode.FreeFly)
                        _camMode.SetMode(CameraMode.ThirdPerson);
                    else
                        _camMode.SetMode(CameraMode.FreeFly);
                    return;

                case BTN_GOTO_PLAYER:
                    _camMode.GoToPlayer(_diag.LastCameraTarget);
                    return;

                case BTN_RESET_CAMERA:
                    _camMode.ResetToDefaults();
                    return;
            }
        }

        // Non-static so it can read injected _camMode.
        private void ApplyPreset(float yaw, float pitch)
        {
            // Presets only make sense in perspective — kick into ThirdPerson if we're Off.
            if (_camMode.CurrentMode == CameraMode.Off)
            {
                _camMode.SetMode(CameraMode.ThirdPerson);
            }
            // FreeFly's PitchDegrees only changes orientation, not the eye position,
            // so flying away then hitting "Top" looks at the player from far away.
            // Snap back to ThirdPerson for the framing to make sense unless user
            // explicitly wanted to fly.
            if (_camMode.CurrentMode == CameraMode.FreeFly)
            {
                _camMode.SetMode(CameraMode.ThirdPerson);
            }
            _camState.SetYawDegrees(yaw);
            _camState.SetPitchDegrees(ClampPitch(pitch));
        }

        private static float WrapYaw(float v)
        {
            while (v < 0f)    v += 360f;
            while (v >= 360f) v -= 360f;
            return v;
        }

        private static float ClampPitch(float v) => MathHelper.Clamp(v, 1f, 89f);
    }
}
