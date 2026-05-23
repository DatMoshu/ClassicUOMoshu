// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Default <see cref="IDisposableRegistry"/>. Tracks resources in a list and disposes
    /// them in reverse-registration order on <see cref="Dispose"/>. Disposal exceptions
    /// are caught and accumulated so a single misbehaving resource does not abort the chain.
    /// </summary>
    public sealed class DisposableRegistry : IDisposableRegistry
    {
        private readonly List<IDisposable> _resources = new(capacity: 32);
        private readonly HashSet<IDisposable> _seen = new();
        private bool _disposed;

        public T Track<T>(T resource) where T : IDisposable
        {
            if (resource is null) throw new ArgumentNullException(nameof(resource));
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableRegistry));

            if (_seen.Add(resource))
                _resources.Add(resource);
            return resource;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            List<Exception> errors = null;
            for (int i = _resources.Count - 1; i >= 0; i--)
            {
                try
                {
                    _resources[i].Dispose();
                }
                catch (Exception ex)
                {
                    (errors ??= new List<Exception>()).Add(ex);
                }
            }
            _resources.Clear();
            _seen.Clear();

            if (errors is { Count: > 0 })
                throw new AggregateException("One or more renderer resources failed to dispose.", errors);
        }
    }
}
