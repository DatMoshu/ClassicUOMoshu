// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — world-level 3D rendering toggles split out from
// the monolithic Debug3DGump. Owns the WORLD section: heightmap /
// projection / wireframe / depth-test / RT toggles / vendor settings
// / mesh alpha / height exaggeration / WASD camera-relative input.
//
// Render-quality state migrated to IRenderQualityService (ADR-012):
// the gump no longer touches World3DRenderer.* for its toggles & sliders.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Camera;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WorldRenderGump : Gump
    {
        public static WorldRenderGump Instance;

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
            public const int OpenRtDiag = 10;
        }

        private readonly IRenderQualityService _quality;
        private readonly IMulti3DConfigService _multi;
        private readonly IMovementInputService _movement;
        private readonly ILegacyRendererDemoService _demo;
        private readonly ICameraModeService _camMode;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        public WorldRenderGump(World world)
            : this(world,
                Renderer3DHost.Services.RenderQuality,
                Renderer3DHost.Services.Multi3DConfig,
                Renderer3DHost.Services.MovementInput,
                Renderer3DHost.Services.LegacyRendererDemo,
                Renderer3DHost.Services.CameraMode) { }

        public WorldRenderGump(World world,
                               IRenderQualityService quality,
                               IMulti3DConfigService multi,
                               IMovementInputService movement,
                               ILegacyRendererDemoService demo,
                               ICameraModeService camMode)
            : base(world, 0, 0)
        {
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
            _multi = multi ?? throw new ArgumentNullException(nameof(multi));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _demo = demo ?? throw new ArgumentNullException(nameof(demo));
            _camMode = camMode ?? throw new ArgumentNullException(nameof(camMode));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;
            Instance = this;

            int y = Debug3DStyle.BuildShell(this, W, "WORLD / HEIGHTMAP",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- World render toggles ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WORLD", y);
            y = AddTwoColCheck(
                "Heightmap (3D world)", _quality.MasterEnabled, v => _quality.SetMasterEnabled(v),
                "Iso projection", _quality.UseIsoProjection, v => _quality.SetUseIsoProjection(v),
                y);
            y = AddTwoColCheck(
                "Depth test", _quality.DepthTestEnabled, v => _quality.SetDepthTestEnabled(v),
                "Wireframe", _quality.Wireframe, v => _quality.SetWireframe(v),
                y);
            y = AddTwoColCheck(
                "Verbose log", _quality.VerboseLog, v => _quality.SetVerboseLog(v),
                "Hide test cube", _quality.HideTestCube, v => _quality.SetHideTestCube(v),
                y);
            // NPC + RT'ed-mobiles toggles moved to Npc3DDebugGump ([npc3d) so
            // there's one canonical home for every NPC-3D switch. The "RT'ed
            // mobiles" master toggle is now driven by RenderModeController per
            // mode + the cross-mode MobilesIn3D flag — manually flipping it
            // from this gump caused mode/flag desync, so it's gone.
            y = AddTwoColCheck(
                "Disable 2D world", _quality.Disable2DWorld, v => _quality.SetDisable2DWorld(v),
                "", false, null,
                y);
            y = AddTwoColCheck(
                "Skip 2D lights in 3D", _quality.Disable2DLightingIn3D,
                    v => _quality.SetDisable2DLightingIn3D(v),
                "", false, null,
                y);
            y = AddTwoColCheck(
                "Hide roof when inside", _multi.HideAbovePlayerZ,
                    v => _multi.SetHideAbovePlayerZ(v),
                "", false, null,
                y);
            y = AddTwoColCheck(
                "Head mesh smoke (.umesh)", _demo.HeadMeshEnabled,
                    v => _demo.SetHeadMeshEnabled(v),
                "Spin head mesh", _demo.HeadMeshSpinDegPerSec > 0f,
                    v => _demo.SetHeadMeshSpinDegPerSec(v ? 30f : 0f),
                y);
            y = AddSlider("Mesh alpha (%)", 10, 100, (int)(_quality.MeshAlpha * 100), y,
                v => _quality.SetMeshAlpha(v / 100f));
            y = AddSlider("Height exag (x10)", 1, 100, (int)(_quality.HeightExaggeration * 10), y,
                v => _quality.SetHeightExaggeration(v / 10f));
            y += Debug3DStyle.SECTION_GAP;

            // NPCs section removed — see Npc3DDebugGump ([npc3d) for the
            // single home of NPC-3D toggles, mobile-3D index controls, the
            // nearby-NPC inspector, and the mobiles-wireframe toggle.

            // ---------- Vendors (legacy single-shared-mesh path; superseded by NPC pipeline) ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "VENDORS (legacy)", y);
            y = AddTwoColCheck(
                "Link vendor scale to player", _demo.VendorLinkScaleToPlayer,
                    v => _demo.SetVendorLinkScaleToPlayer(v),
                "", false, null,
                y);
            y = AddSlider("Vendor scale (x10)", 5, 2000, (int)(_demo.VendorModelScale * 10), y,
                v => _demo.SetVendorModelScale(v / 10f));
            y = AddSlider("Vendor yaw (deg)", -180, 180, (int)_demo.VendorModelYawDegrees, y,
                v => _demo.SetVendorModelYawDegrees(v));
            y += Debug3DStyle.SECTION_GAP;

            // ---------- WASD movement ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WASD CAMERA-RELATIVE INPUT", y);
            y = AddTwoColCheck(
                "WASD walk", _movement.Enabled, v => _movement.SetEnabled(v),
                "Arrow keys", _movement.BindArrows, v => _movement.SetBindArrows(v),
                y);
            y = AddTwoColCheck(
                "Run w/ Shift", _movement.RunWithShift, v => _movement.SetRunWithShift(v),
                "Only in cam mode", _movement.OnlyWhenCameraModeActive,
                    v => _movement.SetOnlyWhenCameraModeActive(v),
                y);
            y = AddTwoColCheck(
                "FreeFly drives chunks", _camMode.FreeFlyDrivesChunks,
                    v => _camMode.SetFreeFlyDrivesChunks(v),
                "", false, null,
                y);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- RT debug entry ----------
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate,
                "RT debug (resolution / framing)...") { ButtonParameter = BtnId.OpenRtDiag });
            y += ROW_H + INNER_PAD;

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
                case BtnId.OpenRtDiag:
                    if (Debug3DRTGump.Instance == null)
                    {
                        var g = new Debug3DRTGump(World);
                        Debug3DRTGump.Instance = g;
                        Game.Managers.UIManager.Add(g);
                    }
                    else
                    {
                        Debug3DRTGump.Instance.SetInScreen();
                        Debug3DRTGump.Instance.BringOnTop();
                    }
                    break;
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
