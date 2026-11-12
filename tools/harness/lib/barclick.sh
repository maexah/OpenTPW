#!/bin/sh
# barclick.sh <stdout.txt> first|second [window height]: a real click on the message bar's first or second button in a
# consolerun.py run (a `SECS !tools/harness/lib/barclick.sh <outdir>/stdout.txt first` line after a `tags` with the bar
# out), the console's `click` reaching only the window stack and not the gadget. The point and the click are
# consoledrive.py's (`bar_button`, `window_click`; a consoledrive script calls `Drive.bar_click`). It acts only on the
# run's own Xvfb: CONSOLERUN_SID, which consolerun.py gives a `!` line, must lead `run_display` to this DISPLAY and
# XAUTHORITY, and the window must be named exactly `Theme Park World`; else it sends nothing and exits 1 (AGENTS.md rule 12).
exec python3 "$(dirname "$0")/consoledrive.py" barclick "$@"
