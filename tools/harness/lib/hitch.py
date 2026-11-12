"""hitch.py - one forced long frame of the park and the park's loop's own account of it, tick by tick. exec() it AFTER parkrun.py.

    hitch(ms, tag)      `sweeps` read, `stall ms` sent (the console stops that frame ms of wall time, so the next owes about ms / 31
                        ticks), `sweeps` read again ms + 2.5 s later; answers the frame's block of the log, parsed, or None with
                        no block: a dict of n, first (its first tick), gt0 (mGameTick at its start), rows (the tick lines' matches),
                        ticks, parts, scripts (each line's tick, particle steps and scripts' tick), sweeps [(tick, scripts seen,
                        mGameTick after)], drops [tick], between [(tick lines before it, line)] (whatever else the frame logged,
                        in order: a sweep's guest, staff and advisor lines sit before their tick's own line), s0 and s1 (the
                        `sweeps` census either side, (mGameTick, run, dropped)). Writes OUT/<tag>-frame.txt and prints a summary.
    steady(f)           (each line one up on the last in tick, particles and scripts; the sweeps and drops on exactly the ticks
                        that divide by 8; each sweep saw the scripts at its own tick; mGameTick one up a sweep)

The block exists only where the frame owes two sweeps or more (ParkLoop's `Park loop:` lines; `stall 600` owes about 20 ticks,
`stall 1200` about 40, so 3 run and 1 or 2 dropped). The census reads are ms + 3 s apart, so their run counts take in the ordinary
frames round the stall too; only their dropped count is the stalled frame's alone (q150, P7's miss). Q150, `q150/confirm.py`."""

_HITCH_HEAD = re.compile(r"Park loop: a frame of (\d+) ticks, (\d+) to (\d+), mGameTick (\d+)")
_HITCH_LINE = re.compile(r"Park loop: tick (\d+): particles to (\S+), scripts to (\S+?)(?:, sweep (\d+) to mGameTick (\d+)|, sweep (dropped))?$")

def _hitch_sweeps():
	for l in reply("sweeps", 0.5, 0):
		m = re.search(r"mGameTick (\d+), (\d+) run, (\d+) dropped", l)
		if m: return tuple(int(g) for g in m.groups())

def hitch(ms, tag):
	s0 = _hitch_sweeps(); mark = len(logtext())
	send(f"stall {ms}"); time.sleep(ms / 1000 + 2.5)
	s1 = _hitch_sweeps(); text = logtext()[mark:].splitlines()
	heads = [i for i, l in enumerate(text) if _HITCH_HEAD.search(l)]
	if not heads:
		print(f"[{tag}] no 'Park loop: a frame' line after stall {ms}"); return None
	h = heads[0]; n, first, last, gt0 = (int(g) for g in _HITCH_HEAD.search(text[h]).groups())
	rows = []; between = []
	for l in text[h + 1:]:
		m = _HITCH_LINE.search(l)
		if m:
			rows.append(m)
			if len(rows) == n: break
		elif not l.startswith("[dbg]") and "fence-" not in l:
			between.append((len(rows), l))
	open(os.path.join(OUT, f"{tag}-frame.txt"), "w").write("\n".join([text[h]] + [r.string for r in rows])
		+ "\n\nlines logged between the tick lines (after N tick lines):\n" + "\n".join(f"  after {k}: {l}" for k, l in between) + "\n")
	f = dict(n=n, first=first, gt0=gt0, rows=rows, between=between, s0=s0, s1=s1,
		ticks=[int(r.group(1)) for r in rows], parts=[int(r.group(2)) for r in rows if r.group(2) != "none"],
		scripts=[int(r.group(3)) for r in rows if r.group(3) != "none"],
		sweeps=[(int(r.group(1)), int(r.group(3)), int(r.group(5))) for r in rows if r.group(4)],
		drops=[int(r.group(1)) for r in rows if r.group(6)])
	print(f"[{tag}] stall {ms}: frame of {n} ticks {first}..{last}, mGameTick {gt0} at its start; {len(rows)} tick lines")
	print(f"[{tag}]   first line: {rows[0].string.split('] ', 1)[-1] if rows else None}")
	print(f"[{tag}]   last line:  {rows[-1].string.split('] ', 1)[-1] if rows else None}")
	print(f"[{tag}]   sweeps (tick, scripts seen, mGameTick after): {f['sweeps']}; dropped at ticks {f['drops']}")
	print(f"[{tag}]   `sweeps` census before {s0} after {s1}")
	for k, l in between: print(f"[{tag}]   between tick lines, after {k} of them: {l[:160]}")
	return f

def steady(f):
	one_up = all(b - a == 1 for seq in (f["ticks"], f["parts"], f["scripts"]) for a, b in zip(seq, seq[1:]))
	on_beat = sorted([t for t, _, _ in f["sweeps"]] + f["drops"]) == [t for t in f["ticks"] if t % 8 == 0]
	own = bool(f["scripts"]) and all(seen == f["scripts"][0] + (t - f["ticks"][0]) for t, seen, _ in f["sweeps"])
	clock = [g for _, _, g in f["sweeps"]] == list(range(f["gt0"] + 1, f["gt0"] + 1 + len(f["sweeps"])))
	return one_up, on_beat, own, clock
