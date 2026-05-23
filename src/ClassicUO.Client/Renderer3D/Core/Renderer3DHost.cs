// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Single static reference to the active <see cref="Renderer3DServices"/> instance.
    /// Exists solely as a migration scaffold (ADR-012 §6) so legacy static subsystems and
    /// dynamically-instantiated gumps can reach the service container while their callers
    /// migrate to constructor-injected services.
    /// </summary>
    /// <remarks>
    /// <para><b>Scheduled for deletion in ADR-012 Phase 3.</b> Do not use this from any new code.
    /// New services receive their dependencies via <see cref="Renderer3DServices"/> at composition
    /// time; new gumps receive the container through their constructor.</para>
    ///
    /// <para>This is the only permitted static reference to renderer services. Any other
    /// service-locator pattern is forbidden by ADR-012 §4.</para>
    /// </remarks>
    public static class Renderer3DHost
    {
        private static Renderer3DServices _services;

        /// <summary>
        /// The active services container. Throws if accessed before <see cref="Bind"/>;
        /// this is intentional — null-tolerant access would mask wiring bugs.
        /// </summary>
        public static Renderer3DServices Services
            => _services ?? throw new InvalidOperationException(
                "Renderer3DHost.Services accessed before Bind(). Ensure Renderer3DServices is constructed and Bind()ed at GameScene initialisation.");

        /// <summary>True after <see cref="Bind"/> has been called and before <see cref="Unbind"/>.</summary>
        public static bool IsBound => _services is not null;

        /// <summary>
        /// Install the active services container. Called exactly once during renderer startup
        /// (typically from <c>GameScene.Load</c>). Throws if already bound.
        /// </summary>
        public static void Bind(Renderer3DServices services)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (_services is not null)
                throw new InvalidOperationException("Renderer3DHost is already bound. Unbind() before re-binding.");
            _services = services;
        }

        /// <summary>Clear the host reference. Called from renderer shutdown.</summary>
        public static void Unbind()
        {
            _services = null;
        }
    }
}
