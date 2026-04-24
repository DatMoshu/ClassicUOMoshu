// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    /// <summary>
    /// Client-side cache of the server-authoritative command list that the local
    /// player is allowed to run. Populated by the 0xBF/0x0110 packet on login and
    /// on AccessLevel change. Used to drive Tab autocomplete in SystemChatControl
    /// and to filter chat history display so demoted players don't re-invoke
    /// commands they no longer have access to.
    ///
    /// The server re-checks AccessLevel on execute, so a tampered client gets
    /// "Access denied" and nothing more.
    /// </summary>
    internal static class ServerCommandRegistry
    {
        // Sorted by name for deterministic Tab cycling. Names are stored without
        // the command prefix ('[' on UOWW).
        private static readonly List<string> _names = new List<string>(256);
        private static readonly HashSet<string> _lookup =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static List<string> _pending;

        public const char Prefix = '[';

        public static int Count => _names.Count;

        public static bool Contains(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return _lookup.Contains(name);
        }

        public static void BeginReplace(int capacityHint)
        {
            _pending = new List<string>(capacityHint <= 0 ? 64 : capacityHint);
        }

        public static void Add(string name, byte minLevel)
        {
            // minLevel is unused for now — server already filtered, and the client
            // doesn't need it for the v1 UX. Kept in the packet for future hover
            // tooltips or colored-by-tier display.
            _ = minLevel;

            if (_pending == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            _pending.Add(name);
        }

        public static void EndReplace()
        {
            if (_pending == null)
            {
                return;
            }

            _pending.Sort(StringComparer.OrdinalIgnoreCase);

            _names.Clear();
            _lookup.Clear();
            foreach (var n in _pending)
            {
                _names.Add(n);
                _lookup.Add(n);
            }

            _pending = null;
        }

        /// <summary>
        /// Returns up to <paramref name="max"/> allowed commands that start with
        /// <paramref name="prefix"/>, in alphabetical order. The prefix should NOT
        /// include the command character ('['); pass the bare token.
        /// </summary>
        public static void Match(string prefix, int max, List<string> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();

            if (_names.Count == 0 || max <= 0)
            {
                return;
            }

            prefix ??= string.Empty;

            foreach (var n in _names)
            {
                if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    into.Add(n);
                    if (into.Count >= max)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>Called on character logout — keeps the next character from
        /// inheriting the previous one's allowed command set.</summary>
        public static void Clear()
        {
            _names.Clear();
            _lookup.Clear();
            _pending = null;
        }
    }
}
