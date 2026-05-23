"""Augment PlayerModel.glb with the UOSet animations.

Workflow:
  1. Import PlayerModel.glb (target rig + meshes + existing actions).
  2. For each FBX in Models/anims/UOSet/:
       - Import as donor armature.
       - Try Rokoko auto bone-list build; fall back to manual mapping by
         stripping common bone-name prefixes (mixamorig:, etc).
       - Run rsl.retarget_animation() per donor action.
       - Rename the new target-side action to the slot label
         (e.g. "12_AttackBash_1H").
       - Drop the donor objects.
  3. Skip .anim files — Unity native format, Blender can't import them.
  4. Export to PlayerModel.glb (overwriting; backup made by caller).

Run:
    blender --background --python augment_playermodel_with_uoset.py -- <player.glb> <out.glb> <uoset_dir>
"""
import os
import re
import sys

import bpy
import addon_utils

argv = sys.argv[sys.argv.index("--") + 1:]
in_path = argv[0]
out_path = argv[1]
uoset_dir = argv[2]

# ---------- Bootstrap ---------------------------------------------------------
bpy.ops.wm.read_homefile(use_empty=True)

addon_name = "rokoko_studio_live"
try:
    addon_utils.enable(addon_name, default_set=True, persistent=True)
    print(f"[aug] enabled '{addon_name}'")
except Exception as e:
    raise SystemExit(f"[aug] could not enable Rokoko addon: {e}")

if not hasattr(bpy.ops.rsl, 'retarget_animation'):
    raise SystemExit("[aug] rsl.retarget_animation not registered")

# ---------- 1. Import target (PlayerModel.glb) --------------------------------
bpy.ops.import_scene.gltf(filepath=in_path)
print(f"[aug] imported target: {in_path}")

armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
if not armatures:
    raise SystemExit("[aug] no armature in target glb")
arm = armatures[0]
print(f"[aug] target armature: {arm.name}  bones={len(arm.data.bones)}")

# Capture the actions that came in with PlayerModel.glb so we never delete them.
existing_actions = list(bpy.data.actions)
for a in existing_actions:
    a.use_fake_user = True
    print(f"[aug] keeping existing action: '{a.name}'")

target_bone_names = {b.name for b in arm.data.bones}


# ---------- Humanoid bone canonicalization -----------------------------------
# Maps every common naming convention (Mixamo, Rigify, Unity Humanoid,
# Malbers, VRM/J_Bip, etc) onto a small set of canonical labels so cross-rig
# retargeting can find the equivalents.

_PREFIXES = (
    'mixamorig:', 'mixamorig_', 'mixamorig.',
    'rig_', 'def-', 'def_',
    'rl_', 'rl ',
    'bsp_rig_', 'bsp_rig.', 'bsp_', 'bsp.',
    'cc_base_', 'cc_',
    'j_bip_c_', 'j_sec_c_', 'j_adj_c_', 'j_bip_', 'j_sec_', 'j_adj_',
    'b-',
)
_SIDE_TOKENS = {
    '.l': 'l', '.r': 'r',
    '_l': 'l', '_r': 'r',
    ' l': 'l', ' r': 'r',
}


def _strip_prefix(s: str) -> str:
    s = s.lower().strip()
    while True:
        changed = False
        for p in _PREFIXES:
            if s.startswith(p):
                s = s[len(p):]
                changed = True
        if not changed:
            return s


def _detect_side(s: str) -> tuple[str, str]:
    """Return (core_without_side, side) where side in {'l','r',''}."""
    s_low = s.lower()
    # Long-form prefix (most specific first): "left", "right".
    for tok, side in (('left', 'l'), ('right', 'r')):
        if s_low.startswith(tok):
            return s[len(tok):], side
        if s_low.endswith(tok):
            return s[:-len(tok)], side
    # Short-form with separator: "_l", " l", ".l", "-l", etc.
    for tok, side in _SIDE_TOKENS.items():
        if s_low.startswith(tok + '_') or s_low.startswith(tok + ' ') \
                or s_low.startswith(tok + '.') or s_low.startswith(tok + '-'):
            return s[len(tok) + 1:], side
        if s_low.endswith('_' + tok) or s_low.endswith(' ' + tok) \
                or s_low.endswith('.' + tok) or s_low.endswith('-' + tok):
            return s[:-(len(tok) + 1)], side
    return s, ''


_CORE_SYNONYMS = {
    # core → canonical
    'pelvis': 'hips',
    'hip': 'hips',
    'hips': 'hips',
    'root': '',         # ignore generic root
    'spine': 'spine',
    'spine1': 'spine1',
    'spine2': 'spine2',
    'spine3': 'chest',
    'chest': 'chest',
    'upperchest': 'chest',
    'chest1': 'chest',
    'neck': 'neck',
    'neck1': 'neck',
    'head': 'head',
    'headtop': '',
    'jaw': '',

    'clavicle': 'shoulder',
    'shoulder': 'shoulder',
    'collar': 'shoulder',

    'arm': 'arm',
    'upperarm': 'arm',
    'upper_arm': 'arm',
    'upparm': 'arm',
    'uparm': 'arm',

    'forearm': 'forearm',
    'lowerarm': 'forearm',
    'lower_arm': 'forearm',

    'hand': 'hand',

    'upleg': 'upleg',
    'upperleg': 'upleg',
    'upper_leg': 'upleg',
    'thigh': 'upleg',

    'leg': 'leg',
    'lowerleg': 'leg',
    'lower_leg': 'leg',
    'shin': 'leg',
    'calf': 'leg',
    'knee': 'leg',

    'foot': 'foot',
    'ankle': 'foot',
    'toe': 'toe',
    'toebase': 'toe',
    'toes': 'toe',
}


def canonical_bone(name: str) -> str:
    """Convert any common humanoid bone name to a canonical token like
    'hips', 'spine', 'arm.l', 'forearm.r', etc. Returns '' if no match."""
    n = _strip_prefix(name)
    n_core, side = _detect_side(n)
    n_core = n_core.replace(' ', '').replace('-', '').replace('.', '_')
    n_core = re.sub(r'_+', '_', n_core).strip('_')
    n_core = re.sub(r'_end(_end)*$', '', n_core)
    n_core = re.sub(r'^c_', '', n_core)
    # Also try without underscores at all.
    n_core_compact = n_core.replace('_', '')

    for variant in (n_core, n_core_compact):
        if variant in _CORE_SYNONYMS:
            base = _CORE_SYNONYMS[variant]
            if not base:
                return ''
            return f"{base}.{side}" if side else base
    # Fingers — class together as 'finger.<side>' (rough).
    for f in ('thumb', 'index', 'middle', 'ring', 'pinky', 'little'):
        if f in n_core_compact:
            return f"{f}.{side}" if side else f
    return ''


def normalize_bone(n: str) -> str:  # backward-compat alias
    return canonical_bone(n)


# Map every target bone -> canonical token. Some canonicals will resolve to
# multiple actual target bones (e.g. spine vs spine1 vs spine2); pick the
# first occurrence and keep a list for finger bones if needed.
target_canon_to_actual: dict[str, str] = {}
for b in target_bone_names:
    c = canonical_bone(b)
    if c and c not in target_canon_to_actual:
        target_canon_to_actual[c] = b
print(f"[aug] target canonical map: {len(target_canon_to_actual)} entries")
print(f"[aug] target sample canon: {dict(list(target_canon_to_actual.items())[:8])}")


def collect_fbx(folder: str) -> list[str]:
    out = []
    for fn in sorted(os.listdir(folder)):
        if fn.lower().endswith('.fbx'):
            out.append(os.path.join(folder, fn))
    return out


def slot_label_from_filename(fn: str) -> str:
    """`12_AttackBash_1H__H_Mace_Hit.fbx` -> `12_AttackBash_1H`."""
    base = os.path.basename(fn)
    base = os.path.splitext(base)[0]
    if '__' in base:
        base = base.split('__', 1)[0]
    return base


fbx_files = collect_fbx(uoset_dir)
print(f"[aug] found {len(fbx_files)} .fbx files in {uoset_dir}")

scene = bpy.context.scene
scene.rsl_retargeting_armature_target = arm

added = []
skipped = []

for src_fbx in fbx_files:
    slot = slot_label_from_filename(src_fbx)
    print(f"\n[aug] ====== {slot}  ({os.path.basename(src_fbx)}) ======")

    pre_objs = set(o.name for o in bpy.data.objects)
    pre_acts = set(a.name for a in bpy.data.actions)

    try:
        bpy.ops.import_scene.fbx(filepath=src_fbx)
    except Exception as e:
        print(f"[aug]   FBX import failed: {e}")
        skipped.append((slot, "import-fail"))
        continue

    donor_objs = [o for o in bpy.data.objects if o.name not in pre_objs]
    donor_actions = [a for a in bpy.data.actions if a.name not in pre_acts]
    donor_arms = [o for o in donor_objs if o.type == 'ARMATURE']

    if not donor_arms:
        print(f"[aug]   no donor armature — skipping")
        for o in donor_objs:
            try:
                bpy.data.objects.remove(o, do_unlink=True)
            except Exception:
                pass
        for a in donor_actions:
            try:
                bpy.data.actions.remove(a)
            except Exception:
                pass
        skipped.append((slot, "no-armature"))
        continue

    if not donor_actions:
        print(f"[aug]   no donor actions — skipping")
        for o in donor_objs:
            try:
                bpy.data.objects.remove(o, do_unlink=True)
            except Exception:
                pass
        skipped.append((slot, "no-actions"))
        continue

    donor = donor_arms[0]
    donor.location = (0, 0, 0)
    print(f"[aug]   donor armature: {donor.name}  bones={len(donor.data.bones)}  actions={len(donor_actions)}")
    print(f"[aug]   donor bone sample: {[b.name for b in donor.data.bones[:6]]}")

    # Build manual bone-pair list using canonical matching.
    pairs: list[tuple[str, str]] = []
    unmatched_donor = []
    seen_canon = set()
    for db in donor.data.bones:
        c = canonical_bone(db.name)
        if not c or c in seen_canon:
            unmatched_donor.append((db.name, c))
            continue
        tgt = target_canon_to_actual.get(c)
        if tgt:
            pairs.append((db.name, tgt))
            seen_canon.add(c)
        else:
            unmatched_donor.append((db.name, c))
    print(f"[aug]   matched {len(pairs)}/{len(donor.data.bones)} donor bones to target")
    if len(pairs) < 8 and unmatched_donor:
        print(f"[aug]   unmatched donor bones (first 8): {unmatched_donor[:8]}")

    if len(pairs) < 8:
        print(f"[aug]   too few bone matches — skipping retarget")
        for o in donor_objs:
            try:
                bpy.data.objects.remove(o, do_unlink=True)
            except Exception:
                pass
        for a in donor_actions:
            try:
                bpy.data.actions.remove(a)
            except Exception:
                pass
        skipped.append((slot, f"few-bones:{len(pairs)}"))
        continue

    scene.rsl_retargeting_armature_source = donor

    # Retarget the FIRST donor action only (most FBX have just one).
    src_action = donor_actions[0]
    if donor.animation_data is None:
        donor.animation_data_create()
    donor.animation_data.action = src_action

    scene.rsl_retargeting_bone_list.clear()
    for src_name, tgt_name in pairs:
        bpy.ops.rsl.add_bone_list_item()
        item = scene.rsl_retargeting_bone_list[-1]
        item.bone_name_source = src_name
        item.bone_name_target = tgt_name

    try:
        res = bpy.ops.rsl.retarget_animation()
        print(f"[aug]   retarget_animation -> {res}")
    except Exception as e:
        print(f"[aug]   retarget failed: {e}")
        for o in donor_objs:
            try:
                bpy.data.objects.remove(o, do_unlink=True)
            except Exception:
                pass
        for a in donor_actions:
            try:
                bpy.data.actions.remove(a)
            except Exception:
                pass
        skipped.append((slot, "retarget-fail"))
        continue

    # Find the new action — it lands on the target armature.
    new_action = None
    if arm.animation_data and arm.animation_data.action:
        cand = arm.animation_data.action
        if cand not in existing_actions and cand not in [a for _, a in added]:
            new_action = cand

    if new_action is None:
        # Fallback: anything new in bpy.data.actions that wasn't there before.
        post_acts = [a for a in bpy.data.actions if a.name not in pre_acts]
        for a in post_acts:
            if a is src_action:
                continue
            new_action = a
            break

    if new_action is None:
        print(f"[aug]   no new action produced")
        skipped.append((slot, "no-new-action"))
    else:
        new_action.name = slot
        new_action.use_fake_user = True
        added.append((slot, new_action))
        print(f"[aug]   action '{new_action.name}' fcurves={len(new_action.fcurves)}")

    # Tear down donor objects + the donor's source action.
    for o in donor_objs:
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except Exception:
            pass
    for a in donor_actions:
        if a is new_action:
            continue
        try:
            bpy.data.actions.remove(a)
        except Exception:
            pass


# ---------- Set active action + export ----------------------------------------
if existing_actions:
    if arm.animation_data is None:
        arm.animation_data_create()
    idle = next((a for a in existing_actions if a.name.lower().startswith('idle')),
                existing_actions[0])
    arm.animation_data.action = idle

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(
    filepath=out_path,
    export_format='GLB',
    use_selection=False,
    export_apply=False,
    export_animations=True,
    export_animation_mode='ACTIONS',
    export_skins=True,
    export_yup=True,
)
print(f"\n[aug] === WROTE {out_path} ===")
print(f"[aug] kept {len(existing_actions)} original actions, added {len(added)} new actions")
print(f"[aug] skipped: {len(skipped)}")
for slot, reason in skipped:
    print(f"[aug]   - {slot}: {reason}")
print(f"[aug] added:")
for slot, a in added:
    print(f"[aug]   + {slot}: fcurves={len(a.fcurves)}")
