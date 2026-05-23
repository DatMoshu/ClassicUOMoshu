// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Client-side cache of a single board-game table. Populated by the 0xE0
    /// packet handler; read by <see cref="BoardGameWindow"/>. One instance per
    /// open board.
    /// </summary>
    internal sealed class BoardGameState
    {
        public uint TableSerial;
        public byte ProtocolVersion = 1;

        // Layout (from JoinGame; mutable via 0x09 GridSettings)
        public BoardKind Kind;
        public ushort GridWidth;
        public ushort GridHeight;
        public ushort TilePixelSize;
        public ushort BackdropGraphicId;
        public ushort GameTag;
        public byte SeatCount;
        public byte OwnSeat = 0xFF; // 0xFF = spectator
        public bool TurnOrder;
        public bool HasMoveClock;
        public bool HasDisconnectGrace;

        // Grid overlay settings (RPG table, sub-cmd 0x09)
        public uint GridColorRgba = 0x80808080u;
        public bool GridLinesVisible = true;

        // Client-local: when true, drag/drop and properties-popup snap moves to
        // the nearest tile (no sub-tile offset sent). When false, drops keep the
        // exact pixel offset within the destination tile. Not synced to the
        // server — each viewer chooses their own input model.
        public bool SnapToGrid = true;

        // GeneralInfo
        public byte CurrentTurnSeat;
        public byte DealerSeat;
        public uint MoveClockMs;

        // Pieces (index = piece number)
        public readonly List<BoardGamePieceView> Pieces = new(64);

        // Players (index = seat)
        public readonly List<BoardGameSeatView> Seats = new(8);

        // RPG-only: HP/MP/Label keyed by piece Number (sub-cmd 0x08)
        public readonly Dictionary<int, BoardGamePieceProps> PieceProps = new();

        // Recent dice rolls (sub-cmd 0x07); newest first; capped.
        public const int DiceHistoryMax = 12;
        public readonly LinkedList<BoardGameDiceRoll> DiceHistory = new();

        // RPG-only: initiative tracker (sub-cmd 0x0A). Ordered piece numbers
        // (max 64) authored by the GM; InitiativeIndex points at the active row.
        public readonly List<ushort> InitiativeOrder = new(8);
        public byte InitiativeIndex;

        // RPG-only: dice macro book (sub-cmd 0x0B). GM-authored named expressions.
        // Key = display name (case-insensitive); value = "[count]d<sides>[(+|-)mod]".
        public readonly Dictionary<string, string> DiceMacros =
            new(System.StringComparer.OrdinalIgnoreCase);

        public bool IsSpectator => OwnSeat == 0xFF;
        public bool IsOwnTurn => !IsSpectator && CurrentTurnSeat == OwnSeat;
        public bool IsRpgTable => GameTag == 0x5250; // 'RP'
        public bool IsArkhamTable => GameTag == 0x4148; // 'AH'

        /// <summary>
        /// Multi-zone layout descriptor tail (parsed when JoinGame flags &amp; 0x08).
        /// Empty for legacy single-grid games (Chess, v1 RpgTable). Pieces with
        /// <see cref="BoardGamePieceView.HandArea"/> matching a zone's
        /// <see cref="BoardGameZoneView.ZoneId"/> render inside that zone.
        /// </summary>
        public readonly List<BoardGameZoneView> Zones = new(8);

        /// <summary>Per-table Arkham state (null for non-Arkham games).</summary>
        public ArkhamState Arkham;

        public void AddDiceRoll(BoardGameDiceRoll roll)
        {
            DiceHistory.AddFirst(roll);
            while (DiceHistory.Count > DiceHistoryMax)
            {
                DiceHistory.RemoveLast();
            }
        }
    }

    internal sealed class BoardGamePieceView
    {
        public ushort Number;
        public ushort GraphicId;
        public short PositionX;
        public short PositionY;
        public byte Layer;
        public byte Direction;
        public bool Flipped;
        public byte OwnerSeat;
        public byte HandArea;
        public ushort Kind;

        public bool IsOnBoard => PositionX >= 0 && PositionY >= 0 && Kind != 0;
    }

    internal sealed class BoardGameSeatView
    {
        public uint Serial;
        public byte SeatIndex;
        public bool IsDealer;
        public bool InGame;
        public bool Pending;
        public int Score;
        public string Name = string.Empty;
    }

    internal sealed class BoardGamePieceProps
    {
        public const ushort DefaultScalePercent = 100;
        public const ushort MinScalePercent = 10;
        public const ushort MaxScalePercent = 400;

        public short Hp;
        public short MaxHp;
        public short Mp;
        public short MaxMp;
        public string Label = string.Empty;

        // Sub-tile render offset (pixels relative to the piece's tile top-left).
        public short OffsetX;
        public short OffsetY;

        // Sprite scale percent of one tile. 100 = exactly fits the tile.
        public ushort ScalePercent = DefaultScalePercent;

        // 16-bit status-condition bitmask (poisoned, prone, blinded, …). Bit
        // assignments are game-defined; client overlays small condition icons
        // per set bit. Default 0 = no conditions.
        public ushort Conditions;

        // Bitmask of seats this piece is hidden from. The GM renders a "hidden"
        // badge on pieces whose mask is non-zero. The actual visibility filter
        // runs server-side: hidden recipients receive an empty-slot payload
        // instead of the real piece state, so non-GM clients usually never see
        // this field set.
        public ushort HiddenFromSeats;

        public bool HasAnyBar => MaxHp > 0 || MaxMp > 0;
        public bool HasAnyCondition => Conditions != 0;
        public bool IsHiddenSomewhere => HiddenFromSeats != 0;

        public static ushort ClampScale(ushort scale)
        {
            if (scale < MinScalePercent) return MinScalePercent;
            if (scale > MaxScalePercent) return MaxScalePercent;
            return scale;
        }
    }

    internal sealed class BoardGameDiceRoll
    {
        public byte RollerSeat;
        public ushort Sides;
        public byte[] Results = System.Array.Empty<byte>();
        public short Total;
        public string Label = string.Empty;
    }

    internal enum BoardKind : byte
    {
        Free = 0,
        SquareGrid = 1,
        HexGrid = 2
    }

    /// <summary>
    /// Client-side mirror of a server-emitted <c>BoardGameZone</c>. Populated
    /// from the multi-zone tail of the JoinGame packet (flags &amp; 0x08).
    /// </summary>
    internal sealed class BoardGameZoneView
    {
        public ushort ZoneId;
        public byte Kind;
        public ushort OriginX;
        public ushort OriginY;
        public ushort Width;
        public ushort Height;
        public ushort BackdropGraphicId;
        public ushort Cols;
        public ushort Rows;
        public byte Flags;
    }

    /// <summary>
    /// Arkham Horror-specific runtime state. Built up by 0xE0 sub-cmds
    /// 0x30..0x36 (ArkhamPackets server-side). The HUD renders from this.
    /// </summary>
    internal sealed class ArkhamState
    {
        public string ScenarioId = string.Empty;
        public byte Phase;
        public byte ActiveSeat;
        public ushort Doom;
        public ushort DoomThreshold;
        public ushort CodexPage;

        public string CodexTitle = string.Empty;
        public string CodexBody = string.Empty;
        public readonly List<(string Label, ushort NextPage)> CodexChoices = new(4);

        public string LastMythosToken = string.Empty;
        public string LastMythosDescription = string.Empty;

        public readonly Dictionary<int, ArkhamInvestigatorView> Investigators = new();
        public readonly List<ArkhamCardView> PendingEncounters = new(4);
        public ArkhamSkillCheckResult LastSkillCheck;

        public string PhaseName => Phase switch
        {
            0 => "Setup",
            1 => "Action",
            2 => "Monster",
            3 => "Encounter",
            4 => "Mythos",
            5 => "Resolved",
            _ => $"#{Phase}"
        };
    }

    internal sealed class ArkhamInvestigatorView
    {
        public int Seat;
        public string Name = string.Empty;
        public byte Health, MaxHealth;
        public byte Sanity, MaxSanity;
        public byte Strength, Agility, Observation, Lore, Influence, Will;
        public byte Clues, Focus, ActionsRemaining;
        public bool Defeated;
        public short LocationX = -1;
        public short LocationY = -1;
    }

    internal sealed class ArkhamCardView
    {
        public string CardId = string.Empty;
        public string Title = string.Empty;
        public string Body = string.Empty;
        public string SkillCheck = string.Empty;
        public ushort ZoneId;
    }

    internal struct ArkhamSkillCheckResult
    {
        public ushort CheckId;
        public byte DicePool;
        public byte Successes;
        public byte[] Rolls;
        public byte CluesSpent;
        public byte FocusSpent;
    }
}
