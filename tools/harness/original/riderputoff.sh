#!/usr/bin/env bash
# riderputoff.sh [SECS] (env HITS, default 8, caps the lines: the RIDER line, then PS and SPE a rider, so four riders need HITS=9; the PS condition is any kids 0x80 sound, a rider's line is known by id = the RIDER handle) - a ride's SALE with its riders seated, in the ORIGINAL (Q1005): the sale's DELETE ITEM box must be open already
# (the ride's window, the bomb (185,435); the pointer is then put on the tick (707,283)), and the sale puts every rider off in one frame, which
# needs them aboard (a ride's riders are aboard a few seconds in a cycle of tens). One gdb session on three addresses (brk.sh):
#   0x004fa087  FUN_004fa030's test of the person's +0x28 (the ride holds the sprite): the first person with it set HOLDS the game, prints
#               `RIDER handle H held F sprite x X y Y z Z drawn-y D` (the sprite as a seated rider has it) and presses the tick (the game is
#               held; the button goes down now and up 0.5 s later, after the detach), so the sale is the next frame's
#   0x004faa00  Person_PlaySound with kids effect 0x80 (a rider put off): `PS id I cat C eff E sprite x X y Y z Z held F drawn-y D`
#   0x0051bfc0  Sound_PlayEffect from it (return 0x004faa52): `SPE eff E ints X H Z`, the three ints the mixer is handed
# A till's guest has +0x28 set too (the Spray's, height 0.98): the trigger wants the sprite above 1.5 as well, a seat's (ours' seats read 9.5 to 11.9).
# (personsound.sh's lines; the sale clears the person's +0x28 before the sound, 0x004fb3df, so `held` there is 0.) Run it beside the game on :77.
# If a rider's sprite is the ride's (a ride that keeps none, like the Crazy Ape, has no +0x28 and never triggers), nothing prints.
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; SECS="${1:-120}"
S='*(unsigned*)(*(unsigned*)0x7b49f0 + 4 * *(int*)($esi + 0xc))'
RET='*(int*)$esp == 0x4faa52'
BRK_INIT='set $trig = 0
set $rf = 0' BRK_COND="(\$pc == 0x4fa087 && \$trig == 0 && *(unsigned*)(\$esi + 0x28) != 0 && *(float*)($S + 0x8c) > 1.5) || (\$pc == 0x4faa00 && *(int*)(\$esp + 8) == 0x80) || (\$pc == 0x51bfc0 && $RET && \$rf == 1)" BRK_PRINT='if $pc == 0x4fa087
set $trig = 1
set $s = *(unsigned*)(*(unsigned*)0x7b49f0 + 4*(*(int*)($esi+0xc)))
printf "RIDER handle %d held %x sprite x %f y %f z %f drawn-y %f\n", *(int*)($esi+0xc), *(unsigned*)($esi+0x28), *(float*)($s+0x88), *(float*)($s+0x8c), *(float*)($s+0x90), *(float*)($s+0xe8)
shell DISPLAY=:77 xdotool mousedown 1; (sleep 0.5; DISPLAY=:77 xdotool mouseup 1) >/dev/null 2>&1 &
else
if $pc == 0x4faa00
set $rf = 1
set $id = *(int*)($ecx+0xc)
set $s = *(unsigned*)(*(unsigned*)0x7b49f0 + 4*$id)
printf "PS id %d cat %x eff %x sprite x %f y %f z %f held %x drawn-y %f\n", $id, *(int*)($esp+4), *(int*)($esp+8), *(float*)($s+0x88), *(float*)($s+0x8c), *(float*)($s+0x90), *(unsigned*)($ecx+0x28), *(float*)($s+0xe8)
else
set $rf = 0
printf "SPE eff %x ints %d %d %d\n", *(int*)($esp+12), *(int*)($esp+16), *(int*)($esp+20), *(int*)($esp+24)
end
end' exec "$H/brk.sh" 0x004fa087,0x004faa00,0x0051bfc0 "${HITS:-8}" "$SECS" 0 | grep -E '^(RIDER|PS|SPE)'
