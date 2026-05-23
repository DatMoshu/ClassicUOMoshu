// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Renderer3D Core (ADR-012).

using System;
using System.Collections.Generic;

namespace ClassicUO.Renderer.Core
{
    /// <summary>
    /// Owns the ordered set of <see cref="IRenderPass"/> instances and executes them per
    /// frame. Replaces the monolithic <c>World3DRenderer.Draw()</c> method (ADR-012 §2).
    /// </summary>
    internal sealed class RenderPassPipeline
    {
        private readonly List<IRenderPass> _passes = new(capacity: 16);
        private bool _frozen;

        /// <summary>
        /// Register a pass. May only be called before the first call to <see cref="Execute"/>;
        /// after that the pipeline is frozen. This keeps the per-frame execute path branch-free.
        /// </summary>
        public void Register(IRenderPass pass)
        {
            if (pass is null) throw new ArgumentNullException(nameof(pass));
            if (_frozen) throw new InvalidOperationException("RenderPassPipeline is frozen; register all passes during composition.");

            _passes.Add(pass);
        }

        /// <summary>
        /// Sort passes by <see cref="IRenderPass.Order"/> and freeze the pipeline. Called once
        /// by the composition root after all passes are registered.
        /// </summary>
        public void Freeze()
        {
            if (_frozen) return;
            _passes.Sort(static (a, b) => a.Order.CompareTo(b.Order));
            _frozen = true;
        }

        /// <summary>
        /// Execute every enabled pass in order. The pipeline does not normalise GPU state
        /// between passes; each pass is responsible for its own state hygiene.
        /// </summary>
        public void Execute(in RenderPassContext ctx)
        {
            if (!_frozen) Freeze();

            // Plain for-loop avoids enumerator allocation. Snapshot count once — passes
            // cannot register mid-frame (pipeline is frozen).
            int count = _passes.Count;
            for (int i = 0; i < count; i++)
            {
                IRenderPass pass = _passes[i];
                if (pass.IsEnabled)
                    pass.Execute(in ctx);
            }
        }

        /// <summary>Number of registered passes (post-freeze includes all). For diagnostics.</summary>
        public int Count => _passes.Count;
    }
}
