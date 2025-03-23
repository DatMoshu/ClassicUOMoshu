// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Speech.Synthesis.TtsEngine;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.SpeechRecognition;
using NAudio.Wave;
using Newtonsoft.Json;
using Vosk;
using static System.Net.Mime.MediaTypeNames;

namespace ClassicUO.Game
{
    internal sealed class SpeechRecognitionManager
    {
        private const string SpeechCommandsFile = "speechcommands.json";
        private List<SpeechCommandInfo> _speechCommands = new List<SpeechCommandInfo>();
        private VoskRecognizer _recognizer;
        private WaveInEvent _waveIn;
        private World _world;
        private static readonly Random _random = new Random();
        private TextObject _overheadMessage;

        public void Initialize(string modelPath, float sampleRate, World world)
        {
            Vosk.Vosk.SetLogLevel(0);
            var model = new Model(modelPath);
            _recognizer = new VoskRecognizer(model, sampleRate);
            _recognizer.SetMaxAlternatives(5);
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat((int)sampleRate, 1),
                BufferMilliseconds = 5
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _world = world;

            if (File.Exists(GetSpeechCommandFilepath()))
            {
                string commandsJson = File.ReadAllText(GetSpeechCommandFilepath());
                _speechCommands = JsonConvert.DeserializeObject<List<SpeechCommandInfo>>(commandsJson);
            }

            // Initialize the overhead message
            InitializeOverheadMessage();
        }

        private void InitializeOverheadMessage()
        {
            if (_world.Player != null)
            {
                //_overheadMessage = new TextObject
                //{
                //    Items = new List<string>() { "[Listening]" },
                //    Text = "[Listening]",
                //    Hue = 0x35,
                //    Font = 3,
                //    IsUnicode = true,
                //    Type = TextType.OBJECT,
                //    TimeToLive = Time.Ticks + 1000000 // Set a long duration
                //};
                //_overheadMessage = TextObject.Create(_world);
                //_overheadMessage.Time = Time.Ticks + 1000000;
                //_overheadMessage.Hue = 0x35;
                //_world.WorldTextManager.AddMessage(_overheadMessage);
                //_world.Player.AddMessage(_overheadMessage);
            }
        }

        private void UpdateOverheadMessage(string message)
        {
            if (_world.Player != null) GameActions.MessageOverhead(_world, message, _world.Player);
            //_world.WorldTextManager.AddMessage(_world.Player, "[Listening]");
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var result = _recognizer.Result();
                HandleSpeechResult(result, true);
            }
            else
            {
                var partialResult = _recognizer.PartialResult();
                HandleSpeechResult(partialResult, false);
            }
        }

        private void HandleSpeechResult(string result, bool consoleWrite)
        {
            if (string.IsNullOrEmpty(result)) return;

            using (JsonDocument doc = JsonDocument.Parse(result))
            {
                //if (Settings.GlobalSettings.SpeechRecognitionEnabled && _world.Player != null)
                //    UpdateOverheadMessage("[Listening]");
                HandleFullSpeechResult(consoleWrite, doc);
                HandlePartialSpeechResult(doc);
            }
        }

        private void HandleFullSpeechResult(bool consoleWrite, JsonDocument doc)
        {
            string text = "";
            if (doc.RootElement.TryGetProperty("text", out JsonElement textElement))
            {
                text = textElement.GetString()?.TrimStart("the ".ToCharArray());
            }

            HandleListeningCommands(text);
            if (!Settings.GlobalSettings.SpeechRecognitionEnabled)
                return;

            if (consoleWrite) Console.WriteLine("Parsing FULL result");
            if (consoleWrite) Console.WriteLine($"Extracted text: {text}");

            if (text.Length > 1)
            {
                var nextCommand = text;
                if (consoleWrite) Console.WriteLine($"Next command: {nextCommand}");

                foreach (var command in _speechCommands)
                {
                    if (command.Command.Length <= 1) continue;

                    if (nextCommand.Equals(command.CommandSpeechFormatted, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Command detected: {command.CommandSpeechFormatted}");
                        GameActions.Say(command.CommandWithBracket);
                        UpdateOverheadMessage($"I heard..\r\n{command.CommandSpeechFormatted}");
                        break;
                    }
                }
            }
        }

        private void HandlePartialSpeechResult(JsonDocument doc)
        {
            if (doc.RootElement.TryGetProperty("alternatives", out JsonElement alternativesElement))
            {
                foreach (JsonElement alternative in alternativesElement.EnumerateArray())
                {
                    if (alternative.TryGetProperty("text", out JsonElement altTextElement) &&
                        alternative.TryGetProperty("confidence", out JsonElement confidenceElement))
                    {
                        string altText = altTextElement.GetString();
                        float confidence = confidenceElement.GetSingle();
                        if (string.IsNullOrEmpty(altText))
                            continue;

                        HandleListeningCommands(altText);
                        if (!Settings.GlobalSettings.SpeechRecognitionEnabled)
                            return;

                        Console.WriteLine($"Alternative text: {altText}, Confidence: {confidence}");

                        if (confidence > 0.7f)
                        {
                            Console.WriteLine($"Alternative command detected: {altText}");
                            if (FindSpeechCommand(altText))
                                break;
                            if (FindQuestions(altText))
                                break;
                        }
                    }
                }
            }
        }

        private bool FindQuestions(string message)
        {
            foreach (var question in SpeechRecognitionStrings.PlayerQuestions)
            {
                foreach (var query in question.Value)
                {
                    if (message.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        if (question.Key.ToLower().StartsWith("say "))
                            GameActions.Say(question.Key.ToLower().Replace("say ", ""));
                        else
                        {
                            var matchedCommand = _speechCommands.Find(cmd => cmd.CommandSpeechFormatted.Equals(question.Key.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (matchedCommand != null)
                            {
                                GameActions.Say(matchedCommand.CommandWithBracket);
                            }
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        private void HandleListeningCommands(string text)
        {
            text = text.ToLower();
            if (!Settings.GlobalSettings.SpeechRecognitionEnabled)
            {
                if (SpeechRecognitionStrings.StartListeningCommands.Contains(text))
                {
                    Settings.GlobalSettings.SpeechRecognitionEnabled = true;
                    UpdateOverheadMessage($"{GetRandomGreeting()}\r\n[Speech On]");
                }
            }
            else
            {
                if (SpeechRecognitionStrings.StopListeningCommands.Contains(text))
                {
                    Settings.GlobalSettings.SpeechRecognitionEnabled = false;
                    UpdateOverheadMessage($"{GetRandomFarewell()}\r\n[Speech Off]");
                }
            }
        }

        private bool FindSpeechCommand(string command)
        {
            var matchedCommand = _speechCommands.Find(cmd => cmd.CommandSpeechFormatted.Equals(command.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matchedCommand != null)
            {
                string numericArgs = CheckForNumericArguments(command, matchedCommand);
                string stringArgs = command.Replace(matchedCommand.CommandSpeechFormatted.ToLower(), "");
                if (matchedCommand.HasArguments)
                {
                    GameActions.Say(matchedCommand.CommandWithBracket);
                    UpdateOverheadMessage($"{GetRandomAcknowledgement()}\r\n{command} {(numericArgs != "" ? numericArgs : stringArgs)}");
                }
                else
                {
                    GameActions.Say(matchedCommand.CommandWithBracket);
                    UpdateOverheadMessage($"Speech Command\r\n{command}");
                }
                return true;
            }
            return false;
        }

        private string CheckForNumericArguments(string command, SpeechCommandInfo matchedCommand)
        {
            string arguments = command.Replace(matchedCommand.CommandSpeechFormatted.ToLower(), "");
            var words = arguments.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var word in words)
            {
                if (SpeechRecognitionStrings.NumberWords.TryGetValue(word, out int number))
                {
                    sb.Append(number);
                }

                if (word == "space")
                    sb.Append(' ');
            }

            if (sb.Length > 0)
            {
                return sb.ToString();
            }

            return "";
        }

        public void Start() => _waveIn.StartRecording();

        public void Stop() => _waveIn.StopRecording();

        public static string GetSpeechCommandFilepath() =>
            Path.Combine(CUOEnviroment.ExecutablePath, "SpeechRecognition", SpeechCommandsFile);

        private int ParseNumberFromText(string text)
        {
            if (SpeechRecognitionStrings.NumberWords.TryGetValue(text, out int number)) return number;

            int result = 0;
            foreach (string part in text.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (SpeechRecognitionStrings.NumberWords.TryGetValue(part, out number))
                {
                    result += number;
                }
            }

            return result;
        }

        private string GetRandomFarewell()
        {
            int index = _random.Next(SpeechRecognitionStrings.Farewells.Length - 1);
            return SpeechRecognitionStrings.Farewells[index];
        }

        private string GetRandomGreeting()
        {
            int index = _random.Next(SpeechRecognitionStrings.Greetings.Length - 1);
            return SpeechRecognitionStrings.Greetings[index];
        }
        private string GetRandomAcknowledgement()
        {
            int index = _random.Next(SpeechRecognitionStrings.Acknowledgements.Length - 1);
            return SpeechRecognitionStrings.Acknowledgements[index];
        }
    }
}
