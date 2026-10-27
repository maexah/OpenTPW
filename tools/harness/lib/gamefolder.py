#!/usr/bin/env python3
"""gamefolder.py - a private game folder whose data differs from the real game's in the files named, for a run that
needs a changed data file (a balance key in Advisor.sam) without touching the real game folder.

    import gamefolder; gamefolder.make(DEST, {"data/Advisor/Advisor.sam": lambda text: text.replace(a, b)})

DEST gets what parkrun.py's private folder has (links to the real game's entries, a save/ copied from the private game folder's, paths.PRIVATE_GAME),
and each changed file's folders are real folders of links down to the file, which is a changed COPY. A change takes
and answers the file's text (latin-1). An existing DEST is rebuilt, its save/ kept. Hand DEST to parkrun.py as
GAME_FOLDER. `gamefolder.py DEST REL OLD NEW` does one replacement from the shell and fails if OLD is not there once.

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
def once(old, new):
    def change(text):
        assert text.count(old) == 1, (old, text.count(old)); return text.replace(old, new)
    return change
if __name__ == "__main__" and sys.argv[1:] == ["private"]:
    print(private())
elif __name__ == "__main__":
    print(make(sys.argv[1], {sys.argv[2]: once(sys.argv[3], sys.argv[4])}))
