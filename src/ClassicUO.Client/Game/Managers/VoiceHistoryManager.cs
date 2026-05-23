// SPDX-License-Identifier: BSD-2-Clause
//
// Ring buffer of the last N NPC voice lines the local player heard. Powers
// the `-rv` / `-replayvoice` client command. Recording happens regardless
// of whether NpcVoiceManager actually played the line — frequency/cooldown
// gates can drop playback, but the player can still ask "what did I miss?".

using System;
using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    internal sealed class VoiceHistoryManager
    {
        public static readonly VoiceHistoryManager Instance = new VoiceHistoryManager();

        public readonly record struct HistoryEntry(
            uint NpcSerial,
            string NpcName,
            string RelPath,
            DateTime At
        );

        private readonly LinkedList<HistoryEntry> _entries = new LinkedList<HistoryEntry>();
        private int _maxEntries = 20;

        /// <summary>Cap, bound by Profile.NpcVoiceHistoryLength. Default 20.</summary>
        public int MaxEntries
        {
            get => _maxEntries;
            set
            {
                _maxEntries = Math.Clamp(value, 1, 200);
                Trim();
            }
        }

        public int Count => _entries.Count;

        public void Record(uint npcSerial, string npcName, string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return;
            _entries.AddFirst(new HistoryEntry(npcSerial, npcName ?? "?", relPath, DateTime.UtcNow));
            Trim();
        }

        /// <summary>Most recent <paramref name="n"/> entries, newest first.</summary>
        public List<HistoryEntry> Last(int n)
        {
            int take = Math.Clamp(n, 0, _entries.Count);
            var list = new List<HistoryEntry>(take);
            var node = _entries.First;
            for (int i = 0; i < take && node != null; i++, node = node.Next)
            {
                list.Add(node.Value);
            }
            return list;
        }

        public void Clear() => _entries.Clear();

        private void Trim()
        {
            while (_entries.Count > _maxEntries)
            {
                _entries.RemoveLast();
            }
        }
    }
}
