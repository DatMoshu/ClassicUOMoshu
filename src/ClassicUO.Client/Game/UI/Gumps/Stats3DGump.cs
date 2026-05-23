// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — perf HUD for the 3D world pass.
// Displays draw calls, primitives, texture/target switches, frame ms,
// chunk counts, camera state, and player tile coords. Refreshes every 250 ms.
//
// MIGRATION (ADR-012 §6 / playbook §Q): camera-mode label injects
// ICameraModeService. World3DRenderer reads stay legacy.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Utility;
using ClassicUO.Renderer.Camera;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class Stats3DGump : Gump
    {
        public static Stats3DGump Instance;

        private readonly ICameraModeService _camMode;
        private readonly ICameraStateService _camState;
        private readonly IRenderQualityService _quality;
        private readonly IRenderDiagnosticsService _diag;

        private const int W = 360;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _frameLabel;
        private Label _gpuLabel;
        private Label _chunkLabel;
        private Label _cameraLabel;
        private Label _playerLabel;

        private long _nextRefresh;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        public Stats3DGump(World world)
            : this(world, Renderer3DHost.Services.CameraMode,
                   Renderer3DHost.Services.CameraState,
                   Renderer3DHost.Services.RenderQuality,
                   Renderer3DHost.Services.RenderDiagnostics) { }

        public Stats3DGump(World world,
            ICameraModeService camMode,
            ICameraStateService camState,
            IRenderQualityService quality,
            IRenderDiagnosticsService diag) : base(world, 0, 0)
        {
            _camMode = camMode ?? throw new ArgumentNullException(nameof(camMode));
            _camState = camState ?? throw new ArgumentNullException(nameof(camState));
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
            _diag    = diag    ?? throw new ArgumentNullException(nameof(diag));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3D  STATS",
                out _outerBg, out _innerBg, out _borders);
            int contentX = Debug3DStyle.GetContentX();
            int innerW   = Debug3DStyle.GetContentWidth(W);

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "FRAME", y);
            _frameLabel = new Label("", true, Debug3DStyle.HUE_OK, innerW, 1) { X = contentX, Y = y };
            Add(_frameLabel);
            y += ROW_H;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "GROUND PASS (last frame)", y);
            _gpuLabel = new Label("", true, Debug3DStyle.HUE_VALUE, innerW, 2) { X = contentX, Y = y };
            Add(_gpuLabel);
            y += ROW_H * 2;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "CHUNKS", y);
            _chunkLabel = new Label("", true, Debug3DStyle.HUE_VALUE, innerW, 1) { X = contentX, Y = y };
            Add(_chunkLabel);
            y += ROW_H;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "CAMERA", y);
            _cameraLabel = new Label("", true, Debug3DStyle.HUE_VALUE, innerW, 3) { X = contentX, Y = y };
            Add(_cameraLabel);
            y += ROW_H * 3;

            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "PLAYER", y);
            _playerLabel = new Label("", true, Debug3DStyle.HUE_VALUE, innerW, 1) { X = contentX, Y = y };
            Add(_playerLabel);
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 30;
            Y = 60;
            WantUpdateSize = false;

            RefreshLabels();
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == Debug3DStyle.BTN_CLOSE) Dispose();
        }

        public override void Update()
        {
            base.Update();
            if (Time.Ticks >= _nextRefresh)
            {
                _nextRefresh = Time.Ticks + 250;
                RefreshLabels();
            }
        }

        private void RefreshLabels()
        {
            uint fps = CUOEnviroment.CurrentRefreshRate;
            float frameMs = fps > 0 ? 1000f / fps : 0f;
            _frameLabel.Text = $"FPS {fps}    Frame {frameMs:F2} ms";

            _gpuLabel.Text =
                $"DrawCalls   {_diag.LastDrawCalls}    (ground only)\n" +
                $"Primitives  {_diag.LastPrimitives}";

            _chunkLabel.Text =
                $"vis {_diag.LastVisibleChunks}    built {_diag.LastBuiltChunks}    drawn {_diag.LastDrawnChunks}";

            _cameraLabel.Text =
                $"mode    {_camMode.Label()}\n" +
                $"pitch={_camState.PitchDegrees:F1}  yaw={_camState.YawDegrees:F1}  zoom={_camState.Zoom:F2}\n" +
                $"proj    {(_quality.UseIsoProjection ? "ISO" : "PERSP")}    fov={_camState.FovDegrees:F0}    eye-dist={_camState.EyeDistance:F0}";

            var p = World?.Player;
            if (p != null)
            {
                _playerLabel.Text = $"tile  X={p.X}  Y={p.Y}  Z={p.Z}    facing={(byte)p.Direction}";
            }
            else
            {
                _playerLabel.Text = "(no player)";
            }
        }
    }
}
