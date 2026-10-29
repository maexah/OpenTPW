#!/usr/bin/env python3
"""content_guard.py - refuses the game's files, so none is ever committed or shipped (AGENTS.md, "No game content").

    content_guard.py staged           every file staged for the next commit (added, copied, modified or renamed)
    content_guard.py paths <path>...  the named paths, as repo-relative names (nothing is read from disk)

A path is refused when its name is a file on the game's disc, its type is one the disc uses (the disc's data/ folder:
source/OpenTPW/Client/DiscData.txt) or one the rest of the install and the wads' members have (ALWAYS_REFUSED_TYPES:
the executables, libraries, installer and menu files, park files, extracted models, sprites and sounds), it is one of
the game's fonts (the TTF members of fonts.wad), it is in a `save/` folder at any depth, or it is in
content/ReferenceScreenshots/. EXEMPT names the few files of ours that share a name or a type
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
    "content/icon.ico",
    "source/OpenTPW/icon.ico",
    "source/OpenTPW/Client/DiscData.txt",
    "tools/harness/requirements.txt",
    "tools/weekly/README.txt",
}
# Types that are never ours although the disc's data/ list does not hold them (commit side only: tools/weekly/release.py
# imports disc(), and its zip legitimately holds .exe and .dll). Measured over every file of a game install, every
# member of its 312 wads and the repo's tracked files: none of these is a type a tracked file has, except .ico (EXEMPT).
ALWAYS_REFUSED_TYPES = set(
    "iso tpws ints "
    "exe dll sys drv vxd cpl icd vol inf ini bat hlp gid cnt cab ex_ ins pkg lib z fls lid bln tag bin new tmp "
    "ddb csp acv uc 118 x86 nec mpd sbk dls 016 256 apm tre img dal dah daw bmp pdf mpg cfg tcf ico "
    "rse hmp sgn esp tpc fpc lnd tct emt mp2 wav".split())
# The TTF members of fonts.wad (lower case). Roboto, ours, shares the type, so these are refused by name.
GAME_FONTS = set(
    "bigla___.ttf bigloa__.ttf clunab__.ttf cluna___.ttf eggitaoe.ttf garga___.ttf gargsai_.ttf gargsa__.ttf "
    "hauntaoe.ttf intrab__.ttf intra___.ttf krelab__.ttf krela___.ttf linupa__.ttf tannao__.ttf tanna___.ttf "
    "younia__.ttf".split())


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
    if path.startswith("save/") or "/save/" in path:   # case-sensitive: source/OpenTPW.Files/Formats/Save/ is ours
        return "it is in a save/ folder, the player's own"
    if path.startswith("content/ReferenceScreenshots/"):
        return "content/ReferenceScreenshots/ holds pictures of the game (attach a picture to the pull request instead)"
    base = path.rsplit("/", 1)[-1].lower()
    kind = base.rsplit(".", 1)[1] if "." in base else ""
    if base in names:
        return "a file of that name is on the game's disc"
    if base in GAME_FONTS:
        return "a file of that name is one of the game's fonts"
    if kind in ALWAYS_REFUSED_TYPES:
        return f".{kind} is a type the game's install, wads or saves hold, or a disc image"
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
