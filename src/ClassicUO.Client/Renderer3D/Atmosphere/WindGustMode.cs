// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Atmosphere domain (ADR-012).

namespace ClassicUO.Renderer.Atmosphere
{
    /// <summary>
    /// Wind gust behaviour selector. Drives the auto-target picker in <see cref="WindService"/>.
    /// </summary>
    public enum WindGustMode
    {
        /// <summary>No auto-gusts. Strength and direction held at user values; only the sine phase breathes.</summary>
        None = 0,

        /// <summary>Semantic alias for <see cref="None"/>; useful in UI to distinguish "off" from "explicitly steady".</summary>
        Steady = 1,

        /// <summary>Periodically retarget strength and direction within configured ranges; gentle eased lerp.</summary>
        Variable = 2,

        /// <summary>Like Variable but tighter cadence and stronger peaks; foliage and particles get yanked.</summary>
        Storm = 3,
    }
}
