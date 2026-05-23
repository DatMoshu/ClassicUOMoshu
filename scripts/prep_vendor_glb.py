"""Prep a vendor GLB for the 3DCUO renderer.

- Delete stray non-rigged meshes (e.g. Icosphere placeholder).
- Reparent rig+meshes out of any scaling parent empties (apply parent xform).
- Recenter so X/Y bbox center == 0 and feet (bbox min Z) sit on Z=0.
- Uniform-rescale so total height ~= TARGET_HEIGHT (default 1.8).
- Re-export as a single GLB with the animation preserved.

Run:
  blender --background --python prep_vendor_glb.py -- <in.glb> <out.glb> [target_height]
"""
import bpy
import sys
import mathutils

argv = sys.argv
argv = argv[argv.index("--") + 1:]
in_path  = argv[0]
out_path = argv[1]
target_h = float(argv[2]) if len(argv) > 2 else 1.8

bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.gltf(filepath=in_path)

print(f"=== PREP {in_path} -> {out_path} (target_height={target_h}) ===")

# --- 1. Delete stray meshes that aren't parented to the armature ---
armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
if not armatures:
    raise SystemExit("ERROR: no armature in GLB")
arm = armatures[0]
print(f"Armature: {arm.name}")

def is_under_armature(obj):
    cur = obj
    while cur:
        if cur == arm:
            return True
        cur = cur.parent
    return False

stray = [o for o in bpy.data.objects if o.type == 'MESH' and not is_under_armature(o)]
for s in stray:
    print(f"  delete stray mesh: {s.name}")
    bpy.data.objects.remove(s, do_unlink=True)

# --- 2. Apply all parent transforms by parenting armature directly to scene ---
# Walk up the parent chain of `arm` and bake each parent's matrix into arm's matrix.
def flatten_parents(obj):
    while obj.parent is not None:
        parent = obj.parent
        # Bake parent matrix into obj
        m = parent.matrix_world @ obj.matrix_local
        obj.parent = parent.parent
        obj.matrix_basis = parent.matrix_basis @ obj.matrix_basis if False else m
        obj.matrix_world = m
        # If parent now has no other children that need it, drop it
        kids = [c for c in bpy.data.objects if c.parent == parent]
        if not kids:
            print(f"  remove empty parent: {parent.name}")
            bpy.data.objects.remove(parent, do_unlink=True)
        else:
            print(f"  unparent from {parent.name} (still has children {[c.name for c in kids]})")

flatten_parents(arm)

# Recompute & apply armature's own transform so loc=0, rot=0, scale=1.
# Select only the armature (with its mesh children) and apply.
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for m in [o for o in bpy.data.objects if o.type == 'MESH']:
    m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# --- 3. Compute world bbox (axis-aligned, Z-up in Blender) ---
def world_bbox(meshes):
    mn = mathutils.Vector(( 1e30,  1e30,  1e30))
    mx = mathutils.Vector((-1e30, -1e30, -1e30))
    for m in meshes:
        for v in m.bound_box:
            wv = m.matrix_world @ mathutils.Vector(v)
            mn.x = min(mn.x, wv.x); mn.y = min(mn.y, wv.y); mn.z = min(mn.z, wv.z)
            mx.x = max(mx.x, wv.x); mx.y = max(mx.y, wv.y); mx.z = max(mx.z, wv.z)
    return mn, mx

meshes = [o for o in bpy.data.objects if o.type == 'MESH']
mn, mx = world_bbox(meshes)
size = mx - mn
print(f"Pre-recentre bbox min={tuple(mn)} max={tuple(mx)} size={tuple(size)}")

# --- 4. Translate so X/Y center=0 and bbox min Z = 0 ---
cx = (mn.x + mx.x) * 0.5
cy = (mn.y + mx.y) * 0.5
dz = -mn.z
arm.location = (arm.location.x - cx, arm.location.y - cy, arm.location.z + dz)
bpy.context.view_layer.update()
mn, mx = world_bbox(meshes)
print(f"Post-recentre bbox min={tuple(mn)} max={tuple(mx)}")

# --- 5. Uniform scale so height (Z) == target_h ---
height = mx.z - mn.z
if height > 1e-6:
    s = target_h / height
    arm.scale = (s, s, s)
    bpy.context.view_layer.update()
    # Re-apply to bake the scale into vertices/bones
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mn, mx = world_bbox(meshes)
    print(f"Post-scale (s={s:.4f}) bbox min={tuple(mn)} max={tuple(mx)}")

# --- 6. Re-translate to feet=Z=0 (rescale may have nudged it), THEN bake the
#       location into the bone rest pose so bone-local rest matches world rest.
#       Without this last apply, copied animations will reference bones whose
#       head_local is far from origin, and any F-curve that isn't perfectly
#       (0,0,0) at rest will visibly offset the model in armature-local space.
mn, mx = world_bbox(meshes)
arm.location = (arm.location.x - (mn.x + mx.x) * 0.5,
                arm.location.y - (mn.y + mx.y) * 0.5,
                arm.location.z - mn.z)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for m in [o for o in bpy.data.objects if o.type == 'MESH']:
    m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
mn, mx = world_bbox(meshes)
print(f"Final bbox min={tuple(mn)} max={tuple(mx)} height={mx.z-mn.z:.3f}")

# --- 6.4. Bake yaw correction so bind-pose faces +Y (toward UO "North"). ---
# The source mesh's authored forward isn't aligned with the renderer's
# expected forward, so without this it stands ~32° off when the in-game
# Direction is North. Quaternion (W=0.962, Z=0.272) ≈ +31.58° about Blender's
# Z (up) — captured by the user in Blender after rotating-to-face-camera.
import math
YAW_FIX_DEG = 31.58
arm.rotation_euler = (0.0, 0.0, math.radians(YAW_FIX_DEG))
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for m in [o for o in bpy.data.objects if o.type == 'MESH']:
    m.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
print(f"  baked yaw correction: +{YAW_FIX_DEG}° about Z (up)")

# --- 6.6. Import every action from PlayerModel.glb and retarget to our armature. ---
# The vendor renderer must match the player's action-name slot system
# (Idle / Run / Walk / Hit / Attack / Die — see Player3DRenderer.StateActionNames),
# so we drop whatever animation came embedded in the source GLB and replace
# the action set with the player's authored set. Both rigs are Mixamo-derived
# (same bind pose), but the GLB-side bone names carry a `_NN` suffix (e.g.
# `mixamorig:Hips_10`) while the player GLB uses clean `mixamorig:Hips`. We
# rewrite each F-curve's data_path to the suffixed bone. F-curves targeting
# bones the vendor rig doesn't have (finger digits) are dropped — at iso
# scale the vendor's hands aren't visibly articulated anyway.
import re
import os

# 1) Drop the source GLB's embedded action(s). The vendor will exclusively
#    use the player-authored action set after this step.
for a in list(bpy.data.actions):
    print(f"  dropping pre-existing action '{a.name}'")
    bpy.data.actions.remove(a)
if arm.animation_data is not None:
    arm.animation_data.action = None

# 2) Build the bone-name remap from the target armature.
base_to_full = {}
for b in arm.data.bones:
    m = re.match(r'^(.*?)(_\d+)?$', b.name)
    base = m.group(1) if m else b.name
    base_to_full[base] = b.name

player_glb = os.path.join(os.path.dirname(in_path), '..', 'PlayerModel.glb')
player_glb = os.path.normpath(player_glb)
if not os.path.exists(player_glb):
    print(f"  WARNING: PlayerModel.glb not found at {player_glb} — vendor will have NO actions")
else:
    print(f"  importing player actions from: {player_glb}")
    pre_objects = set(o.name for o in bpy.data.objects)
    pre_actions = set(a.name for a in bpy.data.actions)

    bpy.ops.import_scene.gltf(filepath=player_glb)

    new_actions = [a for a in bpy.data.actions if a.name not in pre_actions]
    new_objects = [o for o in bpy.data.objects if o.name not in pre_objects]
    src_arms    = [o for o in new_objects if o.type == 'ARMATURE']
    src_meshes  = [o for o in new_objects if o.type == 'MESH']
    src_empties = [o for o in new_objects if o.type == 'EMPTY']

    bone_path_re = re.compile(r'pose\.bones\["([^"]+)"\]')
    for action in new_actions:
        kept = dropped = stripped_loc = 0
        for fc in list(action.fcurves):
            dp = fc.data_path
            # Strip ALL bone .location channels — Mixamo authors root motion
            # via hips translation, and the cm-vs-m scale mismatch between
            # player and vendor rigs makes the values blow the model far off
            # origin even on non-root bones. Game-engine-style: rotations only.
            if dp.endswith('.location') and 'pose.bones[' in dp:
                action.fcurves.remove(fc)
                stripped_loc += 1
                continue
            m = bone_path_re.search(dp)
            if not m:
                continue
            src_name = m.group(1)
            if src_name in base_to_full:
                tgt_name = base_to_full[src_name]
                if tgt_name != src_name:
                    fc.data_path = dp.replace(f'"{src_name}"', f'"{tgt_name}"', 1)
                kept += 1
            else:
                action.fcurves.remove(fc)
                dropped += 1
        action.use_fake_user = True
        print(f"    action '{action.name}': kept={kept} dropped={dropped} stripped_loc={stripped_loc}")

    # Pick a default active action (Idle preferred) so the gltf exporter has
    # something obvious to wire as the slot-0 animation. Other actions still
    # export thanks to use_fake_user + export_animation_mode='ACTIONS'.
    if arm.animation_data is None:
        arm.animation_data_create()
    idle = next((a for a in new_actions if 'idle' in a.name.lower()), new_actions[0] if new_actions else None)
    if idle is not None:
        arm.animation_data.action = idle
        print(f"  active action on {arm.name}: '{idle.name}'")

    # Tear down everything we imported except the actions.
    for o in src_meshes + src_arms + src_empties:
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except Exception:
            pass

# --- 6.5. Resize all images to 32×32 ---
# The 3DCUO TextureSkinnedEffect uses a fixed 32×32 diffuse atlas (Synty
# palette convention). Textures of any other size silently fall back to
# white. For a default vendor mesh, baking all images down to 32×32 here
# means the prototype renderer can sample them. Result is pixelated but
# colored.
ATLAS = 32
for img in list(bpy.data.images):
    if img.size[0] == 0 or img.size[1] == 0:
        continue
    if img.size[0] == ATLAS and img.size[1] == ATLAS:
        continue
    print(f"  resize image '{img.name}' {img.size[0]}x{img.size[1]} -> {ATLAS}x{ATLAS}")
    img.scale(ATLAS, ATLAS)
    img.pack()  # re-embed scaled pixels into the .blend so export bakes them

# --- 6.99. Purge orphan datablocks. After deleting source armatures/meshes
#         we leave behind animation_data slots, empty datablocks, etc. Without
#         this purge the gltf exporter sometimes emits zombie actions or
#         duplicated images that bloat the GLB. Run twice — first pass cleans
#         up dependencies of the second pass's targets.
for _ in range(2):
    bpy.ops.outliner.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)

# --- 7. Export ---
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(
    filepath=out_path,
    export_format='GLB',
    use_selection=False,
    export_apply=False,
    export_animations=True,
    export_animation_mode='ACTIONS',  # one glTF animation per Blender action with fake_user
    export_skins=True,
    export_yup=True,         # convert Z-up Blender -> Y-up glTF
)
print(f"=== WROTE {out_path} ===")
