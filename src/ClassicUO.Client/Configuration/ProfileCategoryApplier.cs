// SPDX-License-Identifier: BSD-2-Clause

// PP.A1 — Explicit property-to-category mapping for selective profile import.
// All copies are direct assignments — no reflection, NativeAOT-safe.
// See design/gdd/player-profile.md and production/sprints/sprint-11.md for spike notes.

using System.Collections.Generic;
using ClassicUO.Game.Data;

namespace ClassicUO.Configuration
{
    /// <summary>
    /// Copies properties from a source <see cref="Profile"/> (loaded from an imported
    /// .uowprofile package) into a target profile (the active profile), applying only
    /// the categories selected by the player.
    ///
    /// All copies are explicit property assignments — no reflection is used.
    /// This is required for NativeAOT compatibility.
    /// </summary>
    internal static class ProfileCategoryApplier
    {
        /// <summary>
        /// Applies selected categories from <paramref name="source"/> into <paramref name="target"/>.
        /// Properties in unselected categories are not modified.
        /// </summary>
        public static void Apply(Profile source, Profile target, ProfileCategory categories)
        {
            if (categories.HasFlag(ProfileCategory.SoundSettings))
                ApplySound(source, target);

            if (categories.HasFlag(ProfileCategory.HueSettings))
                ApplyHues(source, target);

            if (categories.HasFlag(ProfileCategory.GumpLayout))
                ApplyGumpLayout(source, target);

            if (categories.HasFlag(ProfileCategory.VisualSettings))
                ApplyVisual(source, target);

            if (categories.HasFlag(ProfileCategory.CombatSettings))
                ApplyCombat(source, target);

            if (categories.HasFlag(ProfileCategory.TooltipSettings))
                ApplyTooltip(source, target);

            if (categories.HasFlag(ProfileCategory.ChatSettings))
                ApplyChat(source, target);

            if (categories.HasFlag(ProfileCategory.WorldMap))
                ApplyWorldMap(source, target);

            // MacroBindings: handled at file level — macros.xml copied + sanitised by ProfileMacroSanitiser.
        }

        private static void ApplySound(Profile src, Profile tgt)
        {
            tgt.EnableSound                  = src.EnableSound;
            tgt.SoundVolume                  = src.SoundVolume;
            tgt.EnableMusic                  = src.EnableMusic;
            tgt.MusicVolume                  = src.MusicVolume;
            tgt.EnableFootstepsSound         = src.EnableFootstepsSound;
            tgt.EnableCombatMusic            = src.EnableCombatMusic;
            tgt.ReproduceSoundsInBackground  = src.ReproduceSoundsInBackground;
            tgt.EnableSpeechRecognition      = src.EnableSpeechRecognition;
            tgt.EnableProximityChat          = src.EnableProximityChat;
            tgt.SpeechCmdMode                = src.SpeechCmdMode;
            tgt.SpeechCmdPttKey              = src.SpeechCmdPttKey;
            tgt.VoicePttProximityKey         = src.VoicePttProximityKey;
            tgt.VoicePttFactionKey           = src.VoicePttFactionKey;
            tgt.VoicePttGuildKey             = src.VoicePttGuildKey;
            tgt.VoiceMicMuteKey              = src.VoiceMicMuteKey;
            tgt.ProximityAlwaysOn            = src.ProximityAlwaysOn;
        }

        private static void ApplyHues(Profile src, Profile tgt)
        {
            tgt.SpeechHue           = src.SpeechHue;
            tgt.WhisperHue          = src.WhisperHue;
            tgt.EmoteHue            = src.EmoteHue;
            tgt.YellHue             = src.YellHue;
            tgt.PartyMessageHue     = src.PartyMessageHue;
            tgt.GuildMessageHue     = src.GuildMessageHue;
            tgt.AllyMessageHue      = src.AllyMessageHue;
            tgt.ChatMessageHue      = src.ChatMessageHue;
            tgt.InnocentHue         = src.InnocentHue;
            tgt.PartyAuraHue        = src.PartyAuraHue;
            tgt.FriendHue           = src.FriendHue;
            tgt.CriminalHue         = src.CriminalHue;
            tgt.CanAttackHue        = src.CanAttackHue;
            tgt.EnemyHue            = src.EnemyHue;
            tgt.MurdererHue         = src.MurdererHue;
            tgt.BeneficHue          = src.BeneficHue;
            tgt.HarmfulHue          = src.HarmfulHue;
            tgt.NeutralHue          = src.NeutralHue;
            tgt.EnabledSpellHue     = src.EnabledSpellHue;
            tgt.EnabledSpellFormat  = src.EnabledSpellFormat;
            tgt.SpellDisplayFormat  = src.SpellDisplayFormat;
            tgt.PoisonHue           = src.PoisonHue;
            tgt.ParalyzedHue        = src.ParalyzedHue;
            tgt.InvulnerableHue     = src.InvulnerableHue;
        }

        // GumpLayout also replaces gumps.xml from the ZIP — handled by the import orchestrator.
        private static void ApplyGumpLayout(Profile src, Profile tgt)
        {
            tgt.WindowClientBounds                      = src.WindowClientBounds;
            tgt.ContainerDefaultPosition                = src.ContainerDefaultPosition;
            tgt.GameWindowPosition                      = src.GameWindowPosition;
            tgt.GameWindowLock                          = src.GameWindowLock;
            tgt.GameWindowFullSize                      = src.GameWindowFullSize;
            tgt.WindowBorderless                        = src.WindowBorderless;
            tgt.GameWindowSize                          = src.GameWindowSize;
            tgt.TopbarGumpPosition                      = src.TopbarGumpPosition;
            tgt.TopbarGumpIsMinimized                   = src.TopbarGumpIsMinimized;
            tgt.TopbarGumpIsDisabled                    = src.TopbarGumpIsDisabled;
            tgt.OverrideContainerLocation               = src.OverrideContainerLocation;
            tgt.OverrideContainerLocationSetting        = src.OverrideContainerLocationSetting;
            tgt.OverrideContainerLocationPosition       = src.OverrideContainerLocationPosition;
            tgt.HueContainerGumps                       = src.HueContainerGumps;
            tgt.VendorGumpHeight                        = src.VendorGumpHeight;
            tgt.DefaultScale                            = src.DefaultScale;
            tgt.EnableMousewheelScaleZoom               = src.EnableMousewheelScaleZoom;
            tgt.SaveScaleAfterClose                     = src.SaveScaleAfterClose;
            tgt.RestoreScaleAfterUnpressCtrl            = src.RestoreScaleAfterUnpressCtrl;
            tgt.CloseHealthBarType                      = src.CloseHealthBarType;
            tgt.SaveHealthbars                          = src.SaveHealthbars;
            tgt.CustomBarsToggled                       = src.CustomBarsToggled;
            tgt.CBBlackBGToggled                        = src.CBBlackBGToggled;
            tgt.ShowInfoBar                             = src.ShowInfoBar;
            tgt.InfoBarHighlightType                    = src.InfoBarHighlightType;
            tgt.CounterBarEnabled                       = src.CounterBarEnabled;
            tgt.CounterBarHighlightOnChange             = src.CounterBarHighlightOnChange;
            tgt.CounterBarHighlightOnAmount             = src.CounterBarHighlightOnAmount;
            tgt.CounterBarDisplayAbbreviatedAmount      = src.CounterBarDisplayAbbreviatedAmount;
            tgt.CounterBarAbbreviatedAmount             = src.CounterBarAbbreviatedAmount;
            tgt.CounterBarHighlightAmount               = src.CounterBarHighlightAmount;
            tgt.CounterBarCellSize                      = src.CounterBarCellSize;
            tgt.NameOverheadTypeAllowed                 = src.NameOverheadTypeAllowed;
            tgt.NameOverheadToggled                     = src.NameOverheadToggled;
            tgt.NameOverheadShowGump                    = src.NameOverheadShowGump;
            tgt.NameOverheadShowHpBar                   = src.NameOverheadShowHpBar;
        }

        private static void ApplyVisual(Profile src, Profile tgt)
        {
            tgt.EnableStatReport                = src.EnableStatReport;
            tgt.EnableSkillReport               = src.EnableSkillReport;
            tgt.UseOldStatusGump                = src.UseOldStatusGump;
            tgt.StatusGumpBarMutuallyExclusive  = src.StatusGumpBarMutuallyExclusive;
            tgt.BackpackStyle                   = src.BackpackStyle;
            tgt.HighlightGameObjects            = src.HighlightGameObjects;
            tgt.HighlightMobilesByParalize      = src.HighlightMobilesByParalize;
            tgt.HighlightMobilesByPoisoned      = src.HighlightMobilesByPoisoned;
            tgt.HighlightMobilesByInvul         = src.HighlightMobilesByInvul;
            tgt.ShowMobilesHP                   = src.ShowMobilesHP;
            tgt.MobileHPType                    = src.MobileHPType;
            tgt.MobileHPShowWhen                = src.MobileHPShowWhen;
            tgt.DrawRoofs                       = src.DrawRoofs;
            tgt.TreeToStumps                    = src.TreeToStumps;
            tgt.EnableCaveBorder                = src.EnableCaveBorder;
            tgt.HideVegetation                  = src.HideVegetation;
            tgt.FieldsType                      = src.FieldsType;
            tgt.NoColorObjectsOutOfRange        = src.NoColorObjectsOutOfRange;
            tgt.UseCircleOfTransparency         = src.UseCircleOfTransparency;
            tgt.CircleOfTransparencyRadius      = src.CircleOfTransparencyRadius;
            tgt.CircleOfTransparencyType        = src.CircleOfTransparencyType;
            tgt.EnableDeathScreen               = src.EnableDeathScreen;
            tgt.EnableBlackWhiteEffect          = src.EnableBlackWhiteEffect;
            tgt.UseAlternativeLights            = src.UseAlternativeLights;
            tgt.UseCustomLightLevel             = src.UseCustomLightLevel;
            tgt.LightLevel                      = src.LightLevel;
            tgt.LightLevelType                  = src.LightLevelType;
            tgt.UseColoredLights                = src.UseColoredLights;
            tgt.UseDarkNights                   = src.UseDarkNights;
            tgt.ShadowsEnabled                  = src.ShadowsEnabled;
            tgt.ShadowsStatics                  = src.ShadowsStatics;
            tgt.TerrainShadowsLevel             = src.TerrainShadowsLevel;
            tgt.AuraUnderFeetType               = src.AuraUnderFeetType;
            tgt.AuraOnMouse                     = src.AuraOnMouse;
            tgt.PartyAura                       = src.PartyAura;
            tgt.AnimatedWaterEffect             = src.AnimatedWaterEffect;
            tgt.UseXBR                          = src.UseXBR;
            tgt.UseObjectsFading                = src.UseObjectsFading;
            tgt.TextFading                      = src.TextFading;
            tgt.ShowNewMobileNameIncoming       = src.ShowNewMobileNameIncoming;
            tgt.ShowNewCorpseNameIncoming       = src.ShowNewCorpseNameIncoming;
            tgt.ReduceFPSWhenInactive           = src.ReduceFPSWhenInactive;
            tgt.OverrideAllFonts                = src.OverrideAllFonts;
            tgt.OverrideAllFontsIsUnicode       = src.OverrideAllFontsIsUnicode;
            tgt.ContainersScale                 = src.ContainersScale;
            tgt.ScaleItemsInsideContainers      = src.ScaleItemsInsideContainers;
            tgt.UseLargeContainerGumps          = src.UseLargeContainerGumps;
            tgt.HighlightContainerWhenSelected  = src.HighlightContainerWhenSelected;
            tgt.ShowDPSWithDamageNumbers        = src.ShowDPSWithDamageNumbers;
            tgt.UseSmoothBoatMovement           = src.UseSmoothBoatMovement;
            tgt.BuffBarTime                     = src.BuffBarTime;
            tgt.HideChatGradient                = src.HideChatGradient;
            tgt.StandardSkillsGump              = src.StandardSkillsGump;
        }

        private static void ApplyCombat(Profile src, Profile tgt)
        {
            tgt.EnabledCriminalActionQuery              = src.EnabledCriminalActionQuery;
            tgt.EnabledBeneficialCriminalActionQuery    = src.EnabledBeneficialCriminalActionQuery;
            tgt.EnablePathfind                          = src.EnablePathfind;
            tgt.UseShiftToPathfind                      = src.UseShiftToPathfind;
            tgt.AlwaysRun                               = src.AlwaysRun;
            tgt.AlwaysRunUnlessHidden                   = src.AlwaysRunUnlessHidden;
            tgt.FastRotation                            = src.FastRotation;
            tgt.SmoothMovements                         = src.SmoothMovements;
            tgt.HoldDownKeyTab                          = src.HoldDownKeyTab;
            tgt.HoldShiftForContext                     = src.HoldShiftForContext;
            tgt.HoldShiftToSplitStack                   = src.HoldShiftToSplitStack;
            tgt.EnableDragSelect                        = src.EnableDragSelect;
            tgt.DragSelectModifierKey                   = src.DragSelectModifierKey;
            tgt.DragSelectHumanoidsOnly                 = src.DragSelectHumanoidsOnly;
            tgt.DragSelectHostileOnly                   = src.DragSelectHostileOnly;
            tgt.DragSelectStartX                        = src.DragSelectStartX;
            tgt.DragSelectStartY                        = src.DragSelectStartY;
            tgt.DragSelectAsAnchor                      = src.DragSelectAsAnchor;
            tgt.ShowTargetRangeIndicator                = src.ShowTargetRangeIndicator;
            tgt.UseNewTargetSystem                      = src.UseNewTargetSystem;
            tgt.BandageSelfOld                          = src.BandageSelfOld;
            tgt.IgnoreStaminaCheck                      = src.IgnoreStaminaCheck;
            tgt.AutoOpenDoors                           = src.AutoOpenDoors;
            tgt.SmoothDoors                             = src.SmoothDoors;
            tgt.AutoOpenCorpses                         = src.AutoOpenCorpses;
            tgt.AutoOpenCorpseRange                     = src.AutoOpenCorpseRange;
            tgt.CorpseOpenOptions                       = src.CorpseOpenOptions;
            tgt.SkipEmptyCorpse                         = src.SkipEmptyCorpse;
            tgt.DisableDefaultHotkeys                   = src.DisableDefaultHotkeys;
            tgt.DisableArrowBtn                         = src.DisableArrowBtn;
            tgt.DisableTabBtn                           = src.DisableTabBtn;
            tgt.DisableCtrlQWBtn                        = src.DisableCtrlQWBtn;
            tgt.DisableAutoMove                         = src.DisableAutoMove;
            tgt.HoldDownKeyAltToCloseAnchored           = src.HoldDownKeyAltToCloseAnchored;
            tgt.CloseAllAnchoredGumpsInGroupWithRightClick = src.CloseAllAnchoredGumpsInGroupWithRightClick;
            tgt.HoldAltToMoveGumps                      = src.HoldAltToMoveGumps;
            tgt.GrabBagSerial                           = src.GrabBagSerial;
            tgt.SallosEasyGrab                          = src.SallosEasyGrab;
            tgt.CastSpellsByOneClick                    = src.CastSpellsByOneClick;
            tgt.FastSpellsAssign                        = src.FastSpellsAssign;
            tgt.UseKrEquipUnequipPacket                 = src.UseKrEquipUnequipPacket;
            tgt.DoubleClickToLootInsideContainers       = src.DoubleClickToLootInsideContainers;
            tgt.RelativeDragAndDropItems                = src.RelativeDragAndDropItems;
            tgt.GridLootType                            = src.GridLootType;
            tgt.PartyInviteGump                         = src.PartyInviteGump;
        }

        private static void ApplyTooltip(Profile src, Profile tgt)
        {
            tgt.UseTooltip                  = src.UseTooltip;
            tgt.TooltipTextHue              = src.TooltipTextHue;
            tgt.TooltipDelayBeforeDisplay   = src.TooltipDelayBeforeDisplay;
            tgt.TooltipDisplayZoom          = src.TooltipDisplayZoom;
            tgt.TooltipBackgroundOpacity    = src.TooltipBackgroundOpacity;
            tgt.TooltipFont                 = src.TooltipFont;
        }

        private static void ApplyChat(Profile src, Profile tgt)
        {
            tgt.ChatFont                        = src.ChatFont;
            tgt.SpeechDelay                     = src.SpeechDelay;
            tgt.ScaleSpeechDelay                = src.ScaleSpeechDelay;
            tgt.SaveJournalToFile               = src.SaveJournalToFile;
            tgt.ForceUnicodeJournal             = src.ForceUnicodeJournal;
            tgt.IgnoreAllianceMessages          = src.IgnoreAllianceMessages;
            tgt.IgnoreGuildMessages             = src.IgnoreGuildMessages;
            tgt.ShowJournalClient               = src.ShowJournalClient;
            tgt.ShowJournalObjects              = src.ShowJournalObjects;
            tgt.ShowJournalSystem               = src.ShowJournalSystem;
            tgt.ShowJournalGuildAlly            = src.ShowJournalGuildAlly;
            tgt.UseAlternateJournal             = src.UseAlternateJournal;
            tgt.OverheadPartyMessages           = src.OverheadPartyMessages;
            tgt.ActivateChatAfterEnter          = src.ActivateChatAfterEnter;
            tgt.ActivateChatAdditionalButtons   = src.ActivateChatAdditionalButtons;
            tgt.ActivateChatShiftEnterSupport   = src.ActivateChatShiftEnterSupport;
            tgt.JournalDarkMode                 = src.JournalDarkMode;
            tgt.ShowSkillsChangedMessage        = src.ShowSkillsChangedMessage;
            tgt.ShowSkillsChangedDeltaValue     = src.ShowSkillsChangedDeltaValue;
            tgt.ShowStatsChangedMessage         = src.ShowStatsChangedMessage;

            // JournalTabs has no setter — copy contents explicitly.
            tgt.JournalTabs.Clear();
            foreach (KeyValuePair<string, MessageType[]> kvp in src.JournalTabs)
                tgt.JournalTabs[kvp.Key] = kvp.Value;
        }

        private static void ApplyWorldMap(Profile src, Profile tgt)
        {
            tgt.WorldMapWidth                   = src.WorldMapWidth;
            tgt.WorldMapHeight                  = src.WorldMapHeight;
            tgt.WorldMapFont                    = src.WorldMapFont;
            tgt.WorldMapFlipMap                 = src.WorldMapFlipMap;
            tgt.WorldMapTopMost                 = src.WorldMapTopMost;
            tgt.WorldMapFreeView                = src.WorldMapFreeView;
            tgt.WorldMapShowParty               = src.WorldMapShowParty;
            tgt.WorldMapZoomIndex               = src.WorldMapZoomIndex;
            tgt.WorldMapShowCoordinates         = src.WorldMapShowCoordinates;
            tgt.WorldMapShowMouseCoordinates    = src.WorldMapShowMouseCoordinates;
            tgt.WorldMapShowSextantCoordinates  = src.WorldMapShowSextantCoordinates;
            tgt.WorldMapShowMobiles             = src.WorldMapShowMobiles;
            tgt.WorldMapShowPlayerName          = src.WorldMapShowPlayerName;
            tgt.WorldMapShowPlayerBar           = src.WorldMapShowPlayerBar;
            tgt.WorldMapShowGroupName           = src.WorldMapShowGroupName;
            tgt.WorldMapShowGroupBar            = src.WorldMapShowGroupBar;
            tgt.WorldMapShowMarkers             = src.WorldMapShowMarkers;
            tgt.WorldMapShowMarkersNames        = src.WorldMapShowMarkersNames;
            tgt.WorldMapShowMultis              = src.WorldMapShowMultis;
            tgt.WorldMapHiddenMarkerFiles       = src.WorldMapHiddenMarkerFiles;
            tgt.WorldMapHiddenZoneFiles         = src.WorldMapHiddenZoneFiles;
            tgt.WorldMapShowGridIfZoomed        = src.WorldMapShowGridIfZoomed;
            tgt.WorldMapAllowPositionalTarget   = src.WorldMapAllowPositionalTarget;
        }
    }
}
