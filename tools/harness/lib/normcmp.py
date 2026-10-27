"""normcmp.py (Q657): the original's live advisor normals (advlight.py --json) against this game's model file: the +0x64 table
(entry i for vertex-order entry i) and the per-vertex normals OpenTPW computes.  normcmp.py <orig.json> <ours.json>"""
import json, sys, numpy as np
orig=json.load(open(sys.argv[1])); ours=json.load(open(sys.argv[2]))
byname={m['name'].lower():m for m in ours}
for m in orig['meshes']:
    o=byname.get(m['name'].lower())
    if not o: print(m['name'],'missing'); continue
    N=np.array(m['normals']); T=np.array(o['table']) if o['table'] else np.zeros((0,3)); C=np.array(o['computed'])
    s=f"{m['name'][:16]:16} orig {len(N):4} order {o['order']:4} table {len(T):4} computed {len(C):4}"
    k=min(len(N),len(T))
    if k: s+=f"  table-orig max {np.abs(N[:k]-T[:k]).max():.4f}"
    k=min(len(N),len(C))
    if k:
        ang=np.degrees(np.arccos(np.clip((N[:k]*C[:k]).sum(1)/np.maximum(1e-9,np.linalg.norm(N[:k],axis=1)*np.linalg.norm(C[:k],axis=1)),-1,1)))
        s+=f"  computed-orig angle median {np.median(ang):.1f} max {ang.max():.1f}"
    print(s)
