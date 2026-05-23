// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Modal for declaring an Arkham skill check. Lets the player choose how
    /// many focus tokens and clues to spend, then fires
    /// <c>Send_ArkhamDeclareSkillCheck</c>. The result is delivered server-side
    /// via <c>0xE0/0x34 SkillCheckResult</c> and renders in the HUD's
    /// "Last skill check" row — this modal just collects spend choices.
    /// </summary>
    internal sealed class ArkhamSkillCheckGump : Gump
    {
        private const int BtnClose      = 99;
        private const int BtnRoll       = 100;
        private const int BtnFocusPlus  = 110;
        private const int BtnFocusMinus = 111;
        private const int BtnCluePlus   = 112;
        private const int BtnClueMinus  = 113;

        private readonly BoardGameState _state;
        private readonly ushort _checkId;
        private readonly string _label;
        private byte _focus;
        private byte _clues;
        private Label _focusValue;
        private Label _clueValue;
        private Label _availLabel;

        public ArkhamSkillCheckGump(World world, BoardGameState state, ushort checkId, string label)
            : base(world, state.TableSerial, 0x4148)
        {
            _state = state;
            _checkId = checkId;
            _label = label ?? "";

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 280;
            Height = 220;
            X = 540;
            Y = 220;

            Add(new GumpPic(0, 0, ArkhamHudGump.HudPanelGump, 0));
            Add(new Label("Skill Check", true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });
            Add(new Label(_label.Length > 0 ? _label : "(generic check)", false, 0x0481,
                Width - 24, 1) { X = 12, Y = 32 });

            _availLabel = new Label(BuildAvailLabel(), false, 0x035, Width - 24, 1)
            {
                X = 12, Y = 50
            };
            Add(_availLabel);

            // Focus row
            var y = 76;
            Add(new Label("Focus to spend:", false, 0x0481, 140, 1) { X = 12, Y = y + 4 });
            Add(new NiceButton(160, y, 24, 22, ButtonAction.Activate, "-")
                { ButtonParameter = BtnFocusMinus, IsSelectable = false });
            _focusValue = new Label("0", true, 0x0481, 30, 1) { X = 196, Y = y + 4 };
            Add(_focusValue);
            Add(new NiceButton(228, y, 24, 22, ButtonAction.Activate, "+")
                { ButtonParameter = BtnFocusPlus, IsSelectable = false });

            // Clue row
            y += 28;
            Add(new Label("Clues to spend:", false, 0x0481, 140, 1) { X = 12, Y = y + 4 });
            Add(new NiceButton(160, y, 24, 22, ButtonAction.Activate, "-")
                { ButtonParameter = BtnClueMinus, IsSelectable = false });
            _clueValue = new Label("0", true, 0x0481, 30, 1) { X = 196, Y = y + 4 };
            Add(_clueValue);
            Add(new NiceButton(228, y, 24, 22, ButtonAction.Activate, "+")
                { ButtonParameter = BtnCluePlus, IsSelectable = false });

            // Roll + Close
            y += 40;
            Add(new NiceButton(40, y, 80, 24, ButtonAction.Activate, "Roll")
                { ButtonParameter = BtnRoll, IsSelectable = false });
            Add(new NiceButton(160, y, 80, 24, ButtonAction.Activate, "Cancel")
                { ButtonParameter = BtnClose, IsSelectable = false });
        }

        /// <summary>
        /// Open the modal for the active investigator, or refresh if already open.
        /// </summary>
        public static void Open(World world, BoardGameState state, ushort checkId, string label)
        {
            var existing = UIManager.GetGump<ArkhamSkillCheckGump>(state.TableSerial);
            existing?.Dispose();
            UIManager.Add(new ArkhamSkillCheckGump(world, state, checkId, label));
        }

        private (byte focus, byte clues) AvailableTokens()
        {
            var a = _state.Arkham;
            if (a == null) return (0, 0);
            if (!a.Investigators.TryGetValue(a.ActiveSeat, out var inv)) return (0, 0);
            return (inv.Focus, inv.Clues);
        }

        private string BuildAvailLabel()
        {
            var (f, c) = AvailableTokens();
            return $"Available: focus {f}, clues {c}";
        }

        public override void OnButtonClick(int buttonId)
        {
            var (availFocus, availClues) = AvailableTokens();

            switch (buttonId)
            {
                case BtnClose:
                    Dispose();
                    return;
                case BtnFocusPlus:
                    if (_focus < availFocus) _focus++;
                    _focusValue.Text = _focus.ToString();
                    return;
                case BtnFocusMinus:
                    if (_focus > 0) _focus--;
                    _focusValue.Text = _focus.ToString();
                    return;
                case BtnCluePlus:
                    if (_clues < availClues) _clues++;
                    _clueValue.Text = _clues.ToString();
                    return;
                case BtnClueMinus:
                    if (_clues > 0) _clues--;
                    _clueValue.Text = _clues.ToString();
                    return;
                case BtnRoll:
                    BoardGamePackets.Send_ArkhamDeclareSkillCheck(
                        NetClient.Socket, _state.TableSerial,
                        _checkId, _focus, _clues
                    );
                    Dispose();
                    return;
            }
        }
    }
}
