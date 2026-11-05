"""framesweep.py - a console command, then a frame and a census line every STEP seconds, and the lift a thing leaves in the frames.
exec() it AFTER parkrun.py (it uses `send`, `drain`, `lines`, `tail`, `grab`, `time`, `queue` and `re` from the library).

    sweep( cmd, pat, secs, tag, trigger, step=0.1 )
                    grabs the window, sends `trigger` (the console command that starts the thing: `strike`, `bolt X Y`), then every
                    `step` seconds grabs a frame and sends `cmd`, keeping the first reply line containing `pat`; saves each frame as
                    OUT/<tag>-NN.png and answers [(seconds since the trigger was sent, the census line or None, the frame)].
                    The seconds are the wall clock: the thing's own age is that less the frame or two (more on a busy machine, up to
                    0.25 s seen) before the frame that applied the trigger, so keep every bound 0.1 s or more from a boundary
    val( line, key )
                    the number after `key=` in a census line, or None
    lift( samples, base, box, thr )
                    the thing's own pixels: those the first sample after 0.05 s lifted by more than `thr` grey levels over `base` (a frame
                    grabbed before the trigger), inside `box` (a crop box); answers (the mask's size, [(seconds, mean lift over the mask)]).
                    A plain difference of two frames of a live scene carries hundreds of pixels of its own motion (peeps, water); the
                    mask measures the thing alone. Check the mask's size: a thing that comes up elsewhere leaves a handful
    A whole-frame mask in the lobby is NOT usable: the lobby moves on its own after `freeze` and its lift tail outlasts the census (Q1034).
"""
import numpy as np

def val(line, key):
    m = re.search(rf"{key}=(-?[\d.]+)", line or "")
    return float(m.group(1)) if m else None

def sweep(cmd, pat, secs, tag, trigger, step=0.1):
    out = []; drain(); send(trigger); t0 = time.monotonic(); n = 0
    while n * step <= secs:
        time.sleep(max(0.0, t0 + n * step - time.monotonic()))
        im = grab(); el = time.monotonic() - t0
        drain(); send(cmd); got = None; end = time.monotonic() + 2.0
        while time.monotonic() < end and got is None:
            try: l = lines.get(timeout=0.2)
            except queue.Empty: continue
            if "[dbg]" in l and pat in l: got = tail(l)
        out.append((el, got, im)); n += 1
    for i, (el, got, im) in enumerate(out):
        im.save(os.path.join(OUT, f"{tag}-{i:02d}.png"))
        print(f"  {tag} t={el:5.3f}s {got}")
    return out

def lift(samples, base, box, thr):
    b = np.array(base.convert("L").crop(box), dtype=float)
    first = next(im for el, _, im in samples if el > 0.05)
    mask = (np.array(first.convert("L").crop(box), dtype=float) - b) > thr
    return int(mask.sum()), [(el, float((np.array(im.convert("L").crop(box), dtype=float) - b)[mask].mean()) if mask.any() else 0.0)
                             for el, _, im in samples]
