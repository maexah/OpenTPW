"""cellprobe.py - exec'd after parkrun.py: the screen point over a map cell, found by moving the real pointer and reading
`strip`'s drawn census while a tool shows ONE square under the pointer (the patrol tool or the path tool, unanchored).
Written for Q433, where real clicks had to land on named cells.

    drawn()            the `strip` drawn census, as text ("51,27,0^0;..." then " | patrol ..." and so on)
    squares(text)      that census's tool squares as (x, y, marker, lift)
    probe(px, py)      the cell under a window point, or None where the census is not one square
    find((x, y), start)   the window point over cell (x, y): two probes give the slope, then Newton steps (14 at most);
                       None where it does not get there. Set the camera first (`camera X Y 60 0`, 10 units a cell): at
                       zoom 25 a 90 px step stays inside one cell and the slope is nought.
Anchor nothing before finding both corners: anchored, the census is the rectangle, not the pointer's cell.
    strip_end_at((x, y), start)   with the path or queue tool ANCHORED (Q540): the window point whose strip ends on
                       (x, y), the cell in line with the anchor; steps the pointer along the anchor's axis by the last
                       square's cell, 16 steps at most. Works under a moving camera (the locator's follow), where find
                       cannot take a slope. None where it does not get there."""

def drawn():
    g = [l for l in reply("strip", 0.5, 0) if "drawn for the pointer:" in l]
    return g[0].split("drawn for the pointer: ", 1)[1].strip() if g else "?"

def squares(text):
    head = text.split(" | ")[0]
    return [] if head in ("none", "?") else [(int(a), int(b), int(c), float(d)) for a, b, c, d in
        (re.match(r"(-?\d+),(-?\d+),(\d+)\^(-?[\d.]+)", s).groups() for s in head.split(";"))]

def probe(px, py):
    move(px, py, "probe"); time.sleep(0.4)
    s = squares(drawn()); return (s[0][0], s[0][1]) if len(s) == 1 else None

def find(target, start):
    px, py = start; step = 90
    for _ in range(14):
        c = probe(px, py)
        if c == target: return (px, py)
        cx, cy = probe(px + step, py), probe(px, py + step)
        if not c or not cx or not cy: px, py = px + 25, py + 25; continue
        j = [[(cx[0] - c[0]) / step, (cy[0] - c[0]) / step], [(cx[1] - c[1]) / step, (cy[1] - c[1]) / step]]
        det = j[0][0] * j[1][1] - j[0][1] * j[1][0]
        if abs(det) < 1e-6: step = int(step * 1.6); continue
        dx, dy = target[0] - c[0], target[1] - c[1]
        sx = (j[1][1] * dx - j[0][1] * dy) / det; sy = (-j[1][0] * dx + j[0][0] * dy) / det
        px, py = int(px + max(-300, min(300, sx))), int(py + max(-300, min(300, sy)))
        print(f"  probe {c} at ({px - int(sx)},{py - int(sy)}) -> try ({px},{py}) for {target}")
    return None

def strip_end_at(target, start, step=25):
    px, py = start
    for _ in range(16):
        move(px, py, "probe"); time.sleep(0.4); s = squares(drawn())
        if not s: px, py = px + 30, py + 30; continue
        c = s[-1][:2]
        if c == target: return (px, py)
        if c[0] != target[0] and c[1] != target[1]:
            print(f"  strip ends {c}, on neither of {target}'s lines: stopped (the anchor's other axis, or a rotated camera)")
            return None
        if c[0] == target[0]: py -= step * (target[1] - c[1])
        if c[1] == target[1]: px -= step * (target[0] - c[0])
        print(f"  strip ends {c}, next ({px},{py})")
    return None
