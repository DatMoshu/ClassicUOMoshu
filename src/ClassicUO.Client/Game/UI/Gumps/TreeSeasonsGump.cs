// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — admin gump for the seasonal tree recolor + snow system.
// Drives ITreeSeasonService (which feeds TreeTextureCache) and applies to
// both 2D and 3D leaf-static rendering.
//
// MIGRATION (ADR-012 §6 / playbook §Q): constructor-injected ITreeSeasonService.
// TreeTextureCache and TreeStaticRegistry stay on the legacy facades because they
// are not yet migrated.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.WorldEnv;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class TreeSeasonsGump : Gump
    {
        public static TreeSeasonsGump Instance;

        private readonly ITreeSeasonService _treeSeason;

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
            var g = new TreeSeasonsGump(w, _treeSeason) { X = x, Y = y };
            Instance = g;
            Game.Managers.UIManager.Add(g);
        }

        private const int W          = 460;
        private const int INNER_PAD  = Debug3DStyle.INNER_PAD;
        private const int ROW_H      = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int LABEL_W    = 180;

        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;

        // Live status label — sits in the STATUS section at the top.
        private Label _statsLabel;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        /// <summary>
        /// Convenience overload that resolves <see cref="ITreeSeasonService"/> from the
        /// active renderer service container. Existing call sites
        /// (Render3DLauncherGump.Open&lt;TreeSeasonsGump&gt;) keep their
        /// <c>new TreeSeasonsGump(World)</c> shape until they migrate.
        /// </summary>
        public TreeSeasonsGump(World world)
            : this(world, Renderer3DHost.Services.TreeSeason) { }

        public TreeSeasonsGump(World world, ITreeSeasonService treeSeason) : base(world, 0, 0)
        {
            _treeSeason = treeSeason ?? throw new ArgumentNullException(nameof(treeSeason));

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "TREE SEASONS / SNOW",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW   = W - INNER_PAD * 2 - 20;

            // ----------------------------------------------------------------
            // STATUS  (read-only live data — one datum per line, no overlap)
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "STATUS", y);
            _statsLabel = new Label("(loading...)", true, HUE_VALUE, innerW, font: 1)
                { X = contentX, Y = y };
            Add(_statsLabel);
            // Reserve 4 full rows: season / year % / snow % + line % / cached count.
            y += ROW_H * 4 + SECTION_GAP;

            // ----------------------------------------------------------------
            // MASTER  — enable + auto-from-year on their own rows, then slider
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "MASTER", y);

            // Row 1: master enable checkbox.
            Debug3DStyle.AddCheck(this, contentX, y,
                "Enable seasonal recolor (2D + 3D leaf statics)",
                _treeSeason.Enabled,
                v => { _treeSeason.SetEnabled(v); TreeTextureCache.InvalidateAll(); });
            y += ROW_H;

            // Row 2: auto-from-year checkbox — separate row so label never
            // touches the value spinner of the slider below.
            Debug3DStyle.AddCheck(this, contentX, y,
                "Auto from year-progress",
                _treeSeason.AutoFromYear,
                v => { _treeSeason.SetAutoFromYear(v); Reopen(); });
            y += ROW_H;

            // Row 3: year-progress slider — starts on a fresh y-cursor.
            y = AddSlider("Year progress (0..100)", 0, 100,
                (int)(_treeSeason.YearProgress * 100f), y,
                v => _treeSeason.SetYearProgress(v / 100f));

            // Row 4: snap-to-season buttons (Spring / Summer / Autumn / Winter).
            int btnW = (innerW - 30) / 4;
            Add(new NiceButton(contentX + 0 * (btnW + 10), y, btnW, 22,
                ButtonAction.Activate, "Spring") { ButtonParameter = 10 });
            Add(new NiceButton(contentX + 1 * (btnW + 10), y, btnW, 22,
                ButtonAction.Activate, "Summer") { ButtonParameter = 11 });
            Add(new NiceButton(contentX + 2 * (btnW + 10), y, btnW, 22,
                ButtonAction.Activate, "Autumn") { ButtonParameter = 12 });
            Add(new NiceButton(contentX + 3 * (btnW + 10), y, btnW, 22,
                ButtonAction.Activate, "Winter") { ButtonParameter = 13 });
            y += ROW_H + SECTION_GAP;

            // ----------------------------------------------------------------
            // SNOW
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "SNOW", y);
            y = AddSlider("Snow amount %", 0, 100,
                (int)(_treeSeason.SnowAmount * 100f), y,
                v => { _treeSeason.SetSnowAmount(v / 100f); _treeSeason.SetAutoFromYear(false); });
            y = AddSlider("Snow line % (top of sprite)", 5, 100,
                (int)(_treeSeason.SnowLineFrac * 100f), y,
                v => _treeSeason.SetSnowLineFrac(v / 100f));
            y += SECTION_GAP;

            // ----------------------------------------------------------------
            // HUE / SATURATION
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "HUE / SATURATION", y);
            y = AddSlider("Hue shift° (-180..180)", -180, 180,
                (int)_treeSeason.HueShiftDeg, y,
                v => { _treeSeason.SetHueShiftDeg(v); _treeSeason.SetAutoFromYear(false); });
            y = AddSlider("Saturation x100 (10..200)", 10, 200,
                (int)(_treeSeason.SaturationBoost * 100f), y,
                v => { _treeSeason.SetSaturationBoost(v / 100f); _treeSeason.SetAutoFromYear(false); });
            // Fall color speed: 25..300 = sharpness x100 (0.25..3.0).
            // 100 = linear smoothstep, >100 = colors arrive faster.
            y = AddSlider("Fall color speed x100 (25..300)", 25, 300,
                (int)(_treeSeason.FallColorSharpness * 100f), y,
                v => _treeSeason.SetFallColorSharpness(v / 100f));
            y += SECTION_GAP;

            // ----------------------------------------------------------------
            // LEAF MASK
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "LEAF MASK", y);
            y = AddSlider("Greenness threshold (0..40)", 0, 40,
                TreeTextureCache.GreenThreshold, y,
                v => { TreeTextureCache.GreenThreshold = v; TreeTextureCache.InvalidateAll(); });
            y += SECTION_GAP;

            // ----------------------------------------------------------------
            // DIAGNOSTICS + footer
            // ----------------------------------------------------------------
            y = Debug3DStyle.AddSectionHeader(this, contentX, innerW, "DIAGNOSTICS", y);
            y = Debug3DStyle.BeginFooter(this, contentX, innerW, y);

            int fbtnW = (innerW - 10) / 2;
            Add(new NiceButton(contentX,            y, fbtnW, 22, ButtonAction.Activate, "Refresh form")
                { ButtonParameter = 1 });
            Add(new NiceButton(contentX + fbtnW + 10, y, fbtnW, 22, ButtonAction.Activate, "Rebuild cache")
                { ButtonParameter = 2 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "Dump State")
                { ButtonParameter = 3 });
            y += ROW_H + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 470;
            Y = 80;
            WantUpdateSize = false;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case 1: Reopen(); break;
                case 2: TreeTextureCache.InvalidateAll(); break;
                case 3:
                    Console.WriteLine("==================== [3DCUO] TREE SEASONS — FULL STATE ====================");
                    Console.WriteLine("[ITreeSeasonService]");
                    Console.WriteLine($"  Enabled              = {_treeSeason.Enabled}");
                    Console.WriteLine($"  Season               = {_treeSeason.Season}");
                    Console.WriteLine($"  YearProgress         = {_treeSeason.YearProgress:F3}");
                    Console.WriteLine($"  AutoFromYear         = {_treeSeason.AutoFromYear}");
                    Console.WriteLine($"  SnowAmount           = {_treeSeason.SnowAmount:F3}");
                    Console.WriteLine($"  SnowLineFrac         = {_treeSeason.SnowLineFrac:F3}");
                    Console.WriteLine($"  HueShiftDeg          = {_treeSeason.HueShiftDeg:F1}");
                    Console.WriteLine($"  SaturationBoost      = {_treeSeason.SaturationBoost:F3}");
                    Console.WriteLine($"  FallColorSharpness   = {_treeSeason.FallColorSharpness:F2}");
                    Console.WriteLine($"  CacheToken           = 0x{_treeSeason.QuantisedCacheToken():X8}");
                    Console.WriteLine("[TreeTextureCache]");
                    Console.WriteLine($"  GreenThreshold       = {TreeTextureCache.GreenThreshold}");
                    Console.WriteLine($"  CachedCount          = {TreeTextureCache.CachedCount}");
                    Console.WriteLine("[TreeStaticRegistry]");
                    Console.WriteLine($"  Count                = {TreeStaticRegistry.Count}");
                    Console.WriteLine($"  LastSource           = {TreeStaticRegistry.LastSource}");
                    Console.WriteLine("===========================================================================");
                    break;

                case 10: _treeSeason.SnapToSeason(TreeSeasonKind.Spring); Reopen(); break;
                case 11: _treeSeason.SnapToSeason(TreeSeasonKind.Summer); Reopen(); break;
                case 12: _treeSeason.SnapToSeason(TreeSeasonKind.Autumn); Reopen(); break;
                case 13: _treeSeason.SnapToSeason(TreeSeasonKind.Winter); Reopen(); break;
            }
        }

        public override void Update()
        {
            base.Update();
            if (_statsLabel != null)
            {
                // Each datum on its own line — fixed indent, no inline concatenation
                // that could cause values to run into each other at varying widths.
                _statsLabel.Text =
                    $"season    {_treeSeason.Season}\n" +
                    $"year      {_treeSeason.YearProgress * 100f,5:F0}%\n" +
                    $"snow      {_treeSeason.SnowAmount * 100f,5:F0}%   line {_treeSeason.SnowLineFrac * 100f,3:F0}%\n" +
                    $"cached    {TreeTextureCache.CachedCount} textures";
            }
        }

        private int AddSlider(string label, int min, int max, int value, int y, Action<int> onChange)
        {
            int contentX = INNER_PAD + 10;
            int rowW     = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSlider(this, contentX, rowW, LABEL_W,
                label, min, max, value, y, onChange);
        }
    }
}
