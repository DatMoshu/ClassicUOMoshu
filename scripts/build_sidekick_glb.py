"""Build PlayerModel_Sidekick.glb — Synty Sidekick armature + combined human
base mesh + every UOSet animation retargeted onto the Sidekick rig + the
original PlayerModel.glb actions retargeted onto the Sidekick rig.

Output: a single GLB you can preview in Blender or load via the engine,
with the bone names matching the Synty attachment pipeline (so head/teeth/
hair/armor weights skin correctly without bone-name mismatches).

Run:
    blender --background --python build_sidekick_glb.py -- \
        <sidekick_fbx> <player_glb> <uoset_dir> <out_glb>

Pipeline:
  1. Import SK_BaseModel.fbx  -> Sidekick rig + 25 human base meshes.
  2. Drop any embedded "Take 001" actions on the Sidekick rig.
  3. Import PlayerModel.glb as a donor (Mixamo-rigged), retarget its 6
     existing actions to the Sidekick rig.
  4. Walk Models/anims/UOSet/*.fbx, import each as donor, retarget the
     first action onto the Sidekick rig with the slot name baked in.
  5. Tear down all donor objects + pre-existing/un-retargeted actions.
  6. Export as one GLB.
"""
import os
import re
import sys

import addon_utils
import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
sk_fbx = argv[0]
player_glb = argv[1]
uoset_dir = argv[2]
out_glb = argv[3]

# ---------- Bootstrap ---------------------------------------------------------
bpy.ops.wm.read_homefile(use_empty=True)
addon_utils.enable("rokoko_studio_live", default_set=True, persistent=True)
if not hasattr(bpy.ops.rsl, 'retarget_animation'):
    raise SystemExit("[bld] rsl.retarget_animation missing")


# ---------- Bone canonicalization (cross-rig humanoid matching) ---------------
_PREFIXES = (
    'mixamorig:', 'mixamorig_', 'mixamorig.',
    'rig_', 'def-', 'def_',
    'rl_', 'rl ',
    'bsp_rig_', 'bsp_rig.', 'bsp_', 'bsp.',
    'cc_base_', 'cc_',
    'j_bip_c_', 'j_sec_c_', 'j_adj_c_', 'j_bip_', 'j_sec_', 'j_adj_',
    'humanoid_ ', 'humanoid_', 'humanoid ',
    'b-',
)
_SIDE_TOKENS = {
    '.l': 'l', '.r': 'r',
    '_l': 'l', '_r': 'r',
    ' l': 'l', ' r': 'r',
    'l_': 'l', 'r_': 'r',
    'l.': 'l', 'r.': 'r',
    'l ': 'l', 'r ': 'r',
}

MID_AXIS = {'hips', 'spine', 'spine1', 'spine2', 'chest', 'neck', 'head'}


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


def _detect_side_once(s: str) -> tuple[str, str]:
    s_low = s.lower()
    for tok, side in (('left', 'l'), ('right', 'r')):
        if s_low.startswith(tok):
            return s[len(tok):], side
        if s_low.endswith(tok):
            return s[:-len(tok)], side
    for tok, side in _SIDE_TOKENS.items():
        if s_low.endswith(tok):
            return s[:-len(tok)], side
        if s_low.startswith(tok) and len(s_low) > len(tok):
            return s[len(tok):], side
    return s, ''


def _detect_side(s: str) -> tuple[str, str]:
    """Recursive side detection — innermost (last-found) side wins.
    Handles Malbers-style 'R_L Hand' (namespace 'R_' + side 'L ')."""
    last_side = ''
    cur = s
    for _ in range(3):
        new_cur, side = _detect_side_once(cur)
        if not side:
            break
        last_side = side
        cur = new_cur
    return cur, last_side


_CORE = {
    'pelvis': 'hips', 'hip': 'hips', 'hips': 'hips',
    'root': '',
    'spine': 'spine', 'spine1': 'spine1', 'spine2': 'spine2',
    'spine3': 'chest', 'chest': 'chest', 'upperchest': 'chest',
    'chest1': 'chest',
    'neck': 'neck', 'neck1': 'neck',
    'head': 'head', 'headtop': '', 'jaw': '',
    'clavicle': 'shoulder', 'shoulder': 'shoulder', 'collar': 'shoulder',
    'arm': 'arm', 'upperarm': 'arm', 'upper_arm': 'arm',
    'upparm': 'arm', 'uparm': 'arm',
    'forearm': 'forearm', 'lowerarm': 'forearm', 'lower_arm': 'forearm',
    'hand': 'hand',
    'upleg': 'upleg', 'upperleg': 'upleg', 'upper_leg': 'upleg', 'thigh': 'upleg',
    'leg': 'leg', 'lowerleg': 'leg', 'lower_leg': 'leg',
    'shin': 'leg', 'calf': 'leg', 'knee': 'leg',
    'foot': 'foot', 'ankle': 'foot',
    'toe': 'toe', 'toebase': 'toe', 'toes': 'toe',
}


def canonical_bone(name: str) -> str:
    n = _strip_prefix(name)
    n_core, side = _detect_side(n)
    n_core = n_core.replace(' ', '').replace('-', '').replace('.', '_')
    n_core = re.sub(r'_+', '_', n_core).strip('_')
    n_core = re.sub(r'_end(_end)*$', '', n_core)
    n_core = re.sub(r'^c_', '', n_core)

    # Handle numbered variants: spine_01 -> spine, spine_02 -> spine1, etc.
    # Synty Sidekick convention. Map suffix _01 -> '', _02 -> '1', _03 -> '2', _04 -> '3'.
    # Also handles trailing 01/02/03 without underscore.
    m = re.match(r'^(.*?)[_]?(\d{1,2})$', n_core)
    if m and m.group(1):
        base = m.group(1).rstrip('_')
        idx = int(m.group(2))
        if idx == 1:
            n_core = base
        elif idx <= 4:
            n_core = f"{base}{idx - 1}"

    compact = n_core.replace('_', '')
    for v in (n_core, compact):
        if v in _CORE:
            base = _CORE[v]
            if not base:
                return ''
            if base in MID_AXIS:
                return base
            return f"{base}.{side}" if side else base
    for f in ('thumb', 'index', 'middle', 'ring', 'pinky', 'little'):
        if f in compact:
            return f"{f}.{side}" if side else f
    return ''


# Canonical-map debug: log every input -> output mapping for the target rig.
def debug_canonical_map(bone_names: list[str], label: str) -> dict[str, str]:
    out = {}
    miss = []
    for b in bone_names:
        c = canonical_bone(b)
        if c:
            out.setdefault(c, b)
        else:
            miss.append(b)
    print(f"[bld] {label}: {len(out)} canonical entries from {len(bone_names)} bones")
    print(f"[bld]   sample mapped: {list(out.items())[:8]}")
    if miss:
        print(f"[bld]   unmapped (first 12): {miss[:12]}")
    return out


# ---------- 1. Import Sidekick rig + meshes -----------------------------------
print(f"[bld] === importing Sidekick rig + meshes: {sk_fbx} ===")
bpy.ops.import_scene.fbx(filepath=sk_fbx)

sk_arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
if not sk_arms:
    raise SystemExit("[bld] SK_BaseModel.fbx has no armature")
arm = sk_arms[0]
print(f"[bld] target rig '{arm.name}' bones={len(arm.data.bones)}")
print(f"[bld] target meshes: {[o.name for o in bpy.data.objects if o.type == 'MESH']}")

# Drop any actions that came in with the Sidekick FBX (rest-pose junk).
for a in list(bpy.data.actions):
    print(f"[bld]   dropping baseline action '{a.name}'")
    bpy.data.actions.remove(a)
if arm.animation_data is not None:
    arm.animation_data.action = None

# Build target canonical map.
target_canon = debug_canonical_map([b.name for b in arm.data.bones], "target rig")

scene = bpy.context.scene
scene.rsl_retargeting_armature_target = arm

added: list[tuple[str, "bpy.types.Action"]] = []
skipped: list[tuple[str, str]] = []


def retarget_donor(donor_obj, donor_action, slot_name: str) -> bool:
    pairs: list[tuple[str, str]] = []
    seen = set()
    for db in donor_obj.data.bones:
        c = canonical_bone(db.name)
        if not c or c in seen:
            continue
        tgt = target_canon.get(c)
        if tgt:
            pairs.append((db.name, tgt))
            seen.add(c)
    print(f"[bld]   matched {len(pairs)}/{len(donor_obj.data.bones)} donor bones")
    if len(pairs) < 8:
        skipped.append((slot_name, f"few-bones:{len(pairs)}"))
        return False

    if donor_obj.animation_data is None:
        donor_obj.animation_data_create()
    donor_obj.animation_data.action = donor_action
    scene.rsl_retargeting_armature_source = donor_obj
    scene.rsl_retargeting_bone_list.clear()
    for src_name, tgt_name in pairs:
        bpy.ops.rsl.add_bone_list_item()
        item = scene.rsl_retargeting_bone_list[-1]
        item.bone_name_source = src_name
        item.bone_name_target = tgt_name
    try:
        res = bpy.ops.rsl.retarget_animation()
        print(f"[bld]   retarget -> {res}")
    except Exception as e:  # noqa: BLE001
        print(f"[bld]   retarget threw: {e}")
        skipped.append((slot_name, "retarget-fail"))
        return False

    new_action = arm.animation_data.action if arm.animation_data else None
    if new_action is None or new_action is donor_action \
            or new_action in [a for _, a in added]:
        skipped.append((slot_name, "no-new-action"))
        return False
    new_action.name = slot_name
    new_action.use_fake_user = True
    added.append((slot_name, new_action))
    print(f"[bld]   action '{new_action.name}' fcurves={len(new_action.fcurves)}")
    return True


def cleanup_donor(donor_objs, donor_actions):
    for o in donor_objs:
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except Exception:
            pass
    for a in donor_actions:
        if any(a is x for _, x in added):
            continue
        try:
            bpy.data.actions.remove(a)
        except Exception:
            pass


# ---------- 2. Retarget the existing PlayerModel.glb actions ------------------
print(f"\n[bld] === Retargeting PlayerModel.glb actions ===")
pre_objs = set(o.name for o in bpy.data.objects)
pre_acts = set(a.name for a in bpy.data.actions)
bpy.ops.import_scene.gltf(filepath=player_glb)
donor_objs = [o for o in bpy.data.objects if o.name not in pre_objs]
donor_acts = [a for a in bpy.data.actions if a.name not in pre_acts]
donor_arms = [o for o in donor_objs if o.type == 'ARMATURE']

if donor_arms:
    pdonor = donor_arms[0]
    pdonor.location = (0, 0, 0)
    print(f"[bld] PlayerModel donor '{pdonor.name}' bones={len(pdonor.data.bones)} actions={len(donor_acts)}")
    for src in list(donor_acts):
        clean_name = src.name.replace('_Armature', '').strip()
        slot = f"player__{clean_name}"
        print(f"\n[bld] -- {slot}")
        retarget_donor(pdonor, src, slot)
else:
    print("[bld] WARN no PlayerModel donor armature — skipping its actions")

cleanup_donor(donor_objs, donor_acts)

# ---------- 3. Retarget every UOSet FBX ---------------------------------------
print(f"\n[bld] === Retargeting UOSet FBX files in {uoset_dir} ===")
fbx_files = sorted(
    os.path.join(uoset_dir, fn) for fn in os.listdir(uoset_dir)
    if fn.lower().endswith('.fbx')
)
print(f"[bld] {len(fbx_files)} FBX files queued")


def slot_label(path: str) -> str:
    base = os.path.splitext(os.path.basename(path))[0]
    return base.split('__', 1)[0] if '__' in base else base


for fbx in fbx_files:
    slot = slot_label(fbx)
    print(f"\n[bld] -- {slot}  ({os.path.basename(fbx)})")
    pre_objs = set(o.name for o in bpy.data.objects)
    pre_acts = set(a.name for a in bpy.data.actions)
    try:
        bpy.ops.import_scene.fbx(filepath=fbx)
    except Exception as e:  # noqa: BLE001
        print(f"[bld]   import-fail: {e}")
        skipped.append((slot, "import-fail"))
        continue
    donor_objs = [o for o in bpy.data.objects if o.name not in pre_objs]
    donor_acts = [a for a in bpy.data.actions if a.name not in pre_acts]
    donor_arms = [o for o in donor_objs if o.type == 'ARMATURE']
    if not donor_arms:
        print(f"[bld]   no donor armature — skip")
        skipped.append((slot, "no-armature"))
        cleanup_donor(donor_objs, donor_acts)
        continue
    if not donor_acts:
        print(f"[bld]   no donor actions — skip")
        skipped.append((slot, "no-actions"))
        cleanup_donor(donor_objs, donor_acts)
        continue
    donor = donor_arms[0]
    donor.location = (0, 0, 0)
    print(f"[bld]   donor '{donor.name}' bones={len(donor.data.bones)}")
    retarget_donor(donor, donor_acts[0], slot)
    cleanup_donor(donor_objs, donor_acts)


# ---------- 4. Set active action + export -------------------------------------
if added:
    if arm.animation_data is None:
        arm.animation_data_create()
    idle = next((a for s, a in added if 'idle' in s.lower()), added[0][1])
    arm.animation_data.action = idle
    print(f"\n[bld] active action: '{idle.name}'")

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(
    filepath=out_glb,
    export_format='GLB',
    use_selection=False,
    export_apply=False,
    export_animations=True,
    export_animation_mode='ACTIONS',
    export_skins=True,
    export_yup=True,
)

print(f"\n[bld] === WROTE {out_glb} ===")
print(f"[bld] added: {len(added)}  skipped: {len(skipped)}")
for s, a in added:
    print(f"[bld]   + {s}  fcurves={len(a.fcurves)}")
for s, r in skipped:
    print(f"[bld]   - {s}  ({r})")
