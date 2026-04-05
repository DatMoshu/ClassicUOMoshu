// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;

namespace ClassicUO.Game.Managers
{
    internal static class SpeechMacroStrings
    {
        public static readonly Dictionary<MacroType, Dictionary<MacroSubType, string[]>> MacroSpeechCommands = new()
        {
            {
                MacroType.Open, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Paperdoll, new[] { "paperdoll", "open paperdoll", "show paperdoll", "character", "open character", "show character", "paper doll", "open paper doll", "char screen" } },
                    { MacroSubType.Configuration, new[] { "settings", "open settings", "configuration", "open configuration", "options", "open options", "show options", "game options", "game settings" } },
                    { MacroSubType.Status, new[] { "status", "open status", "show status", "stats", "show stats", "my stats" } },
                    { MacroSubType.Journal, new[] { "journal", "open journal", "show journal", "chat log", "show chat log" } },
                    { MacroSubType.Skills, new[] { "skills", "open skills", "show skills" } },
                    { MacroSubType.MageSpellbook, new[] { "spellbook", "open spellbook", "mage spellbook" } },
                    { MacroSubType.Chat, new[] { "chat", "open chat", "show chat" } },
                    { MacroSubType.Backpack, new[] { "backpack", "open backpack", "show backpack", "inventory", "open inventory", "show inventory" } },
                    { MacroSubType.Overview, new[] { "overview", "open overview", "show overview" } },
                    { MacroSubType.WorldMap, new[] { "world map", "open world map", "show world map", "map", "open map", "show map", "mini map", "minimap", "open minimap" } },
                    { MacroSubType.Mail, new[] { "mail", "open mail", "show mail" } },
                    { MacroSubType.PartyManifest, new[] { "party", "open party", "show party" } },
                    { MacroSubType.Guild, new[] { "guild", "open guild", "show guild" } },
                    { MacroSubType.QuestLog, new[] { "quest log", "open quest log", "show quest log", "quests", "open quests", "show quests", "my quests" } }
                }
            },
            {
                MacroType.Close, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Paperdoll, new[] { "close paperdoll", "hide paperdoll" } },
                    { MacroSubType.Configuration, new[] { "close settings", "close configuration", "hide settings", "hide configuration" } },
                    { MacroSubType.Status, new[] { "close status", "hide status" } },
                    { MacroSubType.Journal, new[] { "close journal", "hide journal" } },
                    { MacroSubType.Skills, new[] { "close skills", "hide skills" } },
                    { MacroSubType.MageSpellbook, new[] { "close spellbook", "hide spellbook" } },
                    { MacroSubType.Chat, new[] { "close chat", "hide chat" } },
                    { MacroSubType.Backpack, new[] { "close backpack", "hide backpack" } },
                    { MacroSubType.Overview, new[] { "close overview", "hide overview" } },
                    { MacroSubType.WorldMap, new[] { "close world map", "hide world map" } },
                    { MacroSubType.Mail, new[] { "close mail", "hide mail" } },
                    { MacroSubType.PartyManifest, new[] { "close party", "hide party" } },
                    { MacroSubType.Guild, new[] { "close guild", "hide guild" } },
                    { MacroSubType.QuestLog, new[] { "close quest log", "hide quest log" } }
                }
            },
            {
                MacroType.Minimize, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Paperdoll, new[] { "minimize paperdoll" } },
                    { MacroSubType.Configuration, new[] { "minimize settings", "minimize configuration" } },
                    { MacroSubType.Status, new[] { "minimize status" } },
                    { MacroSubType.Journal, new[] { "minimize journal" } },
                    { MacroSubType.Skills, new[] { "minimize skills" } },
                    { MacroSubType.MageSpellbook, new[] { "minimize spellbook" } },
                    { MacroSubType.Chat, new[] { "minimize chat" } },
                    { MacroSubType.Backpack, new[] { "minimize backpack" } },
                    { MacroSubType.Overview, new[] { "minimize overview" } },
                    { MacroSubType.WorldMap, new[] { "minimize world map" } },
                    { MacroSubType.Mail, new[] { "minimize mail" } },
                    { MacroSubType.PartyManifest, new[] { "minimize party" } },
                    { MacroSubType.Guild, new[] { "minimize guild" } },
                    { MacroSubType.QuestLog, new[] { "minimize quest log" } }
                }
            },
            {
                MacroType.Maximize, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Paperdoll, new[] { "maximize paperdoll" } },
                    { MacroSubType.Configuration, new[] { "maximize settings", "maximize configuration" } },
                    { MacroSubType.Status, new[] { "maximize status" } },
                    { MacroSubType.Journal, new[] { "maximize journal" } },
                    { MacroSubType.Skills, new[] { "maximize skills" } },
                    { MacroSubType.MageSpellbook, new[] { "maximize spellbook" } },
                    { MacroSubType.Chat, new[] { "maximize chat" } },
                    { MacroSubType.Backpack, new[] { "maximize backpack" } },
                    { MacroSubType.Overview, new[] { "maximize overview" } },
                    { MacroSubType.WorldMap, new[] { "maximize world map" } },
                    { MacroSubType.Mail, new[] { "maximize mail" } },
                    { MacroSubType.PartyManifest, new[] { "maximize party" } },
                    { MacroSubType.Guild, new[] { "maximize guild" } },
                    { MacroSubType.QuestLog, new[] { "maximize quest log" } }
                }
            },
            {
                MacroType.Say, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "say", "speak", "utter" } }
                }
            },
            {
                MacroType.Emote, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "emote", "express", "show emotion" } }
                }
            },
            {
                MacroType.Whisper, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "whisper", "speak softly", "murmur" } }
                }
            },
            {
                MacroType.Yell, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "yell", "shout", "scream" } }
                }
            },
            {
                MacroType.Walk, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.NW, new[] { "walk northwest", "move northwest" } },
                    { MacroSubType.N, new[] { "walk north", "move north" } },
                    { MacroSubType.NE, new[] { "walk northeast", "move northeast" } },
                    { MacroSubType.E, new[] { "walk east", "move east" } },
                    { MacroSubType.SE, new[] { "walk southeast", "move southeast" } },
                    { MacroSubType.S, new[] { "walk south", "move south" } },
                    { MacroSubType.SW, new[] { "walk southwest", "move southwest" } },
                    { MacroSubType.W, new[] { "walk west", "move west" } }
                }
            },
            {
                MacroType.WarPeace, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle war mode", "war mode", "peace mode" } }
                }
            },
            {
                MacroType.Paste, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "paste", "insert text", "insert clipboard" } }
                }
            },
            {
                MacroType.OpenDoor, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "open door", "unlock door", "enter door" } }
                }
            },
            {
                MacroType.UseSkill, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Anatomy, new[] { "use anatomy", "anatomy skill" } },
                    { MacroSubType.AnimalLore, new[] { "use animal lore", "animal lore skill" } },
                    { MacroSubType.AnimalTaming, new[] { "use animal taming", "animal taming skill" } },
                    { MacroSubType.ArmsLore, new[] { "use arms lore", "arms lore skill" } },
                    { MacroSubType.Begging, new[] { "use begging", "begging skill" } },
                    { MacroSubType.Cartography, new[] { "use cartography", "cartography skill" } },
                    { MacroSubType.DetectingHidden, new[] { "use detecting hidden", "detecting hidden skill" } },
                    { MacroSubType.Discordance, new[] { "use discordance", "discordance skill" } },
                    { MacroSubType.EvaluatingIntelligence, new[] { "use evaluating intelligence", "evaluating intelligence skill" } },
                    { MacroSubType.ForensicEvaluation, new[] { "use forensic evaluation", "forensic evaluation skill" } },
                    { MacroSubType.Hiding, new[] { "use hiding", "hiding skill" } },
                    { MacroSubType.Imbuing, new[] { "use imbuing", "imbuing skill" } },
                    { MacroSubType.Inscription, new[] { "use inscription", "inscription skill" } },
                    { MacroSubType.ItemIdentification, new[] { "use item identification", "item identification skill" } },
                    { MacroSubType.Meditation, new[] { "use meditation", "meditation skill" } },
                    { MacroSubType.Peacemaking, new[] { "use peacemaking", "peacemaking skill" } },
                    { MacroSubType.Poisoning, new[] { "use poisoning", "poisoning skill" } },
                    { MacroSubType.Provocation, new[] { "use provocation", "provocation skill" } },
                    { MacroSubType.RemoveTrap, new[] { "use remove trap", "remove trap skill" } },
                    { MacroSubType.SpiritSpeak, new[] { "use spirit speak", "spirit speak skill" } },
                    { MacroSubType.Stealing, new[] { "use stealing", "stealing skill" } },
                    { MacroSubType.Stealth, new[] { "use stealth", "stealth skill" } },
                    { MacroSubType.TasteIdentification, new[] { "use taste identification", "taste identification skill" } },
                    { MacroSubType.Tracking, new[] { "use tracking", "tracking skill" } }
                }
            },
            {
                MacroType.LastSkill, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "last skill", "use last skill" } }
                }
            },
            {
                MacroType.CastSpell, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Clumsy, new[] { "cast clumsy", "clumsy spell" } },
                    { MacroSubType.CreateFood, new[] { "cast create food", "create food spell" } },
                    { MacroSubType.Feeblemind, new[] { "cast feeblemind", "feeblemind spell" } },
                    { MacroSubType.Heal, new[] { "cast heal", "heal spell" } },
                    { MacroSubType.MagicArrow, new[] { "cast magic arrow", "magic arrow spell" } },
                    { MacroSubType.NightSight, new[] { "cast night sight", "night sight spell" } },
                    { MacroSubType.ReactiveArmor, new[] { "cast reactive armor", "reactive armor spell" } },
                    { MacroSubType.Weaken, new[] { "cast weaken", "weaken spell" } },
                    { MacroSubType.Agility, new[] { "cast agility", "agility spell" } },
                    { MacroSubType.Cunning, new[] { "cast cunning", "cunning spell" } },
                    { MacroSubType.Cure, new[] { "cast cure", "cure spell" } },
                    { MacroSubType.Harm, new[] { "cast harm", "harm spell" } },
                    { MacroSubType.MagicTrap, new[] { "cast magic trap", "magic trap spell" } },
                    { MacroSubType.MagicUntrap, new[] { "cast magic untrap", "magic untrap spell" } },
                    { MacroSubType.Protection, new[] { "cast protection", "protection spell" } },
                    { MacroSubType.Strength, new[] { "cast strength", "strength spell" } },
                    { MacroSubType.Bless, new[] { "cast bless", "bless spell" } },
                    { MacroSubType.Fireball, new[] { "cast fireball", "fireball spell" } },
                    { MacroSubType.MagicLock, new[] { "cast magic lock", "magic lock spell" } },
                    { MacroSubType.Poison, new[] { "cast poison", "poison spell" } },
                    { MacroSubType.Telekinesis, new[] { "cast telekinesis", "telekinesis spell" } },
                    { MacroSubType.Teleport, new[] { "cast teleport", "teleport spell" } },
                    { MacroSubType.Unlock, new[] { "cast unlock", "unlock spell" } },
                    { MacroSubType.WallOfStone, new[] { "cast wall of stone", "wall of stone spell" } },
                    { MacroSubType.ArchCure, new[] { "cast arch cure", "arch cure spell" } },
                    { MacroSubType.ArchProtection, new[] { "cast arch protection", "arch protection spell" } },
                    { MacroSubType.Curse, new[] { "cast curse", "curse spell" } },
                    { MacroSubType.FireField, new[] { "cast fire field", "fire field spell" } },
                    { MacroSubType.GreaterHeal, new[] { "cast greater heal", "greater heal spell" } },
                    { MacroSubType.Lightning, new[] { "cast lightning", "lightning spell" } },
                    { MacroSubType.ManaDrain, new[] { "cast mana drain", "mana drain spell" } },
                    { MacroSubType.Recall, new[] { "cast recall", "recall spell" } },
                    { MacroSubType.BladeSpirits, new[] { "cast blade spirits", "blade spirits spell" } },
                    { MacroSubType.DispellField, new[] { "cast dispel field", "dispel field spell" } },
                    { MacroSubType.Incognito, new[] { "cast incognito", "incognito spell" } },
                    { MacroSubType.MagicReflection, new[] { "cast magic reflection", "magic reflection spell" } },
                    { MacroSubType.MindBlast, new[] { "cast mind blast", "mind blast spell" } },
                    { MacroSubType.Paralyze, new[] { "cast paralyze", "paralyze spell" } },
                    { MacroSubType.PoisonField, new[] { "cast poison field", "poison field spell" } },
                    { MacroSubType.SummonCreature, new[] { "cast summon creature", "summon creature spell" } },
                    { MacroSubType.Dispel, new[] { "cast dispel", "dispel spell" } },
                    { MacroSubType.EnergyBolt, new[] { "cast energy bolt", "energy bolt spell" } },
                    { MacroSubType.Explosion, new[] { "cast explosion", "explosion spell" } },
                    { MacroSubType.Invisibility, new[] { "cast invisibility", "invisibility spell" } },
                    { MacroSubType.Mark, new[] { "cast mark", "mark spell" } },
                    { MacroSubType.MassCurse, new[] { "cast mass curse", "mass curse spell" } },
                    { MacroSubType.ParalyzeField, new[] { "cast paralyze field", "paralyze field spell" } },
                    { MacroSubType.Reveal, new[] { "cast reveal", "reveal spell" } },
                    { MacroSubType.ChainLightning, new[] { "cast chain lightning", "chain lightning spell" } },
                    { MacroSubType.EnergyField, new[] { "cast energy field", "energy field spell" } },
                    { MacroSubType.FlameStrike, new[] { "cast flame strike", "flame strike spell" } },
                    { MacroSubType.GateTravel, new[] { "cast gate travel", "gate travel spell" } },
                    { MacroSubType.ManaVampire, new[] { "cast mana vampire", "mana vampire spell" } },
                    { MacroSubType.MassDispel, new[] { "cast mass dispel", "mass dispel spell" } },
                    { MacroSubType.MeteorSwarm, new[] { "cast meteor swarm", "meteor swarm spell" } },
                    { MacroSubType.Polymorph, new[] { "cast polymorph", "polymorph spell" } },
                    { MacroSubType.Earthquake, new[] { "cast earthquake", "earthquake spell" } },
                    { MacroSubType.EnergyVortex, new[] { "cast energy vortex", "energy vortex spell" } },
                    { MacroSubType.Resurrection, new[] { "cast resurrection", "resurrection spell" } },
                    { MacroSubType.AirElemental, new[] { "cast air elemental", "air elemental spell" } },
                    { MacroSubType.SummonDaemon, new[] { "cast summon daemon", "summon daemon spell" } },
                    { MacroSubType.EarthElemental, new[] { "cast earth elemental", "earth elemental spell" } },
                    { MacroSubType.FireElemental, new[] { "cast fire elemental", "fire elemental spell" } },
                    { MacroSubType.WaterElemental, new[] { "cast water elemental", "water elemental spell" } },
                    { MacroSubType.AnimateDead, new[] { "cast animate dead", "animate dead spell" } },
                    { MacroSubType.BloodOath, new[] { "cast blood oath", "blood oath spell" } },
                    { MacroSubType.CorpseSkin, new[] { "cast corpse skin", "corpse skin spell" } },
                    { MacroSubType.CurseWeapon, new[] { "cast curse weapon", "curse weapon spell" } },
                    { MacroSubType.EvilOmen, new[] { "cast evil omen", "evil omen spell" } },
                    { MacroSubType.HorrificBeast, new[] { "cast horrific beast", "horrific beast spell" } },
                    { MacroSubType.LichForm, new[] { "cast lich form", "lich form spell" } },
                    { MacroSubType.MindRot, new[] { "cast mind rot", "mind rot spell" } },
                    { MacroSubType.PainSpike, new[] { "cast pain spike", "pain spike spell" } },
                    { MacroSubType.PoisonStrike, new[] { "cast poison strike", "poison strike spell" } },
                    { MacroSubType.Strangle, new[] { "cast strangle", "strangle spell" } },
                    { MacroSubType.SummonFamilar, new[] { "cast summon familiar", "summon familiar spell" } },
                    { MacroSubType.VampiricEmbrace, new[] { "cast vampiric embrace", "vampiric embrace spell" } },
                    { MacroSubType.VengefulSpirit, new[] { "cast vengeful spirit", "vengeful spirit spell" } },
                    { MacroSubType.Wither, new[] { "cast wither", "wither spell" } },
                    { MacroSubType.WraithForm, new[] { "cast wraith form", "wraith form spell" } },
                    { MacroSubType.Exorcism, new[] { "cast exorcism", "exorcism spell" } },
                    { MacroSubType.CleanceByFire, new[] { "cast cleanse by fire", "cleanse by fire spell" } },
                    { MacroSubType.CloseWounds, new[] { "cast close wounds", "close wounds spell" } },
                    { MacroSubType.ConsecrateWeapon, new[] { "cast consecrate weapon", "consecrate weapon spell" } },
                    { MacroSubType.DispelEvil, new[] { "cast dispel evil", "dispel evil spell" } },
                    { MacroSubType.DivineFury, new[] { "cast divine fury", "divine fury spell" } },
                    { MacroSubType.EnemyOfOne, new[] { "cast enemy of one", "enemy of one spell" } },
                    { MacroSubType.HolyLight, new[] { "cast holy light", "holy light spell" } },
                    { MacroSubType.NobleSacrifice, new[] { "cast noble sacrifice", "noble sacrifice spell" } },
                    { MacroSubType.RemoveCurse, new[] { "cast remove curse", "remove curse spell" } },
                    { MacroSubType.SacredJourney, new[] { "cast sacred journey", "sacred journey spell" } },
                    { MacroSubType.HonorableExecution, new[] { "cast honorable execution", "honorable execution spell" } },
                    { MacroSubType.Confidence, new[] { "cast confidence", "confidence spell" } },
                    { MacroSubType.Evasion, new[] { "cast evasion", "evasion spell" } },
                    { MacroSubType.CounterAttack, new[] { "cast counter attack", "counter attack spell" } },
                    { MacroSubType.LightingStrike, new[] { "cast lightning strike", "lightning strike spell" } },
                    { MacroSubType.MomentumStrike, new[] { "cast momentum strike", "momentum strike spell" } },
                    { MacroSubType.FocusAttack, new[] { "cast focus attack", "focus attack spell" } },
                    { MacroSubType.DeathStrike, new[] { "cast death strike", "death strike spell" } },
                    { MacroSubType.AnimalForm, new[] { "cast animal form", "animal form spell" } },
                    { MacroSubType.KiAttack, new[] { "cast ki attack", "ki attack spell" } },
                    { MacroSubType.SurpriceAttack, new[] { "cast surprise attack", "surprise attack spell" } },
                    { MacroSubType.Backstab, new[] { "cast backstab", "backstab spell" } },
                    { MacroSubType.Shadowjump, new[] { "cast shadow jump", "shadow jump spell" } },
                    { MacroSubType.MirrorImage, new[] { "cast mirror image", "mirror image spell" } },
                    { MacroSubType.ArcaneCircle, new[] { "cast arcane circle", "arcane circle spell" } },
                    { MacroSubType.GiftOfRenewal, new[] { "cast gift of renewal", "gift of renewal spell" } },
                    { MacroSubType.ImmolatingWeapon, new[] { "cast immolating weapon", "immolating weapon spell" } },
                    { MacroSubType.Attunement, new[] { "cast attunement", "attunement spell" } },
                    { MacroSubType.Thunderstorm, new[] { "cast thunderstorm", "thunderstorm spell" } },
                    { MacroSubType.NaturesFury, new[] { "cast nature's fury", "nature's fury spell" } },
                    { MacroSubType.SummonFey, new[] { "cast summon fey", "summon fey spell" } },
                    { MacroSubType.SummonFiend, new[] { "cast summon fiend", "summon fiend spell" } },
                    { MacroSubType.ReaperForm, new[] { "cast reaper form", "reaper form spell" } },
                    { MacroSubType.Wildfire, new[] { "cast wildfire", "wildfire spell" } },
                    { MacroSubType.EssenceOfWind, new[] { "cast essence of wind", "essence of wind spell" } },
                    { MacroSubType.DryadAllure, new[] { "cast dryad allure", "dryad allure spell" } },
                    { MacroSubType.EtherealVoyage, new[] { "cast ethereal voyage", "ethereal voyage spell" } },
                    { MacroSubType.WordOfDeath, new[] { "cast word of death", "word of death spell" } },
                    { MacroSubType.GiftOfLife, new[] { "cast gift of life", "gift of life spell" } },
                    { MacroSubType.ArcaneEmpowermen, new[] { "cast arcane empowerment", "arcane empowerment spell" } },
                    { MacroSubType.NetherBolt, new[] { "cast nether bolt", "nether bolt spell" } },
                    { MacroSubType.HealingStone, new[] { "cast healing stone", "healing stone spell" } },
                    { MacroSubType.PurgeMagic, new[] { "cast purge magic", "purge magic spell" } },
                    { MacroSubType.Enchant, new[] { "cast enchant", "enchant spell" } },
                    { MacroSubType.Sleep, new[] { "cast sleep", "sleep spell" } },
                    { MacroSubType.EagleStrike, new[] { "cast eagle strike", "eagle strike spell" } },
                    { MacroSubType.AnimatedWeapon, new[] { "cast animated weapon", "animated weapon spell" } },
                    { MacroSubType.StoneForm, new[] { "cast stone form", "stone form spell" } },
                    { MacroSubType.SpellTrigger, new[] { "cast spell trigger", "spell trigger spell" } },
                    { MacroSubType.MassSleep, new[] { "cast mass sleep", "mass sleep spell" } },
                    { MacroSubType.CleansingWinds, new[] { "cast cleansing winds", "cleansing winds spell" } },
                    { MacroSubType.Bombard, new[] { "cast bombard", "bombard spell" } },
                    { MacroSubType.SpellPlague, new[] { "cast spell plague", "spell plague spell" } },
                    { MacroSubType.HailStorm, new[] { "cast hail storm", "hail storm spell" } },
                    { MacroSubType.NetherCyclone, new[] { "cast nether cyclone", "nether cyclone spell" } },
                    { MacroSubType.RisingColossus, new[] { "cast rising colossus", "rising colossus spell" } },
                    { MacroSubType.Inspire, new[] { "cast inspire", "inspire spell" } },
                    { MacroSubType.Invigorate, new[] { "cast invigorate", "invigorate spell" } },
                    { MacroSubType.Resilience, new[] { "cast resilience", "resilience spell" } },
                    { MacroSubType.Perseverance, new[] { "cast perseverance", "perseverance spell" } },
                    { MacroSubType.Tribulation, new[] { "cast tribulation", "tribulation spell" } },
                    { MacroSubType.Despair, new[] { "cast despair", "despair spell" } }
                }
            },
            {
                MacroType.LastSpell, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "last spell", "cast last spell" } }
                }
            },
            {
                MacroType.Bow, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "bow", "perform bow" } }
                }
            },
            {
                MacroType.Salute, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "salute", "perform salute" } }
                }
            },
            {
                MacroType.QuitGame, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "quit game", "exit game", "close game" } }
                }
            },
            {
                MacroType.AllNames, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "all names", "show all names" } }
                }
            },
            {
                MacroType.LastObject, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "last object", "use last object" } }
                }
            },
            {
                MacroType.UseItemInHand, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "use item in hand", "use held item" } }
                }
            },
            {
                MacroType.LastTarget, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "last target", "target last" } }
                }
            },
            {
                MacroType.TargetSelf, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "target self", "self target" } }
                }
            },
            {
                MacroType.ArmDisarm, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.LeftHand, new[] { "arm left hand", "disarm left hand" } },
                    { MacroSubType.RightHand, new[] { "arm right hand", "disarm right hand" } }
                }
            },
            {
                MacroType.WaitForTarget, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "wait for target", "hold target" } }
                }
            },
            {
                MacroType.TargetNext, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "target next", "next target" } }
                }
            },
            {
                MacroType.AttackLast, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "attack last", "last attack" } }
                }
            },
            {
                MacroType.Delay, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "delay", "wait" } }
                }
            },
            {
                MacroType.CircleTrans, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "circle transparency", "toggle circle transparency" } }
                }
            },
            {
                MacroType.CloseGump, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "close gump", "hide gump" } }
                }
            },
            {
                MacroType.AlwaysRun, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "always run", "toggle always run" } }
                }
            },
            {
                MacroType.SaveDesktop, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "save desktop", "save layout" } }
                }
            },
            {
                MacroType.EnableRangeColor, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "enable range color", "turn on range color" } }
                }
            },
            {
                MacroType.DisableRangeColor, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "disable range color", "turn off range color" } }
                }
            },
            {
                MacroType.ToggleRangeColor, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle range color", "switch range color" } }
                }
            },
            {
                MacroType.AttackSelectedTarget, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "attack selected target", "attack target" } }
                }
            },
            {
                MacroType.UseSelectedTarget, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "use selected target", "use target" } }
                }
            },
            {
                MacroType.CurrentTarget, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "current target", "target current" } }
                }
            },
            {
                MacroType.TargetSystemOnOff, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "target system on off", "toggle target system" } }
                }
            },
            {
                MacroType.BandageSelf, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "bandage self", "self bandage" } }
                }
            },
            {
                MacroType.BandageTarget, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "bandage target", "target bandage" } }
                }
            },
            {
                MacroType.SetUpdateRange, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "set update range", "update range" } }
                }
            },
            {
                MacroType.ModifyUpdateRange, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "modify update range", "change update range" } }
                }
            },
            {
                MacroType.IncreaseUpdateRange, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "increase update range", "raise update range" } }
                }
            },
            {
                MacroType.DecreaseUpdateRange, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "decrease update range", "lower update range" } }
                }
            },
            {
                MacroType.MaxUpdateRange, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "max update range", "maximum update range" } }
                }
            },
            {
                MacroType.MinUpdateRange, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "min update range", "minimum update range" } }
                }
            },
            {
                MacroType.DefaultUpdateRange, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "default update range", "reset update range" } }
                }
            },
            {
                MacroType.SelectNext, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Hostile, new[] { "select next hostile", "next hostile" } },
                    { MacroSubType.Party, new[] { "select next party", "next party" } },
                    { MacroSubType.Follower, new[] { "select next follower", "next follower" } },
                    { MacroSubType.Object, new[] { "select next object", "next object" } },
                    { MacroSubType.Mobile, new[] { "select next mobile", "next mobile" } }
                }
            },
            {
                MacroType.SelectPrevious, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Hostile, new[] { "select previous hostile", "previous hostile" } },
                    { MacroSubType.Party, new[] { "select previous party", "previous party" } },
                    { MacroSubType.Follower, new[] { "select previous follower", "previous follower" } },
                    { MacroSubType.Object, new[] { "select previous object", "previous object" } },
                    { MacroSubType.Mobile, new[] { "select previous mobile", "previous mobile" } }
                }
            },
            {
                MacroType.SelectNearest, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Hostile, new[] { "select nearest hostile", "nearest hostile" } },
                    { MacroSubType.Party, new[] { "select nearest party", "nearest party" } },
                    { MacroSubType.Follower, new[] { "select nearest follower", "nearest follower" } },
                    { MacroSubType.Object, new[] { "select nearest object", "nearest object" } },
                    { MacroSubType.Mobile, new[] { "select nearest mobile", "nearest mobile" } }
                }
            },
            {
                MacroType.ToggleBuffIconGump, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle buff icon", "buff icon" } }
                }
            },
            {
                MacroType.InvokeVirtue, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.Honor, new[] { "invoke honor", "honor virtue" } },
                    { MacroSubType.Sacrifice, new[] { "invoke sacrifice", "sacrifice virtue" } },
                    { MacroSubType.Valor, new[] { "invoke valor", "valor virtue" } }
                }
            },
            {
                MacroType.PrimaryAbility, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "primary ability", "use primary ability" } }
                }
            },
            {
                MacroType.SecondaryAbility, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "secondary ability", "use secondary ability" } }
                }
            },
            {
                MacroType.ToggleGargoyleFly, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle gargoyle fly", "gargoyle fly" } }
                }
            },
            {
                MacroType.EquipLastWeapon, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "equip last weapon", "last weapon" } }
                }
            },
            {
                MacroType.KillGumpOpen, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "kill gump open", "close all gumps" } }
                }
            },
            {
                MacroType.Zoom, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.DefaultZoom, new[] { "default zoom", "reset zoom" } },
                    { MacroSubType.ZoomIn, new[] { "zoom in", "increase zoom" } },
                    { MacroSubType.ZoomOut, new[] { "zoom out", "decrease zoom" } }
                }
            },
            {
                MacroType.ToggleChatVisibility, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle chat visibility", "chat visibility" } }
                }
            },
            {
                MacroType.Aura, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "aura", "toggle aura" } }
                }
            },
            {
                MacroType.AuraOnOff, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "aura on off", "toggle aura visibility" } }
                }
            },
            {
                MacroType.Grab, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "grab", "grab item" } }
                }
            },
            {
                MacroType.SetGrabBag, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "set grab bag", "set item bag" } }
                }
            },
            {
                MacroType.NamesOnOff, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "names on off", "toggle names" } }
                }
            },
            {
                MacroType.UsePotion, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.ConfusionBlastPotion, new[] { "use confusion blast potion", "confusion blast potion" } },
                    { MacroSubType.CurePotion, new[] { "use cure potion", "cure potion" } },
                    { MacroSubType.AgilityPotion, new[] { "use agility potion", "agility potion" } },
                    { MacroSubType.StrengthPotion, new[] { "use strength potion", "strength potion" } },
                    { MacroSubType.PoisonPotion, new[] { "use poison potion", "poison potion" } },
                    { MacroSubType.RefreshPotion, new[] { "use refresh potion", "refresh potion" } },
                    { MacroSubType.HealPotion, new[] { "use heal potion", "heal potion" } },
                    { MacroSubType.ExplosionPotion, new[] { "use explosion potion", "explosion potion" } }
                }
            },
            {
                MacroType.UseObject, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.BestHealPotion, new[] { "use best heal potion", "best heal potion" } },
                    { MacroSubType.BestCurePotion, new[] { "use best cure potion", "best cure potion" } },
                    { MacroSubType.BestRefreshPotion, new[] { "use best refresh potion", "best refresh potion" } },
                    { MacroSubType.BestStrengthPotion, new[] { "use best strength potion", "best strength potion" } },
                    { MacroSubType.BestAgiPotion, new[] { "use best agility potion", "best agility potion" } },
                    { MacroSubType.BestExplosionPotion, new[] { "use best explosion potion", "best explosion potion" } },
                    { MacroSubType.BestConflagPotion, new[] { "use best conflag potion", "best conflag potion" } },
                    { MacroSubType.EnchantedApple, new[] { "use enchanted apple", "enchanted apple" } },
                    { MacroSubType.PetalsOfTrinsic, new[] { "use petals of trinsic", "petals of trinsic" } },
                    { MacroSubType.OrangePetals, new[] { "use orange petals", "orange petals" } },
                    { MacroSubType.TrappedBox, new[] { "use trapped box", "trapped box" } },
                    { MacroSubType.SmokeBomb, new[] { "use smoke bomb", "smoke bomb" } },
                    { MacroSubType.HealStone, new[] { "use heal stone", "heal stone" } },
                    { MacroSubType.SpellStone, new[] { "use spell stone", "spell stone" } }
                }
            },
            {
                MacroType.CloseAllHealthBars, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "close all health bars", "hide all health bars" } }
                }
            },
            {
                MacroType.CloseInactiveHealthBars, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "close inactive health bars", "hide inactive health bars" } }
                }
            },
            {
                MacroType.CloseCorpses, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "close corpses", "hide corpses" } }
                }
            },
            {
                MacroType.ToggleDrawRoofs, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle draw roofs", "draw roofs" } }
                }
            },
            {
                MacroType.ToggleTreeStumps, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle tree stumps", "tree stumps" } }
                }
            },
            {
                MacroType.ToggleVegetation, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle vegetation", "vegetation" } }
                }
            },
            {
                MacroType.ToggleCaveTiles, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "toggle cave tiles", "cave tiles" } }
                }
            },
            {
                MacroType.LookAtMouse, new Dictionary<MacroSubType, string[]>
                {
                    { MacroSubType.MSC_NONE, new[] { "look at mouse", "follow mouse" } }
                }
            }
        };
    }
}
