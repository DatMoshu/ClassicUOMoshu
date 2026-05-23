// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — settings gump for the 3D tree renderer. Toggles between
// the original single-billboard mode and the crossed-planes 3D mode, exposes
// leaf animation controls, z-fighting controls, and the year-cycle test driver.
//
// MIGRATION (ADR-012 §6 / playbook §Q): partially migrated — the season-cycle
// portion (29 touch points) injects ISeasonService; the tree-registry portion
// (6 touch points) injects ITreeStaticRegistry. Static3DRenderer (77 touch
// points) and StaticClassifier (3 touch points) stay on the legacy facades —
// those systems are not yet migrated.

using System;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Statics;
using ClassicUO.Renderer.World;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class Trees3DSettingsGump : Gump
    {
        public static Trees3DSettingsGump Instance;

        private readonly ISeasonService _season;
        private readonly ITreeStaticRegistry _treeRegistry;
        private readonly IStatic3DConfigService _static;
        private readonly IStatic3DDiagnosticsService _staticDiag;
        private readonly IFoliage3DConfigService _foliage;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private void Reopen()
        {
            var w = World;
            int x = X, y = Y;
            Dispose();
            var g = new Trees3DSettingsGump(w, _season, _treeRegistry, _static, _staticDiag, _foliage) { X = x, Y = y };
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        private const int W = 460;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int LABEL_W     = 180;

        // Fixed column widths for diagnostics text — prevents label/value drift
        // as numbers change width each frame.
        private const int DIAG_LABEL_W = 16;
        private const int DIAG_VAL_W   = 5;

        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;

        // Two live-update labels updated each frame in Update().
        private Label _presenceLabel;   // LEAF ANIMATION section readout
        private Label _statsLabel;      // DIAGNOSTICS section readout

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        /// <summary>
        /// Convenience overload that resolves <see cref="ISeasonService"/> and
        /// <see cref="ITreeStaticRegistry"/> from the active renderer service container.
        /// </summary>
        public Trees3DSettingsGump(World world)
            : this(world,
                Renderer3DHost.Services.Season,
                Renderer3DHost.Services.TreeStaticRegistry,
                Renderer3DHost.Services.Static3DConfig,
                Renderer3DHost.Services.Static3DDiagnostics,
                Renderer3DHost.Services.Foliage3DConfig) { }

        public Trees3DSettingsGump(World world, ISeasonService season, ITreeStaticRegistry treeRegistry,
                                   IStatic3DConfigService staticCfg, IStatic3DDiagnosticsService staticDiag,
                                   IFoliage3DConfigService foliage)
            : base(world, 0, 0)
        {
            _season = season ?? throw new ArgumentNullException(nameof(season));
            _treeRegistry = treeRegistry ?? throw new ArgumentNullException(nameof(treeRegistry));
            _static = staticCfg ?? throw new ArgumentNullException(nameof(staticCfg));
            _staticDiag = staticDiag ?? throw new ArgumentNullException(nameof(staticDiag));
            _foliage = foliage ?? throw new ArgumentNullException(nameof(foliage));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3D TREES",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ----------------------------------------------------------------
            // MODE
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "MODE", y);
            Debug3DStyle.AddCheck(this, contentX, y, "Original Billboard (camera-facing)",
                _foliage.TreeMode == TreeRenderMode.OriginalBillboard,
                v =>
                {
                    if (v) _foliage.SetTreeMode(TreeRenderMode.OriginalBillboard);
                    Reopen();
                });
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y, "Crossed Planes 3D (volumetric)",
                _foliage.TreeMode == TreeRenderMode.CrossedPlanes3D,
                v =>
                {
                    if (v) _foliage.SetTreeMode(TreeRenderMode.CrossedPlanes3D);
                    Reopen();
                });
            y += ROW_H + SECTION_GAP;

            // ----------------------------------------------------------------
            // CROSSED PLANES  (only when that mode is active)
            // ----------------------------------------------------------------
            if (_foliage.TreeMode == TreeRenderMode.CrossedPlanes3D)
            {
                y = Debug3DStyle.AddSectionHeader(this, contentX, innerW,
                    "CROSSED PLANES (1 trunk + N leaf planes)", y);
                y = AddSlider("Leaf plane count (0..4)", 0, 4,
                    _foliage.LeafPlaneCount, y,
                    v => _foliage.SetLeafPlaneCount(v));
                y = AddSlider("Leaf plane yaw step (deg)", 15, 90,
                    (int)_foliage.LeafPlaneYawDeg, y,
                    v => _foliage.SetLeafPlaneYawDeg(v));

                // Wind-sway checkbox (new, no stable button ID needed — checkbox only)
                Debug3DStyle.AddCheck(this, contentX, y,
                    "Wind sway (whole canopy rotates uniformly)",
                    _foliage.LeafPlaneWindEnabled,
                    v => _foliage.SetLeafPlaneWindEnabled(v));
                y += ROW_H;

                // Wind amplitude slider (new, ID 30 reserved for any future button)
                y = AddSlider("Sway amplitude (deg)", 1, 30,
                    (int)_foliage.LeafPlaneWindAmpDeg, y,
                    v => _foliage.SetLeafPlaneWindAmpDeg(v));

                y += SECTION_GAP;
            }

            // ----------------------------------------------------------------
            // LEAF ANIMATION  (new section)
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "LEAF ANIMATION", y);

            // LeafFadeMode picker (mutually-exclusive radio-style toggles).
            Debug3DStyle.AddCheck(this, contentX, y,
                "Mode: Cull (hard cut at presence < 1)",
                _foliage.LeafFadeMode == LeafFadeMode.Cull,
                v => { if (v) { _foliage.SetLeafFadeMode(LeafFadeMode.Cull); Reopen(); } });
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y,
                "Mode: Scale (canopy shrinks toward center)",
                _foliage.LeafFadeMode == LeafFadeMode.Scale,
                v => { if (v) { _foliage.SetLeafFadeMode(LeafFadeMode.Scale); Reopen(); } });
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y,
                "Mode: Fade (alpha 1 -> 0, full size)",
                _foliage.LeafFadeMode == LeafFadeMode.Fade,
                v => { if (v) { _foliage.SetLeafFadeMode(LeafFadeMode.Fade); Reopen(); } });
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y,
                "Mode: Scale + fade after 50% (combo)",
                _foliage.LeafFadeMode == LeafFadeMode.ScaleThenFade,
                v => { if (v) { _foliage.SetLeafFadeMode(LeafFadeMode.ScaleThenFade); Reopen(); } });
            y += ROW_H;

            // RegrowInSpring: lives on ISeasonService.
            Debug3DStyle.AddCheck(this, contentX, y,
                "Regrow leaves in spring (season cycle)",
                _season.RegrowInSpring,
                v => _season.SetRegrowInSpring(v));
            y += ROW_H;

            // Live presence readout — 1 line, updated each frame.
            _presenceLabel = new Label(BuildPresenceText(), true, HUE_VALUE, innerW, 4)
                { X = contentX, Y = y };
            Add(_presenceLabel);
            y += ROW_H + SECTION_GAP;

            // ----------------------------------------------------------------
            // CLASSIFICATION
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW,
                $"CLASSIFICATION (registry: {_treeRegistry.Count} entries)", y);
            y = AddTwoColCheck(
                "Use tree-statics.json registry", StaticClassifier.UseTreeStaticRegistry,
                    v => { StaticClassifier.UseTreeStaticRegistry = v; Reopen(); },
                "Force WholeTree -> LeafOverlay", _foliage.ForceWholeTreeAsLeafOverlay,
                    v => _foliage.SetForceWholeTreeAsLeafOverlay(v),
                y);
            int btnRow = y;
            int btnW3 = (innerW - 20) / 3;
            Add(new NiceButton(contentX, btnRow, btnW3, 22, ButtonAction.Activate, "Reload mapping")
                { ButtonParameter = 10 });
            Add(new NiceButton(contentX + btnW3 + 10, btnRow, btnW3, 22, ButtonAction.Activate, "Run dump")
                { ButtonParameter = 11 });
            Add(new NiceButton(contentX + (btnW3 + 10) * 2, btnRow, btnW3, 22, ButtonAction.Activate, "Open dumps dir")
                { ButtonParameter = 12 });
            y += ROW_H + SECTION_GAP;

            // ----------------------------------------------------------------
            // YEAR CYCLE
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "YEAR CYCLE", y);
            Debug3DStyle.AddCheck(this, contentX, y,
                "Enable cycle (Spring -> Summer -> Autumn -> Winter, loops)",
                _season.Enabled,
                v =>
                {
                    if (v) _season.SetEnabled(true);
                    else   _season.Stop();
                });
            y += ROW_H;
            Debug3DStyle.AddCheck(this, contentX, y,
                "Drive rain/snow particles in lockstep",
                _season.DriveWeatherParticles,
                v => _season.SetDriveWeatherParticles(v));
            y += ROW_H;
            // Showcase: doubles the loop period to 2 years; year 2 layers
            // Storm / Sandstorm / Blood Moon / Blizzard at season midpoints.
            Debug3DStyle.AddCheck(this, contentX, y,
                "Showcase mode (2-year loop with extreme weather in Y2)",
                _season.ShowcaseMode,
                v => _season.SetShowcaseMode(v));
            y += ROW_H;
            // Auto-sway: Storm/Blizzard/Sandstorm turn sway on; Clear/light
            // rain/light snow keep canopy still.
            Debug3DStyle.AddCheck(this, contentX, y,
                "Auto tree sway from weather (Storm/Blizzard on, Clear off)",
                _season.DriveSwayFromWeather,
                v => _season.SetDriveSwayFromWeather(v));
            y += ROW_H;
            // FoliageShaderTint: when ON, ALSO enable the world-cell-hashed
            // FallFoliage shader pass on top of the per-species registry recolor.
            // Default OFF — the shader pass causes per-plane color shifts that
            // are visible as the camera turns.
            Debug3DStyle.AddCheck(this, contentX, y,
                "Also drive FoliageSeason=Fall shader pass (legacy, may shift)",
                _season.DriveFoliageShaderTint,
                v => _season.SetDriveFoliageShaderTint(v));
            y += ROW_H;
            y = AddSlider("Seconds per year (10..600)", 10, 600,
                (int)_season.SecondsPerYear, y,
                v => _season.SetSecondsPerYear(v));
            // Snap-to-season buttons (4 across). IDs 20..23 — preserved.
            int btnRowS = y;
            int btnWs = (innerW - 30) / 4;
            Add(new NiceButton(contentX,                     btnRowS, btnWs, 22, ButtonAction.Activate, "Spring") { ButtonParameter = 20 });
            Add(new NiceButton(contentX + (btnWs + 10),      btnRowS, btnWs, 22, ButtonAction.Activate, "Summer") { ButtonParameter = 21 });
            Add(new NiceButton(contentX + (btnWs + 10) * 2,  btnRowS, btnWs, 22, ButtonAction.Activate, "Autumn") { ButtonParameter = 22 });
            Add(new NiceButton(contentX + (btnWs + 10) * 3,  btnRowS, btnWs, 22, ButtonAction.Activate, "Winter") { ButtonParameter = 23 });
            y += ROW_H + SECTION_GAP;

            // ----------------------------------------------------------------
            // Z-FIGHTING
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "Z-FIGHTING", y);
            y = AddSlider("Tree Z bias x10 (camera-right)", 0, 30,
                (int)(_foliage.TreeZBiasMagnitude * 10f), y,
                v => _foliage.SetTreeZBiasMagnitude(v / 10f));
            y = AddSlider("Tree Y jitter x100", 0, 50,
                (int)(_foliage.TreeYJitter * 100f), y,
                v => _foliage.SetTreeYJitter(v / 100f));
            y = AddSlider("Ground decal lift x100", 0, 100,
                (int)(_foliage.GroundDecalLift * 100f), y,
                v => _foliage.SetGroundDecalLift(v / 100f));
            y = AddTwoColCheck(
                "Use rasterizer DepthBias", _foliage.UseTreeDepthBias,
                    v => _foliage.SetUseTreeDepthBias(v),
                "Sort chunks back-to-front", _foliage.SortTreesBackToFront,
                    v => _foliage.SetSortTreesBackToFront(v),
                y);
            y += SECTION_GAP;

            // ----------------------------------------------------------------
            // DIAGNOSTICS
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW,
                "DIAGNOSTICS (last frame)", y);
            // Stats text: 5 lines. Allocate ROW_H * 5 so the footer divider
            // never overlaps the bottom line even at narrow line-heights.
            _statsLabel = new Label(BuildStatsText(), true, HUE_VALUE, innerW, 4)
                { X = contentX, Y = y };
            Add(_statsLabel);
            y += ROW_H * 5;

            // ----------------------------------------------------------------
            // Footer
            // ----------------------------------------------------------------
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);
            int btnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Refresh form")
                { ButtonParameter = 1 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Dump State")
                { ButtonParameter = 2 });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 470;
            Y = 60;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case 1: Reopen(); break;
                case 2: DumpFullState(); break;
                case 10:
                    if (_treeRegistry.Load())
                        Console.WriteLine($"[Trees3DSettingsGump] Reloaded tree-statics.json — {_treeRegistry.Count} entries.");
                    else
                        Console.WriteLine($"[Trees3DSettingsGump] Reload FAILED: {_treeRegistry.LastError}");
                    Reopen();
                    break;
                case 11:
                {
                    var path = StaticClassifyDumper.Dump();
                    Console.WriteLine(path != null
                        ? $"[Trees3DSettingsGump] Dump written: {path}"
                        : "[Trees3DSettingsGump] Dump failed (no visible chunks?).");
                    break;
                }
                case 12:
                {
                    var path = StaticClassifyDumper.LastDumpFile;
                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            var dir = System.IO.Path.GetDirectoryName(path);
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dir,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex) { Console.WriteLine($"[Trees3DSettingsGump] open dir failed: {ex.Message}"); }
                    }
                    else Console.WriteLine("[Trees3DSettingsGump] No dump captured yet — click Run dump first.");
                    break;
                }
                case 20: _season.SetEnabled(true); _season.SnapTo(0.00f); break;
                case 21: _season.SetEnabled(true); _season.SnapTo(0.30f); break;
                case 22: _season.SetEnabled(true); _season.SnapTo(0.60f); break;
                case 23: _season.SetEnabled(true); _season.SnapTo(0.85f); break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (_presenceLabel != null)
                _presenceLabel.Text = BuildPresenceText();
            if (_statsLabel != null)
                _statsLabel.Text = BuildStatsText();
        }

        // ---- comprehensive state dump ----
        // Walks every public-static field that affects 3D tree rendering so a
        // bug report can be reproduced from a single console block. Grouped
        // so future readers can find specific subsystems quickly.
        // Non-static so it can use injected _season / _treeRegistry. Other readers
        // (Static3DRenderer) stay on the legacy facade as those systems are unmigrated.
        private void DumpFullState()
        {
            Console.WriteLine("==================== [3DCUO] 3D TREES — FULL STATE ====================");

            Console.WriteLine("[Static3DRenderer]");
            Console.WriteLine($"  Enabled                  = {_static.Enabled}");
            Console.WriteLine($"  ClassifyStatics          = {_static.ClassifyStatics}");
            Console.WriteLine($"  BillboardAllStatics      = {_static.BillboardAllStatics}");
            Console.WriteLine($"  BillboardItems           = {_static.BillboardItems}");
            Console.WriteLine($"  AlphaCutoff              = {_static.AlphaCutoff}");
            Console.WriteLine($"  TreeMode                 = {_foliage.TreeMode}");
            Console.WriteLine($"  LeafPlaneCount           = {_foliage.LeafPlaneCount}");
            Console.WriteLine($"  LeafPlaneYawDeg          = {_foliage.LeafPlaneYawDeg}");
            Console.WriteLine($"  LeafPlaneWindEnabled     = {_foliage.LeafPlaneWindEnabled}");
            Console.WriteLine($"  LeafPlaneWindAmpDeg      = {_foliage.LeafPlaneWindAmpDeg}");
            Console.WriteLine($"  LeafSwayMode             = {_foliage.LeafSwayMode}");
            Console.WriteLine($"  LeafSwayPhasePerPlane    = {_foliage.LeafSwayPhasePerPlane}");
            Console.WriteLine($"  LeafSwayBobAmount        = {_foliage.LeafSwayBobAmount}");
            Console.WriteLine($"  LeafSwaySmoothstep       = {_foliage.LeafSwaySmoothstep}");
            Console.WriteLine($"  LeafSwayPerTreePhase     = {_foliage.LeafSwayPerTreePhase}");
            Console.WriteLine($"  LeafSwayPerTreeAmount    = {_foliage.LeafSwayPerTreeAmount}");
            Console.WriteLine($"  LeafFadeMode             = {_foliage.LeafFadeMode}");
            Console.WriteLine($"  LastLeafFadeQuads        = {_staticDiag.LastLeafFadeQuads}");
            Console.WriteLine($"  LeafPresence             = {_foliage.LeafPresence:P0}");
            Console.WriteLine($"  ApplyOverlayToFoliage    = {_foliage.ApplyOverlayToFoliage}");
            Console.WriteLine($"  ApplyOverlayToTrunks     = {_foliage.ApplyOverlayToTrunks}");
            Console.WriteLine($"  DropLeavesWorldwide      = {_foliage.DropLeavesWorldwide}");
            Console.WriteLine($"  DropLeavesNearby         = {_foliage.DropLeavesNearby}");
            Console.WriteLine($"  DropLeavesRadius         = {_foliage.DropLeavesRadius}");
            Console.WriteLine($"  ForceWholeTreeAsLeaf     = {_foliage.ForceWholeTreeAsLeafOverlay}");
            Console.WriteLine($"  TreeZBiasMagnitude       = {_foliage.TreeZBiasMagnitude:F3}");
            Console.WriteLine($"  TreeYJitter              = {_foliage.TreeYJitter:F3}");
            Console.WriteLine($"  GroundDecalLift          = {_foliage.GroundDecalLift:F3}");
            Console.WriteLine($"  UseTreeDepthBias         = {_foliage.UseTreeDepthBias}");
            Console.WriteLine($"  SortTreesBackToFront     = {_foliage.SortTreesBackToFront}");

            Console.WriteLine("[StaticClassifier / Registry]");
            Console.WriteLine($"  UseTreeStaticRegistry    = {StaticClassifier.UseTreeStaticRegistry}");
            Console.WriteLine($"  Registry entries         = {_treeRegistry.Count}");
            Console.WriteLine($"  Registry source          = {_treeRegistry.LastSource}");

            Console.WriteLine("[SeasonCycleDriver]");
            Console.WriteLine($"  Enabled                  = {_season.Enabled}");
            Console.WriteLine($"  Progress                 = {_season.Progress:P1}");
            Console.WriteLine($"  SecondsPerYear           = {_season.SecondsPerYear}");
            Console.WriteLine($"  LastPhase                = {_season.LastPhase}");
            Console.WriteLine($"  DriveWeatherParticles    = {_season.DriveWeatherParticles}");
            Console.WriteLine($"  DriveFoliageShaderTint   = {_season.DriveFoliageShaderTint}");
            Console.WriteLine($"  RegrowInSpring           = {_season.RegrowInSpring}");

            Console.WriteLine("[Diagnostics — last frame]");
            Console.WriteLine($"  LastStaticSeen           = {_staticDiag.LastStaticSeen}");
            Console.WriteLine($"  LastBillboards           = {_staticDiag.LastBillboards}");
            Console.WriteLine($"  LastGroundDecals         = {_staticDiag.LastGroundDecals}");
            Console.WriteLine($"  LastTextures             = {_staticDiag.LastTextures}");
            Console.WriteLine($"  LastSkipped              = {_staticDiag.LastSkipped}");
            Console.WriteLine($"  LastWholeTrees           = {_staticDiag.LastWholeTrees}");
            Console.WriteLine($"  LastLeafOverlays         = {_staticDiag.LastLeafOverlays}");
            Console.WriteLine($"  LastBillboardedItems     = {_staticDiag.LastBillboardedItems}");

            Console.WriteLine("=======================================================================");
        }

        // ---- text helpers ----

        /// Single-line presence readout for the LEAF ANIMATION section.
        // Non-static so it can read injected _foliage.
        private string BuildPresenceText() =>
            $"Presence: {_foliage.LeafPresence:P0}";

        /// Fixed-width five-line stats block for the DIAGNOSTICS section.
        /// Using PadRight/PadLeft on constant widths prevents column drift
        /// as integer values change digit count each frame.
        // Non-static so it can read injected _season state.
        private string BuildStatsText()
        {
            string cycleStr = _season.Enabled
                ? $"{_season.Progress * 100f,5:F1}%  {_season.LastPhase}"
                : "off";

            // Each stat line: label (16 chars, left-aligned) + value (5 chars, right-aligned).
            // Two-column rows share the row with a double space between columns.
            return
                $"{"static seen",-DIAG_LABEL_W} {_staticDiag.LastStaticSeen,DIAG_VAL_W}\n" +
                $"{"billboards",-DIAG_LABEL_W} {_staticDiag.LastBillboards,DIAG_VAL_W}  " +
                    $"{"decals",-DIAG_LABEL_W} {_staticDiag.LastGroundDecals,DIAG_VAL_W}\n" +
                $"{"WholeTree",-DIAG_LABEL_W} {_staticDiag.LastWholeTrees,DIAG_VAL_W}  " +
                    $"{"LeafOverlay",-DIAG_LABEL_W} {_staticDiag.LastLeafOverlays,DIAG_VAL_W}\n" +
                $"{"cycle",-DIAG_LABEL_W} {cycleStr}";
        }

        // ---- layout helpers ----

        private int AddSlider(string label, int min, int max, int value, int y, Action<int> onChange)
        {
            int contentX = INNER_PAD + 10;
            int rowW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSlider(this, contentX, rowW, LABEL_W,
                label, min, max, value, y, onChange);
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
