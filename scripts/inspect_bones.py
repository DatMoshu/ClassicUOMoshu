"""Print bone names of every armature found across the input files."""
import bpy, sys

argv = sys.argv[sys.argv.index("--") + 1:]

bpy.ops.wm.read_homefile(use_empty=True)
for path in argv:
    print(f"=== {path} ===")
    if path.lower().endswith(".glb") or path.lower().endswith(".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path)
    for arm in [o for o in bpy.data.objects if o.type == 'ARMATURE']:
        print(f"  armature '{arm.name}' bones={len(arm.data.bones)}")
        for b in arm.data.bones:
            print(f"    - {b.name}")
    for a in bpy.data.actions:
        print(f"  action '{a.name}' frames={a.frame_range}")
        # Distinct bone names referenced by F-curves
        bones = set()
        for fc in a.fcurves:
            dp = fc.data_path
            if 'pose.bones[' in dp:
                start = dp.find('"') + 1
                end = dp.find('"', start)
                bones.add(dp[start:end])
        print(f"    F-curve bones ({len(bones)}): {sorted(bones)[:20]}...")
    bpy.ops.wm.read_homefile(use_empty=True)
