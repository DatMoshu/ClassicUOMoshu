using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicUO.SpeechRecognition;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassicUO.Game
{
    // AOT-safe serializer context for speechcommands.json deserialization.
    [JsonSerializable(typeof(List<SpeechCommandInfo>))]
    internal sealed partial class SpeechCommandsJsonContext : JsonSerializerContext { }

    internal sealed class SpeechCommandsManager
    {
        private const string SpeechCommandsFile = "speechcommands.json";
        private List<SpeechCommandInfo> _speechCommands = new List<SpeechCommandInfo>();

        public SpeechCommandsManager()
        {
            if (File.Exists(SpeechCommandsManager.GetSpeechCommandFilepath()))
            {
                string commandsJson = File.ReadAllText(SpeechCommandsManager.GetSpeechCommandFilepath());
                LoadSpeechCommands(commandsJson);
            }
        }

        public void LoadSpeechCommands(string commandsJson)
        {
            _speechCommands = JsonSerializer.Deserialize(
                commandsJson, SpeechCommandsJsonContext.Default.ListSpeechCommandInfo)
                ?? new List<SpeechCommandInfo>();
        }

        public bool FindSpeechCommand(string command)
        {
            var matchedCommand = _speechCommands
                .Where(cmd => command.StartsWith(cmd.CommandSpeechFormatted.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(cmd.CommandSpeechFormatted))
                .OrderByDescending(cmd => cmd.CommandSpeechFormatted.Length)
                .FirstOrDefault();

            if (matchedCommand != null)
            {
                string arguments = SpeechArguments.ParseArgumentsWithWords(
                    command.Replace(matchedCommand.CommandSpeechFormatted.ToLower(), ""));

                // Send server command
                GameActions.Say(matchedCommand.CommandWithBracket + " " + arguments);

                return true;
            }
            return false;
        }

        public static string GetSpeechCommandFilepath() =>
            Path.Combine(CUOEnviroment.ExecutablePath, "SpeechRecognition", SpeechCommandsFile);
    }
}
