// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Default <see cref="IRendererEventBus"/> implementation. Per-event-type subscriber
    /// lists keyed by generic type. Snapshot-on-publish so handlers may safely subscribe
    /// or unsubscribe during dispatch.
    /// </summary>
    public sealed class RendererEventBus : IRendererEventBus
    {
        private static class Channel<TEvent> where TEvent : struct
        {
            // One static list per RendererEventBus instance is not possible without an instance
            // identity. We instead store a per-bus dictionary entry; this static class is only
            // used for the keyed accessor below.
        }

        // Map of event-type -> list of Action<TEvent> boxed as object. The cast is safe because
        // we only ever insert delegates of the matching type via Subscribe<TEvent>.
        private readonly Dictionary<Type, List<object>> _subscribers = new();
        private readonly object _lock = new();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler is null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                if (!_subscribers.TryGetValue(typeof(TEvent), out var list))
                {
                    list = new List<object>(capacity: 4);
                    _subscribers[typeof(TEvent)] = list;
                }
                list.Add(handler);
            }
            return new Subscription<TEvent>(this, handler);
        }

        public void Publish<TEvent>(in TEvent evt) where TEvent : struct
        {
            // Snapshot the subscriber list so handlers may subscribe/unsubscribe during dispatch.
            // We copy into a stack-allocated span when small, falling back to a pooled array for
            // larger fan-outs. For typical renderer event counts (<8 subscribers) this is one
            // small allocation per publish; we accept that for correctness over a lock-free path.
            object[] snapshot;
            int count;
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
                    return;
                count = list.Count;
                snapshot = list.ToArray();
            }

            for (int i = 0; i < count; i++)
            {
                ((Action<TEvent>)snapshot[i])(evt);
            }
        }

        private void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            lock (_lock)
            {
                if (_subscribers.TryGetValue(typeof(TEvent), out var list))
                {
                    list.Remove(handler);
                }
            }
        }

        private sealed class Subscription<TEvent> : IDisposable where TEvent : struct
        {
            private RendererEventBus _bus;
            private Action<TEvent> _handler;

            public Subscription(RendererEventBus bus, Action<TEvent> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_bus is null) return;
                _bus.Unsubscribe(_handler);
                _bus = null;
                _handler = null;
            }
        }
    }
}
