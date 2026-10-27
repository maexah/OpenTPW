"""gmsfile.py <gms.dat>... - a player profile's progress: the golden tickets, keys, records and ticket-bought rides.

    from gmsfile import read        (with tools/harness/lib on sys.path)
    put(path, mode=None, all_researched=None, tickets=None, records=None, park_records=None, spent=None, ride_ids=None)
                                    rewrites the mode byte, records' all-researched bytes, park tickets ({theme: [6
                                    flags]}; Q706), the four player-wide record tickets ([4 flags]), a theme's records
                                    ({theme: {k: (held, value)}}; Q706's rest), the tickets spent and the ticket-bought
                                    ride ids ([ids]; Q709) in place
    read(path) -> dict(version, records [4 flags], secret [2 flags], spent, keys_outright, mode (1 Instant Action),
                      themes {name: dict(flags [6 park tickets], records [(held, value)] x4,
                      renamed, all_researched (Q550: the record's last two bytes))}, ride_ids, tickets, keys)

The layout is OpenTPW docs/exe/saves.md, "gms.dat" (FileFormats options-and-players.md); `tickets` counts every
theme's park flags, which the game does only for a theme whose global.sam loads (all four ship one), and `keys` is
tickets // 3 + keys_outright (PlayerProgress_CountKeys). Written for Q141, to read what the ORIGINAL awarded
(Alexah's Full Simulation profile: jungle Locals 0-2 and record 0 at 106). A record value is garbage while its flag is 0.
"""
import struct, sys

def read(path):
    d = open(path, 'rb').read()
    out = dict(version=struct.unpack_from('<I', d, 0)[0])
    o = 4
    out['records'] = list(d[o:o + 4]); o += 4
    out['secret'] = list(d[o:o + 2]); o += 2
    out['spent'], out['keys_outright'] = struct.unpack_from('<ii', d, o); o += 8
    out['mode'] = d[o]; o += 3                                  # mode, swear filter, first park not started
    n, = struct.unpack_from('<I', d, o); o += 4
    themes = {}
    for _ in range(n):
        ln, = struct.unpack_from('<I', d, o); o += 4
        name = d[o:o + ln].decode('latin1'); o += ln
        flags = list(d[o:o + 6]); o += 6
        recs = [(d[o + 5 * k], struct.unpack_from('<i', d, o + 5 * k + 1)[0]) for k in range(4)]; o += 20
        o += 33 * 4                                             # the park's name
        renamed, researched = d[o], d[o + 1]; o += 2           # mNameChanged, mAllResearchCompleted (the record's +0xb8, +0xb9)
        themes[name] = dict(flags=flags, records=recs, renamed=renamed, all_researched=researched)
    out['themes'] = themes
    o += 39                                                     # the player's options
    m, = struct.unpack_from('<I', d, o); o += 4
    out['ride_ids'] = list(struct.unpack_from('<%dH' % m, d, o)); o += 2 * m
    out['tickets'] = sum(map(bool, out['records'])) + sum(map(bool, out['secret'])) + sum(sum(map(bool, t['flags'])) for t in themes.values())
    out['keys'] = out['tickets'] // 3 + out['keys_outright']
    out['left_over'] = len(d) - o
    return out

def put(path, mode=None, all_researched=None, tickets=None, records=None, park_records=None, spent=None, ride_ids=None):
    """Rewrite a gms.dat in place: the mode byte (+0x24 in memory; 1 Instant Action, 0 Full Simulation), each named
    theme record's mAllResearchCompleted ({name: 0 or 1}) and its six park tickets ({name: [6 flags]}). Q550 made a
    Full Simulation player of an Instant Action one with it; Q706 took tickets away so a park wins them again. Back the
    file up first, or write a copy."""
    d = bytearray(open(path, 'rb').read())
    if mode is not None: d[18] = mode
    if records is not None: d[4:8] = bytes(records)
    if spent is not None: struct.pack_into('<i', d, 10, spent)
    o = 21; n, = struct.unpack_from('<I', d, o); o += 4
    for _ in range(n):
        ln, = struct.unpack_from('<I', d, o); o += 4
        name = d[o:o + ln].decode('latin1'); o += ln
        if tickets and name in tickets: d[o:o + 6] = bytes(tickets[name])
        for k, (held, value) in ((park_records or {}).get(name) or {}).items():
            d[o + 6 + 5 * k] = held; struct.pack_into('<i', d, o + 6 + 5 * k + 1, value)
        o += 6 + 20 + 33 * 4
        if all_researched and name in all_researched: d[o + 1] = all_researched[name]
        o += 2
    if ride_ids is not None:
        o += 39; m, = struct.unpack_from('<I', d, o)
        d[o:o + 4 + 2 * m] = struct.pack('<I%dH' % len(ride_ids), len(ride_ids), *ride_ids)
    open(path, 'wb').write(bytes(d))

if __name__ == '__main__':
    for path in sys.argv[1:]:
        g = read(path)
        print(path)
        print('   version %d mode %d records %s secret %s spent %d keys outright %d -> tickets %d keys %d, ticket-bought rides %s%s' % (
            g['version'], g['mode'], g['records'], g['secret'], g['spent'], g['keys_outright'], g['tickets'], g['keys'], g['ride_ids'],
            '' if not g['left_over'] else ', %d bytes left over' % g['left_over']))
        for name, t in g['themes'].items():
            print('   %-8s park tickets %s records %s renamed %d all-researched %d' % (name, t['flags'], [v if f else '-' for f, v in t['records']],
                t['renamed'], t['all_researched']))
