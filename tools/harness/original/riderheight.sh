#!/usr/bin/env bash
# riderheight.sh [HITS] [SECS] - a seated rider's sprite each frame, in the ORIGINAL (Q1005), no sale. One brk.sh session on 0x004fa087
# (FUN_004fa030's test of the person's +0x28): every person with it set and the sprite's +0x8c above 1.5 (a till's guest reads 0.98) holds the
# game some ms and prints `RIDER handle H held F sprite x X y Y z Z drawn-y D` (drawn-y is +0xe8, the previous frame's draw), after brk.sh's
# `hit N at 4fa087: tick T ...` line. HITS default 60, SECS default 150. Load the park and put the camera on the ride first
# (q1005/orig/range.sh does, for the Belly Bounce). Run it beside the game on :77.
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"
S='*(unsigned*)(*(unsigned*)0x7b49f0 + 4 * *(int*)($esi + 0xc))'
BRK_COND="*(unsigned*)(\$esi + 0x28) != 0 && *(float*)($S + 0x8c) > 1.5" BRK_PRINT='set $s = *(unsigned*)(*(unsigned*)0x7b49f0 + 4*(*(int*)($esi+0xc)))
printf "RIDER handle %d held %x sprite x %f y %f z %f drawn-y %f\n", *(int*)($esi+0xc), *(unsigned*)($esi+0x28), *(float*)($s+0x88), *(float*)($s+0x8c), *(float*)($s+0x90), *(float*)($s+0xe8)' exec "$H/brk.sh" 0x004fa087 "${1:-60}" "${2:-150}" 0 | grep -E '^(hit|RIDER)'
