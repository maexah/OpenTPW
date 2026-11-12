#!/usr/bin/env python3
"""consoledrive.py: OpenTPW off-screen under xvfb-run with the debug console, driven by a script that reads its replies.

consolerun.py's interactive sibling for a cloud lane (no python-xlib, Q862): a run script imports it, starts the game with
`Drive(out, bin, game)`, then `ask(command, until)` sends one console command and returns the lines printed up to the
first one matching the regex `until` (or the timeout); `key(name)`, `focus_key(name)`, `click(x, y)` and `wheel(x, y, notches)` are real X events by xdotool on the
run's own Xvfb display, and on no other: each is refused where `run_display` cannot find that Xvfb in the run's session; `quit()` ends the run and kills what is left. Everything the game prints is in <out>/stdout.txt,
each command after `>>> `.

As consolerun.py, the game never runs on <game> itself (consolegame.py): <out>/game links to <game>'s entries with a COPY
of its save/ (empty where it has none), XDG_DATA_HOME is <out>/xdg, and <game>'s save/ is checksummed either side
(AGENTS.md rule 12). Sets SDL_AUDIODRIVER=dummy, OPENTPW_DEBUG_CONSOLE=1, OPENTPW_NO_MESSAGE_BOX=1, OPENTPW_LOG_DIR=<out>
and SDL_VIDEODRIVER=x11, with WAYLAND_DISPLAY taken out, so the game opens on the run's Xvfb and nowhere else. The
display is the first free one from `display` up (no /tmp/.X<n>-lock or socket), as xvfb-run's -a picks, and the run
stops with the game's output if xvfb-run ends before the lobby wait does (a display taken after all, a build that will
not start), so no key or click is ever sent to a display that is not the run's."""
import os, queue, re, signal, subprocess, sys, threading, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import consolegame


def run_display(sid):
    """The run's own X display, {DISPLAY, XAUTHORITY}, or None: the X server must be an Xvfb in the run's session `sid`
    (xvfb-run started with start_new_session=True, so its pid is the session's), its display and `-auth` file read from that
    Xvfb's own command line, and the game's process (the one whose command line names OpenTPW.dll) in that session must
    have been given the same two. Nothing is taken from the caller's environment or from xvfb-run's shell, which inherits
    the caller's DISPLAY: on a desk that is the desktop's. A caller that gets None sends no X event (AGENTS.md rule 12)."""
    xvfb = game = None
    for pid in filter(str.isdigit, os.listdir("/proc")):
        try:
            if os.getsid(int(pid)) != sid:
                continue
            argv = [a.decode() for a in open(f"/proc/{pid}/cmdline", "rb").read().split(b"\0") if a]
            if not argv:
                continue
            if os.path.basename(argv[0]) == "Xvfb":
                display = next((a for a in argv[1:] if a.startswith(":")), None)
                auth = argv[argv.index("-auth") + 1] if "-auth" in argv[:-1] else None
                if display and auth:
                    xvfb = (display, auth)
            elif any(a.endswith("OpenTPW.dll") for a in argv):
                seen = dict(v.decode().split("=", 1) for v in open(f"/proc/{pid}/environ", "rb").read().split(b"\0")
                            if v.startswith((b"DISPLAY=", b"XAUTHORITY=")))
                game = (seen.get("DISPLAY"), seen.get("XAUTHORITY"))
        except (OSError, ProcessLookupError, ValueError, UnicodeDecodeError):
            pass
    if xvfb is None or game != xvfb:
        return None
    return {"DISPLAY": xvfb[0], "XAUTHORITY": xvfb[1]}


def free_display(first):
    """The first display number from `first` with no X server's lock file or socket."""
    for n in range(first, first + 400):
        if not os.path.exists(f"/tmp/.X{n}-lock") and not os.path.exists(f"/tmp/.X11-unix/X{n}"):
            return n
    raise SystemExit(f"consoledrive: no free X display from :{first}")


class Drive:
    def __init__(self, out, binp, game, display=97, lobby_secs=25):
        self.out = out = os.path.abspath(out); self.game = game = os.path.abspath(game)
        os.makedirs(out, exist_ok=True)
        private, xdg, self.before = consolegame.private_game(out, game, "consoledrive")
        self.auth = os.path.join(out, "xauth")
        display = free_display(display)
        self.display = f":{display}"
        env = consolegame.console_env(out, private, xdg)
        env["SDL_VIDEODRIVER"] = "x11"
        env.pop("WAYLAND_DISPLAY", None)
        self.p = subprocess.Popen(["xvfb-run", "-n", str(display), "-f", self.auth, "-s", "-screen 0 1280x1024x24",
                                   "dotnet", os.path.join(binp, "OpenTPW.dll"), "--game", private],
                                  stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                  env=env, start_new_session=True)
        self.log = open(os.path.join(out, "stdout.txt"), "w")
        self.lines = queue.Queue()
        threading.Thread(target=self._pump, daemon=True).start()
        end = time.time() + lobby_secs
        while time.time() < end:
            if self.p.poll() is not None:
                self.log.flush()
                tail = open(os.path.join(out, "stdout.txt")).read()[-2000:]
                raise SystemExit(f"consoledrive: xvfb-run on {self.display} ended (exit {self.p.returncode}) before the lobby wait did:\n{tail}")
            time.sleep(0.5)

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
        """xdotool on the run's own Xvfb only (run_display): where the display this run picked (free_display) is not this
        run's server (Xvfb failed to start on it, another server took it), nothing is sent and the refusal is logged."""
        found = run_display(self.p.pid)
        if found != {"DISPLAY": self.display, "XAUTHORITY": self.auth}:
            self.log.write(f"!!! refused xdotool {' '.join(args)}: {self.display} is not this run's Xvfb ({found})\n"); self.log.flush()
            print(f"consoledrive: xdotool refused, {self.display} is not this run's Xvfb ({found})")
            return
        subprocess.run(["xdotool", *args], env=dict(os.environ, **found), check=False)

    def key(self, name): self._xdotool("key", name)

    def click(self, x, y): self._xdotool("mousemove", str(int(x)), str(int(y)), "click", "1")

    def focus_key(self, name):
        """key(name) with the game's window given the keyboard first (windowfocus --sync), which a key the console has no
        command for needs (Q994: a plain key was not seen); F1 over the park, Q231."""
        self._xdotool("search", "--onlyvisible", "--name", ".", "windowfocus", "--sync", "%1", "key", name)

    def wheel(self, x, y, notches):
        """The pointer to (x, y) and the wheel turned `notches` times: away (button 4) for a positive count, toward (button 5,
        a notch down, the park's zoom out) for a negative one (Q231)."""
        self._xdotool("mousemove", str(int(x)), str(int(y)), "click", "--repeat", str(abs(int(notches))), "--delay", "150",
                      "4" if notches > 0 else "5")

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
        if consolegame.save_sum(self.game) != self.before:
            print("consoledrive: the game folder's save/ CHANGED during the run")
