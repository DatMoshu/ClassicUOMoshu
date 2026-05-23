// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Gateway over the shared player-rig glTF model that the NPC anim driver writes
    /// pose state into. Production-side wraps <c>Player3DRenderer.Model</c>; tests use
    /// a no-op fake.
    /// </summary>
    /// <remarks>
    /// <b>Threading invariant (review finding #1):</b> the snapshot/restore pose dance is
    /// safe ONLY because the renderer pipeline runs on a single thread in MonoGame. If
    /// mobile updates are ever pushed off-thread, this gateway becomes a race. A future
    /// migration replaces the shared-rig pattern with stateless per-NPC pose evaluation.
    /// </remarks>
    public interface IAnimatedModel
    {
        /// <summary>True when the underlying rig is loaded and writable.</summary>
        bool Available { get; }

        /// <summary>Number of animations in the rig (0 when <see cref="Available"/> is false).</summary>
        int AnimationCount { get; }

        /// <summary>Active animation index. Out-of-range values produce undefined pose.</summary>
        int ActiveAnim { get; set; }

        /// <summary>Target animation index for blending; -1 disables blending.</summary>
        int TargetAnim { get; set; }

        /// <summary>Current animation playback time in seconds.</summary>
        float AnimTime { get; set; }

        /// <summary>Blend weight in [0,1].</summary>
        float BlendWeight { get; set; }

        /// <summary>Lookup an animation by name. Returns -1 when not found.</summary>
        int FindAnimByName(string name);

        /// <summary>Total duration of animation <paramref name="index"/> in seconds. 0 when out-of-range.</summary>
        float GetAnimationDuration(int index);

        /// <summary>
        /// Refresh skin matrices using the current <see cref="ActiveAnim"/>/<see cref="AnimTime"/>.
        /// Pass 0 to leave AnimTime unchanged.
        /// </summary>
        void Update(float dt);
    }
}
