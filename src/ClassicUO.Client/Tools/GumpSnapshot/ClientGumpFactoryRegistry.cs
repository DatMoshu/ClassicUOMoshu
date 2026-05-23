// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Game.UI.Gumps.BoardGames;

namespace ClassicUO.Tools.GumpSnapshot
{
    /// <summary>
    /// Maps a stable factory name to a delegate that returns a fully-seeded
    /// client-built gump. Hand-coded dictionary mirrors the server-side
    /// <c>GumpFactoryRegistry</c> in <c>custom/Scripts/Tools/GumpDump/</c> —
    /// no reflection, AOT-safe, one line per gump to add.
    /// </summary>
    internal static class ClientGumpFactoryRegistry
    {
        public delegate Gump Factory(World world);

        private static readonly Dictionary<string, Factory> _factories =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["RpgCharacterSheetGump"] = world =>
                {
                    var sheet = DemoStateBuilder.BuildCharacterSheet();
                    return new RpgCharacterSheetGump(world, sheet);
                },

                ["BoardGameDiceGump"] = world =>
                {
                    var state = DemoStateBuilder.BuildBoardGameState();
                    return new BoardGameDiceGump(world, state.TableSerial, state);
                },

                ["BoardGameGridSettingsGump"] = world =>
                {
                    var state = DemoStateBuilder.BuildBoardGameState();
                    return new BoardGameGridSettingsGump(world, state.TableSerial, state);
                },

                ["BoardGamePiecePropertiesGump"] = world =>
                {
                    var state = DemoStateBuilder.BuildBoardGameState();
                    state.PieceProps.TryGetValue(1, out var props);
                    return new BoardGamePiecePropertiesGump(
                        world, state.TableSerial, 1, state, props);
                }
            };

        public static bool TryGet(string name, out Factory factory)
            => _factories.TryGetValue(name, out factory);

        public static IEnumerable<string> Names => _factories.Keys;
    }
}
