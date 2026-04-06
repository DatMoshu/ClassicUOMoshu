# Simple Mode vs Suggested Commands Comparison

This document compares the **currently implemented** voice commands in Simple Mode (non-AI, fast string matching) against the **suggested commands** in the quick reference documentation.

---

## Architecture Comparison

| Aspect | Simple Mode (Current) | Advanced Mode (Future) |
|--------|----------------------|------------------------|
| **Matching** | Exact string match via `Dictionary<>` | LLM inference, fuzzy matching |
| **Latency** | < 50ms (dictionary lookup) | 200-2000ms (model inference) |
| **AI Required** | ❌ No | ✅ Yes (Ollama/LLM) |
| **Context Aware** | ❌ No | ✅ Yes |
| **Compound Commands** | ❌ No | ✅ Yes ("heal self", "fireball enemy") |
| **Learning** | ❌ Static | ✅ Adaptive |

---

## Command Pipeline (Simple Mode)

```
Speech → STT → CommandRouter.RouteFullResult()
                    │
                    ├─→ 1. RouteListeningToggle()    [exact match]
                    ├─→ 2. AvatarLlm (skipped in simple mode)
                    ├─→ 3. SpeechCommandsManager     [prefix match]
                    ├─→ 4. RouteMacroCommand()       [exact match via SpeechMacroStrings]
                    ├─→ 5. RoutePetCommand()         [contains match]
                    ├─→ 6. RouteUowwIntent()         [NLP - disabled in simple mode]
                    └─→ 7. RouteQuestion()           [contains match]
```

---

## Spellcasting Commands

### ✅ Currently Implemented (SpeechMacroStrings)

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "cast fireball" | ✅ | `"cast fireball"`, `"fireball spell"` |
| "cast greater heal" | ✅ | `"cast greater heal"`, `"greater heal spell"` |
| "cast cure" | ✅ | `"cast cure"`, `"cure spell"` |
| "cast energy bolt" | ✅ | `"cast energy bolt"`, `"energy bolt spell"` |
| "cast flame strike" | ✅ | `"cast flame strike"`, `"flame strike spell"` |
| "cast lightning" | ✅ | `"cast lightning"`, `"lightning spell"` |
| "cast recall" | ✅ | `"cast recall"`, `"recall spell"` |
| "last spell" | ✅ | `"last spell"`, `"cast last spell"` |

### ❌ NOT Implemented (Suggested Shortcuts)

| Say This | Status | Notes |
|----------|:------:|-------|
| "fireball" (no "cast") | ❌ | Requires prefix "cast" |
| "gheal" | ❌ | Shorthand not mapped |
| "ebolt" | ❌ | Shorthand not mapped |
| "fs" | ❌ | Shorthand not mapped |
| "bolt" | ❌ | Shorthand not mapped |
| "again" | ❌ | Not mapped to last spell |

### Gap Analysis: Spellcasting
- **143 spells implemented** with `"cast [spell]"` and `"[spell] spell"` patterns
- **Missing**: Single-word shortcuts, abbreviations
- **Recommendation**: Add shorthand aliases to existing spell mappings

---

## Targeting Commands

### ✅ Currently Implemented

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "target self" | ✅ | `"target self"`, `"self target"` |
| "target last" | ✅ | `"last target"`, `"target last"` |
| "next target" | ✅ | `"target next"`, `"next target"` |
| "nearest hostile" | ✅ | `"select nearest hostile"`, `"nearest hostile"` |
| "nearest party" | ✅ | `"select nearest party"`, `"nearest party"` |
| "next hostile" | ✅ | `"select next hostile"`, `"next hostile"` |
| "next follower" | ✅ | `"select next follower"`, `"next follower"` |

### ❌ NOT Implemented (Suggested)

| Say This | Status | Notes |
|----------|:------:|-------|
| "self" (single word) | ❌ | Requires "target self" |
| "last" (single word) | ❌ | Requires "target last" or "last target" |
| "nearest enemy" | ❌ | Uses "hostile" not "enemy" |

### Gap Analysis: Targeting
- **Core targeting implemented** with 2-3 word phrases
- **Missing**: Single-word shortcuts, "enemy" alias for "hostile"
- **Recommendation**: Add word aliases

---

## Combined Spell + Target Commands

### ❌ NOT Implemented (Advanced Mode Only)

| Say This | Status | Notes |
|----------|:------:|-------|
| "heal self" | ❌ | Requires LLM inference |
| "cure self" | ❌ | Requires LLM inference |
| "heal party" | ❌ | Requires LLM inference |
| "fireball enemy" | ❌ | Requires LLM inference |

**These are ADVANCED MODE features** - they require contextual understanding to:
1. Parse spell name
2. Parse target type
3. Execute spell
4. Wait for target cursor
5. Apply target

---

## Combat Abilities

### ✅ Currently Implemented

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "primary ability" | ✅ | `"primary ability"`, `"use primary ability"` |
| "secondary ability" | ✅ | `"secondary ability"`, `"use secondary ability"` |
| "attack last" | ✅ | `"attack last"`, `"last attack"` |
| "attack target" | ✅ | `"attack selected target"`, `"attack target"` |
| "war mode" | ✅ | `"toggle war mode"`, `"war mode"`, `"peace mode"` |

### ❌ NOT Implemented (Suggested)

| Say This | Status | Notes |
|----------|:------:|-------|
| "primary" (single word) | ❌ | Requires "primary ability" |
| "secondary" (single word) | ❌ | Requires "secondary ability" |
| "attack" (single word) | ❌ | Requires "attack last" or "attack target" |
| "kill" | ❌ | Not mapped |
| "war" | ❌ | Requires "war mode" or "toggle war mode" |
| "peace" | ❌ | Requires "peace mode" |

---

## Emergency Commands

### ✅ Currently Implemented

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "bandage self" | ✅ | `"bandage self"`, `"self bandage"` |
| "bandage target" | ✅ | `"bandage target"`, `"target bandage"` |
| "cure potion" | ✅ | `"use cure potion"`, `"cure potion"` |
| "heal potion" | ✅ | `"use heal potion"`, `"heal potion"` |
| Safe word recall | ✅ | Configured via `recall_safe_word` setting |

### ❌ NOT Implemented (Suggested)

| Say This | Status | Notes |
|----------|:------:|-------|
| "bandage" (single word) | ❌ | Requires "bandage self" |
| "heal me" | ❌ | Not mapped |

---

## Skills

### ✅ Currently Implemented

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "use hiding" | ✅ | `"use hiding"`, `"hiding skill"` |
| "use stealth" | ✅ | `"use stealth"`, `"stealth skill"` |
| "use meditation" | ✅ | `"use meditation"`, `"meditation skill"` |
| "use tracking" | ✅ | `"use tracking"`, `"tracking skill"` |
| "use detecting hidden" | ✅ | `"use detecting hidden"`, `"detecting hidden skill"` |
| "use spirit speak" | ✅ | `"use spirit speak"`, `"spirit speak skill"` |
| "last skill" | ✅ | `"last skill"`, `"use last skill"` |

### ❌ NOT Implemented (Suggested Shortcuts)

| Say This | Status | Notes |
|----------|:------:|-------|
| "hide" (single word) | ❌ | Requires "use hiding" |
| "stealth" (single word) | ❌ | Requires "use stealth" |
| "meditate" / "med" | ❌ | Requires "use meditation" |
| "track" | ❌ | Requires "use tracking" |
| "detect" | ❌ | Requires "use detecting hidden" |

---

## Pet Commands

### ✅ Currently Implemented (SpeechRecognitionStrings.PetCommands)

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "all kill" | ✅ | `"all kill"`, `"kill"`, `"all attack"`, `"attack"`, `"all engage"`, `"engage"`, `"kill them all"` |
| "all follow" | ✅ | `"all follow me"`, `"follow me"`, `"all come with me"`, `"come with me"`, `"all follow"`, `"follow"` |
| "all stay" | ✅ | `"all stay"`, `"stay"`, `"all hold position"`, `"hold position"`, `"all wait here"`, `"wait here"` |
| "all guard" | ✅ | `"all guard me"`, `"guard me"`, `"all protect me"`, `"protect me"`, `"all defend me"`, `"defend me"` |
| "all stop" | ✅ | `"all stop"`, `"stop"`, `"all halt"`, `"halt"`, `"all cease"`, `"cease"` |
| "all come" | ✅ | `"all come"`, `"come"`, `"all here"`, `"here"`, `"all return"`, `"return"`, `"all to me"` |
| "heel" / "come" | ✅ | Included in "all come" |
| "all release" | ✅ | `"all release"`, `"release"`, `"all free"`, `"free"`, `"all dismiss"`, `"dismiss"` |
| "kill closest" | ✅ | `"all kill closest"`, `"kill closest"`, `"all attack nearest"`, `"attack nearest"` |

### ❌ NOT Implemented (Suggested)

| Say This | Status | Notes |
|----------|:------:|-------|
| "pet attack" | ❌ | Uses "all attack" pattern |
| "sick em" | ❌ | Not mapped |

**Pet commands are well-implemented** with multiple phrase variations.

---

## UI & Gumps

### ✅ Currently Implemented (SpeechMacroStrings - Open/Close/Minimize/Maximize)

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "open backpack" | ✅ | `"backpack"`, `"open backpack"`, `"show backpack"`, `"inventory"`, `"open inventory"`, `"show inventory"` |
| "open paperdoll" | ✅ | `"paperdoll"`, `"open paperdoll"`, `"show paperdoll"`, `"character"`, `"open character"`, `"show character"`, `"paper doll"`, `"open paper doll"`, `"char screen"` |
| "open journal" | ✅ | `"journal"`, `"open journal"`, `"show journal"`, `"chat log"`, `"show chat log"` |
| "open map" | ✅ | `"world map"`, `"open world map"`, `"show world map"`, `"map"`, `"open map"`, `"show map"`, `"mini map"`, `"minimap"`, `"open minimap"` |
| "open skills" | ✅ | `"skills"`, `"open skills"`, `"show skills"` |
| "open spellbook" | ✅ | `"spellbook"`, `"open spellbook"`, `"mage spellbook"` |
| "open options" | ✅ | `"settings"`, `"open settings"`, `"configuration"`, `"open configuration"`, `"options"`, `"open options"`, `"show options"`, `"game options"`, `"game settings"` |
| "close [gump]" | ✅ | All gumps have close variants |
| "close all" | ✅ | `"kill gump open"`, `"close all gumps"` |

**UI commands are well-implemented** with extensive phrase variations.

---

## View Controls

### ✅ Currently Implemented

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "zoom in" | ✅ | `"zoom in"`, `"increase zoom"` |
| "zoom out" | ✅ | `"zoom out"`, `"decrease zoom"` |
| "default zoom" | ✅ | `"default zoom"`, `"reset zoom"` |
| "all names" | ✅ | `"all names"`, `"show all names"` |
| "toggle roofs" | ✅ | `"toggle draw roofs"`, `"draw roofs"` |
| "circle trans" | ✅ | `"circle transparency"`, `"toggle circle transparency"` |

---

## Communication

### ✅ Currently Implemented

| Say This | Implemented? | Phrases Supported |
|----------|:------------:|-------------------|
| "say" | ✅ | `"say"`, `"speak"`, `"utter"` |
| "emote" | ✅ | `"emote"`, `"express"`, `"show emotion"` |
| "whisper" | ✅ | `"whisper"`, `"speak softly"`, `"murmur"` |
| "yell" | ✅ | `"yell"`, `"shout"`, `"scream"` |
| "bow" | ✅ | `"bow"`, `"perform bow"` |
| "salute" | ✅ | `"salute"`, `"perform salute"` |

---

## Summary: Simple Mode Coverage

| Category | Implemented | Suggested | Coverage |
|----------|:-----------:|:---------:|:--------:|
| **Spellcasting** | 143 spells | 143 + shortcuts | 100% base, 0% shortcuts |
| **Targeting** | 15 commands | 15 + shortcuts | 100% base, 0% shortcuts |
| **Combined Spell+Target** | 0 | 4+ | 0% (Advanced only) |
| **Combat Abilities** | 5 commands | 5 + shortcuts | 100% base, 0% shortcuts |
| **Emergency** | 5 commands | 5 + shortcuts | 100% base, 60% shortcuts |
| **Skills** | 24 skills | 24 + shortcuts | 100% base, 0% shortcuts |
| **Pet Commands** | 12 commands | 12 | **100%** ✅ |
| **UI/Gumps** | 56 commands | 14 | **100%** ✅ |
| **View Controls** | 10 commands | 6 | **100%** ✅ |
| **Communication** | 6 commands | 6 | **100%** ✅ |

---

## Recommendations for Simple Mode Enhancement

### High Priority (Combat Critical)
Add single-word aliases to existing command mappings:

```csharp
// In SpeechMacroStrings.cs - Add shortcuts
{ MacroSubType.GreaterHeal, new[] { "cast greater heal", "greater heal spell", "gheal", "g heal" } },
{ MacroSubType.EnergyBolt, new[] { "cast energy bolt", "energy bolt spell", "ebolt", "e bolt" } },
{ MacroSubType.FlameStrike, new[] { "cast flame strike", "flame strike spell", "fs", "flamestrike" } },
{ MacroSubType.Lightning, new[] { "cast lightning", "lightning spell", "bolt" } },
{ MacroSubType.Heal, new[] { "cast heal", "heal spell", "heal" } },  // single word
```

### Medium Priority (Convenience)
```csharp
// Skills - add single word triggers
{ MacroSubType.Hiding, new[] { "use hiding", "hiding skill", "hide" } },
{ MacroSubType.Stealth, new[] { "use stealth", "stealth skill", "stealth" } },
{ MacroSubType.Meditation, new[] { "use meditation", "meditation skill", "meditate", "med" } },

// Abilities - add single word triggers  
{ MacroSubType.MSC_NONE, new[] { "primary ability", "use primary ability", "primary" } },
{ MacroSubType.MSC_NONE, new[] { "secondary ability", "use secondary ability", "secondary" } },

// Targeting - add enemy alias
{ MacroSubType.Hostile, new[] { "select nearest hostile", "nearest hostile", "nearest enemy" } },
```

### Low Priority (Future)
- Combined spell+target → **Advanced Mode only**
- Context-aware shortcuts → **Advanced Mode only**
- Fuzzy matching → **Advanced Mode only**

---

## Mode Selection Logic

```csharp
// Settings.cs
[JsonPropertyName("voice_command_mode")] 
public string VoiceCommandMode { get; set; } = "simple";

// simple = Fast dictionary matching only
// advanced = LLM inference + fuzzy matching + compound commands
```

### Simple Mode
- ✅ Dictionary-based exact matching
- ✅ Pet commands with contains matching
- ✅ Zero AI dependencies
- ✅ < 50ms command resolution
- ❌ No compound commands
- ❌ No context awareness
- ❌ No fuzzy matching

### Advanced Mode  
- ✅ LLM inference (ActionInferenceEngine)
- ✅ Compound commands ("heal self")
- ✅ Context-aware targeting
- ✅ Fuzzy phrase matching
- ✅ Learning from corrections
- ❌ 200-2000ms latency
- ❌ Requires Ollama/LLM backend
