// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>
    /// Combat context restriction for a voice command.
    /// </summary>
    internal enum CombatContext
    {
        /// <summary>Command works in any state.</summary>
        Any,
        /// <summary>Command only works while in war mode (combat).</summary>
        CombatOnly,
        /// <summary>Command only works while in peace mode (non-combat).</summary>
        NonCombatOnly
    }

    /// <summary>
    /// Voice command mode — determines which commands are available.
    /// </summary>
    internal enum VoiceCommandCategory
    {
        /// <summary>Available in both Simple and Advanced modes.</summary>
        Core,
        /// <summary>Only available in Advanced mode.</summary>
        Advanced
    }

    /// <summary>
    /// A single entry in the command registry used by the TokenScorer and LlmScorer.
    /// Built once at startup from all speech command sources.
    /// </summary>
    internal sealed class CommandRegistryEntry
    {
        /// <summary>The server command string, e.g. "[warmap]".</summary>
        public string Command { get; init; }

        /// <summary>Human-readable label shown in the ActionHud, e.g. "Open War Map".</summary>
        public string Label { get; init; }

        /// <summary>All known speech phrases that invoke this command (exact matching candidates).</summary>
        public string[] Phrases { get; init; }

        /// <summary>Key terms extracted from phrases used for overlap scoring.</summary>
        public string[] Keywords { get; init; }

        /// <summary>
        /// When true, this command can fire immediately on a high-confidence partial result
        /// without showing the HUD (e.g. spell casts, macro triggers, read-only queries).
        /// When false, the command always goes through the HUD confirmation flow.
        /// </summary>
        public bool IsEager { get; init; }

        /// <summary>Combat context restriction. Defaults to Any.</summary>
        public CombatContext Combat { get; init; } = CombatContext.Any;

        /// <summary>Command category for Simple/Advanced mode filtering.</summary>
        public VoiceCommandCategory Category { get; init; } = VoiceCommandCategory.Core;
    }
}
