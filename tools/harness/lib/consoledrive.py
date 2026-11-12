#!/usr/bin/env python3
"""consoledrive.py: OpenTPW off-screen under xvfb-run with the debug console, driven by a script that reads its replies.

consolerun.py's interactive sibling for a cloud lane (no python-xlib, Q862): a run script imports it, starts the game with
`Drive(out, bin, game)`, then `ask(command, until)` sends one console command and returns the lines printed up to the
first one matching the regex `until` (or the timeout); `key(name)`, `focus_key(name)`, `click(x, y)` and
`wheel(x, y, notches)` are real X events by xdotool on the run's own Xvfb display, and on no other: each is refused where
`run_display` cannot find that Xvfb in the run's session; `bar_click(which)` clicks the message bar's first or second
button there (`bar_button`, also barclick.sh's, for consolerun.py); `quit()` ends the run and kills what is left.
Everything the game prints is in <out>/stdout.txt,
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


def bar_button(stdout, which="first", high=720):
    """The window point (x, y) of the message bar's first or second button, from the last `bar: out=1 rect=X,Y` a `tags`
    reply printed in the file `stdout`, or None where no reply had the bar out. The buttons sit at the bar's
    +(28,9)..(131,112) and +(28,117)..(131,219) (ParkGadget, 0x10d30 and 0x10d2f), clicked at +(79,60) and +(79,168); the
    virtual screen is 1536 high and pinned left and bottom, so on a window at least 4:3 wide a point is its virtual one
    times the window's height `high` over 1536 (720 is consolerun.py's and Drive's window). The console's `click` reaches
    the window stack only, never the gadget, so this is clicked as a real X event (`window_click`)."""
    found = re.findall(r"bar: out=1 rect=(-?\d+),(-?\d+)", open(stdout, errors="replace").read())
    if not found:
        return None
    a, b = (int(v) for v in found[-1])
    return (a + 79) * high // 1536, (b + (168 if which == "second" else 60)) * high // 1536


def window_click(display, x, y):
    """A real click at (x, y) in the game's own window, the one named exactly `Theme Park World`, on `display` (the
    {DISPLAY, XAUTHORITY} run_display answered, never the caller's): False, and nothing sent, where no such window is
    visible there."""
    env = dict(os.environ, **display)
    found = subprocess.run(["xdotool", "search", "--onlyvisible", "--name", "^Theme Park World$"], env=env,
                           stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True).stdout.split()
    if not found:
        return False
    subprocess.run(["xdotool", "windowfocus", "--sync", found[0], "mousemove", "--window", found[0], str(int(x)), str(int(y)),
                    "click", "1"], env=env, check=False)
    return True


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

    def _own(self, what):
        """The run's own display (run_display), or None, logged as refused, where the display this run picked
        (free_display) is not this run's server (Xvfb failed to start on it, another server took it)."""
        found = run_display(self.p.pid)
        if found != {"DISPLAY": self.display, "XAUTHORITY": self.auth}:
            self.log.write(f"!!! refused {what}: {self.display} is not this run's Xvfb ({found})\n"); self.log.flush()
            print(f"consoledrive: {what} refused, {self.display} is not this run's Xvfb ({found})")
            return None
        return found

    def _xdotool(self, *args):
        """xdotool on the run's own Xvfb only (_own): elsewhere nothing is sent."""
        if (found := self._own(f"xdotool {' '.join(args)}")) is not None:
            subprocess.run(["xdotool", *args], env=dict(os.environ, **found), check=False)

    def key(self, name): self._xdotool("key", name)

    def focus_key(self, name):
        """A key sent to the game's window with the focus given first (`windowfocus --sync`), the pointer put on it: without
        the focus the window hears no key (Q994). Q547's cursor Up and Down over an object window; Q231's F1 over the park,
        a key the console has no command for."""
        self._xdotool("search", "--onlyvisible", "--name", ".", "windowfocus", "--sync", "%1",
                      "mousemove", "--window", "%1", "640", "600", "key", name)

    def click(self, x, y): self._xdotool("mousemove", str(int(x)), str(int(y)), "click", "1")

    def bar_click(self, which="first", high=720):
        """A real click on the message bar's first or second button (bar_button, from the last `tags` reply with the bar
        out), in the game's window on the run's own Xvfb: False where the bar is not out, the display is not the run's,
        or the window is not found."""
        point = bar_button(os.path.join(self.out, "stdout.txt"), which, high)
        found = self._own(f"bar_click {which}")
        if point is None or found is None:
            return False
        self.log.write(f"!!! bar_click {which} at {point[0]} {point[1]}\n"); self.log.flush()
        return window_click(found, *point)

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


if __name__ == "__main__":
    # consoledrive.py barclick <stdout.txt> first|second [window height]: barclick.sh's body, for consolerun.py's `!` line.
    # It acts only where CONSOLERUN_SID (the run's session, which consolerun.py gives a `!` line) leads run_display to the
    # very DISPLAY and XAUTHORITY this process was given; anywhere else, a desk's own DISPLAY too, it sends nothing, exit 1.
    if len(sys.argv) < 4 or sys.argv[1] != "barclick":
        raise SystemExit("usage: consoledrive.py barclick <stdout.txt> first|second [window height]")
    out, which = sys.argv[2], sys.argv[3]
    high = int(sys.argv[4]) if len(sys.argv) > 4 else 720
    sid = os.environ.get("CONSOLERUN_SID", "")
    own = run_display(int(sid)) if sid.isdigit() else None
    if own is None or own != {"DISPLAY": os.environ.get("DISPLAY"), "XAUTHORITY": os.environ.get("XAUTHORITY")}:
        print(f"barclick: refused, DISPLAY {os.environ.get('DISPLAY')} is not a consolerun.py run's own Xvfb ({own})")
        raise SystemExit(1)
    point = bar_button(out, which, high)
    if point is None:
        print(f"barclick: no bar out in {out}")
        raise SystemExit(1)
    print(f"barclick: {which} button at {point[0]} {point[1]} on {own['DISPLAY']}")
    if not window_click(own, *point):
        print(f"barclick: no visible window named 'Theme Park World' on {own['DISPLAY']}")
        raise SystemExit(1)
