// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Statics domain (ADR-012).

using System;

namespace ClassicUO.Renderer.Statics
{
    /// <summary>
    /// Pure-delegation implementation of <see cref="IStatic3DConfigService"/>.
    /// </summary>
    public sealed class Static3DConfigService : IStatic3DConfigService
    {
        private readonly IStatic3DConfigBridge _bridge;

        public Static3DConfigService(IStatic3DConfigBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool Enabled => _bridge.Enabled;
        public bool VerboseLog => _bridge.VerboseLog;
        public bool ClassifyStatics => _bridge.ClassifyStatics;
        public bool BillboardAllStatics => _bridge.BillboardAllStatics;
        public bool BillboardItems => _bridge.BillboardItems;
        public int AlphaCutoff => _bridge.AlphaCutoff;
        public bool Use3DIris2Statics => _bridge.Use3DIris2Statics;
        public bool Iris2OverrideAll => _bridge.Iris2OverrideAll;

        public void SetEnabled(bool value) => _bridge.Enabled = value;
        public void SetVerboseLog(bool value) => _bridge.VerboseLog = value;
        public void SetClassifyStatics(bool value) => _bridge.ClassifyStatics = value;
        public void SetBillboardAllStatics(bool value) => _bridge.BillboardAllStatics = value;
        public void SetBillboardItems(bool value) => _bridge.BillboardItems = value;
        public void SetAlphaCutoff(int value) => _bridge.AlphaCutoff = value;
        public void SetUse3DIris2Statics(bool value) => _bridge.Use3DIris2Statics = value;
        public void SetIris2OverrideAll(bool value) => _bridge.Iris2OverrideAll = value;
    }
}
