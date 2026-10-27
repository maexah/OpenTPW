# The test harnesses

The scripts the project uses to prove a change in the running game: launch OpenTPW with its debug console, click
and type through XTEST, grab frames, save and load parks, read park files, put bugs back on purpose, and drive the
original game under Proton beside it. Each file's docstring (or its opening comment) is its manual;
[`docs/TOOLING.md`](../../docs/TOOLING.md), "The tools every item reuses", lists what each one is for, and its
"Recipes" hold the clicks and commands found by trial.

| Folder | What it is |
|---|---|
| `lib/` | Shared by every task: the run scaffold (`parkrun.py`, which `exec`s `gamedrive.py`), the park-file reader (`parkfile.py`), `mutate.py`, `stryker.sh`, the frame and sound measures, and `paths.py` / `paths.sh`. |
| `original/` | Drives the original game under Proton: `original.sh` starts and stops it, `tpwmem.py` reads its memory at Ghidra addresses, `gmove.py` moves its pointer, `record.sh` films it, `watch.sh` finds who writes an address. |
| `dotnet/` | Three small C# readers built on `OpenTPW.Files`: `wadcat` (a wad's entries), `wct2raw` (a texture to raw RGBA), `rselisting` (a ride script's listing). |

Everything here runs on **Linux with X11** (or XWayland). Nothing here is part of the build or the tests.

## Setting up

1. `pip install -r tools/harness/requirements.txt` (a virtual environment is fine; use its `python`).
2. Name the game and, if you like, where runs write:

   ```sh
   export OPENTPW_GAME_PATH="/path/to/Sim Theme Park"
   export OPENTPW_HARNESS_WORK=~/.cache/opentpw-harness     # the default; evidence, logs and private game folders go here
   ```

   `lib/paths.py`'s docstring lists every variable the scripts read (the original's install, a Ghidra client, an
   Xvfb for off-screen runs, a Python with unicorn). A script that needs one you have not set says so and stops.
3. Build the C# readers if you will use them: `dotnet build tools/harness/dotnet/wadcat` (and `wct2raw`, `rselisting`).
4. Make the private game folder, once. Runs never touch your real `save/`: they play in a folder of links to your
   game's files with a `save/` of its own.

   ```sh
   python3 tools/harness/lib/gamefolder.py private
   dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game ~/.cache/opentpw-harness/game
   ```

   In that game, make a player named **ref** in the **first** slot, start Lost Kingdom once, and quit. The runs click
   that slot and read `save/users/1ref/`.
5. Check it: `python3 -u tools/harness/lib/parkrun-smoke.py ~/.cache/opentpw-harness/smoke source/OpenTPW/bin/Debug/net10.0`
   enters the park, saves, loads the file back and prints `SCORE 1 of 1`. Add `off` to run it on a private display
   (it needs `Xvfb` and the lavapipe Vulkan driver; every texture is drawn white there, so its pictures are not evidence).

## Writing a run

A task's own run script lives in your work folder, not here. It puts this library on the path and `exec`s the
scaffold:

```python
import os, sys
sys.path.insert(0, "/path/to/OpenTPW/tools/harness/lib"); import paths
OUT = paths.work("q123/run1"); BIN = paths.BIN; NAME = "Q123"
exec(open(os.path.join(paths.LIB, "parkrun.py")).read())
try:
    enter(); ask("pause", "pause")
    predict("what you expect, before you look")
    ...
finally:
    done()
```

`parkrun.py`'s docstring lists what it gives you and the names it uses, which your script must not reuse. When you
write something a later task could use, put it in `lib/` (or `original/`) and give it a row in `docs/TOOLING.md`
in the same commit (`AGENTS.md` rule 21).

## Rules for this folder

- **No game content, ever**: no bytes copied from the game's files or `testme.exe`, and nothing made from them
  (dumps, tables of its strings, its function names). Scripts read them from your own copy at run time.
- **No machine paths.** Every folder comes from `paths.py` / `paths.sh`.
- Some scripts read an earlier task's evidence from the work folder (`q489/easy-paths.txt` and the like); those
  name the task that made it in their docstring.
