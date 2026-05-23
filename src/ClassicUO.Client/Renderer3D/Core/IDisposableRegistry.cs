// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Tracks <see cref="IDisposable"/> resources owned by the renderer's service container
    /// so they can be released in reverse-registration order at shutdown. Closes review
    /// finding #5 (GPU resources never disposed: <c>BasicEffect</c>, <c>RasterizerState</c>,
    /// custom HLSL effects).
    /// </summary>
    public interface IDisposableRegistry : IDisposable
    {
        /// <summary>
        /// Register <paramref name="resource"/> for disposal at shutdown. Idempotent: registering
        /// the same instance twice has no effect. Returns the same instance for fluent use:
        /// <c>_effect = registry.Track(new BasicEffect(device));</c>
        /// </summary>
        T Track<T>(T resource) where T : IDisposable;
    }
}
