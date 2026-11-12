#!/usr/bin/env python3
"""consolegame.py: the private game folder, data folder, save/ checksum and environment that consolerun.py and
consoledrive.py both run the game with (AGENTS.md rule 12), kept once.

    import consolegame
    private, xdg, before = consolegame.private_game(out, game, "consolerun")
    env = consolegame.console_env(out, private, xdg)
    ... run the game on --game private with env ...
    after = consolegame.save_sum(game)

`private_game` makes <out>/game: links to <game>'s entries with a COPY of its save/ (empty where it has none), and
<out>/xdg for XDG_DATA_HOME, and answers them with <game>'s save/ checksum taken before the run; it refuses when
<out>/game is <game> itself. `console_env` is os.environ with OPENTPW_DEBUG_CONSOLE=1, SDL_AUDIODRIVER (dummy, or the
driver given), OPENTPW_NO_MESSAGE_BOX=1, OPENTPW_LOG_DIR=<out>, OPENTPW_GAME_PATH=<out>/game and XDG_DATA_HOME=<out>/xdg.
Needs nothing but the standard library, so it runs in a cloud lane."""
import hashlib, os, shutil


def save_sum(folder):
    """The md5 of every file under <folder>/save, names and bytes, in a fixed order."""
    h = hashlib.md5()
    for root_, _, files in sorted(os.walk(os.path.join(folder, "save"))):
        for f in sorted(files):
            p = os.path.join(root_, f); h.update(p.encode()); h.update(open(p, "rb").read())
    return h.hexdigest()


def private_game(out, game, tool):
    """<out>/game and <out>/xdg made afresh for a run, and <game>'s save/ checksum before it."""
    private = os.path.join(out, "game")
    if os.path.realpath(private) == os.path.realpath(game):
        raise SystemExit(f"{tool}: <out>/game is <game> itself")
    if os.path.lexists(private):
        shutil.rmtree(private)
    os.makedirs(private)
    for entry in os.listdir(game):
        if entry != "save":
            os.symlink(os.path.join(game, entry), os.path.join(private, entry))
    if os.path.isdir(os.path.join(game, "save")):
        shutil.copytree(os.path.join(game, "save"), os.path.join(private, "save"), symlinks=True)
    else:
        os.makedirs(os.path.join(private, "save"))
    xdg = os.path.join(out, "xdg"); os.makedirs(xdg, exist_ok=True)
    return private, xdg, save_sum(game)


def console_env(out, private, xdg, audio="dummy"):
    """The run's environment: the debug console on stdin, no sound device, no message box, the log in <out>."""
    return dict(os.environ, OPENTPW_DEBUG_CONSOLE="1", SDL_AUDIODRIVER=audio, OPENTPW_NO_MESSAGE_BOX="1",
                OPENTPW_LOG_DIR=out, OPENTPW_GAME_PATH=private, XDG_DATA_HOME=xdg)
