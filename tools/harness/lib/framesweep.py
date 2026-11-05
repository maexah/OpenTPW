"""framesweep.py - a console command, then a frame and a census line every STEP seconds, and the lift a thing leaves in the frames.
exec() it AFTER parkrun.py (it uses `send`, `drain`, `lines`, `tail`, `grab`, `time`, `queue` and `re` from the library).

    sweep( cmd, pat, secs, tag, trigger, step=0.1 )
                    grabs the window, sends `trigger` (the console command that starts the thing: `strike`, `bolt X Y`), then every
                    `step` seconds grabs a frame and sends `cmd`, keeping the first reply line containing `pat`; saves each frame as
                    OUT/<tag>-NN.png and answers [(seconds since the trigger was sent, the census line or None, the frame)].
                    The seconds are the wall clock: the thing's own age is that less the frame or two (more on a busy machine, up to
                    0.25 s seen) before the frame that applied the trigger, so keep every bound 0.1 s or more from a boundary
    stepsweep( cmd, pat, n, tag, trigger=None, steps=3 )
                    the same on a PAUSED clock, so every age is exact: sends `trigger` (if any), then n times `step <steps>` (each step is 1/60 s,
                    Time.StepDelta), waits for `state` to say stepping=0, grabs a frame and sends `cmd`; answers [(age, census, frame)] with the age
                    0 (before any step) first, age = k * steps / 60. A paused world does not strike or drift by itself, but the stepped one still
                    moves (ocean, mascot, flyers: tens of thousands of pixels over a second). Pause BEFORE the thing is triggered, and in the lobby
                    check first that nothing has struck by itself (`near` says axisDist=-): the lobby strikes about every 2.5 s, from
                    Random.Shared, and a strike before yours moves Lightning's own random sequence on, so the bolt lands elsewhere (Q1034)
    val( line, key )
                    the number after `key=` in a census line, or None
    mask_of( samples, base, box, thr, after=0.05 )
                    the boolean mask lift() builds (the pixels the first sample after `after` seconds lifted by more than `thr`)
    lift( samples, base, box, thr, mask=None )
                    the thing's own pixels: those the first sample after 0.05 s lifted by more than `thr` grey levels over `base` (a frame
                    grabbed before the trigger), inside `box` (a crop box); answers (the mask's size, [(seconds, mean lift over the mask)]); `mask=` takes a mask from mask_of() of another sweep, so a bolt-free control is measured over the same pixels.
                    A plain difference of two frames of a live scene carries hundreds of pixels of its own motion (peeps, water); the
                    mask measures the thing alone. Check the mask's size: a thing that comes up elsewhere leaves a handful
    A whole-lobby frame is not a usable mask: after `freeze` the lobby's ocean (the lower half of the frame), the bomb mascot's hands and the flyers still move,
                    about 25,000 pixels over 15 grey levels in a second, bolt or no bolt (Q1034); measure the bolt in a box above the water, on a mask of the
                    pixels lit in the first three frames at once (a flyer does not stand still), and give a lobby sweep a pause + stepsweep()
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

def stepsweep(cmd, pat, n, tag, trigger=None, steps=3):
    drain()
    if trigger: send(trigger)
    time.sleep(0.4); out = [(0.0, ask(cmd, pat), grab())]
    for k in range(1, n + 1):
        drain(); send(f"step {steps}"); end = time.monotonic() + 10
        while time.monotonic() < end:
            drain(); send("state")
            got = [tail(l) for l in collect(0.3) if "stepping=" in l]
            if got and "stepping=0" in got[0]: break
        time.sleep(0.25)
        out.append((k * steps / 60.0, ask(cmd, pat), grab()))
    for i, (age, got, im) in enumerate(out):
        im.save(os.path.join(OUT, f"{tag}-{i:02d}.png")); print(f"  {tag} age={age:.3f}s {got}")
    return out

def mask_of(samples, base, box, thr, after=0.05):
    """The mask lift() measures with: the pixels the first sample taken later than `after` seconds lifted by more than `thr`."""
    b = np.array(base.convert("L").crop(box), dtype=float)
    first = next(im for el, _, im in samples if el > after)
    return (np.array(first.convert("L").crop(box), dtype=float) - b) > thr

def lift(samples, base, box, thr, mask=None):
    b = np.array(base.convert("L").crop(box), dtype=float)
    if mask is None: mask = mask_of(samples, base, box, thr)
    return int(mask.sum()), [(el, float((np.array(im.convert("L").crop(box), dtype=float) - b)[mask].mean()) if mask.any() else 0.0)
                             for el, _, im in samples]
