# Macro Voice Control Feasibility Analysis

This document analyzes the existing macro system in ClassicUO from the perspective of speech recognition control, categorizing each macro type by its suitability for voice activation.

## Feasibility Categories

| Category | Description | Voice Suitability |
|----------|-------------|-------------------|
| 🟢 **Excellent** | One-shot actions, no parameters, clear verbal trigger | Perfect for voice |
| 🟡 **Good** | Requires simple parameter or confirmation | Works with voice |
| 🟠 **Moderate** | Requires complex parameters or context | Needs careful design |
| 🔴 **Poor** | Real-time/continuous input, timing-critical | Not suitable for voice |

---

## Existing Macro Analysis

### Combat & Targeting

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `WarPeace` | Toggle | 🟢 Excellent | "war mode", "peace mode", "toggle war" | Clear toggle, instant |
| `AttackLast` | Action | 🟢 Excellent | "attack", "kill", "fight" | No parameters needed |
| `AttackSelectedTarget` | Action | 🟢 Excellent | "attack target", "kill target" | Uses pre-selected target |
| `LastTarget` | Targeting | 🟢 Excellent | "last target", "target last" | Requires active targeting cursor |
| `TargetSelf` | Targeting | 🟢 Excellent | "target self", "target me" | Simple self-target |
| `TargetNext` | Selection | 🟢 Excellent | "next target", "cycle target" | Cycles through valid targets |
| `CurrentTarget` | Targeting | 🟢 Excellent | "current target", "this target" | Uses selected target |
| `SelectNext` | Selection | 🟡 Good | "next hostile", "next party member" | Requires sub-type |
| `SelectPrevious` | Selection | 🟡 Good | "previous hostile", "previous ally" | Requires sub-type |
| `SelectNearest` | Selection | 🟡 Good | "nearest enemy", "closest hostile" | Requires sub-type |
| `WaitForTarget` | Timing | 🔴 Poor | N/A | Internal macro timing only |

### Spellcasting

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `CastSpell` | Action | 🟢 Excellent | "cast fireball", "flamestrike" | 151 spells, excellent for voice |
| `LastSpell` | Action | 🟢 Excellent | "last spell", "repeat spell" | No parameters |
| `UseSkill` | Action | 🟢 Excellent | "use hiding", "stealth", "meditation" | 24 skills, natural phrases |
| `LastSkill` | Action | 🟢 Excellent | "last skill", "repeat skill" | No parameters |

### Movement

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `Walk` | Movement | 🔴 Poor | N/A | Continuous, better with keyboard/mouse |
| `AlwaysRun` | Toggle | 🟢 Excellent | "always run", "toggle run" | Simple toggle |
| `OpenDoor` | Action | 🟢 Excellent | "open door", "door" | Context-aware action |

### Inventory & Equipment

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `UseItemInHand` | Action | 🟢 Excellent | "use item", "use weapon" | Uses equipped item |
| `LastObject` | Action | 🟢 Excellent | "last object", "use last" | Repeats last interaction |
| `ArmDisarm` | Action | 🟡 Good | "arm left", "disarm right" | Requires hand specification |
| `EquipLastWeapon` | Action | 🟢 Excellent | "equip weapon", "last weapon" | No parameters |
| `UsePotion` | Action | 🟡 Good | "heal potion", "cure potion" | Requires potion type |
| `UseObject` | Action | 🟡 Good | "enchanted apple", "smoke bomb" | Requires object type |
| `BandageSelf` | Action | 🟢 Excellent | "bandage", "heal me", "bandage self" | Priority voice command |
| `BandageTarget` | Action | 🟢 Excellent | "bandage target", "heal target" | Uses selected target |
| `Grab` | Targeting | 🟡 Good | "grab item", "pick up" | Requires target selection |
| `SetGrabBag` | Targeting | 🟠 Moderate | "set grab bag" | Setup command |

### UI & Gumps

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `Open` | UI | 🟢 Excellent | "open paperdoll", "open backpack" | Clear sub-types |
| `Close` | UI | 🟢 Excellent | "close journal", "close map" | Clear sub-types |
| `Minimize` | UI | 🟡 Good | "minimize status" | Less common voice use |
| `Maximize` | UI | 🟡 Good | "maximize paperdoll" | Less common voice use |
| `CloseGump` | UI | 🟢 Excellent | "close all", "close windows" | Closes all gumps |
| `ToggleBuffIconGump` | Toggle | 🟢 Excellent | "toggle buffs", "show buffs" | Simple toggle |
| `CloseAllHealthBars` | UI | 🟢 Excellent | "close health bars" | Cleanup command |
| `CloseInactiveHealthBars` | UI | 🟢 Excellent | "close inactive bars" | Cleanup command |
| `CloseCorpses` | UI | 🟢 Excellent | "close corpses", "close loot" | Cleanup command |

### Communication

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `Say` | Text | 🟢 Excellent | Transcribed speech | Natural voice-to-text |
| `Emote` | Text | 🟡 Good | "emote [text]" | Prefix-triggered |
| `Whisper` | Text | 🟡 Good | "whisper [text]" | Prefix-triggered |
| `Yell` | Text | 🟡 Good | "yell [text]" | Prefix-triggered |
| `Bow` | Emote | 🟢 Excellent | "bow" | Single word |
| `Salute` | Emote | 🟢 Excellent | "salute" | Single word |

### Abilities & Virtues

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `PrimaryAbility` | Action | 🟢 Excellent | "primary", "primary ability" | Combat essential |
| `SecondaryAbility` | Action | 🟢 Excellent | "secondary", "secondary ability" | Combat essential |
| `InvokeVirtue` | Action | 🟡 Good | "honor", "sacrifice", "valor" | Virtue names |
| `ToggleGargoyleFly` | Toggle | 🟢 Excellent | "fly", "land", "toggle fly" | Race-specific |

### View & Display

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `Zoom` | Action | 🟡 Good | "zoom in", "zoom out", "default zoom" | Clear sub-types |
| `CircleTrans` | Toggle | 🟢 Excellent | "circle trans", "transparency" | Toggle |
| `AllNames` | Action | 🟢 Excellent | "all names", "show names" | Display toggle |
| `NamesOnOff` | Toggle | 🟢 Excellent | "names on", "names off" | Persistent toggle |
| `AuraOnOff` | Toggle | 🟢 Excellent | "aura on", "aura off" | Visibility toggle |
| `ToggleDrawRoofs` | Toggle | 🟢 Excellent | "toggle roofs", "hide roofs" | Visual toggle |
| `ToggleTreeStumps` | Toggle | 🟢 Excellent | "toggle trees", "tree stumps" | Visual toggle |
| `ToggleVegetation` | Toggle | 🟢 Excellent | "hide vegetation", "show plants" | Visual toggle |
| `ToggleCaveTiles` | Toggle | 🟢 Excellent | "cave tiles", "toggle caves" | Visual toggle |
| `EnableRangeColor` | Toggle | 🟡 Good | "enable range color" | Less common |
| `DisableRangeColor` | Toggle | 🟡 Good | "disable range color" | Less common |
| `ToggleRangeColor` | Toggle | 🟡 Good | "toggle range color" | Less common |

### System & Settings

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `SaveDesktop` | Action | 🟢 Excellent | "save desktop", "save layout" | Infrequent use |
| `QuitGame` | Action | 🟠 Moderate | "quit game" | Should require confirmation |
| `SetUpdateRange` | Setting | 🟠 Moderate | "set range [number]" | Requires number |
| `IncreaseUpdateRange` | Setting | 🟢 Excellent | "increase range" | Simple increment |
| `DecreaseUpdateRange` | Setting | 🟢 Excellent | "decrease range" | Simple decrement |
| `MaxUpdateRange` | Setting | 🟢 Excellent | "max range" | Preset value |
| `MinUpdateRange` | Setting | 🟢 Excellent | "min range" | Preset value |
| `DefaultUpdateRange` | Setting | 🟢 Excellent | "default range" | Preset value |
| `ToggleChatVisibility` | Toggle | 🟢 Excellent | "toggle chat", "hide chat" | UI toggle |
| `TargetSystemOnOff` | Toggle | 🟢 Excellent | "target system on/off" | System toggle |

### Timing & Scripting

| Macro | Type | Voice Feasibility | Recommended Phrase | Notes |
|-------|------|-------------------|-------------------|-------|
| `Delay` | Timing | 🔴 Poor | N/A | Internal macro timing |
| `Paste` | Text | 🔴 Poor | N/A | Clipboard operation |
| `RazorMacro` | External | 🟠 Moderate | "razor [macro name]" | External tool integration |
| `Aura` | Hold | 🔴 Poor | N/A | Hold-to-activate |
| `LookAtMouse` | Continuous | 🔴 Poor | N/A | Mouse-dependent |
| `UseCounterBarSlot` | Action | 🟡 Good | "slot one", "counter one" | Requires slot number |

---

## Voice Control Priority Tiers

### Tier 1: Combat Critical (Lowest Latency Required)
These macros are essential during combat and should have the shortest, most distinct trigger phrases:

1. **BandageSelf** - "bandage", "heal"
2. **CastSpell** - Direct spell names
3. **PrimaryAbility** - "primary"
4. **SecondaryAbility** - "secondary"
5. **AttackLast** - "attack"
6. **LastTarget** - "target"
7. **TargetSelf** - "self"
8. **UsePotion** (heal/cure) - "potion"

### Tier 2: Tactical Actions
Actions used during combat but not time-critical:

1. **SelectNearest** (Hostile) - "nearest enemy"
2. **EquipLastWeapon** - "weapon"
3. **WarPeace** - "war", "peace"
4. **UseSkill** - Skill names
5. **LastSpell** - "again", "repeat"

### Tier 3: Utility Commands
Non-combat actions:

1. **Open/Close** - "open [gump]", "close [gump]"
2. **Say/Emote/Whisper/Yell** - Natural speech
3. **Zoom** - "zoom in/out"
4. **AllNames** - "names"
5. **SaveDesktop** - "save"

### Tier 4: Setup & Configuration
Rarely used, can have longer phrases:

1. **SetGrabBag** - "set grab bag"
2. **Update Range** commands
3. **Toggle** visual settings

---

## Speech Recognition Considerations

### Disambiguation Challenges

| Conflict | Resolution Strategy |
|----------|---------------------|
| "heal" vs "heel" | Context: combat = heal spell, otherwise = command |
| "fire" vs "fireball" vs "fire field" | Prefix matching with confirmation |
| "greater heal" vs "heal" | Exact match takes priority |
| "target" (noun) vs "target" (verb) | Combine with action: "target self" |

### Homophone Handling

| Word | Homophones | Solution |
|------|------------|----------|
| Heal | Heel, He'll | High confidence threshold |
| Cure | Q-er | Unique in context |
| Mana | Manner | Unique in context |
| Recall | Re-call | Compound word bias |

### Latency Requirements

| Tier | Max Acceptable Latency | Use Case |
|------|----------------------|----------|
| Combat | < 200ms | Spell casting, abilities |
| Tactical | < 500ms | Target selection, equipment |
| Utility | < 1000ms | UI operations, settings |
| Setup | < 2000ms | Configuration, rarely used |

---

## Next Steps

See [02-proposed-voice-macros.md](02-proposed-voice-macros.md) for new macro types specifically designed for voice control.
