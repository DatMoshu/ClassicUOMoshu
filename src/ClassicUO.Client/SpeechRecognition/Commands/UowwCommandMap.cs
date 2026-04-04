// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading.Tasks;
using ClassicUO.Game;

namespace ClassicUO.SpeechRecognition.Commands
{
    /// <summary>
    /// Maps parsed intents to UOWW bracket commands and executes them via GameActions.Say().
    ///
    /// All UOWW commands use ModernUO's [CommandName args] format.
    /// </summary>
    internal sealed class UowwCommandMap
    {
        private readonly World _world;

        public UowwCommandMap(World world)
        {
            _world = world;
        }

        /// <summary>
        /// Execute a parsed intent as a UOWW server command.
        /// Returns true if the intent was recognized and dispatched.
        /// </summary>
        public bool Execute(ParsedIntent intent, GameStateContext ctx)
        {
            if (intent == null || intent.IntentName == "none") return false;

            switch (intent.IntentName)
            {
                case "territory_query":
                    if (intent.Argument == "current" || string.IsNullOrEmpty(intent.Argument))
                        Say("[qi");
                    else
                        Say($"[quadrantinfo {intent.Argument}");
                    return true;

                case "faction_info":
                    Say("[factioninfo");
                    return true;

                case "warmap":
                    Say("[warmap");
                    return true;

                case "army_dispatch":
                {
                    string unit = intent.Argument ?? "infantry_standard";
                    string dest = intent.SecondaryArg ?? "here";
                    Say(dest == "current" || dest == "here"
                        ? $"[DispatchSquad {unit}"
                        : $"[DispatchSquad {unit} {dest}");
                    return true;
                }

                case "army_status":
                    Say("[ArmyStatus");
                    return true;

                case "nearby_enemies":
                    // Announce hostiles from context
                    AnnounceNearbyEnemies(ctx);
                    return true;

                case "voice_channel":
                    Say($"[voice {intent.Argument}");
                    return true;

                default:
                    return false;
            }
        }

        private void AnnounceNearbyEnemies(GameStateContext ctx)
        {
            if (ctx == null) return;

            int hostileCount = 0;
            foreach (var (name, dist, isHostile) in ctx.NearbyMobiles)
            {
                if (isHostile) hostileCount++;
            }

            if (hostileCount == 0)
                Say("No hostiles detected nearby.");
            else
                Say($"{hostileCount} hostile{(hostileCount > 1 ? "s" : "")} nearby.");
        }

        private void Say(string command)
        {
            if (_world?.Player != null)
                GameActions.Say(command);
        }
    }
}
