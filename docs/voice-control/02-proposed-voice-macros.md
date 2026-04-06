# Proposed Voice-Specific Macros

This document outlines new macro types that would enhance the voice control experience in ClassicUO, designed specifically for speech recognition interaction patterns.

---

## New MacroType Additions

### 1. Voice Navigation & Targeting

#### `VoiceTargetByName`
Target a mobile or object by speaking its name.

```csharp
MacroType.VoiceTargetByName  // "target [name]", "select [name]"
```

**Use Cases:**
- "Target dragon" - Selects nearest dragon
- "Target John" - Selects player named John
- "Select moongate" - Targets nearest moongate

**Implementation Notes:**
- Fuzzy name matching with configurable threshold
- Prioritizes: Party > Followers > Neutrals > Hostiles (configurable)
- Caches nearby entity names for fast lookup

---

#### `VoiceTargetByType`
Target by creature/object type using natural language.

```csharp
MacroType.VoiceTargetByType  // "target [type]"
```

**Sub-Types:**
| SubType | Trigger Phrases | Targets |
|---------|-----------------|---------|
| `Healer` | "healer", "medic" | Healing NPCs, party healers |
| `Vendor` | "vendor", "merchant", "shop" | NPC vendors |
| `Mount` | "mount", "horse", "ride" | Mountable creatures |
| `Container` | "chest", "corpse", "bag" | Containers |
| `Door` | "door", "gate" | Openable objects |
| `Shrine` | "shrine", "ankh" | Resurrection shrines |

---

#### `VoiceFollowTarget`
Voice-controlled following behavior.

```csharp
MacroType.VoiceFollowTarget  // "follow [target]", "stop following"
```

**Sub-Types:**
- `FollowSelected` - "follow", "follow target"
- `FollowByName` - "follow [name]"
- `StopFollowing` - "stop", "halt", "stay"

---

### 2. Combat Voice Commands

#### `VoiceCastOnTarget`
Combined spell + target in single voice command.

```csharp
MacroType.VoiceCastOnTarget  // "fireball dragon", "heal John"
```

**Benefits:**
- Reduces two commands to one
- More natural speech pattern: "[spell] [target]"
- Handles targeting automatically

**Implementation:**
```csharp
// Example processing
"heal party" → CastSpell(GreaterHeal) + SelectNearest(Party) + Target
"lightning enemy" → CastSpell(Lightning) + SelectNearest(Hostile) + Target
"cure self" → CastSpell(Cure) + TargetSelf
```

---

#### `VoiceQuickCast`
Pre-configured spell+target combinations for rapid combat.

```csharp
MacroType.VoiceQuickCast  // Configurable voice triggers
```

**Sub-Types (User Configurable):**
| Slot | Default Phrase | Default Action |
|------|----------------|----------------|
| `QuickCast1` | "nuke" | EnergyBolt + LastTarget |
| `QuickCast2` | "dump" | Flamestrike + CurrentTarget |
| `QuickCast3` | "burst" | Explosion + LastTarget |
| `QuickCast4` | "emergency" | Recall + Self |

---

#### `VoiceAbilityCombo`
Chain abilities with voice commands.

```csharp
MacroType.VoiceAbilityCombo  // "combo", "execute combo"
```

**Sub-Types:**
- `AbilityThenAttack` - "strike" → Primary + Attack
- `DoubleAbility` - "double" → Primary + Secondary
- `DefensiveCombo` - "defend" → Evasion + Confidence

---

### 3. Pet & Follower Commands

#### `VoicePetCommand`
Natural language pet control.

```csharp
MacroType.VoicePetCommand  // Pet control phrases
```

**Sub-Types:**
| SubType | Trigger Phrases | UO Command |
|---------|-----------------|------------|
| `PetAttack` | "sick em", "attack", "kill" | "[pet] kill [target]" |
| `PetFollow` | "heel", "come", "follow me" | "[pet] follow me" |
| `PetStay` | "stay", "wait", "stop" | "[pet] stay" |
| `PetGuard` | "guard", "protect", "guard me" | "[pet] guard me" |
| `PetPatrol` | "patrol", "guard here" | "[pet] patrol" |
| `PetRelease` | "release", "set free" | "[pet] release" |
| `PetAll` | "all [command]" | "all [command]" |

---

#### `VoicePetSelect`
Select specific pet by name or type.

```csharp
MacroType.VoicePetSelect  // "select [pet name/type]"
```

**Examples:**
- "Select dragon" - Selects your dragon
- "Select all" - Selects all pets
- "Select Fluffy" - Selects pet named Fluffy

---

### 4. Communication Macros

#### `VoicePartyCommand`
Party-specific voice commands.

```csharp
MacroType.VoicePartyCommand  // Party management
```

**Sub-Types:**
| SubType | Trigger Phrases | Action |
|---------|-----------------|--------|
| `PartyAdd` | "invite [name]", "add to party" | Party invite |
| `PartyKick` | "kick [name]", "remove [name]" | Remove from party |
| `PartyLoot` | "toggle loot", "can loot" | Toggle loot rights |
| `PartyLeave` | "leave party", "quit party" | Leave party |
| `PartyChat` | "party [message]" | Party chat message |

---

#### `VoiceQuickChat`
Pre-configured chat macros with voice triggers.

```csharp
MacroType.VoiceQuickChat  // Quick chat slots
```

**User Configurable Slots:**
| Slot | Example Phrase | Example Message |
|------|----------------|-----------------|
| `QuickChat1` | "incoming" | "Enemies incoming from the north!" |
| `QuickChat2` | "fall back" | "Fall back to the rally point!" |
| `QuickChat3` | "need heals" | "Need healing ASAP!" |
| `QuickChat4` | "oom" | "Out of mana, covering!" |
| `QuickChat5` | "on my way" | "On my way!" |

---

#### `VoiceEmoteAction`
Combined emote + action for roleplay.

```csharp
MacroType.VoiceEmoteAction  // RP-friendly emotes
```

**Sub-Types:**
| SubType | Trigger | Action |
|---------|---------|--------|
| `SitDown` | "sit", "sit down" | Emote + animation |
| `StandUp` | "stand", "get up" | Emote + animation |
| `Wave` | "wave", "hello" | Wave animation |
| `Point` | "point", "look there" | Point animation |
| `Clap` | "clap", "applaud" | Clap animation |
| `Dance` | "dance" | Dance animation |

---

### 5. Inventory & Banking

#### `VoiceUseItem`
Use items by name with fuzzy matching.

```csharp
MacroType.VoiceUseItem  // "use [item name]"
```

**Examples:**
- "Use bandages" - Uses bandage stack
- "Use recall rune" - Uses recall rune
- "Use red potion" - Uses heal potion
- "Drink refresh" - Uses refresh potion

**Features:**
- Fuzzy item name matching
- Learns from correction ("no, the other one")
- Supports partial names

---

#### `VoiceEquipItem`
Equip items by name or slot.

```csharp
MacroType.VoiceEquipItem  // "equip [item/slot]"
```

**Examples:**
- "Equip katana" - Equips item named katana
- "Wear helmet" - Equips head armor
- "Shield up" - Equips shield
- "Two handed" - Swaps to 2H weapon

---

#### `VoiceBankCommand`
Banking voice shortcuts.

```csharp
MacroType.VoiceBankCommand  // Bank operations
```

**Sub-Types:**
| SubType | Trigger | Action |
|---------|---------|--------|
| `OpenBank` | "bank", "open bank" | Says "bank" near banker |
| `DepositGold` | "deposit gold", "store gold" | Deposits gold |
| `CheckBalance` | "balance", "how much gold" | Says "balance" |

---

### 6. Navigation & Travel

#### `VoiceRecallLocation`
Voice-activated recall with named locations.

```csharp
MacroType.VoiceRecallLocation  // "recall [location]"
```

**Features:**
- Named rune/runebook entries
- Fuzzy location matching
- "Recall home", "Recall bank", "Recall Luna"

---

#### `VoiceGateLocation`
Open gates with voice commands.

```csharp
MacroType.VoiceGateLocation  // "gate [location]"
```

**Examples:**
- "Gate to Britain" - Opens gate to Britain rune
- "Portal home" - Opens gate to home rune
- "Moongate Luna" - Opens gate to Luna

---

#### `VoiceNavigate`
Natural language pathfinding commands.

```csharp
MacroType.VoiceNavigate  // Navigation commands
```

**Sub-Types:**
| SubType | Trigger | Action |
|---------|---------|--------|
| `GoToBank` | "go to bank", "find bank" | Pathfinds to nearest bank |
| `GoToShrine` | "find shrine" | Pathfinds to nearest shrine |
| `GoToPlayer` | "go to [name]" | Follows player |
| `ReturnHome` | "go home" | Recalls or pathfinds to home |
| `StopMoving` | "stop", "halt" | Cancels pathfinding |

---

### 7. System & UI Voice Commands

#### `VoiceToggleSetting`
Voice control for game settings.

```csharp
MacroType.VoiceToggleSetting  // Setting toggles
```

**Sub-Types:**
| SubType | Trigger | Setting |
|---------|---------|---------|
| `ToggleMusic` | "music on/off" | Background music |
| `ToggleSounds` | "sounds on/off" | Sound effects |
| `ToggleFootsteps` | "footsteps on/off" | Footstep sounds |
| `ToggleWeather` | "weather on/off" | Weather effects |
| `ToggleParticles` | "particles on/off" | Particle effects |

---

#### `VoiceScreenshot`
Voice-activated screenshots.

```csharp
MacroType.VoiceScreenshot  // "screenshot", "capture"
```

---

#### `VoiceHelp`
Context-sensitive help.

```csharp
MacroType.VoiceHelp  // "help", "what can I say"
```

**Features:**
- Lists available voice commands
- Context-aware (combat vs. peaceful)
- "Help spells" - Lists spell triggers
- "Help targeting" - Lists targeting commands

---

### 8. Safety & Emergency

#### `VoiceSafeWord`
Emergency actions with high-priority recognition.

```csharp
MacroType.VoiceSafeWord  // Pre-configured emergency trigger
```

**Current Implementation:** Already exists as `RecallSafeWord` in settings.

**Enhancements:**
- Multiple safe words for different emergencies
- "Shit fuck" → Emergency recall (existing)
- "Abort" → Cancel all actions + recall
- "Freeze" → Stop all macros

---

#### `VoiceConfirmCancel`
Confirmation/cancellation commands.

```csharp
MacroType.VoiceConfirmCancel  // Confirmation flow
```

**Sub-Types:**
| SubType | Trigger | Action |
|---------|---------|--------|
| `Confirm` | "yes", "confirm", "do it" | Confirms pending action |
| `Cancel` | "no", "cancel", "nevermind" | Cancels pending action |
| `Undo` | "undo", "take back" | Reverts last action |

---

## MacroSubType Additions

### Voice Target Types
```csharp
// Add to MacroSubType enum
VoiceTargetNearest = 250,
VoiceTargetByName,
VoiceTargetMobile,
VoiceTargetObject,
VoiceTargetPet,
VoiceTargetParty,
VoiceTargetEnemy,
VoiceTargetSelf,
```

### Voice Combat Sub-Types
```csharp
// Combat voice specifics
VoiceOffensive = 260,
VoiceDefensive,
VoiceHealing,
VoiceSupport,
VoiceEmergency,
```

### Voice Communication Sub-Types
```csharp
// Communication specifics
VoiceSay = 270,
VoiceEmote,
VoiceWhisper,
VoiceYell,
VoiceParty,
VoiceGuild,
VoiceAlliance,
```

---

## Integration Architecture

### Voice Command Registration
```csharp
public class VoiceCommandRegistry
{
    // Maps spoken phrases to macro actions
    Dictionary<string, MacroAction> _voiceCommands;
    
    // Fuzzy matching for flexibility
    float _matchThreshold = 0.85f;
    
    // Context-aware filtering
    CommandContext _currentContext;
}
```

### Command Contexts
```csharp
public enum CommandContext
{
    Peaceful,      // Non-combat, full command set
    Combat,        // Combat mode, prioritize combat commands
    Targeting,     // Active target cursor, limit to target commands
    Conversation,  // Talking to NPC, disable most commands
    Trading,       // Trade window open, limit commands
    Dead           // Ghost mode, only resurrection-related
}
```

---

## Implementation Priority

### Phase 1: Combat Essentials
1. `VoiceCastOnTarget` - Unified spell+target
2. `VoicePetCommand` - Pet control
3. `VoiceQuickCast` - Pre-configured combos

### Phase 2: Navigation & Items
1. `VoiceRecallLocation` - Named locations
2. `VoiceUseItem` - Item by name
3. `VoiceEquipItem` - Equipment by name

### Phase 3: Communication
1. `VoiceQuickChat` - Pre-configured messages
2. `VoicePartyCommand` - Party management
3. `VoiceEmoteAction` - RP emotes

### Phase 4: Polish
1. `VoiceHelp` - Command discovery
2. `VoiceToggleSetting` - Settings control
3. `VoiceConfirmCancel` - Confirmation flow

---

## Next Steps

See [03-voice-integration-guide.md](03-voice-integration-guide.md) for implementation details and code examples.
