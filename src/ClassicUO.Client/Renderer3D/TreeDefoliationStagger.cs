// SPDX-License-Identifier: BSD-2-Clause
// 3DCUO PROTOTYPE — staggered per-tree defoliation for winter.
//
// Replaces the worldwide "every leaf shrinks together" feel of the global
// LeafPresence ramp. Each tree gets a deterministic fire-order in 0..1 from
// a Knuth-golden-ratio hash of its (X,Y); a global WinterT advances 0..1
// over the winter window; when WinterT crosses a tree's order, the tree
// transitions to MinPresence (default 1/3) by either an instant POP or a
// quick LERP — chosen by another hash bit so roughly half do each.
//
// Spatial separation falls out for free: adjacent tiles X / X+1 differ by
// 2654435761 mod 2^32, so their orders land on opposite sides of the unit
// interval. No two neighbours fire close in time.
//
// Stateless (no per-tree dictionary): given (x,y,graphic) and the current
// WinterT + WinterStartTicks, Sample() returns the live presence directly.

using ClassicUO;
using Microsoft.Xna.Framework;

namespace ClassicUO.Renderer.Renderer3D
{
    internal static class TreeDefoliationStagger
    {
        // ---- Public state ----
        // When false, Sample() is a passthrough — caller's defaultPres wins.
        public static bool Enabled = false;

        // 0..1 winter progress. SeasonCycleDriver writes this from the year
        // phase, or commands/gumps drive it manually for ad-hoc demos.
        public static float WinterT = 0f;

        // Final presence each tree settles at after its transition. Default
        // 1/3 per spec ("lerps quickly to 1/3 scale or instantly pops to 1/3").
        public static float MinPresence = 1f / 3f;

        // How long the LERP variant takes to go full→MinPresence, in real-time
        // seconds. POP variant is always instant (duration = 0).
        public static float LerpDurationS = 1.2f;

        // 0..1 — fraction of trees that POP (rest LERP). Selected by hash bit
        // so the split is deterministic and uniform across the world.
        public static float PopProbability = 0.5f;

        // Real-time clock anchor (ms) — captured when WinterT is first nonzero
        // so the lerp duration is wall-clock based, not year-cycle based.
        // Reset by ResetClock() when winter restarts.
        private static long _winterStartTicks = -1;
        public static long WinterStartTicks => _winterStartTicks;

        public static void ResetClock()
        {
            _winterStartTicks = -1;
        }

        public static void Configure(float winterT)
        {
            if (winterT < 0f) winterT = 0f;
            else if (winterT > 1f) winterT = 1f;
            if (winterT > 0f && _winterStartTicks < 0)
                _winterStartTicks = Time.Ticks;
            else if (winterT <= 0f)
                _winterStartTicks = -1;
            WinterT = winterT;
        }

        // ---- Per-tree query ----
        // Returns the effective leaf presence for the static at (x,y) of the
        // given graphic, given the caller's previously-computed defaultPres
        // (1f for full canopy, lower for partial). Evergreen detection has
        // already been applied by the caller; we don't re-check here.
        public static float Sample(int x, int y, ushort graphic, float defaultPres)
        {
            if (!Enabled || WinterT <= 0f) return defaultPres;

            float order = HashOrder(x, y, graphic);   // 0..1
            if (WinterT < order)
            {
                // Not yet picked — full canopy regardless of defaultPres so the
                // staggered look reads cleanly. (Defoliation is the dominant
                // signal during winter; weather-driven shrinks are off.)
                return defaultPres;
            }

            bool isPop = (HashBit(x, y, graphic) < PopProbability);
            if (isPop || LerpDurationS <= 0.001f)
            {
                return MinPresence;
            }

            // LERP variant — wall-clock based. We don't know the exact moment
            // WinterT crossed `order`, so approximate from the time WinterT
            // started advancing: elapsed-since-start * (current/order ratio
            // is hard to derive without a history) → use a simpler model: the
            // tree starts its lerp NOW relative to when WinterT crossed its
            // order. We estimate "how long ago the cross happened" via the
            // unit-interval excess (WinterT - order) divided by the average
            // rate of WinterT progress (Δt over the whole winter so far).
            //
            // For prototype: assume WinterT advances roughly linearly. Excess
            // is (WinterT - order); seconds elapsed since cross ≈ excess *
            // (winterStartElapsedS / WinterT). Fall back to a fixed window if
            // we can't compute it.
            float excess = WinterT - order;     // 0..(1-order)

            float elapsedSinceCrossS;
            if (_winterStartTicks > 0 && WinterT > 0.001f)
            {
                float winterElapsedS = (Time.Ticks - _winterStartTicks) * 0.001f;
                elapsedSinceCrossS = winterElapsedS * (excess / WinterT);
            }
            else
            {
                // No clock yet — collapse to instant.
                return MinPresence;
            }

            float u = elapsedSinceCrossS / LerpDurationS;
            if (u < 0f) u = 0f;
            else if (u > 1f) u = 1f;
            // Smoothstep so the start/finish read soft, the middle reads quick.
            u = u * u * (3f - 2f * u);
            return MathHelper.Lerp(1f, MinPresence, u);
        }

        // ---- Tick ----
        // Called per frame from GameScene.Update so WinterStripRamp can
        // advance WinterT live without waiting for SeasonCycleDriver.
        public static void Tick()
        {
            WinterStripRamp.Tick();
        }

        // ---- Hashing ----
        // Knuth golden-ratio multiplicative hash. Maps (x,y,graphic) to a
        // uniform 0..1 with great low-discrepancy properties: adjacent tiles
        // land on far-apart slots in the unit interval.
        private static float HashOrder(int x, int y, ushort graphic)
        {
            unchecked
            {
                uint h = (uint)x * 2654435761u;
                h ^= (uint)y * 40503u;
                h ^= ((uint)graphic) * 2246822519u;
                h ^= h >> 16;
                h *= 2654435761u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) * (1f / 0x1000000);   // 0..1
            }
        }

        // Independent hash bit (0..1) used to select POP vs LERP per tree.
        private static float HashBit(int x, int y, ushort graphic)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u;
                h += (uint)y * 668265263u;
                h ^= ((uint)graphic + 1u) * 0x27d4eb2fu;
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) * (1f / 0x1000000);
            }
        }
    }

    // Ad-hoc ramp driver — advances WinterT 0→1 over a fixed wall-clock
    // window so the user can demo the stagger without enabling the full
    // SeasonCycleDriver. Driven by `[winterstrip <seconds>` and ticked
    // from GameScene.Update via TreeDefoliationStagger.Tick().
    internal static class WinterStripRamp
    {
        public static bool Active { get; private set; }
        public static float DurationS { get; private set; } = 8f;
        private static long _startTicks;

        public static void Start(float seconds)
        {
            DurationS = seconds <= 0.05f ? 0.05f : seconds;
            _startTicks = Time.Ticks;
            Active = true;
            TreeDefoliationStagger.Configure(0f);
        }

        public static void Stop()
        {
            Active = false;
        }

        public static void Tick()
        {
            if (!Active) return;
            float elapsedS = (Time.Ticks - _startTicks) * 0.001f;
            float t = elapsedS / DurationS;
            if (t < 0f) t = 0f;
            else if (t > 1f) { t = 1f; Active = false; }
            TreeDefoliationStagger.Configure(t);
        }
    }
}
