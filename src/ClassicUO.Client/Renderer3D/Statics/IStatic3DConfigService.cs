// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Gump/admin contract for the 3D static-renderer's core toggles. Replaces direct
    /// reads/writes of <c>Static3DRenderer.{Enabled, BillboardAllStatics, AlphaCutoff, ...}</c>.
    /// Tree-/leaf-specific config is a separate sub-domain (deferred to follow-up).
    /// </summary>
    public interface IStatic3DConfigService
    {
        bool Enabled { get; }
        bool VerboseLog { get; }
        bool ClassifyStatics { get; }
        bool BillboardAllStatics { get; }
        bool BillboardItems { get; }
        int AlphaCutoff { get; }
        bool Use3DIris2Statics { get; }
        bool Iris2OverrideAll { get; }

        void SetEnabled(bool value);
        void SetVerboseLog(bool value);
        void SetClassifyStatics(bool value);
        void SetBillboardAllStatics(bool value);
        void SetBillboardItems(bool value);
        void SetAlphaCutoff(int value);
        void SetUse3DIris2Statics(bool value);
        void SetIris2OverrideAll(bool value);
    }
}
