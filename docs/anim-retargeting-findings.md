# 3DCUO — Animation Retargeting Findings

Captured 2026-05-02 / 2026-05-03 after a multi-day attempt to populate the player rig with the 38-slot UO animation set.

## TL;DR

- **Headless Blender + Rokoko Studio Live retargeting cannot produce shippable clips for cross-pack humanoids.** It's name-based with no rest-pose normalization. Arms snap to T-pose, legs go stiff, hair/teeth detach because a few weighted bones don't have a match.
- **Unity Mecanim Humanoid is the right tool.** Its muscle-space normalization handles rest-pose deltas, scale, and bone-name divergence across rigs by design. The `UoSidekickBaker` editor tool wraps the bake into a 4-step pipeline.
- **PROTOFACTOR Ultimate Animation Collection is the correct single source.** ~3000 humanoid FBXs, all on one consistent skeleton. 30 of 38 UO slots map cleanly; the 8 mounted slots need a different pack (Farming Engine's `character@riding_*`).
- **Synty Sidekick rig (`SK_BaseModel.fbx`)** is the correct target. It ships with the rig + 25 base mesh parts pre-bound (head, hair, eyebrows, eyes, ears, facial hair, torso, arms, legs, hands, feet, hips, nose, **teeth**, tongue) so attachments referencing Synty bone names skin correctly.

## What didn't work

### Attempt 1 — multi-source FBX pull + headless Blender Rokoko retarget

- Sources: 24 FBXs from Mixamo / Malbers / RPG Creation Kit / Aurora FPS / KAWAII / VRoid mixed packs.
- 6 different bone-naming conventions (Mixamo `mixamorig:Hips`, Malbers `R_Pelvis`, VRM `J_Bip_C_Hips`, BSP `BSP_rig_Neck`, KAWAII `Hips` with spaces, etc.).
- 12 of 24 FBXs were anim-only exports without armature → unimportable as donors at all.
- Of the 12 with armatures, name canonicalizer matched ≤9 bones to the Mixamo target — only torso/spine/head matched, never arms/legs.
- Even when bone matching succeeded, Rokoko's literal F-curve copy ignored rest-pose deltas → arms locked in donor's T-pose at frame 0.

### Attempt 2 — same pipeline targeting Synty Sidekick

- 36/36 retargeted "successfully" by name matching after canonicalizer fixes (added `humanoid_ ` prefix stripping, `MID_AXIS` set for non-sided bones, recursive side detection for `R_L Hand`-style namespaced bones).
- Visually broken: same arms-T-posed / stiff-legs / detached-hair issues. Confirmed via Blender preview.
- Conclusion: name-based retargeting will get further with one consistent source pack but cannot solve the rest-pose-delta problem without per-bone manual fixup.

## What works — Unity Mecanim Humanoid

The `UoSidekickBaker` editor tool at `D:\_repos\SyntyAssets\SyntyAssets\Assets\Editor\UoSidekickBaker\` runs this pipeline:

1. **Configure Imports**: each PROTOFACTOR FBX → `Animation Type = Humanoid` with `Avatar Definition = Create From This Model`. Each FBX gets its own auto-built avatar; Unity normalizes the clip into avatar-independent muscle space at import time.
2. **Bake Clips**: spawn the Sidekick prefab, sample each Humanoid clip via `AnimationMode.SampleAnimationClip(go, clip, time)` per frame on the **target** rig. Capture every Sidekick bone's `localPosition`/`localRotation`. Write a generic `AnimationClip` bound to Sidekick transform paths.
3. **Build Animator Prefab**: AnimatorController + prefab with all baked clips.
4. **Export FBX**: binary FBX with `AnimateSkinnedMesh = true`, `EmbedTextures = true`. Convert to GLB with one-line Blender script.

Why this works where Blender Rokoko fails: muscle space encodes joint angle relative to a normalized humanoid skeleton, so the same clip plays correctly on a stretched, squashed, or differently-named rig. Name matching isn't required between source and target avatars.

## Critical Unity-side gotchas hit (and fixed)

| Symptom | Root cause | Fix |
|---|---|---|
| `Rig Error: Copied Avatar Rig Configuration mis-match. Transform 'pelvis' for human bone 'Hips' not found` (×30) | Tried `Copy From Other Avatar` pointing at SK_BaseModel — Unity applied SK's literal `Hips→pelvis` transform mapping to PROTOFACTOR rigs that have no `pelvis` transform. | Switch to `CreateFromThisModel` so each FBX builds its own bone-mapping. |
| `skip (no clip)` for every FBX in Step 2 | Failed Humanoid imports left no AnimationClip subasset. Cascade from above. | Same fix. |
| `error CS0122: 'ExportSettings' is inaccessible` | The `ExportSettings` ScriptableObject in `com.unity.formats.fbx@5.1.5` is `internal`. The enums (`ExportFormat`, `Include`, `ObjectPosition`) are top-level public in the same namespace, **not** nested. | Drop the `ExportSettings.` prefix; the existing `using UnityEditor.Formats.Fbx.Exporter;` brings them into scope. |
| `ASCII FBX files are not supported` | Default `ModelExporter.ExportObject(path, obj)` writes ASCII. | Construct `ExportModelOptions` with `ExportFormat = ExportFormat.Binary`, `AnimateSkinnedMesh = true`, `EmbedTextures = true`. |
| Unity MCP not callable from Claude Code | MCP server runs inside Unity Editor on `127.0.0.1:8080`, but Claude Code needs it registered. | `claude mcp add --transport http unity http://127.0.0.1:8080/mcp` then restart the Claude Code session. |

## Source-pack mapping (PROTOFACTOR → UO slots)

30 of 38 UO action slots have direct PROTOFACTOR matches. See `Data/protofactor-mapping.json`:

| Slot | PROTOFACTOR | Notes |
|---|---|---|
| 0 Walk_Unarmed | `Basic Locomotion / Humanoid@WalkForwardUnarmed.FBX` | |
| 1 Walk_Armed | `Sword&Shield / Humanoid@WalkForwardS&S.fbx` | |
| 2 Run_Unarmed | `Basic Locomotion / Humanoid@RunForwardUnarmed.FBX` | |
| 3 Run_Armed | `Sword&Shield / Humanoid@RunForwardS&S.fbx` | |
| 4 Idle | `Basic Locomotion / Humanoid@IdleUnarmed.FBX` | |
| 6 Fidget_Yawn | `Basic Locomotion / Humanoid@IdleLookAroundScratchYawnUnarmed.fbx` | combo |
| 7 Fidget_Scratch | `Crowd / Humanoid@Idle1ScratchHead.fbx` | |
| 10–15 Attack* | `1Handed/2Handed Melee Animset` | |
| 17–18 Spell_* | `Wizard / Humanoid@CastSpell{1,5}Wizard.fbx` | |
| 19 Hit_Reaction | `Combat Bare Fists / Humanoid@GetHitFrontLightCombat.fbx` | |
| 20–21 Die_* | `Combat Bare Fists / Humanoid@DeathBackCombat.fbx` etc. | |
| 30 Block_Parry | `Sword&Shield / Humanoid@BlockLightS&S.fbx` | |
| 31 Punch | `Combat Bare Fists / Humanoid@CrushingFists.fbx` | |
| 32 Kick | `Combat Bare Fists / Humanoid@2KicksCombo1Forward.fbx` | |
| 33–34 Bow/Crossbow | `Bow & Arrow / Humanoid@DrawArrow.fbx` | crossbow uses bow draw — pack has no native crossbow |
| 35 GetHit_Back | `Combat Bare Fists / Humanoid@GetHitBackLightCombat.fbx` | |
| 36 Salute | `Crowd / Humanoid@Idle1Wave.fbx` | closest pack option |
| 37 Eat | `Campfire / Humanoid@KneelEatSkewerCampfire.fbx` | the only "eat"-like clip in the pack |
| 22–29 Mounted_* | **(none in PROTOFACTOR)** | use Farming Engine `character@riding_*` |

To re-run the mapping after pack updates:
```bash
python tools/scripts/protofactor_map.py     # writes Data/protofactor-mapping.json
python tools/scripts/protofactor_pull.py    # copies FBXs to prototypes/3DCUO/Models/anims/UOSet_PROTOFACTOR/
```

## Per-clip QA status

To be filled in once the in-client `PlayerAnimDebugGump` (Sprint 12 TB-12.2) is live and we can inspect each baked clip on the Sidekick rig:

| Slot | Clip | Status | Notes |
|---|---|---|---|
| 0 | 00_Walk_Unarmed | TBD | |
| 1 | 01_Walk_Armed | TBD | |
| ... | | | |

## Tooling artifacts produced

- `tools/scripts/protofactor_map.py` — UO slot → PROTOFACTOR FBX mapper
- `tools/scripts/protofactor_pull.py` — copies mapped FBXs into prototype Models/anims/
- `prototypes/3DCUO/scripts/build_sidekick_glb.py` — **deprecated**, headless Blender Rokoko approach
- `D:\_repos\SyntyAssets\SyntyAssets\Assets\Editor\UoSidekickBaker\` — Unity editor tool (canonical replacement)
- `Data/protofactor-mapping.json` — slot → FBX path JSON
- `Data/uo-human-animations.xlsx` — full UO-slot inventory + PROTOFACTOR best-pick column
- `prototypes/3DCUO/Models/PlayerModel.backup.glb` — original Mixamo build, preserved
- `prototypes/3DCUO/Models/PlayerModel_Sidekick.glb` — current Synty build (broken animations until baker rerun via Unity)
- `prototypes/3DCUO/Models/Synty/SK_BaseModel.fbx` — Synty Sidekick rig + 25 base mesh parts

## Decision

Going forward: **all retargeting goes through the Unity `UoSidekickBaker`.** The Blender Rokoko script is kept for reference but should not be used to produce shippable clips.
