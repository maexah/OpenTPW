"""pk.py: a park file taken apart and put back (Q241h's instrument). Park(path) parses; .write(path) deflates the body
behind the file's own preamble. Offsets are the FileFormats saves page's."""
import zlib, struct, collections
SIZE = {1:533, 3:1099, 4:511, 5:513, 6:509, 7:511, 8:509, 9:103, 10:18, 11:5846, 12:16, 13:73544, 14:4190, 15:99,
        16:300, 17:16, 19:937}
PEOPLE = (1, 4, 5, 6, 7, 8)
STAFFNEXT = {4: 509, 5: 511, 6: 507, 7: 509, 8: 507}
HEADER = [('version',4),('av1',2),('av2',2),('av3',2),('bank',2),('curav',2),('tick',4),('mechhq',2),
          ('analyser',2),('closed',4),('visitors',4),('gates',2),('lights',2),('seed',4),('lab',2),('staffhq',2),
          ('tags',2),('uimsg',2),('weather',2),('state',4),('firstHandyman',2),('firstMechanic',2),
          ('firstEntertainer',2),('firstGuard',2),('firstResearcher',2),('firstObject',2)]
class Park:
    def __init__(self, path):
        self.src = open(path, 'rb').read(); self.b = bytearray(zlib.decompress(self.src[0x629:])); self.parse()
    def u32(self, o): return struct.unpack_from('<I', self.b, o)[0]
    def u16(self, o): return struct.unpack_from('<H', self.b, o)[0]
    def f32(self, o): return struct.unpack_from('<f', self.b, o)[0]
    def parse(self):
        b = self.b; rec = self.u32(4); self.h = h = 8 + rec
        self.hdr = {}; o = h
        for n, s in HEADER: self.hdr[n] = (o, s); o += s
        self.pool = h + 64 + 150*32 + 6          # staff pool: 32 x 20, then counts, then time sig
        at = h + 64 + 150*32 + 6 + 32*20 + 76; self.map = at; self.cell = []
        for _ in range(16384):
            s = b[at]; assert s & ~7 == 0; self.cell.append(at)
            at += 1 + (52 if s & 1 else 0) + (31 if s & 2 else 0) + (10 if s & 4 else 0)
        self.headat = at; head = self.u32(at); at += 4; self.things = collections.OrderedDict(); tid = head
        while tid:
            nxt, model = self.u32(at), self.u32(at+4); self.things[tid] = (at, model); at += SIZE[model]; tid = nxt
        assert b[at:at+4] == b'DLRW'; self.wend = at
        s = at + 4; assert b[s:s+4] == b'TPCS'; self.sp = s; self.slots = self.u32(s+8)
        self.handles = [self.u32(s+12+4*i) for i in range(self.slots)]; r = s + 12 + 4*self.slots; self.sprite = {}
        for i, hd in enumerate(self.handles):
            if hd: self.sprite[i] = r; r += 280
        assert b[r:r+4] == b'CSPS'; self.spend = r
        p = b.index(b'TRAP', r); self.mess = m = p + 4; n = self.u32(m); m += 4; self.sets = []
        for k in range(n):
            c = self.u32(m); m += 4; self.sets.append([self.u16(m+2*i) for i in range(c)]); m += 2*c
        assert b[m:m+4] == b'SSEM'; self.messend = m
    def get(self, name):
        o, s = self.hdr[name]; return self.u32(o) if s == 4 else self.u16(o)
    def who(self, x, y): return self.u16(self.cell[y*128+x] + 1 + 50)   # mWho
    def model(self, tid): return self.things[tid][1]
    def at(self, tid): return self.things[tid][0]
    def write(self, path, body=None):
        body = bytes(self.b if body is None else body); z = zlib.compress(body, 9)
        out = struct.pack('<I', 500) + self.src[4:0x60d] + b'BILZ' + struct.pack('<6I', len(body), len(z)+28, 15, 9, 0, 0) + z
        open(path, 'wb').write(out)
