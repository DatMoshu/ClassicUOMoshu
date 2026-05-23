// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Typed publish/subscribe bus used for cross-system communication inside the renderer.
    /// Replaces direct static calls between subsystems (e.g., <c>FireSpreadSystem</c>
    /// reading <c>WindManager.VectorXZ</c>, or <c>Weather3DSystem</c> directly invoking
    /// <c>WeatherAudioSystem</c>).
    /// </summary>
    /// <remarks>
    /// Synchronous dispatch on the publishing thread. Event payload types must be immutable
    /// (record / readonly struct). AOT-safe: no reflection on event types — keying is by
    /// generic type parameter and resolved at call site.
    /// Subscribers receive a snapshot of the subscriber list at publish time, so unsubscribe
    /// during dispatch is safe and does not skip pending callbacks.
    /// </remarks>
    public interface IRendererEventBus
    {
        /// <summary>
        /// Register a handler for events of type <typeparamref name="TEvent"/>. Returns an
        /// <see cref="IDisposable"/> token; disposing the token unsubscribes the handler.
        /// </summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        /// <summary>
        /// Synchronously dispatch <paramref name="evt"/> to every subscriber of
        /// <typeparamref name="TEvent"/>. Allocation-free in the steady state.
        /// </summary>
        void Publish<TEvent>(in TEvent evt) where TEvent : struct;
    }
}
