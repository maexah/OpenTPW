#!/usr/bin/env bash
# tilecap.sh OUTDIR [HITS] [SECS] - every ground tile the ORIGINAL's procedural compositor makes, caught as it is made
# (Q483; docs/exe/park-engine.md, "The procedural ground"): a hardware breakpoint on FUN_0055f780's entry keeps its
# dest, x, y and the record, and one on each return into FUN_00572440 (0x0057257a, 0x005725a5) dumps the finished
# N x N x 4 tile to OUTDIR/tile-K-X-Y-N-rRECTEX-wWORD.bin (the record's texture, 0 when no path is laid over it, and
# the cell's word). Move the camera while it runs: a tile is made only when a cell comes into view uncached.
# A tile with a path laid over also leaves its record, rec-K-X-Y.bin (the five dwords: texel pointer, width, height,
# bytes a texel, word), and its texels as the locked surface holds them, tex-K-X-Y.bin (Q489).
# Compare with lib/lndtile.py (lndcmp.py beside this). The game is held while gdb attaches and for a few ms a tile.
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; D="$1"; N="${2:-40}"; T="${3:-60}"; mkdir -p "$D"
P="$("$H/original.sh" pid)"; C="$(mktemp)"
cat > "$C" <<GDB
set pagination off
set confirm off
set print thread-events off
handle SIGUSR1 SIGUSR2 SIGSEGV SIGPIPE SIGALRM SIGCHLD SIGIO SIGQUIT nostop noprint pass
set \$n = 0
set \$dest = 0
hbreak *0x0055f780
commands 1
silent
set \$dest = *(unsigned*)(\$esp + 4)
set \$x = *(int*)(\$esp + 8)
set \$y = *(int*)(\$esp + 12)
set \$rec = *(unsigned*)(\$esp + 16)
set \$sz = *(unsigned char*)\$ecx
if *(unsigned*)\$rec != 0
eval "dump binary memory $D/rec-%d-%d-%d.bin %u %u", \$n + 1, \$x, \$y, \$rec, \$rec + 20
eval "dump binary memory $D/tex-%d-%d-%d.bin %u %u", \$n + 1, \$x, \$y, *(unsigned*)\$rec, *(unsigned*)\$rec + *(unsigned*)(\$rec + 4) * *(unsigned*)(\$rec + 8) * *(unsigned*)(\$rec + 12)
end
continue
end
hbreak *0x0057257a
hbreak *0x005725a5
commands 2-3
silent
set \$n = \$n + 1
eval "dump binary memory $D/tile-%d-%d-%d-%d-r%x-w%x.bin %u %u", \$n, \$x, \$y, \$sz, *(unsigned*)\$rec, *(unsigned*)(\$rec + 16), \$dest, \$dest + \$sz * \$sz * 4
printf "tile %d: x %d y %d N %d pc %x dest %x rectex %x word %x\n", \$n, \$x, \$y, \$sz, \$pc, \$dest, *(unsigned*)\$rec, *(unsigned*)(\$rec + 16)
if \$n >= $N
detach
quit
end
continue
end
continue
detach
quit
GDB
timeout -s INT "$T" gdb -q -batch -p "$P" -x "$C" 2>&1 | grep -v "^\[New LWP\|^\[Thread debugging\|^Using host\|warning:\|^$" || true
rm -f "$C"
