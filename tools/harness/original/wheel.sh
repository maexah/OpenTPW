#!/usr/bin/env bash
# wheel.sh [PID] NOTCHES [N] - N (default 1) mouse-wheel messages of NOTCHES x 120 (negative = toward the player, a zoom out) handed to the
# running ORIGINAL's window procedure (FUN_0046b600, WM_MOUSEWHEEL 0x20a) by gdb, in the process itself; PID defaults to original.sh pid.
# The pointer must already stand where the wheel is wanted (gmove.py): the procedure takes no position for the wheel, it posts 0x1000d to
# the control the pointer is over (hovered [0x00faa5e0], or the capture [0x00faa5e8]; UI_QueueMessage, 0x00658ccc). From there on it is
# the game's own code. Why not xdotool: its buttons 4 and 5 (the X wheel) reach the original, under Proton on a private Xvfb, as nothing
# (Q930: three notches over a scrolling Load Park list scrolled it by none; the same three through this scrolled it by three rows).
# Read the park camera's zoom afterwards with camera.py (a notch over the park moves it by 10 and the update clamps it to 70..130).
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
if [ $# -ge 2 ] && [ "$1" -gt 1000 ] 2>/dev/null; then PID=$1; shift; else PID="$("$H/original.sh" pid)"; fi
K=$1; N=${2:-1}
W=$(( (K * 120) & 0xffff ))
C="$(mktemp)"
cat > "$C" <<GDB
set pagination off
set confirm off
set debuginfod enabled off
handle SIGUSR1 SIGUSR2 SIGSEGV SIGPIPE SIGALRM SIGCHLD SIGIO SIGQUIT nostop noprint pass
set \$i = 0
while \$i < $N
  call ((int(*)(int,unsigned,unsigned,unsigned))0x0046b600)(0, 0x20a, ($W << 16), (300 << 16) | 400)
  set \$i = \$i + 1
end
detach
quit
GDB
timeout 20 gdb -q -batch -p "$PID" -x "$C" 2>&1 | grep -v "^\[New LWP\|^\[Thread debugging\|^Using host\|warning:\|^$\|ebuginfo\|^To make this setting\|^  <https" || true
rm -f "$C"
