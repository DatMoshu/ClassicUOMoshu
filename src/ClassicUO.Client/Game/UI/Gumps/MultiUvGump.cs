// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — gump for tweaking the per-graphic UV / height override
// of multi (and static) tile pieces. Workflow:
//   1. Click "Target" → cursor switches to crosshair.
//   2. Click any tile in the world; the gump captures its graphic ID and
//      shows sliders for the override fields.
//   3. Tweak sliders — every instance of that graphic re-renders with the
//      new values on the next frame.
//   4. Click "Save" — the override is persisted to
//      {bin}/Data/multi_uv_overrides.json so it loads next session.

using System;
using System.Text;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class MultiUvGump : Gump
    {
        public static MultiUvGump Instance;
        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        // ---- Style ----
        private const int W = 500;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = Debug3DStyle.ROW_H;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int LABEL_W     = 130;
        private const int TITLE_BAR_H = Debug3DStyle.TITLE_BAR_H;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;
        private const ushort HUE_OK    = Debug3DStyle.HUE_OK;
        private const ushort HUE_WARN  = Debug3DStyle.HUE_WARN;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        // Selected graphic. 0 = none.
        private ushort _selectedGraphic;
        private string _selectedName = "(no selection)";
        private MultiUvOverride _selected;
        private Label _selectionLabel;
        private Label _statusLabel;

        // Track widgets so we can rebuild on selection change.
        private ScrollArea _scroll;
        private HSliderBar _slOffsetU, _slOffsetV, _slScaleU, _slScaleV, _slHScale, _slHOffset;
        private Checkbox  _cbFlipU, _cbFlipV, _cbVisible;
        private bool _suppressEdits;   // true while syncing widget state -> data; ignore callbacks

        public MultiUvGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            int y = Debug3DStyle.BuildShell(this, W, "MULTI UV EDITOR",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---- Selection block ----
            y = AddSection("SELECTION", y);
            _selectionLabel = new Label(_selectedName, true, HUE_VALUE, innerW, 0)
                { X = contentX, Y = y };
            Add(_selectionLabel);
            y += ROW_H * 2;

            int btnW = (innerW - 20) / 2;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Target Tile") { ButtonParameter = 1 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Pick under cursor") { ButtonParameter = 2 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Reset This") { ButtonParameter = 3 });
            Add(new NiceButton(contentX + btnW + 10, y, btnW, 22, ButtonAction.Activate, "Save All") { ButtonParameter = 4 });
            y += ROW_H + 4;
            Add(new NiceButton(contentX, y, btnW, 22, ButtonAction.Activate, "Dump Values to Console") { ButtonParameter = 5 });
            y += ROW_H + SECTION_GAP;

            // ---- Sliders (always visible; bound to a temp object when no selection) ----
            y = AddSection("UV  ADJUST", y);
            y = AddSlider(contentX, "Offset U", -100, 100, 0, y, v => OnEdit(o => o.OffsetU = v / 100f), out _slOffsetU);
            y = AddSlider(contentX, "Offset V", -100, 100, 0, y, v => OnEdit(o => o.OffsetV = v / 100f), out _slOffsetV);
            y = AddSlider(contentX, "Scale U (x100)", 1, 300, 100, y, v => OnEdit(o => o.ScaleU = v / 100f), out _slScaleU);
            y = AddSlider(contentX, "Scale V (x100)", 1, 300, 100, y, v => OnEdit(o => o.ScaleV = v / 100f), out _slScaleV);
            y += SECTION_GAP;

            y = AddSection("HEIGHT", y);
            y = AddSlider(contentX, "H scale (x100)", 10, 300, 100, y, v => OnEdit(o => o.HeightScale = v / 100f), out _slHScale);
            y = AddSlider(contentX, "H offset (px)", -100, 100, 0, y, v => OnEdit(o => o.HeightOffset = v), out _slHOffset);
            y += SECTION_GAP;

            y = AddSection("TOGGLES", y);
            _cbFlipU   = AddCheck(contentX, "Flip U", false, y, v => OnEdit(o => o.FlipU = v));
            _cbFlipV   = AddCheck(contentX + innerW / 2, "Flip V", false, y, v => OnEdit(o => o.FlipV = v));
            y += ROW_H;
            _cbVisible = AddCheck(contentX, "Visible", true, y, v => OnEdit(o => o.Visible = v));
            y += ROW_H + SECTION_GAP;

            // ---- Status ----
            y = AddSection("STATUS", y);
            _statusLabel = new Label("(idle)", true, HUE_LABEL, innerW, 0) { X = contentX, Y = y };
            Add(_statusLabel);
            y += ROW_H * 3 + INNER_PAD;

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
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case 1: BeginTarget(); break;
                case 2: PickUnderCursor(); break;
                case 3:
                    if (_selectedGraphic != 0)
                    {
                        MultiUvOverrides.Remove(_selectedGraphic);
                        // Re-create as a fresh default and snap widgets back.
                        _selected = MultiUvOverrides.GetOrCreate(_selectedGraphic);
                        SyncWidgetsToSelection();
                        SetStatus($"reset 0x{_selectedGraphic:X4}", HUE_OK);
                    }
                    break;
                case 4:
                    if (MultiUvOverrides.Save())
                        SetStatus($"saved {MultiUvOverrides.All.Count} overrides", HUE_OK);
                    else
                        SetStatus($"save failed: {MultiUvOverrides.LastError}", HUE_WARN);
                    break;
                case 5:
                    Console.WriteLine(BuildDumpString());
                    break;
            }
        }

        private string BuildDumpString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================== [3DCUO] MULTI UV VALUES ====================");
            sb.AppendLine($"  selected graphic = 0x{_selectedGraphic:X4}");
            if (_selected != null && _selectedGraphic != 0)
            {
                sb.AppendLine($"  OffsetU={_selected.OffsetU:F3}  OffsetV={_selected.OffsetV:F3}");
                sb.AppendLine($"  ScaleU={_selected.ScaleU:F3}   ScaleV={_selected.ScaleV:F3}");
                sb.AppendLine($"  FlipU={_selected.FlipU}        FlipV={_selected.FlipV}");
                sb.AppendLine($"  HeightScale={_selected.HeightScale:F3}  HeightOffset={_selected.HeightOffset}");
                sb.AppendLine($"  Visible={_selected.Visible}");
            }
            sb.AppendLine($"  total overrides = {MultiUvOverrides.All.Count}");
            sb.Append(    "==============================================================");
            return sb.ToString();
        }

        private void BeginTarget()
        {
            SetStatus("targeting — click a multi/static tile", HUE_VALUE);
            World.TargetManager.SetTargeting(OnTargetClicked, 0u, TargetType.Neutral);
        }

        private void PickUnderCursor()
        {
            // Use whatever the SelectedObject manager last hovered over.
            var obj = SelectedObject.Object as GameObject;
            if (obj != null) OnTargetClicked(obj);
            else SetStatus("no object under cursor", HUE_WARN);
        }

        private void OnTargetClicked(GameObject obj)
        {
            if (obj == null) { SetStatus("no object selected", HUE_WARN); return; }

            // Accept multi components and statics. Skip mobiles / land — those
            // aren't multi UVs.
            ushort graphic = 0;
            string name = obj.GetType().Name;
            if (obj is Multi m) { graphic = m.Graphic; name = $"Multi 0x{graphic:X4}"; }
            else if (obj is Static s) { graphic = s.Graphic; name = $"Static 0x{graphic:X4}"; }
            else if (obj is Item it && it.IsMulti) { graphic = it.Graphic; name = $"Multi(item) 0x{graphic:X4}"; }
            else
            {
                SetStatus($"selected {name} — not a multi/static, ignoring", HUE_WARN);
                return;
            }

            _selectedGraphic = graphic;
            _selectedName = $"{name}";
            _selected = MultiUvOverrides.GetOrCreate(graphic);
            SyncWidgetsToSelection();
            UpdateSelectionLabel();
            SetStatus($"selected 0x{graphic:X4}", HUE_OK);
        }

        private void OnEdit(Action<MultiUvOverride> mutate)
        {
            if (_selectedGraphic == 0) return;
            _selected ??= MultiUvOverrides.GetOrCreate(_selectedGraphic);
            mutate(_selected);
        }

        private void UpdateSelectionLabel()
        {
            if (_selectionLabel == null) return;
            if (_selected != null && _selectedGraphic != 0)
            {
                _selectionLabel.Text =
                    $"{_selectedName}\n" +
                    $"offU={_selected.OffsetU:F2}  offV={_selected.OffsetV:F2}  " +
                    $"sclU={_selected.ScaleU:F2}  sclV={_selected.ScaleV:F2}\n" +
                    $"flipU={_selected.FlipU}  flipV={_selected.FlipV}  " +
                    $"hScl={_selected.HeightScale:F2}  hOff={_selected.HeightOffset:F0}  vis={_selected.Visible}";
            }
            else
            {
                _selectionLabel.Text = "(no selection — click Target Tile)";
            }
        }

        public override void Update()
        {
            base.Update();
            UpdateSelectionLabel();
        }

        private void SetStatus(string msg, ushort hue)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = msg;
            _statusLabel.Hue = hue;
        }

        // ---- Layout helpers ----
        private int AddSection(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }

        private int AddSlider(int contentX, string label, int min, int max, int initial, int y, Action<int> onChange, out HSliderBar slider)
        {
            const int valueBoxW = Debug3DStyle.VALUE_BOX_W;
            const int resetW    = Debug3DStyle.RESET_BTN_W;
            int sliderW = W - INNER_PAD * 2 - 20 - LABEL_W - valueBoxW - resetW - 24;
            if (sliderW < 40) sliderW = 40;

            Add(new Label(label, true, HUE_LABEL, LABEL_W, 1) { X = contentX, Y = y + 2 });

            var s = new HSliderBar(
                contentX + LABEL_W,
                y + 4,
                sliderW,
                min, max, initial,
                HSliderBarStyle.MetalWidgetRecessedBar,
                hasText: false);
            Add(s);

            var box = new StbTextBox(1, max_char_count: 7, maxWidth: valueBoxW, isunicode: true, hue: HUE_VALUE)
            {
                X = contentX + LABEL_W + sliderW + 6,
                Y = y + 2,
                Width = valueBoxW,
                Height = 20
            };
            box.SetText(initial.ToString());
            Add(box);

            var resetBtn = new NiceButton(
                contentX + LABEL_W + sliderW + valueBoxW + 10,
                y + 2, resetW, 20,
                ButtonAction.Activate, "R")
            { ButtonParameter = -1 };
            Add(resetBtn);

            bool syncing = false;
            s.ValueChanged += (snd, e) =>
            {
                if (syncing) return;
                syncing = true;
                box.SetText(s.Value.ToString());
                syncing = false;
                if (_suppressEdits) return;
                onChange(s.Value);
            };
            box.TextChanged += (snd, e) =>
            {
                if (syncing) return;
                if (int.TryParse(box.Text, out int v))
                {
                    v = Math.Clamp(v, min, max);
                    if (s.Value != v)
                    {
                        syncing = true;
                        s.Value = v;
                        syncing = false;
                        if (_suppressEdits) return;
                        onChange(v);
                    }
                }
            };
            int resetVal = Math.Clamp(initial, min, max);
            resetBtn.MouseUp += (snd, e) =>
            {
                syncing = true;
                s.Value = resetVal;
                box.SetText(resetVal.ToString());
                syncing = false;
                if (_suppressEdits) return;
                onChange(resetVal);
            };
            slider = s;
            return y + ROW_H;
        }

        private Checkbox AddCheck(int x, string label, bool initial, int y, Action<bool> onChange)
        {
            var cb = new Checkbox(0x00D2, 0x00D3, label, font: 1, color: HUE_LABEL, isunicode: true)
                { X = x, Y = y, IsChecked = initial };
            cb.ValueChanged += (s, e) =>
            {
                if (_suppressEdits) return;
                onChange(cb.IsChecked);
            };
            Add(cb);
            return cb;
        }

        private void SyncWidgetsToSelection()
        {
            if (_selected == null) return;
            _suppressEdits = true;
            try
            {
                _slOffsetU.Value = (int)Math.Round(_selected.OffsetU * 100f);
                _slOffsetV.Value = (int)Math.Round(_selected.OffsetV * 100f);
                _slScaleU.Value  = (int)Math.Round(_selected.ScaleU  * 100f);
                _slScaleV.Value  = (int)Math.Round(_selected.ScaleV  * 100f);
                _slHScale.Value  = (int)Math.Round(_selected.HeightScale * 100f);
                _slHOffset.Value = (int)Math.Round(_selected.HeightOffset);
                _cbFlipU.IsChecked   = _selected.FlipU;
                _cbFlipV.IsChecked   = _selected.FlipV;
                _cbVisible.IsChecked = _selected.Visible;
            }
            finally { _suppressEdits = false; }
        }
    }
}
