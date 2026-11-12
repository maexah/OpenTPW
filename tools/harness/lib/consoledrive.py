#!/usr/bin/env python3
"""consoledrive.py: OpenTPW off-screen under xvfb-run with the debug console, driven by a script that reads its replies.

consolerun.py's interactive sibling for a cloud lane (no python-xlib, Q862): a run script imports it, starts the game with
`Drive(out, bin, game)`, then `ask(command, until)` sends one console command and returns the lines printed up to the
first one matching the regex `until` (or the timeout); `key(name)` and `click(x, y)` are real X events by xdotool on the
run's own Xvfb display; `quit()` ends the run and kills what is left. Everything the game prints is in <out>/stdout.txt,
each command after `>>> `.

As consolerun.py, the game never runs on <game> itself: <out>/game links to <game>'s entries with a COPY of its save/
(empty where it has none), XDG_DATA_HOME is <out>/xdg, and <game>'s save/ is checksummed either side (AGENTS.md rule
12). Sets SDL_AUDIODRIVER=dummy, OPENTPW_DEBUG_CONSOLE=1, OPENTPW_NO_MESSAGE_BOX=1 and OPENTPW_LOG_DIR=<out>."""
import hashlib, os, queue, re, shutil, signal, subprocess, threading, time


def _save_sum(folder):
    h = hashlib.md5()
    for root_, _, files in sorted(os.walk(os.path.join(folder, "save"))):
        for f in sorted(files):
            p = os.path.join(root_, f); h.update(p.encode()); h.update(open(p, "rb").read())
    return h.hexdigest()


class Drive:
    def __init__(self, out, binp, game, display=97, lobby_secs=25):
        self.out = out = os.path.abspath(out); self.game = game = os.path.abspath(game)
        os.makedirs(out, exist_ok=True)
        private = os.path.join(out, "game")
        if os.path.realpath(private) == os.path.realpath(game):
            raise SystemExit("consoledrive: <out>/game is <game> itself")
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
        self.before = _save_sum(game)
        self.auth = os.path.join(out, "xauth")
        self.display = f":{display}"
        env = dict(os.environ, OPENTPW_DEBUG_CONSOLE="1", SDL_AUDIODRIVER="dummy", OPENTPW_NO_MESSAGE_BOX="1",
                   OPENTPW_LOG_DIR=out, OPENTPW_GAME_PATH=private, XDG_DATA_HOME=xdg)
        self.p = subprocess.Popen(["xvfb-run", "-n", str(display), "-f", self.auth, "-s", "-screen 0 1280x1024x24",
                                   "dotnet", os.path.join(binp, "OpenTPW.dll"), "--game", private],
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                  env=env, start_new_session=True)
        self.log = open(os.path.join(out, "stdout.txt"), "w")
        self.lines = queue.Queue()
        threading.Thread(target=self._pump, daemon=True).start()
        time.sleep(lobby_secs)

    def _pump(self):
        for line in self.p.stdout:
            self.log.write(line); self.log.flush(); self.lines.put(line.rstrip("\n"))

    def drain(self):
        got = []
        while True:
            try: got.append(self.lines.get_nowait())
            except queue.Empty: return got

    def ask(self, command, until, timeout=15):
        """Send one console command; the lines printed until one matches `until` (a regex), or the timeout."""
        self.drain()
        self.log.write(f">>> {command}\n"); self.log.flush()
        self.p.stdin.write(command + "\n"); self.p.stdin.flush()
        got, end = [], time.time() + timeout
        while time.time() < end:
            try: line = self.lines.get(timeout=0.2)
            except queue.Empty: continue
            got.append(line)
            if re.search(until, line): break
        return got

    def collect(self, secs):
        time.sleep(secs); return self.drain()

    def _xdotool(self, *args):
        env = dict(os.environ, DISPLAY=self.display, XAUTHORITY=self.auth)
        subprocess.run(["xdotool", *args], env=env, check=False)

    def key(self, name): self._xdotool("key", name)

    def click(self, x, y): self._xdotool("mousemove", str(int(x)), str(int(y)), "click", "1")

    def quit(self):
        try:
            self.p.stdin.write("quit\n"); self.p.stdin.flush()
            self.p.wait(timeout=20)
        except Exception:
            pass
        for sig in (signal.SIGTERM, signal.SIGKILL):
            try: os.killpg(self.p.pid, sig)
            except ProcessLookupError: break
            time.sleep(5)
        if _save_sum(self.game) != self.before:
            print("consoledrive: the game folder's save/ CHANGED during the run")
