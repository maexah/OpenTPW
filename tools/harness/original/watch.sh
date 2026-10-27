#!/usr/bin/env bash
# watch.sh - which code of the ORIGINAL writes a piece of its memory: a hardware watchpoint through gdb, attached to
# the running game and detached again. Prints the program counter after each write (the instruction before it is the
# writer: read it in Ghidra, the addresses are the listing's) and the new value.
#   watch.sh 'EXPR' SIZE [HITS] [SECS]
#     EXPR  a gdb expression for the ADDRESS, e.g. '*(unsigned*)0x7cf83c + 0x1da70c' (mGameTick) or 0x007b49f4
#     SIZE  1, 2 or 4 bytes;  HITS  stop after this many writes (default 5);  SECS  give up after this long (default 20)
#   WATCH=rwatch (reads) or WATCH=awatch (reads and writes) instead of writes; the pc printed is then after the reader.
# The game is held while gdb attaches and at each hit (a few ms); it runs on after the detach.
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; E="$1"; Z="${2:-4}"; N="${3:-5}"; T="${4:-20}"
case "$Z" in 1) TY="unsigned char";; 2) TY="unsigned short";; 4) TY="unsigned int";; *) echo "SIZE is 1, 2 or 4" >&2; exit 1;; esac
P="$("$H/original.sh" pid)"; C="$(mktemp)"; W="${WATCH:-watch}"
cat > "$C" <<GDB
set pagination off
set confirm off
set print thread-events off
handle SIGUSR1 SIGUSR2 SIGSEGV SIGPIPE SIGALRM SIGCHLD SIGIO SIGQUIT nostop noprint pass
set \$n = 0
set \$a = (unsigned long)($E)
printf "watching %d bytes at 0x%lx, now %u\n", $Z, \$a, *($TY *)\$a
$W *($TY *)\$a
commands
silent
set \$n = \$n + 1
printf "hit %d: pc 0x%lx value %u\n", \$n, (unsigned long)\$pc, *($TY *)\$a
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
