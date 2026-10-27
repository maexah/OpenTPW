#!/usr/bin/env python3
"""itemcosts.py [theme...] - every item's three Upgrades[k].CostOfUpgrade from the game's own .sam files (wadcat --cat
.sam over each theme's features, shops, sideshow, rides and upgrades wads), as {Info.Id: [tier 0, 1, 2]}; a tier the
item's file does not declare is 0 (every one of the 265 items declares its tier 0; no Easy_/Online_ override declares a
cost). Ids are unique across the four themes but 100-102, every tier 0 there. Cached in itemcosts.json beside this file. prices() answers the table; parkfile.park_value(p, prices()) sums a
park's value with it (Q388). Run alone it prints the table for the themes named (all four by default)."""
import json, os, subprocess, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
HERE = os.path.dirname(os.path.abspath(__file__)); CACHE = os.path.join(HERE, 'itemcosts.json')
WADCAT = paths.tool("wadcat"); GAME = paths.game()
THEMES = ('jungle', 'fantasy', 'hallow', 'space')
def build():
    env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()['DOTNET_ROOT']); out = {}
    for th in THEMES:
        d = f'{GAME}/data/levels/{th}'
        for sub in ('features', 'shops', 'sideshow', 'rides', 'upgrades'):
            if not os.path.isdir(f'{d}/{sub}'): continue
            for w in sorted(os.listdir(f'{d}/{sub}')):
                if not w.lower().endswith('.wad'): continue
                text = subprocess.run([WADCAT, '--cat', '.sam', f'{d}/{sub}/{w}'], capture_output=True, text=True, env=env).stdout
                cur = None
                for line in text.replace('\r', '').split('\n'):
                    f = line.split()
                    if len(f) < 2: continue
                    if f[0] == 'Info.Id': cur = int(f[1]); out.setdefault(str(cur), [0, 0, 0])
                    elif cur is not None and f[0].startswith('Upgrades[') and f[0].endswith('].CostOfUpgrade'): out[str(cur)][int(f[0][9])] = int(f[1])
    json.dump(out, open(CACHE, 'w')); return out
def prices():
    t = json.load(open(CACHE)) if os.path.exists(CACHE) else build()
    return {int(k): v for k, v in t.items()}
if __name__ == '__main__':
    t = prices(); print(len(t), 'items'); [print(k, v) for k, v in sorted(t.items())]
