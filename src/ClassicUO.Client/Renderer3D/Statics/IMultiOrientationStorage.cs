// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — IMultiOrientationService gateway (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Storage gateway for the orientation table. Implementations load the
    /// hand-authored + auto-derived JSON files and merge them with hand entries
    /// overriding auto entries on collision.
    /// </summary>
    public interface IMultiOrientationStorage
    {
        MultiOrientationLoadResult Load();
    }
}
