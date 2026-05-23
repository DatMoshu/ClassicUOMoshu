// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Modal that surfaces a single encounter card drawn during the Encounter
    /// phase. Shows the card title, body text, and the required skill-check
    /// spec (if any). "Resolve" opens <see cref="ArkhamSkillCheckGump"/>
    /// pre-filled; "Defer" closes without effect (re-shown next Refresh).
    /// </summary>
    internal sealed class ArkhamEncounterCardGump : Gump
    {
        private const int BtnClose   = 99;
        private const int BtnResolve = 100;

        private readonly BoardGameState _state;
        private readonly ArkhamCardView _card;

        public ArkhamEncounterCardGump(World world, BoardGameState state, ArkhamCardView card)
            : base(world, state.TableSerial, 0x4148)
        {
            _state = state;
            _card = card;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;
            Width = 320;
            Height = 260;
            X = 500;
            Y = 200;

            Add(new GumpPic(0, 0, ArkhamHudGump.HudPanelGump, 0));
            Add(new Label("Encounter", true, 0x0481, Width - 16, 1) { X = 12, Y = 10 });

            // Title
            Add(new Label(card.Title, true, 0x035, Width - 24, 1) { X = 12, Y = 36 });

            // Body — single Label can multi-line; we trust the text length
            // from the server fits this 220-px tall window.
            Add(new Label(card.Body, false, 0x0481, Width - 28, 1) { X = 12, Y = 60 });

            // Skill check spec
            var spec = string.IsNullOrEmpty(card.SkillCheck) ? "(no skill check)" : $"Skill check: {card.SkillCheck}";
            Add(new Label(spec, false, 0x0035, Width - 28, 1) { X = 12, Y = 174 });

            // Buttons
            var y = 200;
            Add(new NiceButton(40, y, 100, 24, ButtonAction.Activate, "Resolve")
                { ButtonParameter = BtnResolve, IsSelectable = false });
            Add(new NiceButton(180, y, 100, 24, ButtonAction.Activate, "Defer")
                { ButtonParameter = BtnClose, IsSelectable = false });
        }

        /// <summary>
        /// Pop the topmost pending encounter for the active seat (if any).
        /// Called by <see cref="ArkhamHudGump.OnArkhamStateChanged"/>.
        /// </summary>
        public static void OpenNextPending(World world, BoardGameState state)
        {
            if (state?.Arkham == null) return;
            if (state.Arkham.PendingEncounters.Count == 0) return;

            // Don't stack multiple modals.
            var existing = UIManager.GetGump<ArkhamEncounterCardGump>(state.TableSerial);
            if (existing != null) return;

            var card = state.Arkham.PendingEncounters[0];
            state.Arkham.PendingEncounters.RemoveAt(0);
            UIManager.Add(new ArkhamEncounterCardGump(world, state, card));
        }

        public override void OnButtonClick(int buttonId)
        {
            switch (buttonId)
            {
                case BtnClose:
                    Dispose();
                    return;
                case BtnResolve:
                    // Hand off to the skill-check modal. checkId=0 (generic);
                    // the server uses the active investigator's Will skill by
                    // default. A future pass can wire the card's SkillCheck
                    // spec into a per-skill server route.
                    var label = string.IsNullOrEmpty(_card.SkillCheck)
                        ? _card.Title
                        : $"{_card.Title} ({_card.SkillCheck})";
                    Dispose();
                    ArkhamSkillCheckGump.Open(World, _state, checkId: 0, label);
                    return;
            }
        }
    }
}
