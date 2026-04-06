# Voice Command Mode Comparison

This document compares the **three voice command modes** available in ClassicUO:

| Mode | Processor | Best For |
|------|-----------|----------|
| **basic** | `BasicVoiceProcessor` | Combat PvP, lowest latency |
| **simple** | `CommandRouter` | General use, original behavior |
| **advanced** | `ActionInferenceEngine` | Complex commands, LLM features |

---

## Quick Comparison

| Feature | Basic | Simple | Advanced |
|---------|:-----:|:------:|:--------:|
| **Latency** | **< 10ms** | < 50ms | 200-2000ms |
| **AI Required** | ❌ | ❌ | ✅ |
| Single-word shortcuts | ✅ | ❌ | ✅ |
| Spell abbreviations | ✅ | ❌ | ✅ |
| Pet commands | ✅ | ✅ | ✅ |
| Gump commands | ✅ | ✅ | ✅ |
| Compound commands | ❌ | ❌ | ✅ |
| Context awareness | ❌ | ❌ | ✅ |
| UOWW war commands | ❌ | ✅ | ✅ |
| LLM avatar chat | ❌ | ✅ | ✅ |

---

## Basic Mode (NEW - Recommended for Combat)

### Architecture
```
Speech → STT → BasicVoiceProcessor.TryProcess()
                    │
                    ├─→ 1. Shortcuts (O(1) hash)     "fireball" → Cast Fireball
                    ├─→ 2. Exact phrases (O(1) hash) "cast fireball" → Cast Fireball
                    └─→ 3. Contains patterns (O(n))  "all kill" → Pet Attack
```

### Key Features
- **O(1) hash lookup** for shortcuts and exact phrases
- **Single-word combat shortcuts** for lowest latency
- **Built-in deduplication** (1 second window)
- **Fully isolated** - no dependencies on other systems

### Shortcuts (Single Words)
| Say | Action |
|-----|--------|
| fireball | Cast Fireball |
| gheal | Cast Greater Heal |
| ebolt | Cast Energy Bolt |
| fs | Cast Flamestrike |
| heal | Cast Heal |
| cure | Cast Cure |
| para | Cast Paralyze |
| invis | Cast Invisibility |
| hide | Use Hiding |
| stealth | Use Stealth |
| med | Use Meditation |
| primary | Primary Ability |
| secondary | Secondary Ability |
| attack | Attack Last |
| bandage | Bandage Self |
| self | Target Self |
| last | Last Target |

### Statistics
- **~40 shortcuts** (single-word combat commands)
- **~500 exact phrases** (full spell/skill/gump commands)
- **~15 contains patterns** (pet commands)

---

## Simple Mode (Original)

### Architecture  
```
Speech → STT → CommandRouter.RouteFullResult()
                    │
                    ├─→ 1. RouteListeningToggle()
                    ├─→ 2. AvatarLlm
                    ├─→ 3. SpeechCommandsManager
                    ├─→ 4. RouteMacroCommand()
                    ├─→ 5. RoutePetCommand()
                    ├─→ 6. RouteUowwIntent()
                    └─→ 7. RouteQuestion()
```

### Key Features
- **Original behavior** from SpeechMacroStrings
- **UOWW commands** (war map, faction info, dispatching)
- **Avatar LLM integration** ("hey avatar...")
- **Player questions** (jokes, greetings)

### Limitations
- No single-word shortcuts (requires "cast fireball" not "fireball")
- No spell abbreviations (requires full spell name)
- Higher latency due to iteration

---

## Advanced Mode

### Architecture
```
Speech → STT → ActionInferenceEngine
                    │
                    ├─→ TokenScorer (fast local scoring)
                    └─→ LlmScorer (Ollama inference)
                            │
                            └─→ ActionHudGump (confirmation UI)
```

### Key Features
- **LLM inference** for complex commands
- **Compound commands** ("heal self", "fireball enemy")
- **Context awareness** (combat vs peaceful state)
- **HUD overlay** for confirmation/selection
- **Learning** from corrections

### Limitations
- Requires Ollama/LLM backend
- 200-2000ms latency
- Higher resource usage

---

## Configuration

### settings.json
```json
{
  "voice_command_mode": "basic",    // "basic", "simple", or "advanced"
  "inference_mode_enabled": false,  // true to use Advanced mode
  "speech_recognition_enabled": true
}
```

### Mode Selection Logic
1. If `inference_mode_enabled` = true → **Advanced mode**
2. Else if `voice_command_mode` = "basic" → **Basic mode**
3. Else → **Simple mode**

---

## Command Coverage by Mode

### Spells

| Command | Basic | Simple | Advanced |
|---------|:-----:|:------:|:--------:|
| "fireball" | ✅ | ❌ | ✅ |
| "cast fireball" | ✅ | ✅ | ✅ |
| "gheal" | ✅ | ❌ | ✅ |
| "cast greater heal" | ✅ | ✅ | ✅ |
| "heal self" | ❌ | ❌ | ✅ |
| "fireball enemy" | ❌ | ❌ | ✅ |

### Targeting

| Command | Basic | Simple | Advanced |
|---------|:-----:|:------:|:--------:|
| "self" | ✅ | ❌ | ✅ |
| "target self" | ✅ | ✅ | ✅ |
| "last" | ✅ | ❌ | ✅ |
| "nearest enemy" | ✅ | ❌ | ✅ |
| "nearest hostile" | ✅ | ✅ | ✅ |

### Skills

| Command | Basic | Simple | Advanced |
|---------|:-----:|:------:|:--------:|
| "hide" | ✅ | ❌ | ✅ |
| "use hiding" | ✅ | ✅ | ✅ |
| "med" | ✅ | ❌ | ✅ |
| "use meditation" | ✅ | ✅ | ✅ |

### Abilities

| Command | Basic | Simple | Advanced |
|---------|:-----:|:------:|:--------:|
| "primary" | ✅ | ❌ | ✅ |
| "primary ability" | ✅ | ✅ | ✅ |
| "secondary" | ✅ | ❌ | ✅ |

### Gumps

| Command | Basic | Simple | Advanced |
|---------|:-----:|:------:|:--------:|
| "backpack" | ✅ | ✅ | ✅ |
| "open backpack" | ✅ | ✅ | ✅ |
| "inventory" | ✅ | ✅ | ✅ |

### Pet Commands

| Command | Basic | Simple | Advanced |
|---------|:-----:|:------:|:--------:|
| "all kill" | ✅ | ✅ | ✅ |
| "attack" | ✅ | ✅ | ✅ |
| "heel" | ✅ | ✅ | ✅ |
| "sick em" | ✅ | ✅ | ✅ |

---

## Recommendations

### For PvP Combat
Use **Basic mode** for:
- Lowest latency (< 10ms)
- Single-word spell shortcuts
- No AI dependencies
- Works offline

### For General Use
Use **Simple mode** for:
- Original behavior
- UOWW features
- Avatar chat
- Player questions

### For Complex Commands
Use **Advanced mode** for:
- Compound commands
- Context-aware targeting
- LLM-powered features
- Learning from corrections

---

## Migration from Simple to Basic

If you're currently using Simple mode and want Basic mode benefits:

1. Change `voice_command_mode` from `"simple"` to `"basic"`
2. Learn single-word shortcuts for combat
3. Full phrase commands still work

No other changes needed - Basic mode is a superset of Simple mode's core features.
