// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wiring ILightingProfileGateway to the legacy
// ProfileManager static. Lives outside the Atmosphere folder because it depends on
// concrete ClassicUO.Configuration types; the service-side interface stays decoupled
// from those types so tests don't need to reference them.

using ClassicUO.Configuration;
using ClassicUO.Renderer.Atmosphere;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="ILightingProfileGateway"/> backed by
    /// <see cref="ClassicUO.Configuration.ProfileManager.CurrentProfile"/>.
    /// </summary>
    internal sealed class ProfileManagerLightingGateway : ILightingProfileGateway
    {
        public bool HasActiveProfile => ProfileManager.CurrentProfile != null;

        public bool ReadEnabled() => ProfileManager.CurrentProfile.RealtimeLightingEnabled;
        public bool ReadAutoCycle() => ProfileManager.CurrentProfile.SunCycleAuto;
        public float ReadTimeOfDay() => ProfileManager.CurrentProfile.SunTimeOfDay;
        public float ReadCyclePeriodSeconds() => ProfileManager.CurrentProfile.SunCyclePeriodSeconds;

        public void WriteEnabled(bool enabled)
        {
            var p = ProfileManager.CurrentProfile;
            if (p != null) p.RealtimeLightingEnabled = enabled;
        }

        public void WriteAutoCycle(bool autoCycle)
        {
            var p = ProfileManager.CurrentProfile;
            if (p != null) p.SunCycleAuto = autoCycle;
        }

        public void WriteTimeOfDay(float timeOfDay)
        {
            var p = ProfileManager.CurrentProfile;
            if (p != null) p.SunTimeOfDay = timeOfDay;
        }

        public void WriteCyclePeriodSeconds(float seconds)
        {
            var p = ProfileManager.CurrentProfile;
            if (p != null) p.SunCyclePeriodSeconds = seconds;
        }
    }
}
