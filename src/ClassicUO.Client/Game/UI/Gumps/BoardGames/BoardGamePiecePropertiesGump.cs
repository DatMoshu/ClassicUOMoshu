// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Pop-out gump shown when the player double-clicks an RPG piece. Edits
    /// label, HP/MP, tile X/Y, sub-tile offset, and sprite scale; exposes
    /// Rotate + Delete actions.
    /// </summary>
    internal sealed class BoardGamePiecePropertiesGump : Gump
    {
        private const int BtnApply = 1;
        private const int BtnRotate = 2;
        private const int BtnDelete = 3;
        private const int BtnClose = 4;

        private readonly uint _tableSerial;
        private readonly ushort _pieceNumber;
        private readonly BoardGameState _state;

        private readonly StbTextBox _hp;
        private readonly StbTextBox _maxHp;
        private readonly StbTextBox _mp;
        private readonly StbTextBox _maxMp;
        private readonly StbTextBox _label;
        private readonly StbTextBox _tileX;
        private readonly StbTextBox _tileY;
        private readonly StbTextBox _offX;
        private readonly StbTextBox _offY;
        private readonly StbTextBox _scale;

        public BoardGamePiecePropertiesGump(
            World world, uint tableSerial, ushort pieceNumber,
            BoardGameState state, BoardGamePieceProps existing
        )
            : base(world, tableSerial, pieceNumber)
        {
            _tableSerial = tableSerial;
            _pieceNumber = pieceNumber;
            _state = state;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 280;
            Height = 326;
            X = 200;
            Y = 200;

            // Seed tile X/Y from the current piece position.
            short curX = 0, curY = 0;
            if (state != null)
            {
                foreach (var p in state.Pieces)
                {
                    if (p.Number == pieceNumber)
                    {
                        curX = p.PositionX;
                        curY = p.PositionY;
                        break;
                    }
                }
            }

            var scaleSeed = (existing?.ScalePercent ?? BoardGamePieceProps.DefaultScalePercent);

            Add(new ResizePic(9270) { Width = Width, Height = Height });
            Add(new Label("Piece Properties", true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });

            int y = 38;
            Add(new Label("Label:", false, 0x0481) { X = 14, Y = y });
            _label = MakeBox(80, y, 180, existing?.Label ?? "", maxLen: 24);
            y += 26;

            Add(new Label("HP / Max:", false, 0x0481) { X = 14, Y = y });
            _hp    = MakeBox(80, y, 60, (existing?.Hp ?? 0).ToString(), maxLen: 6, numeric: true);
            _maxHp = MakeBox(150, y, 60, (existing?.MaxHp ?? 0).ToString(), maxLen: 6, numeric: true);
            y += 26;

            Add(new Label("MP / Max:", false, 0x0481) { X = 14, Y = y });
            _mp    = MakeBox(80, y, 60, (existing?.Mp ?? 0).ToString(), maxLen: 6, numeric: true);
            _maxMp = MakeBox(150, y, 60, (existing?.MaxMp ?? 0).ToString(), maxLen: 6, numeric: true);
            y += 28;

            Add(new Label("Tile X / Y:", false, 0x0481) { X = 14, Y = y });
            _tileX = MakeBox(80, y, 60, curX.ToString(), maxLen: 4, numeric: true);
            _tileY = MakeBox(150, y, 60, curY.ToString(), maxLen: 4, numeric: true);
            y += 26;

            Add(new Label("Offset X/Y:", false, 0x0481) { X = 14, Y = y });
            _offX  = MakeBox(80, y, 60, (existing?.OffsetX ?? 0).ToString(), maxLen: 5, numeric: false); // allow '-'
            _offY  = MakeBox(150, y, 60, (existing?.OffsetY ?? 0).ToString(), maxLen: 5, numeric: false);
            y += 26;

            Add(new Label("Scale %:", false, 0x0481) { X = 14, Y = y });
            _scale = MakeBox(80, y, 60, scaleSeed.ToString(), maxLen: 4, numeric: true);
            Add(new Label("(10-400, 100 = fit tile)", false, 0x0481, 160, 1) { X = 150, Y = y + 4 });
            y += 32;

            // Action buttons
            Add(new NiceButton(14,  y, 60, 22, ButtonAction.Activate, "Apply")  { ButtonParameter = BtnApply,  IsSelectable = false });
            Add(new NiceButton(78,  y, 60, 22, ButtonAction.Activate, "Rotate") { ButtonParameter = BtnRotate, IsSelectable = false });
            Add(new NiceButton(142, y, 60, 22, ButtonAction.Activate, "Delete") { ButtonParameter = BtnDelete, IsSelectable = false });
            Add(new NiceButton(206, y, 60, 22, ButtonAction.Activate, "Close")  { ButtonParameter = BtnClose,  IsSelectable = false });
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
                case BtnApply:    SendApply(); Dispose(); break;
                case BtnRotate:   SendRotate(); break;
                case BtnDelete:   SendDelete(); Dispose(); break;
                case BtnClose:    Dispose(); break;
            }
        }

        private void SendApply()
        {
            short hp = ParseShort(_hp.Text);
            short maxHp = ParseShort(_maxHp.Text);
            short mp = ParseShort(_mp.Text);
            short maxMp = ParseShort(_maxMp.Text);
            var label = (_label.Text ?? "").Trim();
            if (label.Length > 24) label = label.Substring(0, 24);

            short offsetX = ParseShort(_offX.Text);
            short offsetY = ParseShort(_offY.Text);
            ushort scalePercent = BoardGamePieceProps.ClampScale(ParseUShort(_scale.Text, BoardGamePieceProps.DefaultScalePercent));

            // 1) Push the props update (HP/MP/label/offset/scale).
            SendPropsUpdate(
                NetClient.Socket, _tableSerial, _pieceNumber,
                hp, maxHp, mp, maxMp, label, offsetX, offsetY, scalePercent
            );

            // 2) If tile X/Y changed, send a MovePiece so the piece relocates.
            short targetX = ParseShort(_tileX.Text);
            short targetY = ParseShort(_tileY.Text);

            var piece = FindPiece(_pieceNumber);
            if (piece != null && (piece.PositionX != targetX || piece.PositionY != targetY))
            {
                BoardGamePackets.Send_MovePiece(
                    NetClient.Socket,
                    _tableSerial,
                    _pieceNumber,
                    targetX,
                    targetY,
                    piece.Direction,
                    (byte)(piece.Layer | 0x01),
                    piece.Flipped,
                    0
                );
            }
        }

        /// <summary>
        /// Build + send a SetPieceProps (action 0x02) with the full extended
        /// payload (label + offsetX/Y + scalePercent). Shared with
        /// <see cref="BoardGameWindow"/> so drag-drop can preserve label/scale
        /// when only the offset changes.
        /// </summary>
        public static void SendPropsUpdate(
            NetClient socket, uint tableSerial, ushort pieceNumber,
            short hp, short maxHp, short mp, short maxMp, string label,
            short offsetX, short offsetY, ushort scalePercent
        )
        {
            label ??= string.Empty;
            if (label.Length > 24) label = label.Substring(0, 24);

            // pieceNum(2) hp(2) maxHp(2) mp(2) maxMp(2) label(24) ofsX(2) ofsY(2) scale(2) = 40
            var buf = new byte[2 + 2 + 2 + 2 + 2 + 24 + 2 + 2 + 2];
            int o = 0;
            WriteUInt16BE(buf, ref o, pieceNumber);
            WriteInt16BE(buf, ref o, hp);
            WriteInt16BE(buf, ref o, maxHp);
            WriteInt16BE(buf, ref o, mp);
            WriteInt16BE(buf, ref o, maxMp);
            WriteAscii(buf, ref o, label, 24);
            WriteInt16BE(buf, ref o, offsetX);
            WriteInt16BE(buf, ref o, offsetY);
            WriteUInt16BE(buf, ref o, scalePercent);

            BoardGamePackets.Send_ActionRequest(socket, tableSerial, 0x02, buf);
        }

        private void SendRotate()
        {
            var buf = new byte[2];
            int o = 0;
            WriteUInt16BE(buf, ref o, _pieceNumber);
            BoardGamePackets.Send_ActionRequest(NetClient.Socket, _tableSerial, 0x06, buf);
        }

        private void SendDelete()
        {
            var buf = new byte[2];
            int o = 0;
            WriteUInt16BE(buf, ref o, _pieceNumber);
            BoardGamePackets.Send_ActionRequest(NetClient.Socket, _tableSerial, 0x04, buf);
        }

        private BoardGamePieceView FindPiece(ushort number)
        {
            if (_state == null) return null;
            foreach (var p in _state.Pieces)
            {
                if (p.Number == number) return p;
            }
            return null;
        }

        private static short ParseShort(string s)
        {
            return short.TryParse(s, out var v) ? v : (short)0;
        }

        private static ushort ParseUShort(string s, ushort def)
        {
            return ushort.TryParse(s, out var v) ? v : def;
        }

        internal static void WriteUInt16BE(byte[] buf, ref int o, ushort v)
        {
            buf[o++] = (byte)((v >> 8) & 0xFF);
            buf[o++] = (byte)(v & 0xFF);
        }

        internal static void WriteInt16BE(byte[] buf, ref int o, short v)
        {
            buf[o++] = (byte)((v >> 8) & 0xFF);
            buf[o++] = (byte)(v & 0xFF);
        }

        internal static void WriteUInt32BE(byte[] buf, ref int o, uint v)
        {
            buf[o++] = (byte)((v >> 24) & 0xFF);
            buf[o++] = (byte)((v >> 16) & 0xFF);
            buf[o++] = (byte)((v >> 8) & 0xFF);
            buf[o++] = (byte)(v & 0xFF);
        }

        internal static void WriteAscii(byte[] buf, ref int o, string s, int fixedLen)
        {
            s ??= string.Empty;
            for (var i = 0; i < fixedLen; i++)
            {
                buf[o + i] = i < s.Length ? (byte)(s[i] & 0x7F) : (byte)0;
            }
            o += fixedLen;
        }
    }
}
