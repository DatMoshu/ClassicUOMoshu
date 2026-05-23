import sys, struct, json
path = sys.argv[1]
with open(path, 'rb') as f: data = f.read()
j_len, j_type = struct.unpack_from('<II', data, 12)
j = json.loads(data[20:20 + j_len].rstrip(b'\x00').decode('utf-8'))
print(f"meshes={len(j.get('meshes',[]))} nodes={len(j.get('nodes',[]))} skins={len(j.get('skins',[]))} animations={len(j.get('animations',[]))}")
print("nodes:")
for i, n in enumerate(j.get('nodes', [])):
    print(f"  [{i}] '{n.get('name','?')}' mesh={n.get('mesh')} skin={n.get('skin')} children={len(n.get('children',[]))} translation={n.get('translation')}")
print("meshes:")
for i, m in enumerate(j.get('meshes', [])):
    print(f"  [{i}] '{m.get('name','?')}' prims={len(m.get('primitives',[]))}")
