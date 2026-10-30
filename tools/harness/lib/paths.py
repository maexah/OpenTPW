"""paths.py - where everything a harness needs is, on this computer. Every script asks here; none names a folder.

    import sys, os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib"))
    import paths

Set by environment variable; each default is what an unset variable means (README.md, "Setting up"):
    OPENTPW_GAME_PATH      the game's folder (needed by every run that reads or launches the game)
    OPENTPW_HARNESS_WORK   where runs write: evidence, private game folders, logs (default ~/.cache/opentpw-harness)
    OPENTPW_EXE            the original's testme.exe (default <game>/testme.exe)
    TPW_ORIGINAL           the original installed under Proton (default ~/Games/TPW-original)
    OPENTPW_DOTNET         the dotnet command (default `dotnet` on PATH, else ~/.dotnet/dotnet)
    OPENTPW_EMU_PYTHON     a Python with unicorn and pefile, for the scripts that run the executable's own code
    OPENTPW_GHIDRA_CLIENT  a command that runs `run_python code=...` against a Ghidra server holding testme.exe
    OPENTPW_FULLSIM_PLAYER a Full Simulation player's folder (save/users/<n><name>) written by the original
    OPENTPW_FULLSIM_PARK   a Full Simulation park file (.TPWS) written by the original, for memrun.py's `fs`
    OPENTPW_XVFB           an Xvfb binary for off-screen runs (default `Xvfb` on PATH)
    OPENTPW_MUTATE_ROOT    where mutate.py makes its copies of the tree (default <work>/mutate)
    OPENTPW_STRYKER        the dotnet-stryker command (default `dotnet-stryker` on PATH); OPENTPW_STRYKER_WORK its copy
    OPENTPW_FULLSIM_PLAYER, TPW_ORIGINAL and OPENTPW_GHIDRA_CLIENT are read by the shell scripts too (paths.sh)"""
import os, shutil

LIB = os.path.dirname(os.path.abspath(__file__))
HARNESS = os.path.dirname(LIB)
ORIGINAL_TOOLS = os.path.join(HARNESS, "original")
REPO = os.path.dirname(os.path.dirname(HARNESS))
BIN = os.path.join(REPO, "source/OpenTPW/bin/Debug/net10.0")
WORK = os.path.expanduser(os.environ.get("OPENTPW_HARNESS_WORK", "~/.cache/opentpw-harness"))
ORIGINAL = os.path.expanduser(os.environ.get("TPW_ORIGINAL", "~/Games/TPW-original"))
PRIVATE_GAME = os.path.join(WORK, "game")         # the desktop runs' private game folder (privategame.py makes it)
PRIVATE_GAME_OFF = os.path.join(WORK, "game-off")  # the off-screen runs' own


def game():
    g = os.environ.get("OPENTPW_GAME_PATH")
    if not g:
        raise SystemExit("set OPENTPW_GAME_PATH to the game's folder")
    return os.path.expanduser(g)


def exe():
    return os.path.expanduser(os.environ.get("OPENTPW_EXE") or os.path.join(game(), "testme.exe"))


def dotnet():
    d = os.environ.get("OPENTPW_DOTNET") or shutil.which("dotnet") or os.path.expanduser("~/.dotnet/dotnet")
    return os.path.expanduser(d)


def dotnet_env(**extra):
    """os.environ with DOTNET_ROOT set from the dotnet in use, and the game named."""
    env = dict(os.environ, **extra)
    env.setdefault("DOTNET_ROOT", os.path.dirname(os.path.realpath(dotnet())))
    if "OPENTPW_GAME_PATH" in os.environ:
        env.setdefault("OPENTPW_GAME_PATH", game())
    return env


def tool(name):
    """A harness C# tool, built by `dotnet build tools/harness/dotnet/<name>` (README.md)."""
    return os.path.join(HARNESS, "dotnet", name, "bin/Debug/net10.0", name)


def work(*parts):
    """A path under WORK; the folder above it is made."""
    p = os.path.join(WORK, *parts)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def need(var):
    v = os.environ.get(var)
    if not v:
        raise SystemExit(f"set {var} (paths.py says what it is)")
    return os.path.expanduser(v)


def xvfb():
    return os.path.expanduser(os.environ.get("OPENTPW_XVFB") or shutil.which("Xvfb") or "Xvfb")
