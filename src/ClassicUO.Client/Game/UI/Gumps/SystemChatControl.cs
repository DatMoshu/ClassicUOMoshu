// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Network;
using ClassicUO.Renderer;
using ClassicUO.Resources;
using SDL3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClassicUO.Game.UI.Gumps
{
    internal enum ChatMode
    {
        Default,
        Whisper,
        Emote,
        Yell,
        Party,
        //PartyPrivate,
        Guild,
        Alliance,
        ClientCommand,
        UOAMChat,
        Prompt,
        UOChat
    }

    internal class SystemChatControl : Control
    {
        private const int MAX_MESSAGE_LENGTH = 100;
        private const int TEXTBOX_LENGTH = 500;
        private const int CHAT_X_OFFSET = 3;
        private const int CHAT_HEIGHT = 15;
        private const int MAX_HISTORY = 50;
        private const int MAX_SUGGESTIONS = 5;
        private const string HISTORY_FILENAME = "chat_history.txt";
        private static readonly List<Tuple<ChatMode, string>> _messageHistory = new List<Tuple<ChatMode, string>>();
        private static int _messageHistoryIndex = -1;
        private static string _loadedHistoryPath;

        private readonly Label _currentChatModeLabel;
        private readonly Label _suggestionLabel;
        private readonly List<string> _suggestionBuffer = new List<string>(MAX_SUGGESTIONS);
        private int _tabCycleIndex = -1;
        private string _tabCycleOrigToken;

        private bool _isActive;
        private ChatMode _mode = ChatMode.Default;

        private readonly WorldViewportGump _gump;
        private readonly LinkedList<ChatLineTime> _textEntries;
        private readonly AlphaBlendControl _trans;

        private static readonly ChatMode[] SINGLE_LINE_CHAT_MODES = [ChatMode.ClientCommand, ChatMode.Prompt];


        public SystemChatControl(WorldViewportGump gump, int x, int y, int w, int h)
        {
            _gump = gump;
            X = x;
            Y = y;
            Width = w;
            Height = h;
            _textEntries = new LinkedList<ChatLineTime>();
            CanCloseWithRightClick = false;
            AcceptMouseInput = false;
            AcceptKeyboardInput = false;

            TextBoxControl = new StbTextBox
            (
                ProfileManager.CurrentProfile.ChatFont,
                TEXTBOX_LENGTH,
                Width,
                true,
                FontStyle.BlackBorder,
                33
            )
            {
                Multiline = true,
                PassEnterToParent = true
            };

            TextBoxControl.BeforeTextChanged += TextBoxControl_BeforeTextChanged;

            TextBoxControl.TextChanged += TextBoxControl_TextChanged;

            _gump.World.MessageManager.ServerPromptChanged += MessageManager_ServerPromptChanged;

            float gradientTransparency = ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.HideChatGradient ? 0.0f : 0.5f;

            Add
            (
                _trans = new AlphaBlendControl(gradientTransparency)
                {
                    IsVisible = !ProfileManager.CurrentProfile.ActivateChatAfterEnter,
                    AcceptMouseInput = true
                }
            );

            Add(TextBoxControl);

            Add
            (
                _currentChatModeLabel = new Label(string.Empty, true, 0, style: FontStyle.BlackBorder)
                {
                    IsVisible = false
                }
            );

            Add
            (
                _suggestionLabel = new Label(string.Empty, true, 0x03B2, style: FontStyle.BlackBorder)
                {
                    IsVisible = false,
                    AcceptMouseInput = false
                }
            );

            LoadHistoryFromDisk();

            WantUpdateSize = false;

            _gump.World.MessageManager.MessageReceived += ChatOnMessageReceived;
            Mode = ChatMode.Default;
            TextBoxControl.Hue = GetChatHue(Mode);

            IsActive = !ProfileManager.CurrentProfile.ActivateChatAfterEnter;

            SetFocus();
        }

        private void MessageManager_ServerPromptChanged(object sender, PromptData e)
        {
            if (e.Prompt == ConsolePrompt.None)
            {
                if (Mode == ChatMode.Prompt)
                {
                    Mode = ChatMode.Default;
                    RecalculateHuesAndSizes();
                }
            }
            else
            {
                if (Mode != ChatMode.Prompt)
                {
                    Mode = ChatMode.Prompt;
                    RecalculateHuesAndSizes();
                }
            }
        }

        private void TextBoxControl_TextChanged(object sender, EventArgs e)
        {
            RecalculateHuesAndSizes();
            RefreshSuggestionLabel();
            // Any manual edit invalidates the Tab cycle — next Tab re-scans from the current token.
            _tabCycleIndex = -1;
            _tabCycleOrigToken = null;
        }

        private void RecalculateHuesAndSizes()
        {
            ushort hue = GetChatHue(Mode);
            TextBoxControl.Hue = hue;
            _currentChatModeLabel.Hue = hue;
            Resize();
        }

        private void TextBoxControl_BeforeTextChanged(object sender, StbTextBox.BeforeTextChangedEventArgs e)
        {
            // Normalize text before creating new newlines
            string text = e.NewText.Replace("\n", " ");

            string result = string.Empty;
            string message;

            int relativeCursorIndex = e.NewCaretIndex;

            // repeatedly split the message up, create line breaks
            // and move the caret accordingly in case we had to add a new character instead of just replacing
            // a space with a newline
            // The latter happens only if there are no candidate spaces to break in the overflowing line
            while (TrySplitMessage(text, Mode, out message, out string remainder))
            {
                result = AppendMultilinePart(result, message);
                if (relativeCursorIndex >= message.Length)
                {
                    int cursorIndexDelta = (message.Length + 1 + remainder.Length) - text.Length;

                    e.NewCaretIndex += cursorIndexDelta;
                    relativeCursorIndex += cursorIndexDelta;
                }

                relativeCursorIndex -= message.Length;
                text = remainder;
            }

            result = AppendMultilinePart(result, message);
            e.NewText = result;
        }

        private static string AppendMultilinePart(string result, string message)
        {
            if (result.Length > 0)
            {
                result += "\n" + message;
            }
            else
            {
                result = message;
            }

            return result;
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = TextBoxControl.IsVisible = TextBoxControl.IsEditable = value;

                if (_isActive)
                {
                    TextBoxControl.Width = _trans.Width - CHAT_X_OFFSET;
                    TextBoxControl.ClearText();
                }

                SetFocus();
            }
        }


        public ChatMode Mode
        {
            get => _mode;
            set
            {
                ChatMode previousMode = _mode;
                _mode = value;

                if (previousMode == ChatMode.Prompt && previousMode != value)
                {
                    _gump.World.MessageManager.CancelServerPrompt();
                }

                if (IsActive)
                {
                    switch (value)
                    {
                        case ChatMode.Default:
                            DisposeChatModePrefix();
                            TextBoxControl.ClearText();

                            break;

                        case ChatMode.Whisper:
                            AppendChatModePrefix(ResGumps.Whisper, TextBoxControl.Text);

                            break;

                        case ChatMode.Emote:
                            AppendChatModePrefix(ResGumps.Emote, TextBoxControl.Text);

                            break;

                        case ChatMode.Yell:
                            AppendChatModePrefix(ResGumps.Yell, TextBoxControl.Text);

                            break;

                        case ChatMode.Party:
                            AppendChatModePrefix(ResGumps.Party, TextBoxControl.Text);

                            break;

                        case ChatMode.Guild:
                            AppendChatModePrefix(ResGumps.Guild, TextBoxControl.Text);

                            break;

                        case ChatMode.Alliance:
                            AppendChatModePrefix(ResGumps.Alliance, TextBoxControl.Text);

                            break;

                        case ChatMode.ClientCommand:
                            AppendChatModePrefix(ResGumps.Command, TextBoxControl.Text);

                            break;

                        case ChatMode.UOAMChat:
                            DisposeChatModePrefix();
                            AppendChatModePrefix(ResGumps.UOAM, TextBoxControl.Text);

                            break;

                        case ChatMode.UOChat:
                            DisposeChatModePrefix();

                            AppendChatModePrefix(ResGumps.Chat, TextBoxControl.Text);

                            break;

                        case ChatMode.Prompt:
                            AppendChatModePrefix(ResGumps.Prompt, TextBoxControl.Text);
                            
                            break;
                    }
                }
            }
        }

        private ushort GetChatHue(ChatMode mode)
        {
            return mode switch
            {
                ChatMode.Default => ProfileManager.CurrentProfile.SpeechHue,
                ChatMode.Whisper => ProfileManager.CurrentProfile.WhisperHue,
                ChatMode.Emote => ProfileManager.CurrentProfile.EmoteHue,
                ChatMode.Yell => ProfileManager.CurrentProfile.YellHue,
                ChatMode.Party => ProfileManager.CurrentProfile.PartyMessageHue,
                ChatMode.Guild => ProfileManager.CurrentProfile.GuildMessageHue,
                ChatMode.Alliance => ProfileManager.CurrentProfile.AllyMessageHue,
                ChatMode.ClientCommand => 1161,
                ChatMode.UOAMChat => 83,
                ChatMode.UOChat => ProfileManager.CurrentProfile.ChatMessageHue,
                ChatMode.Prompt => 946,
                _ => 33,
            };
        }

        public readonly StbTextBox TextBoxControl;

        public void SetFocus()
        {
            TextBoxControl.IsEditable = true;
            TextBoxControl.SetKeyboardFocus();
            TextBoxControl.IsEditable = _isActive;
            _trans.IsVisible = _isActive;
        }

        public void ToggleChatVisibility()
        {
            IsActive = !IsActive;
        }

        private void ChatOnMessageReceived(object sender, MessageEventArgs e)
        {
            if (e.TextType == TextType.CLIENT)
            {
                return;
            }

            switch (e.Type)
            {
                case MessageType.Regular when e.Parent == null || !SerialHelper.IsValid(e.Parent.Serial):
                case MessageType.System:
                    if (!string.IsNullOrEmpty(e.Name) && !e.Name.Equals("system", StringComparison.InvariantCultureIgnoreCase))
                    {
                        AddLine($"{e.Name}: {e.Text}", e.Font, e.Hue, e.IsUnicode);
                    }
                    else
                    {
                        AddLine(e.Text, e.Font, e.Hue, e.IsUnicode);
                    }

                    break;

                case MessageType.Label when e.Parent == null || !SerialHelper.IsValid(e.Parent.Serial):
                    AddLine(e.Text, e.Font, e.Hue, e.IsUnicode);

                    break;

                case MessageType.Party:
                    AddLine(string.Format(ResGumps.PartyName0Text1, e.Name, e.Text), e.Font, ProfileManager.CurrentProfile.PartyMessageHue, e.IsUnicode);

                    break;

                case MessageType.Guild:
                    AddLine(string.Format(ResGumps.GuildName0Text1, e.Name, e.Text), e.Font, ProfileManager.CurrentProfile.GuildMessageHue, e.IsUnicode);

                    break;

                case MessageType.Alliance:
                    AddLine(string.Format(ResGumps.AllianceName0Text1, e.Name, e.Text), e.Font, ProfileManager.CurrentProfile.AllyMessageHue, e.IsUnicode);

                    break;

                default:

                    if (e.Parent == null || !SerialHelper.IsValid(e.Parent.Serial))
                    {
                        if (string.IsNullOrEmpty(e.Name) || e.Name.Equals("system", StringComparison.InvariantCultureIgnoreCase))
                        {
                            AddLine(e.Text, e.Font, e.Hue, e.IsUnicode);
                        }
                    }

                    break;
            }
        }

        public override void Dispose()
        {
            TextBoxControl.BeforeTextChanged -= TextBoxControl_BeforeTextChanged;
            TextBoxControl.TextChanged -= TextBoxControl_TextChanged;
            _gump.World.MessageManager.ServerPromptChanged -= MessageManager_ServerPromptChanged;
            _gump.World.MessageManager.MessageReceived -= ChatOnMessageReceived;
            SaveHistoryToDisk();
            ServerCommandRegistry.Clear();
            base.Dispose();
        }

        private void AppendChatModePrefix(string labelText, string text)
        {
            if (!_currentChatModeLabel.IsVisible)
            {
                _currentChatModeLabel.Text = labelText;
                _currentChatModeLabel.IsVisible = true;
                Resize();

                int idx = string.IsNullOrEmpty(text) ? -1 : TextBoxControl.Text.IndexOf(text);
                string str = string.Empty;

                if (idx > 0)
                {
                    str = TextBoxControl.Text.Substring(idx, TextBoxControl.Text.Length - labelText.Length - 1);
                }

                TextBoxControl.SetText(str);
            }
        }

        private void DisposeChatModePrefix()
        {
            _currentChatModeLabel.IsVisible = false;
            RecalculateHuesAndSizes();
        }

        public void AddLine(string text, byte font, ushort hue, bool isunicode)
        {
            if (_textEntries.Count >= 30)
            {
                LinkedListNode<ChatLineTime> lineToRemove = _textEntries.First;
                lineToRemove.Value.Destroy();
                _textEntries.Remove(lineToRemove);
            }

            _textEntries.AddLast(new ChatLineTime(text, font, isunicode, hue));
        }

        internal void Resize()
        {
            if (TextBoxControl != null)
            {
                int lines = TextBoxControl.Text.Count('\n') + 1;

                // the chat mode is always on the left and on the bottom
                _currentChatModeLabel.X = CHAT_X_OFFSET;
                _currentChatModeLabel.Y = Height - CHAT_HEIGHT - CHAT_X_OFFSET;

                // if the chat mode is visible, it should push the text box further to the right
                int chatModeOffset = _currentChatModeLabel.IsVisible ? _currentChatModeLabel.Width : 0;
                TextBoxControl.X = CHAT_X_OFFSET + chatModeOffset;
                TextBoxControl.Y = Height - lines * CHAT_HEIGHT - CHAT_X_OFFSET;
                // if the text box has been pushed to the right, it should not clip into the void
                TextBoxControl.Width = Width - CHAT_X_OFFSET - chatModeOffset;
                // if the text box has more than one line, it will grow upwards
                TextBoxControl.Height = lines * CHAT_HEIGHT + CHAT_X_OFFSET;

                // the dark background should always cover chat mode and text box fully
                _trans.X = TextBoxControl.X - CHAT_X_OFFSET - chatModeOffset;
                _trans.Y = TextBoxControl.Y;
                _trans.Width = Width;
                _trans.Height = TextBoxControl.Height;

                // Autocomplete strip sits directly above the entry so it never covers text.
                if (_suggestionLabel != null && _suggestionLabel.IsVisible)
                {
                    _suggestionLabel.X = TextBoxControl.X;
                    _suggestionLabel.Y = TextBoxControl.Y - CHAT_HEIGHT;
                }
            }
        }

        public override void Update()
        {
            LinkedListNode<ChatLineTime> first = _textEntries.First;

            while (first != null)
            {
                LinkedListNode<ChatLineTime> next = first.Next;

                first.Value.Update();

                if (first.Value.IsDisposed)
                {
                    _textEntries.Remove(first);
                }

                first = next;
            }

            if (Mode == ChatMode.Default && IsActive)
            {
                if (TextBoxControl.Text.Length > 0)
                {
                    switch (TextBoxControl.Text[0])
                    {
                        case '/':

                            int pos = 1;

                            while (pos < TextBoxControl.Text.Length && TextBoxControl.Text[pos] != ' ')
                            {
                                pos++;
                            }

                            if (pos < TextBoxControl.Text.Length && int.TryParse(TextBoxControl.Text.Substring(1, pos), out int index) && index > 0 && index < 11)
                            {
                                if (_gump.World.Party.Members[index - 1] != null && _gump.World.Party.Members[index - 1].Serial != 0)
                                {
                                    AppendChatModePrefix(string.Format(ResGumps.Tell0, _gump.World.Party.Members[index - 1].Name), string.Empty);
                                }
                                else
                                {
                                    AppendChatModePrefix(ResGumps.TellEmpty, string.Empty);
                                }

                                Mode = ChatMode.Party;
                                TextBoxControl.SetText($"{index} ");
                            }
                            else
                            {
                                Mode = ChatMode.Party;
                            }

                            break;

                        case '\\':
                            Mode = ChatMode.Guild;

                            break;

                        case '|':
                            Mode = ChatMode.Alliance;

                            break;

                        case '-':
                            Mode = ChatMode.ClientCommand;

                            break;

                        case ',' when _gump.World.ChatManager.ChatIsEnabled == ChatStatus.Enabled:
                            Mode = ChatMode.UOChat;

                            break;


                        case ':' when TextBoxControl.Text.Length > 1 && TextBoxControl.Text[1] == ' ':
                            Mode = ChatMode.Emote;

                            break;

                        case ';' when TextBoxControl.Text.Length > 1 && TextBoxControl.Text[1] == ' ':
                            Mode = ChatMode.Whisper;

                            break;

                        case '!' when TextBoxControl.Text.Length > 1 && TextBoxControl.Text[1] == ' ':
                            Mode = ChatMode.Yell;

                            break;
                    }
                }
            }
            else if (Mode == ChatMode.ClientCommand && TextBoxControl.Text.Length == 1 && TextBoxControl.Text[0] == '-')
            {
                Mode = ChatMode.UOAMChat;
            }

            _trans.Alpha = ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.HideChatGradient ? 0.0f : 0.5f;

            base.Update();
        }

        public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
        {
            int yy = TextBoxControl.Y + y - 20;
            var scale = 1f;

            LinkedListNode<ChatLineTime> last = _textEntries.Last;

            var depth = layerDepthRef;

            renderLists.AddGumpNoAtlas(batcher =>
            {
                while (last != null)
                {
                    LinkedListNode<ChatLineTime> prev = last.Previous;

                    if (last.Value.IsDisposed)
                    {
                        _textEntries.Remove(last);
                    }
                    else
                    {
                        yy -= last.Value.TextHeight;

                        if (yy >= y)
                        {
                            last.Value.Draw(batcher, x + 2, yy, depth, scale);
                        }
                    }

                    last = prev;
                }
                return true;
            });

            return base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);
        }

        protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
        {
            switch (key)
            {
                case SDL.SDL_Keycode.SDLK_TAB when IsActive && TryHandleTabComplete():
                    break;

                case SDL.SDL_Keycode.SDLK_Q when Keyboard.Ctrl && _messageHistoryIndex > -1 && !ProfileManager.CurrentProfile.DisableCtrlQWBtn:

                    GameScene scene = Client.Game.GetScene<GameScene>();

                    if (scene == null)
                    {
                        return;
                    }

                    if (_gump.World.Macros.FindMacro(key, false, true, false) != null)
                    {
                        return;
                    }

                    if (!IsActive)
                    {
                        return;
                    }

                    {
                        int target = FindPrevAllowedHistoryIndex(_messageHistoryIndex);
                        if (target >= 0)
                        {
                            _messageHistoryIndex = target;
                            Mode = _messageHistory[_messageHistoryIndex].Item1;
                            TextBoxControl.SetText(_messageHistory[_messageHistoryIndex].Item2);
                        }
                    }

                    break;

                case SDL.SDL_Keycode.SDLK_W when Keyboard.Ctrl && !ProfileManager.CurrentProfile.DisableCtrlQWBtn:

                    scene = Client.Game.GetScene<GameScene>();

                    if (scene == null)
                    {
                        return;
                    }

                    if (_gump.World.Macros.FindMacro(key, false, true, false) != null)
                    {
                        return;
                    }

                    if (!IsActive)
                    {
                        return;
                    }

                    {
                        int target = FindNextAllowedHistoryIndex(_messageHistoryIndex);
                        if (target >= 0 && target < _messageHistory.Count)
                        {
                            _messageHistoryIndex = target;
                            Mode = _messageHistory[_messageHistoryIndex].Item1;
                            TextBoxControl.SetText(_messageHistory[_messageHistoryIndex].Item2);
                        }
                        else
                        {
                            _messageHistoryIndex = _messageHistory.Count;
                            TextBoxControl.ClearText();
                        }
                    }

                    break;

                case SDL.SDL_Keycode.SDLK_BACKSPACE when Keyboard.Ctrl && !Keyboard.Alt && !Keyboard.Shift:
                    {
                        if (!IsActive)
                        {
                            return;
                        }

                        String text = TextBoxControl.Text;

                        if (string.IsNullOrEmpty(text))
                        {
                            Mode = ChatMode.Default;
                            break;
                        }

                        // ignore the final character since that's a space if the user presses Ctrl + Backspace multiple times
                        int index = text.LastIndexOf(' ', TextBoxControl.CaretIndex - 1);

                        if (index >= 0)
                        {
                            // do not remove the final space since we assume the user wants to continue writing
                            TextBoxControl.SetText(text[..(index + 1)] + text[TextBoxControl.CaretIndex..]);
                            TextBoxControl.CaretIndex = index + 1;
                        }
                        else
                        {
                            TextBoxControl.ClearText();
                        }

                        if (string.IsNullOrEmpty(TextBoxControl.Text))
                        {
                            Mode = ChatMode.Default;
                        }
                        break;
                    }

                case SDL.SDL_Keycode.SDLK_BACKSPACE when !Keyboard.Ctrl && !Keyboard.Alt && !Keyboard.Shift && string.IsNullOrEmpty(TextBoxControl.Text):
                    if (!IsActive)
                    {
                        return;
                    }

                    Mode = ChatMode.Default;

                    break;

                case SDL.SDL_Keycode.SDLK_ESCAPE when Mode == ChatMode.Prompt:

                    Mode = ChatMode.Default;

                    break;
            }
        }

        public string ExtractSendableTextSubstring(ref string textBoxText)
        {
            string toReturn;
            if (textBoxText.Length <= MAX_MESSAGE_LENGTH)
            {
                toReturn = textBoxText;
                textBoxText = string.Empty;
                return toReturn;
            }

            int lastSpaceIndex = textBoxText.LastIndexOf(' ', MAX_MESSAGE_LENGTH);

            if (lastSpaceIndex < 0)
            {
                lastSpaceIndex = MAX_MESSAGE_LENGTH;
            }

            toReturn = textBoxText.Substring(0, lastSpaceIndex);
            textBoxText = textBoxText.Substring(lastSpaceIndex).TrimStart();
            return toReturn;
        }

        private void ResetTextBox()
        {
            TextBoxControl.ClearText();
            Mode = ChatMode.Default;
            DisposeChatModePrefix();
        }

        public bool IsComposing
        {
            get => IsActive && TextBoxControl.Text.Length > 0;
        }

        public override void OnKeyboardReturn(int textID, string text)
        {
            if (!IsActive && ProfileManager.CurrentProfile.ActivateChatAfterEnter || string.IsNullOrEmpty(text))
            {
                ResetTextBox();
            }

            if (TryHandleMessageMultipartSend(text, Mode, out var remainder))
            {
                TextBoxControl.SetText(remainder);
            }
            else
            {
                HandleMessageSend(text, Mode);
                TextBoxControl.ClearText();
            }

            if (TextBoxControl.Length == 0)
            {
                ResetTextBox();
            }
        }

        private bool TryHandleMessageMultipartSend(string text, ChatMode mode, out string remainder)
        {
            if (!TrySplitMessage(text, mode, out string message, out remainder))
            {
                remainder = message;
                return false;
            }

            HandleMessageSend(message, mode);

            // Preserve the party index we're messaging
            // otherwise the remainder would be sent to all instead of that specific person
            if (mode == ChatMode.Party && int.TryParse(text[0..2], out var partyCharIndex) && partyCharIndex is > 0 and < 11)
                remainder = $"{partyCharIndex} {remainder}";

            return true;
        }

        private bool TrySplitMessage(string text, ChatMode mode, out string message, out string remainder)
        {
            // Prompt response messages cannot be multiple parts
            if (text.Length <= MAX_MESSAGE_LENGTH || SINGLE_LINE_CHAT_MODES.Contains(mode))
            {
                message = text;
                remainder = string.Empty;
                return false;
            }

            int lastSpaceIndex = text.LastIndexOfAny([' ', '\n'], MAX_MESSAGE_LENGTH);
            if (lastSpaceIndex < 0)
            {
                lastSpaceIndex = MAX_MESSAGE_LENGTH;
            }

            message = text[..lastSpaceIndex];
            remainder = text[lastSpaceIndex..].TrimStart();
            return true;
        }

        private void HandleMessageSend(string text, ChatMode sentMode)
        {
            _messageHistory.Add(new Tuple<ChatMode, string>(Mode, text));
            _messageHistoryIndex = _messageHistory.Count;


            switch (sentMode)
            {
                case ChatMode.Default:
                    GameActions.Say(text, ProfileManager.CurrentProfile.SpeechHue);

                    break;

                case ChatMode.Whisper:
                    GameActions.Say(text, ProfileManager.CurrentProfile.WhisperHue, MessageType.Whisper);

                    break;

                case ChatMode.Emote:
                    text = ResGeneral.EmoteChar + text + ResGeneral.EmoteChar;
                    GameActions.Say(text, ProfileManager.CurrentProfile.EmoteHue, MessageType.Emote);

                    break;

                case ChatMode.Yell:
                    GameActions.Say(text, ProfileManager.CurrentProfile.YellHue, MessageType.Yell);

                    break;

                case ChatMode.Prompt:
                    _gump.World.MessageManager.SendServerPromptResponse(text);
                    break;

                case ChatMode.Party:

                    switch (text.ToLower())
                    {
                        case "add":
                            if (_gump.World.Party.Leader == 0 || _gump.World.Party.Leader == _gump.World.Player)
                            {
                                GameActions.RequestPartyInviteByTarget();
                            }
                            else
                            {
                                _gump.World.MessageManager.HandleMessage
                                (
                                    null,
                                    ResGumps.YouAreNotPartyLeader,
                                    "System",
                                    0xFFFF,
                                    MessageType.Regular,
                                    3,
                                    TextType.SYSTEM
                                );
                            }

                            break;

                        case "loot":

                            if (_gump.World.Party.Leader != 0)
                            {
                                _gump.World.Party.CanLoot = !_gump.World.Party.CanLoot;
                            }
                            else
                            {
                                _gump.World.MessageManager.HandleMessage
                                (
                                    null,
                                    ResGumps.YouAreNotInAParty,
                                    "System",
                                    0xFFFF,
                                    MessageType.Regular,
                                    3,
                                    TextType.SYSTEM
                                );
                            }


                            break;

                        case "quit":

                            if (_gump.World.Party.Leader == 0)
                            {
                                _gump.World.MessageManager.HandleMessage
                                (
                                    null,
                                    ResGumps.YouAreNotInAParty,
                                    "System",
                                    0xFFFF,
                                    MessageType.Regular,
                                    3,
                                    TextType.SYSTEM
                                );
                            }
                            else
                            {
                                GameActions.RequestPartyQuit(_gump.World.Player);

                                //for (int i = 0; i < World.Party.Members.Length; i++)
                                //{
                                //    if (World.Party.Members[i] != null && World.Party.Members[i].Serial != 0)
                                //        GameActions.RequestPartyRemoveMember(World.Party.Members[i].Serial);
                                //}
                            }

                            break;

                        case "accept":

                            if (_gump.World.Party.Leader == 0 && (_gump.World.Party.Inviter != 0))
                            {
                                GameActions.RequestPartyAccept(_gump.World.Party.Inviter);
                                _gump.World.Party.Leader = _gump.World.Party.Inviter;
                                _gump.World.Party.Inviter = 0;
                            }
                            else
                            {
                                _gump.World.MessageManager.HandleMessage
                                (
                                    null,
                                    ResGumps.NoOneHasInvitedYouToBeInAParty,
                                    "System",
                                    0xFFFF,
                                    MessageType.Regular,
                                    3,
                                    TextType.SYSTEM
                                );
                            }

                            break;

                        case "decline":

                            if (_gump.World.Party.Leader == 0 && _gump.World.Party.Inviter != 0)
                            {
                                NetClient.Socket.Send_PartyDecline(_gump.World.Party.Inviter);
                                _gump.World.Party.Leader = 0;
                                _gump.World.Party.Inviter = 0;
                            }
                            else
                            {
                                _gump.World.MessageManager.HandleMessage
                                (
                                    null,
                                    ResGumps.NoOneHasInvitedYouToBeInAParty,
                                    "System",
                                    0xFFFF,
                                    MessageType.Regular,
                                    3,
                                    TextType.SYSTEM
                                );
                            }


                            break;

                        case "rem":

                            if (_gump.World.Party.Leader != 0 && _gump.World.Party.Leader == _gump.World.Player)
                            {
                                GameActions.RequestPartyRemoveMemberByTarget();
                            }
                            else
                            {
                                _gump.World.MessageManager.HandleMessage
                                (
                                    null,
                                    ResGumps.YouAreNotPartyLeader,
                                    "System",
                                    0xFFFF,
                                    MessageType.Regular,
                                    3,
                                    TextType.SYSTEM
                                );
                            }


                            break;

                        default:

                            if (_gump.World.Party.Leader != 0)
                            {
                                uint serial = 0;

                                int pos = 0;

                                while (pos < text.Length && text[pos] != ' ')
                                {
                                    pos++;
                                }

                                if (pos < text.Length)
                                {
                                    if (int.TryParse(text.Substring(0, pos), out int index) && index > 0 && index < 11 && _gump.World.Party.Members[index - 1] != null && _gump.World.Party.Members[index - 1].Serial != 0)
                                    {
                                        serial = _gump.World.Party.Members[index - 1].Serial;
                                    }
                                }

                                GameActions.SayParty(text, serial);
                            }
                            else
                            {
                                GameActions.Print
                                (
                                    _gump.World,
                                    string.Format(ResGumps.NoteToSelf0, text),
                                    0,
                                    MessageType.System,
                                    3,
                                    false
                                );
                            }

                            break;
                    }

                    break;

                case ChatMode.Guild:
                    GameActions.Say(text, ProfileManager.CurrentProfile.GuildMessageHue, MessageType.Guild);

                    break;

                case ChatMode.Alliance:
                    GameActions.Say(text, ProfileManager.CurrentProfile.AllyMessageHue, MessageType.Alliance);

                    break;

                case ChatMode.ClientCommand:
                    string[] tt = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (tt.Length != 0)
                    {
                        _gump.World.CommandManager.Execute(tt[0], tt);
                    }

                    break;

                case ChatMode.UOAMChat:
                    _gump.World.UoAssist.SignalMessage(text);

                    break;

                case ChatMode.UOChat:
                    NetClient.Socket.Send_ChatMessageCommand(text);

                    break;
            }
        }

        // -------------------------------------------------------------------
        // Autocomplete / history / persistence
        // -------------------------------------------------------------------

        private bool TryHandleTabComplete()
        {
            string text = TextBoxControl.Text ?? string.Empty;
            int caret = Math.Clamp(TextBoxControl.CaretIndex, 0, text.Length);

            // Find the token enclosing the caret. Tokens are whitespace-separated.
            int tokenStart = caret;
            while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1]))
            {
                tokenStart--;
            }

            int tokenEnd = caret;
            while (tokenEnd < text.Length && !char.IsWhiteSpace(text[tokenEnd]))
            {
                tokenEnd++;
            }

            // Only autocomplete the first token, and only if it begins with the command prefix.
            if (tokenStart != 0)
            {
                return false;
            }

            string token = text.Substring(tokenStart, tokenEnd - tokenStart);
            if (token.Length == 0 || token[0] != ServerCommandRegistry.Prefix)
            {
                return false;
            }

            string bareOriginal = token.Substring(1);

            // On first Tab (or after any edit) we scan with the currently-typed prefix.
            // On subsequent Tabs we keep cycling against the *original* prefix so the user
            // can walk the list without the replacement narrowing the match set to 1.
            string scanPrefix;
            if (_tabCycleIndex < 0 || _tabCycleOrigToken == null)
            {
                scanPrefix = bareOriginal;
                _tabCycleOrigToken = bareOriginal;
                _tabCycleIndex = -1;
            }
            else
            {
                scanPrefix = _tabCycleOrigToken;
            }

            ServerCommandRegistry.Match(scanPrefix, 64, _suggestionBuffer);
            if (_suggestionBuffer.Count == 0)
            {
                return false;
            }

            _tabCycleIndex = (_tabCycleIndex + 1) % _suggestionBuffer.Count;
            string match = _suggestionBuffer[_tabCycleIndex];

            string replacement = ServerCommandRegistry.Prefix + match;
            string newText = replacement + text.Substring(tokenEnd);

            // Detach handler to avoid resetting our tab cycle state while we programmatically set text.
            TextBoxControl.TextChanged -= TextBoxControl_TextChanged;
            TextBoxControl.SetText(newText);
            TextBoxControl.CaretIndex = replacement.Length;
            TextBoxControl.TextChanged += TextBoxControl_TextChanged;

            RecalculateHuesAndSizes();
            RefreshSuggestionLabel();
            return true;
        }

        private void RefreshSuggestionLabel()
        {
            if (_suggestionLabel == null)
            {
                return;
            }

            string text = TextBoxControl.Text ?? string.Empty;

            if (text.Length == 0 || text[0] != ServerCommandRegistry.Prefix || ServerCommandRegistry.Count == 0)
            {
                if (_suggestionLabel.IsVisible)
                {
                    _suggestionLabel.IsVisible = false;
                    Resize();
                }
                return;
            }

            // Extract first token after the prefix.
            int end = 1;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }
            string bare = text.Substring(1, end - 1);

            ServerCommandRegistry.Match(bare, MAX_SUGGESTIONS, _suggestionBuffer);
            if (_suggestionBuffer.Count == 0)
            {
                if (_suggestionLabel.IsVisible)
                {
                    _suggestionLabel.IsVisible = false;
                    Resize();
                }
                return;
            }

            var sb = new StringBuilder(128);
            for (int i = 0; i < _suggestionBuffer.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append("  ");
                }
                sb.Append(ServerCommandRegistry.Prefix);
                sb.Append(_suggestionBuffer[i]);
            }

            _suggestionLabel.Text = sb.ToString();
            _suggestionLabel.IsVisible = true;
            Resize();
        }

        private static bool IsHistoryEntryAllowed(Tuple<ChatMode, string> entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Item2))
            {
                return false;
            }

            string text = entry.Item2;
            if (text[0] != ServerCommandRegistry.Prefix)
            {
                // Not a server command — always allowed (party, guild, etc. are mode-based).
                return true;
            }

            // Extract first token after prefix.
            int end = 1;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }
            string bare = text.Substring(1, end - 1);
            return ServerCommandRegistry.Contains(bare);
        }

        private static int FindPrevAllowedHistoryIndex(int fromIdx)
        {
            // Ctrl+Q in the existing code walks backwards but never decrements on
            // the very first press (it replays the current index). Mirror that:
            // if the current index is already at an allowed entry, keep it.
            if (fromIdx >= 0 && fromIdx < _messageHistory.Count &&
                IsHistoryEntryAllowed(_messageHistory[fromIdx]))
            {
                // Same behavior as the original: decrement only if room.
                int start = fromIdx > 0 ? fromIdx - 1 : fromIdx;
                for (int i = start; i >= 0; i--)
                {
                    if (IsHistoryEntryAllowed(_messageHistory[i]))
                    {
                        return i;
                    }
                }
                return fromIdx;
            }

            for (int i = Math.Min(fromIdx, _messageHistory.Count - 1); i >= 0; i--)
            {
                if (IsHistoryEntryAllowed(_messageHistory[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        private static int FindNextAllowedHistoryIndex(int fromIdx)
        {
            for (int i = fromIdx + 1; i < _messageHistory.Count; i++)
            {
                if (IsHistoryEntryAllowed(_messageHistory[i]))
                {
                    return i;
                }
            }
            return _messageHistory.Count; // signals "past the end — clear text"
        }

        private static void LoadHistoryFromDisk()
        {
            string path = GetHistoryPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Already loaded for this profile? Don't re-load (e.g. if the chat
            // control is reconstructed without a logout in between).
            if (_loadedHistoryPath == path && _messageHistory.Count > 0)
            {
                return;
            }

            _messageHistory.Clear();
            _messageHistoryIndex = -1;
            _loadedHistoryPath = path;

            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    int pipe = line.IndexOf('|');
                    if (pipe <= 0 || pipe >= line.Length - 1)
                    {
                        continue;
                    }

                    if (!Enum.TryParse(line.Substring(0, pipe), out ChatMode mode))
                    {
                        continue;
                    }

                    _messageHistory.Add(new Tuple<ChatMode, string>(mode, line.Substring(pipe + 1)));

                    if (_messageHistory.Count >= MAX_HISTORY)
                    {
                        break;
                    }
                }
            }
            catch
            {
                // Corrupt history file — wipe and carry on.
                _messageHistory.Clear();
            }

            _messageHistoryIndex = _messageHistory.Count;
        }

        private static void SaveHistoryToDisk()
        {
            string path = GetHistoryPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                int start = Math.Max(0, _messageHistory.Count - MAX_HISTORY);
                var sb = new StringBuilder(_messageHistory.Count * 32);
                for (int i = start; i < _messageHistory.Count; i++)
                {
                    var e = _messageHistory[i];
                    if (e == null || string.IsNullOrEmpty(e.Item2))
                    {
                        continue;
                    }
                    // Strip newlines defensively — the on-disk format is one entry per line.
                    string text = e.Item2.Replace('\n', ' ').Replace('\r', ' ');
                    sb.Append(e.Item1.ToString()).Append('|').Append(text).Append('\n');
                }

                File.WriteAllText(path, sb.ToString());
            }
            catch
            {
                // I/O failure is non-fatal — history is a convenience.
            }
        }

        private static string GetHistoryPath()
        {
            string profilePath = ProfileManager.ProfilePath;
            if (string.IsNullOrEmpty(profilePath))
            {
                return null;
            }
            return Path.Combine(profilePath, HISTORY_FILENAME);
        }

        private class ChatLineTime
        {
            private uint _createdTime;
            private RenderedText _renderedText;

            public ChatLineTime(string text, byte font, bool isunicode, ushort hue)
            {
                _renderedText = RenderedText.Create
                (
                    text,
                    hue,
                    font,
                    isunicode,
                    FontStyle.BlackBorder,
                    maxWidth: 320
                );
                _createdTime = Time.Ticks + Constants.TIME_DISPLAY_SYSTEM_MESSAGE_TEXT;
            }

            private string Text => _renderedText?.Text ?? string.Empty;

            public bool IsDisposed => _renderedText == null || _renderedText.IsDestroyed;

            public int TextHeight => _renderedText?.Height ?? 0;

            public void Update()
            {
                if (Time.Ticks > _createdTime)
                {
                    Destroy();
                }
            }


            public bool Draw(UltimaBatcher2D batcher, int x, int y, float depth, float scale = 1f)
            {
                return !IsDisposed && _renderedText.Draw(batcher, x, y, depth, scale: scale /*, ShaderHueTranslator.GetHueVector(0, false, _alpha, true)*/);
            }

            public override string ToString()
            {
                return Text;
            }

            public void Destroy()
            {
                if (!IsDisposed)
                {
                    _renderedText?.Destroy();
                    _renderedText = null;
                }
            }
        }
    }
}