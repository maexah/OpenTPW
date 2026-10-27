"""parkscene.py - the scenes and the one-line census readers confirm runs keep writing again. exec() it AFTER parkrun.py.

    var(thing, name)          a script variable's value (`scriptvar`), or None
    thing_line(thing)         the thing's line of `objects` ('' with none)
    staff_line(member)        the member's line of `staff`
    bumper_line()             the first ride's line of `bumpers`
    field(line, word)         what follows a word (and any padding) in a census line, up to the next space ("state", "repair"...), as text; None with none
    hot_pot(guests=5)         buys a Hot Pot (item 1140) on (41,23), joins its queue to the path by clicks on (43,22) and (43,21), admits
                              guests on (43,21) and sends them; answers (pot, [guest]). The cells are fixed in the code: for a
                              freshly entered Lost Kingdom park
    in_a_go(secs=100)         waits until `bumpers` reads Running with somebody seated; answers the line (after `secs` the last
                              line read, Running or not: check it)
    broken_pot(guests=5)      hot_pot + in_a_go + `scriptvar <pot> VAR_BREAKSTAT 1`; answers (pot, guests, where the
                              log stood at the break). What the break then does is the caller's to check: nothing here reads it
    tag_rect(lines, row)      a standing aerial tag's rect, from `tags`' reply lines: the census lists the tags oldest first and
                              the aerial's controls in the same order; answers (rect or None, the ids, the rects)
    tag_click(lines, row)     the window point to click for that tag (TAG_SCALE, 900 of the gadget's 1920; Q411 to Q415), or
                              (56, 441), the one tag's place, where the census has no rect
    pointer()                 the `pointer` reply's line: the park cursor, the help row and its text, the control under it
    pick()                    the `pick` reply's line ("standing thing N" or "standing nothing": the person; then "object thing M"
                              or "object nothing", the cell's object kept apart, Q474)
    at(fx, fy, what, wait=0.4) the real pointer moved to a share of the window across and down
    find_person(want, what)   the pointer stepped round the window's middle (7 x 7, 3.5% across, 4% down) until `pick` names a
                              thing `want(thing)` takes; answers ((fx, fy), thing) or (None, "none"). A locator's camera
                              keeps its person at the middle (Q447, Q451)
    ctrl(k)                   Ctrl+k, then the pointer away from the gadget (1200, 200)
    circle(thing)             a person's pick circle as the last frame drew them (`circle`, Q468): (middle x, middle y,
                              radius across, radius down) in window pixels, or None when they are not drawn
    circles()                 every drawn person's circle, {thing: (middle x, middle y, radius across, radius down, depth)}
    find_cell(cell, sx, sy)   the window pixel nearest (sx, sy), in rings of 10 out to 80, that `pick X Y` answers with the map cell
                              `cell` for it and the points 4 right and 4 down (a cell's edge avoided); raises with none. The cell the
                              hover's arm reads (Q471, Q478); bothcam.to_original() maps it to the original's frame
    wait_log(pattern, secs=6) the first match of a pattern in run.log, polled until it is there or secs pass (the game's
                              lines reach the file a moment after it writes them); answers the match's groups or None
    layout_point(frame, rect, vx, vy)  a point of a window's 2048 x 1536 layout in window pixels, through its frame
                              control's place (`frame` its id in hex, `rect` its stream rect): a screen centred in a window
                              wider than 4:3 is found where it is drawn (Q590, the research screen's frame d2cff)
    press_layout(frame, rect, vx, vy, what)  the real pointer moved there and button 1 pressed and let go
    drag(p0, p1, what, steps=12)  a real drag in window pixels: the pointer to p0, button 1 down, `steps` moves to p1,
                              button 1 up (a slider's thumb; Q590)

"""
def var(thing, name):
    m = re.search(r" is (-?\d+)", " ".join(reply(f"scriptvar {thing} {name}", 0.4, 0))); return int(m.group(1)) if m else None
def thing_line(thing):
    got = [g for g in reply("objects", 1.2, 0) if re.search(rf"thing +{thing} ", g)]; return got[0] if got else ""
def staff_line(member):
    got = [g for g in reply("staff", 1.2, 0) if re.search(rf"thing +{member} ", g)]; return got[0] if got else ""
def bumper_line():
    got = [g for g in reply("bumpers", 1.2, 0) if "ride 0x" in g]; return got[0] if got else ""
def field(line, word):
    m = re.search(rf"\b{word} +(\S+)", line); return m.group(1) if m else None
TAG_SCALE = 900 / 1920
def tag_rect(lines, row):
    text = " ".join(lines); ids = re.findall(r" tag=(\d+)/picture", text)
    rects = re.findall(r"tag(\d+)=(\d+),(\d+),(\d+),(\d+)/frame=(\d+)/shown=(\d)", text)
    if str(row) not in ids: return None, ids, rects
    i = ids.index(str(row)); return (tuple(int(v) for v in rects[i][1:5]) if i < len(rects) else None), ids, rects
def tag_click(lines, row):
    r = tag_rect(lines, row)[0]
    return (int((r[0] + r[2]) / 2 * TAG_SCALE), int((r[1] + r[3]) / 2 * TAG_SCALE)) if r else (56, 441)
def hot_pot(guests=5):
    r = " ".join(reply("buy 1140 41 23 0", 1.5, 0)); pot = int(re.search(r"thing (\d+)", r).group(1)); print("  bought thing", pot)
    for c in (f"tool queue {pot}", "worldclick 43 22", "worldclick 43 21", "tool off"): reply(c, 0.6, 0)
    sent = []
    for i in range(guests):
        g = re.findall(r"guest (\d+)", " ".join(reply("admit 43 21", 0.8, 0)))
        if g: sent.append(int(g[-1])); reply(f"send {g[-1]} {pot}", 0.4, 0)
    return pot, sent
def in_a_go(secs=100):
    line = ""; t0 = time.time()
    while time.time() - t0 < secs:
        line = bumper_line()
        if re.search(r"state Running .*seated [1-9]", line): break
        time.sleep(0.25)
    print("  in a go:", line[:120]); return line
def broken_pot(guests=5):
    pot, sent = hot_pot(guests); in_a_go(); time.sleep(1.5)
    mark = len(logtext()); print("  ", reply(f"scriptvar {pot} VAR_BREAKSTAT 1", 0.3, 0)); return pot, sent, mark
def pointer():
    g = [l for l in reply("pointer", 0.6, 1) if "pointer:" in l]; return g[0] if g else "?"
def pick():
    g = [l for l in reply("pick", 0.6, 1) if "pick:" in l]; return g[0] if g else "?"
def at(fx, fy, what, wait=0.4):
    W, Hh = size(); move(W * fx, Hh * fy, what); time.sleep(wait)
def find_person(want, what):
    for dx, dy in [(0, 0)] + [(i * 0.035, j * 0.04) for j in (-3, -2, -1, 0, 1, 2, 3) for i in (-3, -2, -1, 0, 1, 2, 3) if (i, j) != (0, 0)]:
        at(0.5 + dx, 0.5 + dy, what); m_ = re.search(r"standing thing (\d+)", pick()); t = m_.group(1) if m_ else "none"
        if want(t): return (0.5 + dx, 0.5 + dy), t
    return None, "none"
def ctrl(k):
    key(k, f"Ctrl+{k.upper()}", mods=("Control_L",)); time.sleep(0.8); move(1200, 200, "away from the gadget"); time.sleep(0.3)
def circle(thing):
    g = [l for l in reply(f"circle {thing}", 0.6, 1) if "circle:" in l]
    m_ = re.search(r"middle \((-?[\d.]+),(-?[\d.]+)\)(?: origin pixel \([^)]*\))? radius ([\d.]+) x ([\d.]+)", g[0]) if g else None
    return tuple(float(v) for v in m_.groups()) if m_ else None
def circles():
    out = {}
    for l in reply("circle", 0.6, 0):
        m_ = re.search(r"circle: thing (\d+) middle \((-?[\d.]+),(-?[\d.]+)\)(?: origin pixel \([^)]*\))? radius ([\d.]+) x ([\d.]+) r2 [\d.]+ depth ([\d.]+)", l)
        if m_: out[int(m_.group(1))] = tuple(float(v) for v in m_.groups()[1:])
    return out
def find_cell(cell, sx, sy):
    def at_(px, py):
        g = [l for l in reply(f"pick {px:.0f} {py:.0f}", 0.4, 0) if "pick:" in l]; return g[0] if g else "?"
    want = f"cell ({cell[0]},{cell[1]})"
    for r in range(0, 90, 10):
        for dx in range(-r, r + 1, 10):
            for dy in range(-r, r + 1, 10):
                if max(abs(dx), abs(dy)) != r: continue
                x, y = sx + dx, sy + dy
                if want in at_(x, y) and want in at_(x + 4, y) and want in at_(x, y + 4): return (x, y)
    raise AssertionError(f"no pixel near ({sx},{sy}) picks {want}")
def wait_log(pattern, secs=6):
    t0 = time.time()
    while time.time() - t0 < secs:
        m_ = re.search(pattern, logtext())
        if m_: return m_.groups()
        time.sleep(0.2)
    return None
def layout_point(frame, rect, vx, vy):
    x, y, w_, h = place(frame)
    return x + (vx - rect[0]) * w_ / (rect[2] - rect[0]), y + (vy - rect[1]) * h / (rect[3] - rect[1])
def press_layout(frame, rect, vx, vy, what):
    px, py = layout_point(frame, rect, vx, vy); move(px, py, what); button(1, what); time.sleep(0.8)
def drag(p0, p1, what, steps=12, pressed=None, held=None):
    """A real drag between two window points. pressed() runs once the button is down, before the pointer moves, and
    held() once it has moved, before the release: what a screen does while a thumb is held (Q705)."""
    (x0, y0), (x1, y1) = p0, p1
    move(x0, y0, what); time.sleep(0.2)
    o = win.translate_coords(root, 0, 0)
    xtest.fake_input(d, X.ButtonPress, 1); d.sync(); time.sleep(0.15)
    if pressed: pressed()
    for k in range(1, steps + 1):
        xtest.fake_input(d, X.MotionNotify, x=int(-o.x + x0 + (x1 - x0) * k / steps), y=int(-o.y + y0 + (y1 - y0) * k / steps)); d.sync(); time.sleep(0.05)
    if held: held()
    xtest.fake_input(d, X.ButtonRelease, 1); d.sync(); time.sleep(0.6); print(f"   [drag {what}]")
