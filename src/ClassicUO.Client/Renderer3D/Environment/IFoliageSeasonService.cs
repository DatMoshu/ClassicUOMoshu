// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Environment domain (ADR-012).

namespace ClassicUO.Renderer.Environment
{
    /// <summary>
    /// Gump/admin contract for the foliage seasonal-tint shader. Replaces direct reads/
    /// writes of <c>World3DRenderer.{FoliageSeason, FoliageSeasonIntensity}</c>.
    /// </summary>
    /// <remarks>
    /// State-of-record stays on the legacy <c>World3DRenderer</c> static class because the
    /// per-frame Static3DRenderer foliage pass reads these values in the hot path.
    /// </remarks>
    public interface IFoliageSeasonService
    {
        FoliageSeasonMode Mode { get; }
        float Intensity { get; }

        void SetMode(FoliageSeasonMode mode);
        void SetIntensity(float intensity);
    }
}
