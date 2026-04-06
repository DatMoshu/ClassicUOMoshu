# Voice Control Documentation

This directory contains documentation for the ClassicUO speech recognition and voice control system.

## Documents

| Document | Description |
|----------|-------------|
| [01-macro-voice-feasibility.md](01-macro-voice-feasibility.md) | Analysis of existing macros for voice control suitability |
| [02-proposed-voice-macros.md](02-proposed-voice-macros.md) | New macro types designed for voice interaction |
| [03-voice-integration-guide.md](03-voice-integration-guide.md) | Technical implementation guide |
| [04-quick-reference.md](04-quick-reference.md) | User-facing quick reference card |
| [05-simple-vs-suggested-comparison.md](05-simple-vs-suggested-comparison.md) | Comparison of Simple Mode vs suggested commands |
| [06-mode-comparison.md](06-mode-comparison.md) | **NEW: Comparison of Basic/Simple/Advanced modes** |

## Summary

### Simple Mode vs Advanced Mode

| Aspect | Simple Mode | Advanced Mode |
|--------|-------------|---------------|
| **Matching** | Exact string match | LLM inference |
| **Latency** | < 50ms | 200-2000ms |
| **AI Required** | ❌ No | ✅ Yes |
| **Compound Commands** | ❌ No | ✅ Yes |

**Simple Mode Coverage:**
- ✅ 143 spells (require "cast [spell]" prefix)
- ✅ 24 skills (require "use [skill]" prefix)
- ✅ 12 pet commands with multiple phrase variations
- ✅ 56 UI/gump commands
- ❌ No single-word shortcuts (yet)
- ❌ No compound commands like "heal self"

See [05-simple-vs-suggested-comparison.md](05-simple-vs-suggested-comparison.md) for detailed analysis.

### Existing Macro Compatibility

Of the **78 existing macro types**, voice control feasibility breaks down as:

| Rating | Count | Percentage |
|--------|-------|------------|
| 🟢 Excellent | 45 | 58% |
| 🟡 Good | 18 | 23% |
| 🟠 Moderate | 7 | 9% |
| 🔴 Poor | 8 | 10% |

**Key findings:**
- Most combat macros work well with voice
- Toggle commands are ideal for voice
- Continuous/hold actions don't translate well
- Text input macros benefit from STT transcription

### Proposed New Macros

| Category | New Macros | Priority |
|----------|------------|----------|
| Combat | `VoiceCastOnTarget`, `VoiceQuickCast`, `VoiceAbilityCombo` | High |
| Targeting | `VoiceTargetByName`, `VoiceTargetByType` | High |
| Pets | `VoicePetCommand`, `VoicePetSelect` | High |
| Navigation | `VoiceRecallLocation`, `VoiceGateLocation` | Medium |
| Items | `VoiceUseItem`, `VoiceEquipItem` | Medium |
| Communication | `VoiceQuickChat`, `VoicePartyCommand` | Medium |
| System | `VoiceHelp`, `VoiceConfirmCancel` | Low |

### Implementation Phases

1. **Phase 1 (Combat)**: Cast-on-target, pet commands, quick casts
2. **Phase 2 (Navigation)**: Named locations, item by name
3. **Phase 3 (Communication)**: Quick chat, party management
4. **Phase 4 (Polish)**: Help system, confirmations

## Architecture

```
Speech Input → VAD → STT → Command Parser → Macro Resolver → MacroManager
                                  ↓
                          Action Inference Engine
                                  ↓
                          LLM Backend (optional)
```

## Configuration

Voice control settings are in `settings.json`:

```json
{
  "enable_speech_recognition": true,
  "stt_engine": "vosk",
  "vosk_model": "path/to/model",
  "confidence_threshold": 0.7,
  "voice_command_mode": "simple",
  "inference_mode_enabled": true,
  "inference_backend": "token",
  "recall_safe_word": "hearthstone"
}
```

## Related Code

- `src/ClassicUO.Client/Game/Managers/MacroManager.cs` - Macro execution
- `src/ClassicUO.Client/SpeechRecognition/` - Speech recognition stack
- `src/ClassicUO.Client/SpeechRecognition/Inference/` - Action inference
- `src/ClassicUO.Client/Configuration/Settings.cs` - Voice settings
