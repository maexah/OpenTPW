#!/usr/bin/env python3
"""consolerun.py <outdir> <bin> <game> <script>: OpenTPW run off-screen under xvfb-run with the debug console, fed a script.

For a machine with no harness folders and no paths.py (a cloud lane): needs only xvfb-run and the build. <bin> is the
build's folder (OpenTPW.dll), <game> the game folder (--game), <script> a text file of lines `SECS COMMAND`: each command
is written to the console's stdin, then the run waits SECS seconds; `#` starts a comment. The run first waits 25 s for the
lobby. Sets SDL_AUDIODRIVER=dummy, OPENTPW_DEBUG_CONSOLE=1 and OPENTPW_LOG_DIR=<outdir>. Everything the game prints, each
command after `>>> `, is in <outdir>/stdout.txt. End the script with `8 quit`; the game is killed 20 s after the last line."""
import os, subprocess, sys, threading, time

out, binp, game, script = sys.argv[1:5]
os.makedirs(out, exist_ok=True)
env = dict(os.environ, OPENTPW_DEBUG_CONSOLE="1", SDL_AUDIODRIVER="dummy", OPENTPW_LOG_DIR=out)
p = subprocess.Popen(["xvfb-run", "-a", "-s", "-screen 0 1280x1024x24", "dotnet", os.path.join(binp, "OpenTPW.dll"), "--game", game],
                     stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=env)
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
    try:
        p.stdin.write(cmd + "\n"); p.stdin.flush()
    except BrokenPipeError:
        break
    time.sleep(float(secs))
try:
    p.wait(20)
except subprocess.TimeoutExpired:
    p.kill(); p.wait()
