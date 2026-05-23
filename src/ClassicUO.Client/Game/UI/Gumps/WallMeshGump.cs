// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — wall-mesh / static-3D dump tools split out from
// Debug3DGump's MULTI/STATIC section. Keeps wall-render mode toggles
// (3D Meshes / Wireframe / Shaded Wire / fallback) and the dump
// buttons (statics JSONL, wall candidates, world GLB, info+PNG).

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class WallMeshGump : Gump
    {
        public static WallMeshGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 460;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H     = Debug3DStyle.ROW_H;

        private static class BtnId
        {
            public const int DumpStatics  = 1;
            public const int DumpWalls    = 2;
            public const int DumpInfoPng  = 3;
            public const int DumpMapping  = 5;
            public const int ReloadWalls  = 6;
            public const int DumpMulti    = 7;
            public const int DumpStatic   = 8;
            public const int ExportStatic = 9;
            public const int CornerYawNW  = 10;
            public const int CornerYawNE  = 11;
            public const int CornerYawSE  = 12;
            public const int CornerYawSW  = 13;
            public const int Iris2Pitch   = 20;
            public const int Iris2Yaw     = 21;
            public const int Iris2Roll    = 22;
            public const int Iris2Step    = 23;
            public const int Iris2Reset   = 24;
        }

        private NiceButton _btnYawNW;
        private NiceButton _btnYawNE;
        private NiceButton _btnYawSE;
        private NiceButton _btnYawSW;
        private NiceButton _btnIris2Pitch;
        private NiceButton _btnIris2Yaw;
        private NiceButton _btnIris2Roll;
        private NiceButton _btnIris2Step;

        private static string YawLabel(string corner, float deg) => $"{corner} yaw: {deg,3:F0}° (click +90)";
        private static string Iris2AxisLabel(string axis, float deg, float step) => $"Iris2 {axis}: {deg,4:F0}° (+{step:F0})";
        private static string Iris2StepLabel(float step) => $"Step: {step:F0}° (toggle 90/15)";

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        private readonly IStatic3DConfigService _static;
        private readonly IMulti3DConfigService _multi;

        public WallMeshGump(World world)
            : this(world, Renderer3DHost.Services.Static3DConfig,
                   Renderer3DHost.Services.Multi3DConfig) { }

        public WallMeshGump(World world, IStatic3DConfigService staticCfg, IMulti3DConfigService multiCfg)
            : base(world, 0, 0)
        {
            _static = staticCfg ?? throw new ArgumentNullException(nameof(staticCfg));
            _multi = multiCfg ?? throw new ArgumentNullException(nameof(multiCfg));
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;
            Instance = this;

            int y = Debug3DStyle.BuildShell(this, W, "WALLS (3D MESHES)",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Wall render mode ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "WALL RENDER MODE", y);
            y = AddTwoColCheck(
                "Walls: 3D Meshes", _multi.Use3DWallMeshes,
                    v => _multi.SetUse3DWallMeshes(v),
                "Show wall fallback", _multi.ShowFallbackForUnknownWalls,
                    v => _multi.SetShowFallbackForUnknownWalls(v),
                y);
            y = AddTwoColCheck(
                "Walls: Wireframe", WallMeshDrawer.Wireframe,
                    v => WallMeshDrawer.Wireframe = v,
                "Walls: Shaded Wire", WallMeshDrawer.ShadedWireframe,
                    v => WallMeshDrawer.ShadedWireframe = v,
                y);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Iris 2 static meshes ----------
            // Routes Static graphics through Iris2StaticRegistry → Iris2StaticDrawer.
            // Walls still win first (wall registry checked before iris2). GPLv3-tainted
            // reference assets — debug-only, never ship.
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "IRIS 2 STATICS (DEBUG)", y);
            y = AddTwoColCheck(
                "Iris2: 3D Statics", _static.Use3DIris2Statics,
                    v => _static.SetUse3DIris2Statics(v),
                "Iris2: Wireframe", Iris2StaticDrawer.Wireframe,
                    v => Iris2StaticDrawer.Wireframe = v,
                y);
            // 100% Iris preview — overrides walls + multi components anywhere the
            // registry has a hit. Anything not in the registry still uses its
            // normal renderer, so the world is partially Iris / partially classic.
            y = AddTwoColCheck(
                "Iris2: Override All", _static.Iris2OverrideAll,
                    v => _static.SetIris2OverrideAll(v),
                "(walls + multis → Iris2)", false, _ => { },
                y);
            // Global axis-fix tweakers. Click cycles +Step (90° default, 15° fine).
            // Reset zeroes all three axes. Use these to find the right Z-up→Y-up
            // fix for the Iris 2 GLB pool, then bake into a converter pass.
            int iBtnW = (innerW - 10) / 2;
            _btnIris2Pitch = new NiceButton(contentX,                  y, iBtnW, 22, ButtonAction.Activate,
                Iris2AxisLabel("Pitch (X)", Iris2StaticDrawer.GlobalPitchDeg, Iris2StaticDrawer.YawStepDeg)) { ButtonParameter = BtnId.Iris2Pitch };
            _btnIris2Yaw   = new NiceButton(contentX + iBtnW + 10,      y, iBtnW, 22, ButtonAction.Activate,
                Iris2AxisLabel("Yaw (Y)",   Iris2StaticDrawer.GlobalYawDeg,   Iris2StaticDrawer.YawStepDeg)) { ButtonParameter = BtnId.Iris2Yaw };
            Add(_btnIris2Pitch); Add(_btnIris2Yaw);
            y += ROW_H + 4;
            _btnIris2Roll  = new NiceButton(contentX,                  y, iBtnW, 22, ButtonAction.Activate,
                Iris2AxisLabel("Roll (Z)",  Iris2StaticDrawer.GlobalRollDeg,  Iris2StaticDrawer.YawStepDeg)) { ButtonParameter = BtnId.Iris2Roll };
            _btnIris2Step  = new NiceButton(contentX + iBtnW + 10,      y, iBtnW, 22, ButtonAction.Activate,
                Iris2StepLabel(Iris2StaticDrawer.YawStepDeg)) { ButtonParameter = BtnId.Iris2Step };
            Add(_btnIris2Roll); Add(_btnIris2Step);
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, iBtnW, 22, ButtonAction.Activate, "Iris2: Reset Rotation") { ButtonParameter = BtnId.Iris2Reset });
            y += ROW_H + 4;
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Verbosity ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "VERBOSITY", y);
            y = AddTwoColCheck(
                "Multi verbose", _multi.VerboseLog, v => _multi.SetVerboseLog(v),
                "Static verbose", _static.VerboseLog, v => _static.SetVerboseLog(v),
                y);
            y += Debug3DStyle.SECTION_GAP;

            // ---------- Dump tools ----------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "DUMP TOOLS", y);
            int btnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate, "Dump Statics (JSONL)") { ButtonParameter = BtnId.DumpStatics });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Dump Wall Candidates")  { ButtonParameter = BtnId.DumpWalls });
            y += ROW_H + 4;
            Add(new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate, "Dump Info+PNG")        { ButtonParameter = BtnId.DumpInfoPng });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Dump Wall Mapping")    { ButtonParameter = BtnId.DumpMapping });
            y += ROW_H + 4;
            Add(new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate, "Reload Wall Manifest")  { ButtonParameter = BtnId.ReloadWalls });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Dump Nearest Multi")   { ButtonParameter = BtnId.DumpMulti });
            y += ROW_H + 4;
            Add(new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate, "Dump Static Bldg")     { ButtonParameter = BtnId.DumpStatic });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Export Static GLB+JSON") { ButtonParameter = BtnId.ExportStatic });
            y += ROW_H + INNER_PAD;

            // ---------- Corner yaw tweakers ----------
            // Live-cycle each corner's yaw in 90° steps. Mesh canonical
            // orientation depends on Blender export axis-conversion; iterate
            // visually until all four corners look correct.
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "CORNER YAW (click +90°)", y);
            _btnYawNW = new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate,
                YawLabel("NW", WallMeshDrawer.CornerYawNW_Deg)) { ButtonParameter = BtnId.CornerYawNW };
            _btnYawNE = new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate,
                YawLabel("NE", WallMeshDrawer.CornerYawNE_Deg)) { ButtonParameter = BtnId.CornerYawNE };
            Add(_btnYawNW); Add(_btnYawNE);
            y += ROW_H + 4;
            _btnYawSE = new NiceButton(contentX,              y, btnW, 22, ButtonAction.Activate,
                YawLabel("SE", WallMeshDrawer.CornerYawSE_Deg)) { ButtonParameter = BtnId.CornerYawSE };
            _btnYawSW = new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate,
                YawLabel("SW", WallMeshDrawer.CornerYawSW_Deg)) { ButtonParameter = BtnId.CornerYawSW };
            Add(_btnYawSE); Add(_btnYawSW);
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
                case BtnId.DumpStatics: StaticClassifyDumper.Dump(); break;
                case BtnId.DumpWalls:   WallMeshDumper.Dump(); break;
                case BtnId.DumpInfoPng: InfoDumper.Dump(Client.Game.GraphicsDevice, World); break;
                case BtnId.DumpMapping:
                    WallMappingDumper.Dump();
                    break;
                case BtnId.ReloadWalls:
                    // Drops the manifest dict + cached SkinnedModelGlb instances. Next
                    // frame's TryGet rebuilds _byGraphic from latest.txt and lazy-loads
                    // GLBs on demand. Use after rewriting the manifest on disk.
                    WallMeshRegistry.Invalidate();
                    Console.WriteLine("[3DCUO] WallMeshRegistry invalidated — manifest will reload on next render.");
                    break;
                case BtnId.DumpMulti:
                    WallMultiDumper.DumpNearest();
                    break;
                case BtnId.DumpStatic:
                    WallStaticDumper.DumpNearest();
                    break;
                case BtnId.ExportStatic:
                    WallStaticGlbExporter.ExportNearest();
                    break;
                case BtnId.CornerYawNW:
                    WallMeshDrawer.CornerYawNW_Deg = (WallMeshDrawer.CornerYawNW_Deg + 90f) % 360f;
                    if (_btnYawNW != null) _btnYawNW.TextLabel.Text = YawLabel("NW", WallMeshDrawer.CornerYawNW_Deg);
                    Console.WriteLine($"[3DCUO] CornerYawNW={WallMeshDrawer.CornerYawNW_Deg}°");
                    break;
                case BtnId.CornerYawNE:
                    WallMeshDrawer.CornerYawNE_Deg = (WallMeshDrawer.CornerYawNE_Deg + 90f) % 360f;
                    if (_btnYawNE != null) _btnYawNE.TextLabel.Text = YawLabel("NE", WallMeshDrawer.CornerYawNE_Deg);
                    Console.WriteLine($"[3DCUO] CornerYawNE={WallMeshDrawer.CornerYawNE_Deg}°");
                    break;
                case BtnId.CornerYawSE:
                    WallMeshDrawer.CornerYawSE_Deg = (WallMeshDrawer.CornerYawSE_Deg + 90f) % 360f;
                    if (_btnYawSE != null) _btnYawSE.TextLabel.Text = YawLabel("SE", WallMeshDrawer.CornerYawSE_Deg);
                    Console.WriteLine($"[3DCUO] CornerYawSE={WallMeshDrawer.CornerYawSE_Deg}°");
                    break;
                case BtnId.CornerYawSW:
                    WallMeshDrawer.CornerYawSW_Deg = (WallMeshDrawer.CornerYawSW_Deg + 90f) % 360f;
                    if (_btnYawSW != null) _btnYawSW.TextLabel.Text = YawLabel("SW", WallMeshDrawer.CornerYawSW_Deg);
                    Console.WriteLine($"[3DCUO] CornerYawSW={WallMeshDrawer.CornerYawSW_Deg}°");
                    break;
                case BtnId.Iris2Pitch:
                    Iris2StaticDrawer.GlobalPitchDeg = (Iris2StaticDrawer.GlobalPitchDeg + Iris2StaticDrawer.YawStepDeg) % 360f;
                    if (_btnIris2Pitch != null) _btnIris2Pitch.TextLabel.Text = Iris2AxisLabel("Pitch (X)", Iris2StaticDrawer.GlobalPitchDeg, Iris2StaticDrawer.YawStepDeg);
                    Console.WriteLine($"[3DCUO] Iris2 Pitch={Iris2StaticDrawer.GlobalPitchDeg}°  Yaw={Iris2StaticDrawer.GlobalYawDeg}°  Roll={Iris2StaticDrawer.GlobalRollDeg}°");
                    break;
                case BtnId.Iris2Yaw:
                    Iris2StaticDrawer.GlobalYawDeg = (Iris2StaticDrawer.GlobalYawDeg + Iris2StaticDrawer.YawStepDeg) % 360f;
                    if (_btnIris2Yaw != null) _btnIris2Yaw.TextLabel.Text = Iris2AxisLabel("Yaw (Y)", Iris2StaticDrawer.GlobalYawDeg, Iris2StaticDrawer.YawStepDeg);
                    Console.WriteLine($"[3DCUO] Iris2 Pitch={Iris2StaticDrawer.GlobalPitchDeg}°  Yaw={Iris2StaticDrawer.GlobalYawDeg}°  Roll={Iris2StaticDrawer.GlobalRollDeg}°");
                    break;
                case BtnId.Iris2Roll:
                    Iris2StaticDrawer.GlobalRollDeg = (Iris2StaticDrawer.GlobalRollDeg + Iris2StaticDrawer.YawStepDeg) % 360f;
                    if (_btnIris2Roll != null) _btnIris2Roll.TextLabel.Text = Iris2AxisLabel("Roll (Z)", Iris2StaticDrawer.GlobalRollDeg, Iris2StaticDrawer.YawStepDeg);
                    Console.WriteLine($"[3DCUO] Iris2 Pitch={Iris2StaticDrawer.GlobalPitchDeg}°  Yaw={Iris2StaticDrawer.GlobalYawDeg}°  Roll={Iris2StaticDrawer.GlobalRollDeg}°");
                    break;
                case BtnId.Iris2Step:
                    Iris2StaticDrawer.YawStepDeg = (Iris2StaticDrawer.YawStepDeg == 90f) ? 15f : 90f;
                    if (_btnIris2Step  != null) _btnIris2Step .TextLabel.Text = Iris2StepLabel(Iris2StaticDrawer.YawStepDeg);
                    if (_btnIris2Pitch != null) _btnIris2Pitch.TextLabel.Text = Iris2AxisLabel("Pitch (X)", Iris2StaticDrawer.GlobalPitchDeg, Iris2StaticDrawer.YawStepDeg);
                    if (_btnIris2Yaw   != null) _btnIris2Yaw  .TextLabel.Text = Iris2AxisLabel("Yaw (Y)",   Iris2StaticDrawer.GlobalYawDeg,   Iris2StaticDrawer.YawStepDeg);
                    if (_btnIris2Roll  != null) _btnIris2Roll .TextLabel.Text = Iris2AxisLabel("Roll (Z)",  Iris2StaticDrawer.GlobalRollDeg,  Iris2StaticDrawer.YawStepDeg);
                    Console.WriteLine($"[3DCUO] Iris2 step={Iris2StaticDrawer.YawStepDeg}°");
                    break;
                case BtnId.Iris2Reset:
                    Iris2StaticDrawer.GlobalPitchDeg = 0f;
                    Iris2StaticDrawer.GlobalYawDeg   = 0f;
                    Iris2StaticDrawer.GlobalRollDeg  = 0f;
                    if (_btnIris2Pitch != null) _btnIris2Pitch.TextLabel.Text = Iris2AxisLabel("Pitch (X)", 0f, Iris2StaticDrawer.YawStepDeg);
                    if (_btnIris2Yaw   != null) _btnIris2Yaw  .TextLabel.Text = Iris2AxisLabel("Yaw (Y)",   0f, Iris2StaticDrawer.YawStepDeg);
                    if (_btnIris2Roll  != null) _btnIris2Roll .TextLabel.Text = Iris2AxisLabel("Roll (Z)",  0f, Iris2StaticDrawer.YawStepDeg);
                    Console.WriteLine($"[3DCUO] Iris2 rotation reset");
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
    }
}
