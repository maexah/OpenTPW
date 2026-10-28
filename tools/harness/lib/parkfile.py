"""parkfile.py - ONE reader of a park file (.TPWS / .TPWI) for every confirm script. Do not write another: add to this.

    from parkfile import Park, track, models, head, show        (with tools/harness/lib on sys.path)
    parkfile.py <file>...        prints each file's track rides, cars, their model records and heads
    parkfile.py -e <file>...     prints each file's live emitters instead (show_emitters)
    parkfile.py -t <file>...     prints each file's aerial tags instead (show_tags)
    parkfile.py -a <file>...     prints each file's advisor records instead (show_advisor)
    parkfile.py -s <file>...     prints each file's analyser's last counts and stamps instead (show_analyser)
    parkfile.py -v <file>...     prints each file's arrival vehicles instead: the slots, the lists and each one's record (show_vehicles)
    parkfile.py -l <file>...     prints each file's research lab instead: the kinds, the closing fields and every used project (show_lab)
    parkfile.py -o <file>...     prints each file's objects' customers rings, repair and life, and the all-items row's figures (show_object_days)
    parkfile.py -p <file>...     prints each file's staff patrol corners instead (show_patrols; staff_patrols(p) answers them, Q380)
    parkfile.py -m <file>...     prints each file's analyser money rings instead (show_money; money_rings(p) answers them, Q385)
    parkfile.py -w <file>...     prints each file's park value cells and objects instead (show_park_value; park_value(p, prices) sums them, Q388)
    parkfile.py -x <file>...     prints each file's message sets instead: who hears each of the 29 message types (show_sets; Q397)
    parkfile.py -d <file>...     prints each file's developed cells and its cells by mType instead (show_developed; Q411)
    parkfile.py -g <file>...     prints each file's guests inside, those under 30 happiness, the crowd and the entertainers (show_inside; Q415)
    parkfile.py -L <file>...     prints each file's littered cells and the litter share as FUN_004ccdf0 takes it (show_litter; Q417)
    parkfile.py -P <file>...     prints each file's staff pool slots in use (show_pool; Q422)
    parkfile.py -C <file>...     prints each file's park clock: month and day against the date of its tick (show_calendar; Q526)
    parkfile.py -K <file>...     prints each file's coasters module: the counts, the first coaster's header and pieces (show_coasters; Q593)
    parkfile.py -V <file>...     prints each file's mechanics' and handymen's coverage as the advisor's row 19 and the handymen's 0x0059d383 take it (show_coverage; Q602)
    parkfile.py -B <file>...     prints each file's boundary anchors against their pieces' records (show_boundary; Q620)
    parkfile.py -H <file>...     prints each file's model records' channel counts by item, the walker's reading, and each
                                 item's first record of more than one channel (show_channels; Q778, for Q779)
    parkfile.py -W <file>...     prints each file's weather thing: every field, its stamps as dates and ticks (show_weather; Q528)

    Park(path)        the inflated body, the world block's things, cells and sprites (q241h/pk.py: .b .things .cell
                      .sprite .u32 .u16 .f32 .at(thing) .write(path))
    track(p)          the track-rides module: (bytes, [chunk types], the ride's ten words, [car], leaving, boarding);
                      a car is dict(handle, w = its 43 words, raw, tail = the five words after, riders = [(peep, seat)])
    models(p)         the ride system module: (header, {handle: dict(raw, item, flags, nodes, looks, shared, chan)})
    head(p, slot)     a sprite's kind, bank, program word, state, flags, place and raw record, or None
    particle_density(p) the particles module's density (header +0x20; the original's loaded park runs at it, Q703)
    emitters(p)       the particles module's live emitters: (used chain [(slot, count, effect, (x, h, z) in units,
                      life, particles)], free head, next count); emitter_words(p, slot) is the 16-bit words where
                      an emitter is not its template (Q257v; `parkfile.py -e <file>...` prints them); emitter_aim(p, slot)
                      its own velocity at +0x38, a type 2's aim (Q257w)
    tags(p)           the untagged interface block that ends the body: the aerial's tags, 0x21c bytes each (Q310;
                      `parkfile.py -t <file>...` prints them)
    advisor(p)        the advisor thing's message records not fresh: {message: (tick, line, said, slaps)} (Q313;
                      `parkfile.py -a <file>...` prints them); advisor_raw(p) its 5846 bytes
    analyser(p)       the park analyser's last 76 bytes by name: mLifetimeVisitors, the six calendar stamps
                      (mLastStaffHired at the record's +73524), mMostPaidForTicket... (Q333; `-s` prints them);
                      filetime(tick) is the park calendar on a tick as such a stamp
    vehicles(p)       the arrival vehicles: the header's three slots and current vehicle, the object list, cell (0,0)'s
                      chain and each slot's thing's record by field (Q342; `-v` prints them)
    lab(p)            the research lab (model 14): dict(projects = the used ones as (mObjId, mCat, group, level, mCost,
                      mResearchDone), kinds = five (effort, output, currentProject, highestGroupAvailable), mNumProjects,
                      mWorkLoad, mMostExpensiveProject, mLabActive, raw) (Q349; `parkfile.py -l <file>...` prints them)
    object_days(p, thing)  an object's customers ring, State of repair and remaining life, and the all-items row's
                      customers and bars of them (Q367; `parkfile.py -o <file>...` prints every object's)
    money_rings(p)    the analyser's eight month money rings: the bank's balance at each close (+0x1f100, Q387), all costs
                      (+0x1f5a0), staff costs, training, cash in (+0x1fc90), gate, shop and sideshow takings: {name: dict(current, entries, wrapped, total, newest, months = the closed ones, oldest
                      first)}; the record's sixteen int rings lie from +62598, 589 bytes apiece (Q385; `-m` prints them)
    sets(p)           the message centre's 29 listener sets, the block after the particles' TRAP tag: [tuple of thing ids]
                      by message type, as a load puts them back (FUN_0040fd60; Q397; `-x` prints them)
    litter(p)         the litter share, FUN_004ccdf0: dict(littered = [(x, y, kind, mType)], walked = the cells of mType 1,
                      3 or 9 or littered, share = littered x 100 // walked, nought with none) (Q417; `-L` prints it)
    litter_set(src, dst, cells, kind=1)   a copy of a file with mLitter (the cell's +29) written on each (x, y) of cells,
                      the sprite handle (+35) left as it is: litter neither game draws, which the advisor's rows 8 and 9 read
    thing_set(src, dst, thing, offset, fmt, value)   a copy of a file with one field of one thing's record written
                      over and the block packed again; answers the old value (Q325)
    ring_set(src, dst, [(thing, ring, days, today)])   a copy of a file with objects' day rings (OBJECT_RINGS: customers,
                      walkaways, satisfaction...) written over, days newest first; answers each ring's days before (Q713)
    pool(p)           the staff pool's 32 slots: (slot, mType, mName, mPayGrade, mSubType, mValid, mOnPointer, mTimeSig,
                      mTimeoutTime) (FileFormats saves.md, "The staff pool"; Q422; `parkfile.py -P <file>...` prints those in use)
    pool_set(src, dst, kind, valid=0)   a copy of a file with mValid written on every slot of the pool holding a candidate
                      of mType `kind`, the block packed again; answers the slots it wrote. A pool with nobody of a kind to
                      hire, which the advisor's row 17 asks for (FUN_00508380)
    cells(p)          every cell's (mType, mParentID, track type, track parent) by index y*128 + x, the track record
                      read only where the cell's status has bit 2 (Q469); hoarding(p, x, y) whether it is a type-12 track
                      cell under a type-25 parent, the hover's silent arm 0x004873bd
    effects(p, i)     a cell's five region-effect words (happiness, illness, hunger, security, attraction), nought without
                      its effects sub-record (Q706)
    tickets(p)        the golden tickets' cell measures: (Global 3's covered cells, the cells the Secret counts, those of
                      them with a security word above nought, the whole per cent); `parkfile.py -T <file>...` prints it (Q706)
    show_map(p, x0, y0, x1, y1)   a character map of cells (x0..x1-1, y0..y1-1): H a hoarding, T track 25, then by mType
                      . 0 ground, # 1 path, q 3 queue, o 4 object, 9, A 10, g 30 approach, 2, space 7, ? other;
                      `parkfile.py -c <file> [X0 Y0 X1 Y1]` prints it after the counts by mType and track type (Q469)
    economy(p, thing=8)   the economy thing (model 16, the header's mBankAccount, thing 8 in the jungle files): dict(fee, balance,
                      batch, withdrawals, last, red, profit, loans = eight (available, amount, apr, months, monthly, bought,
                      repaid, lender)) by the FILE's offsets (FileFormats saves.md, "The economy thing"; Q524; `-E` prints it)
    economy_set(src, dst, fields, loans={}, thing=8)   a copy of a file with economy fields written over by name (fee, balance,
                      ...) and loan fields by (slot, name), packed again as thing_set packs it; answers the old economy(p)
    calendar(p)       the park clock's five fields, the 28 bytes before the arrival block (map - 46): dict(start, session as
                      FILETIMEs, month = mMonthAtLastUpdate nought-based, day = mDayAtLastUpdate, rate, tick = mGameTick,
                      date = the date of the tick, matches = whether month and day are that date's, as in every file the
                      original saved past tick 0) (FileFormats saves.md, "The arrival and clock fields"; Q526; `-C` prints it)
    weather(p)        the weather thing (model 15, the header's mWeather, thing 7): dict(quality, changed, forecasted =
                      FILETIMEs, f_time, f_quality, f_valid = the forecast, drops, target, lightning, countdown, struck,
                      thunder, location = three floats or None for the 0xcdcdcdcd fill, voice = mRainSoundHandle, ptype, wind,
                      force, overridden, at = the record) by the FILE's offsets (FileFormats saves.md, "The weather"; Q528;
                      `-W` prints it, the stamps as dates and as ticks of the file's clock)
    weather_set(src, dst, fields)   a copy of a file with weather fields written over by name, packed as economy_set packs it
    analyser_grid(p)  the analyser's +16..+62,598 (FileFormats saves.md, "The park analyser"; Q546): dict(grid = the five
                      dwords 4, 32, 32, 128, 128; blocks = 1,024 (x, y, w, h, nine (value, worked-out) pairs, the first two
                      values floats); rings = four (current, count, wrapped, temp, three u16); kept = six (value, worked-out))
    sound(p)          the sound block after SVDA (Q546): dict(on, second, n = 84, words = the three dwords, length L,
                      ends = whether the records end at NUOS, +12+L)
    voices(p)         the sound manager's voice list after the sound block's 84 bytes (Q560; FileFormats saves.md, "The
                      voice list"): dict(L, count, cats = the library's category names by index, blocks = the pool's
                      100-slot blocks, voices = [dict(handle, type, kind, pos, level, range, cat, effect = the index in
                      its category, variation, sample (nought-based), keys, values, ctl = the four words +8..+0x14,
                      held = the 0x1000 bit, child = a child's record or None)], ends = whether the list ends at L's end);
                      None with sound off. effect_id(cat, index) the effect's id from its SFX map (sfxmap.py)
    script_objects(p) every script's object list (ESSR): [(script handle, thing, [(type, handle, node, lookup, tag)])] (Q560)
                      `parkfile.py -N <file>...` prints the voices, each with the script record or weather that holds its handle
    coasters(p)       the coasters module (SAOC): (the four counts, [dict(header, partners, pieces = each piece's flags,
                      mesh, cell x and y on the 96-wide coaster grid, ordinal, loft, bank, incline, yaw, loop, partners)])
                      (FileFormats saves.md, "The coasters module"; Q593; `-K` prints it)
    channels(p)       the ride system module's records by item: {item: Counter(channels)} and {item: (offset, channels)} of
                      each item's first record of more than one, by rsys.walk's reading (Q778)
    chunks, walk      the two walkers themselves (q257o/kart.py, q257o/rsys.py)

The Park, chunks and walk code is not here: it is imported from pk.py, kart.py and rsys.py beside it. Importing this raises Python's recursion limit to 100000."""
import sys, struct
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.setrecursionlimit(100000)
import kart, rsys
from pk import Park
chunks = kart.chunks; walk = rsys.walk
def track(p):
    """(module bytes, [chunk types], ride words, [car dict], leaving, boarding)"""
    start, end, cs = kart.chunks(bytes(p.b)); cars = []; ride = None; leaving = []; boarding = []
    for t, at, body in cs:
        w = struct.unpack_from('<%dI' % (len(body) // 4), body)
        if t == 3: ride = w
        if t == 5: cars.append(dict(handle=w[0], w=list(struct.unpack_from('<43I', body, 4)), raw=bytes(body[4:4 + 0xac]), tail=list(struct.unpack_from('<5I', body, 4 + 0xac)), riders=[]))
        if t == 9 and cars: cars[-1]['riders'].append((w[1], w[2]))
        if t == 7: leaving.append(w[1])
        if t == 8: boarding.append(w[1])
    return bytes(p.b[start:end]), [c[0] for c in cs], ride, cars, leaving, boarding
def models(p):
    """(header, {handle: dict(raw, item, flags, nodes, looks, shared, chan)})"""
    g, hdr, recs = rsys.walk(p); b = p.b; out = {}
    for slot, r in enumerate(recs or []):
        if not r: continue
        at, ch, base = r; raw = bytes(b[at:base + 44 * ch]); n, l = struct.unpack_from('<HH', raw, 0x2b)
        out[slot + 1] = dict(raw=raw, item=struct.unpack_from('<I', raw, 1)[0], flags=struct.unpack_from('<I', raw, 0x15)[0],
            looks=[struct.unpack_from('<Ii', raw, 0x37 + 8 * i) for i in range(l)], nodes=list(struct.unpack_from('<%dI' % n, raw, 0x37 + 8 * l)),
            shared=struct.unpack_from('<II', raw, 0x2f), chan=[struct.unpack_from('<IiiIIIfiiIf', raw, 0x37 + 8 * l + 4 * n + 44 * c) for c in range(ch)])
    return hdr, out
def head(p, slot):
    """a sprite's (kind, bank, +0x08, state +0x18, flags +0xc4, place) or None"""
    if slot not in p.sprite: return None
    r = bytes(p.b[p.sprite[slot]:p.sprite[slot] + 280]); u = lambda o: struct.unpack_from('<I', r, o)[0]
    return dict(kind=u(0xac), bank=u(0xb0), word=u(0x08), state=u(0x18), flags=u(0xc4), place=struct.unpack_from('<3f', r, 0x88), raw=r)
def _particles(p):
    b = p.b; o = p.spend + 4; assert b[o:o+4] == b'LCTP', b[o:o+4]
    assert struct.unpack_from('<II', b, o+4) == (1, 0x9e68)
    live = o + 12; assert p.u32(live + 0x9e68) == 0x8b60
    return live, live + 0x9e68 + 4
def emitters(p):
    """(the used chain, head first: [(slot, count, effect, (x, h, z) in whole units, life, particles)], the free chain's head, the next count)"""
    live, tmpl = _particles(p); b = p.b; em = live + 0x2c
    used, _, free, _ = struct.unpack_from('<4h', b, live + 0x9e4c); out = []; s = used
    while s >= 0 and len(out) < 120:
        e = em + s*0x140
        out.append((s, struct.unpack_from('<H', b, e+0xa)[0], struct.unpack_from('<h', b, e+0xc)[0],
                    tuple(v // 64 for v in struct.unpack_from('<3i', b, e+0x14)), struct.unpack_from('<i', b, e+0x20)[0], struct.unpack_from('<H', b, e+8)[0]))
        s = struct.unpack_from('<h', b, e+0xd0)[0]
    return out, free, struct.unpack_from('<H', b, live + 0x18)[0]
def particle_density(p):
    """the particle density the file holds (the live header's +0x20): the original's loader FUN_0051f7a0 copies the
    header into 0x0080ced0, so a loaded park scales every effect it starts by this, not by the options' (Q703)"""
    live, _ = _particles(p); return struct.unpack_from('<i', p.b, live + 0x20)[0]
def emitter_words(p, slot):
    """the offsets of the 16-bit words where the emitter in a slot is not the template it names"""
    live, tmpl = _particles(p); b = p.b; e = live + 0x2c + slot*0x140; t = tmpl + struct.unpack_from('<h', b, e+0xc)[0]*0x140
    return [o for o in range(0, 0x140, 2) if b[e+o:e+o+2] != b[t+o:t+o+2]]
def emitter_aim(p, slot):
    """an emitter's own velocity, the three words at +0x38: a type 2's aim (Q257w)"""
    live, tmpl = _particles(p); return struct.unpack_from('<3i', p.b, live + 0x2c + slot*0x140 + 0x38)
def show_emitters(p):
    used, free, nxt = emitters(p); print(' ', len(used), 'emitters in use, free head', free, 'next count', nxt)
    for u in used: print('   slot %3d count %5d handle 0x%x effect %3d at %s life %d particles %d aim %s; not its template at %s' % (u[0], u[1], u[1] << 16 | u[0], u[2], u[3], u[4], u[5], emitter_aim(p, u[0]), ' '.join('%x' % o for o in emitter_words(p, u[0]))))
def show(p):
    mod, kinds, ride, cars, leaving, boarding = track(p); hdr, ms = models(p)
    print(' track module', len(mod), 'bytes:', ' '.join(map(str, kinds))); print(' ride', ride and ' '.join('%x' % x for x in ride), 'leaving', leaving, 'boarding', boarding)
    print(' models header', hdr, 'live sprites', len(p.sprite))
    for i, c in enumerate(cars):
        w = c['w']; print(f"  car {i}: flags {w[0]:x} mesh {w[1]} models {w[2]},{w[3]} anim {w[4]} em {w[9]},{w[10]} at ({w[13]},{w[14]}) +50 {w[20]} +54 {w[21]} +58 {w[22]} buoy {w[31]:x} timer {w[34]} phase {w[36]} +a0 {struct.unpack('<f', struct.pack('<I', w[40]))[0]:.4f} +a4 {w[41]:x} tail {' '.join('%x' % x for x in c['tail'])} riders {c['riders']}")
        for h in (w[2], w[3]):
            m = ms.get(h)
            if not m: print('    model', h, 'NONE'); continue
            print(f"    model {h}: item {m['item']} flags {m['flags']:x} shared {m['shared']} nodes {' '.join('%x' % x for x in m['nodes'])} looks {m['looks']} chan {m['chan']}")
            for f, s in m['looks']:
                if f & 2: hd = head(p, s); print('      head sprite', s, hd and {k: v for k, v in hd.items() if k != 'raw'})
TAG = 0x21c
def tags(p):
    """the untagged interface block after CSDA, the aerial's tags (FUN_0048ee50): [dict(id, text, life, picture,
    buttons, thing, item, raw, after = the bytes after the text's terminator, the writer's stack)]"""
    b = bytes(p.b); at = b.rfind(b'CSDA') + 4; n = struct.unpack_from('<I', b, at)[0]
    assert len(b) == at + 4 + n * TAG, (len(b) - at, n)
    out = []
    for k in range(n):
        r = b[at + 4 + k * TAG:at + 4 + (k + 1) * TAG]; w = struct.unpack_from('<256H', r, 4); z = w.index(0)
        out.append(dict(id=struct.unpack_from('<I', r)[0], text=''.join(map(chr, w[:z])), life=struct.unpack_from('<i', r, 0x204)[0],
            picture=struct.unpack_from('<H', r, 0x208)[0], buttons=struct.unpack_from('<II', r, 0x20c), thing=struct.unpack_from('<H', r, 0x214)[0],
            item=struct.unpack_from('<i', r, 0x218)[0], pads=(r[0x20a:0x20c].hex(), r[0x216:0x218].hex()), raw=r, after=r[4 + 2 * z + 2:0x204]))
    return out
def show_tags(p):
    ts = tags(p); print(' ', len(ts), 'tags')
    for t in ts: print('   id %d life %d picture %d buttons %s thing %d item %d pads %s %s; %d of %d bytes after the text not nought\n     %r' % (t['id'], t['life'], t['picture'], t['buttons'], t['thing'], t['item'], *t['pads'], sum(1 for c in t['after'] if c), len(t['after']), t['text']))
def advisor(p):
    """the advisor thing's message records (model 11, +0xcc + id * 0x10; Q313): {message: (tick, line, said, slaps)}
    for every record not as the constructor leaves it, (0, -1, 0, 0); advisor_raw(p) is the thing's 5846 bytes"""
    raw = advisor_raw(p); out = {}
    for m in range(351):
        r = struct.unpack_from('<IiB3xI', raw, 0xcc + 16 * m)
        if r != (0, -1, 0, 0): out[m] = r
    return out
def advisor_slots(p):
    """the advisor thing's eight slots, the messages waiting to be said (21 bytes at 24 + 21 n, FUN_0059ba30; Q314):
    [(thing, secondary, message, score, valid, line, once, overridden)], taken or not; advisor_clocks(p) is
    (mTimeSig, mSoundId, mNextAvailableQueuePos, mLastActionStarted, mLastActionDuration, the kept message as a slot,
    mRequestedLastTutorial, mMessagePlaying)"""
    raw = advisor_raw(p); return [struct.unpack_from('<HiiiBiBB', raw, 24 + 21 * n) for n in range(8)]
def advisor_clocks(p):
    raw = advisor_raw(p)
    return struct.unpack_from('<Ii', raw, 16) + struct.unpack_from('<iII', raw, 192) + (struct.unpack_from('<HiiiBiBB', raw, 5820),) + struct.unpack_from('<Bi', raw, 5841)
def clock(p):
    """The clock module's two dwords (KOLC): the clock's reading and the second stopwatch's, which the advisor's last action is timed on."""
    b = bytes(p.b); at = b.find(b'SSEM')
    while at >= 0 and b[at + 12:at + 16] != b'KOLC': at = b.find(b'SSEM', at + 1)
    return struct.unpack_from('<II', b, at + 4)
def sets(p):
    """The message centre's listener sets: 29 tuples of thing ids, one per message type, each ascending. The block
    starts after the particles' TRAP tag and ends at the SSEM tag the clock's KOLC follows (FileFormats saves.md,
    "The message centre module"; a load replaces the running sets with these, FUN_0040fd60; Q397)."""
    b = bytes(p.b); at = b.find(b'TRAP') + 4; assert struct.unpack_from('<I', b, at)[0] == 29; at += 4; out = []
    for _ in range(29):
        n = struct.unpack_from('<I', b, at)[0]; out.append(struct.unpack_from('<%dH' % n, b, at + 4)); at += 4 + 2 * n
    assert b[at:at + 4] == b'SSEM', 'the sets do not end at SSEM'
    return out
def show_sets(p):
    for n, s in enumerate(sets(p)): print('   0x%02x: %s' % (n, ' '.join(map(str, s)) or '-'))
def advisor_raw(p):
    t = [t for t in p.things if p.model(t) == 11][0]; return bytes(p.b[p.at(t):p.at(t) + 5846])
def advisor_forget(src, dst, message):
    """A copy of a park file whose advisor has never said `message`: its record made the constructor's (tick 0, line -1,
    said 0, slaps 0; the three bytes after the said byte left), the BILZ block packed again as research.py packs it.
    Says the record it took out. A jungle file's header alone is known (the block at 0x60d)."""
    import zlib
    data = open(src, 'rb').read(); assert data[0x60d:0x611] == b'BILZ'
    p = Park(src); b = bytearray(zlib.decompress(data[0x629:])); assert bytes(b) == bytes(p.b)
    t = [t for t in p.things if p.model(t) == 11][0]; at = p.at(t) + 204 + message * 16
    was = struct.unpack_from('<IiB3xI', b, at)
    struct.pack_into('<Ii', b, at, 0, -1); b[at + 8] = 0; struct.pack_into('<I', b, at + 12, 0)
    packed = zlib.compress(bytes(b), 9); head = bytearray(data[:0x629])
    struct.pack_into('<II', head, 0x611, len(b), 0x629 - 0x60d + len(packed))
    open(dst, 'wb').write(bytes(head) + packed); return was
def thing_set(src, dst, thing, offset, fmt, value):
    """A copy of a park file with one field of one thing's record written over: `struct` format `fmt` at the record's
    `offset` (the FILE's offsets, ParkWorld.cs's: an object's mCanLoad is 214, '<i'), the BILZ block packed again as
    advisor_forget packs it. Answers the value it replaced. A jungle file's header alone is known. How a state no
    screen of either game can reach is put in a file: a staff room shut is thing_set(src, dst, 20, 214, '<i', 0) (Q325)."""
    import zlib
    data = open(src, 'rb').read(); assert data[0x60d:0x611] == b'BILZ'
    p = Park(src); b = bytearray(zlib.decompress(data[0x629:])); assert bytes(b) == bytes(p.b)
    at = p.at(thing) + offset; was = struct.unpack_from(fmt, b, at)[0]; struct.pack_into(fmt, b, at, value)
    packed = zlib.compress(bytes(b), 9); head = bytearray(data[:0x629])
    struct.pack_into('<II', head, 0x611, len(b), 0x629 - 0x60d + len(packed))
    open(dst, 'wb').write(bytes(head) + packed); return was
OBJECT_RINGS = {'costs': 228, 'takings': 361, 'customers': 498, 'walkaways': 635, 'served': 768, 'satisfaction': 901}
def ring_set(src, dst, edits):
    """A copy of a park file with objects' day rings written over: `edits` is [(thing, ring, days, today)], `ring` a name
    of OBJECT_RINGS (the record's offsets, ParkWorld.Objects.cs's RingsAt), `days` the finished days NEWEST FIRST written
    from the ring's entry backwards (None leaves them), `today` its mTemp (None leaves it); the entry, count and wrap
    byte are kept, so a ring holds no more days than it has filled. Packed again as thing_set packs. Answers each ring's
    days before, newest first. How a shop's walk-away share or satisfaction is set for the advisor's rows 238 to 257 (Q713)."""
    import zlib
    data = open(src, 'rb').read(); assert data[0x60d:0x611] == b'BILZ'
    p = Park(src); b = bytearray(zlib.decompress(data[0x629:])); assert bytes(b) == bytes(p.b); was = []
    for thing, ring, days, today in edits:
        at = p.at(thing) + OBJECT_RINGS[ring]
        entry, num = struct.unpack_from('<ii', b, at); filled = num if b[at + 8] else entry + 1
        was.append(ring_last(b, at))
        if days is not None:
            assert len(days) <= filled, f"thing {thing}'s {ring} ring holds {filled} days"
            for i, v in enumerate(days): struct.pack_into('<i', b, at + 13 + 4 * ((entry - i) % num), v)
        if today is not None: struct.pack_into('<i', b, at + 9, today)
    packed = zlib.compress(bytes(b), 9); head = bytearray(data[:0x629])
    struct.pack_into('<II', head, 0x611, len(b), 0x629 - 0x60d + len(packed))
    open(dst, 'wb').write(bytes(head) + packed); return was
def pool(p):
    return [(n,) + struct.unpack_from('<iiBBBBii', p.b, p.pool + 20 * n) for n in range(32)]
def show_pool(p):
    for r in pool(p):
        if r[5]: print('   slot %2d kind %d name %2d grade %d sub %d sig %d timeout %d' % (r[0], r[1], r[2], r[3], r[4], r[7], r[8]))
def pool_set(src, dst, kind, valid=0):
    import zlib
    data = open(src, 'rb').read(); assert data[0x60d:0x611] == b'BILZ'
    p = Park(src); b = bytearray(zlib.decompress(data[0x629:])); assert bytes(b) == bytes(p.b)
    wrote = [r[0] for r in pool(p) if r[5] and r[1] == kind]
    for n in wrote: b[p.pool + 20 * n + 10] = valid
    packed = zlib.compress(bytes(b), 9); head = bytearray(data[:0x629])
    struct.pack_into('<II', head, 0x611, len(b), 0x629 - 0x60d + len(packed))
    open(dst, 'wb').write(bytes(head) + packed); return wrote
def litter(p):
    """The share of the map's walked cells that hold litter, as FUN_004ccdf0 takes it for the advisor's rows 8 and 9: every
    cell, a cell counted where its mType is 1 (path), 3 or 9 (FUN_00536310, FUN_00536320) or its mLitter is not nought,
    and littered where mLitter is not nought; littered x 100 // counted, nought with none counted (Q417)."""
    littered = []; walked = 0
    for i in range(16384):
        at = p.cell[i] + 1; kind = struct.unpack_from('<i', p.b, at + 29)[0]; t = struct.unpack_from('<i', p.b, at + 24)[0]
        if kind: littered.append((i % 128, i // 128, kind, t))
        if kind or t in (1, 3, 9): walked += 1
    return dict(littered=littered, walked=walked, share=len(littered) * 100 // walked if walked else 0)
def show_litter(p):
    l = litter(p); print('   littered %d of %d walked cells: share %d' % (len(l['littered']), l['walked'], l['share']))
    for x, y, kind, t in l['littered'][:40]: print('     (%d,%d) kind %d mType %d' % (x, y, kind, t))
COVERAGE_TYPES = {4: (9,), 5: (1, 3, 9)}
def coverage(p, role=4):
    """The advisor's role coverage as 0x004cd340 / 0x004cd500 take it (Q602; advisor-park.md, "The mechanics' patrol
    areas"): staff of model `role` (4 mechanics, mode 2: mType 9; 5 handymen, mode 3: mType 1, 3, 9; the entertainers'
    mode 1 is not read), those with an area (corners not (1, 16384), 0x00506eb0), and the counted cells inside any of their
    rectangles, corners inclusive. Answers dict(staff, with_area, cells, covered, share = covered x 100 // cells)."""
    cell = lambda c: ((c - 1) & 0x7f, (c - 1) >> 7)
    staff = {t: (bl, tr) for t, (m, bl, tr) in staff_patrols(p).items() if m == role}
    area = [(cell(bl), cell(tr)) for bl, tr in staff.values() if (bl, tr) != (1, 16384) and (bl, tr) != (0, 0)]
    cells = [i for i in range(16384) if struct.unpack_from('<i', p.b, p.cell[i] + 1 + 24)[0] in COVERAGE_TYPES[role]]
    covered = sum(1 for i in cells if any(x0 <= i % 128 <= x1 and y0 <= i // 128 <= y1 for (x0, y0), (x1, y1) in area))
    return dict(staff=len(staff), with_area=sum(1 for v in staff.values() if v != (1, 16384)), cells=len(cells),
                covered=covered, share=covered * 100 // len(cells) if cells else 0)
def show_coverage(p):
    for role in COVERAGE_TYPES: print('   role %d: %s' % (role, coverage(p, role)))
def litter_set(src, dst, cells, kind=1):
    """A copy of a park file with mLitter (the cell record's +29 past its status byte) set to kind on each (x, y) of
    cells, packed again as thing_set packs. The sprite handle a drop makes (mLitterScript, +35) is left as it is, so
    neither game draws it; the advisor's litter share (FUN_004ccdf0) reads mLitter alone (Q417)."""
    import zlib
    data = open(src, 'rb').read(); assert data[0x60d:0x611] == b'BILZ'
    p = Park(src); b = bytearray(zlib.decompress(data[0x629:])); assert bytes(b) == bytes(p.b)
    for x, y in cells: struct.pack_into('<i', b, p.cell[y * 128 + x] + 1 + 29, kind)
    packed = zlib.compress(bytes(b), 9); head = bytearray(data[:0x629])
    struct.pack_into('<II', head, 0x611, len(b), 0x629 - 0x60d + len(packed))
    open(dst, 'wb').write(bytes(head) + packed)
ANALYSER_TAIL = ('mMisbehavingKids', 'mMisbehavingKidsThatGotAwayWithIt', 'mMisbehavingKidsEver', 'mLifetimeVisitors', 'mLastStrike', 'mLastPuke',
                 'mLastPrank', 'mParkLastOpened', 'mLastObjectBuilt', 'mLastStaffHired', 'mMostPaidForTicket', 'mLargestBusLoad', 'mLongestStay')
def staff_patrols(p):
    """Every member of staff's patrol corners as the file holds them, {thing: (model, bl, tr)}: the packed cell ids at
    the record's +476 and +478 (mPatrolRegionBL, mPatrolRegionTR). 1 and 16384 is the whole map, as the original's
    constructor writes it; nought and nought is no area, which the original puts outside every cell (Q380)."""
    return {t: (m, p.u16(a + 476), p.u16(a + 478)) for t, (a, m) in p.things.items() if m in (4, 5, 6, 7, 8)}
def show_patrols(p):
    for t, (m, bl, tr) in staff_patrols(p).items():
        cell = lambda c: ((c - 1) & 0x7f, (c - 1) >> 7)
        print('   thing %d model %d: %d %d = %s-%s%s' % (t, m, bl, tr, cell(bl), cell(tr), '  the whole map' if (bl, tr) == (1, 16384) else '  NO AREA' if (bl, tr) == (0, 0) else ''))
def filetime(tick, rate=15000):
    """The park calendar on a tick as a FILETIME: 2000-01-01 plus tick x rate / 4 seconds (the jungle's rate is 15000)."""
    return (12591158400 + tick * rate // 4) * 10**7
def analyser(p):
    """The park analyser's last 76 bytes (the header's analyser thing, model 13, record +73468): four counts, six
    FILETIME stamps of the park's calendar and three records, by the serialiser's own names (FUN_004c5d70; Q333).
    mLastStaffHired is at the record's +73524: thing_set(src, dst, 5, 73524, '<q', filetime(tick)) writes it."""
    return dict(zip(ANALYSER_TAIL, struct.unpack_from('<4i6q3i', p.b, p.at(p.get('analyser')) + 73468)))
def show_analyser(p):
    import datetime
    for k, v in analyser(p).items():
        stamp = k.startswith('mLast') or k == 'mParkLastOpened'
        print('   %-34s %s' % (k, '%d  %s  tick %.2f' % (v, datetime.datetime(1601, 1, 1) + datetime.timedelta(microseconds=v // 10), (v // 10**7 - 12591158400) / 3750) if stamp else v))
    hires = [struct.unpack_from('<q', p.b, a + 491)[0] for t, (a, m) in p.things.items() if m in (4, 5, 6, 7, 8)]
    print('   newest mTimeHired of the staff: %s' % (max(hires) if hires else 'no staff'))
def show_advisor(p):
    for n, t in enumerate(advisor_slots(p)):
        if t[4]: print('   slot %d: thing %d secondary %d message 0x%x (%d) score %d line %d once %d overridden %d' % (n, t[0], t[1], t[2], t[2], t[3], t[5], t[6], t[7]))
    c = advisor_clocks(p); print('   last action %d + %d ms, playing 0x%x, kept message 0x%x valid %d, tutorial byte %d, next queue pos %d' % (c[3], c[4], c[7] & 0xffffffff, c[5][2] & 0xffffffff, c[5][4], c[6], c[2]))
    k = clock(p); print('   clock %d, second stopwatch %d: the wait %s' % (k[0], k[1], 'is over by %d ms' % (k[1] - c[3] - c[4]) if k[1] >= c[3] + c[4] else 'has %d ms left' % (c[3] + c[4] - k[1])))
    for m, r in advisor(p).items(): print('   message 0x%x (%d): tick %d line %d said %d slaps %d' % (m, m, *r))
def vehicles(p):
    """The arrival vehicles as a file holds them (Q342): dict(slots = mArrivalVehicle_Size1..3, current, first = mFirstObject,
    objects = the object list from it by each record's +208, corner = cell (0,0)'s chain from its mWho by each thing's +12,
    things = {thing: its record's fields}) for each slot's thing. A vehicle made at its summons is an object on cell (0,0)."""
    g = p.get; u16 = lambda a, o: struct.unpack_from('<H', p.b, a + o)[0]; i32 = lambda a, o: struct.unpack_from('<i', p.b, a + o)[0]
    def walk(t, off):
        out = []
        while t and t in p.things and len(out) < 600: out.append(t); t = u16(p.at(t), off)
        return out
    things = {}
    for t in (g('av1'), g('av2'), g('av3')):
        if not t or t not in p.things: continue
        a = p.at(t)
        things[t] = dict(x=u16(a, 8), y=u16(a, 10), cellnext=u16(a, 12), cellprev=u16(a, 14), angle=i32(a, 16), item=u16(a, 20),
            date=tuple(i32(a, 22 + 4 * i) for i in range(8)), model=i32(a, 54), flags=u16(a, 58),
            name=''.join(chr(u16(a, 60 + 4 * i)) for i in range(32)).split('\0')[0], script=i32(a, 192), state=i32(a, 200),
            topleft=u16(a, 204), entry=u16(a, 206), next=u16(a, 208), canload=i32(a, 214), exit=u16(a, 218), raw=bytes(p.b[a:a + 1099]))
    return dict(slots=(g('av1'), g('av2'), g('av3')), current=g('curav'), first=g('firstObject'), objects=walk(g('firstObject'), 208),
                corner=walk(p.who(0, 0), 12) if p.b[p.cell[0]] & 1 else [], things=things)
def show_vehicles(p):
    v = vehicles(p); print('   slots %s current %d mFirstObject %d tick %d; object list %s; cell (0,0): %s' % (v['slots'], v['current'], v['first'], p.get('tick'), v['objects'][:6], v['corner']))
    for t, r in v['things'].items(): print('   thing %d: %s' % (t, ' '.join('%s %s' % (k, r[k]) for k in r if k != 'raw')))
def lab(p):
    """The research lab's record (the thing of model 14, 4190 bytes; FileFormats saves.md, "The research lab"): the used
    projects, the five kinds' records and the four closing fields. The projects past mNumProjects are in raw (Q349)."""
    t = [t for t in p.things if p.model(t) == 14][0]; a = p.at(t); raw = bytes(p.b[a:a + 4190])
    n, work, dear, active = struct.unpack_from('<iiiB', raw, 4177)
    return dict(thing=t, projects=[struct.unpack_from('<HiBBff', raw, 16 + 16 * i) for i in range(n)],
                kinds=[struct.unpack_from('<iiiB', raw, 4112 + 13 * i) for i in range(5)],
                mNumProjects=n, mWorkLoad=work, mMostExpensiveProject=dear, mLabActive=active, raw=raw)
def show_lab(p):
    l = lab(p); print('   lab thing %d: mNumProjects %d mWorkLoad %d mMostExpensiveProject %d mLabActive %d' % (l['thing'], l['mNumProjects'], l['mWorkLoad'], l['mMostExpensiveProject'], l['mLabActive']))
    for i, k in enumerate(l['kinds']): print('   kind %d: effort %d output %d currentProject %d highestGroupAvailable %d' % (i, *k))
    for i, r in enumerate(l['projects']): print('   project %2d: item %d cat %d group %d level %d cost %g done %g' % (i, *r))
def object_days(p, thing):
    """An object's customers ring (file +498: entry, count, wrap byte, today, thirty days), its State of repair (+1074)
    and remaining life (+1070), and what the all-items row shows of them: the last min(30, filled) finished days added,
    today's not among them, and each float's bar ((int(x) & 0xff) << 10) // 100 (Q367; `-o` prints every object's).
    And the shops' and sideshows' columns (Q368): the takings (+361) less the costs (+228) rings' last thirty, each day
    unsigned (profit30); mTotalTakings (+1090) less mTotalCosts (+1086); 50 plus the satisfaction ring's (+901) mean
    truncated toward nought, and its bar."""
    a = p.at(thing); b = p.b
    entry, num = struct.unpack_from('<ii', b, a + 498); wrap = b[a + 506]; today = struct.unpack_from('<i', b, a + 507)[0]
    days = struct.unpack_from('<30i', b, a + 511); life, repair = struct.unpack_from('<ff', b, a + 1070)
    filled = num if wrap else entry + 1; last = [days[(entry - i) % num] for i in range(min(30, filled))] if num else []
    bar = lambda x: ((int(x) & 0xff) << 10) // 100
    costs, takings, satis = (ring_last(b, a + o) for o in (228, 361, 901))
    tcost, ttake = struct.unpack_from('<ii', b, a + 1086)
    sat = 50 + (int(sum(satis) / len(satis)) if satis else 0)
    wrap32 = lambda v: (v + 2**31) % 2**32 - 2**31
    return dict(entry=entry, num=num, wrap=wrap, today=today, filled=filled, last30=sum(d & 0xffffffff for d in last),
                repair=repair, life=life, repair_bar=bar(repair), life_bar=bar(life),
                takings30=sum(d & 0xffffffff for d in takings), costs30=sum(d & 0xffffffff for d in costs),
                profit30=wrap32(sum(d & 0xffffffff for d in takings) - sum(d & 0xffffffff for d in costs)),
                total=wrap32(ttake - tcost), takings_total=ttake, costs_total=tcost, sat_days=len(satis),
                satisfaction=sat, satisfaction_bar=bar(sat))
MONEY_RINGS = (('balance', 0), ('value', 1), ('costs', 2), ('staff', 3), ('train', 4), ('cashin', 5), ('gate', 6), ('shops', 7), ('sshows', 8), ('inside', 9), ('visits', 10), ('rides', 11), ('arrived', 12), ('happy', 13), ('stay', 14), ('score', 15))
FLOAT_RINGS = {'happy'}
def money_rings(p):
    """The analyser's eight money rings (Q385; training, gate, shops, sideshows Q386; the bank's balance at each close Q387, no month so far; the park value Q388 and the lifetime visitors at each close Q389, the 2nd and 11th, none either; the guests inside and the rides standing at each close Q390, the 10th and 12th, `inside` and `rides`, none either; the guests the arrival vehicles dropped Q391, the 13th, `arrived`, with a month so far; the mean happiness of the guests inside at each close Q392, the 14th, `happy`, its months FLOATS, no month so far; the whole days' mean of the last fifty leavers' stays at each close Q393, the 15th, `stay`, none either (the fifty are stays()); the park score out of 100 at each close Q394, the 16th, `score` (FUN_004cceb0; q394/sweep.py reckons it), none either: each ring is mCurrentEntry, mNumEntries, mWrappedAround (a byte), mTemp
    (the month so far) and 144 months; newest is the hire screen's read (0x0049c54f)."""
    base = p.at(p.get('analyser')) + 62598; out = {}
    for name, k in MONEY_RINGS:
        at = base + 589 * k
        cur, num = struct.unpack_from('<ii', p.b, at); wrap = p.b[at + 8]; total = struct.unpack_from('<i', p.b, at + 9)[0]
        months = struct.unpack_from('<144f' if name in FLOAT_RINGS else '<144i', p.b, at + 13)
        newest = 0 if num <= 0 else months[cur] if cur >= 0 else months[cur + num] if wrap else 0
        closed = [months[(cur - i) % num] for i in range(num if wrap else cur + 1)][::-1] if num else []
        out[name] = dict(current=cur, entries=num, wrapped=wrap, total=total, newest=newest, months=closed)
    return out
def stays(p):
    """The analyser's ring of the last fifty leavers' stays in whole park days (+0x21600, FUN_004c7600 from 0x00502333; Q393),
    the record's +72022 after the sixteen month rings, 213 bytes: mCurrentEntry, mNumEntries (50), mWrappedAround, mTemp
    and 50 dwords. Answers dict(current, entries, wrapped, total, stays = oldest first, mean = what the month's close
    pushes into the 15th ring: FUN_004cef30 over the newest min(50, filled), the sum over the count truncated, 0 with none)."""
    at = p.at(p.get('analyser')) + 72022
    cur, num = struct.unpack_from('<ii', p.b, at); wrap = p.b[at + 8]; total = struct.unpack_from('<i', p.b, at + 9)[0]
    data = struct.unpack_from('<50i', p.b, at + 13)
    filled = [data[(cur - i) % num] for i in range(min(50, num if wrap else cur + 1))] if num else []
    return dict(current=cur, entries=num, wrapped=wrap, total=total, stays=filled[::-1], mean=int(sum(filled) / len(filled)) if filled else 0)
def loads(p):
    """The arrival manager's ring of the last five loads (+0x216d8 mTemp, +0x216dc five loads, slot +0x216f0, count +0x216f4,
    wrapped +0x216f8; FUN_004cf3e0 pushes each load's size as the load is called, 0x004cf41f..0x004cf451; Q396), the
    record's +72235 after the stays, 33 bytes: mCurrentEntry, mNumEntries (5), mWrappedAround, mTemp and 5 dwords.
    Answers dict(current, entries, wrapped, total, loads = oldest first, called = how many FUN_005a0780 counts:
    mNumEntries once wrapped, else mCurrentEntry + 1)."""
    at = p.at(p.get('analyser')) + 72235
    cur, num = struct.unpack_from('<ii', p.b, at); wrap = p.b[at + 8]; total = struct.unpack_from('<i', p.b, at + 9)[0]
    data = struct.unpack_from('<5i', p.b, at + 13)
    filled = [data[(cur - i) % num] for i in range(num if wrap else cur + 1)] if num else []
    return dict(current=cur, entries=num, wrapped=wrap, total=total, loads=filled[::-1], called=num if wrap else cur + 1, raw=list(data))
PARK_VALUE_TYPES = {0: 2, 1: 3, 2: 1, 3: 5, 9: 6, 10: 1}
def park_value(p, prices=None):
    """The park value as FUN_004c7db0 sums it (Q388; OpenTPW docs/exe/hud.md, "The park value"), from the file alone: every
    cell without mFlags 0x40 adds by mType (0: 2, 1: 3, 2: 1, 3: 5, 9: 6, 10: 1) and 50 where its track type, or for a
    track type 12/17 its parent's (none with no parent), is 11, 13, 16, 18 or 25; every object (model 3) not condemned
    (mState 4) adds the item's tier costs 0..mUpgradeLevel (+1098) times int(State of repair, +1074) & 0xff over 100,
    truncated toward nought. prices = {item id: [tier 0, 1, 2 CostOfUpgrade]}; without it the objects are listed and
    their sum is None. Answers dict(cells, types = {mType: count of cells in the park}, track, objects = [(thing, item,
    state, level, repair, share)], objects_sum, value)."""
    def rec(i):
        at = p.cell[i]; s = p.b[at]
        if not s & 1: return None
        m = at + 1; t = struct.unpack_from('<i', p.b, m + 24)[0]; fl = struct.unpack_from('<H', p.b, m + 1)[0]
        tt, par = (struct.unpack_from('<i', p.b, m + 52 + 24)[0], struct.unpack_from('<H', p.b, m + 52 + 10)[0]) if s & 2 else (0, 0)
        return t, fl, tt, par
    recs = [rec(i) for i in range(16384)]; types = {}; cells = 0; track = 0
    for r in recs:
        if r is None or r[1] & 0x40: continue
        t, fl, tt, par = r; types[t] = types.get(t, 0) + 1; cells += PARK_VALUE_TYPES.get(t, 0)
        if tt in (12, 17): tt = recs[par - 1][2] if par and recs[par - 1] else 0
        if tt in (11, 13, 16, 18, 25): track += 1; cells += 50
    objs = []; total = 0
    for th in p.things:
        if p.model(th) != 3: continue
        a = p.at(th); item = struct.unpack_from('<H', p.b, a + 20)[0]; state = struct.unpack_from('<i', p.b, a + 200)[0]
        level = p.b[a + 1098]; repair = struct.unpack_from('<f', p.b, a + 1074)[0]; share = None
        if prices is not None:
            costs = sum((prices.get(item, [0, 0, 0]) + [0] * 256)[k] for k in range(level + 1))
            share = 0 if state == 4 else int(((int(repair) & 0xff) * costs) / 100)
            total += share
        objs.append((th, item, state, level, repair, share))
    return dict(cells=cells, types=types, track=track, objects=objs, objects_sum=total if prices is not None else None,
                value=cells + total if prices is not None else None)
def show_park_value(p):
    v = park_value(p); print('   cells %d (types %s, track cells %d); objects:' % (v['cells'], dict(sorted(v['types'].items())), v['track']))
    for o in v['objects']: print('     thing %d item %d state %d level %d repair %r' % o[:5])
def show_money(p):
    for name, r in money_rings(p).items():
        print('   %-6s slot %d of %d wrapped %d month so far %d newest %s closed %s' % (name, r['current'], r['entries'], r['wrapped'], r['total'], r['newest'], r['months'][-6:]))
    s = stays(p); print('   leavers slot %d of %d wrapped %d mTemp %d mean %d newest %s' % (s['current'], s['entries'], s['wrapped'], s['total'], s['mean'], s['stays'][-8:]))
    l = loads(p); print('   loads slot %d of %d wrapped %d mTemp %d called %d loads %s' % (l['current'], l['entries'], l['wrapped'], l['total'], l['called'], l['loads']))
def ring_last(b, at):
    """A day ring's last min(30, filled) finished days, newest first, signed (entry, count, wrap byte, today, 30 days)."""
    entry, num = struct.unpack_from('<ii', b, at); wrap = b[at + 8]; days = struct.unpack_from('<30i', b, at + 13)
    filled = num if wrap else entry + 1
    return [days[(entry - i) % num] for i in range(min(30, filled))] if num else []
def show_object_days(p):
    for t in p.things:
        if p.model(t) == 3: print('   thing %d: %s' % (t, ' '.join('%s %s' % kv for kv in object_days(p, t).items())))
DEVELOPED_TYPES = (1, 3, 4, 9, 10)
def cell_type(p, x, y):
    """A cell's mType (0 where its record holds none) and mFlags."""
    at = p.cell[y * 128 + x]; s = p.b[at]
    if not s & 1: return 0, 0
    return struct.unpack_from('<i', p.b, at + 25)[0], struct.unpack_from('<H', p.b, at + 2)[0]
def developed(p):
    """The map's developed cells as FUN_004c81b0 counts them (Q411; OpenTPW docs/exe/advisor-park.md, "A park too big
    for its handymen"): every one of the 16,384 cells, inside the park or out, whose mType is 1, 3, 4, 9 or 10, which
    the advisor's row 10 sets against the handymen (row 13's keys name developed cells for the guards; its score is
    not read). Answers (count, {mType: cells}). The shipped jungle park: 126."""
    types = {}
    for y in range(128):
        for x in range(128):
            t = cell_type(p, x, y)[0]; types[t] = types.get(t, 0) + 1
    return sum(n for t, n in types.items() if t in DEVELOPED_TYPES), dict(sorted(types.items()))
def bare_blocks(p, w, h):
    """Every w x h block of cells all mType 0 and inside the park (mFlags 0x40 clear), nearest a path cell first, as
    [(distance, x, y)]: where a scene can lay path (`path X Y` on the console; Q411 laid 289 at (60,15))."""
    ok = [[cell_type(p, x, y)[0] == 0 and not cell_type(p, x, y)[1] & 0x40 for x in range(128)] for y in range(128)]
    paths = [(x, y) for y in range(128) for x in range(128) if cell_type(p, x, y)[0] == 1]
    out = []
    for y in range(129 - h):
        for x in range(129 - w):
            if all(ok[y + j][x + i] for j in range(h) for i in range(w)):
                out.append((min((abs(px - x) + abs(py - y) for px, py in paths), default=-1), x, y))
    return sorted(out)
def show_developed(p):
    n, types = developed(p); print('   developed %d; cells by mType %s' % (n, types))
COUNTING_TYPES = (0, 1, 3, 9, 10)
def guests_inside(p, below=30):
    """The guests the analyser counts inside the park (FUN_004c7fa0 / FUN_004c9130: a model-1 thing on a cell of mType
    0, 1, 3, 9 or 10, the cell its mX and mY high bytes name) and those of them under `below` happiness, the float at the
    record's +422 truncated to its low byte, as FUN_004c9750 asks (the advisor's row 14, Q415). Answers (inside, unhappy,
    {thing: happiness} of the inside ones), and the entertainers (model 6 things)."""
    inside, unhappy, hap, ent = 0, 0, {}, 0
    for t, (a, m) in p.things.items():
        if m == 6: ent += 1
        if m != 1: continue
        x, y = p.b[a + 9], p.b[a + 11]
        if cell_type(p, x, y)[0] not in COUNTING_TYPES: continue
        h = struct.unpack_from('<f', p.b, a + 422)[0]; inside += 1; hap[t] = h
        if (int(h) & 0xff) < below: unhappy += 1
    return inside, unhappy, hap, ent
def show_inside(p):
    i, u, hap, ent = guests_inside(p)
    print('   guests inside %d, under 30 happiness %d (%d%%), crowd %d, entertainers %d; happiness spread %s' % (i, u, u * 100 // i if i else 0, min(100, max(0, i // 2)), ent, sorted(set(round(h, 2) for h in hap.values()))[:12]))
def cells(p):
    """Every cell's (mType, mParentID, track type, track parent), by index y*128 + x (Q469)."""
    out = []
    for i in range(16384):
        a = p.cell[i]; s = p.b[a]; m = a + 1; t = m + (52 if s & 1 else 0)
        out.append((struct.unpack_from('<i', p.b, m + 24)[0] if s & 1 else 0, struct.unpack_from('<H', p.b, m + 10)[0] if s & 1 else 0,
                    struct.unpack_from('<i', p.b, t + 24)[0] if s & 2 else 0, struct.unpack_from('<H', p.b, t + 10)[0] if s & 2 else 0))
    return out
def effects(p, i):
    """A cell's five region-effect words (happiness, illness, hunger, security, attraction) by index y*128 + x: its
    effects sub-record, after the map (52) and track (31) ones its status bits name; nought where bit 4 is clear (Q706)."""
    a = p.cell[i]; s = p.b[a]
    if not s & 4: return (0, 0, 0, 0, 0)
    return struct.unpack_from('<5h', p.b, a + 1 + (52 if s & 1 else 0) + (31 if s & 2 else 0))
BOUNDARY_SHIFT = {0: (0, 0), 90: (0, 1), 180: (1, 1), 270: (1, 0)}
def boundary(p):
    """The boundary's anchors (track type 25) as the file holds them (Q620; FileFormats saves.md, "A boundary piece's
    record"): [dict(x, y, mesh = the track sub-record's mMeshInstance, set, row, turn = its mTileData, mask =
    mHoardingNeighbours (0x01 y-2, 0x04 x+2, 0x10 y+2, 0x40 x-2), dir = mDirection (the way a fresh park's walk leaves
    it), flags = mFlags (1: a straight over water), item, cell, angle = the named record's, or None, agrees = the record is
    the anchor's row's model on its cell moved by its turn at 360 less it)]; sorted by mesh it is a fresh park's walk
    (Q656, q656/model.py checks the chooser on it); `-B` prints them."""
    h, m = models(p); out = []
    for i in range(16384):
        a = p.cell[i]; s = p.b[a]
        if not s & 2: continue
        t = a + 1 + (52 if s & 1 else 0)
        tset, tidx, tang, typ = struct.unpack_from('<iiii', p.b, t + 12)
        if typ != 25: continue
        mesh = struct.unpack_from('<i', p.b, t + 3)[0]; r = m.get(mesh)
        d = dict(x=i % 128, y=i // 128, mesh=mesh, set=tset, row=tidx, turn=tang, mask=p.b[t + 28], dir=p.b[t],
                 flags=struct.unpack_from('<H', p.b, t + 1)[0], item=None, cell=None, angle=None, agrees=False)
        if r:
            cx, cy = struct.unpack_from('<ii', r['raw'], 5); ang = struct.unpack_from('<i', r['raw'], 0x27)[0]
            dx, dy = BOUNDARY_SHIFT.get(ang % 360, (0, 0))
            d.update(item=r['item'], cell=(cx, cy), angle=ang,
                     agrees=(cx - dx, cy - dy) == (d['x'], d['y']) and (360 - tang) % 360 == ang)
        out.append(d)
    return out
def show_boundary(p):
    b = boundary(p); from collections import Counter
    print('   anchors %d, records agreeing %d; items %s; masks %s' % (len(b), sum(d['agrees'] for d in b),
        dict(sorted(Counter(d['item'] for d in b).items(), key=lambda kv: kv[0] or 0)), dict(sorted(Counter(d['mask'] for d in b).items()))))
TICKET_TRACK = (11, 13, 16, 18, 25)
def tickets(p):
    """The golden tickets' two cell measures of a park file (Q706; OpenTPW docs/exe/ride-operation.md, "Golden tickets"):
    Global 3's covered cells, mType 9, 10, 4 or 0x15 or a track record of 11, 13, 16, 18 or 25 (a 12 or 17 taking its
    parent's, none without one; FUN_004cd7f0), and the Secret's whole per cent of the cells not of mType 2, 7 or 30 whose
    security word is above nought (FUN_004cd720). Answers (covered, counted, secured, percent); `-T` prints it."""
    c = cells(p); covered = counted = secured = 0
    for i, (mt, par, tt, tp) in enumerate(c):
        if tt in (12, 17): tt = c[tp - 1][2] if tp else 0
        if mt in (9, 10, 4, 0x15) or tt in TICKET_TRACK: covered += 1
        if mt in (2, 7, 30): continue
        counted += 1
        if effects(p, i)[3] > 0: secured += 1
    return covered, counted, secured, secured * 100 // counted if counted else 0
def show_tickets(p):
    covered, counted, secured, percent = tickets(p)
    print('   covered %d; secured %d of %d counted, %d%%' % (covered, secured, counted, percent))
def hoarding(p, x, y, c=None):
    c = c or cells(p); mt, par, tt, tp = c[y*128 + x]
    return tt == 12 and tp != 0 and c[tp - 1][2] == 25
def show_map(p, x0=0, y0=0, x1=128, y1=128):
    import collections
    c = cells(p)
    print(' mType', sorted(collections.Counter(e[0] for e in c).items()), 'track type', sorted(collections.Counter(e[2] for e in c).items()))
    for y in range(y0, y1):
        r = ''
        for x in range(x0, x1):
            mt, par, tt, tp = c[y*128 + x]
            r += 'H' if hoarding(p, x, y, c) else 'T' if tt == 25 else {0: '.', 1: '#', 2: '2', 3: 'q', 4: 'o', 7: ' ', 9: '9', 10: 'A', 30: 'g'}.get(mt, '?')
        print('%4d %s' % (y, r))
_ECON = ('fee', 'balance', 'batch', 'withdrawals', 'last', 'red', 'profit')
_LOAN = ('available', 'amount', 'apr', 'months', 'monthly', 'bought', 'repaid', 'lender')
def economy(p, thing=8):
    a = p.at(thing); v = struct.unpack_from('<7i', p.b, a + 16)
    e = dict(zip(_ECON, v)); e['loans'] = [struct.unpack_from('<8i', p.b, a + 44 + 32 * i) for i in range(8)]; return e
def show_economy(p):
    e = economy(p); print('   ' + ' '.join('%s %d' % (k, e[k]) for k in _ECON))
    for i, l in enumerate(e['loans']): print('   loan %d %s' % (i, ' '.join('%s %d' % kv for kv in zip(_LOAN, l))))
def economy_set(src, dst, fields, loans={}, thing=8):
    import zlib
    data = open(src, 'rb').read(); assert data[0x60d:0x611] == b'BILZ'
    p = Park(src); was = economy(p, thing); b = bytearray(zlib.decompress(data[0x629:])); assert bytes(b) == bytes(p.b); a = p.at(thing)
    for k, v in fields.items(): struct.pack_into('<i', b, a + 16 + 4 * _ECON.index(k), v)
    for (i, k), v in loans.items(): struct.pack_into('<i', b, a + 44 + 32 * i + 4 * _LOAN.index(k), v)
    packed = zlib.compress(bytes(b), 9); head = bytearray(data[:0x629])
    struct.pack_into('<II', head, 0x611, len(b), 0x629 - 0x60d + len(packed))
    open(dst, 'wb').write(bytes(head) + packed); return was
def calendar(p):
    import datetime
    start, session, month, day, rate = struct.unpack_from('<qqiii', p.b, p.map - 46); tick = p.get('tick')
    date = datetime.datetime(1601, 1, 1) + datetime.timedelta(microseconds=start // 10, seconds=tick * rate // 4)
    return dict(start=start, session=session, month=month, day=day, rate=rate, tick=tick, date=date,
                matches=(date.month - 1, date.day) == (month, day))
def show_calendar(p):
    c = calendar(p); print('   tick %d date %s: month %d day %d (%s), rate %d, start %d, session %d' % (c['tick'], c['date'].date(),
        c['month'], c['day'], 'the date of the tick' if c['matches'] else 'NOT the date of the tick', c['rate'], c['start'], c['session']))
_WEATHER = (('quality', 16, '<i'), ('changed', 20, '<q'), ('forecasted', 28, '<q'), ('f_time', 36, '<q'), ('f_quality', 44, '<i'),
    ('f_valid', 48, '<B'), ('drops', 49, '<i'), ('target', 53, '<i'), ('lightning', 57, '<B'), ('countdown', 58, '<i'),
    ('struck', 62, '<i'), ('thunder', 66, '<i'), ('location', 70, '<3f'), ('voice', 82, '<I'), ('ptype', 86, '<I'),
    ('wind', 90, '<f'), ('force', 94, '<f'), ('overridden', 98, '<B'))
def weather(p):
    a = p.at(p.get('weather')); w = dict(at=a)
    for k, o, f in _WEATHER:
        v = struct.unpack_from(f, p.b, a + o); w[k] = v if len(v) > 1 else v[0]
    if p.b[a + 70:a + 82] == b'\xcd' * 12: w['location'] = None
    return w
def _tick_of(ft, rate=15000):
    """The tick of the file's clock a stamp is the calendar of, as a float: (ft - 2000-01-01) / (rate / 4 s)."""
    return (ft / 10**7 - 12591158400) / (rate / 4)
def show_weather(p):
    import datetime
    w = weather(p); d = lambda ft: (datetime.datetime(1601, 1, 1) + datetime.timedelta(microseconds=ft // 10)).strftime('%Y-%m-%d %H:%M:%S')
    print('   tick %d quality %d changed %s (tick %.2f) forecasted %s (tick %.2f) forecast %s q %d valid %d' % (p.get('tick'), w['quality'],
        d(w['changed']), _tick_of(w['changed']), d(w['forecasted']), _tick_of(w['forecasted']), d(w['f_time']), w['f_quality'], w['f_valid']))
    print('   drops %d target %d lightning %d countdown %d struck %d thunder %d location %s voice %d ptype 0x%x wind %.4f force %.4f overridden %d' % (
        w['drops'], w['target'], w['lightning'], w['countdown'], w['struck'], w['thunder'], w['location'], w['voice'], w['ptype'],
        w['wind'], w['force'], w['overridden']))
def weather_set(src, dst, fields):
    import zlib
    data = open(src, 'rb').read(); assert data[0x60d:0x611] == b'BILZ'
    p = Park(src); was = weather(p); b = bytearray(zlib.decompress(data[0x629:])); assert bytes(b) == bytes(p.b); a = was['at']
    for k, v in fields.items():
        o, f = next((o, f) for n, o, f in _WEATHER if n == k); struct.pack_into(f, b, a + o, *(v if isinstance(v, tuple) else (v,)))
    packed = zlib.compress(bytes(b), 9); head = bytearray(data[:0x629])
    struct.pack_into('<II', head, 0x611, len(b), 0x629 - 0x60d + len(packed))
    open(dst, 'wb').write(bytes(head) + packed); return was
def cellflags(p, x, y):
    """A cell's (mType, mFlags) from the file, or None where the record is not stored: mFlags 0x20 NOMODIFY, 0x40 land
    outside the park (Q538)."""
    at = p.cell[y * 128 + x]
    if not p.b[at] & 1: return None
    return struct.unpack_from('<i', p.b, at + 25)[0], struct.unpack_from('<H', p.b, at + 2)[0]
def show_nomodify(p):
    """Every NOMODIFY cell and its four neighbours' type and flags: where a NOMODIFY path meets land outside the park
    (Q538: the queue verdict's second call, a run from (48,17) to (48,16) on the stock park)."""
    for y in range(128):
        for x in range(128):
            r = cellflags(p, x, y)
            if not r or not r[1] & 0x20: continue
            nb = [(x + dx, y + dy, cellflags(p, x + dx, y + dy)) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            print(f"  ({x},{y}) type {r[0]} flags {r[1]:#x} |", ' '.join(f"({a},{b}) t{c[0]} f{c[1]:x}" if c else f"({a},{b}) -" for a, b, c in nb))
def coasters(p):
    """The coasters module (SAOC; FileFormats saves.md, "The coasters module"; the loader FUN_00437b60, Q593): the four
    counts and, for every coaster whose pieces can be walked, dict(header = (flags, state, model, instance, handle,
    pieces, trains, capacity, clashes), partners, pieces = [dict(flags, mesh, x, y, ordinal, loft, bank, incline, yaw,
    loop, partners)], at = the header's offset). A piece is 0x22 bytes: u32 flags, u32 the mesh instance, u16 x, u16 y
    (the coaster grid's cell, 96 to a row), u8 ordinal, five unaligned floats (loft 0x0d to FUN_00444150, bank 0x11 and
    incline 0x15 to FUN_00444270, yaw 0x19 to FUN_004441d0, loop 0x1d), u8 its partner count, then that many u16s. Only
    the first coaster is walked: the trains and cars after it are sized by the coaster type (uiMaxTrains)."""
    b = bytes(p.b); at = b.find(b'EMAK') + 4
    counts = struct.unpack_from('<4I', b, at); at += 16; out = []
    if counts[0]:
        h = struct.unpack_from('<IIHHH8xHHHI', b, at); hat = at; at += 32
        n = struct.unpack_from('<I', b, at)[0]; partners = struct.unpack_from(f'<{n}H', b, at + 4); at += 4 + 2 * n
        pieces = []
        for i in range(h[5]):
            fl, mesh, x, y, o = struct.unpack_from('<IIHHB', b, at); fs = struct.unpack_from('<5f', b, at + 13)
            k = b[at + 33]; pr = struct.unpack_from(f'<{k}H', b, at + 34); at += 34 + 2 * k
            pieces.append(dict(flags=fl, mesh=mesh, x=x, y=y, ordinal=o, loft=fs[0], bank=fs[1], incline=fs[2], yaw=fs[3], loop=fs[4], partners=pr))
        out.append(dict(header=h, partners=partners, pieces=pieces, at=hat))
    return counts, out
def show_coasters(p):
    counts, cs = coasters(p); print('  counts', counts)
    for c in cs:
        print('  header flags %#x state %#x model %d instance %d handle %d pieces %d trains %d capacity %d clashes %d' % c['header'], 'partners', c['partners'])
        for i, q in enumerate(c['pieces']):
            print(f"    piece {i} flags {q['flags']:#x} mesh {q['mesh']} cell ({q['x']},{q['y']}) = {q['y'] * 96 + q['x']} ordinal {q['ordinal']} "
                  f"loft {q['loft']:.4f} bank {q['bank']:.4f} incline {q['incline']:.4f} yaw {q['yaw']:.4f} loop {q['loop']:.4f} partners {q['partners']}")
VOICE_KIND = {1: 'music class', 2: 'class 0x10', 3: 'one-shot', 4: 'any chain', 5: 'plain', 6: 'held chain', 7: 'any chain child',
    9: 'held chain child', 10: 'class 0x100'}
def voices(p):
    """The voice list after the sound block's 84 bytes: the writer 0x006b6c30, a record per voice 0x006bccf0; read back
    by 0x006b6da0 (case per type 1..6) and 0x006bd0a0 (Q560)."""
    b = bytes(p.b); s = sound(p)
    if not s['on']: return None
    sv = b.find(b'SVDA'); o = sv + 16 + s['n'] + 12
    L, count = struct.unpack_from('<II', b, o); end = o + L; o += 8
    n_lib, n = struct.unpack_from('<II', b, o); lib = o; o += 8 + 4 * n
    cats = []
    for k in range(n):
        m = b[o]; cats.append(b[o + 1:o + 1 + m].rstrip(b'\0').decode('latin-1') if m else None); o += 1 + m
    lib_ok = o == lib + 8 + n_lib
    blocks, = struct.unpack_from('<I', b, o); o += 4
    MAGIC = 0x12d591
    def rec(o):
        r = {}
        if struct.unpack_from('<I', b, o)[0] != MAGIC: raise ValueError('no magic at %d' % o)
        r['pos'] = struct.unpack_from('<3f', b, o + 4); r['level'], = struct.unpack_from('<f', b, o + 16)
        r['range'], = struct.unpack_from('<f', b, o + 20)
        r['found'], r['effect'], r['variation'], r['sample'] = struct.unpack_from('<4I', b, o + 24); o += 40
        if struct.unpack_from('<I', b, o)[0] != MAGIC: raise ValueError('no magic at %d' % o)
        has, = struct.unpack_from('<I', b, o + 4); o += 8
        r['keys'] = r['values'] = r['ctl'] = None
        if has:
            r['keys'] = tuple(b[o:o + 4]); r['values'] = tuple(b[o + 4:o + 8])
            c, d, e, f = struct.unpack_from('<4i', b, o + 8); r['ctl'] = (d, c, e, f); o += 24  # file order +0xc, +8: given as +8..+0x14
        if struct.unpack_from('<I', b, o)[0] != MAGIC: raise ValueError('no magic at %d' % o)
        r['held'] = b[o + 4]; r['handle'], ct = struct.unpack_from('<II', b, o + 5); o += 13
        r['cat'] = r['found']; r['child'] = None
        if ct:
            t, = struct.unpack_from('<I', b, o); c, o = rec(o + 4); c['type'] = t; c['kind'] = VOICE_KIND.get(t, '?'); r['child'] = c
        return r, o
    vs = []
    for i in range(count):
        h, t = struct.unpack_from('<II', b, o); r, o = rec(o + 8)
        r['type'] = t; r['kind'] = VOICE_KIND.get(t, '?'); r['handle'] = h; vs.append(r)
    return dict(L=L, count=count, cats=cats, lib_ok=lib_ok, blocks=blocks, voices=vs, ends=o == end, words=s['words'])
_EFFECTS = {}
def effect_id(cat, index, game=None):
    """A voice's category name and effect index -> the effect's id, from <game><cat>SFX.map (any case)."""
    import os
    game = game or os.environ['OPENTPW_GAME_PATH']
    if cat not in _EFFECTS:
        path = game
        for part in cat.strip('\\').split('\\'):
            want = part.lower() + ('sfx.map' if part == cat.split('\\')[-1] else '')
            hit = [f for f in os.listdir(path) if f.lower() == want] if os.path.isdir(path) else []
            path = os.path.join(path, hit[0]) if hit else None
            if path is None: break
        from sfxmap import Category
        _EFFECTS[cat] = [e.id for e in Category(path).effects] if path else []
    ids = _EFFECTS[cat]
    return ids[index] if index < len(ids) else None
def script_objects(p):
    """Every script's object list in the ride script module (FileFormats saves.md, "The object list"; walk as
    q172/coaster/skeptic/essr.py): [(script handle, thing, [(type, handle, node, lookup, tag)])]."""
    b = bytes(p.b); o = b.find(b'RYLF') + 4
    if b[o:o + 4] != b'RSSE': raise ValueError('no RSSE after RYLF')
    o += 4; hl, = struct.unpack_from('<I', b, o); o += 4 + hl + 20
    n, sz = struct.unpack_from('<II', b, o); o += 8
    out = []
    for k in range(n):
        st = b[o:o + sz]; o += sz
        for _ in range(6): L, = struct.unpack_from('<I', b, o); o += 4 + L
        c, = struct.unpack_from('<I', b, o); o += 4 + c * 32
        for _ in range(2): L, = struct.unpack_from('<I', b, o); o += 4 + L
        if b[o:o + 4] != b'OBJ ': raise ValueError('no OBJ at %d' % o)
        oc, ob = struct.unpack_from('<II', b, o + 4); o += 12
        objs = [struct.unpack_from('<IIiiI', b, o + i * ob + 8) for i in range(oc)]; o += oc * ob
        out.append((struct.unpack_from('<H', st, 8)[0], struct.unpack_from('<H', st, 0xac)[0], objs))
    return out
def show_voices(p):
    v = voices(p)
    if v is None: print('   sound off: no voice list'); return
    owners = {}
    for sh, thing, objs in script_objects(p):
        for t, h, node, look, tag in objs:
            if t >= 3: owners.setdefault(h, []).append('script %d (thing %d) type %d node %d tag %d' % (sh, thing, t, node, tag))
    try:
        w = weather(p); owners.setdefault(w['voice'], []).append('weather mRainSoundHandle') if w['voice'] else None
    except Exception: pass
    crowd = struct.unpack_from('<I', bytes(p.b), bytes(p.b).find(b'SVDA') + 16 + 4)[0]
    if crowd: owners.setdefault(crowd, []).append('crowd handle 0x00803aa4')
    print('   %d voices, %d block(s), list %s, library %s' % (v['count'], v['blocks'], 'ends at L' if v['ends'] else 'DOES NOT END AT L', 'ok' if v['lib_ok'] else 'BAD'))
    def one(r, ind):
        cat = v['cats'][r['cat']] if r['cat'] < len(v['cats']) and v['cats'][r['cat']] else '?'
        eid = effect_id(cat, r['effect']) if cat != '?' else None
        parts = cat.split('\\')
        where = parts[-1] + ('@' + parts[3] if len(parts) > 4 and parts[2] == 'levels' else '')
        held = '; '.join(owners.get(r['handle'], ['held by nothing saved']))
        print('%s%-15s %s effect %s (index %d) v%d s%d at (%.0f, %.0f, %.0f) range %g level %.3f keys %s values %s ctl %s held %d handle 0x%08x %s' % (
            ind, r['kind'], where, eid, r['effect'], r['variation'] + 1, r['sample'] + 1, r['pos'][0], r['pos'][1], r['pos'][2],
            r['range'], r['level'], r['keys'], r['values'], r['ctl'], r['held'], r['handle'], held))
        if r['child']: one(r['child'], ind + '      child ')
    for r in v['voices']: one(r, '   ')

def analyser_grid(p):
    """The analyser record's +16..+62,598 (FUN_004c5d70; Q546): grid, blocks, small rings, kept answers."""
    b = p.b; a = p.at(p.get('analyser'))
    def answers(at, n, floats=0):
        return [struct.unpack_from('<fB' if i < floats else '<iB', b, at + i * 5) for i in range(n)]
    blocks = [struct.unpack_from('<4i', b, a + 36 + k * 61) + (answers(a + 52 + k * 61, 9, 2),) for k in range(1024)]
    rings = [struct.unpack_from('<iiBH3H', b, a + 62500 + i * 17) for i in range(4)]
    return dict(grid=struct.unpack_from('<5i', b, a + 16), blocks=blocks, rings=rings, kept=answers(a + 62568, 6))
def sound(p):
    """The sound block between SVDA and NUOS (FUN_0051c350; Q546)."""
    b = bytes(p.b); sv = b.find(b'SVDA'); nu = b.find(b'NUOS', sv)
    on, second, n = struct.unpack_from('<III', b, sv + 4); at = sv + 16 + n
    if not on: return dict(on=on, second=second, n=n, words=None, length=None, ends=at == nu)
    words = struct.unpack_from('<3I', b, at); L = struct.unpack_from('<I', b, at + 12)[0]
    return dict(on=on, second=second, n=n, words=words, length=L, ends=at + 12 + L == nu)

def channels(p):
    """({item: Counter(channel count)}, {item: (offset, channels) of its first record of more than one}) (Q778)."""
    import collections
    g, hdr, recs = rsys.walk(p); by = collections.defaultdict(collections.Counter); first = {}
    for r in recs or []:
        if not r: continue
        at, ch, base = r; item = p.u32(at + 1); by[item][ch] += 1
        if ch != 1 and item not in first: first[item] = (at, ch)
    return dict(by), first
def show_channels(p):
    by, first = channels(p)
    for item in sorted(by): print(f'  item {item:5}: ' + ', '.join(f'{n} of {ch} channel(s)' for ch, n in sorted(by[item].items())) + (f'; first of {first[item][1]} at {first[item][0]}' if item in first else ''))
if __name__ == '__main__':
    flags = [a for a in sys.argv[1:] if a in ('-e', '-t', '-a', '-s', '-v', '-l', '-o', '-p', '-m', '-w', '-x', '-d', '-g', '-L', '-P', '-E', '-C', '-W', '-c', '-n', '-T', '-K', '-V', '-N', '-B', '-H')]
    if '-c' in flags:
        rest = [a for a in sys.argv[1:] if a not in flags]; print(rest[0]); show_map(Park(rest[0]), *map(int, rest[1:5])); sys.exit()
    for path in [a for a in sys.argv[1:] if a not in flags]: print(path); (show_channels if '-H' in flags else show_boundary if '-B' in flags else show_voices if '-N' in flags else show_coverage if '-V' in flags else show_coasters if '-K' in flags else show_tickets if '-T' in flags else show_nomodify if '-n' in flags else show_weather if '-W' in flags else show_calendar if '-C' in flags else show_economy if '-E' in flags else show_pool if '-P' in flags else show_litter if '-L' in flags else show_inside if '-g' in flags else show_developed if '-d' in flags else show_sets if '-x' in flags else show_park_value if '-w' in flags else show_money if '-m' in flags else show_patrols if '-p' in flags else show_object_days if '-o' in flags else show_lab if '-l' in flags else show_emitters if '-e' in flags else show_tags if '-t' in flags else show_advisor if '-a' in flags else show_analyser if '-s' in flags else show_vehicles if '-v' in flags else show)(Park(path))
