// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter (ADR-012 §6).
// Bridges the legacy Static3DRenderer core-config statics to IStatic3DConfigBridge.

using ClassicUO.Renderer.Statics;

namespace ClassicUO.Renderer.Renderer3D
{
    internal sealed class LegacyStatic3DConfigBridge : IStatic3DConfigBridge
    {
        public bool Enabled
        {
            get => Static3DRenderer.Enabled;
            set => Static3DRenderer.Enabled = value;
        }

        public bool VerboseLog
        {
            get => Static3DRenderer.VerboseLog;
            set => Static3DRenderer.VerboseLog = value;
        }

        public bool ClassifyStatics
        {
            get => Static3DRenderer.ClassifyStatics;
            set => Static3DRenderer.ClassifyStatics = value;
        }

        public bool BillboardAllStatics
        {
            get => Static3DRenderer.BillboardAllStatics;
            set => Static3DRenderer.BillboardAllStatics = value;
        }

        public bool BillboardItems
        {
            get => Static3DRenderer.BillboardItems;
            set => Static3DRenderer.BillboardItems = value;
        }

        public int AlphaCutoff
        {
            get => Static3DRenderer.AlphaCutoff;
            set => Static3DRenderer.AlphaCutoff = value;
        }

        public bool Use3DIris2Statics
        {
            get => Static3DRenderer.Use3DIris2Statics;
            set => Static3DRenderer.Use3DIris2Statics = value;
        }

        public bool Iris2OverrideAll
        {
            get => Static3DRenderer.Iris2OverrideAll;
            set => Static3DRenderer.Iris2OverrideAll = value;
        }
    }
}
