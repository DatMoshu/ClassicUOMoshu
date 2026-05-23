"""Print mesh world bbox + armature object scale for a GLB."""
import bpy, sys, mathutils
argv = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.gltf(filepath=argv[0])
mn = mathutils.Vector(( 1e30,)*3); mx = mathutils.Vector((-1e30,)*3)
for m in [o for o in bpy.data.objects if o.type == 'MESH']:
    for v in m.bound_box:
        wv = m.matrix_world @ mathutils.Vector(v)
        mn = mathutils.Vector((min(mn[i], wv[i]) for i in range(3)))
        mx = mathutils.Vector((max(mx[i], wv[i]) for i in range(3)))
sz = mx - mn
print(f"FILE: {argv[0]}")
print(f"  world bbox  min={tuple(mn)}")
print(f"  world bbox  max={tuple(mx)}")
print(f"  world size  {tuple(sz)}  (height Z={sz.z:.4f})")
for arm in [o for o in bpy.data.objects if o.type == 'ARMATURE']:
    print(f"  armature {arm.name} obj.scale={tuple(arm.scale)} obj.location={tuple(arm.location)}")
    # bone head_local in armature-local units
    for b in arm.data.bones:
        if 'Hips' in b.name:
            print(f"  bone '{b.name}' head_local={tuple(b.head_local)} length={b.length:.4f}")
            break
