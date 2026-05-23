// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Managers;

namespace ClassicUO.Game.UI.Gumps.BoardGames
{
    /// <summary>
    /// Looks up the open <see cref="BoardGameWindow"/> for a given table serial
    /// or opens a new one. There is at most one window per table at a time.
    /// </summary>
    internal static class BoardGameWindowRegistry
    {
        public static BoardGameWindow Get(uint tableSerial)
        {
            return UIManager.GetGump<BoardGameWindow>(tableSerial);
        }

        public static BoardGameWindow GetOrOpen(World world, uint tableSerial)
        {
            var existing = Get(tableSerial);
            if (existing != null && !existing.IsDisposed)
            {
                return existing;
            }

            var window = new BoardGameWindow(world, tableSerial);
            UIManager.Add(window);
            return window;
        }

        public static void Close(uint tableSerial)
        {
            var w = Get(tableSerial);
            w?.Dispose();
        }
    }
}
