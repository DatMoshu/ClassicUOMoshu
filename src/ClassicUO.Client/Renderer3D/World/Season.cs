// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — World domain (ADR-012).

namespace ClassicUO.Renderer.WorldEnv
{
    /// <summary>
    /// Coarse season label derived from the continuous <see cref="ISeasonService.YearPhase"/>.
    /// Boundaries match the renderer's natural phase map (winter→spring at 0.05, spring→summer
    /// at 0.20, summer→autumn at 0.50, autumn→winter at 0.78).
    /// </summary>
    public enum Season
    {
        Winter = 0,
        Spring = 1,
        Summer = 2,
        Autumn = 3,
    }
}
