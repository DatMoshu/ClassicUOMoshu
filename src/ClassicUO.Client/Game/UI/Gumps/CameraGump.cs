// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — dedicated camera settings form.
// Pitch / yaw / zoom / offsets / projection mode / mouse rotation toggles
// for both iso and perspective 3D modes. Sliders here mirror Camera3D's
// public state directly.
//
// MIGRATION (ADR-012 §6 / playbook §Q): partial migration — the realtime-lighting
// portion (10 touch points) injects ILightingService. Camera3D + CameraModeController
// (~73 touch points) stay on legacy facades — those systems are not yet migrated.

using System;
using System.Text;
using ClassicUO.Assets;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Atmosphere;
using ClassicUO.Renderer.Camera;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Environment;
using ClassicUO.Renderer.World;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class CameraGump : Gump
    {
        public static CameraGump Instance;

        private readonly ILightingService _lighting;
        private readonly ICameraStateService _camState;
        private readonly IEnvironmentService _env;
        private readonly IRenderQualityService _quality;
        private readonly IRenderDiagnosticsService _diag;
        private readonly ICameraInputService _input;
        private readonly IMovementInputService _movement;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private void Reopen()
        {
            var w = World;
            Dispose();
            var g = new CameraGump(w, _lighting, _camState, _env, _quality, _diag, _input, _movement);
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        private const int W = 480;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int LABEL_W     = 160;
        private const int TITLE_BAR_H = Debug3DStyle.TITLE_BAR_H;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;

        private Label _statusLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        /// <summary>
        /// Convenience overload that resolves <see cref="ILightingService"/> from the active
        /// renderer service container.
        /// </summary>
        public CameraGump(World world)
            : this(world, Renderer3DHost.Services.Lighting, Renderer3DHost.Services.CameraState,
                   Renderer3DHost.Services.Environment,
                   Renderer3DHost.Services.RenderQuality,
                   Renderer3DHost.Services.RenderDiagnostics,
                   Renderer3DHost.Services.CameraInput,
                   Renderer3DHost.Services.MovementInput) { }

        public CameraGump(World world, ILightingService lighting, ICameraStateService camState,
                          IEnvironmentService env,
                          IRenderQualityService quality,
                          IRenderDiagnosticsService diag,
                          ICameraInputService input,
                          IMovementInputService movement)
            : base(world, 0, 0)
        {
            _lighting = lighting ?? throw new ArgumentNullException(nameof(lighting));
            _camState = camState ?? throw new ArgumentNullException(nameof(camState));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
            _diag = diag ?? throw new ArgumentNullException(nameof(diag));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "CAMERA SETTINGS",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // Projection / heightmap toggles moved to WorldRenderGump — this
            // form owns camera transform sliders only (no duplication).

            // ---------- Camera ----------
            y = AddSectionHeader("CAMERA", y);
            y = AddSlider("Pitch (deg)", 0, 89, (int)_camState.PitchDegrees, y,
                v => _camState.SetPitchDegrees(v));
            y = AddSlider("Yaw (deg)", 0, 359, (int)_camState.YawDegrees, y,
                v => _camState.SetYawDegrees(v));
            y = AddSlider("Zoom (x10)", 1, 50, (int)(_camState.Zoom * 10), y,
                v => _camState.SetZoom(v / 10f));
            y = AddSlider("Ortho width (px)", 200, 5000, (int)_camState.OrthoWidthPixels, y,
                v => _camState.SetOrthoWidthPixels(v));
            y = AddSlider("Near plane (ortho)",  -4096, 0, (int)_camState.NearPlane, y,
                v => _camState.SetNearPlane(v));
            y = AddSlider("Far plane (ortho)",   0,  32768, (int)_camState.FarPlane,  y,
                v => _camState.SetFarPlane(v));
            y += SECTION_GAP;

            // ---------- Perspective ----------
            y = AddSectionHeader("PERSPECTIVE (free-cam only — Iso projection OFF)", y);
            y = AddTwoColCheck(
                "Use perspective", _camState.UsePerspective,
                    v => _camState.SetUsePerspective(v),
                "", false, null,
                y);
            y = AddSlider("FOV (deg)", 20, 120, (int)_camState.FovDegrees, y,
                v => _camState.SetFovDegrees(v));
            y = AddSlider("Near (persp)", 1, 1024, (int)_camState.PerspectiveNear, y,
                v => _camState.SetPerspectiveNear(v));
            y = AddSlider("Far (persp)", 1024, 32768, (int)_camState.PerspectiveFar, y,
                v => _camState.SetPerspectiveFar(v));
            y += SECTION_GAP;

            // ---------- Render distance ----------
            y = AddSectionHeader("RENDER DISTANCE", y);
            y = AddSlider("Chunk radius (x10)", 10, 80, (int)(_quality.RenderDistanceMultiplier * 10), y,
                v => _quality.SetRenderDistanceMultiplier(v / 10f));
            y += SECTION_GAP;

            // ---------- Fog & background ----------
            y = AddSectionHeader("FOG  +  BACKGROUND", y);
            y = AddTwoColCheck(
                "Fog enabled", _env.FogEnabled,
                    v => _env.SetFogEnabled(v),
                "", false, null,
                y);
            y = AddSlider("Fog start", 0, 8192, (int)_env.FogStart, y,
                v => _env.SetFogStart(v));
            y = AddSlider("Fog end",   0, 16384, (int)_env.FogEnd, y,
                v => _env.SetFogEnd(v));
            y = AddSlider("Fog R", 0, 255, _env.FogColor.R, y, v =>
            {
                var c = _env.FogColor; _env.SetFogColor(new Color((byte)v, c.G, c.B, c.A));
            });
            y = AddSlider("Fog G", 0, 255, _env.FogColor.G, y, v =>
            {
                var c = _env.FogColor; _env.SetFogColor(new Color(c.R, (byte)v, c.B, c.A));
            });
            y = AddSlider("Fog B", 0, 255, _env.FogColor.B, y, v =>
            {
                var c = _env.FogColor; _env.SetFogColor(new Color(c.R, c.G, (byte)v, c.A));
            });
            y = AddSlider("BG R", 0, 255, _env.BackgroundColor.R, y, v =>
            {
                var c = _env.BackgroundColor; _env.SetBackgroundColor(new Color((byte)v, c.G, c.B, c.A));
            });
            y = AddSlider("BG G", 0, 255, _env.BackgroundColor.G, y, v =>
            {
                var c = _env.BackgroundColor; _env.SetBackgroundColor(new Color(c.R, (byte)v, c.B, c.A));
            });
            y = AddSlider("BG B", 0, 255, _env.BackgroundColor.B, y, v =>
            {
                var c = _env.BackgroundColor; _env.SetBackgroundColor(new Color(c.R, c.G, (byte)v, c.A));
            });
            y += SECTION_GAP;

            // ---------- Offsets ----------
            y = AddSectionHeader("CAMERA OFFSET (world units)", y);
            y = AddSlider("Offset X", -2000, 2000, (int)_quality.CameraOffset.X, y, v =>
            {
                var o = _quality.CameraOffset; o.X = v; _quality.SetCameraOffset(o);
            });
            y = AddSlider("Offset Y", -2000, 2000, (int)_quality.CameraOffset.Y, y, v =>
            {
                var o = _quality.CameraOffset; o.Y = v; _quality.SetCameraOffset(o);
            });
            y = AddSlider("Offset Z", -2000, 2000, (int)_quality.CameraOffset.Z, y, v =>
            {
                var o = _quality.CameraOffset; o.Z = v; _quality.SetCameraOffset(o);
            });
            y += SECTION_GAP;

            // ---------- Mouse control ----------
            y = AddSectionHeader("MOUSE LOOK (perspective only)", y);
            y = AddTwoColCheck(
                "MMB rotate", _input.MiddleMouseRotate,
                    v => _input.SetMiddleMouseRotate(v),
                "RMB look",   _input.RightMouseLook,
                    v => _input.SetRightMouseLook(v),
                y);
            y = AddTwoColCheck(
                "Invert X", _input.InvertX,
                    v => _input.SetInvertX(v),
                "Invert Y", _input.InvertY,
                    v => _input.SetInvertY(v),
                y);
            y = AddSlider("Sens X (x100)", 1, 100, (int)(_input.SensitivityX * 100), y,
                v => _input.SetSensitivityX(v / 100f));
            y = AddSlider("Sens Y (x100)", 1, 100, (int)(_input.SensitivityY * 100), y,
                v => _input.SetSensitivityY(v / 100f));
            y = AddSlider("Sens mul 3rd (x100)",   10, 300, (int)(_input.SensMul_ThirdPerson * 100), y,
                v => _input.SetSensMul_ThirdPerson(v / 100f));
            y = AddSlider("Sens mul 1st (x100)",   10, 300, (int)(_input.SensMul_FirstPerson * 100), y,
                v => _input.SetSensMul_FirstPerson(v / 100f));
            y = AddSlider("Sens mul Fly (x100)",   10, 300, (int)(_input.SensMul_FreeFly * 100), y,
                v => _input.SetSensMul_FreeFly(v / 100f));
            y += SECTION_GAP;

            // ---------- Movement (WASD) ----------
            y = AddSectionHeader("MOVEMENT (WASD / arrows)", y);
            y = AddTwoColCheck(
                "WASD enabled", _movement.Enabled,
                    v => _movement.SetEnabled(v),
                "Only in 3D modes", _movement.OnlyWhenCameraModeActive,
                    v => _movement.SetOnlyWhenCameraModeActive(v),
                y);
            y = AddTwoColCheck(
                "Bind WASD", _movement.BindWasd,
                    v => _movement.SetBindWasd(v),
                "Bind arrows", _movement.BindArrows,
                    v => _movement.SetBindArrows(v),
                y);
            y = AddTwoColCheck(
                "Shift = run", _movement.RunWithShift,
                    v => _movement.SetRunWithShift(v),
                "Verbose log", _movement.VerboseLog,
                    v => _movement.SetVerboseLog(v),
                y);
            y += SECTION_GAP;

            // ---------- Lighting (Phase 1: sun toggle + arc) ----------
            // See design/gdd/realtime-lighting-3d.md and day-night-cycle.md.
            // Slider integers are scaled because Debug3DStyle.AddSlider is
            // int-only: SunTime uses 0..240 (÷10 → 0.0..24.0h), period uses
            // 10..600 seconds. Each onChange writes back to the active Profile
            // so settings persist across relog (see Profile.cs).
            y = AddSectionHeader("LIGHTING (3D — sun direction only)", y);
            y = AddTwoColCheck(
                "Realtime lighting", _lighting.Enabled,
                    v => { _lighting.SetEnabled(v);   _lighting.SaveToProfile(); },
                "Auto sun cycle", _lighting.AutoCycle,
                    v => { _lighting.SetAutoCycle(v); _lighting.SaveToProfile(); },
                y);
            y = AddSlider("Sun time (x10 hr)", 0, 240, (int)(_lighting.TimeOfDay * 10f), y, v =>
            {
                _lighting.SetTimeOfDay(v / 10f);
                _lighting.SaveToProfile();
            });
            y = AddSlider("Cycle period (s)", 10, 600, (int)_lighting.CyclePeriodSeconds, y, v =>
            {
                _lighting.SetCyclePeriodSeconds(v);
                _lighting.SaveToProfile();
            });
            y += SECTION_GAP;

            // ---------- Diagnostics ----------
            y = AddSectionHeader("STATE", y);
            _statusLabel = new Label("(loading…)", true, HUE_VALUE, innerW, 4) { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 4;

            // ---------- Footer ----------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            int btnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Reset camera") { ButtonParameter = 1 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Refresh form") { ButtonParameter = 2 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Dump Values to Console") { ButtonParameter = 3 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Stats...")
                { ButtonParameter = 4 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Gizmo...")
                { ButtonParameter = 5 });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 880;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE:
                    Dispose();
                    break;
                case 1:
                    _camState.SetPitchDegrees(30f);
                    _camState.SetYawDegrees(45f);
                    _camState.SetZoom(1f);
                    _quality.SetCameraOffset(Vector3.Zero);
                    Reopen();
                    break;
                case 2:
                    Reopen();
                    break;
                case 3:
                    Console.WriteLine(BuildDumpString());
                    break;
                case 4:
                    OpenSingleton<Stats3DGump>(w => new Stats3DGump(w));
                    break;
                case 5:
                    OpenSingleton<CameraGizmoGump>(w => new CameraGizmoGump(w));
                    break;
            }
        }

        private void OpenSingleton<T>(System.Func<World, T> ctor) where T : Gump
        {
            foreach (var g in Game.Managers.UIManager.Gumps)
            {
                if (g is T existing && !existing.IsDisposed)
                {
                    existing.SetInScreen();
                    existing.BringOnTop();
                    return;
                }
            }
            var fresh = ctor(World);
            Game.Managers.UIManager.Add(fresh);
        }

        // Non-static so it can read injected _camState.
        private string BuildDumpString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== [3DCUO] CAMERA VALUES ====================");
            sb.AppendLine($"  UseIsoProjection           = {_quality.UseIsoProjection}");
            sb.AppendLine($"  Disable2DWorld             = {_quality.Disable2DWorld}");
            sb.AppendLine($"  DepthTestEnabled           = {_quality.DepthTestEnabled}");
            sb.AppendLine($"  Heightmap (3D world)       = {_quality.MasterEnabled}");
            sb.AppendLine($"  Camera.PitchDegrees        = {_camState.PitchDegrees:F2}");
            sb.AppendLine($"  Camera.YawDegrees          = {_camState.YawDegrees:F2}");
            sb.AppendLine($"  Camera.Zoom                = {_camState.Zoom:F3}");
            sb.AppendLine($"  Camera.OrthoWidthPixels    = {_camState.OrthoWidthPixels}");
            sb.AppendLine($"  Camera.NearPlane (ortho)   = {_camState.NearPlane}");
            sb.AppendLine($"  Camera.FarPlane  (ortho)   = {_camState.FarPlane}");
            sb.AppendLine($"  Camera.UsePerspective      = {_camState.UsePerspective}");
            sb.AppendLine($"  Camera.FovDegrees          = {_camState.FovDegrees:F2}");
            sb.AppendLine($"  Camera.PerspectiveNear     = {_camState.PerspectiveNear}");
            sb.AppendLine($"  Camera.PerspectiveFar      = {_camState.PerspectiveFar}");
            sb.AppendLine($"  RenderDistanceMultiplier   = {_quality.RenderDistanceMultiplier:F2}");
            sb.AppendLine($"  FogEnabled                 = {_env.FogEnabled}");
            sb.AppendLine($"  FogStart / FogEnd          = {_env.FogStart} / {_env.FogEnd}");
            sb.AppendLine($"  FogColor                   = {_env.FogColor}");
            sb.AppendLine($"  BackgroundColor            = {_env.BackgroundColor}");
            sb.AppendLine($"  CameraOffset               = {_quality.CameraOffset}");
            sb.AppendLine($"  MMB rotate / RMB look      = {_input.MiddleMouseRotate} / {_input.RightMouseLook}");
            sb.AppendLine($"  Invert X / Y               = {_input.InvertX} / {_input.InvertY}");
            sb.AppendLine($"  Sensitivity X / Y          = {_input.SensitivityX:F2} / {_input.SensitivityY:F2}");
            sb.AppendLine($"  Sens mul 3rd/1st/Fly       = {_input.SensMul_ThirdPerson:F2} / {_input.SensMul_FirstPerson:F2} / {_input.SensMul_FreeFly:F2}");
            sb.AppendLine($"  WASD Enabled               = {_movement.Enabled} (mode-gated: {_movement.OnlyWhenCameraModeActive})");
            sb.Append(    "==============================================================");
            return sb.ToString();
        }

        public override void Update()
        {
            base.Update();
            if (_statusLabel != null)
            {
                _statusLabel.Text =
                    $"target  {_diag.LastCameraTarget}\n" +
                    $"pitch={_camState.PitchDegrees:F1}  yaw={_camState.YawDegrees:F1}  zoom={_camState.Zoom:F2}\n" +
                    $"chunks  vis {_diag.LastVisibleChunks}  drawn {_diag.LastDrawnChunks}\n" +
                    $"projection: {(_quality.UseIsoProjection ? "ISO" : "PERSPECTIVE")}";
            }
        }

        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
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
