// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — xUnit collection definition for the headless GraphicsDevice fixture.

using Xunit;

namespace ClassicUO.Renderer3D.Tests
{
    /// <summary>
    /// xUnit collection that shares one <see cref="HeadlessGraphicsFixture"/> across
    /// every test class marked <c>[Collection("GraphicsDevice")]</c>. One FNA boot
    /// per test session — fixture construction is expensive (~1–2s warm) so we don't
    /// want it per-class.
    /// </summary>
    [CollectionDefinition("GraphicsDevice")]
    public sealed class GraphicsDeviceCollection : ICollectionFixture<HeadlessGraphicsFixture>
    {
        // Marker only — xUnit discovers collection fixtures through the interface.
    }
}
