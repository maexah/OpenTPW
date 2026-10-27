#!/usr/bin/env python3
"""particleoverlay.py PID OUT.png [EFFECT] - the ORIGINAL (off-screen, :77, 1024 x 768): grabs a frame and reads the particles of
EFFECT (default 58; at most 100 an emitter) just before and just after, then marks each particle's sprite (at the mean
of the two readings) on an enlarged crop under two readings of its width: RED the pass's own (half width = half height
in the matrix's units, so 4/3 wider on a 4:3 screen), BLUE square on the screen. Writes OUT.png (marked) and
OUT-bare.png (the same crop unmarked), and prints a line a particle. The crop is fixed in the code, (520,60)-(680,440)
enlarged 6 times: edit it for another scene. The grabbed frame is kept at /tmp/claude-1000/q258-o.png, a fixed path
whose folder must exist. Needs PIL and ImageMagick's `import`."""
import struct, subprocess, sys
from PIL import Image, ImageDraw
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
def parts():
    M = rd(rd(0x0087b088), '<16f'); pool = rd(0x0080cef8); s = rd(0x00816d1c, '<h'); out = []
    for _ in range(120):
        if s < 0 or s >= 120: break
        e = 0x0080cefc + 0x140 * s
        if rd(e + 0xc, '<h') == (int(sys.argv[3]) if len(sys.argv) > 3 else 58):
            p = rd(e + 6, '<h'); n = 0
            while p >= 0 and n < 100:
                a = pool + p * 0x34; x, y, z = (v / 64 for v in rd(a, '<3i'))
                c = [M[i] * x + M[4 + i] * y + M[8 + i] * z + M[12 + i] for i in range(4)]; h = rd(a + 0x28, '<h') / 1024
                out.append((p, 512 + c[0] / c[3] * 512, 384 + c[1] / c[3] * 384, h / c[3]))
                p = rd(a + 0x30, '<h'); n += 1
        s = rd(e + 0xd0, '<h')
    return out
a = parts(); subprocess.run(['import', '-window', 'root', '-display', ':77', '/tmp/claude-1000/q258-o.png'], check=True); b = dict((p[0], p) for p in parts())
im = Image.open('/tmp/claude-1000/q258-o.png').convert('RGB'); print(im.size)
x0, y0, x1, y1, k = 520, 60, 680, 440, 6   # the crop: Q258's two Drinks Shops under original_view(445, 335); change it for another scene
c = im.crop((x0, y0, x1, y1)).resize(((x1 - x0) * k, (y1 - y0) * k), Image.NEAREST); d = ImageDraw.Draw(c)
for p, x, y, h in a:
    if p in b: x, y = (x + b[p][1]) / 2, (y + b[p][2]) / 2
    for col, hw in (((255, 0, 0), h * 512), ((0, 80, 255), h * 384)):
        d.rectangle(((x - hw - x0) * k, (y - h * 384 - y0) * k, (x + hw - x0) * k, (y + h * 384 - y0) * k), outline=col)
    print(f"particle {p} at ({x:.1f},{y:.1f}) half height {h * 384:.1f} px, half width {h * 512:.1f} (red) or {h * 384:.1f} (blue)")
c.save(sys.argv[2]); im.crop((x0, y0, x1, y1)).resize(((x1 - x0) * k, (y1 - y0) * k), Image.NEAREST).save(sys.argv[2].replace('.png', '-bare.png'))
