"""fsplayer.py - the private player made a Full Simulation one for a run, and put back after. exec() it AFTER parkrun.py.

    fs_on()     backs up the private player's gms.dat and puts its mode byte to 0 (gmsfile.put), so enter() loads the
                newest jungle file (the stock easymode.TPWI) as Full Simulation: the catalogue's 70 items, no Easy_ layer
    fs_profile(path)   the private player's gms.dat replaced by a copy of another profile (Alexah's Full Simulation one, or a
                copy lib/gmsfile.py put edited), for a run that must play as that player: their tickets, keys and records (Q706)
    fs_off()    in `finally`, after done(): the gms.dat put back byte for byte, and its mode printed

The original loads a file saved so only through a Full Simulation player (original/fsload.sh): an Instant Action one
crashes the load. Q550 (the recipe), Q580's rest (made a tool)."""
import sys as _sys_fs
_sys_fs.path.insert(0, paths.LIB)
import gmsfile
GMS = os.path.join(os.path.dirname(JUNGLE), "gms.dat")
_GMS_KEEP = open(GMS, "rb").read()
def fs_on():
    gmsfile.put(GMS, mode=0); print(f"  gms.dat put to Full Simulation: mode {gmsfile.read(GMS)['mode']}")
def fs_profile(path):
    shutil.copy(path, GMS); os.chmod(GMS, 0o644); g = gmsfile.read(GMS)
    print(f"  gms.dat replaced by {path}: mode {g['mode']} tickets {g['tickets']} keys {g['keys']}")
def fs_off():
    open(GMS, "wb").write(_GMS_KEEP); print(f"  gms.dat put back: mode {gmsfile.read(GMS)['mode']}")
