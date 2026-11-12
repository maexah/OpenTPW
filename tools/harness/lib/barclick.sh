#!/bin/sh
# barclick.sh <stdout.txt> first|second [window height]: a real click on the message bar's first or second button in a
# consolerun.py run (a `SECS !tools/harness/lib/barclick.sh <outdir>/stdout.txt first` line), the console's `click`
# reaching only the window stack and not the gadget. The bar must be out and its last `tags` reply in <stdout.txt>
# (`bar: out=1 rect=X,Y,...`). The buttons sit at the bar's +(28,9)..(131,112) and +(28,117)..(131,219)
# (ParkGadget, 0x10d30 and 0x10d2f); the virtual screen is 1536 high and pinned left and bottom, so on a window at
# least 4:3 wide a point is its virtual one times the window's height over 1536 (default height 720, consolerun's).
out=$1; which=${2:-first}; high=${3:-720}
line=$(grep -o 'bar: out=1 rect=-\{0,1\}[0-9]*,[0-9]*' "$out" | tail -1)
[ -z "$line" ] && { echo "barclick: no bar out in $out"; exit 1; }
a=$(echo "$line" | sed 's/.*rect=\(-\{0,1\}[0-9]*\),.*/\1/'); b=$(echo "$line" | sed 's/.*,\([0-9]*\)$/\1/')
[ "$which" = second ] && dy=168 || dy=60
x=$(( (a + 79) * high / 1536 )); y=$(( (b + dy) * high / 1536 ))
echo "barclick: $which button at $x $y from bar $a $b"
w=$(xdotool search --onlyvisible --name . | head -1); xdotool windowfocus --sync "$w" mousemove --window "$w" "$x" "$y" click 1
