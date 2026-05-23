// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gateway exposing legacy <c>Static3DRenderer</c> core-config tunables. Read+write.
    /// State-of-record stays on the legacy class because the per-frame Draw() reads these
    /// in the hot path. Tree/leaf sub-config is a separate sub-domain (deferred).
    /// </summary>
    public interface IStatic3DConfigBridge
    {
        bool Enabled { get; set; }
        bool VerboseLog { get; set; }
        bool ClassifyStatics { get; set; }
        bool BillboardAllStatics { get; set; }
        bool BillboardItems { get; set; }
        int AlphaCutoff { get; set; }
        bool Use3DIris2Statics { get; set; }
        bool Iris2OverrideAll { get; set; }
    }
}
