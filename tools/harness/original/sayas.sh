#!/usr/bin/env bash
# sayas.sh RESPONSE[,RESPONSE...] [SECS] - the ORIGINAL's next lines said as the responses given, in order: a hardware
# breakpoint through gdb at Advisor_SayResponse (0x00599050), and at each call that will say a line (the advisor on,
# 0x0078d90c, and not busy, g_Advisor_Busy 0x00f7966c) its one argument, the response id at [esp+4], written over
# with the next of the list. Everything the line is then comes from that response's row of the table at 0x00768fb8:
# its sample, lip file, gesture row, slot and props. The message's own record (its line, its tick) and the event the
# caller sends after it (the tag's group) stay the message's that was really posted. Detaches after the last one.
#   sayas.sh 18,37,82 600     the next three lines said as responses 18, 37 and 82; gives up after 600 s
# It WRITES the original's stack (as thingpoke.py writes its memory): never save a park under it. A line a swap:
# `said <was> as <now>: tick <mGameTick>`. Read what the line plays beside it with advisorline.py (Q596).
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; L="$1"; T="${2:-600}"
P="$("$H/original.sh" pid)"; C="$(mktemp)"; N=$(echo ${L//,/ } | wc -w)
{
cat <<GDB
set pagination off
set confirm off
set print thread-events off
handle SIGUSR1 SIGUSR2 SIGSEGV SIGPIPE SIGALRM SIGCHLD SIGIO SIGQUIT nostop noprint pass
set \$n = 0
hbreak *0x00599050
commands 1
silent
if *(unsigned char*)0x0078d90c != 0 && *(int*)0x00f7966c == 0
set \$was = *(int*)(\$esp + 4)
GDB
i=0; for r in ${L//,/ }; do echo "if \$n == $i"; echo "set \$want = $r"; echo "end"; i=$((i + 1)); done
cat <<GDB
set *(int*)(\$esp + 4) = \$want
set \$n = \$n + 1
printf "said %d as %d: tick %u\n", \$was, \$want, *(unsigned*)(*(unsigned*)0x0080239c + 0x1da70c)
if \$n >= $N
detach
quit
end
end
continue
end
continue
detach
quit
GDB
} > "$C"
timeout -s INT "$T" gdb -q -batch -p "$P" -x "$C" 2>&1 | grep --line-buffered -v "^\[New LWP\|^\[Thread debugging\|^Using host\|warning:\|^$" || true
rm -f "$C"
