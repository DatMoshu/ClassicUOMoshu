using System;
using System.Collections.Generic;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.Managers;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using ClassicUO.Game;

namespace ClassicUO.SpeechRecognition.Gumps
{
    class SpeechAiGump : Gump
    {
        private const int MaxResponses = 10;
        private readonly List<string> _responses = new List<string>();
        //private readonly TextBox _inputTextBox;
        private readonly ScrollArea _scrollArea;
        private readonly Label _responseLabel;

        public SpeechAiGump(World world) : base(world, 0, 0)
        {
            Add(new GumpPic(0, 0, 0x0E14, 0)); // Background

            Add(new Label("Prompt:", true, 0x0386, font: 1)
            {
                X = 20,
                Y = 20
            });

            //_inputTextBox = new (1, 32, 200, 200, false, hue: 0x0386)
            //{
            //    X = 80,
            //    Y = 20,
            //    Width = 200
            //};
            //Add(_inputTextBox);

            Add(new Button(1, 0x0E14, 0x0E15, 0x0E16)
            {
                X = 300,
                Y = 20,
                ButtonAction = ButtonAction.Activate
            });

            _scrollArea = new ScrollArea(20, 60, 360, 200, true);
            Add(_scrollArea);

            _responseLabel = new Label(string.Empty, true, 0x0386, font: 1)
            {
                X = 0,
                Y = 0,
                Width = 340
            };
            _scrollArea.Add(_responseLabel);
        }

        public override void OnButtonClick(int buttonID)
        {
            //if (buttonID == 1)
            //{
            //    string prompt = _inputTextBox.Text;
            //    if (!string.IsNullOrWhiteSpace(prompt))
            //    {
            //        SendPrompt(prompt);
            //    }
            //}
        }

        private void SendPrompt(string prompt)
        {
            // Simulate sending the prompt to the LLM and receiving a response
            string response = $"Response to: {prompt}";

            if (_responses.Count >= MaxResponses)
            {
                _responses.RemoveAt(0);
            }

            _responses.Add(response);
            UpdateResponseLabel();
        }

        private void UpdateResponseLabel()
        {
            _responseLabel.Text = string.Join(Environment.NewLine, _responses);
            //_scrollArea.ScrollToBottom();
        }
    }
}
