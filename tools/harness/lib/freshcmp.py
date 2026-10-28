"""freshcmp.py <mine> <ref.INTS> [file] - a park file's modules, and its world block's parts, against another's, byte by byte (Q783).

    mine   a park file (with `file`: a .TPWS/.INTS) or an inflated body (without)
    ref    a park file, the original's `restart.INTS` of a fresh park for instance (`original/freshfs.sh make`)

Prints, per module (ACTREC, WRLD, SPSC, ..., UI), `same <length>` or the two lengths with the count of differing ranges and the first six, then the
world's parts (header, controls, pool, tail, cells, head, each thing by model): where two files of the same kind differ it says where, so a rule that
makes a field is found and a field that varies per run (a seed, a clock reading, a roll) is told from one that does not. Other modules than the
fresh park's (a played park's thing list) read too: the thing sizes are by model, FileFormats `saves.md`, "Thing records". Imported by q783's runs
(`body_of`, `split`, `world_parts`, `ranges`). Run with review-codex/venv/bin/python -I."""
import sys, struct, zlib
TAGS = ['WRLD','SPSC','PART','MESS','CLOK','VANT','GSYS','RSYS','TRAK','FLYR','RSSE','KAME','COAS','ADVS','SOUN','CHTS','ADSC']
def body_of(path, is_file):
    d = open(path,'rb').read()
    return zlib.decompress(d[0x629:]) if is_file else d
def split(b):
    rec = struct.unpack_from('<I', b, 4)[0]
    mods = [('ACTREC', 0, 8+rec)]; at = 8+rec
    for t in TAGS:
        k = b.find(t[::-1].encode(), at)
        if k < 0: mods.append((t, at, len(b))); at=len(b); continue
        mods.append((t, at, k)); at = k+4
    mods.append(('UI', at, len(b)))
    return mods
def ranges(a, b):
    out=[]; i=0; n=min(len(a),len(b))
    while i<n:
        if a[i]!=b[i]:
            j=i
            while j<n and a[j]!=b[j]: j+=1
            out.append((i,j-i)); i=j
        else: i+=1
    return out
if __name__ == '__main__':
    mine = body_of(sys.argv[1], "file" in sys.argv[3:]); ref = body_of(sys.argv[2], True)
    mm = split(mine); rm = split(ref)
    for (n,s,e),(n2,s2,e2) in zip(mm, rm):
        a = mine[s:e]; r = ref[s2:e2]
        if a == r: print(f'{n:7} same {len(a)}'); continue
        d = ranges(a, r)
        print(f'{n:7} mine {len(a)} ref {len(r)} diff ranges {len(d)} bytes {sum(l for _,l in d)}  first {d[:6]}')

def world_parts(b, wrld):
    s, e = wrld
    parts = []
    at = s
    for name, n in [('header',64),('controls',150*32+6),('pool',640),('tail',76)]:
        parts.append((name, at, at+n)); at += n
    cstart = at
    # cells: walk statuses
    for i in range(16384):
        st=b[at]; at += 1+(52 if st&1 else 0)+(31 if st&2 else 0)+(10 if st&4 else 0)
    parts.append(('cells', cstart, at))
    parts.append(('head', at, at+4)); at += 4
    SIZES={1:533,3:1099,4:511,5:513,6:509,7:511,8:509,9:103,10:18,11:5846,12:16,13:73544,14:4190,15:99,16:300,17:16,19:937}
    i=0
    while at < e:
        nxt, model = struct.unpack_from('<II', b, at)
        parts.append((f'thing{i}-m{model}', at, at+SIZES[model])); at += SIZES[model]; i+=1
    return parts
def sub(mine, ref):
    mm = dict((n,(s,e)) for n,s,e in split(mine)); rm = dict((n,(s,e)) for n,s,e in split(ref))
    mp = world_parts(mine, mm['WRLD']); rp = world_parts(ref, rm['WRLD'])
    rd = {n:(s,e) for n,s,e in rp}
    for n,s,e in mp:
        key = n
        if key not in rd:
            # match by model
            model = n.split('-m')[-1] if '-m' in n else None
            cands = [k for k in rd if k.endswith('-m'+model)] if model else []
            if not cands: print(f'  {n:16} no counterpart'); continue
            key = cands[0]
        s2,e2 = rd[key]
        a = mine[s:e]; r = ref[s2:e2]
        if a == r: print(f'  {n:16} same {len(a)}'); continue
        d = ranges(a, r)
        print(f'  {n:16} mine {len(a)} ref {len(r)} diff ranges {len(d)} bytes {sum(l for _,l in d)} first {d[:8]}')
if __name__ == "__main__":
    print('WRLD parts:'); sub(mine, ref)
