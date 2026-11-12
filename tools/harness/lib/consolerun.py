#!/usr/bin/env python3
"""consolerun.py <outdir> <bin> <game> <script>: OpenTPW run off-screen under xvfb-run with the debug console, fed a script.

A bare feeder for a machine without python-xlib (a cloud lane): needs only xvfb-run and the build, and no private game
folder or `ref` player. Where those are set up, prefer parkrun.py with OFFSCREEN = True (README.md). <bin> is the build's
folder (OpenTPW.dll), <game> the game folder, <script> a text file of lines `SECS COMMAND`: each command is written to the
console's stdin, then the run waits SECS seconds; `#` starts a comment. The run first waits 25 s for the lobby. A command
that starts with `!` is not the console's: the rest is run in a shell with DISPLAY and XAUTHORITY set to the run's own Xvfb, its output
in stdout.txt after `!!! `; where that Xvfb cannot be found in the run's session (consoledrive.run_display), the line is
refused and logged, never run on the caller's DISPLAY, which on a desk is the desktop (AGENTS.md rule 12) (Q994: a real key press where the console has none; focus the window first,
`xdotool windowfocus --sync`, docs/TOOLING.md has the line).

The game never runs on <game> itself: it runs on <outdir>/game, links to <game>'s entries with a COPY of its save/ (empty
where it has none), and XDG_DATA_HOME is <outdir>/xdg, so nothing of the player's is written (AGENTS.md rule 12); <game>'s
save/ is checksummed either side and a difference is printed. Sets SDL_AUDIODRIVER=dummy (or CONSOLERUN_AUDIODRIVER's
value: a driver SDL has not, such as `none`, runs the game with no sound device, Audio.Ready false), OPENTPW_DEBUG_CONSOLE=1,
OPENTPW_NO_MESSAGE_BOX=1 and OPENTPW_LOG_DIR=<outdir>. Everything the game prints, each command after `>>> `, is in
<outdir>/stdout.txt. End the script with `8 quit`; whatever is still running 20 s after the last line (the game, Xvfb,
xvfb-run) is sent SIGTERM as one process group, and SIGKILL 5 s later. No memory cap: on a desk use gamedrive.py's."""
import os, signal, subprocess, sys, threading, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import consolegame
from consoledrive import run_display

out, binp, game, script = sys.argv[1:5]
out = os.path.abspath(out); game = os.path.abspath(game)
os.makedirs(out, exist_ok=True)

# The run's own game folder: links to the game's entries, its own copy of save/ (consolegame.py).
private, xdg, before = consolegame.private_game(out, game, "consolerun")
env = consolegame.console_env(out, private, xdg, os.environ.get("CONSOLERUN_AUDIODRIVER", "dummy"))
p = subprocess.Popen(["xvfb-run", "-a", "-s", "-screen 0 1280x1024x24", "dotnet", os.path.join(binp, "OpenTPW.dll"), "--game", private],
                     stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=env,
                     start_new_session=True)
log = open(os.path.join(out, "stdout.txt"), "w")

def pump():
    for line in p.stdout:
        log.write(line); log.flush()

threading.Thread(target=pump, daemon=True).start()

time.sleep(25)
for line in open(script):
    line = line.split("#", 1)[0].strip()
    if not line:
        continue
    secs, cmd = line.split(" ", 1)
    log.write(f">>> {cmd}\n"); log.flush()
    if cmd.startswith("!"):
        found = run_display(p.pid)
        if found is None:
            log.write("!!! refused: the run's own Xvfb was not found in its session, so nothing was run\n"); log.flush()
            print(f"consolerun: refused `{cmd}`, the run's own Xvfb was not found")
            time.sleep(float(secs))
            continue
        shell = subprocess.run(cmd[1:], shell=True, env=dict(env, **found), stdout=subprocess.PIPE,
                               stderr=subprocess.STDOUT, text=True)
        log.write("".join(f"!!! {l}\n" for l in shell.stdout.splitlines()) + f"!!! exit {shell.returncode}\n"); log.flush()
        time.sleep(float(secs))
        continue
    try:
        p.stdin.write(cmd + "\n"); p.stdin.flush()
    except BrokenPipeError:
        break
    time.sleep(float(secs))

# xvfb-run runs the game in the foreground and kills Xvfb from an EXIT trap, so the whole group is signalled, not the shell.
def signal_group(sig):
    try:
        os.killpg(p.pid, sig)
    except ProcessLookupError:
        pass

try:
    p.wait(20)
except subprocess.TimeoutExpired:
    signal_group(signal.SIGTERM)
    try:
        p.wait(5)
    except subprocess.TimeoutExpired:
        pass
signal_group(signal.SIGKILL)
p.wait()

print(f"consolerun: the game's exit code {p.returncode} (negative: ended by that signal, after the last line's 20 s)")
after = consolegame.save_sum(game)
print(f"consolerun: {game}/save {'unchanged' if after == before else 'CHANGED during the run'} ({before} -> {after})")
