#!/usr/bin/env python3
"""content_guard.py - refuses the game's files, so none is ever committed or shipped (AGENTS.md, "No game content").

    content_guard.py staged           every file staged for the next commit (added, copied, modified or renamed)
    content_guard.py paths <path>...  the named paths, as repo-relative names (nothing is read from disk)

A path is refused when its name is a file on the game's disc, its type is one the disc uses, it is in the player's
`save/` folder or a saved park (.TPWS), it is a disc image (.iso), or it is in content/ReferenceScreenshots/. The disc's
own file list is source/OpenTPW/Client/DiscData.txt. EXEMPT names the few files of ours that share a name or a type
with the disc's; add to it only for a file that is ours. The weekly build's zip is held to the same list of names and
types by tools/weekly/release.py (docs/WORKFLOW.md, "Weekly builds"). The pre-commit hook in tools/hooks runs
`staged`; run it by hand before a pull request. Exit 1 when anything is refused. Python 3.8 or newer, nothing to install."""
import os, subprocess, sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
DISC_LIST = os.path.join(REPO, "source", "OpenTPW", "Client", "DiscData.txt")

# Files of ours whose name or type the disc also has (.txt, .tga): not the game's.
EXEMPT = {
    "content/fonts/LICENSE.txt",
    "content/icon.tga",
    "content/icon-sm.tga",
    "source/OpenTPW/Client/DiscData.txt",
    "tools/harness/requirements.txt",
    "tools/weekly/README.txt",
}
# Types that are never ours although the disc's list does not hold them: a disc image and a saved park.
ALWAYS_REFUSED_TYPES = {"iso", "tpws"}


def disc():
    """The disc's file names (lower case) and file types, from the list the game itself counts against."""
    names, types = set(), set()
    for line in open(DISC_LIST, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        base = line.rsplit("/", 1)[-1].lower()
        names.add(base)
        if "." in base:
            types.add(base.rsplit(".", 1)[1])
    return names, types


def refusal(path, names, types):
    """Why a repo-relative path may not be committed, or None."""
    path = path.replace(os.sep, "/")
    if path in EXEMPT:
        return None
    if path == "save" or path.startswith("save/"):
        return "it is in save/, the player's own folder"
    if path.startswith("content/ReferenceScreenshots/"):
        return "content/ReferenceScreenshots/ holds pictures of the game (attach a picture to the pull request instead)"
    base = path.rsplit("/", 1)[-1].lower()
    kind = base.rsplit(".", 1)[1] if "." in base else ""
    if base in names:
        return "a file of that name is on the game's disc"
    if kind in ALWAYS_REFUSED_TYPES:
        return f".{kind} is a disc image or a saved park"
    if kind in types:
        return f".{kind} is one of the game's file types"
    return None


def staged():
    out = subprocess.check_output(["git", "-C", REPO, "diff", "--cached", "--name-only", "--diff-filter=ACMR", "-z"])
    return [p for p in out.decode("utf-8").split("\0") if p]


def run(paths):
    names, types = disc()
    bad = [(p, why) for p in paths for why in [refusal(p, names, types)] if why]
    print(f"content guard: {len(paths)} path(s) checked, {len(bad)} refused")
    for p, why in bad:
        print(f"  REFUSED {p}: {why}")
    if bad:
        print("  Nothing from the game's files may be committed. If a file is yours (a note, a script), give it another "
              "name or type (.md for text); if the guard is wrong about it, say so in the pull request.")
    return not bad


if __name__ == "__main__":
    a = sys.argv[1:]
    if a == ["staged"]:
        sys.exit(0 if run(staged()) else 1)
    if len(a) >= 2 and a[0] == "paths":
        sys.exit(0 if run(a[1:]) else 1)
    sys.exit(__doc__)
