#!/usr/bin/env python3
"""logpii.py - does a console.log say whose computer it came from? (Q751)

    logpii.py <console.log>... [--also WORD ...]

Looks in each file for this machine's account name and host name (as whole words, any case), its home folder and the
game folder's real path, and any other WORD given (a player's name, also where a slot's digit runs into it). Prints every hit as file:line: text and exits 1 if there is one,
0 if the files are clean. A tester's log must be clean (SafePaths cuts the game folder to `<game folder>`, the home
folder to `~` and the account's name in another path to `<user>`); the terminal's run.log is not cut, so it is the
control: run it on run.log too and see it hit."""
import getpass, os, re, socket, sys

args = sys.argv[1:]
also = []
if "--also" in args:
    i = args.index("--also"); also = args[i + 1:]; args = args[:i]
if not args:
    sys.exit(__doc__)
home = os.path.expanduser("~")
words = [getpass.getuser(), socket.gethostname().split(".")[0]] + also
paths = [home, os.path.realpath(home)] + ([os.environ["OPENTPW_GAME_PATH"]] if "OPENTPW_GAME_PATH" in os.environ else [])
# an --also word (a player's name) may follow a digit: a player's folder is their slot run into the name (users/1Kim)
pats = [re.compile((r"(?<![A-Za-z])" if w in also else r"(?<![A-Za-z0-9])") + re.escape(w) + r"(?![A-Za-z0-9])", re.I)
        for w in words if len(w) >= 2]
pats += [re.compile(re.escape(p)) for p in set(paths)]
hits = 0
for f in args:
    for n, line in enumerate(open(f, errors="replace"), 1):
        for p in pats:
            if p.search(line):
                hits += 1
                if hits <= 40: print(f"{f}:{n}: [{p.pattern[:40]}] {line.rstrip()[:200]}")
                break
print(f"{hits} line(s) name this machine or its user ({', '.join(words)}; {home})" if hits else f"clean: no line names {', '.join(words)} or {home}")
sys.exit(1 if hits else 0)
