// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game;
using ClassicUO.Game.Managers;
using ClassicUO.SpeechRecognition.Diagnostics;

namespace ClassicUO.SpeechRecognition.Basic
{
    /// <summary>
    /// Basic mode voice command processor - fast, deterministic, no AI.
    /// 
    /// Design principles:
    /// 1. O(1) lookup via hash tables - no iteration over all commands
    /// 2. Single-word shortcuts for combat-critical commands
    /// 3. Prefix tree for multi-word phrases
    /// 4. Zero allocations in hot path after initialization
    /// 5. Fully isolated - no dependencies on other speech systems
    /// 
    /// Usage:
    ///   var processor = new BasicVoiceProcessor(world);
    ///   processor.Initialize();
    ///   if (processor.TryProcess("fireball", out var result)) { /* execute */ }
    /// </summary>
    internal sealed class BasicVoiceProcessor
    {
        private readonly World _world;

        // Primary lookup: normalized phrase → command action
        private readonly Dictionary<string, BasicCommandAction> _exactLookup = 
            new(StringComparer.OrdinalIgnoreCase);

        // Single-word shortcuts for combat speed (highest priority)
        private readonly Dictionary<string, BasicCommandAction> _shortcuts = 
            new(StringComparer.OrdinalIgnoreCase);

        // Prefix lookup for "contains" matching (pet commands, etc.)
        private readonly List<(string Trigger, BasicCommandAction Action)> _containsPatterns = new();

        // Deduplication: prevent double-fire from partial+final results
        private string _lastExecutedCommand;
        private long _lastExecutedTicks;
        private const long DEDUP_WINDOW_TICKS = 10_000_000; // 1 second in ticks

        // Stats for debugging
        public int TotalCommands => _exactLookup.Count;
        public int ShortcutCount => _shortcuts.Count;
        public int ContainsPatternCount => _containsPatterns.Count;

        public BasicVoiceProcessor(World world)
        {
            _world = world;
        }

        /// <summary>
        /// Initialize the command registry. Call once at startup.
        /// </summary>
        public void Initialize()
        {
            SpeechLog.Info(SpeechLogChannel.Route, "[BasicMode] Initializing command registry...");

            // Build in priority order
            RegisterShortcuts();
            RegisterSpells();
            RegisterSkills();
            RegisterTargeting();
            RegisterCombat();
            RegisterPotions();
            RegisterGumps();
            RegisterPetCommands();
            RegisterEmergency();
            RegisterView();
            RegisterMisc();

            SpeechLog.Info(SpeechLogChannel.Route, 
                $"[BasicMode] Initialized: {TotalCommands} exact, {ShortcutCount} shortcuts, {ContainsPatternCount} contains");
        }

        /// <summary>
        /// Try to process a voice transcript. Returns true if a command was matched.
        /// Includes deduplication to prevent double-fire from partial+final results.
        /// </summary>
        public bool TryProcess(string transcript, out BasicCommandResult result)
        {
            result = default;

            if (string.IsNullOrWhiteSpace(transcript))
                return false;

            string normalized = transcript.Trim().ToLowerInvariant();

            // Dedup check: skip if same command fired recently
            long now = DateTime.UtcNow.Ticks;
            if (_lastExecutedCommand == normalized && (now - _lastExecutedTicks) < DEDUP_WINDOW_TICKS)
            {
                SpeechLog.Trace(SpeechLogChannel.Route, $"[BasicMode] Dedup skip: '{normalized}'");
                return false;
            }

            // 1. Check single-word shortcuts first (fastest path for combat)
            if (_shortcuts.TryGetValue(normalized, out var shortcutAction))
            {
                result = new BasicCommandResult(shortcutAction, normalized, MatchType.Shortcut);
                SpeechLog.Debug(SpeechLogChannel.Route, $"[BasicMode] Shortcut match: '{normalized}' → {shortcutAction.Label}");
                RecordExecution(normalized);
                return true;
            }

            // 2. Exact phrase match
            if (_exactLookup.TryGetValue(normalized, out var exactAction))
            {
                result = new BasicCommandResult(exactAction, normalized, MatchType.Exact);
                SpeechLog.Debug(SpeechLogChannel.Route, $"[BasicMode] Exact match: '{normalized}' → {exactAction.Label}");
                RecordExecution(normalized);
                return true;
            }

            // 3. Contains matching for flexible commands (pet commands)
            foreach (var (trigger, action) in _containsPatterns)
            {
                if (normalized.Contains(trigger, StringComparison.OrdinalIgnoreCase))
                {
                    result = new BasicCommandResult(action, trigger, MatchType.Contains);
                    SpeechLog.Debug(SpeechLogChannel.Route, $"[BasicMode] Contains match: '{normalized}' contains '{trigger}' → {action.Label}");
                    RecordExecution(trigger);
                    return true;
                }
            }

            SpeechLog.Trace(SpeechLogChannel.Route, $"[BasicMode] No match for: '{normalized}'");
            return false;
        }
        
        /// <summary>
        /// Execute a matched command result.
        /// </summary>
        public void Execute(BasicCommandResult result)
        {
            if (result.Action == null) return;
            
            SpeechLog.Info(SpeechLogChannel.Route, $"[BasicMode] Executing: {result.Action.Label}");
            result.Action.Execute(_world);
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // SHORTCUT REGISTRATION - Single-word combat commands for lowest latency
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterShortcuts()
        {
            // Combat-critical shortcuts (single words)
            Shortcut("fireball", MacroType.CastSpell, MacroSubType.Fireball, "Fireball");
            Shortcut("heal", MacroType.CastSpell, MacroSubType.Heal, "Heal");
            Shortcut("gheal", MacroType.CastSpell, MacroSubType.GreaterHeal, "Greater Heal");
            Shortcut("cure", MacroType.CastSpell, MacroSubType.Cure, "Cure");
            Shortcut("poison", MacroType.CastSpell, MacroSubType.Poison, "Poison");
            Shortcut("lightning", MacroType.CastSpell, MacroSubType.Lightning, "Lightning");
            Shortcut("bolt", MacroType.CastSpell, MacroSubType.Lightning, "Lightning");
            Shortcut("ebolt", MacroType.CastSpell, MacroSubType.EnergyBolt, "Energy Bolt");
            Shortcut("flamestrike", MacroType.CastSpell, MacroSubType.FlameStrike, "Flamestrike");
            Shortcut("fs", MacroType.CastSpell, MacroSubType.FlameStrike, "Flamestrike");
            Shortcut("explosion", MacroType.CastSpell, MacroSubType.Explosion, "Explosion");
            Shortcut("paralyze", MacroType.CastSpell, MacroSubType.Paralyze, "Paralyze");
            Shortcut("para", MacroType.CastSpell, MacroSubType.Paralyze, "Paralyze");
            Shortcut("recall", MacroType.CastSpell, MacroSubType.Recall, "Recall");
            Shortcut("invis", MacroType.CastSpell, MacroSubType.Invisibility, "Invisibility");
            Shortcut("reveal", MacroType.CastSpell, MacroSubType.Reveal, "Reveal");
            Shortcut("dispel", MacroType.CastSpell, MacroSubType.Dispel, "Dispel");
            Shortcut("bless", MacroType.CastSpell, MacroSubType.Bless, "Bless");
            Shortcut("curse", MacroType.CastSpell, MacroSubType.Curse, "Curse");
            
            // Skills shortcuts
            Shortcut("hide", MacroType.UseSkill, MacroSubType.Hiding, "Hiding");
            Shortcut("stealth", MacroType.UseSkill, MacroSubType.Stealth, "Stealth");
            Shortcut("meditate", MacroType.UseSkill, MacroSubType.Meditation, "Meditation");
            Shortcut("med", MacroType.UseSkill, MacroSubType.Meditation, "Meditation");
            Shortcut("track", MacroType.UseSkill, MacroSubType.Tracking, "Tracking");
            Shortcut("detect", MacroType.UseSkill, MacroSubType.DetectingHidden, "Detect Hidden");
            
            // Targeting shortcuts  
            Shortcut("self", MacroType.TargetSelf, MacroSubType.MSC_NONE, "Target Self");
            Shortcut("last", MacroType.LastTarget, MacroSubType.MSC_NONE, "Last Target");
            
            // Ability shortcuts
            Shortcut("primary", MacroType.PrimaryAbility, MacroSubType.MSC_NONE, "Primary Ability");
            Shortcut("secondary", MacroType.SecondaryAbility, MacroSubType.MSC_NONE, "Secondary Ability");
            
            // Combat shortcuts
            Shortcut("attack", MacroType.AttackLast, MacroSubType.MSC_NONE, "Attack Last");
            Shortcut("war", MacroType.WarPeace, MacroSubType.MSC_NONE, "Toggle War Mode");
            Shortcut("peace", MacroType.WarPeace, MacroSubType.MSC_NONE, "Toggle War Mode");
            
            // Emergency
            Shortcut("bandage", MacroType.BandageSelf, MacroSubType.MSC_NONE, "Bandage Self");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // SPELL REGISTRATION - All spells with full and abbreviated phrases
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterSpells()
        {
            // First Circle
            Spell(MacroSubType.Clumsy, "Clumsy", "clumsy", "cast clumsy");
            Spell(MacroSubType.CreateFood, "Create Food", "create food", "cast create food", "food");
            Spell(MacroSubType.Feeblemind, "Feeblemind", "feeblemind", "cast feeblemind", "feeble");
            Spell(MacroSubType.Heal, "Heal", "cast heal", "heal spell", "minor heal");
            Spell(MacroSubType.MagicArrow, "Magic Arrow", "magic arrow", "cast magic arrow", "arrow");
            Spell(MacroSubType.NightSight, "Night Sight", "night sight", "cast night sight", "nightsight");
            Spell(MacroSubType.ReactiveArmor, "Reactive Armor", "reactive armor", "cast reactive armor", "reactive");
            Spell(MacroSubType.Weaken, "Weaken", "weaken", "cast weaken");
            
            // Second Circle
            Spell(MacroSubType.Agility, "Agility", "agility", "cast agility");
            Spell(MacroSubType.Cunning, "Cunning", "cunning", "cast cunning");
            Spell(MacroSubType.Cure, "Cure", "cast cure", "cure spell");
            Spell(MacroSubType.Harm, "Harm", "harm", "cast harm");
            Spell(MacroSubType.MagicTrap, "Magic Trap", "magic trap", "cast magic trap");
            Spell(MacroSubType.MagicUntrap, "Magic Untrap", "magic untrap", "cast magic untrap", "untrap");
            Spell(MacroSubType.Protection, "Protection", "protection", "cast protection", "prot");
            Spell(MacroSubType.Strength, "Strength", "strength", "cast strength", "str");
            
            // Third Circle
            Spell(MacroSubType.Bless, "Bless", "cast bless", "bless spell");
            Spell(MacroSubType.Fireball, "Fireball", "cast fireball", "fireball spell", "fire ball");
            Spell(MacroSubType.MagicLock, "Magic Lock", "magic lock", "cast magic lock");
            Spell(MacroSubType.Poison, "Poison", "cast poison", "poison spell");
            Spell(MacroSubType.Telekinesis, "Telekinesis", "telekinesis", "cast telekinesis", "tk");
            Spell(MacroSubType.Teleport, "Teleport", "teleport", "cast teleport", "tele");
            Spell(MacroSubType.Unlock, "Unlock", "unlock", "cast unlock");
            Spell(MacroSubType.WallOfStone, "Wall of Stone", "wall of stone", "cast wall of stone", "stone wall");
            
            // Fourth Circle
            Spell(MacroSubType.ArchCure, "Arch Cure", "arch cure", "cast arch cure", "archcure");
            Spell(MacroSubType.ArchProtection, "Arch Protection", "arch protection", "cast arch protection");
            Spell(MacroSubType.Curse, "Curse", "cast curse", "curse spell");
            Spell(MacroSubType.FireField, "Fire Field", "fire field", "cast fire field");
            Spell(MacroSubType.GreaterHeal, "Greater Heal", "greater heal", "cast greater heal", "g heal", "big heal");
            Spell(MacroSubType.Lightning, "Lightning", "cast lightning", "lightning spell");
            Spell(MacroSubType.ManaDrain, "Mana Drain", "mana drain", "cast mana drain", "drain");
            Spell(MacroSubType.Recall, "Recall", "cast recall", "recall spell");
            
            // Fifth Circle  
            Spell(MacroSubType.BladeSpirits, "Blade Spirits", "blade spirits", "cast blade spirits", "blades", "bs");
            Spell(MacroSubType.DispellField, "Dispel Field", "dispel field", "cast dispel field");
            Spell(MacroSubType.Incognito, "Incognito", "incognito", "cast incognito", "incog");
            Spell(MacroSubType.MagicReflection, "Magic Reflection", "magic reflection", "cast magic reflection", "reflect", "mr");
            Spell(MacroSubType.MindBlast, "Mind Blast", "mind blast", "cast mind blast", "mb");
            Spell(MacroSubType.Paralyze, "Paralyze", "cast paralyze", "paralyze spell");
            Spell(MacroSubType.PoisonField, "Poison Field", "poison field", "cast poison field");
            Spell(MacroSubType.SummonCreature, "Summon Creature", "summon creature", "cast summon creature", "summon");
            
            // Sixth Circle
            Spell(MacroSubType.Dispel, "Dispel", "cast dispel", "dispel spell");
            Spell(MacroSubType.EnergyBolt, "Energy Bolt", "energy bolt", "cast energy bolt", "e bolt");
            Spell(MacroSubType.Explosion, "Explosion", "cast explosion", "explosion spell", "explo");
            Spell(MacroSubType.Invisibility, "Invisibility", "invisibility", "cast invisibility");
            Spell(MacroSubType.Mark, "Mark", "mark", "cast mark");
            Spell(MacroSubType.MassCurse, "Mass Curse", "mass curse", "cast mass curse");
            Spell(MacroSubType.ParalyzeField, "Paralyze Field", "paralyze field", "cast paralyze field", "para field");
            Spell(MacroSubType.Reveal, "Reveal", "cast reveal", "reveal spell");
            
            // Seventh Circle
            Spell(MacroSubType.ChainLightning, "Chain Lightning", "chain lightning", "cast chain lightning", "chain");
            Spell(MacroSubType.EnergyField, "Energy Field", "energy field", "cast energy field", "efield");
            Spell(MacroSubType.FlameStrike, "Flamestrike", "flame strike", "cast flame strike", "cast flamestrike");
            Spell(MacroSubType.GateTravel, "Gate Travel", "gate travel", "cast gate travel", "gate");
            Spell(MacroSubType.ManaVampire, "Mana Vampire", "mana vampire", "cast mana vampire", "mv");
            Spell(MacroSubType.MassDispel, "Mass Dispel", "mass dispel", "cast mass dispel");
            Spell(MacroSubType.MeteorSwarm, "Meteor Swarm", "meteor swarm", "cast meteor swarm", "meteors");
            Spell(MacroSubType.Polymorph, "Polymorph", "polymorph", "cast polymorph", "poly");
            
            // Eighth Circle
            Spell(MacroSubType.Earthquake, "Earthquake", "earthquake", "cast earthquake", "eq", "quake");
            Spell(MacroSubType.EnergyVortex, "Energy Vortex", "energy vortex", "cast energy vortex", "ev", "vortex");
            Spell(MacroSubType.Resurrection, "Resurrection", "resurrection", "cast resurrection", "res", "rez");
            Spell(MacroSubType.AirElemental, "Air Elemental", "air elemental", "cast air elemental", "air ele");
            Spell(MacroSubType.SummonDaemon, "Summon Daemon", "summon daemon", "cast summon daemon", "daemon", "demon");
            Spell(MacroSubType.EarthElemental, "Earth Elemental", "earth elemental", "cast earth elemental", "earth ele");
            Spell(MacroSubType.FireElemental, "Fire Elemental", "fire elemental", "cast fire elemental", "fire ele");
            Spell(MacroSubType.WaterElemental, "Water Elemental", "water elemental", "cast water elemental", "water ele");
            
            // Necromancy
            Spell(MacroSubType.AnimateDead, "Animate Dead", "animate dead", "cast animate dead", "animate");
            Spell(MacroSubType.BloodOath, "Blood Oath", "blood oath", "cast blood oath");
            Spell(MacroSubType.CorpseSkin, "Corpse Skin", "corpse skin", "cast corpse skin");
            Spell(MacroSubType.CurseWeapon, "Curse Weapon", "curse weapon", "cast curse weapon");
            Spell(MacroSubType.EvilOmen, "Evil Omen", "evil omen", "cast evil omen");
            Spell(MacroSubType.HorrificBeast, "Horrific Beast", "horrific beast", "cast horrific beast", "beast form");
            Spell(MacroSubType.LichForm, "Lich Form", "lich form", "cast lich form", "lich");
            Spell(MacroSubType.MindRot, "Mind Rot", "mind rot", "cast mind rot");
            Spell(MacroSubType.PainSpike, "Pain Spike", "pain spike", "cast pain spike", "pain");
            Spell(MacroSubType.PoisonStrike, "Poison Strike", "poison strike", "cast poison strike");
            Spell(MacroSubType.Strangle, "Strangle", "strangle", "cast strangle");
            Spell(MacroSubType.SummonFamilar, "Summon Familiar", "summon familiar", "cast summon familiar", "familiar");
            Spell(MacroSubType.VampiricEmbrace, "Vampiric Embrace", "vampiric embrace", "cast vampiric embrace", "vamp form");
            Spell(MacroSubType.VengefulSpirit, "Vengeful Spirit", "vengeful spirit", "cast vengeful spirit", "revenant");
            Spell(MacroSubType.Wither, "Wither", "wither", "cast wither");
            Spell(MacroSubType.WraithForm, "Wraith Form", "wraith form", "cast wraith form", "wraith");
            Spell(MacroSubType.Exorcism, "Exorcism", "exorcism", "cast exorcism");
            
            // Chivalry
            Spell(MacroSubType.CleanceByFire, "Cleanse By Fire", "cleanse by fire", "cast cleanse by fire", "cleanse");
            Spell(MacroSubType.CloseWounds, "Close Wounds", "close wounds", "cast close wounds");
            Spell(MacroSubType.ConsecrateWeapon, "Consecrate Weapon", "consecrate weapon", "cast consecrate weapon", "consecrate", "cons");
            Spell(MacroSubType.DispelEvil, "Dispel Evil", "dispel evil", "cast dispel evil");
            Spell(MacroSubType.DivineFury, "Divine Fury", "divine fury", "cast divine fury", "fury");
            Spell(MacroSubType.EnemyOfOne, "Enemy of One", "enemy of one", "cast enemy of one", "eoo");
            Spell(MacroSubType.HolyLight, "Holy Light", "holy light", "cast holy light");
            Spell(MacroSubType.NobleSacrifice, "Noble Sacrifice", "noble sacrifice", "cast noble sacrifice");
            Spell(MacroSubType.RemoveCurse, "Remove Curse", "remove curse", "cast remove curse");
            Spell(MacroSubType.SacredJourney, "Sacred Journey", "sacred journey", "cast sacred journey");
            
            // Bushido
            Spell(MacroSubType.HonorableExecution, "Honorable Execution", "honorable execution", "cast honorable execution");
            Spell(MacroSubType.Confidence, "Confidence", "confidence", "cast confidence");
            Spell(MacroSubType.Evasion, "Evasion", "evasion", "cast evasion");
            Spell(MacroSubType.CounterAttack, "Counter Attack", "counter attack", "cast counter attack");
            Spell(MacroSubType.LightingStrike, "Lightning Strike", "lightning strike", "cast lightning strike");
            Spell(MacroSubType.MomentumStrike, "Momentum Strike", "momentum strike", "cast momentum strike");
            
            // Ninjitsu
            Spell(MacroSubType.FocusAttack, "Focus Attack", "focus attack", "cast focus attack");
            Spell(MacroSubType.DeathStrike, "Death Strike", "death strike", "cast death strike");
            Spell(MacroSubType.AnimalForm, "Animal Form", "animal form", "cast animal form");
            Spell(MacroSubType.KiAttack, "Ki Attack", "ki attack", "cast ki attack");
            Spell(MacroSubType.SurpriceAttack, "Surprise Attack", "surprise attack", "cast surprise attack");
            Spell(MacroSubType.Backstab, "Backstab", "backstab", "cast backstab");
            Spell(MacroSubType.Shadowjump, "Shadow Jump", "shadow jump", "cast shadow jump");
            Spell(MacroSubType.MirrorImage, "Mirror Image", "mirror image", "cast mirror image", "mirror");
            
            // Spellweaving
            Spell(MacroSubType.ArcaneCircle, "Arcane Circle", "arcane circle", "cast arcane circle");
            Spell(MacroSubType.GiftOfRenewal, "Gift of Renewal", "gift of renewal", "cast gift of renewal", "renewal");
            Spell(MacroSubType.ImmolatingWeapon, "Immolating Weapon", "immolating weapon", "cast immolating weapon");
            Spell(MacroSubType.Attunement, "Attunement", "attunement", "cast attunement");
            Spell(MacroSubType.Thunderstorm, "Thunderstorm", "thunderstorm", "cast thunderstorm", "thunder");
            Spell(MacroSubType.NaturesFury, "Nature's Fury", "nature's fury", "cast nature's fury", "natures fury");
            Spell(MacroSubType.SummonFey, "Summon Fey", "summon fey", "cast summon fey", "fey");
            Spell(MacroSubType.SummonFiend, "Summon Fiend", "summon fiend", "cast summon fiend", "fiend");
            Spell(MacroSubType.ReaperForm, "Reaper Form", "reaper form", "cast reaper form");
            Spell(MacroSubType.Wildfire, "Wildfire", "wildfire", "cast wildfire");
            Spell(MacroSubType.EssenceOfWind, "Essence of Wind", "essence of wind", "cast essence of wind");
            Spell(MacroSubType.DryadAllure, "Dryad Allure", "dryad allure", "cast dryad allure");
            Spell(MacroSubType.EtherealVoyage, "Ethereal Voyage", "ethereal voyage", "cast ethereal voyage");
            Spell(MacroSubType.WordOfDeath, "Word of Death", "word of death", "cast word of death");
            Spell(MacroSubType.GiftOfLife, "Gift of Life", "gift of life", "cast gift of life");
            Spell(MacroSubType.ArcaneEmpowermen, "Arcane Empowerment", "arcane empowerment", "cast arcane empowerment");
            
            // Mysticism
            Spell(MacroSubType.NetherBolt, "Nether Bolt", "nether bolt", "cast nether bolt");
            Spell(MacroSubType.HealingStone, "Healing Stone", "healing stone", "cast healing stone");
            Spell(MacroSubType.PurgeMagic, "Purge Magic", "purge magic", "cast purge magic", "purge");
            Spell(MacroSubType.Enchant, "Enchant", "enchant", "cast enchant");
            Spell(MacroSubType.Sleep, "Sleep", "sleep", "cast sleep");
            Spell(MacroSubType.EagleStrike, "Eagle Strike", "eagle strike", "cast eagle strike");
            Spell(MacroSubType.AnimatedWeapon, "Animated Weapon", "animated weapon", "cast animated weapon");
            Spell(MacroSubType.StoneForm, "Stone Form", "stone form", "cast stone form");
            Spell(MacroSubType.SpellTrigger, "Spell Trigger", "spell trigger", "cast spell trigger");
            Spell(MacroSubType.MassSleep, "Mass Sleep", "mass sleep", "cast mass sleep");
            Spell(MacroSubType.CleansingWinds, "Cleansing Winds", "cleansing winds", "cast cleansing winds");
            Spell(MacroSubType.Bombard, "Bombard", "bombard", "cast bombard");
            Spell(MacroSubType.SpellPlague, "Spell Plague", "spell plague", "cast spell plague");
            Spell(MacroSubType.HailStorm, "Hail Storm", "hail storm", "cast hail storm", "hail");
            Spell(MacroSubType.NetherCyclone, "Nether Cyclone", "nether cyclone", "cast nether cyclone", "cyclone");
            Spell(MacroSubType.RisingColossus, "Rising Colossus", "rising colossus", "cast rising colossus", "colossus");
            
            // Last spell
            Exact("last spell", MacroType.LastSpell, MacroSubType.MSC_NONE, "Last Spell");
            Exact("cast last spell", MacroType.LastSpell, MacroSubType.MSC_NONE, "Last Spell");
            Exact("again", MacroType.LastSpell, MacroSubType.MSC_NONE, "Last Spell");
            Exact("repeat", MacroType.LastSpell, MacroSubType.MSC_NONE, "Last Spell");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // SKILLS REGISTRATION
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterSkills()
        {
            Skill(MacroSubType.Anatomy, "Anatomy", "anatomy", "use anatomy");
            Skill(MacroSubType.AnimalLore, "Animal Lore", "animal lore", "use animal lore", "lore");
            Skill(MacroSubType.AnimalTaming, "Animal Taming", "animal taming", "use animal taming", "taming", "tame");
            Skill(MacroSubType.ArmsLore, "Arms Lore", "arms lore", "use arms lore");
            Skill(MacroSubType.Begging, "Begging", "begging", "use begging", "beg");
            Skill(MacroSubType.Cartography, "Cartography", "cartography", "use cartography");
            Skill(MacroSubType.DetectingHidden, "Detecting Hidden", "detecting hidden", "use detecting hidden", "detect hidden");
            Skill(MacroSubType.Discordance, "Discordance", "discordance", "use discordance", "discord");
            Skill(MacroSubType.EvaluatingIntelligence, "Eval Int", "evaluating intelligence", "use evaluating intelligence", "eval int", "eval");
            Skill(MacroSubType.ForensicEvaluation, "Forensics", "forensic evaluation", "use forensic evaluation", "forensics");
            Skill(MacroSubType.Hiding, "Hiding", "hiding", "use hiding");
            Skill(MacroSubType.Imbuing, "Imbuing", "imbuing", "use imbuing", "imbue");
            Skill(MacroSubType.Inscription, "Inscription", "inscription", "use inscription", "scribe");
            Skill(MacroSubType.ItemIdentification, "Item ID", "item identification", "use item identification", "item id", "identify");
            Skill(MacroSubType.Meditation, "Meditation", "meditation", "use meditation");
            Skill(MacroSubType.Peacemaking, "Peacemaking", "peacemaking", "use peacemaking");
            Skill(MacroSubType.Poisoning, "Poisoning", "poisoning", "use poisoning");
            Skill(MacroSubType.Provocation, "Provocation", "provocation", "use provocation", "provo");
            Skill(MacroSubType.RemoveTrap, "Remove Trap", "remove trap", "use remove trap");
            Skill(MacroSubType.SpiritSpeak, "Spirit Speak", "spirit speak", "use spirit speak", "spirit");
            Skill(MacroSubType.Stealing, "Stealing", "stealing", "use stealing", "steal");
            Skill(MacroSubType.Stealth, "Stealth", "use stealth");
            Skill(MacroSubType.TasteIdentification, "Taste ID", "taste identification", "use taste identification", "taste id");
            Skill(MacroSubType.Tracking, "Tracking", "tracking", "use tracking");
            
            // Last skill
            Exact("last skill", MacroType.LastSkill, MacroSubType.MSC_NONE, "Last Skill");
            Exact("use last skill", MacroType.LastSkill, MacroSubType.MSC_NONE, "Last Skill");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // TARGETING REGISTRATION
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterTargeting()
        {
            // Self targeting
            Exact("target self", MacroType.TargetSelf, MacroSubType.MSC_NONE, "Target Self");
            Exact("self target", MacroType.TargetSelf, MacroSubType.MSC_NONE, "Target Self");
            Exact("target me", MacroType.TargetSelf, MacroSubType.MSC_NONE, "Target Self");
            Exact("target myself", MacroType.TargetSelf, MacroSubType.MSC_NONE, "Target Self");
            
            // Last target
            Exact("last target", MacroType.LastTarget, MacroSubType.MSC_NONE, "Last Target");
            Exact("target last", MacroType.LastTarget, MacroSubType.MSC_NONE, "Last Target");
            
            // Next target
            Exact("next target", MacroType.TargetNext, MacroSubType.MSC_NONE, "Target Next");
            Exact("target next", MacroType.TargetNext, MacroSubType.MSC_NONE, "Target Next");
            
            // Current target
            Exact("current target", MacroType.CurrentTarget, MacroSubType.MSC_NONE, "Current Target");
            Exact("target current", MacroType.CurrentTarget, MacroSubType.MSC_NONE, "Current Target");
            
            // Select nearest/next/previous - Hostile
            Exact("nearest hostile", MacroType.SelectNearest, MacroSubType.Hostile, "Nearest Hostile");
            Exact("nearest enemy", MacroType.SelectNearest, MacroSubType.Hostile, "Nearest Hostile");
            Exact("select nearest hostile", MacroType.SelectNearest, MacroSubType.Hostile, "Nearest Hostile");
            Exact("next hostile", MacroType.SelectNext, MacroSubType.Hostile, "Next Hostile");
            Exact("select next hostile", MacroType.SelectNext, MacroSubType.Hostile, "Next Hostile");
            Exact("previous hostile", MacroType.SelectPrevious, MacroSubType.Hostile, "Previous Hostile");
            
            // Select nearest/next/previous - Party
            Exact("nearest party", MacroType.SelectNearest, MacroSubType.Party, "Nearest Party");
            Exact("nearest ally", MacroType.SelectNearest, MacroSubType.Party, "Nearest Party");
            Exact("nearest friendly", MacroType.SelectNearest, MacroSubType.Party, "Nearest Party");
            Exact("select nearest party", MacroType.SelectNearest, MacroSubType.Party, "Nearest Party");
            Exact("next party", MacroType.SelectNext, MacroSubType.Party, "Next Party");
            Exact("previous party", MacroType.SelectPrevious, MacroSubType.Party, "Previous Party");
            
            // Select nearest/next/previous - Follower
            Exact("nearest follower", MacroType.SelectNearest, MacroSubType.Follower, "Nearest Follower");
            Exact("nearest pet", MacroType.SelectNearest, MacroSubType.Follower, "Nearest Follower");
            Exact("next follower", MacroType.SelectNext, MacroSubType.Follower, "Next Follower");
            
            // Select nearest/next/previous - Mobile  
            Exact("nearest mobile", MacroType.SelectNearest, MacroSubType.Mobile, "Nearest Mobile");
            Exact("next mobile", MacroType.SelectNext, MacroSubType.Mobile, "Next Mobile");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // COMBAT REGISTRATION
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterCombat()
        {
            // Attack
            Exact("attack last", MacroType.AttackLast, MacroSubType.MSC_NONE, "Attack Last");
            Exact("last attack", MacroType.AttackLast, MacroSubType.MSC_NONE, "Attack Last");
            Exact("kill", MacroType.AttackLast, MacroSubType.MSC_NONE, "Attack Last");
            Exact("attack target", MacroType.AttackSelectedTarget, MacroSubType.MSC_NONE, "Attack Target");
            Exact("attack selected target", MacroType.AttackSelectedTarget, MacroSubType.MSC_NONE, "Attack Target");
            
            // War/Peace
            Exact("war mode", MacroType.WarPeace, MacroSubType.MSC_NONE, "War/Peace");
            Exact("peace mode", MacroType.WarPeace, MacroSubType.MSC_NONE, "War/Peace");
            Exact("toggle war mode", MacroType.WarPeace, MacroSubType.MSC_NONE, "War/Peace");
            Exact("toggle war", MacroType.WarPeace, MacroSubType.MSC_NONE, "War/Peace");
            
            // Abilities
            Exact("primary ability", MacroType.PrimaryAbility, MacroSubType.MSC_NONE, "Primary Ability");
            Exact("use primary ability", MacroType.PrimaryAbility, MacroSubType.MSC_NONE, "Primary Ability");
            Exact("use primary", MacroType.PrimaryAbility, MacroSubType.MSC_NONE, "Primary Ability");
            Exact("secondary ability", MacroType.SecondaryAbility, MacroSubType.MSC_NONE, "Secondary Ability");
            Exact("use secondary ability", MacroType.SecondaryAbility, MacroSubType.MSC_NONE, "Secondary Ability");
            Exact("use secondary", MacroType.SecondaryAbility, MacroSubType.MSC_NONE, "Secondary Ability");
            
            // Arm/Disarm
            Exact("arm left hand", MacroType.ArmDisarm, MacroSubType.LeftHand, "Arm Left");
            Exact("disarm left hand", MacroType.ArmDisarm, MacroSubType.LeftHand, "Disarm Left");
            Exact("arm right hand", MacroType.ArmDisarm, MacroSubType.RightHand, "Arm Right");
            Exact("disarm right hand", MacroType.ArmDisarm, MacroSubType.RightHand, "Disarm Right");
            Exact("equip last weapon", MacroType.EquipLastWeapon, MacroSubType.MSC_NONE, "Equip Weapon");
            Exact("last weapon", MacroType.EquipLastWeapon, MacroSubType.MSC_NONE, "Equip Weapon");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // POTIONS REGISTRATION
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterPotions()
        {
            Exact("heal potion", MacroType.UsePotion, MacroSubType.HealPotion, "Heal Potion");
            Exact("use heal potion", MacroType.UsePotion, MacroSubType.HealPotion, "Heal Potion");
            Exact("red potion", MacroType.UsePotion, MacroSubType.HealPotion, "Heal Potion");
            
            Exact("cure potion", MacroType.UsePotion, MacroSubType.CurePotion, "Cure Potion");
            Exact("use cure potion", MacroType.UsePotion, MacroSubType.CurePotion, "Cure Potion");
            
            Exact("refresh potion", MacroType.UsePotion, MacroSubType.RefreshPotion, "Refresh Potion");
            Exact("use refresh potion", MacroType.UsePotion, MacroSubType.RefreshPotion, "Refresh Potion");
            
            Exact("agility potion", MacroType.UsePotion, MacroSubType.AgilityPotion, "Agility Potion");
            Exact("use agility potion", MacroType.UsePotion, MacroSubType.AgilityPotion, "Agility Potion");
            
            Exact("strength potion", MacroType.UsePotion, MacroSubType.StrengthPotion, "Strength Potion");
            Exact("use strength potion", MacroType.UsePotion, MacroSubType.StrengthPotion, "Strength Potion");
            
            Exact("explosion potion", MacroType.UsePotion, MacroSubType.ExplosionPotion, "Explosion Potion");
            Exact("use explosion potion", MacroType.UsePotion, MacroSubType.ExplosionPotion, "Explosion Potion");
            
            // Best potions
            Exact("best heal potion", MacroType.UseObject, MacroSubType.BestHealPotion, "Best Heal Potion");
            Exact("best cure potion", MacroType.UseObject, MacroSubType.BestCurePotion, "Best Cure Potion");
            Exact("best refresh potion", MacroType.UseObject, MacroSubType.BestRefreshPotion, "Best Refresh");
            
            // Items
            Exact("enchanted apple", MacroType.UseObject, MacroSubType.EnchantedApple, "Enchanted Apple");
            Exact("use enchanted apple", MacroType.UseObject, MacroSubType.EnchantedApple, "Enchanted Apple");
            Exact("orange petals", MacroType.UseObject, MacroSubType.OrangePetals, "Orange Petals");
            Exact("smoke bomb", MacroType.UseObject, MacroSubType.SmokeBomb, "Smoke Bomb");
            Exact("trapped box", MacroType.UseObject, MacroSubType.TrappedBox, "Trapped Box");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // GUMPS REGISTRATION
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterGumps()
        {
            // Backpack/Inventory
            Gump("backpack", MacroSubType.Backpack, "Backpack");
            Gump("inventory", MacroSubType.Backpack, "Backpack");
            Gump("bag", MacroSubType.Backpack, "Backpack");
            
            // Paperdoll/Character
            Gump("paperdoll", MacroSubType.Paperdoll, "Paperdoll");
            Gump("paper doll", MacroSubType.Paperdoll, "Paperdoll");
            Gump("character", MacroSubType.Paperdoll, "Paperdoll");
            
            // Status
            Gump("status", MacroSubType.Status, "Status");
            Gump("stats", MacroSubType.Status, "Status");
            
            // Journal
            Gump("journal", MacroSubType.Journal, "Journal");
            
            // Skills
            Gump("skills", MacroSubType.Skills, "Skills");
            
            // Spellbook
            Gump("spellbook", MacroSubType.MageSpellbook, "Spellbook");
            Gump("spell book", MacroSubType.MageSpellbook, "Spellbook");
            
            // Map
            Gump("map", MacroSubType.WorldMap, "World Map");
            Gump("world map", MacroSubType.WorldMap, "World Map");
            Gump("minimap", MacroSubType.Overview, "Overview");
            Gump("mini map", MacroSubType.Overview, "Overview");
            Gump("overview", MacroSubType.Overview, "Overview");
            Gump("radar", MacroSubType.Overview, "Overview");
            
            // Options/Settings
            Gump("options", MacroSubType.Configuration, "Options");
            Gump("settings", MacroSubType.Configuration, "Options");
            Gump("configuration", MacroSubType.Configuration, "Options");
            
            // Party
            Gump("party", MacroSubType.PartyManifest, "Party");
            
            // Guild
            Gump("guild", MacroSubType.Guild, "Guild");
            
            // Quest Log
            Gump("quests", MacroSubType.QuestLog, "Quest Log");
            Gump("quest log", MacroSubType.QuestLog, "Quest Log");
            
            // Chat
            Gump("chat", MacroSubType.Chat, "Chat");
            
            // Close all
            Exact("close all", MacroType.CloseGump, MacroSubType.MSC_NONE, "Close All Gumps");
            Exact("close gump", MacroType.CloseGump, MacroSubType.MSC_NONE, "Close All Gumps");
            Exact("close all gumps", MacroType.KillGumpOpen, MacroSubType.MSC_NONE, "Close All Gumps");
            Exact("close health bars", MacroType.CloseAllHealthBars, MacroSubType.MSC_NONE, "Close Health Bars");
            Exact("close inactive health bars", MacroType.CloseInactiveHealthBars, MacroSubType.MSC_NONE, "Close Inactive");
            Exact("close corpses", MacroType.CloseCorpses, MacroSubType.MSC_NONE, "Close Corpses");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // PET COMMANDS REGISTRATION - Uses Contains matching for flexibility
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterPetCommands()
        {
            // All kill/attack
            Contains("all kill", "all kill", "Pet: All Kill");
            Contains("kill", "all kill", "Pet: All Kill");
            Contains("all attack", "all kill", "Pet: All Kill");
            Contains("attack", "all kill", "Pet: All Kill");
            Contains("sick em", "all kill", "Pet: All Kill");
            Contains("sic em", "all kill", "Pet: All Kill");
            
            // All follow
            Contains("all follow", "all follow me", "Pet: All Follow");
            Contains("follow me", "all follow me", "Pet: All Follow");
            Contains("heel", "all follow me", "Pet: All Follow");
            Contains("come", "all come", "Pet: All Come");
            
            // All stay
            Contains("all stay", "all stay", "Pet: All Stay");
            Contains("stay", "all stay", "Pet: All Stay");
            Contains("wait", "all stay", "Pet: All Stay");
            
            // All guard
            Contains("all guard", "all guard me", "Pet: All Guard");
            Contains("guard me", "all guard me", "Pet: All Guard");
            Contains("protect", "all guard me", "Pet: All Guard");
            
            // All stop
            Contains("all stop", "all stop", "Pet: All Stop");
            Contains("halt", "all stop", "Pet: All Stop");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // EMERGENCY COMMANDS
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterEmergency()
        {
            // Bandage
            Exact("bandage self", MacroType.BandageSelf, MacroSubType.MSC_NONE, "Bandage Self");
            Exact("self bandage", MacroType.BandageSelf, MacroSubType.MSC_NONE, "Bandage Self");
            Exact("heal me", MacroType.BandageSelf, MacroSubType.MSC_NONE, "Bandage Self");
            Exact("bandage target", MacroType.BandageTarget, MacroSubType.MSC_NONE, "Bandage Target");
            Exact("target bandage", MacroType.BandageTarget, MacroSubType.MSC_NONE, "Bandage Target");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // VIEW CONTROLS
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterView()
        {
            // Zoom
            Exact("zoom in", MacroType.Zoom, MacroSubType.ZoomIn, "Zoom In");
            Exact("zoom out", MacroType.Zoom, MacroSubType.ZoomOut, "Zoom Out");
            Exact("default zoom", MacroType.Zoom, MacroSubType.DefaultZoom, "Default Zoom");
            Exact("reset zoom", MacroType.Zoom, MacroSubType.DefaultZoom, "Default Zoom");
            
            // Names
            Exact("all names", MacroType.AllNames, MacroSubType.MSC_NONE, "All Names");
            Exact("show all names", MacroType.AllNames, MacroSubType.MSC_NONE, "All Names");
            Exact("names on", MacroType.NamesOnOff, MacroSubType.MSC_NONE, "Names Toggle");
            Exact("names off", MacroType.NamesOnOff, MacroSubType.MSC_NONE, "Names Toggle");
            Exact("toggle names", MacroType.NamesOnOff, MacroSubType.MSC_NONE, "Names Toggle");
            
            // Transparency
            Exact("circle transparency", MacroType.CircleTrans, MacroSubType.MSC_NONE, "Circle Trans");
            Exact("circle trans", MacroType.CircleTrans, MacroSubType.MSC_NONE, "Circle Trans");
            
            // Visual toggles
            Exact("toggle roofs", MacroType.ToggleDrawRoofs, MacroSubType.MSC_NONE, "Toggle Roofs");
            Exact("hide roofs", MacroType.ToggleDrawRoofs, MacroSubType.MSC_NONE, "Toggle Roofs");
            Exact("toggle trees", MacroType.ToggleTreeStumps, MacroSubType.MSC_NONE, "Toggle Trees");
            Exact("toggle vegetation", MacroType.ToggleVegetation, MacroSubType.MSC_NONE, "Toggle Vegetation");
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // MISC COMMANDS
        // ═══════════════════════════════════════════════════════════════════════
        
        private void RegisterMisc()
        {
            // Communication
            Exact("bow", MacroType.Bow, MacroSubType.MSC_NONE, "Bow");
            Exact("salute", MacroType.Salute, MacroSubType.MSC_NONE, "Salute");
            
            // Movement
            Exact("always run", MacroType.AlwaysRun, MacroSubType.MSC_NONE, "Always Run");
            Exact("toggle run", MacroType.AlwaysRun, MacroSubType.MSC_NONE, "Always Run");
            Exact("open door", MacroType.OpenDoor, MacroSubType.MSC_NONE, "Open Door");
            
            // Last object
            Exact("last object", MacroType.LastObject, MacroSubType.MSC_NONE, "Last Object");
            Exact("use last object", MacroType.LastObject, MacroSubType.MSC_NONE, "Last Object");
            Exact("use item in hand", MacroType.UseItemInHand, MacroSubType.MSC_NONE, "Use Item In Hand");
            
            // Virtues
            Exact("invoke honor", MacroType.InvokeVirtue, MacroSubType.Honor, "Honor");
            Exact("honor", MacroType.InvokeVirtue, MacroSubType.Honor, "Honor");
            Exact("invoke sacrifice", MacroType.InvokeVirtue, MacroSubType.Sacrifice, "Sacrifice");
            Exact("invoke valor", MacroType.InvokeVirtue, MacroSubType.Valor, "Valor");
            
            // Buffs
            Exact("toggle buffs", MacroType.ToggleBuffIconGump, MacroSubType.MSC_NONE, "Toggle Buffs");
            Exact("buff icon", MacroType.ToggleBuffIconGump, MacroSubType.MSC_NONE, "Toggle Buffs");

            // Gargoyle
            Exact("fly", MacroType.ToggleGargoyleFly, MacroSubType.MSC_NONE, "Toggle Fly");
            Exact("toggle fly", MacroType.ToggleGargoyleFly, MacroSubType.MSC_NONE, "Toggle Fly");

            // Save
            Exact("save desktop", MacroType.SaveDesktop, MacroSubType.MSC_NONE, "Save Desktop");
            Exact("save layout", MacroType.SaveDesktop, MacroSubType.MSC_NONE, "Save Desktop");

            // Grab
            Exact("grab", MacroType.Grab, MacroSubType.MSC_NONE, "Grab");
            Exact("grab item", MacroType.Grab, MacroSubType.MSC_NONE, "Grab");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════════

        private void RecordExecution(string command)
        {
            _lastExecutedCommand = command;
            _lastExecutedTicks = DateTime.UtcNow.Ticks;
        }

        private void Shortcut(string trigger, MacroType type, MacroSubType sub, string label)
        {
            var action = new BasicCommandAction(type, sub, label);
            _shortcuts[trigger] = action;
        }

        private void Exact(string phrase, MacroType type, MacroSubType sub, string label)
        {
            var action = new BasicCommandAction(type, sub, label);
            _exactLookup[phrase] = action;
        }

        private void Spell(MacroSubType sub, string label, params string[] phrases)
        {
            var action = new BasicCommandAction(MacroType.CastSpell, sub, label);
            foreach (var phrase in phrases)
            {
                _exactLookup[phrase] = action;
            }
        }

        private void Skill(MacroSubType sub, string label, params string[] phrases)
        {
            var action = new BasicCommandAction(MacroType.UseSkill, sub, label);
            foreach (var phrase in phrases)
            {
                _exactLookup[phrase] = action;
            }
        }

        private void Gump(string trigger, MacroSubType sub, string label)
        {
            // Register open, close, and bare versions
            var openAction = new BasicCommandAction(MacroType.Open, sub, $"Open {label}");
            var closeAction = new BasicCommandAction(MacroType.Close, sub, $"Close {label}");

            _exactLookup[trigger] = openAction;
            _exactLookup[$"open {trigger}"] = openAction;
            _exactLookup[$"show {trigger}"] = openAction;
            _exactLookup[$"close {trigger}"] = closeAction;
            _exactLookup[$"hide {trigger}"] = closeAction;
        }

        private void Contains(string trigger, string command, string label)
        {
            var action = new BasicCommandAction(command, label);
            _containsPatterns.Add((trigger, action));
        }
    }

    /// <summary>
    /// Represents a command action to execute.
    /// </summary>
    internal sealed class BasicCommandAction
    {
        public MacroType MacroType { get; }
        public MacroSubType MacroSubType { get; }
        public string Label { get; }
        public string RawCommand { get; }
        public bool IsMacro => string.IsNullOrEmpty(RawCommand);

        public BasicCommandAction(MacroType type, MacroSubType sub, string label)
        {
            MacroType = type;
            MacroSubType = sub;
            Label = label;
            RawCommand = null;
        }

        public BasicCommandAction(string rawCommand, string label)
        {
            MacroType = MacroType.None;
            MacroSubType = MacroSubType.MSC_NONE;
            Label = label;
            RawCommand = rawCommand;
        }

        /// <summary>
        /// Execute this command action. Dispatches to main game thread since
        /// voice recognition callbacks happen on the audio thread.
        /// </summary>
        public void Execute(World world)
        {
            // Must dispatch to main thread - voice callbacks happen on audio thread
            Client.Game.EnqueueAction(0, () =>
            {
                if (IsMacro)
                {
                    var macro = new MacroObject(MacroType, MacroSubType);
                    world.Macros.SetMacroToExecute(macro);
                    world.Macros.Update();
                }
                else
                {
                    // Pet command or raw say
                    GameActions.Say(RawCommand);
                }
            });
        }
    }
    
    /// <summary>
    /// Result of a command match.
    /// </summary>
    internal readonly struct BasicCommandResult
    {
        public BasicCommandAction Action { get; }
        public string MatchedPhrase { get; }
        public MatchType Type { get; }
        
        public BasicCommandResult(BasicCommandAction action, string phrase, MatchType type)
        {
            Action = action;
            MatchedPhrase = phrase;
            Type = type;
        }
    }
    
    internal enum MatchType
    {
        None,
        Shortcut,   // Single-word combat shortcut
        Exact,      // Full phrase exact match
        Contains    // Phrase contains trigger
    }
}
