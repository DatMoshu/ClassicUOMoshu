// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Globalization;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Client-side character-sheet editor for an RpgTable piece. Supports
    /// arbitrary user-defined fields (int / string / bool / counter), a
    /// scrollable inventory list, configurable portrait + background art, and
    /// a free-form notes panel. Persisted via
    /// <see cref="RpgCharacterSheetManager"/>; never sent to the server.
    ///
    /// Layout (600 × 640):
    ///   Top:      character name
    ///   Left:     Stats panel — scrollable typed fields + Add control
    ///   Right T:  Art panel — portrait gump id with preview
    ///   Right B:  Inventory panel — scrollable expandable item list
    ///   Bottom:   Notes panel — multi-line scrollable text
    ///   Footer:   Background gump id, Save, Close
    /// </summary>
    internal sealed class RpgCharacterSheetGump : Gump
    {
        // Button IDs --------------------------------------------------------
        private const int BtnClose      = 1;
        private const int BtnSave       = 2;
        private const int BtnAddField   = 3;
        private const int BtnAddItem    = 4;
        private const int BtnCycleType  = 5;
        private const int BtnApplyBg    = 6;
        private const int BtnApplyArt   = 7;

        // Per-row button parameter encoding: kind * 1000 + rowIndex
        private const int KindDeleteField = 1;   // 1000..1999
        private const int KindToggleBool  = 2;   // 2000..2999
        private const int KindDeleteItem  = 3;   // 3000..3999
        private const int KindCounterInc  = 4;   // 4000..4999
        private const int KindCounterDec  = 5;   // 5000..5999

        // Field editor sizing
        private const int RowH = 28;
        private const int InvRowH = 26;

        private readonly World _world;
        private readonly RpgCharacterSheet _sheet;

        // Per-build control references for read-back on Save
        private StbTextBox _nameBox;
        private StbTextBox _bgBox;
        private StbTextBox _portraitBox;
        private StbTextBox _notesBox;

        private StbTextBox _newFieldName;
        private SheetFieldType _newFieldType = SheetFieldType.Int;
        private Label _newFieldTypeLabel;

        private readonly Dictionary<string, StbTextBox> _fieldNameBoxes = new();
        private readonly Dictionary<string, StbTextBox> _fieldValueBoxes = new();
        private readonly Dictionary<string, StbTextBox> _fieldMaxBoxes = new();
        private readonly Dictionary<string, Checkbox> _fieldBoolBoxes = new();

        private readonly Dictionary<string, StbTextBox> _itemNameBoxes = new();
        private readonly Dictionary<string, StbTextBox> _itemQtyBoxes = new();

        private readonly List<string> _fieldOrder = new();
        private readonly List<string> _itemOrder = new();

        public RpgCharacterSheetGump(World world, RpgCharacterSheet sheet)
            : base(world, (uint)sheet.SheetId.GetHashCode(), 0)
        {
            _world = world;
            _sheet = sheet;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 600;
            Height = 640;
            X = 100;
            Y = 60;

            Build();
        }

        public static void Open(World world, uint tableSerial, int pieceNumber)
        {
            if (pieceNumber < 0) return;
            var sheet = RpgCharacterSheetManager.GetOrCreate(tableSerial, pieceNumber);

            foreach (var g in UIManager.Gumps)
            {
                if (g is RpgCharacterSheetGump existing && existing._sheet.SheetId == sheet.SheetId)
                {
                    existing.BringOnTop();
                    return;
                }
            }

            UIManager.Add(new RpgCharacterSheetGump(world, sheet));
        }

        // -------------------------------------------------------------------
        // Build
        // -------------------------------------------------------------------

        private void Build()
        {
            Clear();
            _fieldNameBoxes.Clear();
            _fieldValueBoxes.Clear();
            _fieldMaxBoxes.Clear();
            _fieldBoolBoxes.Clear();
            _itemNameBoxes.Clear();
            _itemQtyBoxes.Clear();
            _fieldOrder.Clear();
            _itemOrder.Clear();

            Add(new ResizePic(_sheet.BackgroundGumpId) { Width = Width, Height = Height });

            BuildHeader();
            BuildStatsPanel(10, 60, 290, 360);
            BuildArtPanel(310, 60, 280, 180);
            BuildInventoryPanel(310, 250, 280, 170);
            BuildNotesPanel(10, 430, 580, 150);
            BuildFooter();
        }

        private void BuildHeader()
        {
            Add(new Label("Character:", true, 0x0481) { X = 14, Y = 14 });
            _nameBox = MakeBox(110, 12, 320, _sheet.CharacterName, 64);
        }

        private void BuildStatsPanel(int x, int y, int w, int h)
        {
            Add(new Label("Stats", true, 0x0481) { X = x + 4, Y = y - 2 });
            Add(new AlphaBlendControl(0.25f) { X = x, Y = y + 16, Width = w, Height = h - 60 });

            var scroll = new ScrollArea(x + 2, y + 18, w - 4, h - 64, true);
            int row = 0;
            foreach (var f in _sheet.Fields)
            {
                BuildFieldRow(scroll, f, row);
                row++;
            }
            Add(scroll);

            // Add-field row
            int addY = y + h - 40;
            Add(new Label("Add:", false, 0x0481) { X = x + 4, Y = addY + 4 });
            _newFieldName = MakeBox(x + 40, addY, 110, "", 32);
            _newFieldTypeLabel = new Label(TypeName(_newFieldType), false, 0x0481, 50, 1)
            {
                X = x + 156, Y = addY + 4
            };
            Add(_newFieldTypeLabel);
            Add(new NiceButton(x + 210, addY, 36, 22, ButtonAction.Activate, "T")
                { ButtonParameter = BtnCycleType, IsSelectable = false });
            Add(new NiceButton(x + 250, addY, 36, 22, ButtonAction.Activate, "+")
                { ButtonParameter = BtnAddField, IsSelectable = false });
        }

        private void BuildFieldRow(ScrollArea scroll, SheetField f, int rowIndex)
        {
            _fieldOrder.Add(f.Id);

            int y = rowIndex * RowH;
            int colName = 0, colType = 100, colValue = 140, colDel = 252;
            int innerW = 286;

            scroll.Add(new AlphaBlendControl(0.15f) { X = 0, Y = y, Width = innerW, Height = RowH - 2 });

            var nameBox = MakeBox(colName + 2, y + 3, 96, f.Name, 32, parent: scroll);
            _fieldNameBoxes[f.Id] = nameBox;

            scroll.Add(new Label(TypeShort(f.Type), false, 0x0481, 36, 1) { X = colType, Y = y + 6 });

            switch (f.Type)
            {
                case SheetFieldType.Int:
                {
                    var box = MakeBox(colValue, y + 3, 100, f.IntValue.ToString(CultureInfo.InvariantCulture),
                                      12, parent: scroll, numeric: true);
                    _fieldValueBoxes[f.Id] = box;
                    break;
                }
                case SheetFieldType.String:
                {
                    var box = MakeBox(colValue, y + 3, 100, f.StringValue ?? string.Empty, 64, parent: scroll);
                    _fieldValueBoxes[f.Id] = box;
                    break;
                }
                case SheetFieldType.Bool:
                {
                    var cb = new Checkbox(0x00D2, 0x00D3, "", 0, 0x0481, false)
                    {
                        X = colValue + 6, Y = y + 4,
                        IsChecked = f.BoolValue
                    };
                    scroll.Add(cb);
                    _fieldBoolBoxes[f.Id] = cb;
                    break;
                }
                case SheetFieldType.Counter:
                {
                    // [-] [cur] / [max] [+]
                    scroll.Add(new NiceButton(colValue, y + 3, 18, 22, ButtonAction.Activate, "-")
                        { ButtonParameter = KindCounterDec * 1000 + rowIndex, IsSelectable = false });
                    var cur = MakeBox(colValue + 22, y + 3, 36, f.IntValue.ToString(CultureInfo.InvariantCulture),
                                      6, parent: scroll, numeric: true);
                    _fieldValueBoxes[f.Id] = cur;
                    scroll.Add(new Label("/", false, 0x0481, 10, 1) { X = colValue + 62, Y = y + 6 });
                    var max = MakeBox(colValue + 70, y + 3, 36, f.CounterMax.ToString(CultureInfo.InvariantCulture),
                                      6, parent: scroll, numeric: true);
                    _fieldMaxBoxes[f.Id] = max;
                    scroll.Add(new NiceButton(colValue + 110, y + 3, 18, 22, ButtonAction.Activate, "+")
                        { ButtonParameter = KindCounterInc * 1000 + rowIndex, IsSelectable = false });
                    break;
                }
            }

            scroll.Add(new NiceButton(colDel, y + 3, 30, 22, ButtonAction.Activate, "X")
                { ButtonParameter = KindDeleteField * 1000 + rowIndex, IsSelectable = false });
        }

        private void BuildArtPanel(int x, int y, int w, int h)
        {
            Add(new Label("Art", true, 0x0481) { X = x + 4, Y = y - 2 });
            Add(new AlphaBlendControl(0.25f) { X = x, Y = y + 16, Width = w, Height = h - 18 });

            int inner = y + 22;
            Add(new Label("Portrait Gump:", false, 0x0481) { X = x + 6, Y = inner + 4 });
            _portraitBox = MakeBox(x + 100, inner, 80,
                _sheet.PortraitGumpId.ToString(CultureInfo.InvariantCulture), 8, numeric: true);
            Add(new NiceButton(x + 188, inner, 50, 22, ButtonAction.Activate, "Apply")
                { ButtonParameter = BtnApplyArt, IsSelectable = false });

            // Preview area
            int previewY = inner + 32;
            int previewH = h - 60;
            Add(new AlphaBlendControl(0.40f) { X = x + 6, Y = previewY, Width = w - 12, Height = previewH });
            if (_sheet.PortraitGumpId != 0)
            {
                Add(new GumpPic(x + 6, previewY, _sheet.PortraitGumpId, 0));
            }
            else
            {
                Add(new Label("(no portrait)", false, 0x0481, w - 12, 1)
                {
                    X = x + 6, Y = previewY + previewH / 2 - 8
                });
            }
        }

        private void BuildInventoryPanel(int x, int y, int w, int h)
        {
            Add(new Label("Inventory", true, 0x0481) { X = x + 4, Y = y - 2 });
            Add(new AlphaBlendControl(0.25f) { X = x, Y = y + 16, Width = w, Height = h - 16 });

            var scroll = new ScrollArea(x + 2, y + 18, w - 4, h - 50, true);
            int row = 0;
            foreach (var it in _sheet.Inventory)
            {
                _itemOrder.Add(it.Id);
                int ry = row * InvRowH;
                scroll.Add(new AlphaBlendControl(0.15f) { X = 0, Y = ry, Width = w - 24, Height = InvRowH - 2 });

                var nameBox = MakeBox(2, ry + 2, 140, it.Name, 64, parent: scroll);
                _itemNameBoxes[it.Id] = nameBox;

                scroll.Add(new Label("x", false, 0x0481, 8, 1) { X = 146, Y = ry + 6 });
                var qtyBox = MakeBox(156, ry + 2, 50, it.Quantity.ToString(CultureInfo.InvariantCulture),
                                     6, parent: scroll, numeric: true);
                _itemQtyBoxes[it.Id] = qtyBox;

                scroll.Add(new NiceButton(212, ry + 2, 30, 22, ButtonAction.Activate, "X")
                    { ButtonParameter = KindDeleteItem * 1000 + row, IsSelectable = false });
                row++;
            }
            Add(scroll);

            Add(new NiceButton(x + w - 90, y + h - 28, 80, 22, ButtonAction.Activate, "+ Item")
                { ButtonParameter = BtnAddItem, IsSelectable = false });
        }

        private void BuildNotesPanel(int x, int y, int w, int h)
        {
            Add(new Label("Notes", true, 0x0481) { X = x + 4, Y = y - 2 });
            Add(new AlphaBlendControl(0.30f) { X = x, Y = y + 16, Width = w, Height = h - 16 });

            _notesBox = new StbTextBox(0xFF, 4096, w - 8, false, FontStyle.None, 0x0481)
            {
                X = x + 4,
                Y = y + 20,
                Width = w - 8,
                Height = h - 24,
                Multiline = true
            };
            _notesBox.SetText(_sheet.Notes ?? string.Empty);
            Add(_notesBox);
        }

        private void BuildFooter()
        {
            int y = Height - 32;
            Add(new Label("Background:", false, 0x0481) { X = 14, Y = y + 4 });
            _bgBox = MakeBox(98, y, 80, _sheet.BackgroundGumpId.ToString(CultureInfo.InvariantCulture),
                             8, numeric: true);
            Add(new NiceButton(184, y, 50, 22, ButtonAction.Activate, "Apply")
                { ButtonParameter = BtnApplyBg, IsSelectable = false });

            Add(new NiceButton(Width - 170, y, 70, 22, ButtonAction.Activate, "Save")
                { ButtonParameter = BtnSave, IsSelectable = false });
            Add(new NiceButton(Width - 90, y, 70, 22, ButtonAction.Activate, "Close")
                { ButtonParameter = BtnClose, IsSelectable = false });
        }

        // -------------------------------------------------------------------
        // Button dispatch
        // -------------------------------------------------------------------

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case BtnClose:
                    CommitToModel();
                    RpgCharacterSheetManager.Save(_sheet);
                    Dispose();
                    return;

                case BtnSave:
                    CommitToModel();
                    RpgCharacterSheetManager.Save(_sheet);
                    Build();
                    return;

                case BtnCycleType:
                    _newFieldType = (SheetFieldType)(((int)_newFieldType + 1) % 4);
                    if (_newFieldTypeLabel != null) _newFieldTypeLabel.Text = TypeName(_newFieldType);
                    return;

                case BtnAddField:
                {
                    CommitToModel();
                    var name = _newFieldName?.Text?.Trim();
                    if (string.IsNullOrEmpty(name)) name = TypeName(_newFieldType);
                    _sheet.AddField(_newFieldType, name);
                    Build();
                    return;
                }

                case BtnAddItem:
                    CommitToModel();
                    _sheet.AddInventoryItem();
                    Build();
                    return;

                case BtnApplyBg:
                    if (_bgBox != null && ushort.TryParse(_bgBox.Text, out var bg))
                    {
                        _sheet.BackgroundGumpId = bg;
                        CommitToModel();
                        Build();
                    }
                    return;

                case BtnApplyArt:
                    if (_portraitBox != null && ushort.TryParse(_portraitBox.Text, out var art))
                    {
                        _sheet.PortraitGumpId = art;
                        CommitToModel();
                        Build();
                    }
                    return;
            }

            // Row-encoded buttons
            int kind = buttonID / 1000;
            int idx = buttonID % 1000;
            switch (kind)
            {
                case KindDeleteField:
                    if (idx >= 0 && idx < _fieldOrder.Count)
                    {
                        CommitToModel();
                        _sheet.RemoveField(_fieldOrder[idx]);
                        Build();
                    }
                    return;

                case KindDeleteItem:
                    if (idx >= 0 && idx < _itemOrder.Count)
                    {
                        CommitToModel();
                        _sheet.RemoveInventoryItem(_itemOrder[idx]);
                        Build();
                    }
                    return;

                case KindCounterInc:
                case KindCounterDec:
                    if (idx >= 0 && idx < _fieldOrder.Count)
                    {
                        CommitToModel();
                        var f = FindField(_fieldOrder[idx]);
                        if (f != null && f.Type == SheetFieldType.Counter)
                        {
                            f.IntValue += (kind == KindCounterInc ? 1 : -1);
                            if (f.CounterMax > 0 && f.IntValue > f.CounterMax) f.IntValue = f.CounterMax;
                            if (f.IntValue < 0) f.IntValue = 0;
                            Build();
                        }
                    }
                    return;
            }
        }

        // -------------------------------------------------------------------
        // Model commit
        // -------------------------------------------------------------------

        /// <summary>
        /// Read every editable control back into the underlying
        /// <see cref="RpgCharacterSheet"/>. Called before any mutation that
        /// would Rebuild() (and therefore discard the controls).
        /// </summary>
        private void CommitToModel()
        {
            if (_nameBox != null) _sheet.CharacterName = _nameBox.Text ?? string.Empty;
            if (_notesBox != null) _sheet.Notes = _notesBox.Text ?? string.Empty;

            foreach (var f in _sheet.Fields)
            {
                if (_fieldNameBoxes.TryGetValue(f.Id, out var nb)) f.Name = nb.Text ?? string.Empty;

                switch (f.Type)
                {
                    case SheetFieldType.Int:
                        if (_fieldValueBoxes.TryGetValue(f.Id, out var vb))
                            f.IntValue = RpgCharacterSheet.ParseInt(vb.Text);
                        break;
                    case SheetFieldType.String:
                        if (_fieldValueBoxes.TryGetValue(f.Id, out var sb))
                            f.StringValue = sb.Text ?? string.Empty;
                        break;
                    case SheetFieldType.Bool:
                        if (_fieldBoolBoxes.TryGetValue(f.Id, out var cb))
                            f.BoolValue = cb.IsChecked;
                        break;
                    case SheetFieldType.Counter:
                        if (_fieldValueBoxes.TryGetValue(f.Id, out var cv))
                            f.IntValue = RpgCharacterSheet.ParseInt(cv.Text);
                        if (_fieldMaxBoxes.TryGetValue(f.Id, out var mv))
                            f.CounterMax = RpgCharacterSheet.ParseInt(mv.Text);
                        break;
                }
            }

            foreach (var it in _sheet.Inventory)
            {
                if (_itemNameBoxes.TryGetValue(it.Id, out var nb)) it.Name = nb.Text ?? string.Empty;
                if (_itemQtyBoxes.TryGetValue(it.Id, out var qb))
                    it.Quantity = RpgCharacterSheet.ParseInt(qb.Text, 1);
            }
        }

        private SheetField FindField(string id)
        {
            foreach (var f in _sheet.Fields)
                if (f.Id == id) return f;
            return null;
        }

        // -------------------------------------------------------------------
        // Control helpers
        // -------------------------------------------------------------------

        private StbTextBox MakeBox(int x, int y, int w, string text, int maxLen,
                                   bool numeric = false, Control parent = null)
        {
            var box = new StbTextBox(0xFF, maxLen, w, false, FontStyle.None, 0x0481)
            {
                X = x, Y = y, Width = w, Height = 22, NumbersOnly = numeric
            };
            box.SetText(text ?? string.Empty);

            var bg = new AlphaBlendControl(0.30f) { X = x, Y = y, Width = w, Height = 22 };
            if (parent != null)
            {
                parent.Add(bg);
                parent.Add(box);
            }
            else
            {
                Add(bg);
                Add(box);
            }
            return box;
        }

        private static string TypeName(SheetFieldType t) => t switch
        {
            SheetFieldType.Int => "Int",
            SheetFieldType.String => "Text",
            SheetFieldType.Bool => "Bool",
            SheetFieldType.Counter => "Counter",
            _ => "?"
        };

        private static string TypeShort(SheetFieldType t) => t switch
        {
            SheetFieldType.Int => "i",
            SheetFieldType.String => "s",
            SheetFieldType.Bool => "b",
            SheetFieldType.Counter => "c",
            _ => "?"
        };
    }
}
