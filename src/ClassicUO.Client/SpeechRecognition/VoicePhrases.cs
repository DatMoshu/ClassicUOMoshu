// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Shared phrase sets used by both CommandRouter and ActionInferenceEngine.
    /// Single source of truth — prevents drift between the two routing paths.
    /// </summary>
    internal static class VoicePhrases
    {
        /// <summary>Phrases that confirm the top action while the inference HUD is showing.</summary>
        public static readonly HashSet<string> Confirm = new(StringComparer.OrdinalIgnoreCase)
        {
            "yes", "accept", "confirm", "do it", "go", "ok", "okay"
        };

        /// <summary>Phrases that cancel/dismiss the inference HUD.</summary>
        public static readonly HashSet<string> Cancel = new(StringComparer.OrdinalIgnoreCase)
        {
            "no", "cancel", "stop", "abort", "never mind", "nevermind"
        };

        /// <summary>Returns true if the text is a confirmation or cancellation phrase.</summary>
        public static bool IsConfirmOrCancel(string text)
            => Confirm.Contains(text) || Cancel.Contains(text);
    }
}
