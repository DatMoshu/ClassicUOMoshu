// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading.Tasks;
using ClassicUO.Assets;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Renderer;
using ClassicUO.Utility.Collections;
using SDL3;

namespace ClassicUO.SpeechRecognition.Gumps
{
    /// <summary>
    /// Stone-framed chat gump for talking to the LLM Avatar.
    /// Uses ResizePic(0x0A28) stone background with gem corners.
    /// Open with: -llm   (or -llm &lt;auto-submit prompt&gt;)
    /// </summary>
    internal sealed class LlmChatGump : Gump
    {
        // ── Layout ────────────────────────────────────────────────────────────
        private const int W          = 360;
        private const int H          = 430;
        private const int MARGIN     = 14;
        private const int TITLE_H    = 32;
        private const int INPUT_H    = 26;
        private const int BTN_W      = 52;
        private const int BORDER     = 3;
        private const int CHAT_TOP   = TITLE_H + MARGIN;
        private const int CHAT_H     = H - CHAT_TOP - MARGIN - BORDER * 2 - INPUT_H - MARGIN;
        private const int CHAT_W     = W - MARGIN * 2 - 20; // 20 = scrollbar space
        private const int INPUT_TOP  = CHAT_TOP + CHAT_H + BORDER * 2 + MARGIN / 2;

        // ── Colors ────────────────────────────────────────────────────────────
        private const ushort HUE_YOU    = 0x0059; // bright yellow — player
        private const ushort HUE_AVATAR = 0x0481; // gold — avatar
        private const ushort HUE_SYS    = 0x0386; // grey-blue — system
        private const ushort HUE_TITLE  = 0x0035; // bright white

        // ── Controls ─────────────────────────────────────────────────────────
        private readonly ScrollFlag    _scrollBar;
        private readonly ChatTextList  _chatList;
        private readonly StbTextBox    _inputBox;
        private readonly NiceButton    _sendBtn;
        private bool _waiting;

        // ── LLM callback ─────────────────────────────────────────────────────
        private readonly Func<string, Task<string>> _submit;

        public LlmChatGump(World world, Func<string, Task<string>> submit)
            : base(world, 0xFAC0_0002, 0)
        {
            _submit = submit;
            Width   = W;
            Height  = H;
            CanMove = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;

            // ── Stone background (ResizePic 0x0A28 has built-in gem corners) ─
            Add(new ResizePic(0x0A28) { Width = W, Height = H });


            // ── Title ─────────────────────────────────────────────────────────
            Add(new Label("Avatar", true, HUE_TITLE, W - MARGIN * 2, 3,
                FontStyle.BlackBorder, TEXT_ALIGN_TYPE.TS_CENTER)
            {
                X = MARGIN,
                Y = MARGIN + 6
            });

            // ── Separator under title ─────────────────────────────────────────
            Add(new Line(MARGIN + 10, CHAT_TOP - 4, W - MARGIN * 2 - 20, 1, 0xFF888888));

            // ── Scrollbar + message list ──────────────────────────────────────
            _scrollBar = new ScrollFlag(W - MARGIN - 14, CHAT_TOP, CHAT_H, true);

            _chatList = new ChatTextList(
                MARGIN,
                CHAT_TOP,
                CHAT_W,
                CHAT_H,
                _scrollBar);

            Add(_chatList);
            Add(_scrollBar);

            // ── Input border + box ────────────────────────────────────────────
            Add(new BorderControl(MARGIN, INPUT_TOP - BORDER, W - MARGIN * 2 - BTN_W - 4, INPUT_H + BORDER * 2, BORDER));

            _inputBox = new StbTextBox(1, maxWidth: CHAT_W - BTN_W - 16, hue: 0xFFFF)
            {
                X      = MARGIN + BORDER + 2,
                Y      = INPUT_TOP + (INPUT_H - 18) / 2,
                Width  = W - MARGIN * 2 - BTN_W - 10 - BORDER * 2,
                Height = 18
            };
            Add(_inputBox);

            // ── Send button ───────────────────────────────────────────────────
            _sendBtn = new NiceButton(
                W - MARGIN - BTN_W + 2,
                INPUT_TOP - BORDER,
                BTN_W - 2,
                INPUT_H + BORDER * 2,
                ButtonAction.Activate,
                "Send",
                hue: HUE_AVATAR)
            {
                ButtonParameter = 1
            };
            _sendBtn.MouseUp += (_, _) => Submit();
            Add(_sendBtn);

            // ── Initial greeting ─────────────────────────────────────────────
            AddSystemMessage("Type a message and press Enter.");
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void AddMessage(string role, string text)
        {
            if (IsDisposed) return;
            switch (role)
            {
                case "user":
                    _chatList.AddEntry($"You: {text}", 1, HUE_YOU, true, DateTime.Now);
                    break;
                case "assistant":
                    _chatList.AddEntry($"Avatar: {text}", 1, HUE_AVATAR, true, DateTime.Now);
                    break;
                default:
                    AddSystemMessage(text);
                    break;
            }
        }

        // ── Input handling ────────────────────────────────────────────────────

        protected override void OnKeyDown(SDL.SDL_Keycode key, SDL.SDL_Keymod mod)
        {
            if (key == SDL.SDL_Keycode.SDLK_RETURN || key == SDL.SDL_Keycode.SDLK_KP_ENTER)
            {
                Submit();
                return;
            }
            base.OnKeyDown(key, mod);
        }

        protected override void OnMouseWheel(MouseEventType delta)
            => _scrollBar.InvokeMouseWheel(delta);

        // ── Submit ────────────────────────────────────────────────────────────

        private void Submit()
        {
            if (_waiting) return;
            string text = _inputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputBox.SetText(string.Empty);
            AddMessage("user", text);
            AddSystemMessage("Thinking...");
            _waiting = true;
            _sendBtn.IsEnabled = false;

            _ = SendAsync(text);
        }

        private async Task SendAsync(string prompt)
        {
            try
            {
                string response = await _submit(prompt);
                Client.Game.EnqueueAction(0, () =>
                {
                    if (IsDisposed) return;
                    _chatList.RemoveLast();
                    AddMessage("assistant", response);
                    _waiting = false;
                    _sendBtn.IsEnabled = true;
                });
            }
            catch (Exception ex)
            {
                Client.Game.EnqueueAction(0, () =>
                {
                    if (IsDisposed) return;
                    _chatList.RemoveLast();
                    AddSystemMessage($"Error: {ex.Message}");
                    _waiting = false;
                    _sendBtn.IsEnabled = true;
                });
            }
        }

        public void AutoSubmit(string prompt)
        {
            _inputBox.SetText(prompt);
            Submit();
        }

        private void AddSystemMessage(string text)
            => _chatList.AddEntry(text, 1, HUE_SYS, true, DateTime.Now);

        // ── Inner: ChatTextList ───────────────────────────────────────────────

        private sealed class ChatTextList : Control
        {
            private readonly Deque<RenderedText> _entries;
            private readonly Deque<RenderedText> _hours;
            private readonly ScrollBarBase       _scrollBar;

            public ChatTextList(int x, int y, int width, int height, ScrollBarBase scrollBar)
            {
                _scrollBar = scrollBar;
                _scrollBar.IsVisible = false;
                AcceptMouseInput = true;
                CanMove = true;
                X = x; Y = y; Width = width; Height = height;
                _entries = new Deque<RenderedText>();
                _hours   = new Deque<RenderedText>();
                WantUpdateSize = false;
            }

            public void AddEntry(string text, int font, ushort hue, bool isUnicode, DateTime time)
            {
                bool atBottom = _scrollBar.Value == _scrollBar.MaxValue;

                while (_entries.Count > 199)
                {
                    _entries.RemoveFromFront().Destroy();
                    _hours.RemoveFromFront().Destroy();
                }

                RenderedText hour = RenderedText.Create(
                    $"{time:t} ", 1150, 1, true, FontStyle.BlackBorder);
                _hours.AddToBack(hour);

                RenderedText entry = RenderedText.Create(
                    text, hue, (byte)font, isUnicode,
                    FontStyle.Indention | FontStyle.BlackBorder,
                    maxWidth: Width - (18 + hour.Width));
                _entries.AddToBack(entry);

                _scrollBar.MaxValue += entry.Height;
                if (atBottom) _scrollBar.Value = _scrollBar.MaxValue;
            }

            public void RemoveLast()
            {
                if (_entries.Count == 0) return;
                var e = _entries.RemoveFromBack();
                var h = _hours.RemoveFromBack();
                _scrollBar.MaxValue = Math.Max(0, _scrollBar.MaxValue - e.Height);
                e.Destroy();
                h.Destroy();
            }

            public override void Update()
            {
                base.Update();
                if (!IsVisible) return;
                _scrollBar.X = X + Width - (_scrollBar.Width >> 1) + 5;
                _scrollBar.Height = Height;
                RecalcScrollMax();
                _scrollBar.IsVisible = _scrollBar.MaxValue > _scrollBar.MinValue;
            }

            private void RecalcScrollMax()
            {
                bool atBottom = _scrollBar.Value == _scrollBar.MaxValue;
                int h = 0;
                for (int i = 0; i < _entries.Count; i++) h += _entries[i].Height;
                h -= _scrollBar.Height;
                if (h > 0)
                {
                    _scrollBar.MaxValue = h;
                    if (atBottom) _scrollBar.Value = h;
                }
                else
                {
                    _scrollBar.MaxValue = 0;
                    _scrollBar.Value = 0;
                }
            }

            public override bool AddToRenderLists(RenderLists renderLists, int x, int y, ref float layerDepthRef)
            {
                base.AddToRenderLists(renderLists, x, y, ref layerDepthRef);
                float depth = layerDepthRef;
                int mx = x, my = y;
                int height = 0;
                int maxheight = _scrollBar.Value + _scrollBar.Height;

                renderLists.AddGumpNoAtlas(batcher =>
                {
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        RenderedText t    = _entries[i];
                        RenderedText hour = _hours[i];

                        if (height + t.Height <= _scrollBar.Value)
                        {
                            height += t.Height;
                        }
                        else if (height + t.Height <= maxheight)
                        {
                            int yy = height - _scrollBar.Value;
                            if (yy < 0)
                            {
                                hour.Draw(batcher, hour.Width, hour.Height, mx, y,
                                    t.Width, t.Height + yy, 0, -yy, depth);
                                t.Draw(batcher, t.Width, t.Height, mx + hour.Width, y,
                                    t.Width, t.Height + yy, 0, -yy, depth);
                                my += t.Height + yy;
                            }
                            else
                            {
                                hour.Draw(batcher, mx, my, depth);
                                t.Draw(batcher, mx + hour.Width, my, depth);
                                my += t.Height;
                            }
                            height += t.Height;
                        }
                        else
                        {
                            int yyy = maxheight - height;
                            hour.Draw(batcher, hour.Width, hour.Height, mx,
                                y + _scrollBar.Height - yyy, t.Width, yyy, 0, 0, depth);
                            t.Draw(batcher, t.Width, t.Height, mx + hour.Width,
                                y + _scrollBar.Height - yyy, t.Width, yyy, 0, 0, depth);
                            break;
                        }
                    }
                    return true;
                });
                return true;
            }

            public override void Dispose()
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    _entries[i].Destroy();
                    _hours[i].Destroy();
                }
                _entries.Clear();
                _hours.Clear();
                base.Dispose();
            }
        }
    }
}
