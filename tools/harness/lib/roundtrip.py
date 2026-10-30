#!/usr/bin/env python3
"""roundtrip.py <outdir> <bin> (Q832): the lobby, then the stock Lost Kingdom park and back to the lobby, TRIPS times
(env, default 3), on the stock park (no player's slot clicked: a player's own park is richer and gives other figures).
At the lobby's start and after every park and every return it reads `mem gc` then `mem compact` (the compacted
VmRSS, VmHWM, the game's own `mem` lines) and `assets list`, saves the census as `<stage>.assets` and takes a frame
(`<stage>.png`); stages are lobby0, park1, lobby1, park2 ... Its end prints the texture count at each stage, the park loads'
totals and `terrain` and `objects` phases, the resident figures, and `assetdiff.py`'s account of what the first return
and the last left in the lobby that the lobby did not hold before. OFFSCREEN=1 runs on the private display as parkrun does.
What a trip leaves behind is the lobby's figures against `lobby0`'s: Q832 read 339 textures and 349 UI meshes kept, and
a creep of about one texture and 8 MB resident a trip (Q833). DUMP=1 collects a `dotnet-dump` heap dump at the third
return (or the last, if TRIPS is fewer) and keeps the .dmp under `~/.cache/dn-tmp` (`memdump.py`: DUMPCMD, DUMPOUT)."""
import os, sys, time, re
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "memrun"; TRIPS = int(os.environ.get("TRIPS", "3"))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
if os.environ.get("OFFSCREEN"): OFFSCREEN = True
exec(open(paths.LIB + "/parkrun.py").read())
import memmap, assetdiff, memdump
os.makedirs(OUT, exist_ok=True)
stages = {}
def stage(tag):
    reply("mem gc", 2, 0, 300)
    got = reply("mem compact", 3, 8, 300)
    lines_ = reply("assets list", 3, 0, 300)
    open(os.path.join(OUT, tag + ".assets"), "w").write("\n".join(lines_))
    rss = memmap.status(game.pid)
    stages[tag] = dict(textures=[l for l in got if "mem gpu" in l][0], vulkan=[l for l in got if "mem vulkan" in l][0], rss=rss["VmRSS"] / 1024,
                       hwm=rss["VmHWM"] / 1024, assets=assetdiff.census("\n".join(lines_)))
    print(f"STAGE {tag}: VmRSS {rss['VmRSS']/1024:.0f} MB VmHWM {rss['VmHWM']/1024:.0f} MB; " + stages[tag]["textures"][:120])
    print(f"VULKAN {tag}: " + stages[tag]["vulkan"])
def loads(): return [int(x) for x in re.findall(r"\[load\] jungle: (\d+) ms in total", logtext())]
def phase(name): return [int(x) for x in re.findall(r"\[load\] jungle: " + name + r" took (\d+) ms", logtext())]
def lobby_back(n):
    send("lobby"); 
    end = time.monotonic() + 90
    while time.monotonic() < end and len(re.findall(r"Loaded the lobby", logtext())) < n: time.sleep(0.5)
    time.sleep(8)
try:
    wait_for("Loaded the lobby", 150); attach(); time.sleep(6.0)
    stage("lobby0"); shot("lobby0")
    send("park jungle"); wait_for("Loaded jungle", 180); time.sleep(10.0)
    stage("park1"); shot("park1")
    for i in range(1, TRIPS + 1):
        lobby_back(i + 1); stage(f"lobby{i}"); shot(f"lobby{i}")
        if os.environ.get("DUMP") and i == min(3, TRIPS):
            print("DUMP kept:", memdump.dump(game.pid, OUT, keep=True))
        send("park jungle")
        end = time.monotonic() + 120
        while time.monotonic() < end and len(loads()) < i + 1: time.sleep(0.5)
        time.sleep(10.0); stage(f"park{i+1}"); shot(f"park{i+1}")
    lobby_back(TRIPS + 2); stage(f"lobby{TRIPS + 1}"); shot(f"lobby{TRIPS + 1}")
    t0, t1, t2 = stages["lobby0"], stages["lobby1"], stages[f"lobby{TRIPS + 1}"]
    n = lambda s: sum(c for (k, _), c in s["assets"].items() if k == "Texture")
    print("TEXTURES lobby0", n(t0), "park1", n(stages["park1"]), "lobby1", n(t1), f"park{TRIPS + 1}", n(stages[f"park{TRIPS + 1}"]), f"lobby{TRIPS + 1}", n(t2))
    print("LOADS", loads(), "TERRAIN", phase("terrain"), "OBJECTS", phase("objects"))
    print("RSS", {k: round(v["rss"]) for k, v in stages.items()}, "HWM", {k: round(v["hwm"]) for k, v in stages.items()})
    print("DIFF lobby0 -> lobby1"); sys.stdout.flush()
    os.system(f"python3 -I {os.path.dirname(os.path.abspath(__file__))}/assetdiff.py {OUT}/lobby0.assets {OUT}/lobby1.assets 3 | tee -a {OUT}/confirm.txt")
    os.system(f"python3 -I {os.path.dirname(os.path.abspath(__file__))}/assetdiff.py {OUT}/lobby1.assets {OUT}/lobby{TRIPS + 1}.assets 3 | tee -a {OUT}/confirm.txt")
finally:
    done()
