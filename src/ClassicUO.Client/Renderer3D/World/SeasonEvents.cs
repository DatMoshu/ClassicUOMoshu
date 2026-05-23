// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Published by <see cref="ISeasonService"/> when the coarse <see cref="Season"/> changes
    /// (transition through phase boundaries) or when the user calls <see cref="ISeasonService.SnapTo"/>
    /// across a boundary. Subscribers: tree foliage tint, ground material, ambient audio.
    /// </summary>
    public readonly struct SeasonChangedEvent
    {
        /// <summary>Season the cycle is now in.</summary>
        public readonly Season Current;

        /// <summary>Season the cycle was in immediately before this transition.</summary>
        public readonly Season Previous;

        /// <summary>Continuous year phase in [0,1) at the moment of transition.</summary>
        public readonly float YearPhase;

        public SeasonChangedEvent(Season current, Season previous, float yearPhase)
        {
            Current = current;
            Previous = previous;
            YearPhase = yearPhase;
        }
    }
}
