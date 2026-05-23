// SPDX-License-Identifier: BSD-2-Clause

using System.IO;
using ClassicUO.Game.Managers;
using ClassicUO.IO;
using ClassicUO.Network;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Client-side handler for the UOWW Board Game Framework packet
    /// <c>0xE0</c>. Mirror of <c>custom/Scripts/BoardGames/Net/BoardGamePackets.cs</c>.
    /// Parses inbound sub-commands into <see cref="BoardGameState"/>, drives the
    /// <see cref="BoardGameWindow"/> lifecycle, and provides Send helpers for
    /// the outbound <c>MovePiece</c> / <c>LeaveGame</c> / <c>EndTurn</c> packets.
    /// </summary>
    internal static class BoardGamePackets
    {
        public const byte PacketId = 0xE0;
        public const byte ProtocolVersion = 0x01;

        // Outbound (C→S)
        public const byte SubLeaveGame = 0x06;
        public const byte SubMovePiece = 0x10;
        public const byte SubActionRequest = 0x11;
        public const byte SubEndTurn = 0x12;
        public const byte SubOpenSeat = 0x13;
        public const byte SubAssignDealer = 0x14;
        public const byte SubChangeOption = 0x15;

        // Inbound (S→C)
        public const byte SubPlayersInfo = 0x02;
        public const byte SubPieceInfo = 0x03;
        public const byte SubPiecesInfo = 0x04;
        public const byte SubGeneralInfo = 0x05;
        public const byte SubOutcomeAnnounce = 0x06;
        public const byte SubDiceRoll = 0x07;
        public const byte SubPieceProps = 0x08;
        public const byte SubGridSettings = 0x09;
        public const byte SubInitiativeOrder = 0x0A;
        public const byte SubDiceMacros = 0x0B;
        public const byte SubJoinGame = 0x19;
        public const byte SubRelieve = 0x1A;
        public const byte SubToastMessage = 0x1B;

        // RpgTable action IDs (sent inside 0x11 ActionRequest body).
        public const ushort ActRpgRollDice            = 0x01;
        public const ushort ActRpgSetPieceProps       = 0x02;
        public const ushort ActRpgAddPiece            = 0x03;  // GM-only
        public const ushort ActRpgRemovePiece         = 0x04;  // GM-only
        public const ushort ActRpgChangeGrid          = 0x05;  // GM-only
        public const ushort ActRpgRotatePiece         = 0x06;
        public const ushort ActRpgSetInitiative       = 0x07;  // GM-only
        public const ushort ActRpgNextInitiative      = 0x08;  // GM-only
        public const ushort ActRpgAddDiceMacro        = 0x09;  // GM-only
        public const ushort ActRpgRemoveDiceMacro     = 0x0A;  // GM-only
        public const ushort ActRpgRollDiceMacro       = 0x0B;
        public const ushort ActRpgSetPieceVisibility  = 0x0C;  // GM-only

        // Wire-format widths (must match the server's RpgTable / BoardGamePackets constants).
        public const int RpgLabelMaxBytes = 24;
        public const int DiceMacroNameBytes = 24;
        public const int DiceMacroExprBytes = 48;

        // Arkham-specific outbound sub-commands (0x30..0x36)
        public const byte SubArkhamScenarioInfo     = 0x30;
        public const byte SubArkhamCodexPage        = 0x31;
        public const byte SubArkhamEncounterCard    = 0x32;
        public const byte SubArkhamMythosDraw       = 0x33;
        public const byte SubArkhamSkillCheckResult = 0x34;
        public const byte SubArkhamInvestigatorState = 0x35;
        public const byte SubArkhamMonsterState     = 0x36;

        // Inbound ActionRequest action ids (sent inside 0x11 ActionRequest body).
        public const ushort ActArkhamPerformAction      = 0x2001;
        public const ushort ActArkhamDeclareSkillCheck  = 0x2002;
        public const ushort ActArkhamResolveMythosToken = 0x2003;
        public const ushort ActArkhamAdvancePhase       = 0x2004;
        public const ushort ActArkhamInvestigatorChoice = 0x2005;
        public const ushort ActArkhamGmOverride         = 0x2006;

        /// <summary>
        /// Top-level packet 0xE0 dispatcher. Hook from
        /// <c>PacketHandlers.Load</c> via <c>Handler.Add(0xE0, BoardGamePackets.Handle);</c>.
        /// </summary>
        public static void Handle(World world, ref StackDataReader p)
        {
            // Header consumed by caller: byte 0 (packet id) + bytes 1-2 (length).
            var serial = p.ReadUInt32BE();
            var version = p.ReadUInt8();

            if (version != ProtocolVersion)
            {
                Log.Warn($"BoardGamePackets: protocol version 0x{version:X2} unsupported (want 0x{ProtocolVersion:X2}).");
                return;
            }

            var sub = p.ReadUInt8();

            switch (sub)
            {
                case SubJoinGame:
                {
                    HandleJoinGame(world, serial, ref p);
                    break;
                }
                case SubRelieve:
                {
                    HandleRelieve(serial);
                    break;
                }
                case SubPlayersInfo:
                {
                    HandlePlayersInfo(serial, ref p);
                    break;
                }
                case SubPieceInfo:
                {
                    HandlePieceInfo(serial, ref p);
                    break;
                }
                case SubPiecesInfo:
                {
                    HandlePiecesInfo(serial, ref p);
                    break;
                }
                case SubGeneralInfo:
                {
                    HandleGeneralInfo(serial, ref p);
                    break;
                }
                case SubOutcomeAnnounce:
                {
                    HandleOutcome(world, serial, ref p);
                    break;
                }
                case SubToastMessage:
                {
                    // payload reserved; v0 ignores
                    break;
                }
                case SubDiceRoll:
                {
                    HandleDiceRoll(serial, ref p);
                    break;
                }
                case SubPieceProps:
                {
                    HandlePieceProps(serial, ref p);
                    break;
                }
                case SubGridSettings:
                {
                    HandleGridSettings(serial, ref p);
                    break;
                }
                case SubInitiativeOrder:
                {
                    HandleInitiativeOrder(serial, ref p);
                    break;
                }
                case SubDiceMacros:
                {
                    HandleDiceMacros(serial, ref p);
                    break;
                }
                case SubArkhamScenarioInfo:
                {
                    HandleArkhamScenarioInfo(serial, ref p);
                    break;
                }
                case SubArkhamCodexPage:
                {
                    HandleArkhamCodexPage(serial, ref p);
                    break;
                }
                case SubArkhamEncounterCard:
                {
                    HandleArkhamEncounterCard(serial, ref p);
                    break;
                }
                case SubArkhamMythosDraw:
                {
                    HandleArkhamMythosDraw(serial, ref p);
                    break;
                }
                case SubArkhamSkillCheckResult:
                {
                    HandleArkhamSkillCheckResult(serial, ref p);
                    break;
                }
                case SubArkhamInvestigatorState:
                {
                    HandleArkhamInvestigatorState(serial, ref p);
                    break;
                }
                case SubArkhamMonsterState:
                {
                    // v1: piece state is already mirrored via SubPieceInfo; we
                    // ignore the dedicated monster delta until the HUD wants it.
                    break;
                }
                default:
                {
                    Log.Warn($"BoardGamePackets: unknown sub-cmd 0x{sub:X2}.");
                    break;
                }
            }
        }

        // --- Inbound handlers ---------------------------------------------------

        private static void HandleJoinGame(World world, uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.GetOrOpen(world, serial);
            var st = window.State;

            st.TableSerial = serial;
            st.SeatCount = p.ReadUInt8();
            st.OwnSeat = p.ReadUInt8();

            st.Kind = (BoardKind)p.ReadUInt8();
            st.GridWidth = p.ReadUInt16BE();
            st.GridHeight = p.ReadUInt16BE();
            st.TilePixelSize = p.ReadUInt16BE();
            st.BackdropGraphicId = p.ReadUInt16BE();
            st.GameTag = p.ReadUInt16BE();

            var flags = p.ReadUInt8();
            st.TurnOrder = (flags & 0x01) != 0;
            st.HasMoveClock = (flags & 0x02) != 0;
            st.HasDisconnectGrace = (flags & 0x04) != 0;

            // Multi-zone tail (flag bit 0x08). Older servers omit it entirely.
            st.Zones.Clear();
            if ((flags & 0x08) != 0 && p.Remaining > 0)
            {
                var zoneCount = p.ReadUInt8();
                for (var i = 0; i < zoneCount; i++)
                {
                    var z = new BoardGameZoneView
                    {
                        ZoneId = p.ReadUInt16BE(),
                        Kind = p.ReadUInt8(),
                        OriginX = p.ReadUInt16BE(),
                        OriginY = p.ReadUInt16BE(),
                        Width = p.ReadUInt16BE(),
                        Height = p.ReadUInt16BE(),
                        BackdropGraphicId = p.ReadUInt16BE(),
                        Cols = p.ReadUInt16BE(),
                        Rows = p.ReadUInt16BE(),
                        Flags = p.ReadUInt8()
                    };
                    st.Zones.Add(z);
                }
            }

            // Bootstrap Arkham state if needed.
            if (st.IsArkhamTable && st.Arkham == null)
            {
                st.Arkham = new ArkhamState();
            }

            window.ApplyLayout();
        }

        // --- Arkham sub-command handlers --------------------------------------

        private static void HandleArkhamScenarioInfo(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;
            var st = window.State;
            st.Arkham ??= new ArkhamState();

            st.Arkham.ScenarioId    = p.ReadASCII(24).TrimEnd('\0');
            st.Arkham.Phase         = p.ReadUInt8();
            st.Arkham.ActiveSeat    = p.ReadUInt8();
            st.Arkham.Doom          = p.ReadUInt16BE();
            st.Arkham.DoomThreshold = p.ReadUInt16BE();
            st.Arkham.CodexPage     = p.ReadUInt16BE();
            window.OnArkhamStateChanged();
        }

        private static void HandleArkhamCodexPage(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;
            var st = window.State;
            st.Arkham ??= new ArkhamState();

            st.Arkham.CodexPage = p.ReadUInt16BE();
            st.Arkham.CodexTitle = p.ReadASCII(48).TrimEnd('\0');
            var bodyLen = p.ReadUInt16BE();
            st.Arkham.CodexBody = bodyLen > 0 ? p.ReadUTF8(bodyLen) : string.Empty;

            st.Arkham.CodexChoices.Clear();
            var choiceCount = p.ReadUInt8();
            for (var i = 0; i < choiceCount; i++)
            {
                var label = p.ReadASCII(64).TrimEnd('\0');
                var next  = p.ReadUInt16BE();
                st.Arkham.CodexChoices.Add((label, next));
            }
            window.OnArkhamStateChanged();
        }

        private static void HandleArkhamEncounterCard(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;
            var st = window.State;
            st.Arkham ??= new ArkhamState();

            var card = new ArkhamCardView
            {
                CardId = p.ReadASCII(24).TrimEnd('\0'),
                Title  = p.ReadASCII(48).TrimEnd('\0')
            };
            var bodyLen = p.ReadUInt16BE();
            card.Body = bodyLen > 0 ? p.ReadUTF8(bodyLen) : string.Empty;
            card.SkillCheck = p.ReadASCII(24).TrimEnd('\0');
            card.ZoneId = p.ReadUInt16BE();
            st.Arkham.PendingEncounters.Add(card);
            window.OnArkhamStateChanged();
        }

        private static void HandleArkhamMythosDraw(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;
            var st = window.State;
            st.Arkham ??= new ArkhamState();

            var tokenType = p.ReadUInt8();
            st.Arkham.LastMythosToken = tokenType switch
            {
                0 => "Blank",
                1 => "Monster",
                2 => "Doom",
                3 => "Clue",
                4 => "Reckoning",
                _ => $"#{tokenType}"
            };
            var descLen = p.ReadUInt16BE();
            st.Arkham.LastMythosDescription = descLen > 0 ? p.ReadUTF8(descLen) : string.Empty;
            window.OnArkhamStateChanged();
        }

        private static void HandleArkhamSkillCheckResult(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;
            var st = window.State;
            st.Arkham ??= new ArkhamState();

            var dicePool = p.ReadUInt8();
            var successes = p.ReadUInt8();
            var rolls = new byte[16];
            for (var i = 0; i < 16; i++) rolls[i] = p.ReadUInt8();
            var cluesSpent = p.ReadUInt8();
            var focusSpent = p.ReadUInt8();
            var checkId = p.ReadUInt16BE();

            st.Arkham.LastSkillCheck = new ArkhamSkillCheckResult
            {
                CheckId = checkId,
                DicePool = dicePool,
                Successes = successes,
                Rolls = rolls,
                CluesSpent = cluesSpent,
                FocusSpent = focusSpent
            };
            window.OnArkhamStateChanged();
        }

        private static void HandleArkhamInvestigatorState(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;
            var st = window.State;
            st.Arkham ??= new ArkhamState();

            var seat = p.ReadUInt8();
            var inv = new ArkhamInvestigatorView
            {
                Seat = seat,
                Health      = p.ReadUInt8(),
                MaxHealth   = p.ReadUInt8(),
                Sanity      = p.ReadUInt8(),
                MaxSanity   = p.ReadUInt8(),
                Strength    = p.ReadUInt8(),
                Agility     = p.ReadUInt8(),
                Observation = p.ReadUInt8(),
                Lore        = p.ReadUInt8(),
                Influence   = p.ReadUInt8(),
                Will        = p.ReadUInt8(),
                Clues       = p.ReadUInt8(),
                Focus       = p.ReadUInt8(),
                ActionsRemaining = p.ReadUInt8(),
                Defeated    = p.ReadUInt8() != 0
            };
            p.ReadUInt8(); // reserved
            inv.LocationX = (short)p.ReadUInt16BE();
            inv.LocationY = (short)p.ReadUInt16BE();
            inv.Name = p.ReadASCII(48).TrimEnd('\0');

            st.Arkham.Investigators[seat] = inv;
            window.OnArkhamStateChanged();
        }

        // --- Outbound (Arkham action helpers) ---------------------------------

        public static void Send_ArkhamAdvancePhase(NetClient socket, uint tableSerial)
        {
            Send_ActionRequest(socket, tableSerial, (short)ActArkhamAdvancePhase, System.Array.Empty<byte>());
        }

        public static void Send_ArkhamPerformAction(NetClient socket, uint tableSerial, byte actionType, short tx, short ty)
        {
            var payload = new byte[1 + 2 + 2];
            payload[0] = actionType;
            payload[1] = (byte)(tx >> 8); payload[2] = (byte)(tx & 0xFF);
            payload[3] = (byte)(ty >> 8); payload[4] = (byte)(ty & 0xFF);
            Send_ActionRequest(socket, tableSerial, (short)ActArkhamPerformAction, payload);
        }

        public static void Send_ArkhamDeclareSkillCheck(NetClient socket, uint tableSerial, ushort checkId, byte focusSpent, byte cluesSpent)
        {
            var payload = new byte[2 + 1 + 1];
            payload[0] = (byte)(checkId >> 8); payload[1] = (byte)(checkId & 0xFF);
            payload[2] = focusSpent;
            payload[3] = cluesSpent;
            Send_ActionRequest(socket, tableSerial, (short)ActArkhamDeclareSkillCheck, payload);
        }

        public static void Send_ArkhamInvestigatorChoice(NetClient socket, uint tableSerial, ushort choiceId, byte optionIndex)
        {
            var payload = new byte[2 + 1];
            payload[0] = (byte)(choiceId >> 8); payload[1] = (byte)(choiceId & 0xFF);
            payload[2] = optionIndex;
            Send_ActionRequest(socket, tableSerial, (short)ActArkhamInvestigatorChoice, payload);
        }

        private static void HandleRelieve(uint serial)
        {
            // Close the main board window first; then sweep every Arkham-side
            // gump keyed to the same table serial (HUD, encounter modal,
            // skill-check modal). These live in UIManager, not the registry,
            // so the registry's Close() wouldn't touch them — they would
            // orphan on screen after a Forfeit.
            BoardGameWindowRegistry.Close(serial);
            ClassicUO.Game.Managers.UIManager.GetGump<ArkhamHudGump>(serial)?.Dispose();
            ClassicUO.Game.Managers.UIManager.GetGump<ArkhamEncounterCardGump>(serial)?.Dispose();
            ClassicUO.Game.Managers.UIManager.GetGump<ArkhamSkillCheckGump>(serial)?.Dispose();
            ClassicUO.Game.Managers.UIManager.GetGump<ArkhamDebugGump>(serial)?.Dispose();
        }

        private static void HandlePlayersInfo(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var st = window.State;
            var count = p.ReadUInt8();

            st.Seats.Clear();
            for (var i = 0; i < count; i++)
            {
                var seat = new BoardGameSeatView
                {
                    Serial = p.ReadUInt32BE(),
                    SeatIndex = p.ReadUInt8(),
                    IsDealer = p.ReadUInt8() == 1,
                    InGame = p.ReadBool(),
                    Pending = p.ReadBool(),
                    Score = p.ReadInt32BE(),
                    Name = p.ReadASCII(30).TrimEnd('\0')
                };
                st.Seats.Add(seat);
            }

            window.ApplyPlayers();
        }

        private static void HandlePieceInfo(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var view = ReadPiecePayload(ref p);
            var st = window.State;

            // Upsert by Number
            for (var i = 0; i < st.Pieces.Count; i++)
            {
                if (st.Pieces[i].Number == view.Number)
                {
                    st.Pieces[i] = view;
                    window.ApplyPieces();
                    return;
                }
            }

            st.Pieces.Add(view);
            window.ApplyPieces();
        }

        private static void HandlePiecesInfo(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var st = window.State;
            st.Pieces.Clear();
            var count = p.ReadUInt16BE();
            for (var i = 0; i < count; i++)
            {
                st.Pieces.Add(ReadPiecePayload(ref p));
            }
            window.ApplyPieces();
        }

        private static void HandleGeneralInfo(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var st = window.State;
            st.CurrentTurnSeat = p.ReadUInt8();
            st.DealerSeat = p.ReadUInt8();
            st.MoveClockMs = p.ReadUInt32BE();
            p.ReadUInt8(); // reserved

            window.ApplyGeneral();
        }

        private static void HandleOutcome(World world, uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var kind = p.ReadUInt8();
            var winningSeat = (sbyte)p.ReadUInt8();
            var cliloc = p.ReadInt32BE();

            window.AnnounceOutcome(kind, winningSeat, cliloc);
        }

        private static void HandleDiceRoll(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var roll = new BoardGameDiceRoll
            {
                RollerSeat = p.ReadUInt8(),
                Sides = p.ReadUInt16BE()
            };
            var n = p.ReadUInt8();
            roll.Results = new byte[n];
            for (var i = 0; i < n; i++) roll.Results[i] = p.ReadUInt8();
            roll.Total = p.ReadInt16BE();
            roll.Label = p.ReadASCII(24).TrimEnd('\0');

            window.State.AddDiceRoll(roll);
            window.OnDiceRollReceived(roll);
        }

        private static void HandlePieceProps(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var num = p.ReadUInt16BE();
            var props = new BoardGamePieceProps
            {
                Hp = p.ReadInt16BE(),
                MaxHp = p.ReadInt16BE(),
                Mp = p.ReadInt16BE(),
                MaxMp = p.ReadInt16BE(),
                Label = p.ReadASCII(24).TrimEnd('\0')
            };

            // Optional tail #1: offsetX(2) + offsetY(2) + scalePercent(2). Legacy
            // server packets stop after the label; treat missing as defaults.
            if (p.Remaining >= 6)
            {
                props.OffsetX = p.ReadInt16BE();
                props.OffsetY = p.ReadInt16BE();
                props.ScalePercent = BoardGamePieceProps.ClampScale(p.ReadUInt16BE());
            }

            // Optional tail #2 (2026-05-15+): conditions(2) + hiddenFromSeats(2).
            // Carry forward existing values if the server's an older build.
            if (p.Remaining >= 4)
            {
                props.Conditions = p.ReadUInt16BE();
                props.HiddenFromSeats = p.ReadUInt16BE();
            }
            else if (window.State.PieceProps.TryGetValue(num, out var existing))
            {
                props.Conditions = existing.Conditions;
                props.HiddenFromSeats = existing.HiddenFromSeats;
            }

            window.State.PieceProps[num] = props;
            window.ApplyPieces();
        }

        private static void HandleInitiativeOrder(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var st = window.State;
            var count = p.ReadUInt8();
            var current = p.ReadUInt8();

            st.InitiativeOrder.Clear();
            for (var i = 0; i < count; i++)
            {
                st.InitiativeOrder.Add(p.ReadUInt16BE());
            }
            st.InitiativeIndex = current;

            window.ApplyGeneral();
        }

        private static void HandleDiceMacros(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var st = window.State;
            var count = p.ReadUInt8();

            st.DiceMacros.Clear();
            for (var i = 0; i < count; i++)
            {
                var name = p.ReadASCII(DiceMacroNameBytes).TrimEnd('\0').Trim();
                var expr = p.ReadASCII(DiceMacroExprBytes).TrimEnd('\0').Trim();
                if (name.Length > 0) st.DiceMacros[name] = expr;
            }

            window.ApplyGeneral();
        }

        private static void HandleGridSettings(uint serial, ref StackDataReader p)
        {
            var window = BoardGameWindowRegistry.Get(serial);
            if (window == null) return;

            var st = window.State;
            st.GridWidth = p.ReadUInt16BE();
            st.GridHeight = p.ReadUInt16BE();
            st.TilePixelSize = p.ReadUInt16BE();
            st.BackdropGraphicId = p.ReadUInt16BE();
            st.GridColorRgba = p.ReadUInt32BE();
            var flags = p.ReadUInt8();
            st.GridLinesVisible = (flags & 0x01) != 0;

            window.ApplyLayout();
        }

        // --- Outbound game-action helpers (sub-cmd 0x11 ActionRequest) ----------

        /// <summary>
        /// Send an ActionRequest with a caller-built payload. The payload is the
        /// game-specific bytes after the 2-byte actionId.
        /// </summary>
        public static void Send_ActionRequest(NetClient socket, uint tableSerial, short actionId, byte[] payload)
        {
            payload ??= System.Array.Empty<byte>();
            // Header(9) + actionId(2) + payload
            var length = 9 + 2 + payload.Length;
            var writer = new StackDataWriter(length);
            writer.WriteUInt8(PacketId);
            writer.WriteZero(2);
            writer.WriteUInt32BE(tableSerial);
            writer.WriteUInt8(ProtocolVersion);
            writer.WriteUInt8(SubActionRequest);

            writer.WriteInt16BE(actionId);
            writer.Write(payload);

            writer.Seek(1, SeekOrigin.Begin);
            writer.WriteUInt16BE((ushort)writer.BytesWritten);

            socket.Send(writer.BufferWritten);
            writer.Dispose();
        }

        private static BoardGamePieceView ReadPiecePayload(ref StackDataReader p)
        {
            return new BoardGamePieceView
            {
                Number = p.ReadUInt16BE(),
                GraphicId = p.ReadUInt16BE(),
                PositionY = p.ReadInt16BE(),
                PositionX = p.ReadInt16BE(),
                Layer = p.ReadUInt8(),
                Direction = p.ReadUInt8(),
                Flipped = p.ReadBool(),
                OwnerSeat = p.ReadUInt8(),
                HandArea = p.ReadUInt8(),
                Kind = p.ReadUInt16BE()
            };
        }

        // --- Outbound (C→S) -----------------------------------------------------

        public static void Send_MovePiece(
            NetClient socket, uint tableSerial, ushort pieceNumber,
            short destX, short destY, byte direction, byte layer, bool flipped, uint requestId
        )
        {
            // Header (9) + payload (14) = 23
            var writer = new StackDataWriter(23);
            writer.WriteUInt8(PacketId);
            writer.WriteZero(2);
            writer.WriteUInt32BE(tableSerial);
            writer.WriteUInt8(ProtocolVersion);
            writer.WriteUInt8(SubMovePiece);

            writer.WriteUInt16BE(pieceNumber);
            writer.WriteUInt8(direction);
            writer.WriteUInt8(layer);
            writer.WriteBool(flipped);
            writer.WriteInt16BE(destY);
            writer.WriteInt16BE(destX);
            writer.WriteUInt32BE(requestId);

            writer.Seek(1, SeekOrigin.Begin);
            writer.WriteUInt16BE((ushort)writer.BytesWritten);

            socket.Send(writer.BufferWritten);
            writer.Dispose();
        }

        public static void Send_LeaveGame(NetClient socket, uint tableSerial) =>
            SendNoPayload(socket, tableSerial, SubLeaveGame);

        public static void Send_EndTurn(NetClient socket, uint tableSerial) =>
            SendNoPayload(socket, tableSerial, SubEndTurn);

        /// <summary>
        /// GM pass-ownership. Sub-cmd 0x14 with a single-byte target-seat payload.
        /// Server validates the sender is the current dealer + the target is a
        /// seated player; rejects silently otherwise.
        /// </summary>
        public static void Send_AssignDealer(NetClient socket, uint tableSerial, byte targetSeat)
        {
            var writer = new StackDataWriter(10);
            writer.WriteUInt8(PacketId);
            writer.WriteUInt16BE(10);
            writer.WriteUInt32BE(tableSerial);
            writer.WriteUInt8(ProtocolVersion);
            writer.WriteUInt8(SubAssignDealer);
            writer.WriteUInt8(targetSeat);

            socket.Send(writer.BufferWritten);
            writer.Dispose();
        }

        // --- RpgTable action helpers ------------------------------------------

        public static void Send_RpgSetInitiative(NetClient socket, uint tableSerial, ushort[] pieceNums)
        {
            pieceNums ??= System.Array.Empty<ushort>();
            var count = pieceNums.Length;
            if (count > 64) count = 64;
            var payload = new byte[1 + count * 2];
            payload[0] = (byte)count;
            for (var i = 0; i < count; i++)
            {
                payload[1 + i * 2] = (byte)(pieceNums[i] >> 8);
                payload[2 + i * 2] = (byte)(pieceNums[i] & 0xFF);
            }
            Send_ActionRequest(socket, tableSerial, (short)ActRpgSetInitiative, payload);
        }

        public static void Send_RpgNextInitiative(NetClient socket, uint tableSerial)
        {
            Send_ActionRequest(socket, tableSerial, (short)ActRpgNextInitiative, System.Array.Empty<byte>());
        }

        public static void Send_RpgAddDiceMacro(NetClient socket, uint tableSerial, string name, string expression)
        {
            var payload = new byte[DiceMacroNameBytes + DiceMacroExprBytes];
            WriteFixedAscii(payload, 0, DiceMacroNameBytes, name);
            WriteFixedAscii(payload, DiceMacroNameBytes, DiceMacroExprBytes, expression);
            Send_ActionRequest(socket, tableSerial, (short)ActRpgAddDiceMacro, payload);
        }

        public static void Send_RpgRemoveDiceMacro(NetClient socket, uint tableSerial, string name)
        {
            var payload = new byte[DiceMacroNameBytes];
            WriteFixedAscii(payload, 0, DiceMacroNameBytes, name);
            Send_ActionRequest(socket, tableSerial, (short)ActRpgRemoveDiceMacro, payload);
        }

        public static void Send_RpgRollDiceMacro(NetClient socket, uint tableSerial, string name)
        {
            var payload = new byte[DiceMacroNameBytes];
            WriteFixedAscii(payload, 0, DiceMacroNameBytes, name);
            Send_ActionRequest(socket, tableSerial, (short)ActRpgRollDiceMacro, payload);
        }

        public static void Send_RpgSetPieceVisibility(
            NetClient socket, uint tableSerial, ushort pieceNum, ushort hiddenFromSeats
        )
        {
            var payload = new byte[4];
            payload[0] = (byte)(pieceNum >> 8); payload[1] = (byte)(pieceNum & 0xFF);
            payload[2] = (byte)(hiddenFromSeats >> 8); payload[3] = (byte)(hiddenFromSeats & 0xFF);
            Send_ActionRequest(socket, tableSerial, (short)ActRpgSetPieceVisibility, payload);
        }

        private static void WriteFixedAscii(byte[] buffer, int offset, int width, string value)
        {
            value ??= string.Empty;
            var max = System.Math.Min(value.Length, width);
            for (var i = 0; i < max; i++)
            {
                var c = value[i];
                buffer[offset + i] = c < 128 ? (byte)c : (byte)'?';
            }
            for (var i = max; i < width; i++)
            {
                buffer[offset + i] = 0;
            }
        }

        private static void SendNoPayload(NetClient socket, uint tableSerial, byte sub)
        {
            var writer = new StackDataWriter(9);
            writer.WriteUInt8(PacketId);
            writer.WriteUInt16BE(9);
            writer.WriteUInt32BE(tableSerial);
            writer.WriteUInt8(ProtocolVersion);
            writer.WriteUInt8(sub);

            socket.Send(writer.BufferWritten);
            writer.Dispose();
        }
    }
}
