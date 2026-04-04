// SPDX-License-Identifier: BSD-2-Clause
// Implements: LLM response display panel for the -llm command
// Design Doc: VoiceInteractionManager LLM integration

using ClassicUO.Game;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;

namespace ClassicUO.SpeechRecognition.Gumps
{
    /// <summary>
    /// Draggable chat-style gump that displays the running conversation between
    /// the player and the LLM avatar. Opens on the first -llm command and
    /// accumulates messages via <see cref="AddMessage"/>.
    /// </summary>
    internal sealed class LlmResponseGump : Gump
    {
        private const uint LOCAL_SERIAL = 0xFAC00001u;

        private readonly ScrollArea _scroll;

        public LlmResponseGump(World world) : base(world, LOCAL_SERIAL, 0)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;

            Width = 420;
            Height = 500;

            // Semi-transparent black background
            Add(new AlphaBlendControl(200f / 255f)
            {
                X = 0,
                Y = 0,
                Width = 420,
                Height = 500
            });

            // Title label
            Add(new Label("Avatar", true, 0x0481, font: 3)
            {
                X = 20,
                Y = 10
            });

            // Close button — ButtonAction.Activate routes through OnButtonClick(0)
            var closeBtn = new NiceButton(390, 8, 20, 20, ButtonAction.Activate, "X")
            {
                ButtonParameter = 0
            };
            Add(closeBtn);

            // Scrollable message area
            _scroll = new ScrollArea(10, 40, 400, 450, true);
            Add(_scroll);
        }

        /// <summary>
        /// Appends a message line to the scroll area.
        /// </summary>
        /// <param name="role">"user" or "assistant"</param>
        /// <param name="text">Raw message text (prefix is added automatically)</param>
        public void AddMessage(string role, string text)
        {
            bool isUser = role == "user";
            ushort hue = isUser ? (ushort)0x03B2 : (ushort)0x0481;
            string prefix = isUser ? "You: " : "Avatar: ";

            var label = new Label(prefix + text, true, hue, maxwidth: 380, font: 3)
            {
                X = 0,
                Y = 0
            };

            _scroll.Add(label);
        }

        public override void OnButtonClick(int buttonID)
        {
            // Button 0 = close
            if (buttonID == 0)
                Dispose();
        }
    }
}
