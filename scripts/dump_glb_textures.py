"""Report what textures + materials a GLB contains."""
import sys, struct, json

path = sys.argv[1]
with open(path, 'rb') as f:
    data = f.read()

magic, ver, total = struct.unpack_from('<III', data, 0)
assert magic == 0x46546C67, f"not a GLB: {hex(magic)}"
print(f"GLB v{ver} size={total} bytes (file={len(data)})")

# Read JSON chunk
j_len, j_type = struct.unpack_from('<II', data, 12)
assert j_type == 0x4E4F534A, "first chunk must be JSON"
j_str = data[20:20 + j_len].rstrip(b'\x00').decode('utf-8')
j = json.loads(j_str)

print(f"meshes: {len(j.get('meshes',[]))}")
print(f"materials: {len(j.get('materials',[]))}")
print(f"textures: {len(j.get('textures',[]))}")
print(f"images: {len(j.get('images',[]))}")
print(f"samplers: {len(j.get('samplers',[]))}")

for i, m in enumerate(j.get('materials', [])):
    pbr = m.get('pbrMetallicRoughness', {})
    bct = pbr.get('baseColorTexture')
    bcf = pbr.get('baseColorFactor')
    print(f"  mat[{i}] name={m.get('name')!r} bcTex={bct} bcFactor={bcf}")

for i, im in enumerate(j.get('images', [])):
    print(f"  img[{i}] name={im.get('name')!r} mime={im.get('mimeType')} bvIdx={im.get('bufferView')} uri={im.get('uri','<embedded>')[:80]}")

# Binary chunk size
if len(data) > 20 + j_len:
    b_len, b_type = struct.unpack_from('<II', data, 20 + j_len)
    print(f"BIN chunk: {b_len} bytes type=0x{b_type:08x}")
