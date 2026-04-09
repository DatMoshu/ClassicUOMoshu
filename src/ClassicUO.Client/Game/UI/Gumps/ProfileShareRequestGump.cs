// SPDX-License-Identifier: BSD-2-Clause

// PP.A-Share — Profile share approval gump
// ------------------------------------------
// Shown when another player uses [shareprofile and targets you.
// Displays: "[SenderName] wants to share their profile with you. Save it to your profiles?"
// Accept → saves the share code to journal so the player can import it later
// Decline → dismisses

using ClassicUO.Configuration;
using ClassicUO.Game.UI.Controls;
using SDL3;

namespace ClassicUO.Game.UI.Gumps
{
    internal class ProfileShareRequestGump : Gump
    {
        private readonly string _senderName;
        private readonly string _shareCode;

        public ProfileShareRequestGump(World world, string senderName, string shareCode)
            : base(world, 0, 0)
        {
            _senderName = senderName;
            _shareCode  = shareCode;

            CanCloseWithRightClick = true;
            IsModal = false;

            const int W = 340;
            const int H = 120;

            int cx = Client.Game.Scene.Camera.Bounds.Width  / 2 - W / 2;
            int cy = 180;

            Add(new AlphaBlendControl(0.88f) { X = cx, Y = cy, Width = W, Height = H });

            string msg = $"{senderName} wants to share their profile with you.";
            Add(new Label(msg, true, 0x0035) { X = cx + 12, Y = cy + 12 });
            Add(new Label("Save it to your profiles?", true, 0xFFFF) { X = cx + 12, Y = cy + 32 });

            NiceButton acceptBtn = new NiceButton
            (
                cx + W - 112,
                cy + H - 34,
                50, 25,
                ButtonAction.Activate,
                "Accept"
            ) { IsSelectable = false };

            acceptBtn.MouseUp += (_, _) => OnAccept();
            Add(acceptBtn);

            NiceButton declineBtn = new NiceButton
            (
                cx + W - 58,
                cy + H - 34,
                50, 25,
                ButtonAction.Activate,
                "Decline"
            ) { IsSelectable = false };

            declineBtn.MouseUp += (_, _) => Dispose();
            Add(declineBtn);
        }

        private void OnAccept()
        {
            // Copy the code to clipboard so the player can paste it in the import field
            SDL.SDL_SetClipboardText(_shareCode);

            // Also write the code to the share-codes file for later use
            SaveCodeLocally();

            // Print to journal
            GameActions.Print(World,
                $"Profile code from {_senderName} saved! Use Options → Profiles → Import to apply it.");
            GameActions.Print(World, $"Code: {ProfileCodeGenerator.Format(_shareCode)}");
            GameActions.Print(World, "(Code copied to clipboard)");

            Dispose();
        }

        private void SaveCodeLocally()
        {
            if (string.IsNullOrEmpty(ProfileManager.ProfilePath)) return;

            string codesFile = System.IO.Path.Combine(
                ProfileManager.ProfilePath, "received-profiles.txt");

            try
            {
                System.IO.File.AppendAllText(codesFile,
                    $"{System.DateTime.UtcNow:yyyy-MM-dd HH:mm} | from {_senderName} | {_shareCode}\n");
            }
            catch
            {
                // Non-critical — clipboard copy already succeeded
            }
        }
    }
}
