// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;

namespace ClassicUO.SpeechRecognition.Inference
{
    /// <summary>Ranked inference result returned by TokenScorer or LlmScorer.</summary>
    internal sealed class InferenceResult
    {
        public static readonly InferenceResult Empty = new InferenceResult(string.Empty, System.Array.Empty<InferenceAction>());

        /// <summary>The original transcript that was scored.</summary>
        public string Transcript { get; }

        /// <summary>Up to 3 ranked actions, highest confidence first.</summary>
        public IReadOnlyList<InferenceAction> Actions { get; }

        public bool HasResults => Actions.Count > 0 && !string.IsNullOrEmpty(Actions[0].Command);

        public InferenceResult(string transcript, IReadOnlyList<InferenceAction> actions)
        {
            Transcript = transcript;
            Actions = actions;
        }
    }

    /// <summary>A single ranked action within an InferenceResult.</summary>
    internal sealed class InferenceAction
    {
        public string Command { get; init; }
        public string Label { get; init; }
        public float Confidence { get; init; }
        public bool IsEager { get; init; }

        public static readonly InferenceAction NoMatch = new InferenceAction
        {
            Command = string.Empty,
            Label = "No match",
            Confidence = 0f,
            IsEager = false
        };
    }
}
