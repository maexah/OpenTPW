"""uitypes.py - every item's Info.WhichUIType across the four themes: the folder's (rides 0, upgrades 0, shops 1, sideshow 2,
features 3) unless the item's own .sam sets one (the nine of 4 in each theme, and burger's 1). Cached in uitypes.json beside this file; types() answers {Info.Id: kind}, the reader parkfile and q390/sweep.py use to count a
file's rides (Q390, FUN_004c8070). Run alone it prints the kinds' counts."""
import json, os, subprocess
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
HERE = os.path.dirname(os.path.abspath(__file__)); CACHE = os.path.join(HERE, 'uitypes.json')
W = paths.tool("wadcat"); G = os.path.join(paths.game(), "data/levels")
FOLDER = {'rides': 0, 'upgrades': 0, 'shops': 1, 'sideshow': 2, 'features': 3}
def build():
    env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()['DOTNET_ROOT']); out = {}
    for th in ('jungle', 'fantasy', 'hallow', 'space'):
        for sub, t in FOLDER.items():
            d = f'{G}/{th}/{sub}'
            if not os.path.isdir(d): continue
            for w in sorted(os.listdir(d)):
                if not w.lower().endswith('.wad'): continue
                text = subprocess.run([W, '--cat', '.sam', f'{d}/{w}'], capture_output=True, text=True, env=env).stdout
                cur = None
                for line in text.replace('\r', '').split('\n'):
                    f = line.split()
                    if len(f) < 2: continue
                    if f[0] == 'Info.Id': cur = int(f[1]); out[str(cur)] = t
                    elif f[0] == 'Info.WhichUIType' and cur is not None: out[str(cur)] = int(f[1])
    json.dump(out, open(CACHE, 'w')); return out
def types():
    t = json.load(open(CACHE)) if os.path.exists(CACHE) else build()
    return {int(k): v for k, v in t.items()}
if __name__ == '__main__':
    import collections; t = types(); print(len(t), collections.Counter(t.values()))
