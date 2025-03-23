using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClassicUO.SpeechRecognition
{
    public class SpeechCommandInfo
    {
        private int id;
        private string name;
        private string description;
        private string command;
        private string commandSpeechFormat;
        private string commandOverride;
        private bool hasArguments;

        public int Id
        {
            get => id;
            set => id = value;
        }

        public string Name
        {
            get => name;
            set => name = value;
        }

        public string Description
        {
            get => description;
            set => description = value;
        }

        public string Command
        {
            get => command;
            set => command = value;
        }

        public string CommandOverride
        {
            get => commandOverride;
            set => commandOverride = value;
        }

        public bool HasArguments
        {
            get => hasArguments;
            set => hasArguments = value;
        }

        public string CommandSpeechFormatted
        {
            get
            {
                var cmd = command.Length == 1 ? commandOverride : command;
                if (string.IsNullOrEmpty(cmd))
                {
                    return string.Empty;
                }

                // Split the command at each capital letter (except the first one)
                var readableCommand = Regex.Replace(cmd, "(\\B[A-Z])", " $1");

                //// Console write out any that are all lower case
                //if (cmd.All(char.IsLower))
                //{
                //    Console.WriteLine(cmd);
                //}

                return readableCommand;
            }
        }

        public string CommandWithBracket
        {
            get => $"[{command}";
        }
    }
}
