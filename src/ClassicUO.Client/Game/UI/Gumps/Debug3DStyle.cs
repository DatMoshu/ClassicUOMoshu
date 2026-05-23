// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — shared theming + control helpers for all 3D debug gumps.
// Centralises: black-bordered title bar with centered title + X close button,
// white section headers, slider-with-textbox-and-reset rows, and a common
// "Dump Values to Console" button.

using System;
using ClassicUO.Assets;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps
{
    internal static class Debug3DStyle
    {
        // ---- Layout ----
        // Title bar height was 28; bumped to 32 so the centered Label (font 1,
        // ~16px tall) clears the top edge of the stone frame art and the close
        // button doesn't sit on the corner ornament.
        public const int TITLE_BAR_H = 32;
        public const int INNER_PAD   = 12;
        public const int ROW_H       = 24;
        public const int SECTION_GAP = 10;

        // The inner wood panel (ResizePic 0x13BE) draws ~9-10px of border art
        // on every edge. Real content must inset further than INNER_PAD or it
        // renders on top of the wood frame. CONTENT_INSET is that extra inset.
        public const int CONTENT_INSET = 10;

        public static int GetContentX() => INNER_PAD + CONTENT_INSET;
        public static int GetContentWidth(int gumpW) => gumpW - (INNER_PAD + CONTENT_INSET) * 2;

        // Standard slider row geometry.
        public const int VALUE_BOX_W = 56;
        public const int RESET_BTN_W = 36;

        // ---- Hues ----
        // Title text: hue 0 with unicode font 1 = pure black bitmap render — readable
        // on the stone title bar.
        public const ushort HUE_TITLE_BLACK   = 0;
        public const ushort HUE_LABEL_WHITE   = 1153;
        public const ushort HUE_SECTION_WHITE = 1153;
        public const ushort HUE_VALUE         = 998;
        public const ushort HUE_OK            = 88;
        public const ushort HUE_WARN          = 53;

        // ---- Borders / dividers ----
        // Black for everything per the new spec.
        public const uint BORDER_RGBA  = 0xFF000000;
        public const uint DIVIDER_RGBA = 0xFF000000;

        // Standard close button art (small X) — same as PartyGump uses.
        private const ushort CLOSE_BTN_NORMAL  = 0x0FB1;
        private const ushort CLOSE_BTN_PRESSED = 0x0FB3;
        private const ushort CLOSE_BTN_OVER    = 0x0FB2;

        /// Reserved button IDs used by the shared shell. Gump-specific button
        /// switch statements MUST forward these to the shared handler.
        public const int BTN_CLOSE          = 9990;
        public const int BTN_DUMP_TO_CONSOLE = 9991;

        /// Build the outer stone shell + black border + centered black title +
        /// X close button + inner dark content panel. Returns the Y of the first
        /// content row. The caller must resize <paramref name="outerBg"/> /
        /// <paramref name="innerBg"/> at the end of construction once final
        /// height is known.
        public static int BuildShell(
            Gump gump, int width, string title,
            out ResizePic outerBg, out ResizePic innerBg,
            out Line[] borderLines)
        {
            // Outer stone background.
            outerBg = new ResizePic(0x0A28) { Width = width, Height = 100, X = 0, Y = 0 };
            gump.Add(outerBg);

            // Centered black title — inset from both edges so the centered
            // text doesn't ride the stone-frame top corners and so the close
            // button has a clear reserved zone on the right.
            const int TITLE_INSET = 28;
            int titleW = width - TITLE_INSET * 2;
            gump.Add(new Label(title, true, HUE_TITLE_BLACK, titleW, font: 1,
                align: TEXT_ALIGN_TYPE.TS_CENTER)
                { X = TITLE_INSET, Y = 9 });

            // X close button — sits inside the reserved title-bar right zone,
            // not on the corner ornament of the stone frame.
            gump.Add(new Button(BTN_CLOSE, CLOSE_BTN_NORMAL, CLOSE_BTN_PRESSED, CLOSE_BTN_OVER)
                { X = width - 26, Y = 10, ButtonAction = ButtonAction.Activate });

            // Thin black divider under the title bar — formal boundary so the
            // title visually separates from the inner content panel art.
            gump.Add(new Line(INNER_PAD, TITLE_BAR_H - 2, width - INNER_PAD * 2, 1, BORDER_RGBA));

            // Inner content panel.
            innerBg = new ResizePic(0x13BE)
            {
                X = INNER_PAD,
                Y = TITLE_BAR_H,
                Width = width - INNER_PAD * 2,
                Height = 100
            };
            gump.Add(innerBg);

            // Black border lines around the outer stone frame. We add them now
            // with placeholder height; FinalizeShell resizes the bottom/right
            // edges once total height is known.
            var top    = new Line(0, 0, width, 1, BORDER_RGBA);
            var bottom = new Line(0, 99, width, 1, BORDER_RGBA);
            var left   = new Line(0, 0, 1, 100, BORDER_RGBA);
            var right  = new Line(width - 1, 0, 1, 100, BORDER_RGBA);
            gump.Add(top);
            gump.Add(bottom);
            gump.Add(left);
            gump.Add(right);
            borderLines = new[] { top, bottom, left, right };

            return TITLE_BAR_H + INNER_PAD;
        }

        /// Resize backgrounds + borders to the final gump height.
        public static void FinalizeShell(
            Gump gump, int width, int height,
            ResizePic outerBg, ResizePic innerBg, Line[] borderLines)
        {
            gump.Width  = width;
            gump.Height = height;
            outerBg.Height = height;
            innerBg.Height = height - TITLE_BAR_H - INNER_PAD;

            if (borderLines != null && borderLines.Length == 4)
            {
                // borderLines[0] = top  (already correct y=0)
                borderLines[1].Y      = height - 1;
                borderLines[1].Width  = width;
                borderLines[2].Height = height;
                borderLines[3].X      = width - 1;
                borderLines[3].Height = height;
            }
        }

        /// Add a white section header label + black underline divider.
        /// Returns the Y of the first row beneath the header.
        public static int AddSectionHeader(Gump gump, int x, int innerW, string title, int y)
        {
            gump.Add(new Label(title, true, HUE_SECTION_WHITE, innerW, font: 1)
                { X = x, Y = y });
            gump.Add(new Line(x, y + 18, innerW, 1, DIVIDER_RGBA));
            return y + ROW_H + 6;
        }

        /// Add a single slider row: label > slider > textbox (live-synced) > Reset button.
        /// The reset button reverts to <paramref name="resetValue"/> (defaults to the initial value).
        public static int AddSlider(
            Gump gump,
            int contentX, int rowWidth,
            int labelW,
            string label, int min, int max, int initial,
            int y,
            Action<int> onChange,
            int? resetValue = null)
        {
            int reset = resetValue ?? initial;
            int sliderW = rowWidth - labelW - VALUE_BOX_W - RESET_BTN_W - 24;
            if (sliderW < 40) sliderW = 40;

            gump.Add(new Label(label, true, HUE_LABEL_WHITE, labelW, font: 1)
                { X = contentX, Y = y + 2 });

            var slider = new HSliderBar(
                contentX + labelW, y + 4,
                sliderW, min, max, initial,
                HSliderBarStyle.MetalWidgetRecessedBar,
                hasText: false);
            gump.Add(slider);

            // Dark backdrop behind the editable value box so the digits read
            // against the stone panel (without it the StbTextBox is just bare
            // glyphs on the same hue family — invisible at a glance).
            int boxX = contentX + labelW + sliderW + 6;
            int boxY = y + 2;
            gump.Add(new AlphaBlendControl(0.55f)
            {
                X = boxX - 2,
                Y = boxY,
                Width = VALUE_BOX_W + 4,
                Height = 20
            });
            // Thin black frame so the box reads as an interactive field.
            gump.Add(new Line(boxX - 2,                  boxY,                  VALUE_BOX_W + 4, 1, BORDER_RGBA));
            gump.Add(new Line(boxX - 2,                  boxY + 19,             VALUE_BOX_W + 4, 1, BORDER_RGBA));
            gump.Add(new Line(boxX - 2,                  boxY,                  1, 20,              BORDER_RGBA));
            gump.Add(new Line(boxX + VALUE_BOX_W + 1,    boxY,                  1, 20,              BORDER_RGBA));

            var box = new StbTextBox(1, max_char_count: 7, maxWidth: VALUE_BOX_W,
                isunicode: true, hue: HUE_VALUE)
            {
                X = boxX,
                Y = boxY + 2,
                Width = VALUE_BOX_W,
                Height = 18
            };
            gump.Add(box);
            // Set after Add so the renderer is initialised when the text lands.
            box.SetText(initial.ToString());

            var resetBtn = new NiceButton(
                contentX + labelW + sliderW + VALUE_BOX_W + 10,
                y + 2,
                RESET_BTN_W, 20,
                ButtonAction.Activate,
                "R")
            { ButtonParameter = -1 }; // -1 = inert; we handle via lambda
            gump.Add(resetBtn);

            bool syncing = false;
            slider.ValueChanged += (s, e) =>
            {
                if (syncing) return;
                syncing = true;
                box.SetText(slider.Value.ToString());
                syncing = false;
                onChange?.Invoke(slider.Value);
            };
            box.TextChanged += (s, e) =>
            {
                if (syncing) return;
                if (int.TryParse(box.Text, out int v))
                {
                    v = Math.Clamp(v, min, max);
                    if (slider.Value != v)
                    {
                        syncing = true;
                        slider.Value = v;
                        syncing = false;
                        onChange?.Invoke(v);
                    }
                }
            };
            resetBtn.MouseUp += (s, e) =>
            {
                int v = Math.Clamp(reset, min, max);
                syncing = true;
                slider.Value = v;
                box.SetText(v.ToString());
                syncing = false;
                onChange?.Invoke(v);
            };

            return y + ROW_H;
        }

        /// Add a checkbox helper using the shared label hue.
        public static Checkbox AddCheck(Gump gump, int x, int y, string label, bool initial,
            Action<bool> onChange)
        {
            var cb = new Checkbox(0x00D2, 0x00D3, label, font: 1, color: HUE_LABEL_WHITE,
                isunicode: true)
                { X = x, Y = y, IsChecked = initial };
            if (onChange != null)
                cb.ValueChanged += (s, e) => onChange(cb.IsChecked);
            gump.Add(cb);
            return cb;
        }

        /// Reserve vertical space for a footer separator and return the Y at
        /// which the first footer button should be placed. Draws a thin black
        /// divider above the footer row so footer actions read as distinct
        /// from the section content above.
        public static int BeginFooter(Gump gump, int contentX, int contentW, int y)
        {
            y += SECTION_GAP;
            gump.Add(new Line(contentX, y, contentW, 1, DIVIDER_RGBA));
            return y + SECTION_GAP;
        }

        /// Returns Y after the row.
        public static int AddTwoColCheck(
            Gump gump, int contentX, int innerW, int y,
            string label1, bool init1, Action<bool> on1,
            string label2, bool init2, Action<bool> on2)
        {
            AddCheck(gump, contentX, y, label1, init1, on1);
            if (!string.IsNullOrEmpty(label2))
                AddCheck(gump, contentX + innerW / 2, y, label2, init2, on2);
            return y + ROW_H;
        }
    }
}
