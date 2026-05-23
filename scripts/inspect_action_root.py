"""For each action in a GLB, report Hips translation/rotation channel ranges."""
import bpy, sys
argv = sys.argv[sys.argv.index("--") + 1:]
path = argv[0]
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.gltf(filepath=path)

print(f"=== {path} ===")
for arm in [o for o in bpy.data.objects if o.type == 'ARMATURE']:
    print(f"armature {arm.name}: world={tuple(arm.location)}")
    for b in arm.data.bones:
        if 'Hips' in b.name or 'rootJoint' in b.name:
            print(f"  bone '{b.name}' head_local={tuple(b.head_local)} tail_local={tuple(b.tail_local)}")

for a in bpy.data.actions:
    hips_loc = []
    hips_rot = []
    other_loc = 0
    for fc in a.fcurves:
        dp = fc.data_path
        if 'Hips' in dp:
            if 'location' in dp:
                vmin = min((kp.co.y for kp in fc.keyframe_points), default=0)
                vmax = max((kp.co.y for kp in fc.keyframe_points), default=0)
                hips_loc.append((dp.split('"')[-1] if '"' in dp else dp, fc.array_index, vmin, vmax))
            elif 'rotation' in dp:
                hips_rot.append(fc.array_index)
        elif 'location' in dp:
            other_loc += 1
    print(f"action '{a.name}' fcurves={len(a.fcurves)}")
    print(f"  hips_loc channels: {hips_loc}")
    print(f"  hips_rot indices: {sorted(set(hips_rot))}")
    print(f"  other-bone .location channels: {other_loc}")
