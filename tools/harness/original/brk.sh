#!/usr/bin/env bash
# brk.sh - who CALLS a routine of the ORIGINAL: a hardware breakpoint through gdb, attached to the running game and
# detached again. At each hit it prints mGameTick, the registers and the stack's words that point into the game's
# code (0x00401000..0x006fffff), nearest first: the first is the caller's return address, the rest are the chain
# (and some stale words: read each in Ghidra, a real one follows a CALL).
#   brk.sh ADDR [HITS] [SECS] [WORDS]
#     ADDR  the listing's address, e.g. 0x004c7680, or up to four joined by commas (each hit prints its pc);
#           HITS  stop after this many, all addresses together (default 3);
#     SECS  give up after this long (default 60);  WORDS  stack words scanned (default 400)
#     BRK_COND='*(int*)($esp+12) == 0xd0'  a gdb condition on every address: a hit counts only when it holds
#           (at a routine's first byte $esp+4 is its first argument; Sound_PlayEffect's effect is $esp+12; Q567)
#     BRK_PRINT='printf "fctrl %x st0 %.17g st1 %.17g\n", $fctrl, (double)$st0, (double)$st1'  one more gdb line a hit
#           (the x87's control word and stack at a compare: is the product rounded to a float before it?)
# The game is held while gdb attaches (about a second) and for some ms a hit; it runs on after the detach.
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; A="$1"; N="${2:-3}"; T="${3:-60}"; W="${4:-400}"
P="$("$H/original.sh" pid)"; C="$(mktemp)"
cat > "$C" <<GDB
set pagination off
set confirm off
set print thread-events off
handle SIGUSR1 SIGUSR2 SIGSEGV SIGPIPE SIGALRM SIGCHLD SIGIO SIGQUIT nostop noprint pass
set \$n = 0
$(for a in ${A//,/ }; do echo "hbreak *$a"; done)
$(if [ -n "${BRK_COND:-}" ]; then n=$(echo ${A//,/ } | wc -w); for i in $(seq 1 $n); do echo "condition $i ${BRK_COND}"; done; fi)
commands $(n=$(echo ${A//,/ } | wc -w); [ "$n" = 1 ] && echo 1 || echo 1-$n)
silent
set \$n = \$n + 1
printf "hit %d at %x: tick %u eax %x ecx %x edx %x ebx %x esi %x edi %x ebp %x esp %x\n", \$n, \$pc, *(unsigned*)(*(unsigned*)0x0080239c + 0x1da70c), \$eax, \$ecx, \$edx, \$ebx, \$esi, \$edi, \$ebp, \$esp
${BRK_PRINT:-}
set \$i = 0
while \$i < $W
set \$w = *(unsigned*)(\$esp + 4 * \$i)
if \$w >= 0x00401000 && \$w < 0x00700000
printf "  esp+0x%x: 0x%08x\n", 4 * \$i, \$w
end
set \$i = \$i + 1
end
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
