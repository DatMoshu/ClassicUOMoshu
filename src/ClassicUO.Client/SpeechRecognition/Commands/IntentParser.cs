// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;

namespace ClassicUO.SpeechRecognition.Commands
{
    /// <summary>
    /// Parsed intent from a voice command transcript.
    /// </summary>
    internal sealed class ParsedIntent
    {
        public string IntentName { get; init; }
        public string Argument { get; init; }       // primary argument (location, unit type, etc.)
        public string SecondaryArg { get; init; }   // secondary argument (destination, etc.)

        public static readonly ParsedIntent None = new ParsedIntent { IntentName = "none" };
    }

    /// <summary>
    /// Keyword-based intent extraction for UOWW-specific voice commands.
    /// Converts natural language to structured intents that UowwCommandMap can dispatch.
    ///
    /// Examples:
    ///   "who controls this area" → Intent("territory_query", "current")
    ///   "dispatch infantry to Britain" → Intent("army_dispatch", "infantry_standard", "britain")
    ///   "what is my rank" → Intent("faction_info", "rank")
    ///   "open the war map" → Intent("warmap")
    ///   "what faction am I in" → Intent("faction_info", "faction")
    /// </summary>
    internal static class IntentParser
    {
        // ── Intent detection rules ────────────────────────────────────────────

        public static ParsedIntent Parse(string transcript, GameStateContext ctx)
        {
            string t = transcript.ToLowerInvariant().Trim();

            // Territory / control point queries
            if (ContainsAny(t, "who controls", "who owns", "what faction controls", "quadrant info", "control point"))
            {
                string location = ExtractLocation(t) ?? (ctx?.IsOnWarMap == true ? "current" : "current");
                return new ParsedIntent { IntentName = "territory_query", Argument = location };
            }

            // Faction info
            if (ContainsAny(t, "what faction", "my faction", "which faction am i", "faction info"))
                return new ParsedIntent { IntentName = "faction_info", Argument = "faction" };

            if (ContainsAny(t, "my rank", "what is my rank", "what rank am i"))
                return new ParsedIntent { IntentName = "faction_info", Argument = "rank" };

            if (ContainsAny(t, "my war points", "how many war points", "my points"))
                return new ParsedIntent { IntentName = "faction_info", Argument = "points" };

            // War map
            if (ContainsAny(t, "open war map", "show war map", "war map", "show map", "open map"))
                return new ParsedIntent { IntentName = "warmap" };

            // Army / squad dispatch
            if (ContainsAny(t, "dispatch", "send squad", "send troops", "deploy squad", "move squad"))
            {
                string unitType = ExtractUnitType(t) ?? "infantry_standard";
                string destination = ExtractLocation(t) ?? "here";
                return new ParsedIntent { IntentName = "army_dispatch", Argument = unitType, SecondaryArg = destination };
            }

            // Army status
            if (ContainsAny(t, "army status", "squad status", "troop status", "how many troops"))
                return new ParsedIntent { IntentName = "army_status" };

            // Nearby enemies
            if (ContainsAny(t, "any enemies", "enemies nearby", "hostile nearby", "who is around"))
                return new ParsedIntent { IntentName = "nearby_enemies" };

            // Voice chat channel
            if (ContainsAny(t, "switch to faction", "faction channel", "join faction chat"))
                return new ParsedIntent { IntentName = "voice_channel", Argument = "faction" };

            if (ContainsAny(t, "switch to guild", "guild channel", "join guild chat"))
                return new ParsedIntent { IntentName = "voice_channel", Argument = "guild" };

            if (ContainsAny(t, "proximity voice", "proximity channel", "local chat"))
                return new ParsedIntent { IntentName = "voice_channel", Argument = "proximity" };

            return ParsedIntent.None;
        }

        // ── Extraction helpers ────────────────────────────────────────────────

        private static string ExtractLocation(string text)
        {
            foreach (var loc in KnownLocations)
            {
                if (text.Contains(loc.Key))
                    return loc.Value;
            }
            return null;
        }

        private static string ExtractUnitType(string text)
        {
            if (ContainsAny(text, "infantry", "foot soldier", "footman")) return "infantry_standard";
            if (ContainsAny(text, "archer", "ranged")) return "archer_squad";
            if (ContainsAny(text, "cavalry", "horse", "mounted")) return "cavalry_squad";
            if (ContainsAny(text, "medic", "healer", "support")) return "support_squad";
            if (ContainsAny(text, "assault", "heavy", "siege")) return "assault_squad";
            return null;
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            foreach (string kw in keywords)
                if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ── Known location names ──────────────────────────────────────────────

        private static readonly Dictionary<string, string> KnownLocations = new(StringComparer.OrdinalIgnoreCase)
        {
            // Britannia cities
            ["britain"] = "Britain",
            ["trinsic"] = "Trinsic",
            ["vesper"] = "Vesper",
            ["cove"] = "Cove",
            ["minoc"] = "Minoc",
            ["yew"] = "Yew",
            ["skara brae"] = "Skara Brae",
            ["moonglow"] = "Moonglow",
            ["magincia"] = "Magincia",
            ["buccaneer"] = "Buccaneer's Den",
            ["jhelom"] = "Jhelom",
            ["papua"] = "Papua",
            ["serpent"] = "Serpent's Hold",
            // UOWW quadrant shorthands
            ["crossroads"] = "Crossroads",
            ["north"] = "North",
            ["south"] = "South",
            ["east"] = "East",
            ["west"] = "West",
            ["here"] = "current",
            ["this area"] = "current",
            ["this quadrant"] = "current",
        };
    }
}
