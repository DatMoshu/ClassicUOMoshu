"""Prep the vendor GLB and retarget the player's actions onto its rig using
the Rokoko Studio Live addon's retargeter.

Pipeline:
    1.  Import jar_jar source GLB (target rig + meshes).
    2.  Collapse Sketchfab parent-empties, drop stray Icosphere, recenter,
        scale to ~1.8m, bake yaw correction, bake recenter into bone rest.
    3.  Drop the source GLB's embedded "Take 001" action.
    4.  Import PlayerModel.glb as donor source rig (kept invisible / off to
        the side; we only need its armature + actions).
    5.  For each donor action:
            - assign as source.action
            - rsl.build_bone_list  (auto-map bones by Mixamo name match)
            - rsl.retarget_animation  (bakes a new action on the target)
    6.  Drop donor armature/meshes + any donor-side actions.
    7.  Resize all images to 32x32 (TextureSkinnedEffect requirement).
    8.  Export.

Run:
    blender --background --python prep_vendor_rokoko.py -- <in.glb> <out.glb> [target_height]
"""
import bpy
import math
import os
import re
import sys
import addon_utils
import mathutils

argv = sys.argv[sys.argv.index("--") + 1:]
in_path  = argv[0]
out_path = argv[1]
target_h = float(argv[2]) if len(argv) > 2 else 1.8

# ---------- Bootstrap Blender + Rokoko addon ----------------------------------
bpy.ops.wm.read_homefile(use_empty=True)

# Enable the Rokoko addon. Folder name in scripts/addons MUST be `rokoko_studio_live`.
addon_name = "rokoko_studio_live"
try:
    addon_utils.enable(addon_name, default_set=True, persistent=True)
    print(f"[prep] enabled addon '{addon_name}'")
except Exception as e:
    raise SystemExit(f"[prep] could not enable Rokoko addon '{addon_name}': {e}")

# Sanity-check the operator exists.
if not hasattr(bpy.ops.rsl, 'retarget_animation'):
    raise SystemExit("[prep] rsl.retarget_animation operator not registered — addon load failed?")

# ---------- 1. Import jar_jar (target) ----------------------------------------
bpy.ops.import_scene.gltf(filepath=in_path)
print(f"[prep] imported source target: {in_path}")

armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
if not armatures:
    raise SystemExit("[prep] no armature in target glb")
arm = armatures[0]
print(f"[prep] target armature: {arm.name}")

# Drop stray meshes not under the armature (e.g. Icosphere).
def is_under(obj, root):
    cur = obj
    while cur:
        if cur == root: return True
        cur = cur.parent
    return False

for m in [o for o in bpy.data.objects if o.type == 'MESH' and not is_under(o, arm)]:
    print(f"[prep] delete stray mesh: {m.name}")
    bpy.data.objects.remove(m, do_unlink=True)

# Flatten parent transforms.
def flatten_parents(obj):
    while obj.parent is not None:
        parent = obj.parent
        m = parent.matrix_world @ obj.matrix_local
        obj.parent = parent.parent
        obj.matrix_world = m
        if not [c for c in bpy.data.objects if c.parent == parent]:
            print(f"[prep]   remove empty parent: {parent.name}")
            bpy.data.objects.remove(parent, do_unlink=True)
        else:
            print(f"[prep]   unparent from {parent.name}")
flatten_parents(arm)

# Apply parent-baked transforms onto rig+mesh.
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
target_meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for m in target_meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Compute world bbox.
def world_bbox(meshes):
    mn = mathutils.Vector(( 1e30,)*3); mx = mathutils.Vector((-1e30,)*3)
    for m in meshes:
        for v in m.bound_box:
            wv = m.matrix_world @ mathutils.Vector(v)
            mn = mathutils.Vector((min(mn[i], wv[i]) for i in range(3)))
            mx = mathutils.Vector((max(mx[i], wv[i]) for i in range(3)))
    return mn, mx

mn, mx = world_bbox(target_meshes)
size = mx - mn
print(f"[prep] pre-scale bbox size={tuple(size)} (Z={size.z:.3f})")

# Recenter X/Y, drop feet to Z=0.
arm.location = (arm.location.x - (mn.x + mx.x) * 0.5,
                arm.location.y - (mn.y + mx.y) * 0.5,
                arm.location.z - mn.z)

# Uniform scale to target_h.
mn, mx = world_bbox(target_meshes); h = mx.z - mn.z
if h > 1e-6:
    s = target_h / h
    arm.scale = (s, s, s)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for m in target_meshes: m.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# Re-recenter feet=0 X/Y=0.
mn, mx = world_bbox(target_meshes)
arm.location = (arm.location.x - (mn.x + mx.x) * 0.5,
                arm.location.y - (mn.y + mx.y) * 0.5,
                arm.location.z - mn.z)
bpy.context.view_layer.update()

# Yaw correction (face camera in iso convention).
arm.rotation_euler = (0.0, 0.0, math.radians(31.58))
bpy.context.view_layer.update()

# Bake recenter+yaw into bones so rest pose matches world.
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for m in target_meshes: m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)

mn, mx = world_bbox(target_meshes)
print(f"[prep] post-prep target bbox min={tuple(mn)} max={tuple(mx)} h={mx.z-mn.z:.3f}")

# Drop the source GLB's embedded action(s) before retarget.
for a in list(bpy.data.actions):
    print(f"[prep] dropping pre-existing action '{a.name}'")
    bpy.data.actions.remove(a)
if arm.animation_data is not None:
    arm.animation_data.action = None

# ---------- 2. Import PlayerModel.glb as donor --------------------------------
player_glb = os.path.normpath(os.path.join(os.path.dirname(in_path), '..', 'PlayerModel.glb'))
if not os.path.exists(player_glb):
    raise SystemExit(f"[prep] PlayerModel.glb not found at {player_glb}")

pre_objs = set(o.name for o in bpy.data.objects)
pre_acts = set(a.name for a in bpy.data.actions)
bpy.ops.import_scene.gltf(filepath=player_glb)
donor_objs    = [o for o in bpy.data.objects if o.name not in pre_objs]
donor_actions = [a for a in bpy.data.actions if a.name not in pre_acts]
donor_arms    = [o for o in donor_objs if o.type == 'ARMATURE']
if not donor_arms:
    raise SystemExit("[prep] PlayerModel.glb has no armature")
donor = donor_arms[0]
print(f"[prep] donor armature: {donor.name} actions={len(donor_actions)}")

# Keep donor at origin — Rokoko retargets in armature-local space, so visual
# overlap is fine, AND if the donor is offset Rokoko bakes the worldspace
# delta into the target action's bone translation channels (a 10m teleport
# at every keyframe).

# ---------- 3. Configure Rokoko + run retarget per donor action ---------------
scene = bpy.context.scene
scene.rsl_retargeting_armature_source = donor
scene.rsl_retargeting_armature_target = arm
print(f"[prep] rsl source={donor.name} target={arm.name}")

# Build bone-name pairs ourselves: target bones strip a trailing _NN, then
# match against donor bones. Rokoko's auto-detect can't see through the
# numeric suffixes our jar_jar rig carries, so feeding the list directly is
# more reliable than rsl.build_bone_list anyway.
def strip_suffix(n):
    m = re.match(r'^(.*?)(_\d+)?$', n)
    return m.group(1) if m else n

donor_bone_names  = {b.name for b in donor.data.bones}
target_pairs = []
for tb in arm.data.bones:
    base = strip_suffix(tb.name)
    if base in donor_bone_names:
        target_pairs.append((base, tb.name))
print(f"[prep] manual bone pairs: {len(target_pairs)}/{len(arm.data.bones)} target bones")

# Newly created retargeted actions land on the TARGET armature.
retargeted = []
for src_action in donor_actions:
    print(f"[prep] === retarget '{src_action.name}' ===")

    if donor.animation_data is None:
        donor.animation_data_create()
    donor.animation_data.action = src_action

    # Seed the bone list by hand (Rokoko's auto-detect doesn't recognize
    # `_NN`-suffixed Mixamo bones). Using add_bone_list_item to mark each
    # entry is_custom=True so the operator's own validation accepts it.
    scene.rsl_retargeting_bone_list.clear()
    for src_name, tgt_name in target_pairs:
        bpy.ops.rsl.add_bone_list_item()
        item = scene.rsl_retargeting_bone_list[-1]
        item.bone_name_source = src_name
        item.bone_name_target = tgt_name

    res = bpy.ops.rsl.retarget_animation()
    print(f"[prep]   retarget_animation -> {res}")

    # Find the new action that landed on the target armature this iteration.
    if arm.animation_data and arm.animation_data.action:
        new_action = arm.animation_data.action
        # Strip the donor-source name decoration if present, normalize.
        clean = src_action.name.replace('_Armature', '')
        new_action.name = clean
        new_action.use_fake_user = True
        retargeted.append(new_action)
        print(f"[prep]   produced retargeted action '{new_action.name}' fcurves={len(new_action.fcurves)}")

# ---------- 4. Tear down donor + donor-side actions ---------------------------
for o in list(donor_objs):
    try:
        bpy.data.objects.remove(o, do_unlink=True)
    except Exception:
        pass
for a in list(bpy.data.actions):
    if a in retargeted:
        continue
    print(f"[prep] purging non-retargeted action '{a.name}'")
    bpy.data.actions.remove(a)

# Set the active action on target so the export wires it as anim slot 0.
if retargeted:
    if arm.animation_data is None:
        arm.animation_data_create()
    idle = next((a for a in retargeted if 'idle' in a.name.lower()), retargeted[0])
    arm.animation_data.action = idle
    print(f"[prep] active action: '{idle.name}'")

# ---------- 5. Resize images for TextureSkinnedEffect's 32×32 atlas -----------
ATLAS = 32
for img in list(bpy.data.images):
    if img.size[0] == 0 or img.size[1] == 0: continue
    if img.size[0] == ATLAS and img.size[1] == ATLAS: continue
    print(f"[prep] resize image '{img.name}' {img.size[0]}x{img.size[1]} -> {ATLAS}x{ATLAS}")
    img.scale(ATLAS, ATLAS); img.pack()

# Purge orphans (donor mesh leftovers, etc.)
for _ in range(2):
    try: bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
    except Exception: pass

# ---------- 6. Export ---------------------------------------------------------
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
print(f"[prep] === WROTE {out_path} ===")
