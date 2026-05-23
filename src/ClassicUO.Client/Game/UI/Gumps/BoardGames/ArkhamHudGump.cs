// SPDX-License-Identifier: BSD-2-Clause

using System.Text;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Heads-up display for an Arkham Horror 3e table. Rendered as a side panel
    /// next to the main <see cref="BoardGameWindow"/>; shows scenario / phase /
    /// doom / codex / per-investigator state and exposes the inbound action
    /// helpers (Advance Phase, Perform Action, Declare Skill Check, Codex
    /// Choice).
    /// <para>
    /// Opens automatically when the server sends the first
    /// <c>0xE0/0x30 ScenarioInfo</c> packet. Closes when the table closes.
    /// </para>
    /// </summary>
    internal sealed class ArkhamHudGump : Gump
    {
        /// <summary>Gump ID of the themed Arkham HUD backdrop (injected via uoasset MCP).</summary>
        public const ushort HudPanelGump   = 0xC202;
        public const ushort BoardBackdropGump = 0xC200;
        public const ushort DoomTrackGump  = 0xC201;

        /// <summary>
        /// Max codex choice buttons to render inline in the codex strip. Page
        /// 1+ in night-stars has 2 choices — fits cleanly between y=148..192.
        /// Anything beyond gets a "+N more" hint; players read the codex body
        /// text and pick from there.
        /// </summary>
        private const int MaxVisibleChoices = 2;

        private const int BtnAdvancePhase = 100;
        private const int BtnActMove      = 110;
        private const int BtnActInvestigate = 111;
        private const int BtnActRest      = 112;
        private const int BtnActFocus     = 113;
        private const int BtnActAttack    = 114;
        private const int BtnSkillCheck   = 120;
        private const int BtnDebug        = 121;
        private const int BtnChoiceBase   = 200; // BtnChoiceBase + index

        private readonly BoardGameWindow _window;
        private readonly BoardGameState _state;
        private Label _scenarioLabel;
        private Label _phaseLabel;
        private Label _doomLabel;
        private Label _codexTitle;
        private Label _codexBody;
        private Label _lastMythos;
        private Label _lastCheck;
        private Label _invLabel;
        private ScrollArea _investigatorsScroll;

        public ArkhamHudGump(World world, BoardGameWindow window)
            : base(world, window.State.TableSerial, 0x4148)
        {
            _window = window;
            _state = window.State;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 360;
            Height = 520;
            X = 920;
            Y = 80;

            // Themed HUD backdrop — single-image gump injected at HudPanelGump
            // via uoasset MCP. We use GumpPic (not ResizePic) because ResizePic
            // expects a 9-slice atlas of consecutive gump IDs (corners + edges
            // + center), which would mis-render adjacent Arkham gumps as frame
            // tiles. The PNG source is authored at 360x520 to match Width/Height.
            Add(new GumpPic(0, 0, HudPanelGump, 0));
            Add(new Label("Arkham Horror — HUD", true, 0x0481, Width - 100, 1) { X = 12, Y = 10 });

            // Debug button — opens / toggles the live state dump.
            Add(new NiceButton(Width - 64, 8, 50, 20, ButtonAction.Activate, "Dbg")
                { ButtonParameter = BtnDebug, IsSelectable = false });

            // ── Layout plan (panel is 360 × 520) ──────────────────────────────
            //   y 30..96   fixed: scenario / phase / doom (3 labels)
            //   y 96..200  codex (label + title + body + dynamic choice buttons)
            //   y 200..264 fixed: last skill check + last mythos
            //   y 264..318 action button rows (2 rows × 26 px)
            //   y 318..510 ScrollArea: investigators list
            // ──────────────────────────────────────────────────────────────────

            var y = 36;
            _scenarioLabel = MakeLine(y); y += 18;
            _phaseLabel    = MakeLine(y); y += 18;
            _doomLabel     = MakeLine(y); y += 22;

            Add(new Label("Codex:", true, 0x035, Width - 16, 1) { X = 12, Y = y }); y += 18;
            _codexTitle = MakeLine(y); y += 18;
            _codexBody  = new Label("", false, 0x0481, Width - 28, 1) { X = 12, Y = y };
            Add(_codexBody); y += 70;
            // Codex choices append below _codexBody from Refresh(). Reserve a
            // dynamic-but-bounded strip; over-long choices are clipped by the
            // following fixed-section labels.

            y = 204; // hard-snap below codex body
            Add(new Label("Last skill check:", true, 0x035, Width - 16, 1) { X = 12, Y = y }); y += 18;
            _lastCheck = MakeLine(y); y += 22;

            Add(new Label("Last Mythos:", true, 0x035, Width - 16, 1) { X = 12, Y = y }); y += 18;
            _lastMythos = MakeLine(y); y += 24;

            // Action buttons — fixed-position, BEFORE the investigators panel
            // so the variable-height list can't push them off-screen.
            y = 268;
            Add(new NiceButton(12, y, 100, 22, ButtonAction.Activate, "Advance Phase")
                { ButtonParameter = BtnAdvancePhase, IsSelectable = false });
            Add(new NiceButton(120, y, 60, 22, ButtonAction.Activate, "Move")
                { ButtonParameter = BtnActMove, IsSelectable = false });
            Add(new NiceButton(184, y, 80, 22, ButtonAction.Activate, "Investigate")
                { ButtonParameter = BtnActInvestigate, IsSelectable = false });
            Add(new NiceButton(268, y, 60, 22, ButtonAction.Activate, "Attack")
                { ButtonParameter = BtnActAttack, IsSelectable = false });
            y += 26;
            Add(new NiceButton(12, y, 60, 22, ButtonAction.Activate, "Rest")
                { ButtonParameter = BtnActRest, IsSelectable = false });
            Add(new NiceButton(78, y, 60, 22, ButtonAction.Activate, "Focus")
                { ButtonParameter = BtnActFocus, IsSelectable = false });
            Add(new NiceButton(146, y, 100, 22, ButtonAction.Activate, "Skill Check")
                { ButtonParameter = BtnSkillCheck, IsSelectable = false });

            // ── Investigators list — variable-height, scrollable ──────────────
            // The investigator block grows with seat count (and any per-seat
            // condition lines we add later). ScrollArea keeps the rest of the
            // HUD pinned and lets the list scroll if it overflows.
            y = 322;
            Add(new Label("Investigators:", true, 0x035, Width - 16, 1) { X = 12, Y = y }); y += 18;

            const int scrollH = 520 - 322 - 18 - 8; // ~172 px
            _investigatorsScroll = new ScrollArea(
                x: 8, y: y, w: Width - 16, h: scrollH,
                normalScrollbar: true
            );
            Add(_investigatorsScroll);

            _invLabel = new Label("", false, 0x0481, Width - 40, 1) { X = 4, Y = 0 };
            _investigatorsScroll.Add(_invLabel);

            Refresh();
        }

        private Label MakeLine(int y)
        {
            var lbl = new Label("", false, 0x0481, Width - 28, 1) { X = 12, Y = y };
            Add(lbl);
            return lbl;
        }

        public static void RefreshIfOpenOrOpen(World world, BoardGameWindow window)
        {
            var existing = UIManager.GetGump<ArkhamHudGump>(window.State.TableSerial);
            if (existing != null) { existing.Refresh(); return; }

            // Only auto-open for Arkham tables and only after a ScenarioInfo packet
            // (Arkham state is populated).
            if (!window.State.IsArkhamTable || window.State.Arkham == null) return;
            UIManager.Add(new ArkhamHudGump(world, window));
        }

        public void Refresh()
        {
            var a = _state.Arkham;
            if (a == null) return;

            // Surface any newly-drawn encounter cards as a modal. The modal
            // pops one card per call and decrements the pending list, so
            // subsequent Refreshes naturally walk the queue.
            ArkhamEncounterCardGump.OpenNextPending(World, _state);

            _scenarioLabel.Text = string.IsNullOrEmpty(a.ScenarioId) ? "(no scenario loaded)" : $"Scenario: {a.ScenarioId}";
            _phaseLabel.Text    = $"Phase: {a.PhaseName}   Active seat: #{a.ActiveSeat}";
            _doomLabel.Text     = $"Doom: {a.Doom} / {a.DoomThreshold}";

            _codexTitle.Text = string.IsNullOrEmpty(a.CodexTitle) ? "(no codex page)" : $"#{a.CodexPage}  {a.CodexTitle}";
            _codexBody.Text  = TruncateForLine(a.CodexBody, 320);

            // Replace previous choice buttons with current page's choices.
            for (var i = 0; i < 8; i++)
            {
                var btn = FindButtonByParam(BtnChoiceBase + i);
                btn?.Dispose();
            }

            // Cap visible choice buttons to MaxVisibleChoices so they fit
            // inside the codex strip (y ~ 130..200). Extras get a "+N more"
            // hint button that opens the codex modal (TODO) — for v1 we just
            // truncate; the codex page text mentions every choice anyway.
            var choices = a.CodexChoices;
            var visible = choices.Count < MaxVisibleChoices ? choices.Count : MaxVisibleChoices;
            var cy = _codexBody.Y + 16;
            for (var i = 0; i < visible; i++)
            {
                var c = choices[i];
                var btn = new NiceButton(12, cy, Width - 28, 20, ButtonAction.Activate, $"➤ {c.Label}")
                {
                    ButtonParameter = BtnChoiceBase + i, IsSelectable = false
                };
                Add(btn);
                cy += 22;
            }
            if (choices.Count > visible)
            {
                var more = new Label($"(+{choices.Count - visible} more — see codex text)",
                    false, 0x0035, Width - 28, 1) { X = 14, Y = cy };
                Add(more);
            }

            _lastCheck.Text = a.LastSkillCheck.DicePool == 0
                ? "(none)"
                : $"pool {a.LastSkillCheck.DicePool}  successes {a.LastSkillCheck.Successes}  clues {a.LastSkillCheck.CluesSpent}";
            _lastMythos.Text = string.IsNullOrEmpty(a.LastMythosToken) ? "(none)" : $"{a.LastMythosToken} — {a.LastMythosDescription}";

            var sb = new StringBuilder();
            foreach (var kv in a.Investigators)
            {
                var inv = kv.Value;
                sb.Append('#').Append(inv.Seat).Append(' ').Append(inv.Name)
                    .Append("  hp ").Append(inv.Health).Append('/').Append(inv.MaxHealth)
                    .Append("  sn ").Append(inv.Sanity).Append('/').Append(inv.MaxSanity)
                    .Append("  cl ").Append(inv.Clues)
                    .Append("  fc ").Append(inv.Focus)
                    .Append("  act ").Append(inv.ActionsRemaining)
                    .Append(inv.Defeated ? "  [DOWN]" : "")
                    .Append('\n');
            }
            _invLabel.Text = sb.ToString();
        }

        private Control FindButtonByParam(int param)
        {
            foreach (var c in Children)
            {
                if (c is NiceButton nb && nb.ButtonParameter == param) return c;
            }
            return null;
        }

        private static string TruncateForLine(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= maxChars ? s : s.Substring(0, maxChars - 1) + "…";
        }

        public override void OnButtonClick(int buttonId)
        {
            var socket = NetClient.Socket;
            if (socket == null) return;
            var serial = _state.TableSerial;

            switch (buttonId)
            {
                case BtnAdvancePhase:
                    BoardGamePackets.Send_ArkhamAdvancePhase(socket, serial);
                    return;
                case BtnActMove:
                    BoardGamePackets.Send_ArkhamPerformAction(socket, serial, 1, 0, 0);
                    return;
                case BtnActInvestigate:
                    BoardGamePackets.Send_ArkhamPerformAction(socket, serial, 2, 0, 0);
                    return;
                case BtnActRest:
                    BoardGamePackets.Send_ArkhamPerformAction(socket, serial, 4, 0, 0);
                    return;
                case BtnActFocus:
                    BoardGamePackets.Send_ArkhamPerformAction(socket, serial, 5, 0, 0);
                    return;
                case BtnActAttack:
                    BoardGamePackets.Send_ArkhamPerformAction(socket, serial, 6, 0, 0);
                    return;
                case BtnSkillCheck:
                    // Open the focus/clue-spend modal instead of firing a
                    // blank check. The modal sends the actual packet when
                    // the player clicks Roll.
                    ArkhamSkillCheckGump.Open(World, _state, checkId: 0, label: "Manual check");
                    return;
                case BtnDebug:
                    ArkhamDebugGump.OpenOrToggle(World, _state);
                    return;
            }

            if (buttonId >= BtnChoiceBase && buttonId < BtnChoiceBase + 8)
            {
                var idx = buttonId - BtnChoiceBase;
                BoardGamePackets.Send_ArkhamInvestigatorChoice(socket, serial, _state.Arkham?.CodexPage ?? (ushort)0, (byte)idx);
            }
        }
    }
}
