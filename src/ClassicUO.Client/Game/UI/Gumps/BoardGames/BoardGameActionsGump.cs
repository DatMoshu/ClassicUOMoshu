// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Persistent quick-action panel for RPG tables. Operates on the currently
    /// selected piece in the owning <see cref="BoardGameWindow"/> — single-click
    /// a piece on the board to target it, then click any action button here.
    ///
    /// Every action is sent via the existing protocol (no new packets):
    /// damage/heal/scale go through <see cref="BoardGamePiecePropertiesGump.SendPropsUpdate"/>
    /// (ActionRequest 0x02 SetPieceProps); rotate is ActionRequest 0x06; delete
    /// is ActionRequest 0x04.
    /// </summary>
    internal sealed class BoardGameActionsGump : Gump
    {
        private const int BtnClose = 1;
        private const int BtnDmg1   = 10;
        private const int BtnDmg5   = 11;
        private const int BtnDmg10  = 12;
        private const int BtnHeal1  = 13;
        private const int BtnHeal5  = 14;
        private const int BtnFullHp = 15;
        private const int BtnKill   = 16;
        private const int BtnRevive = 17;
        private const int BtnScaleDown = 18;
        private const int BtnScaleUp   = 19;
        private const int BtnRotate    = 20;
        private const int BtnDelete    = 21;
        private const int BtnSheet     = 22;

        private const int ScaleStep = 25;

        private readonly BoardGameWindow _ownerWindow;
        private readonly BoardGameState _state;

        public BoardGameActionsGump(World world, BoardGameWindow owner, BoardGameState state)
            : base(world, state.TableSerial, 0)
        {
            _ownerWindow = owner;
            _state = state;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 280;
            Height = 296;
            X = 80;
            Y = 80;

            Build();
        }

        /// <summary>
        /// Toggle the actions gump for this owner — opens at default position if
        /// none exists, otherwise closes the existing one.
        /// </summary>
        public static void Toggle(World world, BoardGameWindow owner)
        {
            if (owner?.State == null) return;

            foreach (var g in UIManager.Gumps)
            {
                if (g is BoardGameActionsGump ag && ag.LocalSerial == owner.State.TableSerial)
                {
                    ag.Dispose();
                    return;
                }
            }

            UIManager.Add(new BoardGameActionsGump(world, owner, owner.State));
        }

        /// <summary>
        /// If an actions gump is open for the given table, rebuild its contents
        /// in place to reflect the latest selection / piece props. No-op if no
        /// gump is open.
        /// </summary>
        public static void RefreshIfOpen(World world, uint tableSerial)
        {
            foreach (var g in UIManager.Gumps)
            {
                if (g is BoardGameActionsGump ag && ag.LocalSerial == tableSerial)
                {
                    ag.Build();
                    return;
                }
            }
        }

        private void Build()
        {
            Clear();

            Add(new ResizePic(9270) { Width = Width, Height = Height });

            var selNum = _ownerWindow.SelectedPieceNumber;
            var piece = selNum >= 0 ? FindPiece(selNum) : null;
            _state.PieceProps.TryGetValue(selNum, out var props);

            // --- Header ---------------------------------------------------------
            string title;
            if (piece == null)
            {
                title = "Actions — (select a piece)";
            }
            else
            {
                var name = !string.IsNullOrEmpty(props?.Label) ? props.Label : $"Piece #{selNum}";
                title = $"Actions — {name}";
            }
            Add(new Label(title, true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });

            // Stats row (if a piece is selected)
            if (piece != null && props != null)
            {
                Add(new Label(
                    $"HP {props.Hp}/{props.MaxHp}   MP {props.Mp}/{props.MaxMp}   Scale {props.ScalePercent}%",
                    false, 0x0481, Width - 16, 1)
                {
                    X = 12, Y = 36
                });
            }
            else
            {
                Add(new Label(
                    "Click a piece on the board to target it.",
                    false, 0x0481, Width - 16, 1)
                {
                    X = 12, Y = 36
                });
            }

            // --- Button grid ----------------------------------------------------
            // 3 columns × 22 px height × 28 px row pitch
            int y = 60;
            const int col0 = 10, col1 = 100, col2 = 190, btnW = 80;

            Add(new NiceButton(col0, y, btnW, 22, ButtonAction.Activate, "Dmg 1")  { ButtonParameter = BtnDmg1,  IsSelectable = false });
            Add(new NiceButton(col1, y, btnW, 22, ButtonAction.Activate, "Dmg 5")  { ButtonParameter = BtnDmg5,  IsSelectable = false });
            Add(new NiceButton(col2, y, btnW, 22, ButtonAction.Activate, "Dmg 10") { ButtonParameter = BtnDmg10, IsSelectable = false });
            y += 28;

            Add(new NiceButton(col0, y, btnW, 22, ButtonAction.Activate, "Heal 1") { ButtonParameter = BtnHeal1,  IsSelectable = false });
            Add(new NiceButton(col1, y, btnW, 22, ButtonAction.Activate, "Heal 5") { ButtonParameter = BtnHeal5,  IsSelectable = false });
            Add(new NiceButton(col2, y, btnW, 22, ButtonAction.Activate, "Full HP") { ButtonParameter = BtnFullHp, IsSelectable = false });
            y += 28;

            Add(new NiceButton(col0, y, btnW, 22, ButtonAction.Activate, "Kill")   { ButtonParameter = BtnKill,   IsSelectable = false });
            Add(new NiceButton(col1, y, btnW, 22, ButtonAction.Activate, "Revive") { ButtonParameter = BtnRevive, IsSelectable = false });
            y += 28;

            Add(new NiceButton(col0, y, btnW, 22, ButtonAction.Activate, $"Scale -{ScaleStep}%") { ButtonParameter = BtnScaleDown, IsSelectable = false });
            Add(new NiceButton(col1, y, btnW, 22, ButtonAction.Activate, $"Scale +{ScaleStep}%") { ButtonParameter = BtnScaleUp,   IsSelectable = false });
            y += 28;

            Add(new NiceButton(col0, y, btnW, 22, ButtonAction.Activate, "Rotate") { ButtonParameter = BtnRotate, IsSelectable = false });
            Add(new NiceButton(col1, y, btnW, 22, ButtonAction.Activate, "Delete") { ButtonParameter = BtnDelete, IsSelectable = false });
            Add(new NiceButton(col2, y, btnW, 22, ButtonAction.Activate, "Sheet")  { ButtonParameter = BtnSheet,  IsSelectable = false });
            y += 28;

            // Close pinned to bottom-right
            Add(new NiceButton(Width - 70, Height - 32, 60, 22, ButtonAction.Activate, "Close")
                { ButtonParameter = BtnClose, IsSelectable = false });
        }

        public override void OnButtonClick(int buttonID)
        {
            if (buttonID == BtnClose)
            {
                Dispose();
                return;
            }

            var selNum = _ownerWindow.SelectedPieceNumber;
            if (selNum < 0) return;

            if (buttonID == BtnSheet)
            {
                RpgCharacterSheetGump.Open(World, _state.TableSerial, selNum);
                return;
            }

            var piece = FindPiece(selNum);
            if (piece == null) return;

            _state.PieceProps.TryGetValue(selNum, out var existing);
            var hp     = existing?.Hp    ?? 0;
            var maxHp  = existing?.MaxHp ?? 0;
            var mp     = existing?.Mp    ?? 0;
            var maxMp  = existing?.MaxMp ?? 0;
            var label  = existing?.Label ?? string.Empty;
            var offX   = existing?.OffsetX ?? (short)0;
            var offY   = existing?.OffsetY ?? (short)0;
            var scale  = existing?.ScalePercent ?? BoardGamePieceProps.DefaultScalePercent;

            switch (buttonID)
            {
                case BtnDmg1:  hp = ClampHp((short)(hp - 1),  maxHp); break;
                case BtnDmg5:  hp = ClampHp((short)(hp - 5),  maxHp); break;
                case BtnDmg10: hp = ClampHp((short)(hp - 10), maxHp); break;
                case BtnHeal1: hp = ClampHp((short)(hp + 1),  maxHp); break;
                case BtnHeal5: hp = ClampHp((short)(hp + 5),  maxHp); break;
                case BtnFullHp: hp = maxHp; break;
                case BtnKill:   hp = 0; break;
                case BtnRevive: hp = maxHp > 0 ? maxHp : (short)1; break;
                case BtnScaleDown: scale = BoardGamePieceProps.ClampScale((ushort)System.Math.Max(0, scale - ScaleStep)); break;
                case BtnScaleUp:   scale = BoardGamePieceProps.ClampScale((ushort)(scale + ScaleStep)); break;
                case BtnRotate:
                {
                    SendBarePieceAction(0x06);
                    return;
                }
                case BtnDelete:
                {
                    SendBarePieceAction(0x04);
                    return;
                }
                default:
                    return;
            }

            BoardGamePiecePropertiesGump.SendPropsUpdate(
                NetClient.Socket, _state.TableSerial, (ushort)selNum,
                hp, maxHp, mp, maxMp, label, offX, offY, scale
            );
        }

        private void SendBarePieceAction(short actionId)
        {
            var selNum = _ownerWindow.SelectedPieceNumber;
            if (selNum < 0) return;

            var buf = new byte[2];
            int o = 0;
            BoardGamePiecePropertiesGump.WriteUInt16BE(buf, ref o, (ushort)selNum);
            BoardGamePackets.Send_ActionRequest(NetClient.Socket, _state.TableSerial, actionId, buf);
        }

        private static short ClampHp(short value, short maxHp)
        {
            if (value < 0) return 0;
            if (maxHp > 0 && value > maxHp) return maxHp;
            return value;
        }

        private BoardGamePieceView FindPiece(int number)
        {
            if (number < 0 || _state == null) return null;
            foreach (var p in _state.Pieces)
            {
                if (p.Number == number) return p;
            }
            return null;
        }
    }
}
