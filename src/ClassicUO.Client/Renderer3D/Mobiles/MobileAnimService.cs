// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — Mobiles domain (ADR-012).
//
// Per-NPC animation state driver. Each tick advances NowSec, runs periodic stale-entry
// eviction, and updates per-NPC motion-detection. ApplyNpcPose writes Walk/Idle pose
// into the shared rig via IAnimatedModel; caller wraps per-NPC draws in
// SnapshotPose/RestorePose to prevent the player's pose from being clobbered.

using System;
using System.Collections.Generic;
using ClassicUO.Renderer.Core;

namespace ClassicUO.Renderer.Mobiles
{
    /// <summary>
    /// Production implementation of <see cref="IMobileAnimService"/>. LRU+TTL eviction
    /// bounds memory; allocation-free in steady state apart from new-NPC state allocation
    /// and the rare hard-cap fallback sort.
    /// </summary>
    public sealed class MobileAnimService : IMobileAnimService, IFrameService
    {
        private readonly MobileAnimServiceConfig _config;
        private readonly IAnimatedModel _model;
        private readonly Dictionary<uint, MobileAnimState> _state = new();
        private readonly List<uint> _evictionScratch = new();
        private float _nowSec;

        public MobileAnimService(MobileAnimServiceConfig config, IAnimatedModel model)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public int CachedCount => _state.Count;
        public float NowSec => _nowSec;

        public void Drop(uint serial) => _state.Remove(serial);
        public void Clear() => _state.Clear();

        // ===== IFrameService =====

        public void Tick(in FrameTickContext ctx)
        {
            _nowSec += ctx.DeltaSeconds;
            if ((ctx.FrameNumber % _config.EvictionFrameInterval) == 0)
                EvictStale();
        }

        // ===== Motion detection =====

        public void UpdateMotion(uint serial, float x, float y, float z)
        {
            if (!_state.TryGetValue(serial, out MobileAnimState st))
            {
                st = new MobileAnimState
                {
                    LastUoX = x, LastUoY = y, LastUoZ = z,
                    LastMoveTimeSec = float.NegativeInfinity,
                    LastSeenSec = _nowSec,
                };
                _state[serial] = st;
            }
            else
            {
                st.LastSeenSec = _nowSec;
            }

            float dx = x - st.LastUoX;
            float dy = y - st.LastUoY;
            float dz = z - st.LastUoZ;
            bool moved = (dx * dx + dy * dy + dz * dz) > _config.MotionDeltaSqEpsilon;
            if (moved)
            {
                st.LastUoX = x; st.LastUoY = y; st.LastUoZ = z;
                st.LastMoveTimeSec = _nowSec;
            }
        }

        // ===== Pose snapshot/restore =====

        public ModelPoseSnapshot SnapshotPose()
        {
            if (!_model.Available) return default;
            return new ModelPoseSnapshot
            {
                ActiveAnim = _model.ActiveAnim,
                TargetAnim = _model.TargetAnim,
                AnimTime = _model.AnimTime,
                BlendWeight = _model.BlendWeight,
                Valid = true,
            };
        }

        public void RestorePose(ModelPoseSnapshot s)
        {
            if (!s.Valid || !_model.Available) return;
            _model.ActiveAnim = s.ActiveAnim;
            _model.TargetAnim = s.TargetAnim;
            _model.AnimTime = s.AnimTime;
            _model.BlendWeight = s.BlendWeight;
            _model.Update(0f);
        }

        public void ApplyNpcPose(uint serial, float dt)
        {
            if (!_model.Available || _model.AnimationCount == 0) return;
            if (!_state.TryGetValue(serial, out MobileAnimState st)) return;

            bool walking = (_nowSec - st.LastMoveTimeSec) < _config.WalkingMotionThresholdSeconds;
            if (st.PrevWalking != walking) st.AnimTime = 0f;
            st.PrevWalking = walking;

            int desired = _model.FindAnimByName(walking ? "Walk" : "Idle");
            if (desired < 0 || desired >= _model.AnimationCount) desired = 0;
            float duration = _model.GetAnimationDuration(desired);
            if (duration > 0f) st.AnimTime = (st.AnimTime + dt) % duration;

            _model.ActiveAnim = desired;
            _model.AnimTime = st.AnimTime;
            _model.TargetAnim = -1;
            _model.BlendWeight = 0f;
            _model.Update(0f);
        }

        public bool IsWalking(uint serial)
            => _state.TryGetValue(serial, out MobileAnimState st)
               && (_nowSec - st.LastMoveTimeSec) < _config.WalkingMotionThresholdSeconds;

        // ===== Eviction =====

        private void EvictStale()
        {
            float threshold = _nowSec - _config.StaleEntrySeconds;
            _evictionScratch.Clear();
            foreach (KeyValuePair<uint, MobileAnimState> kvp in _state)
            {
                if (kvp.Value.LastSeenSec < threshold)
                    _evictionScratch.Add(kvp.Key);
            }
            for (int i = 0; i < _evictionScratch.Count; i++)
                _state.Remove(_evictionScratch[i]);
            _evictionScratch.Clear();

            if (_state.Count <= _config.MaxTrackedEntries) return;
            EvictHardCap();
        }

        private void EvictHardCap()
        {
            // Hard cap fallback: collect serials with their LastSeenSec, sort ascending,
            // evict until under cap. Allocates only here (rare).
            var ordered = new List<KeyValuePair<uint, float>>(_state.Count);
            foreach (KeyValuePair<uint, MobileAnimState> kvp in _state)
                ordered.Add(new KeyValuePair<uint, float>(kvp.Key, kvp.Value.LastSeenSec));
            ordered.Sort(static (a, b) => a.Value.CompareTo(b.Value));

            int toEvict = _state.Count - _config.MaxTrackedEntries;
            for (int i = 0; i < toEvict; i++)
                _state.Remove(ordered[i].Key);
        }
    }

    /// <summary>Per-NPC anim state. Mutable; allocated once per first-seen mobile.</summary>
    internal sealed class MobileAnimState
    {
        public float AnimTime;
        public float LastUoX, LastUoY, LastUoZ;
        public float LastMoveTimeSec;
        public float LastSeenSec;
        public bool PrevWalking;
    }
}
