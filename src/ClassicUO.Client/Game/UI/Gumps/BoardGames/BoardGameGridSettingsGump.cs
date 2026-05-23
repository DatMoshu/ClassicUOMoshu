// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Pop-out gump for editing the board's grid + backdrop settings.
    /// Sends ActionRequest 0x05 (ChangeGridSettings).
    /// </summary>
    internal sealed class BoardGameGridSettingsGump : Gump
    {
        private const int BtnApply = 1;
        private const int BtnClose = 2;
        private const int BtnGridToggle = 3;
        private const int BtnSnapToggle = 4;

        private readonly uint _tableSerial;
        private readonly BoardGameState _state;
        private readonly StbTextBox _cols;
        private readonly StbTextBox _rows;
        private readonly StbTextBox _tilePx;
        private readonly StbTextBox _backdrop;
        private readonly StbTextBox _colorHex;
        private bool _gridOn;

        public BoardGameGridSettingsGump(World world, uint tableSerial, BoardGameState st)
            : base(world, tableSerial, 0x5051)
        {
            _tableSerial = tableSerial;
            _state = st;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 260;
            Height = 276;
            X = 220;
            Y = 220;

            Add(new ResizePic(9270) { Width = Width, Height = Height });
            Add(new Label("Grid & Background", true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });

            int y = 38;
            Add(new Label("Cols:", false, 0x0481) { X = 14, Y = y });
            _cols = MakeBox(80, y, 60, st.GridWidth.ToString(), 3, numeric: true);
            Add(new Label("Rows:", false, 0x0481) { X = 150, Y = y });
            _rows = MakeBox(196, y, 50, st.GridHeight.ToString(), 3, numeric: true);
            y += 28;

            Add(new Label("Tile px:", false, 0x0481) { X = 14, Y = y });
            _tilePx = MakeBox(80, y, 60, st.TilePixelSize.ToString(), 3, numeric: true);
            y += 28;

            Add(new Label("Backdrop graphic (hex 0xXXXX):", false, 0x0481) { X = 14, Y = y });
            y += 16;
            _backdrop = MakeBox(14, y, 100, "0x" + st.BackdropGraphicId.ToString("X4"), 8);
            y += 28;

            Add(new Label("Grid color RRGGBBAA:", false, 0x0481) { X = 14, Y = y });
            y += 16;
            _colorHex = MakeBox(14, y, 100, st.GridColorRgba.ToString("X8"), 8);
            y += 28;

            // Snap-to-grid is a client-local preference (each viewer has their
            // own input model). Drag-drop and the properties popup honor it.
            Add(new Label("Drag drop:", false, 0x0481) { X = 14, Y = y + 4 });
            Add(new NiceButton(110, y, 124, 22, ButtonAction.Activate,
                st.SnapToGrid ? "Snap: ON" : "Snap: OFF (free)")
                { ButtonParameter = BtnSnapToggle, IsSelectable = false });
            y += 30;

            _gridOn = st.GridLinesVisible;
            Add(new NiceButton(14, y, 92, 22, ButtonAction.Activate, _gridOn ? "Grid: ON" : "Grid: OFF")
                { ButtonParameter = BtnGridToggle, IsSelectable = false });
            Add(new NiceButton(110, y, 60, 22, ButtonAction.Activate, "Apply")
                { ButtonParameter = BtnApply, IsSelectable = false });
            Add(new NiceButton(174, y, 60, 22, ButtonAction.Activate, "Close")
                { ButtonParameter = BtnClose, IsSelectable = false });
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
                case BtnApply: SendApply(); Dispose(); break;
                case BtnClose: Dispose(); break;
                case BtnGridToggle: _gridOn = !_gridOn; Dispose(); break;
                case BtnSnapToggle:
                    _state.SnapToGrid = !_state.SnapToGrid;
                    Dispose();
                    break;
            }
        }

        private void SendApply()
        {
            ushort cols  = ParseUShort(_cols.Text, 20);
            ushort rows  = ParseUShort(_rows.Text, 20);
            ushort tilePx = ParseUShort(_tilePx.Text, 32);
            ushort backdrop = ParseHex16(_backdrop.Text);
            uint color = ParseHex32(_colorHex.Text);
            byte flags = (byte)(_gridOn ? 0x01 : 0x00);

            var buf = new byte[2 + 2 + 2 + 2 + 4 + 1];
            int o = 0;
            BoardGamePiecePropertiesGump.WriteUInt16BE(buf, ref o, cols);
            BoardGamePiecePropertiesGump.WriteUInt16BE(buf, ref o, rows);
            BoardGamePiecePropertiesGump.WriteUInt16BE(buf, ref o, tilePx);
            BoardGamePiecePropertiesGump.WriteUInt16BE(buf, ref o, backdrop);
            BoardGamePiecePropertiesGump.WriteUInt32BE(buf, ref o, color);
            buf[o++] = flags;

            BoardGamePackets.Send_ActionRequest(NetClient.Socket, _tableSerial, 0x05, buf);
        }

        private static ushort ParseUShort(string s, ushort def)
        {
            return ushort.TryParse(s, out var v) ? v : def;
        }

        private static ushort ParseHex16(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            if (s.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return ushort.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : (ushort)0;
        }

        private static uint ParseHex32(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0x80808080u;
            s = s.Trim();
            if (s.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            return uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0x80808080u;
        }
    }
}
