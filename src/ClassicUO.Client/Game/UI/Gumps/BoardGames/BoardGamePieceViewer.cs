// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Piece viewer / placer. Type a graphic ID (or a body ID with the toggle),
    /// preview, place at the next free board square. v1 is intentionally
    /// minimal — a fully searchable static catalog is a follow-up: see RPG
    /// Tabletop task notes.
    /// </summary>
    internal sealed class BoardGamePieceViewer : Gump
    {
        private const int BtnPlace = 1;
        private const int BtnClose = 2;
        private const int BtnToggleKind = 3;

        private readonly uint _tableSerial;
        private readonly BoardGameState _state;
        private readonly StbTextBox _gfxBox;
        private readonly StbTextBox _xBox;
        private readonly StbTextBox _yBox;
        private readonly StbTextBox _labelBox;
        private StaticPic _preview;
        private bool _isMonster;
        private NiceButton _kindBtn;

        public BoardGamePieceViewer(World world, uint tableSerial, BoardGameState state)
            : base(world, tableSerial, 0x5053)
        {
            _tableSerial = tableSerial;
            _state = state;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 300;
            Height = 240;
            X = 260;
            Y = 260;

            Add(new ResizePic(9270) { Width = Width, Height = Height });
            Add(new Label("Piece Viewer / Placer", true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });

            int y = 38;
            Add(new Label("Graphic (hex):", false, 0x0481) { X = 14, Y = y });
            _gfxBox = MakeBox(110, y, 80, "0x3589", 8);
            _kindBtn = new NiceButton(196, y, 80, 22, ButtonAction.Activate, "Static")
                { ButtonParameter = BtnToggleKind, IsSelectable = false };
            Add(_kindBtn);
            y += 28;

            Add(new Label("X / Y:", false, 0x0481) { X = 14, Y = y });
            _xBox = MakeBox(60, y, 50, "0", 3, numeric: true);
            _yBox = MakeBox(116, y, 50, "0", 3, numeric: true);

            Add(new Label("Label:", false, 0x0481) { X = 174, Y = y });
            _labelBox = MakeBox(216, y, 70, "", 20);
            y += 28;

            _preview = new StaticPic(0x3589, 0)
            {
                X = 14, Y = y, Width = 60, Height = 60, AcceptMouseInput = false
            };
            Add(_preview);

            Add(new NiceButton(90, y, 80, 22, ButtonAction.Activate, "Refresh")
                { ButtonParameter = 99, IsSelectable = false });
            Add(new NiceButton(174, y, 60, 22, ButtonAction.Activate, "Place")
                { ButtonParameter = BtnPlace, IsSelectable = false });
            Add(new NiceButton(238, y, 50, 22, ButtonAction.Activate, "Close")
                { ButtonParameter = BtnClose, IsSelectable = false });

            y += 72;
            Add(new Label(
                "Tip: graphic IDs like 0x3589 (pawn), 0x000C (dragon body),\n0x0190 (human body). Toggle Static/Monster to pick the namespace.",
                false, 0x0481, Width - 28, 1) { X = 14, Y = y });
        }

        private StbTextBox MakeBox(int x, int y, int w, string text, int maxLen, bool numeric = false)
        {
            var box = new StbTextBox(0xFF, maxLen, w, false, FontStyle.None, 0x0481)
            {
                X = x,
                Y = y,
                Width = w,
                Height = 22,
                NumbersOnly = numeric
            };
            box.SetText(text);
            Add(new AlphaBlendControl(0.30f) { X = x, Y = y, Width = w, Height = 22 });
            Add(box);
            return box;
        }

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case BtnPlace:    SendPlace(); break;
                case BtnClose:    Dispose(); break;
                case BtnToggleKind:
                    _isMonster = !_isMonster;
                    _kindBtn.TextLabel.Text = _isMonster ? "Monster" : "Static";
                    RefreshPreview();
                    break;
                case 99: RefreshPreview(); break;
            }
        }

        private void RefreshPreview()
        {
            var gfx = ParseHex16(_gfxBox.Text);
            // For the static preview we use the raw graphic; mobile bodies need
            // a different render path that's out of scope for v1 — the server
            // still accepts the placement either way.
            _preview?.Dispose();
            _preview = new StaticPic(gfx, 0)
            {
                X = 14, Y = 94, Width = 60, Height = 60, AcceptMouseInput = false
            };
            Add(_preview);
        }

        private void SendPlace()
        {
            ushort gfx = ParseHex16(_gfxBox.Text);
            short x = (short)ParseInt(_xBox.Text, 0);
            short y = (short)ParseInt(_yBox.Text, 0);
            var label = (_labelBox.Text ?? "").Trim();
            if (label.Length > 24) label = label.Substring(0, 24);

            // Payload: graphicId(2) x(2) y(2) ownerSeat(1) kind(2) label(24)
            var buf = new byte[2 + 2 + 2 + 1 + 2 + 24];
            int o = 0;
            BoardGamePiecePropertiesGump.WriteUInt16BE(buf, ref o, gfx);
            BoardGamePiecePropertiesGump.WriteInt16BE(buf, ref o, x);
            BoardGamePiecePropertiesGump.WriteInt16BE(buf, ref o, y);
            buf[o++] = (byte)(_state.OwnSeat == 0xFF ? 0xFF : _state.OwnSeat);
            BoardGamePiecePropertiesGump.WriteInt16BE(buf, ref o, (short)(_isMonster ? 2 : 1));
            BoardGamePiecePropertiesGump.WriteAscii(buf, ref o, label, 24);

            BoardGamePackets.Send_ActionRequest(NetClient.Socket, _tableSerial, 0x03, buf);
        }

        private static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out var v) ? v : def;
        }

        private static ushort ParseHex16(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            if (s.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return ushort.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : (ushort)0;
        }
    }
}
