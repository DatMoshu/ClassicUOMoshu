# Voice Command Quick Reference

A quick reference card for voice commands in ClassicUO with speech recognition.

**Default Mode: Basic** - Optimized for combat with single-word shortcuts.

---

## Combat Shortcuts (Basic Mode Only)

These single-word commands work **only in Basic mode** for lowest latency:

| Say | Action | Say | Action |
|-----|--------|-----|--------|
| "fireball" | Cast Fireball | "gheal" | Cast Greater Heal |
| "heal" | Cast Heal | "cure" | Cast Cure |
| "ebolt" | Cast Energy Bolt | "fs" | Flamestrike |
| "bolt" | Lightning | "para" | Paralyze |
| "invis" | Invisibility | "recall" | Recall |
| "hide" | Use Hiding | "stealth" | Use Stealth |
| "med" | Meditation | "track" | Tracking |
| "primary" | Primary Ability | "secondary" | Secondary Ability |
| "attack" | Attack Last | "bandage" | Bandage Self |
| "self" | Target Self | "last" | Last Target |
| "war" | Toggle War Mode | "peace" | Toggle War Mode |

---

## Combat Commands

### Spellcasting
| Say This | Does This |
|----------|-----------|
| "fireball" | Casts Fireball |
| "greater heal" / "gheal" | Casts Greater Heal |
| "cure" | Casts Cure |
| "energy bolt" / "ebolt" | Casts Energy Bolt |
| "flamestrike" / "fs" | Casts Flamestrike |
| "lightning" / "bolt" | Casts Lightning |
| "recall" | Casts Recall |
| "last spell" / "again" | Repeats last spell |

### Targeting
| Say This | Does This |
|----------|-----------|
| "target self" / "self" | Targets yourself |
| "target last" / "last" | Targets last target |
| "nearest enemy" | Selects nearest hostile |
| "next target" | Cycles to next target |
| "nearest party" | Selects nearest ally |

### Combined Spell + Target
| Say This | Does This |
|----------|-----------|
| "heal self" | Greater Heal on self |
| "cure self" | Cure on self |
| "heal party" | Greater Heal on nearest party |
| "fireball enemy" | Fireball on nearest hostile |

### Abilities
| Say This | Does This |
|----------|-----------|
| "primary" | Primary ability |
| "secondary" | Secondary ability |
| "attack" / "kill" | Attack last target |
| "war mode" / "war" | Enter war mode |
| "peace mode" / "peace" | Enter peace mode |

### Emergency
| Say This | Does This |
|----------|-----------|
| "bandage" / "heal me" | Bandage self |
| "[safe word]" | Emergency recall |
| "heal potion" | Uses heal potion |
| "cure potion" | Uses cure potion |

---

## Skills

| Say This | Does This |
|----------|-----------|
| "hide" / "hiding" | Use Hiding |
| "stealth" | Use Stealth |
| "meditate" / "med" | Use Meditation |
| "track" / "tracking" | Use Tracking |
| "detect" / "detect hidden" | Use Detecting Hidden |
| "spirit speak" | Use Spirit Speak |

---

## Pet Commands

| Say This | Does This |
|----------|-----------|
| "all kill" | All pets attack |
| "all follow" | All pets follow |
| "all stay" | All pets stay |
| "all guard" | All pets guard |
| "pet attack" | Selected pet attacks |
| "heel" / "come" | Pet follows you |

---

## UI & Gumps

### Open Windows
| Say This | Does This |
|----------|-----------|
| "open backpack" / "inventory" | Opens backpack |
| "open paperdoll" / "character" | Opens paperdoll |
| "open journal" | Opens journal |
| "open map" / "world map" | Opens world map |
| "open skills" | Opens skill list |
| "open spellbook" | Opens spellbook |
| "open options" / "settings" | Opens options |

### Close Windows
| Say This | Does This |
|----------|-----------|
| "close all" | Closes all gumps |
| "close map" | Closes map |
| "close journal" | Closes journal |
| "close health bars" | Closes health bars |

---

## View Controls

| Say This | Does This |
|----------|-----------|
| "zoom in" | Zooms camera in |
| "zoom out" | Zooms camera out |
| "default zoom" | Resets zoom |
| "all names" | Shows all names |
| "toggle roofs" | Hides/shows roofs |
| "circle trans" | Circle transparency |

---

## Communication

| Say This | Does This |
|----------|-----------|
| "say [text]" | Says text |
| "emote [text]" | Emotes text |
| "whisper [text]" | Whispers text |
| "yell [text]" | Yells text |
| "bow" | Bow emote |
| "salute" | Salute emote |

---

## Tips

### Speak Clearly
- Enunciate spell names
- Pause briefly between commands
- Avoid background noise

### Combat Efficiency
- Use short forms: "gheal", "ebolt", "fs"
- Chain spell + target: "heal self"
- Set up quick casts for combos

### Troubleshooting
- Say "help" for available commands
- Check speech log for recognition issues
- Adjust confidence threshold in settings

---

## Configuration

### Settings File Keys
```json
{
  "enable_speech_recognition": true,
  "stt_engine": "vosk",
  "confidence_threshold": 0.7,
  "voice_command_mode": "basic",
  "recall_safe_word": "hearthstone"
}
```

### Command Modes
- **basic**: Fast hash lookup with single-word shortcuts (recommended for PvP)
- **simple**: Original CommandRouter, requires full phrases
- **advanced**: Full war commands, LLM integration, compound commands

---

## Phonetic Alternatives

Some words sound similar. Use these alternatives:

| If Misheard | Try Saying |
|-------------|------------|
| "Heal" → "heel" | "greater heal", "cure" |
| "Mana" → "manner" | "mana drain" |
| "Cure" → ??? | "remove poison" |
| "Fire" → ??? | "fireball", "flamestrike" |

---

*Generated for ClassicUO Voice Control System*
