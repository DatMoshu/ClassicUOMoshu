// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO prototype — admin debug gump for the player's bone-attachment slots
// (Head / Back / HipFront / HipBack / HipLeft / HipRight). Drives
// AttachmentRenderer.Slots[]. The slot dropdown selects which slot the rest of
// the controls operate on; switching slots rebinds the sliders to that slot's
// state.

using System;
using ClassicUO.Assets;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer.Renderer3D;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    internal sealed class PlayerMountsGump : Gump
    {
        public static PlayerMountsGump Instance;

        public override void Dispose()
        {
            if (Instance == this) Instance = null;
            base.Dispose();
        }

        private const int W = 580;
        private const int INNER_PAD   = Debug3DStyle.INNER_PAD;
        private const int ROW_H       = 22;
        private const int SECTION_GAP = Debug3DStyle.SECTION_GAP;
        private const int TITLE_BAR_H = Debug3DStyle.TITLE_BAR_H;

        private const ushort HUE_LABEL = Debug3DStyle.HUE_LABEL_WHITE;
        private const ushort HUE_VALUE = Debug3DStyle.HUE_VALUE;
        private const ushort HUE_OK    = Debug3DStyle.HUE_OK;
        private const ushort HUE_WARN  = Debug3DStyle.HUE_WARN;
        private ResizePic _outerBg;
        private ResizePic _innerBg;
        private Line[] _borders;

        private int _selectedSlot;

        private NiceButton _slotButton;
        private Label _slotMetaLabel;
        private Label _hatPathLabel;
        private Label _statusLabel;
        private NiceButton _speciesButton;

        // Per-slot widgets — rebound on slot change.
        private HSliderBar _itemSlider;
        private StbTextBox _itemBox;
        private SliderRow _scaleRow, _yawRow, _pitchRow, _rollRow, _offXRow, _offYRow, _offZRow;
        private bool _syncingItem;
        private bool _rebinding;

        public PlayerMountsGump(World world) : base(world, 0, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            Width = W;

            // Populate slot lists once.
            AttachmentRenderer.RescanAll();

            int y = Debug3DStyle.BuildShell(this, W, "3DCUO PLAYER MOUNTS",
                out _outerBg, out _innerBg, out _borders);
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;

            // ---------- Master ----------
            y = AddSectionHeader("MASTER", y);
            var enabledCb = new Checkbox(0x00D2, 0x00D3, "AttachmentRenderer.Enabled",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX, Y = y, IsChecked = AttachmentRenderer.Enabled };
            enabledCb.ValueChanged += (s, e) => AttachmentRenderer.Enabled = enabledCb.IsChecked;
            Add(enabledCb);
            y += ROW_H;

            // Debug A/B: force identity skin palette on every attachment so we can
            // see static placement (no animation) vs skinned placement at the same
            // moment.
            var idCb = new Checkbox(0x00D2, 0x00D3, "Force IDENTITY palette (no anim)",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX, Y = y, IsChecked = AttachmentRenderer.ForceIdentityPalette };
            idCb.ValueChanged += (s, e) => AttachmentRenderer.ForceIdentityPalette = idCb.IsChecked;
            Add(idCb);
            y += ROW_H;

            // Definitive visibility test — paints every attachment bright RED so
            // we can see whether they're being drawn at all.
            var dbgCb = new Checkbox(0x00D2, 0x00D3, "Force RED debug color (visibility test)",
                font: 1, color: HUE_LABEL, isunicode: true)
                { X = contentX, Y = y, IsChecked = AttachmentRenderer.ForceDebugColor };
            dbgCb.ValueChanged += (s, e) => AttachmentRenderer.ForceDebugColor = dbgCb.IsChecked;
            Add(dbgCb);
            y += ROW_H;

            // Species cycler — drives base-mesh fallback (used when a slot has no
            // equipment selected). Click to cycle through species in the registry.
            Add(new Label("Species", true, HUE_LABEL, 60, 1) { X = contentX, Y = y + 2 });
            Add(new NiceButton(contentX + 60, y, 26, 22, ButtonAction.Activate, "<") { ButtonParameter = 201 });
            _speciesButton = new NiceButton(contentX + 90, y, 180, 22, ButtonAction.Activate,
                AttachmentRenderer.CurrentSpecies) { ButtonParameter = 200 };
            Add(_speciesButton);
            Add(new NiceButton(contentX + 274, y, 26, 22, ButtonAction.Activate, ">") { ButtonParameter = 200 });
            y += ROW_H + SECTION_GAP;

            // ---------- Slot picker ----------
            y = AddSectionHeader("SLOT", y);
            Add(new Label("Slot", true, HUE_LABEL, 40, 1) { X = contentX, Y = y + 2 });

            // < Prev button — wraps from 0 to last slot
            Add(new NiceButton(contentX + 40, y, 26, 22, ButtonAction.Activate, "<") { ButtonParameter = 101 });

            // Slot label/picker — clicking advances forward (legacy behavior)
            _slotButton = new NiceButton(contentX + 70, y, 180, 22, ButtonAction.Activate,
                SlotButtonLabel(0)) { ButtonParameter = 100 };
            Add(_slotButton);

            // > Next button (mirrors clicking the slot label)
            Add(new NiceButton(contentX + 254, y, 26, 22, ButtonAction.Activate, ">") { ButtonParameter = 102 });

            _slotMetaLabel = new Label("", true, HUE_LABEL, innerW - 290, 1)
                { X = contentX + 290, Y = y + 2 };
            Add(_slotMetaLabel);
            y += ROW_H + SECTION_GAP;

            // ---------- Selected item ----------
            y = AddSectionHeader("ITEM", y);
            Add(new Label("Item", true, HUE_LABEL, 40, 1) { X = contentX, Y = y + 2 });

            _itemSlider = new HSliderBar(
                contentX + 40, y + 4, innerW - 40 - 70,
                0, 1, 0,
                HSliderBarStyle.MetalWidgetRecessedBar, hasText: false);
            Add(_itemSlider);

            _itemBox = new StbTextBox(1, max_char_count: 5, maxWidth: 60, isunicode: true, hue: HUE_VALUE)
            {
                X = contentX + innerW - 60, Y = y + 2, Width = 60, Height = 20
            };
            Add(_itemBox);

            _itemSlider.ValueChanged += (s, e) =>
            {
                if (_syncingItem || _rebinding) return;
                int idx = _itemSlider.Value - 1;
                _syncingItem = true;
                _itemBox.SetText(idx.ToString());
                CurrentSlot.CurrentIndex = idx;
                UpdateItemPathLabel();
                _syncingItem = false;
            };
            _itemBox.TextChanged += (s, e) =>
            {
                if (_syncingItem || _rebinding) return;
                if (int.TryParse(_itemBox.Text, out int idx))
                {
                    idx = Math.Clamp(idx, -1, CurrentSlot.Available.Count - 1);
                    _syncingItem = true;
                    _itemSlider.Value = idx + 1;
                    CurrentSlot.CurrentIndex = idx;
                    UpdateItemPathLabel();
                    _syncingItem = false;
                }
            };
            y += ROW_H + 2;

            _hatPathLabel = new Label("(none)", true, HUE_VALUE, innerW, 1) { X = contentX, Y = y };
            Add(_hatPathLabel);
            y += ROW_H + SECTION_GAP;

            // ---------- Tuning ----------
            y = AddSectionHeader("TUNING (selected slot)", y);
            _scaleRow = AddFloatSlider("Scale",  CurrentSlot.Scale,            0.01f, 5.0f, contentX, ref y, innerW);
            _yawRow   = AddFloatSlider("Yaw",    CurrentSlot.YawDegrees,      -180f, 180f, contentX, ref y, innerW);
            _pitchRow = AddFloatSlider("Pitch",  CurrentSlot.PitchDegrees,    -180f, 180f, contentX, ref y, innerW);
            _rollRow  = AddFloatSlider("Roll",   CurrentSlot.RollDegrees,     -180f, 180f, contentX, ref y, innerW);
            _offXRow  = AddFloatSlider("Off X",  CurrentSlot.LocalOffset.X,   -100f, 100f, contentX, ref y, innerW);
            _offYRow  = AddFloatSlider("Off Y",  CurrentSlot.LocalOffset.Y,   -100f, 100f, contentX, ref y, innerW);
            _offZRow  = AddFloatSlider("Off Z",  CurrentSlot.LocalOffset.Z,   -100f, 100f, contentX, ref y, innerW);

            _scaleRow.OnChange = v => CurrentSlot.Scale = v;
            _yawRow.OnChange   = v => CurrentSlot.YawDegrees   = v;
            _pitchRow.OnChange = v => CurrentSlot.PitchDegrees = v;
            _rollRow.OnChange  = v => CurrentSlot.RollDegrees  = v;
            _offXRow.OnChange  = v => { var o = CurrentSlot.LocalOffset; o.X = v; CurrentSlot.LocalOffset = o; };
            _offYRow.OnChange  = v => { var o = CurrentSlot.LocalOffset; o.Y = v; CurrentSlot.LocalOffset = o; };
            _offZRow.OnChange  = v => { var o = CurrentSlot.LocalOffset; o.Z = v; CurrentSlot.LocalOffset = o; };

            y += SECTION_GAP;

            // ---------- Buttons ----------
            int btnW = (innerW - 40) / 5;
            Add(new NiceButton(contentX + 0*(btnW+10), y, btnW, 22, ButtonAction.Activate, "Refresh") { ButtonParameter = 1 });
            Add(new NiceButton(contentX + 1*(btnW+10), y, btnW, 22, ButtonAction.Activate, "Reset slot") { ButtonParameter = 2 });
            Add(new NiceButton(contentX + 2*(btnW+10), y, btnW, 22, ButtonAction.Activate, "Clear all") { ButtonParameter = 3 });
            Add(new NiceButton(contentX + 3*(btnW+10), y, btnW, 22, ButtonAction.Activate, "Randomize") { ButtonParameter = 5 });
            Add(new NiceButton(contentX + 4*(btnW+10), y, btnW, 22, ButtonAction.Activate, "Dump") { ButtonParameter = 4 });
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

            RebindToSlot(0);
            int total = 0;
            foreach (var s in AttachmentRenderer.Slots) total += s.Available.Count;
            SetStatus($"{AttachmentRenderer.Slots.Length} slots / {total} glbs", HUE_OK);
        }

        private AttachSlot CurrentSlot => AttachmentRenderer.Slots[_selectedSlot];

        private string SlotButtonLabel(int idx)
        {
            var s = AttachmentRenderer.Slots[idx];
            return $"{idx + 1}/{AttachmentRenderer.Slots.Length}  {s.DisplayName}";
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case Debug3DStyle.BTN_CLOSE: Dispose(); break;
                case 100: // cycle slot forward (also fired by ">")
                case 102:
                    _selectedSlot = (_selectedSlot + 1) % AttachmentRenderer.Slots.Length;
                    RebindToSlot(_selectedSlot);
                    break;
                case 101: // cycle slot backward ("<")
                    _selectedSlot = (_selectedSlot - 1 + AttachmentRenderer.Slots.Length) % AttachmentRenderer.Slots.Length;
                    RebindToSlot(_selectedSlot);
                    break;
                case 200: // species forward
                case 201: // species backward
                    {
                        BaseMeshRegistry.EnsureLoaded();
                        var list = BaseMeshRegistry.AvailableSpecies;
                        if (list != null && list.Length > 0)
                        {
                            int cur = Array.IndexOf(list, AttachmentRenderer.CurrentSpecies);
                            if (cur < 0) cur = 0;
                            int next = buttonID == 200
                                ? (cur + 1) % list.Length
                                : (cur - 1 + list.Length) % list.Length;
                            AttachmentRenderer.SetCurrentSpecies(list[next]);
                            if (_speciesButton != null) _speciesButton.TextLabel.Text = list[next];
                            SetStatus($"species -> {list[next]}", HUE_OK);
                        }
                        else
                        {
                            SetStatus($"no species in registry ({BaseMeshRegistry.LastError})", HUE_WARN);
                        }
                    }
                    break;
                case 1: // Refresh — rescan disk + rebuild item slider
                    AttachmentRenderer.Invalidate();
                    AttachmentRenderer.RescanAll();
                    RebindToSlot(_selectedSlot);
                    int total = 0;
                    foreach (var s in AttachmentRenderer.Slots) total += s.Available.Count;
                    SetStatus($"rescanned: {total} glbs across {AttachmentRenderer.Slots.Length} slots", HUE_OK);
                    break;
                case 2: // reset current slot
                    var slot = CurrentSlot;
                    slot.Scale = 1.0f;
                    slot.YawDegrees = 0f;
                    slot.PitchDegrees = 0f;
                    slot.RollDegrees = 0f;
                    slot.LocalOffset = Vector3.Zero;
                    RebindToSlot(_selectedSlot);
                    SetStatus($"{slot.DisplayName} tuning reset", HUE_OK);
                    break;
                case 3: // clear all (no attachments visible)
                    foreach (var sl in AttachmentRenderer.Slots) sl.CurrentIndex = -1;
                    RebindToSlot(_selectedSlot);
                    SetStatus("cleared all slots", HUE_OK);
                    break;
                case 4: // dump
                    foreach (var sl in AttachmentRenderer.Slots)
                    {
                        Console.WriteLine(
                            $"[3DCUO][mounts] {sl.DisplayName,-10} bone='{sl.BoneName}' " +
                            $"idx={sl.CurrentIndex}/{sl.Available.Count} '{sl.CurrentPath}' " +
                            $"scale={sl.Scale} yaw={sl.YawDegrees} pitch={sl.PitchDegrees} " +
                            $"roll={sl.RollDegrees} off={sl.LocalOffset} err='{sl.LastError}'");
                    }
                    SetStatus("dumped all slots to console", HUE_OK);
                    break;
                case 5: // randomize all — pick a random valid index per slot
                    // Hair vs Hat are mutually exclusive (hair clips through helmets,
                    // hats hide most hair anyway). Roll once for which one wins.
                    bool pickHair = _rng.Next(2) == 0;
                    int populated = 0, cleared = 0;
                    foreach (var sl in AttachmentRenderer.Slots)
                    {
                        // Hands + teeth never randomize — they don't read well with
                        // skinning approximations and clutter the silhouette.
                        if (sl.Kind == AttachSlotKind.HandL ||
                            sl.Kind == AttachSlotKind.HandR ||
                            sl.Kind == AttachSlotKind.Teeth)
                        {
                            sl.CurrentIndex = -1;
                            cleared++;
                            continue;
                        }
                        // Hair-vs-Hat exclusion.
                        if (sl.Kind == AttachSlotKind.Head && pickHair) { sl.CurrentIndex = -1; cleared++; continue; }
                        if (sl.Kind == AttachSlotKind.Hair && !pickHair) { sl.CurrentIndex = -1; cleared++; continue; }

                        if (sl.Available.Count == 0) { sl.CurrentIndex = -1; continue; }
                        sl.CurrentIndex = _rng.Next(0, sl.Available.Count);
                        populated++;
                    }
                    RebindToSlot(_selectedSlot);
                    SetStatus($"randomized {populated}, cleared {cleared} (pick={(pickHair ? "hair" : "hat")})", HUE_OK);
                    break;
            }
        }

        private static readonly Random _rng = new Random();

        private void RebindToSlot(int idx)
        {
            _rebinding = true;
            _selectedSlot = Math.Clamp(idx, 0, AttachmentRenderer.Slots.Length - 1);
            var slot = CurrentSlot;

            if (_slotButton != null) _slotButton.TextLabel.Text = SlotButtonLabel(_selectedSlot);
            if (_slotMetaLabel != null)
                _slotMetaLabel.Text = $"bone '{slot.BoneName}' • {slot.Available.Count} glbs";

            // Item slider range.
            int max = Math.Max(1, slot.Available.Count);
            _itemSlider.MaxValue = max;
            int curIdx = Math.Clamp(slot.CurrentIndex, -1, slot.Available.Count - 1);
            slot.CurrentIndex = curIdx;
            _itemSlider.Value = curIdx + 1;
            _itemBox.SetText(curIdx.ToString());
            UpdateItemPathLabel();

            // Tuning sliders.
            _scaleRow.SetValue(slot.Scale);
            _yawRow.SetValue(slot.YawDegrees);
            _pitchRow.SetValue(slot.PitchDegrees);
            _rollRow.SetValue(slot.RollDegrees);
            _offXRow.SetValue(slot.LocalOffset.X);
            _offYRow.SetValue(slot.LocalOffset.Y);
            _offZRow.SetValue(slot.LocalOffset.Z);

            _rebinding = false;
        }

        private void UpdateItemPathLabel()
        {
            if (_hatPathLabel == null) return;
            var slot = CurrentSlot;
            int idx = slot.CurrentIndex;
            if (idx < 0)
            {
                _hatPathLabel.Text = $"({slot.DisplayName} — none)";
                _hatPathLabel.Hue = HUE_LABEL;
                return;
            }
            string path = slot.CurrentPath;
            int slash = path.LastIndexOf('/');
            string display = slash >= 0 ? path.Substring(slash + 1) : path;
            _hatPathLabel.Text = $"[{idx}] {display}";
            _hatPathLabel.Hue = HUE_VALUE;
        }

        private sealed class SliderRow
        {
            public HSliderBar Slider;
            public StbTextBox Box;
            public Action<float> OnChange;
            public float Min, Max;
            public bool Syncing;

            public int Encode(float v) => (int)MathF.Round((v - Min) / (Max - Min) * 1000f);
            public float Decode(int s) => Min + (s / 1000f) * (Max - Min);

            public void SetValue(float v)
            {
                v = Math.Clamp(v, Min, Max);
                Syncing = true;
                Slider.Value = Math.Clamp(Encode(v), 0, 1000);
                Box.SetText(v.ToString("F2"));
                Syncing = false;
            }
        }

        private SliderRow AddFloatSlider(string label, float initial, float min, float max,
                                         int x, ref int y, int innerW)
        {
            const int LABEL_W = 50;
            const int VALUE_W = Debug3DStyle.VALUE_BOX_W;
            const int RESET_W = Debug3DStyle.RESET_BTN_W;
            int sliderW = innerW - LABEL_W - VALUE_W - RESET_W - 24;
            if (sliderW < 40) sliderW = 40;

            Add(new Label(label, true, HUE_LABEL, LABEL_W, 1) { X = x, Y = y + 2 });

            var row = new SliderRow { Min = min, Max = max };
            int initInt = Math.Clamp(row.Encode(initial), 0, 1000);

            row.Slider = new HSliderBar(
                x + LABEL_W, y + 4, sliderW,
                0, 1000, initInt,
                HSliderBarStyle.MetalWidgetRecessedBar, hasText: false);
            Add(row.Slider);

            row.Box = new StbTextBox(1, max_char_count: 8, maxWidth: VALUE_W, isunicode: true, hue: HUE_VALUE)
            {
                X = x + LABEL_W + sliderW + 6,
                Y = y + 2,
                Width = VALUE_W,
                Height = 20
            };
            row.Box.SetText(initial.ToString("F2"));
            Add(row.Box);

            var resetBtn = new NiceButton(
                x + LABEL_W + sliderW + VALUE_W + 10,
                y + 2, RESET_W, 20,
                ButtonAction.Activate, "R")
            { ButtonParameter = -1 };
            Add(resetBtn);

            row.Slider.ValueChanged += (s, e) =>
            {
                if (row.Syncing || _rebinding) return;
                row.Syncing = true;
                float v = row.Decode(row.Slider.Value);
                row.Box.SetText(v.ToString("F2"));
                row.OnChange?.Invoke(v);
                row.Syncing = false;
            };
            row.Box.TextChanged += (s, e) =>
            {
                if (row.Syncing || _rebinding) return;
                if (float.TryParse(row.Box.Text, out float v))
                {
                    v = Math.Clamp(v, row.Min, row.Max);
                    int iv = Math.Clamp(row.Encode(v), 0, 1000);
                    if (row.Slider.Value != iv)
                    {
                        row.Syncing = true;
                        row.Slider.Value = iv;
                        row.OnChange?.Invoke(v);
                        row.Syncing = false;
                    }
                }
            };
            float resetVal = Math.Clamp(initial, min, max);
            resetBtn.MouseUp += (s, e) =>
            {
                if (_rebinding) return;
                row.Syncing = true;
                row.Slider.Value = Math.Clamp(row.Encode(resetVal), 0, 1000);
                row.Box.SetText(resetVal.ToString("F2"));
                row.OnChange?.Invoke(resetVal);
                row.Syncing = false;
            };

            y += ROW_H;
            return row;
        }

        private void SetStatus(string text, ushort hue)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = text;
            _statusLabel.Hue = hue;
        }

        private int AddSectionHeader(string title, int y)
        {
            int contentX = INNER_PAD + 10;
            int innerW = W - INNER_PAD * 2 - 20;
            return Debug3DStyle.AddSectionHeader(this, contentX, innerW, title, y);
        }
    }
}
