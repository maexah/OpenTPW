#!/usr/bin/env python3
"""gamefolder.py - a private game folder whose data differs from the real game's in the files named, for a run that
needs a changed data file (a balance key in Advisor.sam) without touching the real game folder.

    import gamefolder; gamefolder.make(DEST, {"data/Advisor/Advisor.sam": lambda text: text.replace(a, b)})

DEST gets what parkrun.py's private folder has (links to the real game's entries, a save/ copied from the private game folder's, paths.PRIVATE_GAME),
and each changed file's folders are real folders of links down to the file, which is a changed COPY. A change takes
and answers the file's text (latin-1). An existing DEST is rebuilt, its save/ kept. Hand DEST to parkrun.py as
GAME_FOLDER. `gamefolder.py DEST REL OLD NEW` does one replacement from the shell and fails if OLD is not there once.

`gamefolder.py lock DEST` makes DEST (as `make( DEST, {} )` does) with its save/ read-only at every level and the first
player's `restart.INTS` taken out (Q982: `lock( DEST )`); `unlock( DEST )` puts the write bit back, which removing the folder needs.
Since Q984 the game does not stay in a `save/` it cannot write: its saves go in the player's data folder (`$XDG_DATA_HOME/OpenTPW/save`,
`~/.local/share/OpenTPW/save`), and a locked `save/` that holds players opens the one SAVES GO IN box. **Set XDG_DATA_HOME to a folder of the
run's own first** (and make that folder read-only too for Q982's boxes, which fire only when the fallback cannot be written); `lock()` and
`fresh()` print a warning, and refuse with `GAMEFOLDER_ALLOW_REAL_HOME=1` unset, when it is not set (AGENTS.md rule 12).

`gamefolder.py fresh DEST` makes DEST (`fresh( DEST )`) as a stock Program Files install stands for a player whose account may not write
there: the game's entries linked, **no save/** and the folder itself read-only (Q984: the game then keeps its saves in the player's data folder, so set
XDG_DATA_HOME to a folder of the run's first, and `LEDGER` for parkrun.py); `unfresh( DEST )` puts the write bit back, which removing it needs.

`gamefolder.py private` makes paths.PRIVATE_GAME itself, once per computer: links to the real game's entries and an
empty save/ of its own. The runs click the first player slot and read save/users/1ref, so start OpenTPW once on that
folder and make a player named "ref" in the first slot (README.md, "Setting up")."""
import os, shutil, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import paths
TEMPLATE = paths.PRIVATE_GAME
def _links(real, dest, keep=()):
    os.makedirs(dest, exist_ok=True)
    for entry in os.listdir(real):
        if entry in keep: continue
        at = os.path.join(dest, entry)
        if os.path.islink(at): os.remove(at)
        elif os.path.isdir(at): shutil.rmtree(at)
        elif os.path.exists(at): os.remove(at)
        os.symlink(os.path.join(real, entry), at)
def private():
    REAL = paths.game(); dest = TEMPLATE; _links(REAL, dest, keep=("save",))
    os.makedirs(os.path.join(dest, "save"), exist_ok=True)   # its own, empty: the player's own save/ is never copied
    return dest
def make(dest, changes):
    REAL = paths.game(); dest = os.path.expanduser(dest); _links(REAL, dest, keep=("save",))
    if not os.path.isdir(os.path.join(dest, "save")): shutil.copytree(os.path.join(TEMPLATE, "save"), os.path.join(dest, "save"))
    for rel, change in changes.items():
        parts = rel.split("/"); real, at = REAL, dest
        for part in parts[:-1]:
            real = os.path.join(real, part); at = os.path.join(at, part)
            if os.path.islink(at): os.remove(at); _links(real, at)
        real = os.path.join(real, parts[-1]); at = os.path.join(at, parts[-1])
        assert os.path.isfile(real), real
        if os.path.lexists(at): os.remove(at)
        text = open(real, encoding="latin-1", newline="").read(); new = change(text); assert new != text, rel + ": the change changed nothing"
        open(at, "w", encoding="latin-1", newline="").write(new)
    return dest
def _home_check():
    if os.environ.get("XDG_DATA_HOME"): return
    print("gamefolder: XDG_DATA_HOME is not set, so a game started on this folder would write saves in the real ~/.local/share/OpenTPW/save (Q984)")
    if os.environ.get("GAMEFOLDER_ALLOW_REAL_HOME") != "1": raise SystemExit("gamefolder: set XDG_DATA_HOME to a folder of the run's own (or GAMEFOLDER_ALLOW_REAL_HOME=1)")
def lock(dest, keep_restart=False):
    _home_check()
    dest = make(dest, {}); jungle = os.path.join(dest, "save/users/1ref/jungle")
    if not keep_restart and os.path.exists(os.path.join(jungle, "restart.INTS")): os.remove(os.path.join(jungle, "restart.INTS"))
    for root, _, files in os.walk(os.path.join(dest, "save")):
        for name in files: os.chmod(os.path.join(root, name), 0o444)
        os.chmod(root, 0o555)
    return dest
def unlock(dest):
    for root, _, files in os.walk(os.path.join(os.path.expanduser(dest), "save")):
        os.chmod(root, 0o755)
        for name in files: os.chmod(os.path.join(root, name), 0o644)
def fresh(dest):
    _home_check()
    dest = make(dest, {}); save = os.path.join(dest, "save")
    if os.path.isdir(save): unlock(dest); shutil.rmtree(save)
    os.chmod(dest, 0o555); return dest
def unfresh(dest):
    os.chmod(os.path.expanduser(dest), 0o755)
def once(old, new):
    def change(text):
        assert text.count(old) == 1, (old, text.count(old)); return text.replace(old, new)
    return change
if __name__ == "__main__" and sys.argv[1:] == ["private"]:
    print(private())
elif __name__ == "__main__" and sys.argv[1:2] == ["fresh"]:
    print(fresh(sys.argv[2]))
elif __name__ == "__main__" and sys.argv[1:2] == ["lock"]:
    print(lock(sys.argv[2]))
elif __name__ == "__main__":
    print(make(sys.argv[1], {sys.argv[2]: once(sys.argv[3], sys.argv[4])}))
