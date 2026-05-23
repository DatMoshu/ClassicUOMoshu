// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — Equipment Tagger gump.
//
// Walks Data/model-index.json one untagged GLB at a time, attaches it to the
// live player rig (via TaggerSession → AttachmentRenderer), and lets the user
// confirm the matching {Layer, Graphic} or skip/discard.
//
// Suggestions surfaced via Data/equipment-models.suggestions.json populate
// up to five clickable candidate buttons. A free-form graphic textbox covers
// the long tail when no suggestion fits.

using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;

#nullable disable

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class EquipmentTaggerGump : Gump
    {
        public static EquipmentTaggerGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 620;
        private const int INNER_PAD = Debug3DStyle.INNER_PAD;
        private const int ROW_H = 22;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;
        private const ushort HUE_OK    = Debug3DStyle.HUE_OK;
        private const ushort HUE_WARN  = Debug3DStyle.HUE_WARN;

        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        private Label _progressLabel;
        private Label _modelPathLabel;
        private Label _slotPackLabel;
        private Label _statusLabel;
        private StbTextBox _graphicBox;
        private StbTextBox _layerBox;

        // Per-suggestion buttons (up to 5).
        private const int BTN_SUGGEST_BASE = 200;
        private const int BTN_PREV = 1;
        private const int BTN_SKIP = 2;
        private const int BTN_NEXT = 3;
        private const int BTN_CONFIRM = 4;
        private const int BTN_DUPLICATE = 5;
        private const int BTN_DISCARD = 6;
        private const int BTN_REFRESH = 7;

        public EquipmentTaggerGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "3DCUO EQUIPMENT TAGGER",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---- header ----
            y = AddSectionHeader("CURRENT MODEL", y);
            _progressLabel = new Label("(load...)", true, HUE_LABEL, innerW, 1) { X = contentX, Y = y };
            Add(_progressLabel);
            y += ROW_H;
            _modelPathLabel = new Label("", true, HUE_VALUE, innerW, 1) { X = contentX, Y = y };
            Add(_modelPathLabel);
            y += ROW_H;
            _slotPackLabel = new Label("", true, HUE_LABEL, innerW, 1) { X = contentX, Y = y };
            Add(_slotPackLabel);
            y += ROW_H + SECTION_GAP;

            // ---- nav / actions ----
            y = AddSectionHeader("NAVIGATE", y);
            int btnW = (innerW - 20) / 3;
            Add(new NiceButton(contentX,                  y, btnW, 22, ButtonAction.Activate, "<- Prev untagged") { ButtonParameter = BTN_PREV });
            Add(new NiceButton(contentX + btnW + 10,      y, btnW, 22, ButtonAction.Activate, "Skip") { ButtonParameter = BTN_SKIP });
            Add(new NiceButton(contentX + (btnW + 10)*2,  y, btnW, 22, ButtonAction.Activate, "Next untagged ->") { ButtonParameter = BTN_NEXT });
            y += ROW_H + SECTION_GAP;

            // ---- suggestions ----
            y = AddSectionHeader("SUGGESTIONS (click to confirm)", y);
            for (int i = 0; i < 5; i++)
            {
                Add(new NiceButton(contentX, y, innerW, 22, ButtonAction.Activate, "(none)")
                    { ButtonParameter = BTN_SUGGEST_BASE + i });
                y += ROW_H;
            }
            y += SECTION_GAP;

            // ---- manual entry ----
            y = AddSectionHeader("MANUAL ENTRY", y);
            Add(new Label("Layer:", true, HUE_LABEL, 60, 1) { X = contentX, Y = y + 2 });
            _layerBox = new StbTextBox(1, max_char_count: 24, maxWidth: 160, isunicode: true, hue: HUE_VALUE)
            { X = contentX + 60, Y = y + 2, Width = 160, Height = 20 };
            Add(_layerBox);

            Add(new Label("Graphic:", true, HUE_LABEL, 70, 1) { X = contentX + 240, Y = y + 2 });
            _graphicBox = new StbTextBox(1, max_char_count: 8, maxWidth: 90, isunicode: true, hue: HUE_VALUE)
            { X = contentX + 320, Y = y + 2, Width = 90, Height = 20 };
            Add(_graphicBox);

            Add(new NiceButton(contentX + 430, y, innerW - 430, 22, ButtonAction.Activate, "Confirm")
                { ButtonParameter = BTN_CONFIRM });
            y += ROW_H + SECTION_GAP;

            // ---- secondary actions ----
            y = AddSectionHeader("ACTIONS", y);
            int actW = (innerW - 20) / 3;
            Add(new NiceButton(contentX,                 y, actW, 22, ButtonAction.Activate, "Mark Duplicate") { ButtonParameter = BTN_DUPLICATE });
            Add(new NiceButton(contentX + actW + 10,     y, actW, 22, ButtonAction.Activate, "Discard") { ButtonParameter = BTN_DISCARD });
            Add(new NiceButton(contentX + (actW + 10)*2, y, actW, 22, ButtonAction.Activate, "Reload Index") { ButtonParameter = BTN_REFRESH });
            y += ROW_H + SECTION_GAP;

            // ---- status ----
            y = AddSectionHeader("STATUS", y);
            _statusLabel = new Label("(idle)", true, HUE_VALUE, innerW, 0) { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 3 + INNER_PAD;

            int H = y + INNER_PAD;
            Debug3DStyle.FinalizeShell(this, W, H, _outerBg, _innerBg, _borders);

            X = 460;
            Y = 60;
            WantUpdateSize = false;

            ReloadAndApply();
        }

        private void ReloadAndApply()
        {
            if (!TaggerSession.LoadIfNeeded())
            {
                _statusLabel.Text = "ERR: " + (TaggerSession.LastError ?? "unknown");
                _statusLabel.Hue = HUE_WARN;
                return;
            }
            // Make sure the live AttachmentRenderer has scanned, otherwise the
            // Available list is empty and the tagger can't show anything.
            AttachmentRenderer.RescanAll();
            TaggerSession.ApplyCurrent();
            RefreshUi();
        }

        private void RefreshUi()
        {
            var m = TaggerSession.Current;
            int total = TaggerSession.Models.Count;
            int untagged = TaggerSession.UntaggedCount;
            _progressLabel.Text = $"cursor {TaggerSession.Cursor + 1}/{total}  •  untagged {untagged}";

            if (m == null)
            {
                _modelPathLabel.Text = "(no model)";
                _slotPackLabel.Text = "";
            }
            else
            {
                _modelPathLabel.Text = m.Path;
                _slotPackLabel.Text = $"slot={m.Slot}  pack={m.Pack}  variant={m.Variant}  hue={m.HueCode}";
            }

            // Repaint suggestion buttons.
            for (int i = 0; i < 5; i++)
            {
                var btn = FindButton(BTN_SUGGEST_BASE + i);
                if (btn == null) continue;
                if (m == null || i >= m.Suggestions.Count)
                {
                    btn.TextLabel.Text = "(none)";
                    continue;
                }
                var s = m.Suggestions[i];
                btn.TextLabel.Text = $"[{s.Score}] {s.Layer,-12} {s.Graphic}  {s.Name}";
            }

            _statusLabel.Text = TaggerSession.LastError ?? TaggerSession.LastInfo ?? "(idle)";
            _statusLabel.Hue = TaggerSession.LastError != null ? HUE_WARN : HUE_OK;
        }

        private NiceButton FindButton(int id)
        {
            foreach (var c in Children)
            {
                if (c is NiceButton nb && nb.ButtonParameter == id) return nb;
            }
            return null;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE:
                    Dispose();
                    break;
                case BTN_PREV: TaggerSession.Prev(); break;
                case BTN_SKIP: TaggerSession.Skip(); break;
                case BTN_NEXT: TaggerSession.Next(); break;
                case BTN_CONFIRM:
                    TaggerSession.Confirm(_graphicBox.Text, _layerBox.Text);
                    _graphicBox.SetText("");
                    _layerBox.SetText("");
                    break;
                case BTN_DUPLICATE: TaggerSession.MarkDuplicate(); break;
                case BTN_DISCARD:   TaggerSession.Discard(); break;
                case BTN_REFRESH:
                    // Force a fresh reload by clearing state.
                    TaggerSession.Models.Clear();
                    TaggerSession.Cursor = -1;
                    ReloadAndApply();
                    return;
                default:
                    if (buttonID >= BTN_SUGGEST_BASE && buttonID < BTN_SUGGEST_BASE + 5)
                    {
                        int i = buttonID - BTN_SUGGEST_BASE;
                        var m = TaggerSession.Current;
                        if (m != null && i < m.Suggestions.Count)
                        {
                            var s = m.Suggestions[i];
                            TaggerSession.Confirm(s.Graphic, s.Layer);
                        }
                    }
                    break;
            }
            RefreshUi();
        }

        // ---- shared chrome helpers ----

        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }
    }
}
