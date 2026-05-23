// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Player animation state. Mirrors legacy <c>AnimState</c> bit-for-bit; numeric values
    /// locked by xUnit parity theory.
    /// </summary>
    public enum PlayerAnimState
    {
        Idle = 0,
        Run = 1,
        Hit = 2,
        Attack = 3,
        Walk = 4,
        Die = 5,
    }
}
