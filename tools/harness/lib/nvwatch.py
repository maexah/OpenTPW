#!/usr/bin/env python3
"""nvwatch.py [--keep] [--quiet]: has NeoVeldrid released a version newer than the one we build on?

Reads every NeoVeldrid* PackageReference and its Version from main's .csproj files in the main clone (git objects, so a
child's branch does not matter), asks NuGet's public flat container for each package's versions (a plain GET, no
account, nothing about the user sent), and compares the newest stable one with both the pinned version and the newest
one already announced (state in <work>/nvwatch/state.json).

Prints one line per package: `pinned X, newest Y`, and `NEW <package> <version>` for a version never announced before.
A NEW line is also appended to <work>/nvwatch/alerts.log (the Sentinel's check-in shows it) and, when
a desktop session is reachable, sent with notify-send. --keep records the newest as announced (the timer passes it, so
each version is announced once). --quiet prints only NEW lines. Exit 0 always unless NuGet cannot be reached (1).

Run weekly (Monday 06:17) by the user timer tpw-nvwatch.timer (Alexah, 2026-10-26: "monitor NeoVeldrid automatically so we don't miss
out on updates"). The release notes: https://github.com/jhm-ciberman/neo-veldrid/releases/tag/v<version>."""
import json, os, re, subprocess, sys, urllib.request, datetime
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths

REPO = paths.REPO
D = os.path.join(paths.WORK, "nvwatch")
STATE = os.path.join(D, "state.json")
ALERTS = os.path.join(D, "alerts.log")
keep = "--keep" in sys.argv
quiet = "--quiet" in sys.argv

def vkey(v):
    return tuple(int(x) for x in re.findall(r"\d+", v.split("-")[0]))

def pinned():
    files = subprocess.run(["git", "-C", REPO, "ls-tree", "-r", "--name-only", "main"], capture_output=True, text=True).stdout.split()
    found = {}
    for f in files:
        if not f.endswith(".csproj"):
            continue
        text = subprocess.run(["git", "-C", REPO, "show", f"main:{f}"], capture_output=True, text=True).stdout
        for name, ver in re.findall(r'PackageReference\s+Include="(NeoVeldrid[^"]*)"\s+Version="([^"]+)"', text):
            found.setdefault(name, set()).add(ver)
    return found

def newest(name):
    url = f"https://api.nuget.org/v3-flatcontainer/{name.lower()}/index.json"
    req = urllib.request.Request(url, headers={"User-Agent": "nvwatch"})
    with urllib.request.urlopen(req, timeout=20) as r:
        versions = [v for v in json.load(r)["versions"] if "-" not in v]
    return max(versions, key=vkey)

os.makedirs(D, exist_ok=True)
state = json.load(open(STATE)) if os.path.exists(STATE) else {}
pins = pinned()
if not pins:
    print("no NeoVeldrid PackageReference found on main"); sys.exit(0)
new_lines = []
for name in sorted(pins):
    try:
        top = newest(name)
    except Exception as e:
        print(f"{name}: NuGet not reached ({type(e).__name__}: {e})"); sys.exit(1)
    pin = max(pins[name], key=vkey)
    seen = state.get(name, pin)
    if not quiet:
        print(f"{name}: pinned {', '.join(sorted(pins[name], key=vkey))}, newest {top}")
    if vkey(top) > vkey(pin) and vkey(top) > vkey(seen):
        new_lines.append(f"NEW {name} {top} (we are on {pin})")
    if keep and vkey(top) > vkey(seen):
        state[name] = top
stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
for line in new_lines:
    print(line)
    with open(ALERTS, "a") as a:
        a.write(f"{stamp} {line}\n")
if new_lines:
    version = new_lines[0].split()[2]
    body = f"NeoVeldrid {version} is out (we are on {max(max(v, key=vkey) for v in pins.values()):s}). " \
           f"https://github.com/jhm-ciberman/neo-veldrid/releases/tag/v{version}"
    try:
        subprocess.run(["notify-send", "-a", "OpenTPW", "NeoVeldrid update", body], timeout=10, capture_output=True)
    except Exception:
        pass
if keep:
    json.dump(state, open(STATE, "w"), indent=1)
