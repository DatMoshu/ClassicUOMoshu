# UO Animation Checklist (3DCUO Prototype)

Source enum: `PeopleAnimationGroup` in `src/ClassicUO.Assets/AnimationsLoader.cs:1617`.
There are **35 entries** in UO's humanoid (People) anim set. For the prototype we
only need a subset to feel like a UO client; the rest can be added incrementally.

Files live in `prototypes/3DCUO/Models/anims/`. Player3DRenderer maps `AnimState`
to a glb file in `Player3DRenderer.StatePaths`. To wire a new anim into the
runtime, add an entry to `AnimState` enum + `StatePaths` dict, optionally a
gump button.

Mixamo clip suggestions are starting points — pick whichever feels right for
the project's tone.

---

## Tier 1 — Movement + basic combat (MVP, 6 clips)

These are the bare minimum for the prototype to look alive.

| ID | UO Group              | State enum  | glb file                  | Mixamo clip                | Status |
|----|-----------------------|-------------|---------------------------|----------------------------|--------|
| 04 | Stand                 | `Idle`      | `Idle_001.glb`            | "Idle" / "Breathing Idle"  | DONE   |
| 02 | RunUnarmed            | `Run`       | `Run_001.glb`             | "Slow Run" / "Run"         | DONE   |
| 20 | GetHit                | `Hit`       | `Hit_001.glb`             | "Hit Reaction"             | DONE   |
| 10 | AttackUnarmed1        | `Attack`    | `Attack_Punch_001.glb`    | "Punching"                 | DONE   |
| 00 | WalkUnarmed           | `Walk`      | `Walk_001.glb`            | "Walking"                  | TODO   |
| 21 | Die1                  | `Die`       | `Die_001.glb`             | "Dying" / "Falling Back Death" | TODO |

## Tier 2 — Combat variety (8 clips)

Once the basic loop works, these make combat read clearly.

| ID | UO Group              | State enum         | glb file                  | Mixamo clip                                | Status |
|----|-----------------------|--------------------|---------------------------|--------------------------------------------|--------|
| 09 | AttackOnehanded       | `Attack1H`         | `Attack_1H_001.glb`       | "Sword And Shield Slash" / "Standing 2H Magic Attack 01" | TODO |
| 12 | AttackTwohandedDown   | `Attack2HDown`     | `Attack_2HDown_001.glb`   | "Standing Reaction Hit Slash"              | TODO   |
| 13 | AttackTwohandedWide   | `Attack2HWide`     | `Attack_2HWide_001.glb`   | "Sword Slash"                              | TODO   |
| 14 | AttackTwohandedJab    | `Attack2HJab`      | `Attack_2HJab_001.glb`    | "Sword Inward Slash"                       | TODO   |
| 18 | AttackBow             | `AttackBow`        | `Attack_Bow_001.glb`      | "Standing Aim Walk Forward" / "Standing Draw Arrow" | TODO |
| 19 | AttackCrossbow        | `AttackCrossbow`   | `Attack_Crossbow_001.glb` | "Standing 2H Magic Idle 01"                | TODO   |
| 11 | AttackUnarmed2        | `AttackKick`       | `Attack_Kick_001.glb`     | "Mma Kick"                                 | TODO   |
| 22 | Die2                  | `Die2`             | `Die2_001.glb`            | "Falling Forward Death"                    | TODO   |

## Tier 3 — Casting + states (5 clips)

Magic-using gameplay needs these.

| ID | UO Group              | State enum         | glb file                  | Mixamo clip                          | Status |
|----|-----------------------|--------------------|---------------------------|--------------------------------------|--------|
| 16 | CastDirected          | `CastDirected`     | `Cast_Directed_001.glb`   | "Standing 2H Magic Attack 01"        | TODO   |
| 17 | CastArea              | `CastArea`         | `Cast_Area_001.glb`       | "Standing 2H Magic Attack 02"        | TODO   |
| 15 | WalkWarmode           | `WalkWarmode`      | `Walk_Warmode_001.glb`    | "Sword And Shield Walk"              | TODO   |
| 07 | StandOnehandedAttack  | `StandReady1H`     | `Stand_Ready1H_001.glb`   | "Sword And Shield Idle"              | TODO   |
| 08 | StandTwohandedAttack  | `StandReady2H`     | `Stand_Ready2H_001.glb`   | "Standing 2H Magic Idle 01"          | TODO   |

## Tier 4 — Mounted (7 clips)

UO classic mounted set. Lower priority — many shards skip mount-specific anims.

| ID | UO Group                | State enum             | glb file                       | Mixamo clip suggestion             | Status |
|----|-------------------------|------------------------|--------------------------------|------------------------------------|--------|
| 23 | OnmountRideSlow         | `MountedWalk`          | `Mounted_Walk_001.glb`         | "Horse Walk" (or rider clip)       | TODO   |
| 24 | OnmountRideFast         | `MountedRun`           | `Mounted_Run_001.glb`          | "Horse Gallop"                     | TODO   |
| 25 | OnmountStand            | `MountedIdle`          | `Mounted_Idle_001.glb`         | "Horse Idle"                       | TODO   |
| 26 | OnmountAttack           | `MountedAttack`        | `Mounted_Attack_001.glb`       | "Mounted Sword Slash"              | TODO   |
| 27 | OnmountAttackBow        | `MountedAttackBow`     | `Mounted_Bow_001.glb`          | "Mounted Bow Aim"                  | TODO   |
| 28 | OnmountAttackCrossbow   | `MountedAttackCrossbow`| `Mounted_Crossbow_001.glb`     | "Mounted Bow Aim"                  | TODO   |
| 29 | OnmountSlapHorse        | `MountedSlap`          | `Mounted_Slap_001.glb`         | (custom / skip)                    | TODO   |

## Tier 5 — Polish + emotes (9 clips)

Flavor — adds personality but optional for combat-only prototype.

| ID | UO Group                 | State enum          | glb file                      | Mixamo clip suggestion         | Status |
|----|--------------------------|---------------------|-------------------------------|--------------------------------|--------|
| 01 | WalkArmed                | `WalkArmed`         | `Walk_Armed_001.glb`          | "Sword And Shield Walk"        | TODO   |
| 03 | RunArmed                 | `RunArmed`          | `Run_Armed_001.glb`           | "Sword And Shield Run"         | TODO   |
| 05 | Fidget1                  | `Fidget1`           | `Fidget_Look_001.glb`         | "Look Around"                  | TODO   |
| 06 | Fidget2                  | `Fidget2`           | `Fidget_Stretch_001.glb`      | "Idle Stretch" / "Yawn"        | TODO   |
| 30 | Turn                     | `Turn`              | `Turn_001.glb`                | "Standing Turn 90 Right"       | TODO   |
| 31 | AttackUnarmedAndWalk     | `AttackWalk`        | `Attack_Walk_001.glb`         | "Punching Walk"                | TODO   |
| 32 | EmoteBow                 | `EmoteBow`          | `Emote_Bow_001.glb`           | "Bow"                          | TODO   |
| 33 | EmoteSalute              | `EmoteSalute`       | `Emote_Salute_001.glb`        | "Salute"                       | TODO   |
| 34 | Fidget3                  | `Fidget3`           | `Fidget_Bored_001.glb`        | "Bored"                        | TODO   |

---

## Workflow for adding a new anim

1. Mixamo → download clip as **FBX (Without Skin)**, **In Place**, 30 fps, uniform keyframes.
2. Blender: open the master `.blend` (the one with the rigged character + existing actions), `File → Import → FBX`.
3. The import adds a new Action; select it, enable **Fake User** (the shield icon).
4. Delete or hide the imported armature/empty (you only wanted the action).
5. Assign that Action as the active action of the *original* character armature, scrub the timeline to verify it drives the bones.
6. **Important Blender 4.2 quirk:** the glTF exporter only correctly samples actions that are **the active action of the armature that originally owned them via FBX import**. Re-targeting via NLA/active-set produces rest-pose-only samples. Easiest workaround: keep one armature per source FBX and export each separately.
7. `File → Export → glTF 2.0` with:
   - Format: **GLB**
   - Animation: ✅ Animations, ✅ Force Sampling, ❌ Optimize Animation Size, ✅ Export Deformation Bones Only
   - Animation mode: **Active Actions**
8. Save to `prototypes/3DCUO/Models/anims/<Name>_001.glb`.
9. Code: add a row to `AnimState` enum + `Player3DRenderer.StatePaths` mapping.
10. Hot Reload in CUO; verify with the Anim test buttons in Debug3DGump.

## Tips when picking Mixamo clips

- **Always In Place** — UO movement is server-driven; root motion would fight position sync.
- **30 fps + uniform keyframes** keeps clips short and consistent.
- For combat anims, the strike should peak around mid-clip; long wind-up clips read poorly at UO's small character size.
- Match weapon stance — a 1H clip wielding a 2H sword looks wrong even at low resolution.
- For Die anims, freeze on the last frame in the runtime so the corpse stays down (currently just loops; needs work).

## Open questions for later

- Equipment-driven anim selection: the runtime currently hard-codes `Attack` to punch. Real UO picks one of 6+ attack anims based on weapon type. Needs a weapon → AnimState mapping.
- Crossfading: each state swap is currently a hard cut. Adding `prevModel` + lerp over ~150 ms is a small change once we have ≥ 3 states wired up.
- Death state: needs the "play once, hold last frame" mode (currently all clips loop). Add a `holdLastFrame` flag to `AnimState`.
