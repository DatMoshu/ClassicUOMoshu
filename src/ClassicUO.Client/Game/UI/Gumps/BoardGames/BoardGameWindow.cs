// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Network;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Generic board-game window driven by the framework packet <c>0xE0</c>.
    /// One instance per open table; renders any framework game (chess, checkers,
    /// future games) using the server-supplied layout descriptor and per-piece
    /// graphic IDs — no per-game client code is required.
    /// </summary>
    internal sealed class BoardGameWindow : Gump
    {
        // Chrome
        private const int Padding = 16;
        private const int SeatPlateHeight = 24;
        private const int FooterHeight = 32;

        public BoardGameState State { get; } = new();

        // UI layer references that get rebuilt on each Apply call.
        private Control _boardLayer;
        private Control _pieceLayer;
        private Control _playerLayer;
        private Label _turnLabel;
        private Label _outcomeBanner;

        // Click-to-move state.
        private int _selectedPieceNumber = -1;

        // Drag-and-drop state. _dragActive flips on once the cursor moves past
        // a few pixels from the down position — short clicks fall back to the
        // existing click-to-select logic.
        private int _dragPieceNumber = -1;
        private int _dragStartFile = -1;
        private int _dragStartRank = -1;
        private int _dragStartLocalX;
        private int _dragStartLocalY;
        private bool _dragActive;

        private const int DragThresholdPx = 4;

        public BoardGameWindow(World world, uint tableSerial) : base(world, tableSerial, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;
            WantUpdateSize = false;

            // Width/Height are set once we receive the JoinGame layout descriptor.
            Width = 320;
            Height = 320;

            X = 100;
            Y = 100;
        }

        // --- Apply hooks called by BoardGamePackets ----------------------------

        public void ApplyLayout()
        {
            Clear();

            var st = State;
            var board = ComputeBoardPixelSize();
            var totalW = board + Padding * 2;
            var totalH = board + Padding * 2 + SeatPlateHeight * st.SeatCount + FooterHeight;

            Width = totalW;
            Height = totalH;

            // Gump frame
            Add(new ResizePic(9270) { Width = Width, Height = Height });

            // Board surface: server may supply a backdrop gump (rendered as
            // gump-art, scaled to fit the board), otherwise draw a checkerboard
            // grid. Note: StaticPic reads from art.mul (items/statics) and is
            // wrong here — backdrop ids live in gumpart.mul/gumpartLegacyMUL.uop.
            if (st.BackdropGraphicId != 0)
            {
                Add(new ScaledGumpPic(st.BackdropGraphicId)
                {
                    X = Padding,
                    Y = Padding,
                    Width = board,
                    Height = board
                });
            }
            else
            {
                Add(new CheckerboardLayer(st.GridWidth, st.GridHeight, st.TilePixelSize)
                {
                    X = Padding,
                    Y = Padding,
                    Width = board,
                    Height = board
                });
            }

            // Grid-line overlay on top of the backdrop (RPG tables).
            if (st.IsRpgTable && st.GridLinesVisible)
            {
                Add(new GridLineOverlay(st.GridWidth, st.GridHeight, st.TilePixelSize, st.GridColorRgba)
                {
                    X = Padding,
                    Y = Padding,
                    Width = board,
                    Height = board
                });
            }

            // Board layer holds the grid (selection highlight + click capture).
            _boardLayer = new BoardClickLayer(this, board)
            {
                X = Padding,
                Y = Padding,
                Width = board,
                Height = board
            };
            Add(_boardLayer);

            // Piece layer is rebuilt every ApplyPieces.
            _pieceLayer = new EmptyContainer
            {
                X = Padding,
                Y = Padding,
                Width = board,
                Height = board
            };
            Add(_pieceLayer);

            // Player layer below the board.
            _playerLayer = new EmptyContainer
            {
                X = Padding,
                Y = Padding + board + 4,
                Width = board,
                Height = SeatPlateHeight * st.SeatCount
            };
            Add(_playerLayer);

            // Turn indicator footer.
            _turnLabel = new Label("Waiting...", false, 0x0481, Width - Padding * 2, 2)
            {
                X = Padding,
                Y = Height - FooterHeight + 4
            };
            Add(_turnLabel);

            // Leave button (top-right).
            Add(new Button(BtnLeave, 0x00FB, 0x00FC, 0x00FA)
            {
                X = Width - 22,
                Y = 4,
                ButtonAction = ButtonAction.Activate
            });

            // RPG / dice toolbar — only shown when the table publishes the
            // RPG game tag (or the dice are otherwise applicable in future).
            if (st.IsRpgTable)
            {
                var bx = 4;
                var by = Height - FooterHeight + 4;
                Add(new NiceButton(bx, by, 50, 22, ButtonAction.Activate, "Dice")
                    { ButtonParameter = BtnDice, IsSelectable = false });
                Add(new NiceButton(bx + 54, by, 50, 22, ButtonAction.Activate, "Grid")
                    { ButtonParameter = BtnGrid, IsSelectable = false });
                Add(new NiceButton(bx + 108, by, 60, 22, ButtonAction.Activate, "Pieces")
                    { ButtonParameter = BtnPieceViewer, IsSelectable = false });
                Add(new NiceButton(bx + 172, by, 64, 22, ButtonAction.Activate, "Actions")
                    { ButtonParameter = BtnActions, IsSelectable = false });
            }

            ApplyPlayers();
            ApplyPieces();
            ApplyGeneral();
        }

        public void ApplyPieces()
        {
            if (_pieceLayer == null) return;
            _pieceLayer.Clear();

            var st = State;
            var size = st.TilePixelSize;

            foreach (var piece in st.Pieces)
            {
                if (!piece.IsOnBoard) continue;
                if (piece.Number == _dragPieceNumber && _dragActive) continue; // hide piece while dragging

                // Pieces store logical (file, rank). Flip rendering vertically so
                // rank 0 (White's back rank) sits at the bottom of the window.
                var (px, py) = LogicalToPixel(piece.PositionX, piece.PositionY, size);

                st.PieceProps.TryGetValue(piece.Number, out var props);
                var (rx, ry, scaledSize) = ComputeSpriteRect(px, py, size, props);

                // Arkham pieces use gump-art (0xC200+ range) so we render via
                // ScaledGumpPic; legacy games (chess, RpgTable, stock games)
                // still use item static-art via StaticPic. The 0xC000 threshold
                // matches ADR-013's reserved gump-art range. Both renderers
                // accept Width/Height so the piece fits the cell pixel size.
                Control sprite;
                if (st.IsArkhamTable && piece.GraphicId >= 0xC000)
                {
                    sprite = new ScaledGumpPic(piece.GraphicId)
                    {
                        X = rx,
                        Y = ry,
                        Width = scaledSize,
                        Height = scaledSize
                    };
                }
                else
                {
                    sprite = new StaticPic(piece.GraphicId, 0)
                    {
                        X = rx,
                        Y = ry,
                        Width = scaledSize,
                        Height = scaledSize,
                        AcceptMouseInput = false
                    };
                }

                _pieceLayer.Add(sprite);

                // RPG-only: HP/MP bars + label anchored to the scaled sprite.
                if (props != null)
                {
                    AddPieceBars(_pieceLayer, rx, ry, scaledSize, props);
                }
            }

            // Selection highlight: hue-tinted alpha block over the selected square.
            if (_selectedPieceNumber >= 0)
            {
                var sel = FindPiece(_selectedPieceNumber);
                if (sel != null && sel.IsOnBoard)
                {
                    var (px, py) = LogicalToPixel(sel.PositionX, sel.PositionY, size);
                    _pieceLayer.Add(new AlphaBlendControl(0.35f)
                    {
                        X = px,
                        Y = py,
                        Width = size,
                        Height = size,
                        Hue = 0x0035 // yellow-ish UO hue
                    });
                }
            }

            // Keep any open Actions gump in sync with the current selection +
            // latest piece props (HP/MP/scale just changed, label may have, etc).
            BoardGameActionsGump.RefreshIfOpen(World, State.TableSerial);
        }

        public void ApplyPlayers()
        {
            if (_playerLayer == null) return;
            _playerLayer.Clear();

            var y = 0;
            foreach (var seat in State.Seats)
            {
                var hue = seat.InGame ? (ushort)0x0481 : (ushort)0x0481;
                if (seat.Pending) hue = 0x0028; // red-ish pending
                if (seat.IsDealer) hue = 0x0035;

                var tag = seat.SeatIndex == State.OwnSeat ? " (you)" : "";
                var stateTag = seat.Pending ? " [DC]" : seat.InGame ? "" : " [empty]";

                _playerLayer.Add(new Label(
                    $"#{seat.SeatIndex} {seat.Name}{tag}{stateTag}  pts:{seat.Score}",
                    false, hue, _playerLayer.Width, 2)
                {
                    X = 2,
                    Y = y
                });
                y += SeatPlateHeight;
            }
        }

        public void ApplyGeneral()
        {
            if (_turnLabel == null) return;
            var st = State;

            string turn;
            if (st.IsSpectator)
            {
                turn = $"Spectating — seat #{st.CurrentTurnSeat} to move";
            }
            else if (st.IsOwnTurn)
            {
                turn = "Your turn.";
            }
            else
            {
                turn = $"Waiting on seat #{st.CurrentTurnSeat}.";
            }

            if (st.HasMoveClock && st.MoveClockMs > 0)
            {
                turn += $"  ({st.MoveClockMs / 1000}s)";
            }

            _turnLabel.Text = turn;
        }

        /// <summary>
        /// Called by the packet pipeline after any Arkham 0xE0/0x30..0x36 sub-cmd
        /// updates <see cref="BoardGameState.Arkham"/>. The Arkham HUD listens on
        /// this hook and re-renders. Non-Arkham tables are unaffected.
        /// </summary>
        public void OnArkhamStateChanged()
        {
            ArkhamHudGump.RefreshIfOpenOrOpen(World, this);
        }

        public void AnnounceOutcome(byte kind, sbyte winningSeat, int cliloc)
        {
            string label = kind switch
            {
                1 => $"Winner: seat #{winningSeat}",
                2 => "Draw.",
                3 => $"Forfeit by seat #{winningSeat}",
                _ => "Game ended."
            };

            _outcomeBanner?.Dispose();
            _outcomeBanner = new Label(label, true, 0x0035, Width, 3)
            {
                X = 0,
                Y = Height / 2 - 12
            };
            Add(_outcomeBanner);
        }

        // --- Click-to-move pipeline --------------------------------------------

        /// <summary>
        /// Double-click hook. In RPG mode, double-clicking a piece opens its
        /// properties gump. Non-RPG games ignore double-click for now (chess
        /// will get drag-and-drop in a polish pass).
        /// </summary>
        internal void OnBoardDoubleClick(int file, int rank)
        {
            var st = State;
            if (!st.IsRpgTable) return;

            foreach (var p in st.Pieces)
            {
                if (p.IsOnBoard && p.PositionX == file && p.PositionY == rank)
                {
                    st.PieceProps.TryGetValue(p.Number, out var props);
                    UIManager.Add(new BoardGamePiecePropertiesGump(World, st.TableSerial, p.Number, st, props));
                    return;
                }
            }
        }

        internal void OnBoardClick(int file, int rank)
        {
            var st = State;

            // Arkham click-to-move: when the table is Arkham, the local player
            // is the active seat, and the click is in-bounds, send the click
            // as a PerformAction(Move). Server validates phase / action count.
            // No drag, no selection model — every click is a Move request.
            if (st.IsArkhamTable && !st.IsSpectator && st.Arkham != null
                && st.OwnSeat == st.Arkham.ActiveSeat)
            {
                BoardGamePackets.Send_ArkhamPerformAction(
                    NetClient.Socket, st.TableSerial,
                    actionType: 1 /* Move */,
                    tx: (short)file, ty: (short)rank);
                return;
            }

            if (st.IsSpectator || !CanMovePieceAt(file, rank))
            {
                if (!st.IsRpgTable)
                {
                    _selectedPieceNumber = -1;
                }
                ApplyPieces();
                return;
            }

            // Find a piece on the clicked tile that the caller could target.
            BoardGamePieceView pieceOnTile = null;
            foreach (var p in st.Pieces)
            {
                if (!p.IsOnBoard || p.PositionX != file || p.PositionY != rank) continue;
                if (!st.IsRpgTable && p.OwnerSeat != st.OwnSeat) continue;
                pieceOnTile = p;
                break;
            }

            if (st.IsRpgTable)
            {
                // RPG tables: clicks only ever (re-)target a piece for the Actions
                // gump. Clicks on empty tiles or repeated clicks on the same piece
                // are no-ops — selection is sticky until another piece is clicked.
                // (Movement happens exclusively via drag.)
                if (pieceOnTile != null)
                {
                    _selectedPieceNumber = pieceOnTile.Number;
                    ApplyPieces();
                }
                return;
            }

            // --- Chess / strict-turn-order games: legacy click-to-move ---------
            if (_selectedPieceNumber < 0)
            {
                if (pieceOnTile != null)
                {
                    _selectedPieceNumber = pieceOnTile.Number;
                    ApplyPieces();
                }
                return;
            }

            var selected = FindPiece(_selectedPieceNumber);
            if (selected == null)
            {
                _selectedPieceNumber = -1;
                return;
            }

            // Click on the same square deselects.
            if (selected.PositionX == file && selected.PositionY == rank)
            {
                _selectedPieceNumber = -1;
                ApplyPieces();
                return;
            }

            SendMoveAndOptionalOffset(selected, (short)file, (short)rank, snappedOffsetX: 0, snappedOffsetY: 0, snapToGrid: true);
            _selectedPieceNumber = -1;
        }

        // --- Drag-and-drop pipeline --------------------------------------------

        internal void OnBoardMouseDown(int file, int rank, int localX, int localY)
        {
            var st = State;

            if (st.IsSpectator) return;
            if (!CanMovePieceAt(file, rank)) return;

            // Find the topmost piece on this tile that we're allowed to drag.
            // We only record drag-candidate state here — selection is committed
            // by OnBoardClick on mouseUp (a tap), so a partial press-and-release
            // doesn't deselect an already-targeted piece.
            foreach (var p in st.Pieces)
            {
                if (!p.IsOnBoard || p.PositionX != file || p.PositionY != rank) continue;
                if (!st.IsRpgTable && p.OwnerSeat != st.OwnSeat) continue;

                _dragPieceNumber = p.Number;
                _dragStartFile = file;
                _dragStartRank = rank;
                _dragStartLocalX = localX;
                _dragStartLocalY = localY;
                _dragActive = false; // promoted on first significant movement in OnBoardMouseMove
                return;
            }
        }

        internal void OnBoardMouseMove(int localX, int localY)
        {
            if (_dragPieceNumber < 0 || _dragActive) return;

            var dx = localX - _dragStartLocalX;
            var dy = localY - _dragStartLocalY;
            if (dx * dx + dy * dy < DragThresholdPx * DragThresholdPx) return;

            _dragActive = true;
            ApplyPieces(); // hide the source piece so the user sees it lift
        }

        internal void OnBoardMouseUp(int file, int rank, int localX, int localY)
        {
            if (_dragPieceNumber < 0)
            {
                // Plain click — defer to existing select / destination flow.
                OnBoardClick(file, rank);
                return;
            }

            var dragNum = _dragPieceNumber;
            var startFile = _dragStartFile;
            var startRank = _dragStartRank;
            var wasActive = _dragActive;

            // Clear drag state up front so re-render logic doesn't see ghosts.
            _dragPieceNumber = -1;
            _dragStartFile = -1;
            _dragStartRank = -1;
            _dragActive = false;

            if (!wasActive)
            {
                // Treat as a click — re-run the click handler at the down tile.
                OnBoardClick(startFile, startRank);
                return;
            }

            var piece = FindPiece(dragNum);
            if (piece == null)
            {
                ApplyPieces();
                return;
            }

            // Compute pixel offset within the destination tile. The sprite's
            // pivot is bottom-center, so offset (0,0) means feet at the tile's
            // bottom-center. We translate cursor position into that frame so a
            // drop puts the feet exactly under the cursor.
            var st = State;
            var tilePx = st.TilePixelSize == 0 ? 30 : st.TilePixelSize;
            var (tileBaseX, tileBaseY) = LogicalToPixel((short)file, (short)rank, tilePx);
            var offsetX = localX - tileBaseX - tilePx / 2;
            var offsetY = localY - tileBaseY - tilePx;

            // Snap behaviour: when SnapToGrid is true, drop on tile center (zero offset).
            // When false, keep the cursor's sub-tile offset — but only RPG tables honor it.
            var snap = st.SnapToGrid || !st.IsRpgTable;
            if (snap)
            {
                offsetX = 0;
                offsetY = 0;
            }

            SendMoveAndOptionalOffset(piece, (short)file, (short)rank, (short)offsetX, (short)offsetY, snap);

            if (st.IsRpgTable)
            {
                // RPG: keep the just-dropped piece selected so the Actions gump
                // continues to operate on the same target across moves.
                _selectedPieceNumber = piece.Number;
            }
            else
            {
                // Chess: clearing selection after a move is the legacy UX.
                _selectedPieceNumber = -1;
            }
        }

        private void SendMoveAndOptionalOffset(
            BoardGamePieceView piece, short file, short rank,
            short snappedOffsetX, short snappedOffsetY, bool snapToGrid
        )
        {
            var st = State;

            BoardGamePackets.Send_MovePiece(
                NetClient.Socket,
                st.TableSerial,
                piece.Number,
                file,
                rank,
                piece.Direction,
                (byte)(piece.Layer | 0x01),
                piece.Flipped,
                _nextRequestId++
            );

            // For RPG tables, push the new sub-tile offset (or zero it when
            // snapping) by re-sending the existing piece properties. Other games
            // ignore PieceProps and just snap to tile centers.
            if (!st.IsRpgTable) return;

            st.PieceProps.TryGetValue(piece.Number, out var existing);
            var hp = existing?.Hp ?? 0;
            var maxHp = existing?.MaxHp ?? 0;
            var mp = existing?.Mp ?? 0;
            var maxMp = existing?.MaxMp ?? 0;
            var label = existing?.Label ?? string.Empty;
            var scale = existing?.ScalePercent ?? BoardGamePieceProps.DefaultScalePercent;

            var newOffsetX = snapToGrid ? (short)0 : snappedOffsetX;
            var newOffsetY = snapToGrid ? (short)0 : snappedOffsetY;

            // Only send if anything actually changed — avoids needless echo.
            if (existing != null && existing.OffsetX == newOffsetX && existing.OffsetY == newOffsetY)
            {
                return;
            }

            BoardGamePiecePropertiesGump.SendPropsUpdate(
                NetClient.Socket, st.TableSerial, piece.Number,
                hp, maxHp, mp, maxMp, label, newOffsetX, newOffsetY, scale
            );
        }

        internal void OnBoardDragCancel()
        {
            if (_dragPieceNumber < 0) return;
            _dragPieceNumber = -1;
            _dragStartFile = -1;
            _dragStartRank = -1;
            _dragActive = false;
            ApplyPieces();
        }

        private bool CanMovePieceAt(int file, int rank)
        {
            var st = State;
            if (file < 0 || file >= st.GridWidth) return false;
            if (rank < 0 || rank >= st.GridHeight) return false;

            // RPG tables: no turn order, anyone seated can move anything.
            if (st.IsRpgTable) return !st.IsSpectator;

            // Other games: enforce own-turn for actual move attempts.
            return st.IsOwnTurn;
        }

        /// <summary>
        /// Produce the actual draw rect (x, y, size) for a piece given its tile
        /// position, the tile pixel size, and optional RPG props. The pivot is
        /// the sprite's bottom-center: scaling grows the piece upward, keeping
        /// its "feet" anchored to the bottom-center of the tile. OffsetX/Y are
        /// expressed relative to that anchor so dragging by the feet works
        /// naturally regardless of scale.
        /// </summary>
        private static (int x, int y, int size) ComputeSpriteRect(int px, int py, int tilePx, BoardGamePieceProps props)
        {
            if (props == null)
            {
                return (px, py, tilePx);
            }

            var scaledSize = tilePx * (int)props.ScalePercent / 100;
            if (scaledSize < 4) scaledSize = 4;

            // Horizontal: center the (scaled) sprite over the tile center, then offset.
            var rx = px + (tilePx - scaledSize) / 2 + props.OffsetX;

            // Vertical: anchor the sprite's bottom edge to the tile's bottom edge,
            // so a 200%-scaled piece extends one tile *upward* (a giant's head goes
            // higher, but its feet stay on the same square). Offset applies after.
            var ry = py + (tilePx - scaledSize) + props.OffsetY;

            return (rx, ry, scaledSize);
        }

        // --- Helpers ------------------------------------------------------------

        private const int BtnLeave = 1;
        private const int BtnDice = 10;
        private const int BtnGrid = 11;
        private const int BtnPieceViewer = 12;
        private const int BtnActions = 13;

        /// <summary>Currently-selected piece number on the board, or -1.</summary>
        public int SelectedPieceNumber => _selectedPieceNumber;

        public override void OnButtonClick(int buttonID)
        {
            switch (buttonID)
            {
                case BtnLeave:
                    BoardGamePackets.Send_LeaveGame(NetClient.Socket, State.TableSerial);
                    Dispose();
                    break;
                case BtnDice:
                    OpenChild(new BoardGameDiceGump(World, State.TableSerial, State));
                    break;
                case BtnGrid:
                    OpenChild(new BoardGameGridSettingsGump(World, State.TableSerial, State));
                    break;
                case BtnPieceViewer:
                    OpenChild(new BoardGamePieceViewer(World, State.TableSerial, State));
                    break;
                case BtnActions:
                    BoardGameActionsGump.Toggle(World, this);
                    break;
            }
        }

        private void OpenChild(Gump g)
        {
            UIManager.Add(g);
        }

        /// <summary>Called by the packet dispatcher when a new dice roll arrives.</summary>
        public void OnDiceRollReceived(BoardGameDiceRoll roll)
        {
            // If a dice gump is open, ask it to refresh history. Otherwise toast
            // the roll into the turn label briefly.
            foreach (var gump in UIManager.Gumps)
            {
                if (gump is BoardGameDiceGump dg && gump.LocalSerial == State.TableSerial)
                {
                    dg.RefreshHistory();
                    return;
                }
            }

            if (_turnLabel != null)
            {
                var lbl = string.IsNullOrEmpty(roll.Label) ? "" : $" [{roll.Label}]";
                _turnLabel.Text = $"#{roll.RollerSeat} rolled {roll.Results.Length}d{roll.Sides} = {roll.Total}{lbl}";
            }
        }

        private void AddPieceBars(Control parent, int px, int py, int size, BoardGamePieceProps props)
        {
            const int barH = 4;
            const int pad = 2;
            var barY = py + size - barH * 2 - pad - 2;

            if (props.MaxHp > 0)
            {
                // backdrop
                parent.Add(new AlphaBlendControl(0.55f)
                {
                    X = px + 2, Y = barY, Width = size - 4, Height = barH, Hue = 0x0021
                });
                var fillW = (size - 4) * System.Math.Max(0, (int)props.Hp) / System.Math.Max(1, (int)props.MaxHp);
                parent.Add(new AlphaBlendControl(0.85f)
                {
                    X = px + 2, Y = barY, Width = fillW, Height = barH, Hue = 0x0026
                });
            }

            if (props.MaxMp > 0)
            {
                var mpY = barY + barH + 1;
                parent.Add(new AlphaBlendControl(0.55f)
                {
                    X = px + 2, Y = mpY, Width = size - 4, Height = barH, Hue = 0x0006
                });
                var fillW = (size - 4) * System.Math.Max(0, (int)props.Mp) / System.Math.Max(1, (int)props.MaxMp);
                parent.Add(new AlphaBlendControl(0.85f)
                {
                    X = px + 2, Y = mpY, Width = fillW, Height = barH, Hue = 0x0058
                });
            }

            if (!string.IsNullOrEmpty(props.Label))
            {
                parent.Add(new Label(props.Label, false, 0x0481, size, 1)
                {
                    X = px, Y = py - 14
                });
            }
        }

        private int ComputeBoardPixelSize()
        {
            var st = State;
            // BoardKind.Free declares GridWidth/Height as raw pixels (Arkham
            // and any future multi-zone game). Grid/Hex modes multiply cells
            // by the tile pixel size. The current render uses a single square
            // dimension so we pick the smaller side to keep the board square.
            if (st.Kind == BoardKind.Free)
            {
                var w = st.GridWidth  > 0 ? (int)st.GridWidth  : 320;
                var h = st.GridHeight > 0 ? (int)st.GridHeight : 320;
                var side = w < h ? w : h;
                // Hard cap so a buggy server descriptor can't blow the window
                // off-screen (a 20k-pixel board destroyed a playtest once).
                return side > 600 ? 600 : side;
            }
            return st.GridWidth * (st.TilePixelSize == 0 ? 30 : st.TilePixelSize);
        }

        /// <summary>
        /// Map a logical (file, rank) to a pixel position relative to the board layer's
        /// top-left corner. Logical rank 0 renders at the bottom; from your seat's view,
        /// non-RPG games (chess) flip the board vertically if you're seat 1 (Black).
        /// RPG tables never flip — every seat sees the same world orientation.
        /// </summary>
        internal (int x, int y) LogicalToPixel(int file, int rank, int tilePx)
        {
            var st = State;
            var renderFile = file;
            var renderRank = (st.GridHeight - 1) - rank;

            // Black seat (1) sees the board rotated 180° so their pieces are at the bottom.
            // Skipped for RPG tables — those have 8 seats with no opposing-side convention.
            if (!st.IsRpgTable && st.OwnSeat == 1)
            {
                renderFile = (st.GridWidth - 1) - file;
                renderRank = rank;
            }

            return (renderFile * tilePx, renderRank * tilePx);
        }

        internal (int file, int rank) PixelToLogical(int px, int py, int tilePx)
        {
            var st = State;
            var renderFile = px / tilePx;
            var renderRank = py / tilePx;

            if (!st.IsRpgTable && st.OwnSeat == 1)
            {
                return ((st.GridWidth - 1) - renderFile, renderRank);
            }

            return (renderFile, (st.GridHeight - 1) - renderRank);
        }

        private BoardGamePieceView FindPiece(int number)
        {
            foreach (var p in State.Pieces)
            {
                if (p.Number == number) return p;
            }
            return null;
        }

        private uint _nextRequestId = 1;

        /// <summary>
        /// Renders a gump-art asset (from gumpart.mul / gumpartLegacyMUL.uop)
        /// stretched to fill the control's Width × Height. Built because none
        /// of the built-in controls (GumpPic = native size, GumpPicTiled = tiled,
        /// GumpPicInPic = cropped, ResizePic = 9-slice) support uniform scaling
        /// of a gump image to an arbitrary rect.
        /// </summary>
        private sealed class ScaledGumpPic : Control
        {
            private readonly ushort _graphic;

            public ScaledGumpPic(ushort graphic)
            {
                _graphic = graphic;
                AcceptMouseInput = false;
                CanMove = false;
            }

            public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
            {
                float layerDepth = layerDepthRef;
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);
                int w = Width, h = Height;

                ref readonly var gumpInfo = ref Client.Game.UO.Gumps.GetGump(_graphic);
                var texture = gumpInfo.Texture;
                if (texture != null)
                {
                    var sourceRectangle = gumpInfo.UV;
                    renderLists.AddGumpWithAtlas(batcher =>
                    {
                        batcher.Draw(
                            texture,
                            new Rectangle(x, y, w, h),
                            sourceRectangle,
                            hueVector,
                            layerDepth
                        );
                        return true;
                    });
                }

                return base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);
            }
        }

        /// <summary>
        /// Draws an N×M checkered grid (light/dark squares) at the control's
        /// pixel position. Used when the server didn't supply a backdrop gump.
        /// </summary>
        private sealed class CheckerboardLayer : Control
        {
            private readonly int _cols;
            private readonly int _rows;
            private readonly int _tile;

            private static readonly Color _light = new(222, 184, 135); // burlywood
            private static readonly Color _dark = new(101, 67, 33);    // dark brown

            public CheckerboardLayer(int cols, int rows, int tile)
            {
                _cols = cols;
                _rows = rows;
                _tile = tile == 0 ? 44 : tile;
                AcceptMouseInput = false;
                CanMove = false;
            }

            public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
            {
                float layerDepth = layerDepthRef;
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);
                int cols = _cols, rows = _rows, tile = _tile;

                renderLists.AddGumpNoAtlas(batcher =>
                {
                    var lightTex = SolidColorTextureCache.GetTexture(_light);
                    var darkTex  = SolidColorTextureCache.GetTexture(_dark);

                    for (var r = 0; r < rows; r++)
                    {
                        for (var f = 0; f < cols; f++)
                        {
                            var tex = ((f + r) & 1) == 0 ? lightTex : darkTex;
                            batcher.Draw(
                                tex,
                                new Rectangle(x + f * tile, y + r * tile, tile, tile),
                                hueVector,
                                layerDepth
                            );
                        }
                    }
                    return true;
                });

                return base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);
            }
        }

        /// <summary>Concrete empty container — used for piece + player layer grouping.</summary>
        private sealed class EmptyContainer : Control
        {
            public EmptyContainer()
            {
                CanMove = false;
                AcceptMouseInput = false;
            }
        }

        /// <summary>Inner control that captures clicks + drag on the board grid.</summary>
        private sealed class BoardClickLayer : Control
        {
            private readonly BoardGameWindow _owner;
            private readonly int _boardPx;

            public BoardClickLayer(BoardGameWindow owner, int boardPx)
            {
                _owner = owner;
                _boardPx = boardPx;
                AcceptMouseInput = true;
                CanMove = false;
            }

            private bool TryTile(int x, int y, out int file, out int rank)
            {
                var tilePx = _owner.State.TilePixelSize == 0 ? 30 : _owner.State.TilePixelSize;
                (file, rank) = _owner.PixelToLogical(x, y, tilePx);
                if (file < 0 || file >= _owner.State.GridWidth) return false;
                if (rank < 0 || rank >= _owner.State.GridHeight) return false;
                return true;
            }

            protected override void OnMouseDown(int x, int y, MouseButtonType button)
            {
                base.OnMouseDown(x, y, button);
                if (button != MouseButtonType.Left) return;
                if (!TryTile(x, y, out var file, out var rank)) return;
                _owner.OnBoardMouseDown(file, rank, x, y);
            }

            protected override void OnMouseOver(int x, int y)
            {
                base.OnMouseOver(x, y);
                _owner.OnBoardMouseMove(x, y);
            }

            protected override void OnMouseUp(int x, int y, MouseButtonType button)
            {
                base.OnMouseUp(x, y, button);
                if (button != MouseButtonType.Left) return;
                if (!TryTile(x, y, out var file, out var rank))
                {
                    // Released outside the board — abort drag without sending.
                    _owner.OnBoardDragCancel();
                    return;
                }
                _owner.OnBoardMouseUp(file, rank, x, y);
            }

            protected override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
            {
                if (button != MouseButtonType.Left) return base.OnMouseDoubleClick(x, y, button);
                if (!TryTile(x, y, out var file, out var rank)) return base.OnMouseDoubleClick(x, y, button);
                _owner.OnBoardDoubleClick(file, rank);
                return true;
            }
        }

        /// <summary>
        /// Lightweight grid-line overlay used on RPG tables. Drawn on top of the
        /// board so cells are visually demarcated without obscuring pieces.
        /// </summary>
        private sealed class GridLineOverlay : Control
        {
            private readonly int _cols;
            private readonly int _rows;
            private readonly int _tile;
            private readonly Color _line;

            public GridLineOverlay(int cols, int rows, int tile, uint rgba)
            {
                _cols = cols;
                _rows = rows;
                _tile = tile == 0 ? 30 : tile;
                _line = new Color(
                    (int)((rgba >> 24) & 0xFF),
                    (int)((rgba >> 16) & 0xFF),
                    (int)((rgba >> 8) & 0xFF),
                    (int)(rgba & 0xFF)
                );
                AcceptMouseInput = false;
                CanMove = false;
            }

            public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
            {
                float layerDepth = layerDepthRef;
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);
                int cols = _cols, rows = _rows, tile = _tile;
                Color line = _line;

                renderLists.AddGumpNoAtlas(batcher =>
                {
                    var lineTex = SolidColorTextureCache.GetTexture(line);

                    // vertical lines
                    for (var f = 0; f <= cols; f++)
                    {
                        batcher.Draw(lineTex,
                            new Rectangle(x + f * tile, y, 1, rows * tile),
                            hueVector, layerDepth);
                    }
                    // horizontal lines
                    for (var r = 0; r <= rows; r++)
                    {
                        batcher.Draw(lineTex,
                            new Rectangle(x, y + r * tile, cols * tile, 1),
                            hueVector, layerDepth);
                    }
                    return true;
                });

                return base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);
            }
        }
    }
}
