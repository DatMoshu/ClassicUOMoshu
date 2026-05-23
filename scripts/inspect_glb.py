"""Background Blender script: import a GLB, report rig/mesh/origin/scale info."""
import bpy
import sys

argv = sys.argv
argv = argv[argv.index("--") + 1:]
glb_path = argv[0]

bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb_path)

print("=== GLB INSPECTION ===")
print(f"File: {glb_path}")

armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
meshes    = [o for o in bpy.data.objects if o.type == 'MESH']
empties   = [o for o in bpy.data.objects if o.type == 'EMPTY']

print(f"Armatures: {len(armatures)} | Meshes: {len(meshes)} | Empties: {len(empties)}")

for o in bpy.data.objects:
    print(f"  obj '{o.name}' type={o.type} loc={tuple(o.location)} rot_euler={tuple(o.rotation_euler)} scale={tuple(o.scale)} parent={o.parent.name if o.parent else None}")

for arm in armatures:
    print(f"  armature '{arm.name}' bones={len(arm.data.bones)}")
    roots = [b.name for b in arm.data.bones if b.parent is None]
    print(f"    root bones: {roots}")

# Combined bbox in world space
import mathutils
mn = mathutils.Vector(( 1e30,  1e30,  1e30))
mx = mathutils.Vector((-1e30, -1e30, -1e30))
for m in meshes:
    for v in m.bound_box:
        wv = m.matrix_world @ mathutils.Vector(v)
        mn.x = min(mn.x, wv.x); mn.y = min(mn.y, wv.y); mn.z = min(mn.z, wv.z)
        mx.x = max(mx.x, wv.x); mx.y = max(mx.y, wv.y); mx.z = max(mx.z, wv.z)
size = mx - mn
print(f"World bbox min={tuple(mn)}")
print(f"World bbox max={tuple(mx)}")
print(f"World size    ={tuple(size)} (height Z={size.z:.3f})")

# Animations
print(f"Actions in file: {len(bpy.data.actions)}")
for a in bpy.data.actions:
    print(f"  action '{a.name}' frame_range={tuple(a.frame_range)}")
print("=== DONE ===")
