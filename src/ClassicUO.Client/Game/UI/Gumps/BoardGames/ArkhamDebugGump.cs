// SPDX-License-Identifier: BSD-2-Clause

using System.Text;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Live debug snapshot of a board-game table (Arkham today, any framework
    /// game later). Read-only — no server writes. Shows:
    /// <list type="bullet">
    ///   <item>Table serial + GameTag</item>
    ///   <item>Layout: BoardKind, GridWidth × GridHeight × TilePixelSize, zone count</item>
    ///   <item>Seat roster (in-game/empty/pending)</item>
    ///   <item>Arkham state: scenario / phase / doom / codex / active seat / investigator stats</item>
    ///   <item>Every non-empty piece with Number / Kind / GraphicId / Position / OwnerSeat</item>
    /// </list>
    /// "Dump State" re-renders the panel and writes the same text to the chat
    /// log via the standard <see cref="Log"/> pipeline, so support sessions
    /// can paste the dump from the log file.
    /// </summary>
    internal sealed class ArkhamDebugGump : Gump
    {
        private const int BtnClose      = 99;
        private const int BtnDumpState  = 100;
        private const int BtnRefresh    = 101;

        private readonly BoardGameState _state;
        private Label _body;
        private ScrollArea _scroll;

        public ArkhamDebugGump(World world, BoardGameState state)
            : base(world, state.TableSerial, 0x4148)
        {
            _state = state;
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 480;
            Height = 560;
            X = 60;
            Y = 60;

            Add(new GumpPic(0, 0, ArkhamHudGump.HudPanelGump, 0));
            Add(new Label("Arkham — Debug", true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });

            Add(new NiceButton(12, 32, 110, 22, ButtonAction.Activate, "Dump State")
                { ButtonParameter = BtnDumpState, IsSelectable = false });
            Add(new NiceButton(130, 32, 80, 22, ButtonAction.Activate, "Refresh")
                { ButtonParameter = BtnRefresh, IsSelectable = false });
            Add(new NiceButton(220, 32, 60, 22, ButtonAction.Activate, "Close")
                { ButtonParameter = BtnClose, IsSelectable = false });

            _scroll = new ScrollArea(
                x: 8, y: 62, w: Width - 16, h: Height - 70,
                normalScrollbar: true
            );
            Add(_scroll);

            _body = new Label("", false, 0x0481, Width - 40, 1) { X = 4, Y = 0 };
            _scroll.Add(_body);

            Refresh();
        }

        /// <summary>
        /// Toggle for any framework board-game table. Bound to <c>[arkham-debug</c>
        /// command (client-side) or opened from a debug menu entry. Multiple
        /// instances per table are blocked.
        /// </summary>
        public static void OpenOrToggle(World world, BoardGameState state)
        {
            if (state == null) return;
            var existing = UIManager.GetGump<ArkhamDebugGump>(state.TableSerial);
            if (existing != null) { existing.Dispose(); return; }
            UIManager.Add(new ArkhamDebugGump(world, state));
        }

        public void Refresh()
        {
            var sb = new StringBuilder(1024);
            BuildReport(sb);
            _body.Text = sb.ToString();
        }

        private void BuildReport(StringBuilder sb)
        {
            var st = _state;
            sb.Append("─ Table ─\n");
            sb.Append("  Serial:      0x").Append(st.TableSerial.ToString("X")).Append('\n');
            sb.Append("  GameTag:     0x").Append(st.GameTag.ToString("X4"));
            if (st.IsArkhamTable) sb.Append(" (Arkham)");
            else if (st.IsRpgTable) sb.Append(" (RpgTable)");
            sb.Append('\n');
            sb.Append("  OwnSeat:     ");
            sb.Append(st.IsSpectator ? "spectator" : ("#" + st.OwnSeat));
            sb.Append('\n');

            sb.Append("\n─ Layout ─\n");
            sb.Append("  Kind:        ").Append(st.Kind).Append('\n');
            sb.Append("  GridW/H:     ").Append(st.GridWidth).Append(" × ").Append(st.GridHeight).Append('\n');
            sb.Append("  TilePixelSz: ").Append(st.TilePixelSize).Append('\n');
            sb.Append("  Backdrop:    0x").Append(st.BackdropGraphicId.ToString("X4")).Append('\n');
            sb.Append("  Zones:       ").Append(st.Zones.Count).Append('\n');
            foreach (var z in st.Zones)
            {
                sb.Append("    zone ").Append(z.ZoneId).Append(": kind ").Append(z.Kind)
                  .Append(" @ (").Append(z.OriginX).Append(',').Append(z.OriginY).Append(')')
                  .Append(" size ").Append(z.Width).Append('x').Append(z.Height)
                  .Append(" backdrop 0x").Append(z.BackdropGraphicId.ToString("X4"))
                  .Append('\n');
            }

            sb.Append("\n─ Seats ─\n");
            foreach (var seat in st.Seats)
            {
                sb.Append("  #").Append(seat.SeatIndex);
                sb.Append(seat.IsDealer ? " [GM]" : "     ");
                sb.Append(' ');
                if (seat.InGame) sb.Append("in-game");
                else if (seat.Pending) sb.Append("pending");
                else sb.Append("empty");
                sb.Append(' ').Append(seat.Name);
                sb.Append("  pts=").Append(seat.Score);
                sb.Append('\n');
            }
            sb.Append("  CurrentTurn: #").Append(st.CurrentTurnSeat).Append('\n');

            if (st.IsArkhamTable && st.Arkham != null)
            {
                var a = st.Arkham;
                sb.Append("\n─ Arkham state ─\n");
                sb.Append("  Scenario:    ").Append(a.ScenarioId).Append('\n');
                sb.Append("  Phase:       ").Append(a.PhaseName).Append(" (raw ").Append(a.Phase).Append(")\n");
                sb.Append("  ActiveSeat:  #").Append(a.ActiveSeat).Append('\n');
                sb.Append("  Doom:        ").Append(a.Doom).Append(" / ").Append(a.DoomThreshold).Append('\n');
                sb.Append("  CodexPage:   #").Append(a.CodexPage).Append("  ").Append(a.CodexTitle).Append('\n');
                sb.Append("  LastMythos:  ").Append(a.LastMythosToken)
                  .Append(' ').Append(a.LastMythosDescription).Append('\n');
                sb.Append("  LastCheck:   pool ").Append(a.LastSkillCheck.DicePool)
                  .Append("  succ ").Append(a.LastSkillCheck.Successes)
                  .Append("  fc ").Append(a.LastSkillCheck.FocusSpent)
                  .Append("  cl ").Append(a.LastSkillCheck.CluesSpent).Append('\n');
                sb.Append("  PendingEnc:  ").Append(a.PendingEncounters.Count).Append('\n');
                sb.Append("  CodexChoices:").Append(a.CodexChoices.Count).Append('\n');

                sb.Append("\n─ Investigators ─\n");
                foreach (var kv in a.Investigators)
                {
                    var inv = kv.Value;
                    sb.Append("  #").Append(inv.Seat).Append(' ').Append(inv.Name);
                    if (inv.Defeated) sb.Append(" [DOWN]");
                    sb.Append("\n    hp ").Append(inv.Health).Append('/').Append(inv.MaxHealth)
                      .Append("  sn ").Append(inv.Sanity).Append('/').Append(inv.MaxSanity)
                      .Append("  cl ").Append(inv.Clues)
                      .Append("  fc ").Append(inv.Focus)
                      .Append("  act ").Append(inv.ActionsRemaining)
                      .Append('\n');
                    sb.Append("    skills: str ").Append(inv.Strength)
                      .Append(" agi ").Append(inv.Agility)
                      .Append(" obs ").Append(inv.Observation)
                      .Append(" lor ").Append(inv.Lore)
                      .Append(" inf ").Append(inv.Influence)
                      .Append(" wil ").Append(inv.Will).Append('\n');
                    sb.Append("    loc:    (").Append(inv.LocationX).Append(',').Append(inv.LocationY)
                      .Append(")\n");
                }
            }

            sb.Append("\n─ Pieces ─\n");
            var nonEmpty = 0;
            foreach (var p in st.Pieces)
            {
                if (p.Kind == 0) continue;
                nonEmpty++;
                sb.Append("  #").Append(p.Number).Append("  kind=").Append(KindName(p.Kind))
                  .Append("  gfx=0x").Append(p.GraphicId.ToString("X4"))
                  .Append("  pos=(").Append(p.PositionX).Append(',').Append(p.PositionY).Append(')')
                  .Append("  layer=").Append(p.Layer)
                  .Append("  ownerSeat=");
                if (p.OwnerSeat == 0xFF) sb.Append("none"); else sb.Append('#').Append(p.OwnerSeat);
                sb.Append("  handArea=");
                if (p.HandArea == 0xFF) sb.Append("none"); else sb.Append(p.HandArea);
                sb.Append('\n');
            }
            sb.Append("  total active pieces: ").Append(nonEmpty)
              .Append(" / ").Append(st.Pieces.Count).Append('\n');
        }

        private static string KindName(ushort kind) => kind switch
        {
            0x10 => "Investigator",
            0x11 => "Monster",
            0x12 => "EncounterCard",
            0x13 => "CodexPage",
            0x14 => "ClueToken",
            0x15 => "DoomMarker",
            0x16 => "AnomalyMarker",
            0x17 => "Item",
            0x18 => "Spell",
            0x19 => "Condition",
            0x1A => "NeighborhoodTile",
            0x1B => "FocusToken",
            0x1C => "DealerToken",
            _ => $"0x{kind:X2}"
        };

        public override void OnButtonClick(int buttonId)
        {
            switch (buttonId)
            {
                case BtnClose:
                    Dispose();
                    return;
                case BtnRefresh:
                    Refresh();
                    return;
                case BtnDumpState:
                    Refresh();
                    var sb = new StringBuilder(1024);
                    BuildReport(sb);
                    // Single Info call (multi-line) — the renderer / log file
                    // both keep newlines. Operators can copy from the log.
                    Log.Info("[ArkhamDebug] dump for table 0x" + _state.TableSerial.ToString("X") + ":\n" + sb);
                    return;
            }
        }
    }
}
