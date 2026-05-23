// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Per-NPC animation state driver. Tracks each mobile's motion, picks Walk vs Idle
    /// based on recent motion, and writes pose into the shared rig via
    /// <see cref="IAnimatedModel"/>. Caller is responsible for snapshot/restore around
    /// the per-NPC draw (see <see cref="SnapshotPose"/>/<see cref="RestorePose"/>).
    /// </summary>
    public interface IMobileAnimService
    {
        int CachedCount { get; }

        /// <summary>Accumulated wall-clock seconds the service has been ticking.</summary>
        float NowSec { get; }

        /// <summary>Drop the cached state for a single NPC. Idempotent.</summary>
        void Drop(uint serial);

        /// <summary>Drop all cached state.</summary>
        void Clear();

        /// <summary>
        /// Update the motion-detection state for an NPC at world-tile coords
        /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>). Records the
        /// LastSeen timestamp (used for eviction).
        /// </summary>
        void UpdateMotion(uint serial, float x, float y, float z);

        /// <summary>
        /// Snapshot the rig's pose so the caller can restore it after the next NPC draw.
        /// Cheap struct copy. Returns an invalid snapshot when the rig is not loaded.
        /// </summary>
        ModelPoseSnapshot SnapshotPose();

        /// <summary>Restore a snapshot taken by <see cref="SnapshotPose"/>. No-op when invalid.</summary>
        void RestorePose(ModelPoseSnapshot snapshot);

        /// <summary>
        /// Apply this NPC's anim state (Walk/Idle) to the shared rig. Caller must have
        /// called <see cref="SnapshotPose"/> first and will restore after the draw.
        /// <paramref name="dt"/> advances the per-NPC anim time; pass 0 to freeze.
        /// </summary>
        void ApplyNpcPose(uint serial, float dt);

        /// <summary>Diagnostic — is this NPC walking right now?</summary>
        bool IsWalking(uint serial);
    }

    /// <summary>
    /// Cheap copyable snapshot of the shared rig's pose state. Used to restore after
    /// an NPC draw so the player's next draw doesn't see leftover NPC pose.
    /// </summary>
    public struct ModelPoseSnapshot
    {
        public int ActiveAnim;
        public int TargetAnim;
        public float AnimTime;
        public float BlendWeight;
        public bool Valid;
    }
}
