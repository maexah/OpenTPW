#!/usr/bin/env python3
"""memrun.py <outdir> <bin> stock|fs (Q954): where the game's memory sits in a park, by the kernel and by the game.
`stock` enters the stock Instant Action Lost Kingdom park; `fs` loads a copy of a Full Simulation park file (env
OPENTPW_FULLSIM_PARK, a .TPWS made by the original; a Full Simulation player's profile from OPENTPW_FULLSIM_PLAYER). At park
entry and 30 s on it prints `memmap.report` (resident split, the largest mappings, the DRM video memory) and the game's own
`mem gc`, `mem compact` and `stats` replies, then a frame (park.png). Environment: OFFSCREEN=1 runs on the private display
under lavapipe (where the process, not the video memory, holds the device's bytes); PATHS=1 lays six path cells (60,15) to (62,16) after the 30 s and reports again (Q829); LEAVE=1 goes back to the lobby after and
reports again (what a park leaves behind); DUMP=1 collects a `dotnet-dump` heap dump and writes `dumpheap` output (DUMPCMD,
`|` between commands, default `dumpheap -stat`) to DUMPOUT (default heapstat.txt) in the outdir, the dump itself removed;
OPENTPW_DOTNET_DUMP names the tool. The run's last line is `game memory peak` (gamedrive.py)."""
import os, sys, shutil, time, subprocess
OUT = sys.argv[1]; BIN = sys.argv[2]; MODE = sys.argv[3]; NAME = "memrun"
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
if os.environ.get("OFFSCREEN"): OFFSCREEN = True
exec(open(paths.LIB + "/parkrun.py").read())
exec(open(paths.LIB + "/fsplayer.py").read())
import memmap
COPY = os.path.join(JUNGLE, "memrun-fs.TPWS")

def stage(tag):
    memmap.report(game.pid, tag, out=print)   # parkrun's print: the block lands in confirm.txt
    for c in ("mem gc", "mem compact", "stats"):
        for l in reply(c, 1.5, 0): print("  " + l)

def dump():
    tool = os.environ.get("OPENTPW_DOTNET_DUMP", os.path.expanduser("~/.dotnet/tools/dotnet-dump"))
    env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()["DOTNET_ROOT"])
    file = os.path.join(os.environ.get("TMPDIR", "/tmp"), f"memrun-{game.pid}.dmp")
    r = subprocess.run([tool, "collect", "-p", str(game.pid), "--type", "Heap", "-o", file], capture_output=True, text=True, env=env)
    print("dump:", (r.stdout + r.stderr)[-200:].strip())
    cmds = os.environ.get("DUMPCMD", "dumpheap -stat").split("|")
    r = subprocess.run([tool, "analyze", file, *sum([["-c", c] for c in cmds], []), "-c", "exit"], capture_output=True, text=True, env=env)
    open(os.path.join(OUT, os.environ.get("DUMPOUT", "heapstat.txt")), "w").write(r.stdout + r.stderr)
    os.remove(file)

try:
    if MODE == "fs":
        fs_profile(os.path.join(paths.need("OPENTPW_FULLSIM_PLAYER"), "gms.dat"))
        shutil.copy(paths.need("OPENTPW_FULLSIM_PARK"), COPY); os.utime(COPY)
    enter()
    stage("park entered")
    time.sleep(30)
    stage("park +30 s")
    shot("park")
    if os.environ.get("PATHS"):
        for x_, y_ in [(60, 15), (61, 15), (62, 15), (60, 16), (61, 16), (62, 16)]:
            for l in reply(f"path {x_} {y_}", 1.5, 0): print("  " + l)
        time.sleep(2)
        shot("paths")
        for l in re.findall(r".*ground tiles.*", logtext()): print("  LOG " + l)
        stage("after paths")
    if os.environ.get("DUMP"):
        reply("mem compact", 1.5, 0)
        dump()
    if os.environ.get("LEAVE"):
        send("lobby"); wait_for("Loaded the lobby", 60); time.sleep(8)
        stage("lobby again (left the park)")
finally:
    done()
    if MODE == "fs":
        fs_off()
        if os.path.exists(COPY): os.remove(COPY); print("removed", COPY)
