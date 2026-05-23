// SPDX-License-Identifier: BSD-2-Clause
//
// Handler for the `-replayvoice` / `-rv` client command. Pulls the last N
// entries out of VoiceHistoryManager and chains them through NpcVoiceManager
// with a narrator intro line first (if the player has narrator enabled).

using System;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;

namespace ClassicUO.Game.Managers
{
    internal static class ReplayVoice
    {
        // Narrator audio relative paths (the server doesn't drive these — they
        // live on disk under Data/voicelines/audio/npc-qwen/narrator/full/).
        private const string NarratorDir = "audio/npc-qwen/narrator/full/narrator_loremaster";
        private static readonly string[] _recallSingle =
        {
            "recall_0.mp3", "recall_1.mp3", "recall_2.mp3", "recall_3.mp3",
        };
        private static readonly string[] _recallMany =
        {
            "recall_many_0.mp3", "recall_many_1.mp3", "recall_many_2.mp3",
        };
        private static readonly System.Random _rng = new System.Random();

        public static void Run(World world, int n)
        {
            n = Math.Max(1, n);
            var entries = VoiceHistoryManager.Instance.Last(n);
            if (entries.Count == 0)
            {
                GameActions.Print(world, "-rv: no voice lines in history yet.", 946);
                return;
            }

            // Reverse to oldest-first (so the chain plays in the order the
            // player originally heard them).
            entries.Reverse();

            var profile = ProfileManager.CurrentProfile;
            bool narratorOn = profile?.NarratorEnabled ?? true;

            string intro = null;
            if (narratorOn)
            {
                var pick = entries.Count == 1 ? _recallSingle : _recallMany;
                intro = $"{NarratorDir}/{pick[_rng.Next(pick.Length)]}";
            }

            // Build a print-friendly summary so the player can read along.
            GameActions.Print(world,
                entries.Count == 1
                    ? $"-rv: replaying last line ({entries[0].NpcName})"
                    : $"-rv: replaying last {entries.Count} lines",
                946);

            // Kick off the chain on a worker task — Play() returns the clip
            // duration and we sleep clip + small gap between entries.
            _ = Task.Run(() => PlayChain(intro, entries));
        }

        private static void PlayChain(string intro, System.Collections.Generic.List<VoiceHistoryManager.HistoryEntry> entries)
        {
            try
            {
                int durMs;
                if (!string.IsNullOrEmpty(intro))
                {
                    durMs = NpcVoiceManager.Instance.Play(
                        intro, NpcVoiceManager.NarratorSerial, "Narrator", bypassGates: true);
                    if (durMs > 0) Task.Delay(durMs + 250).Wait();
                }

                foreach (var e in entries)
                {
                    durMs = NpcVoiceManager.Instance.Play(
                        e.RelPath, e.NpcSerial, e.NpcName, bypassGates: true);
                    if (durMs > 0) Task.Delay(durMs + 150).Wait();
                }
            }
            catch (Exception)
            {
                // Worker thread exceptions are silently swallowed — replay is
                // best-effort and should never crash the client.
            }
        }
    }
}
