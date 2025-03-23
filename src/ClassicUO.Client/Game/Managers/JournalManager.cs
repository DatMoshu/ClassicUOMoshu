// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.SpeechRecognition;
using ClassicUO.Utility;
using ClassicUO.Utility.Collections;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers
{
    internal sealed class JournalManager
    {
        private StreamWriter _fileWriter;
        private bool _writerHasException;
        //private List<string> _messageHistory;

        public static Deque<JournalEntry> Entries { get; } = new Deque<JournalEntry>(Constants.MAX_JOURNAL_HISTORY_COUNT);

        public event EventHandler<JournalEntry> EntryAdded;


        public void Add(string text, ushort hue, string name, TextType type, bool isunicode = true, MessageType messageType = MessageType.Regular)
        {


            //if (_messageHistory == null)
            //    _messageHistory = new List<string>();

            //ProcessMessageHistory();

            //_messageHistory.Add(text);


            JournalEntry entry = Entries.Count >= Constants.MAX_JOURNAL_HISTORY_COUNT ? Entries.RemoveFromFront() : new JournalEntry();

            byte font = (byte) (isunicode ? 0 : 9);

            if (ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.OverrideAllFonts)
            {
                font = ProfileManager.CurrentProfile.ChatFont;
                isunicode = ProfileManager.CurrentProfile.OverrideAllFontsIsUnicode;
            }

            DateTime timeNow = DateTime.Now;

            entry.Text = text;
            entry.Font = font;
            entry.Hue = hue;
            entry.Name = name;
            entry.IsUnicode = isunicode;
            entry.Time = timeNow;
            entry.TextType = type;
            entry.MessageType = messageType;

            if (ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.ForceUnicodeJournal)
            {
                entry.Font = 0;
                entry.IsUnicode = true;
            }

            Entries.AddToBack(entry);
            EntryAdded.Raise(entry);

            if (_fileWriter == null && !_writerHasException)
            {
                CreateWriter();
            }

            string output = $"[{timeNow:G}]  {name}: {text}";

            if (string.IsNullOrWhiteSpace(name))
            {
                output = $"[{timeNow:G}]  {text}";
            }

            _fileWriter?.WriteLine(output);
        }

        private void CreateWriter()
        {
            if (_fileWriter == null && ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.SaveJournalToFile)
            {
                try
                {
                    string path = FileSystemHelper.CreateFolderIfNotExists(Path.Combine(CUOEnviroment.ExecutablePath, "Data"), "Client", "JournalLogs");

                    _fileWriter = new StreamWriter(File.Open(Path.Combine(path, $"{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_journal.txt"), FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        AutoFlush = true
                    };

                    try
                    {
                        string[] files = Directory.GetFiles(path, "*_journal.txt");
                        Array.Sort(files);
                        Array.Reverse(files);

                        for (int i = files.Length - 1; i >= 100; --i)
                        {
                            File.Delete(files[i]);
                        }
                    }
                    catch
                    {
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                    // we don't want to wast time.
                    _writerHasException = true;
                }
            }
        }

        public void CloseWriter()
        {
            _fileWriter?.Flush();
            _fileWriter?.Dispose();
            _fileWriter = null;
        }

        public void Clear()
        {
            //Entries.Clear();
            CloseWriter();
        }

        //public void ProcessMessageHistory()
        //{
        //    List<SpeechCommandInfo> commandList = new List<SpeechCommandInfo>();
        //    int id = 0;

        //    foreach (string message in _messageHistory)
        //    {
        //        string[] splitMessage = message.Split(' ');

        //        foreach (string command in splitMessage)
        //        {
        //            SpeechCommandInfo commandInfo = new SpeechCommandInfo
        //            {
        //                Id = id++,
        //                Name = command,
        //                Command = command,
        //                Description = ""
        //            };
        //            commandList.Add(commandInfo);
        //        }
        //    }

        //    // Manually serialize commandList to JSON with indenting
        //    var json = new System.Text.StringBuilder();
        //    json.AppendLine("[");
        //    for (int i = 0; i < commandList.Count; i++)
        //    {
        //        var command = commandList[i];
        //        json.AppendLine("  {");
        //        json.AppendLine($"    \"Id\": {command.Id},");
        //        json.AppendLine($"    \"Name\": \"{command.Name}\",");
        //        json.AppendLine($"    \"Command\": \"{command.Command}\",");
        //        json.AppendLine($"    \"Description\": \"{command.Description}\"");
        //        json.Append("  }");
        //        if (i < commandList.Count - 1)
        //            json.AppendLine(",");
        //        else
        //            json.AppendLine();
        //    }
        //    json.AppendLine("]");
        //    Console.WriteLine(json.ToString());
        //}

    }

    internal class JournalEntry
    {
        public byte Font;
        public ushort Hue;

        public bool IsUnicode;
        public string Name;
        public string Text;

        public TextType TextType;
        public DateTime Time;

        public MessageType MessageType;
    }
}