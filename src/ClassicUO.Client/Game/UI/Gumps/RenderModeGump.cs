// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — render-mode + camera-mode picker, split out from
// the monolithic Debug3DGump. Owns RenderModeController, the legacy
// quick-mode macros (Pure 2D / Pure 3D ISO / Pers 3D), and the camera
// mode buttons (1st/3rd/Cinematic/FreeFly/Off).
//
// MIGRATION (ADR-012 §6 / playbook §Q): camera-mode portion (7 touch points)
// injects ICameraModeService. RenderModeController stays on legacy.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Camera;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
using LegacyRenderMode = ClassicUO.Renderer.Renderer3D.RenderMode;
using RenderMode = ClassicUO.Renderer.Mobiles.RenderMode;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class RenderModeGump : Gump
    {
        public static RenderModeGump Instance;

        private readonly ICameraModeService _camMode;
        private readonly IRenderModeService _renderMode;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 380;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private static class BtnId
        {
            public const int ModeClassic2D    = 10;
            public const int ModeIso3D        = 11;
            public const int ModeFull3D       = 12;

            public const int Use3DPlayerInClassic2D = 13;

            public const int CamFirstPerson   = 20;
            public const int CamThirdPerson   = 21;
            public const int CamCinematic     = 22;
            public const int CamFreeFly       = 23;
            public const int CamOff           = 24;
        }

        private Label _modeLabel;
        private Label _camModeLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        /// <summary>
        /// Convenience overload that resolves <see cref="ICameraModeService"/> from the
        /// active renderer service container.
        /// </summary>
        public RenderModeGump(World world)
            : this(world, Renderer3DHost.Services.CameraMode, Renderer3DHost.Services.RenderMode) { }

        public RenderModeGump(World world, ICameraModeService camMode, IRenderModeService renderMode)
            : base(world, 0, 0)
        {
            _camMode = camMode ?? throw new ArgumentNullException(nameof(camMode));
            _renderMode = renderMode ?? throw new ArgumentNullException(nameof(renderMode));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;
            Instance = this;

            int y = Debug3DStyle.BuildShell(this, W, "RENDER MODE",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Render mode ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WORLD RENDER MODE  (F3 to cycle)", y);
            int btnW = (innerW - 20) / 3;
            Add(new NiceButton(contentX + 0*(btnW+10), y, btnW, 22, ButtonAction.Activate, "Classic 2D") { ButtonParameter = BtnId.ModeClassic2D });
            Add(new NiceButton(contentX + 1*(btnW+10), y, btnW, 22, ButtonAction.Activate, "3D Iso")     { ButtonParameter = BtnId.ModeIso3D });
            Add(new NiceButton(contentX + 2*(btnW+10), y, btnW, 22, ButtonAction.Activate, "Full 3D")    { ButtonParameter = BtnId.ModeFull3D });
            y += ROW_H + 4;
            _modeLabel = new Label("Mode: " + _renderMode.CurrentLabel,
                true, Debug3DStyle.HUE_OK, innerW, 1) { X = contentX, Y = y };
            Add(_modeLabel);
            y += ROW_H;

            // Sub-toggle: only meaningful when Classic2D is the active mode.
            // Flag is always exposed (per project rule "Always add a UI toggle"),
            // even if currently a no-op for Iso3D/Full3D.
            Debug3DStyle.AddCheck(this, contentX, y, "Render player as 3D mobile (Classic 2D only)",
                _renderMode.Use3DPlayerInClassic2D,
                v => _renderMode.SetUse3DPlayerInClassic2D(v));
            y += ROW_H + Debug3DStyle.SECTION_GAP;

            // ---------- Camera mode ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "CAMERA MODE (Full 3D only)", y);
            int camW = (innerW - 4 * 8) / 5;
            Add(new NiceButton(contentX + 0*(camW+8), y, camW, 22, ButtonAction.Activate, "1st Person") { ButtonParameter = BtnId.CamFirstPerson });
            Add(new NiceButton(contentX + 1*(camW+8), y, camW, 22, ButtonAction.Activate, "3rd Person") { ButtonParameter = BtnId.CamThirdPerson });
            Add(new NiceButton(contentX + 2*(camW+8), y, camW, 22, ButtonAction.Activate, "Cinematic")  { ButtonParameter = BtnId.CamCinematic });
            Add(new NiceButton(contentX + 3*(camW+8), y, camW, 22, ButtonAction.Activate, "Free Fly")   { ButtonParameter = BtnId.CamFreeFly });
            Add(new NiceButton(contentX + 4*(camW+8), y, camW, 22, ButtonAction.Activate, "Off")        { ButtonParameter = BtnId.CamOff });
            y += ROW_H + 4;
            _camModeLabel = new Label("Cam: " + _camMode.CurrentMode.ToString(),
                true, Debug3DStyle.HUE_VALUE, innerW, 1) { X = contentX, Y = y };
            Add(_camModeLabel);
            y += ROW_H + Debug3DStyle.SECTION_GAP;

            y += INNER_PAD;

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

                // Mode buttons drive RenderModeController only — every
                // legacy flag is owned by ApplyToLegacyFlags now, so the
                // gump no longer pokes Multi3DRenderer / Static3DRenderer
                // directly. One source of truth.
                case BtnId.ModeClassic2D: _renderMode.SetMode(RenderMode.Classic2D); break;
                case BtnId.ModeIso3D:     _renderMode.SetMode(RenderMode.Iso3D);     break;
                case BtnId.ModeFull3D:    _renderMode.SetMode(RenderMode.Full3D);    break;

                case BtnId.CamFirstPerson: _camMode.SetMode(CameraMode.FirstPerson); break;
                case BtnId.CamThirdPerson: _camMode.SetMode(CameraMode.ThirdPerson); break;
                case BtnId.CamCinematic:   _camMode.SetMode(CameraMode.Cinematic);   break;
                case BtnId.CamFreeFly:     _camMode.SetMode(CameraMode.FreeFly);     break;
                case BtnId.CamOff:         _camMode.SetMode(CameraMode.Off);         break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (_modeLabel != null)
                _modeLabel.Text = "Mode: " + _renderMode.CurrentLabel;
            if (_camModeLabel != null)
                _camModeLabel.Text = "Cam: " + _camMode.CurrentMode.ToString();
        }
    }
}
