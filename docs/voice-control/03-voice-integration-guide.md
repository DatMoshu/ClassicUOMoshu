# Voice Control Integration Guide

This document provides implementation guidance for integrating voice commands with the ClassicUO macro system.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Voice Interaction Stack                       │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │  Microphone │→ │     VAD     │→ │     STT Engine          │  │
│  │   Capture   │  │  (Silero)   │  │  (Vosk/Whisper)         │  │
│  └─────────────┘  └─────────────┘  └───────────┬─────────────┘  │
│                                                 │                │
│  ┌─────────────────────────────────────────────▼─────────────┐  │
│  │              Action Inference Engine                       │  │
│  │  ┌─────────────────┐  ┌─────────────────────────────────┐ │  │
│  │  │  Command Router │  │  LLM Backend (optional)         │ │  │
│  │  │  (Token Scorer) │  │  (Ollama - gemma4)              │ │  │
│  │  └────────┬────────┘  └───────────────┬─────────────────┘ │  │
│  │           │                           │                    │  │
│  │           └───────────┬───────────────┘                    │  │
│  │                       ▼                                    │  │
│  │           ┌───────────────────────┐                        │  │
│  │           │   Action Resolver     │                        │  │
│  │           │   (Macro Mapping)     │                        │  │
│  │           └───────────┬───────────┘                        │  │
│  └───────────────────────┼───────────────────────────────────┘  │
│                          ▼                                      │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                   MacroManager                             │  │
│  │   ┌─────────────┐  ┌─────────────┐  ┌─────────────────┐   │  │
│  │   │ SetMacroTo  │→ │   Update()  │→ │   Process()     │   │  │
│  │   │   Execute   │  │             │  │                 │   │  │
│  │   └─────────────┘  └─────────────┘  └─────────────────┘   │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Voice-to-Macro Pipeline

### Step 1: Speech Recognition Output
```csharp
// From VoiceInteractionManager or ActionInferenceEngine
public record VoiceTranscript(
    string Text,
    float Confidence,
    long TimestampMs,
    bool IsFinal
);

// Example: "cast greater heal on party"
```

### Step 2: Command Parsing
```csharp
public record ParsedVoiceCommand(
    VoiceCommandType Type,
    string? SpellName,
    string? TargetName,
    MacroSubType? TargetType,
    Dictionary<string, string> Parameters
);

// Example parsed:
// Type: CastOnTarget
// SpellName: "greater heal"  
// TargetType: Party
```

### Step 3: Macro Resolution
```csharp
public class VoiceMacroResolver
{
    private readonly MacroManager _macroManager;
    private readonly World _world;
    
    public MacroObject? ResolveVoiceCommand(ParsedVoiceCommand cmd)
    {
        return cmd.Type switch
        {
            VoiceCommandType.CastSpell => CreateSpellMacro(cmd.SpellName),
            VoiceCommandType.CastOnTarget => CreateCastOnTargetMacro(cmd),
            VoiceCommandType.UseSkill => CreateSkillMacro(cmd.SpellName),
            VoiceCommandType.OpenGump => CreateOpenMacro(cmd.Parameters["gump"]),
            VoiceCommandType.PetCommand => CreatePetMacro(cmd),
            VoiceCommandType.QuickChat => CreateSayMacro(cmd.Parameters["message"]),
            _ => null
        };
    }
}
```

### Step 4: Macro Execution
```csharp
// Execute through MacroManager
public void ExecuteVoiceCommand(MacroObject macro)
{
    // Queue the macro for execution
    _macroManager.SetMacroToExecute(macro);
    
    // Optional: Chain multiple macro objects for complex commands
    if (macro.Next != null)
    {
        // Linked list handles chaining automatically
    }
}
```

---

## Spell Name Mapping

### Comprehensive Spell Dictionary
```csharp
public static class SpellVoiceMap
{
    public static readonly Dictionary<string, MacroSubType> Spells = new()
    {
        // Magery - First Circle
        ["clumsy"] = MacroSubType.Clumsy,
        ["make clumsy"] = MacroSubType.Clumsy,
        ["create food"] = MacroSubType.CreateFood,
        ["food"] = MacroSubType.CreateFood,
        ["feeblemind"] = MacroSubType.Feeblemind,
        ["feeble mind"] = MacroSubType.Feeblemind,
        ["heal"] = MacroSubType.Heal,
        ["minor heal"] = MacroSubType.Heal,
        ["magic arrow"] = MacroSubType.MagicArrow,
        ["arrow"] = MacroSubType.MagicArrow,
        ["night sight"] = MacroSubType.NightSight,
        ["nightsight"] = MacroSubType.NightSight,
        ["reactive armor"] = MacroSubType.ReactiveArmor,
        ["reactive"] = MacroSubType.ReactiveArmor,
        ["weaken"] = MacroSubType.Weaken,
        
        // Second Circle
        ["agility"] = MacroSubType.Agility,
        ["cunning"] = MacroSubType.Cunning,
        ["cure"] = MacroSubType.Cure,
        ["harm"] = MacroSubType.Harm,
        ["magic trap"] = MacroSubType.MagicTrap,
        ["magic untrap"] = MacroSubType.MagicUntrap,
        ["protection"] = MacroSubType.Protection,
        ["strength"] = MacroSubType.Strength,
        
        // Third Circle
        ["bless"] = MacroSubType.Bless,
        ["fireball"] = MacroSubType.Fireball,
        ["fire ball"] = MacroSubType.Fireball,
        ["magic lock"] = MacroSubType.MagicLock,
        ["poison"] = MacroSubType.Poison,
        ["telekinesis"] = MacroSubType.Telekinesis,
        ["teleport"] = MacroSubType.Teleport,
        ["tele"] = MacroSubType.Teleport,
        ["unlock"] = MacroSubType.Unlock,
        ["wall of stone"] = MacroSubType.WallOfStone,
        ["stone wall"] = MacroSubType.WallOfStone,
        
        // Fourth Circle
        ["arch cure"] = MacroSubType.ArchCure,
        ["archcure"] = MacroSubType.ArchCure,
        ["arch protection"] = MacroSubType.ArchProtection,
        ["curse"] = MacroSubType.Curse,
        ["fire field"] = MacroSubType.FireField,
        ["greater heal"] = MacroSubType.GreaterHeal,
        ["gheal"] = MacroSubType.GreaterHeal,
        ["g heal"] = MacroSubType.GreaterHeal,
        ["lightning"] = MacroSubType.Lightning,
        ["bolt"] = MacroSubType.Lightning,
        ["mana drain"] = MacroSubType.ManaDrain,
        ["drain"] = MacroSubType.ManaDrain,
        ["recall"] = MacroSubType.Recall,
        
        // Fifth Circle
        ["blade spirits"] = MacroSubType.BladeSpirits,
        ["blades"] = MacroSubType.BladeSpirits,
        ["dispel field"] = MacroSubType.DispellField,
        ["incognito"] = MacroSubType.Incognito,
        ["magic reflection"] = MacroSubType.MagicReflection,
        ["reflect"] = MacroSubType.MagicReflection,
        ["mind blast"] = MacroSubType.MindBlast,
        ["paralyze"] = MacroSubType.Paralyze,
        ["para"] = MacroSubType.Paralyze,
        ["poison field"] = MacroSubType.PoisonField,
        ["summon creature"] = MacroSubType.SummonCreature,
        ["summon"] = MacroSubType.SummonCreature,
        
        // Sixth Circle
        ["dispel"] = MacroSubType.Dispel,
        ["energy bolt"] = MacroSubType.EnergyBolt,
        ["ebolt"] = MacroSubType.EnergyBolt,
        ["e bolt"] = MacroSubType.EnergyBolt,
        ["explosion"] = MacroSubType.Explosion,
        ["explode"] = MacroSubType.Explosion,
        ["invisibility"] = MacroSubType.Invisibility,
        ["invis"] = MacroSubType.Invisibility,
        ["mark"] = MacroSubType.Mark,
        ["mass curse"] = MacroSubType.MassCurse,
        ["paralyze field"] = MacroSubType.ParalyzeField,
        ["para field"] = MacroSubType.ParalyzeField,
        ["reveal"] = MacroSubType.Reveal,
        
        // Seventh Circle
        ["chain lightning"] = MacroSubType.ChainLightning,
        ["chain"] = MacroSubType.ChainLightning,
        ["energy field"] = MacroSubType.EnergyField,
        ["flamestrike"] = MacroSubType.FlameStrike,
        ["flame strike"] = MacroSubType.FlameStrike,
        ["fs"] = MacroSubType.FlameStrike,
        ["gate travel"] = MacroSubType.GateTravel,
        ["gate"] = MacroSubType.GateTravel,
        ["mana vampire"] = MacroSubType.ManaVampire,
        ["mass dispel"] = MacroSubType.MassDispel,
        ["meteor swarm"] = MacroSubType.MeteorSwarm,
        ["meteors"] = MacroSubType.MeteorSwarm,
        ["polymorph"] = MacroSubType.Polymorph,
        
        // Eighth Circle
        ["earthquake"] = MacroSubType.Earthquake,
        ["quake"] = MacroSubType.Earthquake,
        ["energy vortex"] = MacroSubType.EnergyVortex,
        ["ev"] = MacroSubType.EnergyVortex,
        ["vortex"] = MacroSubType.EnergyVortex,
        ["resurrection"] = MacroSubType.Resurrection,
        ["res"] = MacroSubType.Resurrection,
        ["rez"] = MacroSubType.Resurrection,
        ["air elemental"] = MacroSubType.AirElemental,
        ["summon daemon"] = MacroSubType.SummonDaemon,
        ["daemon"] = MacroSubType.SummonDaemon,
        ["demon"] = MacroSubType.SummonDaemon,
        ["earth elemental"] = MacroSubType.EarthElemental,
        ["fire elemental"] = MacroSubType.FireElemental,
        ["water elemental"] = MacroSubType.WaterElemental,
        
        // Necromancy
        ["animate dead"] = MacroSubType.AnimateDead,
        ["animate"] = MacroSubType.AnimateDead,
        ["blood oath"] = MacroSubType.BloodOath,
        ["corpse skin"] = MacroSubType.CorpseSkin,
        ["curse weapon"] = MacroSubType.CurseWeapon,
        ["evil omen"] = MacroSubType.EvilOmen,
        ["horrific beast"] = MacroSubType.HorrificBeast,
        ["beast form"] = MacroSubType.HorrificBeast,
        ["lich form"] = MacroSubType.LichForm,
        ["lich"] = MacroSubType.LichForm,
        ["mind rot"] = MacroSubType.MindRot,
        ["pain spike"] = MacroSubType.PainSpike,
        ["pain"] = MacroSubType.PainSpike,
        ["poison strike"] = MacroSubType.PoisonStrike,
        ["strangle"] = MacroSubType.Strangle,
        ["summon familiar"] = MacroSubType.SummonFamilar,
        ["familiar"] = MacroSubType.SummonFamilar,
        ["vampiric embrace"] = MacroSubType.VampiricEmbrace,
        ["vamp form"] = MacroSubType.VampiricEmbrace,
        ["vengeful spirit"] = MacroSubType.VengefulSpirit,
        ["revenant"] = MacroSubType.VengefulSpirit,
        ["wither"] = MacroSubType.Wither,
        ["wraith form"] = MacroSubType.WraithForm,
        ["wraith"] = MacroSubType.WraithForm,
        ["exorcism"] = MacroSubType.Exorcism,
        
        // Chivalry
        ["cleanse by fire"] = MacroSubType.CleanceByFire,
        ["cleanse"] = MacroSubType.CleanceByFire,
        ["close wounds"] = MacroSubType.CloseWounds,
        ["consecrate weapon"] = MacroSubType.ConsecrateWeapon,
        ["consecrate"] = MacroSubType.ConsecrateWeapon,
        ["dispel evil"] = MacroSubType.DispelEvil,
        ["divine fury"] = MacroSubType.DivineFury,
        ["fury"] = MacroSubType.DivineFury,
        ["enemy of one"] = MacroSubType.EnemyOfOne,
        ["eoo"] = MacroSubType.EnemyOfOne,
        ["holy light"] = MacroSubType.HolyLight,
        ["noble sacrifice"] = MacroSubType.NobleSacrifice,
        ["sacrifice"] = MacroSubType.NobleSacrifice,
        ["remove curse"] = MacroSubType.RemoveCurse,
        ["sacred journey"] = MacroSubType.SacredJourney,
        
        // Bushido
        ["honorable execution"] = MacroSubType.HonorableExecution,
        ["confidence"] = MacroSubType.Confidence,
        ["evasion"] = MacroSubType.Evasion,
        ["counter attack"] = MacroSubType.CounterAttack,
        ["lightning strike"] = MacroSubType.LightingStrike,
        ["momentum strike"] = MacroSubType.MomentumStrike,
        
        // Ninjitsu
        ["focus attack"] = MacroSubType.FocusAttack,
        ["death strike"] = MacroSubType.DeathStrike,
        ["animal form"] = MacroSubType.AnimalForm,
        ["ki attack"] = MacroSubType.KiAttack,
        ["surprise attack"] = MacroSubType.SurpriceAttack,
        ["backstab"] = MacroSubType.Backstab,
        ["shadow jump"] = MacroSubType.Shadowjump,
        ["mirror image"] = MacroSubType.MirrorImage,
        ["mirror"] = MacroSubType.MirrorImage,
        
        // Spellweaving
        ["arcane circle"] = MacroSubType.ArcaneCircle,
        ["gift of renewal"] = MacroSubType.GiftOfRenewal,
        ["renewal"] = MacroSubType.GiftOfRenewal,
        ["immolating weapon"] = MacroSubType.ImmolatingWeapon,
        ["attunement"] = MacroSubType.Attunement,
        ["thunderstorm"] = MacroSubType.Thunderstorm,
        ["thunder"] = MacroSubType.Thunderstorm,
        ["natures fury"] = MacroSubType.NaturesFury,
        ["summon fey"] = MacroSubType.SummonFey,
        ["fey"] = MacroSubType.SummonFey,
        ["summon fiend"] = MacroSubType.SummonFiend,
        ["fiend"] = MacroSubType.SummonFiend,
        ["reaper form"] = MacroSubType.ReaperForm,
        ["wildfire"] = MacroSubType.Wildfire,
        ["essence of wind"] = MacroSubType.EssenceOfWind,
        ["dryad allure"] = MacroSubType.DryadAllure,
        ["ethereal voyage"] = MacroSubType.EtherealVoyage,
        ["word of death"] = MacroSubType.WordOfDeath,
        ["gift of life"] = MacroSubType.GiftOfLife,
        ["arcane empowerment"] = MacroSubType.ArcaneEmpowermen,
        
        // Mysticism
        ["nether bolt"] = MacroSubType.NetherBolt,
        ["healing stone"] = MacroSubType.HealingStone,
        ["purge magic"] = MacroSubType.PurgeMagic,
        ["purge"] = MacroSubType.PurgeMagic,
        ["enchant"] = MacroSubType.Enchant,
        ["sleep"] = MacroSubType.Sleep,
        ["eagle strike"] = MacroSubType.EagleStrike,
        ["animated weapon"] = MacroSubType.AnimatedWeapon,
        ["stone form"] = MacroSubType.StoneForm,
        ["spell trigger"] = MacroSubType.SpellTrigger,
        ["mass sleep"] = MacroSubType.MassSleep,
        ["cleansing winds"] = MacroSubType.CleansingWinds,
        ["bombard"] = MacroSubType.Bombard,
        ["spell plague"] = MacroSubType.SpellPlague,
        ["hail storm"] = MacroSubType.HailStorm,
        ["hail"] = MacroSubType.HailStorm,
        ["nether cyclone"] = MacroSubType.NetherCyclone,
        ["cyclone"] = MacroSubType.NetherCyclone,
        ["rising colossus"] = MacroSubType.RisingColossus,
        ["colossus"] = MacroSubType.RisingColossus,
        
        // Masteries
        ["inspire"] = MacroSubType.Inspire,
        ["invigorate"] = MacroSubType.Invigorate,
        ["resilience"] = MacroSubType.Resilience,
        ["perseverance"] = MacroSubType.Perseverance,
        ["tribulation"] = MacroSubType.Tribulation,
        ["despair"] = MacroSubType.Despair,
    };
    
    public static MacroSubType? FindSpell(string input)
    {
        var normalized = input.ToLowerInvariant().Trim();
        
        // Exact match first
        if (Spells.TryGetValue(normalized, out var spell))
            return spell;
        
        // Fuzzy match
        var bestMatch = Spells.Keys
            .Select(k => (Key: k, Score: FuzzyMatch(normalized, k)))
            .Where(x => x.Score >= 0.85f)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
        
        return bestMatch.Key != null ? Spells[bestMatch.Key] : null;
    }
}
```

---

## Skill Name Mapping

```csharp
public static class SkillVoiceMap
{
    public static readonly Dictionary<string, MacroSubType> Skills = new()
    {
        ["anatomy"] = MacroSubType.Anatomy,
        ["animal lore"] = MacroSubType.AnimalLore,
        ["lore"] = MacroSubType.AnimalLore,
        ["animal taming"] = MacroSubType.AnimalTaming,
        ["taming"] = MacroSubType.AnimalTaming,
        ["tame"] = MacroSubType.AnimalTaming,
        ["arms lore"] = MacroSubType.ArmsLore,
        ["begging"] = MacroSubType.Begging,
        ["beg"] = MacroSubType.Begging,
        ["cartography"] = MacroSubType.Cartography,
        ["detecting hidden"] = MacroSubType.DetectingHidden,
        ["detect hidden"] = MacroSubType.DetectingHidden,
        ["detect"] = MacroSubType.DetectingHidden,
        ["discordance"] = MacroSubType.Discordance,
        ["discord"] = MacroSubType.Discordance,
        ["evaluating intelligence"] = MacroSubType.EvaluatingIntelligence,
        ["eval int"] = MacroSubType.EvaluatingIntelligence,
        ["forensic evaluation"] = MacroSubType.ForensicEvaluation,
        ["forensics"] = MacroSubType.ForensicEvaluation,
        ["hiding"] = MacroSubType.Hiding,
        ["hide"] = MacroSubType.Hiding,
        ["imbuing"] = MacroSubType.Imbuing,
        ["imbue"] = MacroSubType.Imbuing,
        ["inscription"] = MacroSubType.Inscription,
        ["scribe"] = MacroSubType.Inscription,
        ["item identification"] = MacroSubType.ItemIdentification,
        ["item id"] = MacroSubType.ItemIdentification,
        ["meditation"] = MacroSubType.Meditation,
        ["meditate"] = MacroSubType.Meditation,
        ["med"] = MacroSubType.Meditation,
        ["peacemaking"] = MacroSubType.Peacemaking,
        ["peace"] = MacroSubType.Peacemaking,
        ["poisoning"] = MacroSubType.Poisoning,
        ["provocation"] = MacroSubType.Provocation,
        ["provo"] = MacroSubType.Provocation,
        ["remove trap"] = MacroSubType.RemoveTrap,
        ["spirit speak"] = MacroSubType.SpiritSpeak,
        ["spirit"] = MacroSubType.SpiritSpeak,
        ["stealing"] = MacroSubType.Stealing,
        ["steal"] = MacroSubType.Stealing,
        ["stealth"] = MacroSubType.Stealth,
        ["taste identification"] = MacroSubType.TasteIdentification,
        ["taste id"] = MacroSubType.TasteIdentification,
        ["tracking"] = MacroSubType.Tracking,
        ["track"] = MacroSubType.Tracking,
    };
}
```

---

## Gump Name Mapping

```csharp
public static class GumpVoiceMap
{
    public static readonly Dictionary<string, MacroSubType> Gumps = new()
    {
        // Open commands
        ["configuration"] = MacroSubType.Configuration,
        ["config"] = MacroSubType.Configuration,
        ["options"] = MacroSubType.Configuration,
        ["settings"] = MacroSubType.Configuration,
        ["paperdoll"] = MacroSubType.Paperdoll,
        ["paper doll"] = MacroSubType.Paperdoll,
        ["character"] = MacroSubType.Paperdoll,
        ["status"] = MacroSubType.Status,
        ["stats"] = MacroSubType.Status,
        ["journal"] = MacroSubType.Journal,
        ["skills"] = MacroSubType.Skills,
        ["skill list"] = MacroSubType.Skills,
        ["spellbook"] = MacroSubType.MageSpellbook,
        ["spell book"] = MacroSubType.MageSpellbook,
        ["mage book"] = MacroSubType.MageSpellbook,
        ["necro book"] = MacroSubType.NecroSpellbook,
        ["necromancy"] = MacroSubType.NecroSpellbook,
        ["paladin book"] = MacroSubType.PaladinSpellbook,
        ["chivalry"] = MacroSubType.PaladinSpellbook,
        ["bushido book"] = MacroSubType.BushidoSpellbook,
        ["bushido"] = MacroSubType.BushidoSpellbook,
        ["ninjitsu book"] = MacroSubType.NinjitsuSpellbook,
        ["ninjitsu"] = MacroSubType.NinjitsuSpellbook,
        ["spellweaving book"] = MacroSubType.SpellWeavingSpellbook,
        ["spellweaving"] = MacroSubType.SpellWeavingSpellbook,
        ["mysticism book"] = MacroSubType.MysticismSpellbook,
        ["mysticism"] = MacroSubType.MysticismSpellbook,
        ["chat"] = MacroSubType.Chat,
        ["backpack"] = MacroSubType.Backpack,
        ["bag"] = MacroSubType.Backpack,
        ["inventory"] = MacroSubType.Backpack,
        ["overview"] = MacroSubType.Overview,
        ["minimap"] = MacroSubType.Overview,
        ["mini map"] = MacroSubType.Overview,
        ["radar"] = MacroSubType.Overview,
        ["world map"] = MacroSubType.WorldMap,
        ["map"] = MacroSubType.WorldMap,
        ["party"] = MacroSubType.PartyManifest,
        ["party manifest"] = MacroSubType.PartyManifest,
        ["guild"] = MacroSubType.Guild,
        ["quest log"] = MacroSubType.QuestLog,
        ["quests"] = MacroSubType.QuestLog,
    };
}
```

---

## Voice Command Parser

```csharp
public class VoiceCommandParser
{
    private readonly SpellVoiceMap _spellMap;
    private readonly SkillVoiceMap _skillMap;
    private readonly GumpVoiceMap _gumpMap;
    
    // Command patterns (order matters - more specific first)
    private static readonly (Regex Pattern, VoiceCommandType Type)[] Patterns =
    {
        // Cast on target: "cast X on Y", "X on Y", "X Y"
        (new Regex(@"^(?:cast\s+)?(.+?)\s+(?:on\s+)?(?:self|myself|me)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.CastOnSelf),
        (new Regex(@"^(?:cast\s+)?(.+?)\s+(?:on\s+)?(party|enemy|hostile|target|last)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.CastOnTarget),
        
        // Simple cast: "cast X", "X"
        (new Regex(@"^cast\s+(.+)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.CastSpell),
        
        // Use skill: "use X", "X skill"
        (new Regex(@"^use\s+(.+)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.UseSkill),
        (new Regex(@"^(.+?)\s+skill$", RegexOptions.IgnoreCase), 
            VoiceCommandType.UseSkill),
        
        // Target commands
        (new Regex(@"^target\s+(self|myself|me)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.TargetSelf),
        (new Regex(@"^target\s+(last|previous)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.LastTarget),
        (new Regex(@"^(?:target\s+)?(?:next|nearest)\s+(hostile|enemy|party|mobile)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.SelectNearest),
        
        // Open/Close gumps
        (new Regex(@"^open\s+(.+)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.OpenGump),
        (new Regex(@"^close\s+(.+)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.CloseGump),
        
        // Pet commands
        (new Regex(@"^(?:all\s+)?(?:pets?\s+)?(attack|kill|follow|stay|guard|come|heel)(?:\s+(.+))?$", RegexOptions.IgnoreCase), 
            VoiceCommandType.PetCommand),
        
        // Combat
        (new Regex(@"^(attack|kill|fight)(?:\s+(.+))?$", RegexOptions.IgnoreCase), 
            VoiceCommandType.Attack),
        (new Regex(@"^(primary|secondary)\s*(?:ability)?$", RegexOptions.IgnoreCase), 
            VoiceCommandType.UseAbility),
        
        // Items
        (new Regex(@"^(?:use|drink)\s+(.+?\s*potion)$", RegexOptions.IgnoreCase), 
            VoiceCommandType.UsePotion),
        (new Regex(@"^bandage(?:\s+(self|target|me))?$", RegexOptions.IgnoreCase), 
            VoiceCommandType.Bandage),
        
        // Movement
        (new Regex(@"^(war|peace)\s*(?:mode)?$", RegexOptions.IgnoreCase), 
            VoiceCommandType.WarPeace),
        (new Regex(@"^recall(?:\s+(.+))?$", RegexOptions.IgnoreCase), 
            VoiceCommandType.Recall),
    };
    
    public ParsedVoiceCommand? Parse(string transcript)
    {
        var text = transcript.Trim().ToLowerInvariant();
        
        foreach (var (pattern, type) in Patterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                return BuildCommand(type, match);
            }
        }
        
        // Fallback: try direct spell/skill name match
        if (SpellVoiceMap.FindSpell(text) is { } spell)
        {
            return new ParsedVoiceCommand(VoiceCommandType.CastSpell, text, null, spell, new());
        }
        
        if (SkillVoiceMap.FindSkill(text) is { } skill)
        {
            return new ParsedVoiceCommand(VoiceCommandType.UseSkill, text, null, skill, new());
        }
        
        return null;
    }
}
```

---

## Context-Aware Command Filtering

```csharp
public class CommandContextFilter
{
    private readonly World _world;
    
    public CommandContext GetCurrentContext()
    {
        if (_world.Player.IsDead)
            return CommandContext.Dead;
        
        if (_world.TargetManager.IsTargeting)
            return CommandContext.Targeting;
        
        if (_world.Player.InWarMode)
            return CommandContext.Combat;
        
        // Check for open trade windows, NPC dialogs, etc.
        if (UIManager.Gumps.Any(g => g is TradeGump))
            return CommandContext.Trading;
        
        return CommandContext.Peaceful;
    }
    
    public bool IsCommandValidInContext(VoiceCommandType cmd, CommandContext ctx)
    {
        return ctx switch
        {
            CommandContext.Dead => cmd is 
                VoiceCommandType.OpenGump or 
                VoiceCommandType.CloseGump,
            
            CommandContext.Targeting => cmd is 
                VoiceCommandType.TargetSelf or 
                VoiceCommandType.LastTarget or 
                VoiceCommandType.SelectNearest,
            
            CommandContext.Trading => cmd is 
                VoiceCommandType.Confirm or 
                VoiceCommandType.Cancel,
            
            _ => true // Most commands valid in Peaceful/Combat
        };
    }
}
```

---

## Confirmation Flow for Dangerous Actions

```csharp
public class VoiceConfirmationManager
{
    private ParsedVoiceCommand? _pendingCommand;
    private DateTime _pendingExpiry;
    private readonly TimeSpan _confirmationTimeout = TimeSpan.FromSeconds(5);
    
    // Commands requiring confirmation
    private static readonly HashSet<VoiceCommandType> DangerousCommands = new()
    {
        VoiceCommandType.QuitGame,
        VoiceCommandType.DropItem,
        VoiceCommandType.LeaveParty,
        VoiceCommandType.ReleasePet,
    };
    
    public bool RequiresConfirmation(ParsedVoiceCommand cmd)
    {
        return DangerousCommands.Contains(cmd.Type);
    }
    
    public void RequestConfirmation(ParsedVoiceCommand cmd, Action<bool> callback)
    {
        _pendingCommand = cmd;
        _pendingExpiry = DateTime.UtcNow + _confirmationTimeout;
        
        // Show confirmation UI
        GameActions.Print(_world, 
            $"Say 'confirm' or 'cancel' to {GetActionDescription(cmd)}");
    }
    
    public void HandleConfirmation(string response)
    {
        if (_pendingCommand == null || DateTime.UtcNow > _pendingExpiry)
        {
            _pendingCommand = null;
            return;
        }
        
        var normalized = response.ToLowerInvariant().Trim();
        
        if (normalized is "confirm" or "yes" or "do it" or "affirmative")
        {
            ExecuteCommand(_pendingCommand);
        }
        else if (normalized is "cancel" or "no" or "nevermind" or "abort")
        {
            GameActions.Print(_world, "Action cancelled.");
        }
        
        _pendingCommand = null;
    }
}
```

---

## Testing Voice Commands

### Unit Test Examples
```csharp
[TestClass]
public class VoiceCommandParserTests
{
    private VoiceCommandParser _parser;
    
    [TestInitialize]
    public void Setup() => _parser = new VoiceCommandParser();
    
    [TestMethod]
    [DataRow("cast fireball", VoiceCommandType.CastSpell, "fireball")]
    [DataRow("fireball", VoiceCommandType.CastSpell, "fireball")]
    [DataRow("greater heal", VoiceCommandType.CastSpell, "greater heal")]
    [DataRow("gheal", VoiceCommandType.CastSpell, "gheal")]
    [DataRow("cast energy bolt on target", VoiceCommandType.CastOnTarget, "energy bolt")]
    [DataRow("heal party", VoiceCommandType.CastOnTarget, "heal")]
    [DataRow("cure self", VoiceCommandType.CastOnSelf, "cure")]
    public void ParseSpellCommands(string input, VoiceCommandType expectedType, string expectedSpell)
    {
        var result = _parser.Parse(input);
        
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedType, result.Type);
        Assert.AreEqual(expectedSpell, result.SpellName);
    }
    
    [TestMethod]
    [DataRow("target self", VoiceCommandType.TargetSelf)]
    [DataRow("target me", VoiceCommandType.TargetSelf)]
    [DataRow("target last", VoiceCommandType.LastTarget)]
    [DataRow("nearest hostile", VoiceCommandType.SelectNearest)]
    [DataRow("next enemy", VoiceCommandType.SelectNearest)]
    public void ParseTargetCommands(string input, VoiceCommandType expectedType)
    {
        var result = _parser.Parse(input);
        
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedType, result.Type);
    }
}
```

---

## Performance Considerations

### Latency Budget
```
┌─────────────────────────────────────────────────────────┐
│ Total Voice-to-Action Budget: 500ms (combat)            │
├─────────────────────────────────────────────────────────┤
│ Audio capture + VAD:     50ms  │████                    │
│ STT processing:         150ms  │████████████            │
│ Command parsing:         20ms  │██                      │
│ Macro resolution:        10ms  │█                       │
│ Macro execution:         20ms  │██                      │
│ Network + server:       250ms  │████████████████████    │
└─────────────────────────────────────────────────────────┘
```

### Optimization Tips
1. **Pre-compile regex patterns** - Done at class initialization
2. **Cache entity names** - Update on entity enter/exit view
3. **Spell/Skill hash lookups** - O(1) dictionary access
4. **Lazy macro creation** - Only build MacroObject when needed
5. **Command prediction** - Start processing partial transcripts

---

## Next Steps

1. Implement `VoiceCommandParser` class
2. Add voice command mappings to settings
3. Create `VoiceMacroResolver` integration
4. Build voice command HUD feedback
5. Add voice command discovery ("help" command)
