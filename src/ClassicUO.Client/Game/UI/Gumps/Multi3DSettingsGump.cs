// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — dedicated settings form for the Multi3D / Static3D
// renderers. Exposes every static toggle/slider in one panel and provides
// a "Dump State" button that prints the full state to the console for
// regression diagnosis.
//
// MIGRATION (ADR-012 §6 / playbook §Q): partial migration — orientation-table
// portion injects IMultiOrientationService; dump-state Camera reads inject
// ICameraStateService. Multi3DRenderer + Static3DRenderer + World3DRenderer
// touch points stay on legacy facades — those systems are not yet migrated.

using System;
using ClassicUO.Assets;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Camera;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class Multi3DSettingsGump : Gump
    {
        public static Multi3DSettingsGump Instance;

        private readonly IMultiOrientationService _orient;
        private readonly ICameraStateService _camState;
        private readonly IRenderQualityService _quality;
        private readonly IRenderDiagnosticsService _diag;
        private readonly IStatic3DConfigService _static;
        private readonly IStatic3DDiagnosticsService _staticDiag;
        private readonly IMulti3DConfigService _multi;
        private readonly IMulti3DDiagnosticsService _multiDiag;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private void Reopen()
        {
            var w = World;
            Dispose();
            var g = new Multi3DSettingsGump(w, _orient, _camState, _quality, _diag, _static, _staticDiag, _multi, _multiDiag);
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        private const int W = 500;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int LABEL_W     = 170;
        private const int TITLE_BAR_H = Debug3DStyle.TITLE_BAR_H;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;

        private Label _statsLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;
        private Label _exportStatusLabel;
        // Default to 0x76 (118) "Two Story Wood and Plaster House" — the user's
        // requested test multi from the Multi-Renderer feedback.
        private static int _exportMultiId = 0x76;

        /// <summary>
        /// Convenience overload that resolves <see cref="IMultiOrientationService"/> from
        /// the active renderer service container.
        /// </summary>
        public Multi3DSettingsGump(World world)
            : this(world, Renderer3DHost.Services.MultiOrientation,
                   Renderer3DHost.Services.CameraState,
                   Renderer3DHost.Services.RenderQuality,
                   Renderer3DHost.Services.RenderDiagnostics,
                   Renderer3DHost.Services.Static3DConfig,
                   Renderer3DHost.Services.Static3DDiagnostics,
                   Renderer3DHost.Services.Multi3DConfig,
                   Renderer3DHost.Services.Multi3DDiagnostics) { }

        public Multi3DSettingsGump(World world, IMultiOrientationService orient,
                                   ICameraStateService camState,
                                   IRenderQualityService quality,
                                   IRenderDiagnosticsService diag,
                                   IStatic3DConfigService staticCfg,
                                   IStatic3DDiagnosticsService staticDiag,
                                   IMulti3DConfigService multiCfg,
                                   IMulti3DDiagnosticsService multiDiag)
            : base(world, 0, 0)
        {
            _orient = orient ?? throw new ArgumentNullException(nameof(orient));
            _camState = camState ?? throw new ArgumentNullException(nameof(camState));
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
            _diag = diag ?? throw new ArgumentNullException(nameof(diag));
            _static = staticCfg ?? throw new ArgumentNullException(nameof(staticCfg));
            _staticDiag = staticDiag ?? throw new ArgumentNullException(nameof(staticDiag));
            _multi = multiCfg ?? throw new ArgumentNullException(nameof(multiCfg));
            _multiDiag = multiDiag ?? throw new ArgumentNullException(nameof(multiDiag));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "MULTI3D / STATIC3D SETTINGS",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Multi3D ----------
            y = AddSectionHeader("MULTI3D (axis-aligned walls/floors)", y);
            y = AddTwoColCheck(
                "Enabled", _multi.Enabled, v => _multi.SetEnabled(v),
                "Verbose log", _multi.VerboseLog, v => _multi.SetVerboseLog(v),
                y);
            y = AddTwoColCheck(
                "Wall fallback (magenta)", _multi.ShowFallbackForUnknownWalls,
                    v => _multi.SetShowFallbackForUnknownWalls(v),
                "Test quad@player", _multi.ForceTestQuadAtPlayer,
                    v => _multi.SetForceTestQuadAtPlayer(v),
                y);
            y = AddTwoColCheck(
                "Deskew texture (TODO)", _multi.DeskewMultiTexture,
                    v => _multi.SetDeskewMultiTexture(v),
                "", false, null,
                y);
            y += SECTION_GAP;

            // ---------- Static3D ----------
            y = AddSectionHeader("STATIC3D (camera-facing billboards)", y);
            y = AddTwoColCheck(
                "Enabled", _static.Enabled, v => _static.SetEnabled(v),
                "Verbose log", _static.VerboseLog, v => _static.SetVerboseLog(v),
                y);
            y = AddTwoColCheck(
                "Billboard ALL statics", _static.BillboardAllStatics,
                    v => _static.SetBillboardAllStatics(v),
                "", false, null,
                y);
            y = AddSlider("Alpha cutoff (0..255)", 0, 255, _static.AlphaCutoff, y,
                v => _static.SetAlphaCutoff(v));
            y += SECTION_GAP;

            // World-context toggles moved to WorldRenderGump (single home).

            // ---------- Diagnostics ----------
            y = AddSectionHeader("DIAGNOSTICS (last frame)", y);
            _statsLabel = new Label("(loading…)", true, HUE_VALUE, innerW, 6)
                { X = contentX, Y = y };
            Add(_statsLabel);
            y += ROW_H * 6 + INNER_PAD;

            // ---------- GLB export ----------
            y = AddSectionHeader("EXPORT MULTI -> GLB", y);
            y = AddSlider("Multi ID (decimal)", 1, 4096, _exportMultiId, y, v => _exportMultiId = v);
            _exportStatusLabel = new Label("(idle)", true, HUE_VALUE, innerW - 20, 2)
                { X = contentX, Y = y };
            Add(_exportStatusLabel);
            y += ROW_H * 2;

            int btnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, $"Export multi 0x{_exportMultiId:X4} to GLB")
                { ButtonParameter = 5 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Open exports folder")
                { ButtonParameter = 6 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Dump multi components")
                { ButtonParameter = 7 });
            y += ROW_H;

            // ---------- Footer ----------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Dump Values to Console")
                { ButtonParameter = 1 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Reload orientation table")
                { ButtonParameter = 2 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Reset to defaults")
                { ButtonParameter = 3 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Refresh form")
                { ButtonParameter = 4 });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 440;
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
                    DumpStateExternal();
                    break;
                case 2:
                    _orient.Reload();
                    Console.WriteLine("[3DCUO] orientation table reloaded");
                    break;
                case 3:
                    _multi.SetEnabled(false);
                    _multi.SetShowFallbackForUnknownWalls(true);
                    _multi.SetForceTestQuadAtPlayer(false);
                    _multi.SetDeskewMultiTexture(false);
                    _multi.SetVerboseLog(true);
                    _static.SetEnabled(false);
                    _static.SetBillboardAllStatics(false);
                    _static.SetAlphaCutoff(1);
                    _static.SetVerboseLog(true);
                    _quality.SetDepthTestEnabled(false);
                    _quality.SetDisable2DWorld(false);
                    Reopen();
                    break;
                case 4:
                    Reopen();
                    break;
                case 5:
                    DoExport();
                    break;
                case 6:
                    OpenExportsFolder();
                    break;
                case 7:
                    DumpComponents();
                    break;
            }
        }

        // Non-static so it can read the injected _orient service.
        private void DumpComponents()
        {
            try
            {
                var loader = Client.Game.UO.FileManager.Multis;
                var components = loader.GetMultis((uint)_exportMultiId);
                Console.WriteLine($"==================== MULTI 0x{_exportMultiId:X4} ({components?.Count ?? 0} components) ====================");
                if (components == null) return;
                var perGraphic = new System.Collections.Generic.Dictionary<ushort, int>();
                foreach (var c in components)
                {
                    if (!perGraphic.ContainsKey(c.ID)) perGraphic[c.ID] = 0;
                    perGraphic[c.ID]++;
                }
                Console.WriteLine($"  unique graphics: {perGraphic.Count}");
                Console.WriteLine($"  graphic   count   IsWall  IsSurface  IsRoof  IsDoor  height  orient   name");
                foreach (var kv in perGraphic)
                {
                    ushort g = kv.Key;
                    int count = kv.Value;
                    ref var data = ref Client.Game.UO.FileManager.TileData.StaticData[g];
                    var orient = _orient.Resolve(g, data.IsRoof, data.IsSurface, data.IsWall);
                    bool inJson = _orient.TryGet(g, out _);
                    string mark = inJson ? "    " : " *  "; // * = needs JSON entry
                    Console.WriteLine(
                        $"{mark}0x{g:X4}    {count,3}     {data.IsWall,-5}   {data.IsSurface,-5}      {data.IsRoof,-5}   {data.IsDoor,-5}   {data.Height,3}    {orient,-10}  {data.Name}");
                }
                Console.WriteLine("====================  (entries marked * need to be added to multi-tile-orientations.json)  ====================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] DumpComponents failed: {ex.Message}");
            }
        }

        private void DoExport()
        {
            var gd = Client.Game.GraphicsDevice;
            string outDir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Models", "exports");
            string outPath = System.IO.Path.Combine(outDir, $"multi_0x{_exportMultiId:X4}.glb");

            var r = Renderer.Renderer3D.MultiGlbExporter.Export(gd, (uint)_exportMultiId, outPath);
            if (r.Success)
            {
                string msg = $"OK: components={r.ComponentCount} mats={r.Materials} tris={r.Triangles} skipped={r.Skipped}\n{outPath}";
                if (_exportStatusLabel != null) _exportStatusLabel.Text = msg;
                Console.WriteLine($"[3DCUO] GLB export: {msg}");
            }
            else
            {
                string msg = $"FAIL: {r.Error}";
                if (_exportStatusLabel != null) _exportStatusLabel.Text = msg;
                Console.WriteLine($"[3DCUO] GLB export FAIL: {r.Error}");
            }
        }

        private static void OpenExportsFolder()
        {
            try
            {
                string outDir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Models", "exports");
                System.IO.Directory.CreateDirectory(outDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO] OpenExportsFolder failed: {ex.Message}");
            }
        }

        // Non-static so it can read the injected _orient service. Only caller is this gump's
        // own Dump-State button; the "External" name is a legacy artifact from when
        // Render3DDumper invoked it.
        public void DumpStateExternal()
        {
            Console.WriteLine("==================== [3DCUO] STATE DUMP ====================");
            Console.WriteLine("WORLD3D:");
            Console.WriteLine($"  Enabled            = {_quality.MasterEnabled}");
            Console.WriteLine($"  UseIsoProjection   = {_quality.UseIsoProjection}");
            Console.WriteLine($"  DepthTestEnabled   = {_quality.DepthTestEnabled}");
            Console.WriteLine($"  Disable2DWorld     = {_quality.Disable2DWorld}");
            Console.WriteLine($"  Wireframe          = {_quality.Wireframe}");
            Console.WriteLine($"  MeshAlpha          = {_quality.MeshAlpha:F3}");
            Console.WriteLine($"  HeightExaggeration = {_quality.HeightExaggeration:F3}");
            Console.WriteLine($"  HideTestCube       = {_quality.HideTestCube}");
            Console.WriteLine($"  CameraOffset       = {_quality.CameraOffset}");
            Console.WriteLine($"  Camera             = pitch={_camState.PitchDegrees:F1} yaw={_camState.YawDegrees:F1} zoom={_camState.Zoom:F2}");
            Console.WriteLine($"  Camera.Target      = {_camState.Target}");
            Console.WriteLine($"  Last visible/built/drawn = {_diag.LastVisibleChunks}/{_diag.LastBuiltChunks}/{_diag.LastDrawnChunks}");

            Console.WriteLine("MULTI3D:");
            Console.WriteLine($"  Enabled            = {_multi.Enabled}");
            Console.WriteLine($"  VerboseLog         = {_multi.VerboseLog}");
            Console.WriteLine($"  ShowFallbackWalls  = {_multi.ShowFallbackForUnknownWalls}");
            Console.WriteLine($"  ForceTestQuad      = {_multi.ForceTestQuadAtPlayer}");
            Console.WriteLine($"  DeskewTexture      = {_multi.DeskewMultiTexture}");
            Console.WriteLine($"  Last multisSeen    = {_multiDiag.LastMultiSeen}");
            Console.WriteLine($"  Last staticsSeen   = {_multiDiag.LastStaticSeen}");
            Console.WriteLine($"  Last quads         = {_multiDiag.LastQuadCount}");
            Console.WriteLine($"  Last textures      = {_multiDiag.LastTextureCount}");
            Console.WriteLine($"  Last unknown walls = {_multiDiag.LastUnknownCount}");
            Console.WriteLine($"  Last skipped       = {_multiDiag.LastSkippedCount}");

            Console.WriteLine("STATIC3D:");
            Console.WriteLine($"  Enabled            = {_static.Enabled}");
            Console.WriteLine($"  VerboseLog         = {_static.VerboseLog}");
            Console.WriteLine($"  BillboardAllStatics= {_static.BillboardAllStatics}");
            Console.WriteLine($"  AlphaCutoff        = {_static.AlphaCutoff}");
            Console.WriteLine($"  Last seen          = {_staticDiag.LastStaticSeen}");
            Console.WriteLine($"  Last billboards    = {_staticDiag.LastBillboards}");
            Console.WriteLine($"  Last textures      = {_staticDiag.LastTextures}");
            Console.WriteLine($"  Last skipped       = {_staticDiag.LastSkipped}");

            Console.WriteLine("ORIENTATION TABLE:");
            Console.WriteLine($"  Loaded entries     = {_orient.LoadedEntryCount}");
            Console.WriteLine($"  Last error         = {_orient.LastError ?? "(none)"}");

            Console.WriteLine("============================================================");
        }

        public override void Update()
        {
            base.Update();
            if (_statsLabel != null)
            {
                _statsLabel.Text =
                    $"chunks visible    {_diag.LastVisibleChunks}\n" +
                    $"multisSeen        {_multiDiag.LastMultiSeen}      quads {_multiDiag.LastQuadCount}\n" +
                    $"staticsSeen (M3D) {_multiDiag.LastStaticSeen}\n" +
                    $"static seen (S3D) {_staticDiag.LastStaticSeen}    bb {_staticDiag.LastBillboards}\n" +
                    $"unknown walls     {_multiDiag.LastUnknownCount}\n" +
                    $"orientation tbl   {_orient.LoadedEntryCount} entries";
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
