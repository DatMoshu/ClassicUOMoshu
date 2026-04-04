// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.SpeechRecognition;
using ClassicUO.Utility;
using NAudio.Wave;
using System.Text.Json.Serialization;
using OllamaSharp.Models.Chat;
using Vosk;
using static System.Net.Mime.MediaTypeNames;
using static ClassicUO.Game.LLMClient;


namespace ClassicUO.Game
{
    internal sealed class SpeechRecognitionManager
    {
        private VoskRecognizer _recognizer;
        private WaveInEvent _waveIn;
        private World _world;
        private static readonly Random _random = new Random();
        private TextObject _overheadMessage;
        private LLMClient _lLMClient;
        private bool _chatMode = false;
        private bool _speechDebug;
        private SpeechAvatarManager _speechAvatarManager;
        private SpeechCommandsManager _speechCommandsManager;

        public void Initialize(string modelPath, float sampleRate, World world)
        {
            _lLMClient = new LLMClient("http://localhost:11434/api/generate", "avatar:latest");

            Vosk.Vosk.SetLogLevel(0);
            var model = new Model(modelPath);
            _recognizer = new VoskRecognizer(model, sampleRate);
            _recognizer.SetMaxAlternatives(5);
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat((int)sampleRate, 1),
                //BufferMilliseconds = 5
            };

            _waveIn.DataAvailable += WaveIn_DataAvailable;
            _world = world;
            _speechAvatarManager = new SpeechAvatarManager(_world, _lLMClient);
            _speechCommandsManager = new SpeechCommandsManager();
            _world.Journal.Add("Init Specch Recognition", 0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);

            // Register the llm command
            _world.CommandManager.Register("llm", HandleLlmCommand);
            //DemoSpeaker(model);
        }

        private async void HandleLlmCommand(string[] args)
        {
            if (args.Length == 0)
            {
                PlayerSay("Usage: [llm <prompt>");
                return;
            }

            string prompt = string.Join(" ", args);
            try
            {
                string response = await _lLMClient.CallLlamaAsync(prompt);
                PlayerSay(response);
            }
            catch (Exception ex)
            {
                PlayerSay($"Error: {ex.Message}");
            }
        }

        public void SendMessageToJournal(string message)
        {
            if (_world?.Journal != null)
            {
                _world.Journal.Add(message, 0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
            }
            else
            {
                Console.WriteLine("Journal manager is not available.");
            }
        }

        private void PlayerSay(string message)
        {
            if (_world.Player != null) GameActions.Say(message);
            //_world.WorldTextManager.AddMessage(_world.Player, "[Listening]");

        }


        public static void DemoSpeaker(Model model)
        {
            // Output speakers
            SpkModel spkModel = new SpkModel("model-spk");
            VoskRecognizer rec = new VoskRecognizer(model, 16000.0f);
            rec.SetSpkModel(spkModel);

            using (Stream source = File.OpenRead("C:\\Users\\kaise\\Downloads\\test.wav"))
            {
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (rec.AcceptWaveform(buffer, bytesRead))
                    {
                        Console.WriteLine(rec.Result());
                    }
                    else
                    {
                        Console.WriteLine(rec.PartialResult());
                    }
                }
            }
            Console.WriteLine(rec.FinalResult());
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
                // Check if the text contains at least two words  
                if (doc.RootElement.TryGetProperty("text", out JsonElement textElement))
                {
                    string text = textElement.GetString();

                }

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

            if (_speechAvatarManager.HandleLlmPrompt(text))
                return;

            HandleListeningCommands(text);
            if (!Settings.GlobalSettings.SpeechRecognitionEnabled)
                return;

            if (consoleWrite) Console.WriteLine("Parsing FULL result");
            if (consoleWrite) Console.WriteLine($"Extracted text: {text}");

            if (text.Length > 1)
            {
                var nextCommand = text;
                if (consoleWrite) Console.WriteLine($"Next command: {nextCommand}");

                if (_speechCommandsManager.FindSpeechCommand(nextCommand))
                {
                    PlayerSay($"I heard..\r\n{nextCommand}");
                    return;
                }

                // Check for macro commands
                if (CheckForMacroCommand(text))
                {
                    return;
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
                        string commandText = altTextElement.GetString();
                        float confidence = confidenceElement.GetSingle();
                        Console.WriteLine($"Speech Recognized: {commandText}, Confidence: {confidence}");
                        if (string.IsNullOrEmpty(commandText))
                            continue;

                        HandleListeningCommands(commandText);
                        if (!Settings.GlobalSettings.SpeechRecognitionEnabled)
                            return;

                        Console.WriteLine($"Alternative text: {commandText}, Confidence: {confidence}");

                        if (commandText.StartsWith("speech debug on", StringComparison.OrdinalIgnoreCase))
                        {
                            _speechDebug = true;
                            PlayerSay("Speech debug enabled.");
                        }
                        else if (commandText.StartsWith("speech debug off", StringComparison.OrdinalIgnoreCase))
                        {
                            _speechDebug = false;
                            PlayerSay("Speech debug disabled.");
                        }

                        if (confidence > 0.7f)
                        {
                            Console.WriteLine($"Alternative command detected: {commandText}");

                            if (_speechDebug)
                                GameActions.Say("Heard: " + commandText);

                            if (_speechAvatarManager.HandleLlmPrompt(commandText))
                                return;
                            if (_speechCommandsManager.FindSpeechCommand(commandText))
                                break;
                            if (FindQuestions(commandText))
                                break;
                            if (FindPetCommands(commandText))
                                break;
                            // Check for macro commands
                            if (CheckForMacroCommand(commandText))
                            {
                                return;
                            }
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
                        else if (question.Key.ToLower().StartsWith("joke"))
                            GameActions.Say(GetRandomJoke());
                        else
                        {
                            var matchedCommand = _speechCommandsManager.FindSpeechCommand(question.Key.Trim());
                            if (matchedCommand)
                            {
                                GameActions.Say(question.Key.Trim());
                            }
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        private bool FindPetCommands(string message)
        {
            foreach (var petCommand in SpeechRecognitionStrings.PetCommands)
            {
                foreach (var query in petCommand.Value)
                {
                    if (message.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        //if the command doesn't contain "closest" or "nearest", we will attempt to attack the closest target
                        if (!petCommand.Key.ToLower().Contains("closest") && !petCommand.Key.ToLower().Contains("nearest"))
                            GameActions.Say(petCommand.Key.ToLower().Replace("say ", ""));
                        else
                        {
                            var matchedCommand = _speechCommandsManager.FindSpeechCommand(petCommand.Key.Trim());
                            if (matchedCommand)
                            {
                                GameActions.Say(petCommand.Key.Trim());
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
                    PlayerSay($"{GetRandomGreeting()}\r\n[Speech On]");
                }
            }
            else
            {
                if (SpeechRecognitionStrings.StopListeningCommands.Contains(text))
                {
                    Settings.GlobalSettings.SpeechRecognitionEnabled = false;
                    PlayerSay($"{GetRandomFarewell()}\r\n[Speech Off]");
                }
            }
        }

        public void Start() => _waveIn.StartRecording();

        public void Stop() => _waveIn.StopRecording();

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
        private string GetRandomJoke()
        {
            int index = _random.Next(Jokes.UltimaOnlineJokes.Length - 1);
            return Jokes.UltimaOnlineJokes[index];
        }

        public void GetSurroundingInfo()
        {
            if (_world.Player == null)
            {
                PlayerSay("Player not found.");
                return;
            }

            StringBuilder info = new StringBuilder();
            info.AppendLine("Surrounding Information:");

            // Check for mobiles around the playerqa    1                                                                                                                                                           QQQ               V
            foreach (var mobile in _world.Mobiles.Values)
            {
                if (mobile.Distance <= 10) // Assuming a distance threshold of 10 units
                {
                    info.AppendLine($"Mobile: {mobile.Name}, Distance: {mobile.Distance}");

                    if (IsHostile(_world.Player))
                    {
                        info.AppendLine($"  - {mobile.Name} is attacking you!");
                    }
                }
            }

            // Check for objects around the player
            foreach (var item in _world.Items.Values)
            {
                if (item.Distance <= 10) // Assuming a distance threshold of 10 units
                {
                    if (item.IsTree())
                    {
                        info.AppendLine($"Object: Tree, Distance: {item.Distance}");
                    }
                    else if (item.IsFood())
                    {
                        info.AppendLine($"Object: Food, Distance: {item.Distance}");
                    }
                    else
                    {
                        info.AppendLine($"Object: {item.Name}, Distance: {item.Distance}");
                    }
                }
            }

            PlayerSay(info.ToString());
        }

        public static bool IsHostile(Mobile mobile)
        {
            return mobile.NotorietyFlag == NotorietyFlag.Criminal || mobile.NotorietyFlag == NotorietyFlag.Enemy;
        }

        private bool CheckForMacroCommand(string text)
        {
            foreach (var macroType in SpeechMacroStrings.MacroSpeechCommands)
            {
                foreach (var macroSubType in macroType.Value)
                {
                    if (macroSubType.Value.Any(command => text.Equals(command, StringComparison.OrdinalIgnoreCase)))
                    {
                        ExecuteMacro(macroType.Key, macroSubType.Key);
                        return true;
                    }
                }
            }
            return false;
        }

        private void ExecuteMacro(MacroType macroType, MacroSubType macroSubType)
        {
            var macro = new MacroObject(macroType, macroSubType);
            _world.Macros.SetMacroToExecute(macro);
            _world.Macros.Update();
            PlayerSay($"Executing macro: {macroType} - {macroSubType}");
        }
    }
    // Extension methods for checking item types
    internal static class ItemExtensions
    {
        public static bool IsTree(this Item item)
        {
            // Add logic to determine if the item is a tree
            return item.Serial == 0x0D45; // Example graphic ID for a tree
        }

        public static bool IsFood(this Item item)
        {
            // Add logic to determine if the item is food
            return item.Graphic == 0x09C0; // Example graphic ID for food
        }
    }
}
