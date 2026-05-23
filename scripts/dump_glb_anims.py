"""Dump animation list of a GLB."""
import sys, struct, json
path = sys.argv[1]
with open(path, 'rb') as f: data = f.read()
j_len, j_type = struct.unpack_from('<II', data, 12)
j = json.loads(data[20:20 + j_len].rstrip(b'\x00').decode('utf-8'))
print(f"{path}")
print(f"  animations: {len(j.get('animations', []))}")
for i, a in enumerate(j.get('animations', [])):
    nch = len(a.get('channels', []))
    print(f"    [{i}] name='{a.get('name')}' channels={nch}")
