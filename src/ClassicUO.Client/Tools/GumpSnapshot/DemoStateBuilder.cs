// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.UI.Gumps.BoardGames;

namespace ClassicUO.Tools.GumpSnapshot
{
    /// <summary>
    /// Builds synthetic state objects so the snapshot harness can instantiate
    /// client-built gumps without a live server. Every method returns a
    /// realistic-but-fake instance: same shape as real data, populated with
    /// stable demo values so PNG output is deterministic across runs.
    /// </summary>
    internal static class DemoStateBuilder
    {
        public const uint DemoTableSerial = 0x40000001u;

        public static BoardGameState BuildBoardGameState()
        {
            var s = new BoardGameState
            {
                TableSerial = DemoTableSerial,
                Kind = BoardKind.SquareGrid,
                GridWidth = 20,
                GridHeight = 20,
                TilePixelSize = 32,
                BackdropGraphicId = 0xC1EA,
                GameTag = 0x5250, // 'RP'
                SeatCount = 8,
                OwnSeat = 0,
                TurnOrder = false,
                HasMoveClock = false,
                HasDisconnectGrace = true,
                DealerSeat = 0,
                CurrentTurnSeat = 0
            };

            for (byte i = 0; i < s.SeatCount; i++)
            {
                s.Seats.Add(new BoardGameSeatView
                {
                    SeatIndex = i,
                    IsDealer = i == 0,
                    InGame = i < 2,
                    Pending = false,
                    Name = i == 0 ? "GM" : i == 1 ? "Player" : string.Empty
                });
            }

            // One demo piece, with full props attached so the actions gump has
            // something to bind to.
            s.Pieces.Add(new BoardGamePieceView
            {
                Number = 1,
                GraphicId = 0x190,
                PositionX = 5,
                PositionY = 5,
                Layer = 0,
                Direction = 0,
                Flipped = false,
                OwnerSeat = 0,
                Kind = 1
            });

            s.PieceProps[1] = new BoardGamePieceProps
            {
                Hp = 24,
                MaxHp = 30,
                Mp = 8,
                MaxMp = 10,
                Label = "Goblin",
                ScalePercent = 100
            };

            // Tiny demo dice history so the dice gump has rows.
            s.AddDiceRoll(new BoardGameDiceRoll
            {
                RollerSeat = 0,
                Sides = 20,
                Results = new byte[] { 17 },
                Total = 17,
                Label = "attack"
            });
            s.AddDiceRoll(new BoardGameDiceRoll
            {
                RollerSeat = 1,
                Sides = 6,
                Results = new byte[] { 3, 5, 6 },
                Total = 14,
                Label = "damage"
            });

            return s;
        }

        public static RpgCharacterSheet BuildCharacterSheet()
        {
            var s = new RpgCharacterSheet
            {
                TableSerial = DemoTableSerial,
                PieceNumber = 1,
                CharacterName = "Aelarion the Bold",
                BackgroundGumpId = 9270,
                PortraitGumpId = 0,
                Notes =
                    "Half-elf ranger, level 5.\n" +
                    "Backstory: raised by the wolves of Yew.\n" +
                    "Owes a favor to the High Priest of Skara Brae."
            };

            s.AddField(SheetFieldType.Int, "Strength").IntValue = 14;
            s.AddField(SheetFieldType.Int, "Dexterity").IntValue = 17;
            s.AddField(SheetFieldType.String, "Class").StringValue = "Ranger";
            s.AddField(SheetFieldType.Bool, "Inspired").BoolValue = true;

            var hp = s.AddField(SheetFieldType.Counter, "HP");
            hp.IntValue = 24;
            hp.CounterMax = 30;

            var gold = s.AddField(SheetFieldType.Counter, "Gold");
            gold.IntValue = 142;
            gold.CounterMax = 0;

            s.AddInventoryItem("Longbow").Quantity = 1;
            s.AddInventoryItem("Arrows").Quantity = 24;
            s.AddInventoryItem("Healing Potion").Quantity = 3;

            return s;
        }
    }
}
