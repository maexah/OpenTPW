"""freshpark.py - exec'd BEFORE parkrun.py by a confirm script: a game folder of the run's own whose only player is a
folder with no gms.dat, which this game takes as a new Full Simulation player (Q209), so parkrun's enter() makes a
FRESH Full Simulation park (no park file, the catalogue's 70 items, tick nought) and not the stock Instant Action one.

    FRESH_GAME = paths.work("q349/game")         the folder to make (with OFFSCREEN, "-off" is added); set OFFSCREEN first
    exec(open(paths.LIB + "/freshpark.py").read()); exec(open(paths.LIB + "/parkrun.py").read())

It sets GAME_FOLDER. The folder's save/users is made again on every run, so each run's park is new. Pause straight after
enter(): the advisor's poll starts on tick 1 (row 80's first turn is tick 50). The console's `hire <candidate> x y`
and `buy <item> x y 0` work there under pause; a fresh park cannot be saved (SAVE_PARK_WITH_NO_FILE). Q349."""
import os, shutil, sys
import paths   # the calling script put tools/harness/lib on sys.path (README.md, "Writing a run")
import gamefolder
GAME_FOLDER = gamefolder.make(os.path.expanduser(FRESH_GAME) + ("-off" if globals().get("OFFSCREEN") else ""), {})
_users = os.path.join(GAME_FOLDER, "save/users")
shutil.rmtree(_users); os.makedirs(os.path.join(_users, "1ref/jungle"))
