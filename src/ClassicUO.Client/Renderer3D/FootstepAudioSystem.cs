// SPDX-License-Identifier: BSD-2-Clause
// ClassicUO — TRANSITIONAL FACADE (ADR-012 §6).
//
// Legacy FootstepAudioSystem delegated to IFootstepAudioService via Renderer3DHost.
// Scheduled for deletion in ADR-012 Phase 3.

using System;
using ClassicUO.Renderer.Audio;
using ClassicUO.Renderer.Core;
// Legacy facade had top-level FootstepMaterial / FootwearKind. Alias the canonical
// (Audio) types so unqualified references in this namespace resolve to the new domain
// enums (the gump and Mobile.cs reference these names).
using FootstepMaterial = ClassicUO.Renderer.Audio.FootstepMaterial;
using FootwearKind = ClassicUO.Renderer.Audio.FootwearKind;

namespace ClassicUO.Renderer.Renderer3D
{
    [Obsolete("Use IFootstepAudioService via Renderer3DServices. Will be removed in ADR-012 Phase 3.")]
    internal static class FootstepAudioSystem
    {
        private static IFootstepAudioService Svc => Renderer3DHost.Services.FootstepAudio;

        public static bool Enabled
        {
            get => Svc.Enabled;
            set => Svc.SetEnabled(value);
        }

        public static float Volume
        {
            get => Svc.Volume;
            set => Svc.SetVolume(value);
        }

        // Legacy mirror — service has its own VerboseLog config but no setter; gump references
        // this for a checkbox. Kept as field; mutations have no runtime effect.
        public static bool VerboseLog;

        public static bool AutoUseSnowInWinter
        {
            get => Svc.AutoUseSnowInWinter;
            set => Svc.SetAutoUseSnowInWinter(value);
        }

        public static FootstepMaterial OverrideMaterial
        {
            get => Svc.OverrideMaterial;
            set => Svc.SetOverrideMaterial(value);
        }

        public static FootstepMaterial DefaultMaterial
        {
            get => Svc.DefaultMaterial;
            set => Svc.SetDefaultMaterial(value);
        }

        public static FootwearKind Footwear
        {
            get => Svc.Footwear;
            set => Svc.SetFootwear(value);
        }

        // Legacy mirror — service has Variants in config; not a runtime setter.
        public static int Variants = 5;

        public static int LastPlayCount => Svc.LastPlayCount;
        public static FootstepMaterial LastMaterial => Svc.LastMaterial;

        public static bool TryPlayStep(int x, int y, bool running, bool mounted)
            => Svc.TryPlayStep(x, y, running, mounted);

        public static FootstepMaterial ResolveMaterial() => Svc.ResolveMaterial();
    }
}
