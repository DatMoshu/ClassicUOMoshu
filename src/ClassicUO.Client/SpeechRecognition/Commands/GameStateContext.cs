// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using Microsoft.Xna.Framework;

namespace ClassicUO.SpeechRecognition.Commands
{
    /// <summary>
    /// Snapshot of game state captured when a voice command is recognized.
    /// Used by IntentParser and UowwCommandMap to provide context-aware commands.
    /// </summary>
    internal sealed class GameStateContext
    {
        public int PlayerX { get; init; }
        public int PlayerY { get; init; }
        public int PlayerZ { get; init; }
        public int MapIndex { get; init; }
        public string PlayerName { get; init; }

        /// <summary>Nearest mobiles within 15 tiles with their names and distances.</summary>
        public IReadOnlyList<(string Name, int Distance, bool IsHostile)> NearbyMobiles { get; init; }

        /// <summary>Items near the player that are interactable (NPCs, objects).</summary>
        public IReadOnlyList<(string Name, int Distance)> NearbyItems { get; init; }

        /// <summary>True when the player is on the war map (facet 1).</summary>
        public bool IsOnWarMap => MapIndex == 1;

        public static GameStateContext Capture(World world)
        {
            if (world?.Player == null)
                return new GameStateContext { NearbyMobiles = Array.Empty<(string, int, bool)>(), NearbyItems = Array.Empty<(string, int)>() };

            var mobs = new List<(string, int, bool)>();
            foreach (var mobile in world.Mobiles.Values)
            {
                if (mobile.Distance <= 15 && mobile.Serial != world.Player.Serial)
                    mobs.Add((mobile.Name ?? "Unknown", mobile.Distance, SpeechRecognitionManager.IsHostile(mobile)));
            }
            mobs.Sort((a, b) => a.Item2.CompareTo(b.Item2));

            var items = new List<(string, int)>();
            foreach (var item in world.Items.Values)
            {
                if (item.Distance <= 10 && !string.IsNullOrEmpty(item.Name))
                    items.Add((item.Name, item.Distance));
            }

            return new GameStateContext
            {
                PlayerX = world.Player.X,
                PlayerY = world.Player.Y,
                PlayerZ = world.Player.Z,
                MapIndex = world.MapIndex,
                PlayerName = world.Player.Name ?? string.Empty,
                NearbyMobiles = mobs,
                NearbyItems = items,
            };
        }
    }
}
