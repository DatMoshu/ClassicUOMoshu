// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — live editor for the player model's per-submesh BaseColorFactor
// + sRGB output toggle + a button that dumps the current player RT to PNG so the
// user can compare against Blender's viewport.

using System;
using System.IO;
using System.Text;
using ClassicUO.Assets;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using ClassicUO.Renderer.Core;
using ClassicUO.Renderer.Mobiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PlayerMaterialGump : Gump
    {
        public static PlayerMaterialGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        // ---- Style (delegated to Debug3DStyle) ----
        private const int W = 540;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = 22;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int TITLE_BAR_H = Debug3DStyle.TITLE_BAR_H;

        private const ushort HUE_LABEL   = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_SECTION = Debug3DStyle.HUE_SECTION_WHITE; // used by submesh-row title labels
        private const ushort HUE_VALUE   = Debug3DStyle.HUE_VALUE;
        private const ushort HUE_OK      = Debug3DStyle.HUE_OK;
        private const ushort HUE_WARN    = Debug3DStyle.HUE_WARN;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        private SkinnedModelGlb _shownModel;
        private int _shownSubmeshCount;
        private ScrollArea _scroll;
        private Label _statusLabel;
        private NiceButton _cullButton;

        private readonly IPlayer3DService _player;

        // Non-static so it can read injected _player.
        private string CullModeLabel() => _player.CullMode switch
        {
            1 => "Cull: CW",
            2 => "Cull: None",
            _ => "Cull: CCW (default)",
        };

        private readonly IMobileRT3DService _rt;

        public PlayerMaterialGump(World world)
            : this(world, Renderer3DHost.Services.Player3D, Renderer3DHost.Services.MobileRT3D) { }

        public PlayerMaterialGump(World world, IPlayer3DService player, IMobileRT3DService rt)
            : base(world, 0, 0)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _rt = rt ?? throw new ArgumentNullException(nameof(rt));
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3DCUO PLAYER MATERIAL",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Global toggles ----------
            y = AddSectionHeader("GLOBAL", y);
            var srgbCb = new Checkbox(0x00D2, 0x00D3, "sRGB output (gamma 1/2.2)",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX, Y = y, IsChecked = _player.SrgbOutput };
            srgbCb.ValueChanged += (s, e) => _player.SetSrgbOutput(srgbCb.IsChecked);
            Add(srgbCb);

            var fwCb = new Checkbox(0x00D2, 0x00D3, "Force white material",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX + innerW / 2, Y = y, IsChecked = _player.ForceWhiteMaterial };
            fwCb.ValueChanged += (s, e) => _player.SetForceWhiteMaterial(fwCb.IsChecked);
            Add(fwCb);
            y += ROW_H + SECTION_GAP;

            // ---------- Per-submesh sliders ----------
            y = AddSectionHeader("SUBMESH BASECOLOR FACTOR", y);
            _scroll = new ScrollArea(contentX, y, innerW, ROW_H * 14, true)
                { AcceptMouseInput = true };
            Add(_scroll);
            y += ROW_H * 14 + SECTION_GAP;

            // ---------- Buttons ----------
            int btnW = (innerW - 20) / 3;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate,
                "Reset to glTF") { ButtonParameter = 1 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate,
                "Dump RT (PNG)") { ButtonParameter = 2 });
            Add(new NiceButton(contentX + (btnW + 10) * 2, y, btnW, 22, ButtonAction.Activate,
                "Refresh rows") { ButtonParameter = 3 });
            y += ROW_H + 4;

            _cullButton = new NiceButton(contentX, y, btnW * 2 + 10, 22, ButtonAction.Activate,
                CullModeLabel()) { ButtonParameter = 4 };
            Add(_cullButton);
            Add(new NiceButton(contentX + (btnW + 10) * 2, y, btnW, 22, ButtonAction.Activate,
                "Dump Values to Console") { ButtonParameter = 5 });
            y += ROW_H + SECTION_GAP;

            // ---------- Status ----------
            y = AddSectionHeader("STATUS", y);
            _statusLabel = new Label("(status)", true, HUE_VALUE, innerW, 0)
                { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 3 + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 460;
            Y = 60;
            WantUpdateSize = false;

            RebuildSubmeshRows();
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case 1: ResetToGltfFactors(); break;
                case 2: DumpRtToPng(); break;
                case 3: RebuildSubmeshRows(); break;
                case 4:
                    _player.SetCullMode((_player.CullMode + 1) % 3);
                    if (_cullButton != null) _cullButton.TextLabel.Text = CullModeLabel();
                    SetStatus($"cull mode → {CullModeLabel()}", HUE_OK);
                    break;
                case 5:
                    Console.WriteLine(BuildDumpString());
                    break;
            }
        }

        private string BuildDumpString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== [3DCUO] PLAYER MATERIAL VALUES ====================");
            sb.AppendLine($"  SrgbOutput          = {_player.SrgbOutput}");
            sb.AppendLine($"  ForceWhiteMaterial  = {_player.ForceWhiteMaterial}");
            sb.AppendLine($"  CullMode            = {CullModeLabel()}");
            var m = Player3DRenderer.Model;
            if (m == null)
            {
                sb.AppendLine("  (no model loaded)");
            }
            else
            {
                sb.AppendLine($"  submeshes = {m.Submeshes.Count}");
                for (int i = 0; i < m.Submeshes.Count; i++)
                {
                    var s = m.Submeshes[i];
                    sb.AppendLine($"   [{i}] '{s.MeshName}' factor=({s.BaseColorFactor.X:F3},{s.BaseColorFactor.Y:F3},{s.BaseColorFactor.Z:F3}) tex={(s.Texture != null ? "yes" : "no")}");
                }
            }
            sb.Append(    "======================================================================");
            return sb.ToString();
        }

        private void ResetToGltfFactors()
        {
            var m = Player3DRenderer.Model;
            if (m == null) return;
            foreach (var s in m.Submeshes) s.BaseColorFactor = s.OriginalBaseColorFactor;
            RebuildSubmeshRows();
            SetStatus("reset to authored glTF factors", HUE_OK);
        }

        private void DumpRtToPng()
        {
            try
            {
                var player = World?.Player;
                if (player == null) { SetStatus("no player", HUE_WARN); return; }
                if (!_rt.Enabled)
                {
                    SetStatus("MobileRT3DRenderer is disabled — enable 'RT'ed mobiles' first", HUE_WARN);
                    return;
                }
                var rt = MobileRT3DRenderer.GetCached(player); // GetCached not on IMobileRT3DService — Mobile coupling at interface boundary.
                if (rt == null) { SetStatus("no cached RT yet — wait one frame", HUE_WARN); return; }

                string root = ResolveDumpRoot();
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string outPath = Path.Combine(root, $"player_rt_{ts}.png");

                // GetData on a RenderTarget2D works while the RT isn't currently bound;
                // we're called from a UI button click so the world frame has finished
                // and the back buffer is bound — safe to read.
                var data = new Color[rt.Width * rt.Height];
                rt.GetData(data);
                using var tex = new Texture2D(rt.GraphicsDevice, rt.Width, rt.Height, false, SurfaceFormat.Color);
                tex.SetData(data);
                using var fs = File.Create(outPath);
                tex.SaveAsPng(fs, rt.Width, rt.Height);

                Console.WriteLine($"[3DCUO][material] dumped player RT to {outPath}");
                SetStatus($"dumped {Path.GetFileName(outPath)}", HUE_OK);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[3DCUO][material] RT dump failed: {ex}");
                SetStatus($"FAILED: {ex.Message}", HUE_WARN);
            }
        }

        private static string ResolveDumpRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            var dir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "dumps"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private void SetStatus(string text, ushort hue)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = text;
            _statusLabel.Hue = hue;
        }

        public override void Update()
        {
            base.Update();

            var m = Player3DRenderer.Model;
            int subCount = m?.Submeshes?.Count ?? 0;
            if (!ReferenceEquals(_shownModel, m) || subCount != _shownSubmeshCount)
            {
                _shownModel = m;
                _shownSubmeshCount = subCount;
                RebuildSubmeshRows();
            }
        }

        private void RebuildSubmeshRows()
        {
            if (_scroll == null) return;
            _scroll.Clear();

            var m = Player3DRenderer.Model;
            if (m == null)
            {
                _scroll.Add(new Label("(no model loaded)", true, HUE_WARN, _scroll.Width - 20, 0)
                    { X = 4, Y = 4 });
                return;
            }

            int rowY = 4;
            int innerW = _scroll.Width - 24;

            for (int i = 0; i < m.Submeshes.Count; i++)
            {
                var captured = m.Submeshes[i];

                // Title line: submesh index + name + texture flag
                _scroll.Add(new Label(
                    $"[{i}] {Trim(captured.MeshName, 28)}  tex={(captured.Texture != null ? "yes" : "no")}",
                    true, HUE_SECTION, innerW, 0) { X = 4, Y = rowY });
                rowY += ROW_H;

                // R / G / B sliders. 0..1000 maps to 0..1.
                rowY = AddChannelSlider("R", captured.BaseColorFactor.X, rowY, v =>
                    { var c = captured.BaseColorFactor; c.X = v; captured.BaseColorFactor = c; }, innerW);
                rowY = AddChannelSlider("G", captured.BaseColorFactor.Y, rowY, v =>
                    { var c = captured.BaseColorFactor; c.Y = v; captured.BaseColorFactor = c; }, innerW);
                rowY = AddChannelSlider("B", captured.BaseColorFactor.Z, rowY, v =>
                    { var c = captured.BaseColorFactor; c.Z = v; captured.BaseColorFactor = c; }, innerW);
                rowY += 4;
            }
        }

        private int AddChannelSlider(string channel, float initial01, int y, Action<float> onChange, int innerW)
        {
            const int LABEL_W = 18;
            const int VALUE_W = 50;
            const int RESET_W = 26;
            int sliderW = innerW - LABEL_W - VALUE_W - RESET_W - 30;
            if (sliderW < 40) sliderW = 40;

            _scroll.Add(new Label(channel, true, HUE_LABEL, LABEL_W, 1)
                { X = 8, Y = y + 2 });

            int initInt = (int)MathF.Round(initial01 * 1000f);
            initInt = Math.Clamp(initInt, 0, 1000);
            int resetInt = initInt;

            var slider = new HSliderBar(
                8 + LABEL_W,
                y + 4,
                sliderW,
                0, 1000, initInt,
                HSliderBarStyle.MetalWidgetRecessedBar,
                hasText: false);
            _scroll.Add(slider);

            var box = new StbTextBox(1, max_char_count: 5, maxWidth: VALUE_W, isunicode: true, hue: HUE_VALUE)
            {
                X = 8 + LABEL_W + sliderW + 6,
                Y = y + 2,
                Width = VALUE_W,
                Height = 20
            };
            box.SetText((initInt / 1000f).ToString("F3"));
            _scroll.Add(box);

            var resetBtn = new NiceButton(
                8 + LABEL_W + sliderW + VALUE_W + 10,
                y + 2, RESET_W, 20,
                ButtonAction.Activate, "R")
            { ButtonParameter = -1 };
            _scroll.Add(resetBtn);

            bool syncing = false;
            slider.ValueChanged += (snd, e) =>
            {
                if (syncing) return;
                syncing = true;
                float f = slider.Value / 1000f;
                box.SetText(f.ToString("F3"));
                syncing = false;
                onChange(f);
            };
            box.TextChanged += (snd, e) =>
            {
                if (syncing) return;
                if (float.TryParse(box.Text, out float f))
                {
                    f = Math.Clamp(f, 0f, 1f);
                    int iv = (int)MathF.Round(f * 1000f);
                    if (slider.Value != iv)
                    {
                        syncing = true;
                        slider.Value = iv;
                        syncing = false;
                        onChange(f);
                    }
                }
            };
            resetBtn.MouseUp += (snd, e) =>
            {
                syncing = true;
                slider.Value = resetInt;
                float f = resetInt / 1000f;
                box.SetText(f.ToString("F3"));
                syncing = false;
                onChange(f);
            };

            return y + ROW_H;
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "(none)";
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }
    }
}
