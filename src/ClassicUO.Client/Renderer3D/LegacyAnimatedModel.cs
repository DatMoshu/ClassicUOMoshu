// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — production adapter wrapping the legacy Player3DRenderer.Model rig
// behind IAnimatedModel.

using ClassicUO.Renderer.Mobiles;

namespace ClassicUO.Renderer.Renderer3D
{
    /// <summary>
    /// Production <see cref="IAnimatedModel"/> backed by <see cref="Player3DRenderer.Model"/>.
    /// All operations short-circuit when the model is null (Available=false).
    /// </summary>
    internal sealed class LegacyAnimatedModel : IAnimatedModel
    {
        public bool Available => Player3DRenderer.Model != null;

        public int AnimationCount => Player3DRenderer.Model?.Animations?.Length ?? 0;

        public int ActiveAnim
        {
            get => Player3DRenderer.Model?.ActiveAnim ?? 0;
            set { var m = Player3DRenderer.Model; if (m != null) m.ActiveAnim = value; }
        }

        public int TargetAnim
        {
            get => Player3DRenderer.Model?.TargetAnim ?? -1;
            set { var m = Player3DRenderer.Model; if (m != null) m.TargetAnim = value; }
        }

        public float AnimTime
        {
            get => Player3DRenderer.Model?.AnimTime ?? 0f;
            set { var m = Player3DRenderer.Model; if (m != null) m.AnimTime = value; }
        }

        public float BlendWeight
        {
            get => Player3DRenderer.Model?.BlendWeight ?? 0f;
            set { var m = Player3DRenderer.Model; if (m != null) m.BlendWeight = value; }
        }

        public int FindAnimByName(string name)
            => Player3DRenderer.Model?.FindAnimByName(name) ?? -1;

        public float GetAnimationDuration(int index)
        {
            var anims = Player3DRenderer.Model?.Animations;
            if (anims == null || index < 0 || index >= anims.Length) return 0f;
            return anims[index].Duration;
        }

        public void Update(float dt) => Player3DRenderer.Model?.Update(dt);
    }
}
