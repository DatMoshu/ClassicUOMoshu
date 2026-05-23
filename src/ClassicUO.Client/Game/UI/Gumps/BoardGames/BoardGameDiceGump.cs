// SPDX-License-Identifier: BSD-2-Clause

using System.Text;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Pop-out dice roller for any framework game that opts in.
    /// d4/d6/d8/d10/d12/d20 + custom NdM. Shows recent roll history broadcast
    /// by the server (sub-cmd 0x07 DiceRoll).
    /// </summary>
    internal sealed class BoardGameDiceGump : Gump
    {
        private const int BtnClose = 99;
        private const int BtnRollCustom = 100;

        private readonly uint _tableSerial;
        private readonly BoardGameState _state;
        private readonly StbTextBox _countBox;
        private readonly StbTextBox _sidesBox;
        private readonly StbTextBox _labelBox;
        private readonly Label _history;

        public BoardGameDiceGump(World world, uint tableSerial, BoardGameState state)
            : base(world, tableSerial, 0x5052)
        {
            _tableSerial = tableSerial;
            _state = state;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 280;
            Height = 280;
            X = 240;
            Y = 240;

            Add(new ResizePic(9270) { Width = Width, Height = Height });
            Add(new Label("Dice", true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });

            // Quick-roll buttons
            int[] sides = { 4, 6, 8, 10, 12, 20 };
            int x = 14;
            int y = 38;
            for (var i = 0; i < sides.Length; i++)
            {
                var s = sides[i];
                Add(new NiceButton(x, y, 38, 22, ButtonAction.Activate, $"d{s}")
                    { ButtonParameter = s, IsSelectable = false });
                x += 42;
            }

            y += 30;
            Add(new Label("NdM:", false, 0x0481) { X = 14, Y = y });
            _countBox = MakeBox(60, y, 40, "1", 2, numeric: true);
            Add(new Label("d", false, 0x0481) { X = 104, Y = y });
            _sidesBox = MakeBox(118, y, 40, "20", 3, numeric: true);
            Add(new Label("label:", false, 0x0481) { X = 164, Y = y });
            _labelBox = MakeBox(200, y, 60, "", 20);
            y += 28;
            Add(new NiceButton(14, y, 60, 22, ButtonAction.Activate, "Roll")
                { ButtonParameter = BtnRollCustom, IsSelectable = false });
            Add(new NiceButton(78, y, 60, 22, ButtonAction.Activate, "Close")
                { ButtonParameter = BtnClose, IsSelectable = false });

            y += 32;
            Add(new Label("History:", false, 0x0481) { X = 14, Y = y });
            y += 16;
            _history = new Label("", false, 0x0481, Width - 28, 1)
            {
                X = 14,
                Y = y
            };
            Add(_history);

            RefreshHistory();
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
            if (buttonID == BtnClose)
            {
                Dispose();
                return;
            }

            if (buttonID == BtnRollCustom)
            {
                int count = ParseInt(_countBox.Text, 1);
                int sides = ParseInt(_sidesBox.Text, 20);
                SendRoll(sides, count, _labelBox.Text ?? "");
                return;
            }

            // Quick buttons: buttonID is the side count
            if (buttonID >= 2 && buttonID <= 100)
            {
                SendRoll(buttonID, 1, "");
            }
        }

        public void RefreshHistory()
        {
            if (_state.DiceHistory.Count == 0)
            {
                _history.Text = "(no rolls yet)";
                return;
            }

            var sb = new StringBuilder();
            int i = 0;
            foreach (var r in _state.DiceHistory)
            {
                if (i++ >= 6) break;
                var lbl = string.IsNullOrEmpty(r.Label) ? "" : $" [{r.Label}]";
                sb.Append($"#{r.RollerSeat} {r.Results.Length}d{r.Sides}={r.Total}{lbl}\n");
            }
            _history.Text = sb.ToString();
        }

        private void SendRoll(int sides, int count, string label)
        {
            if (sides < 2) sides = 2;
            if (sides > 100) sides = 100;
            if (count < 1) count = 1;
            if (count > 32) count = 32;
            if (label == null) label = "";
            if (label.Length > 24) label = label.Substring(0, 24);

            // Payload: sides(2) count(1) label(24)
            var buf = new byte[2 + 1 + 24];
            int o = 0;
            BoardGamePiecePropertiesGump.WriteInt16BE(buf, ref o, (short)sides);
            buf[o++] = (byte)count;
            BoardGamePiecePropertiesGump.WriteAscii(buf, ref o, label, 24);

            BoardGamePackets.Send_ActionRequest(NetClient.Socket, _tableSerial, 0x01, buf);
        }

        private static int ParseInt(string s, int def)
        {
            return int.TryParse(s, out var v) ? v : def;
        }
    }
}
