#!/usr/bin/env python3
"""modelchannels.py [-v] <park file>... - the ride system module walked with each record's channel count from the
original's own rule, no search (Q814; docs/exe/saves.md, "How many channels a saved model has"). The loader
(RideSystem_LoadModels, 0x004647a0) reads as many channels as the model it has just made holds, the model type's +0xe,
and a type holds the count it was registered with (FUN_004629d0's seventh argument): a catalogue item's own model its
UsageInfo.NumSimultAnims (0x00414175, 0x004141c7; none or nought is one, 0x00462ad5), a coaster's pylon type five
(0x0042fcbf, 0x0042fd36; its id is coaster.sam's asPylonControls[n].uiPylonTypeUniqueID), and every other type one (a
coaster's car, an item's extra mesh, a queue, hoarding or track piece). counts() is that table from the game's .sam
files, cached in modelchannels.json under the harness work folder (paths.work); walk(p) answers (closed, [(slot, at, item, channels)]).
Run alone it prints, for each file, whether the walk closed on the module's end, the records by item and channel count,
and how many records rsys.py's searched count reads otherwise."""
import collections, json, os, subprocess, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
from pk import Park
CACHE = paths.work('modelchannels.json')
WADCAT = paths.tool("wadcat"); GAME = paths.game()
def build():
    """{id: channels} for every id a .sam under the game's data folder names: each Info.Id with its NumSimultAnims, each
    pylon type with 5, each car type with 1."""
    env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()['DOTNET_ROOT']); out = {}
    wads = sorted(os.path.join(d, f) for d, _, fs in os.walk(f'{GAME}/data') for f in fs if f.lower().endswith('.wad'))
    for w in wads:
        text = subprocess.run([WADCAT, '--cat', '.sam', w], capture_output=True, text=True, env=env).stdout
        item = None; anims = None
        def close():
            if item is not None: out[str(item)] = max(anims or 1, 1)
        for line in text.replace('\r', '').split('\n'):
            f = line.split()
            if len(f) < 2: continue
            if f[0] == 'Info.Id': close(); item, anims = int(f[1]), None
            elif f[0] == 'UsageInfo.NumSimultAnims': anims = int(f[1])
            elif f[0].startswith('asPylonControls[') and f[0].endswith('].uiPylonTypeUniqueID'): out[str(int(f[1]))] = 5
            elif f[0].startswith('asCarTypes[') and f[0].endswith('].uiCarTypeUniqueID'): out.setdefault(str(int(f[1])), 1)
        close()
    json.dump(out, open(CACHE, 'w'), sort_keys=True); return out
def counts():
    t = json.load(open(CACHE)) if os.path.exists(CACHE) else build()
    return {int(k): v for k, v in t.items()}
def walk(p, table=None):
    """(closed, records): each present record's (slot, offset, item, channels), the count from table (counts() by
    default), 1 for an id it does not name."""
    table = counts() if table is None else table
    b = p.b; g = b.index(b'SYSG', p.messend) + 4; n = p.u32(g); end = g + 4 + n
    slots = p.u32(g + 4) + p.u32(g + 8); at = g + 16; out = []
    for slot in range(slots):
        if at >= end: break
        if b[at] != 1: at += 1; continue
        item = p.u32(at + 1); ch = table.get(item, 1)
        out.append((slot, at, item, ch))
        at += 0x37 + p.u16(at + 0x2d) * 8 + p.u16(at + 0x2b) * 4 + ch * 44
    return at == end and b[end:end + 4] == b'SYSR', out
if __name__ == '__main__':
    import rsys
    verbose = '-v' in sys.argv; table = counts()
    for path in [a for a in sys.argv[1:] if a != '-v']:
        p = Park(path); closed, recs = walk(p, table)
        _, _, searched = rsys.walk(p); differ = None
        if searched is not None:
            got = {at: ch for _, at, _, ch in recs}
            differ = sum(1 for r in searched if r and got.get(r[0]) != r[1])
        by = collections.defaultdict(collections.Counter)
        for _, _, item, ch in recs: by[item][ch] += 1
        print(f'{path}: closed {closed}, {len(recs)} records, rsys differs on {differ}')
        if verbose:
            for item in sorted(by): print(f'  item {item:5}: ' + ', '.join(f'{k} of {c}' for c, k in sorted(by[item].items())))
