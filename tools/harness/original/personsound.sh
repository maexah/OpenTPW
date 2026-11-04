#!/usr/bin/env bash
# personsound.sh MODE [HITS] [SECS] - the ORIGINAL's person sounds (Q856): brk.sh on Person_PlaySound (0x004faa00, the sprite's
# +0x88/+0x8c/+0x90 read through the sprite table [0x007b49f0] by the id at [ecx+0xc]) and on the Sound_PlayEffect it calls
# (0x0051bfc0 when the return is 0x004faa52, the place's three ints at [esp+16] x, [esp+20] height, [esp+24] z). Each call is two lines:
#   PS id I cat C eff E sprite x X y Y z Z held F drawn-y D       (the sprite as the person asks; F the PERSON's +0x28, which FUN_004fa030 tests at 0x004fa087: nought for a walker)
#   SPE eff E ints X H Z                                            (what the mixer is handed: floats cut toward nought by _ftol)
# MODE plateau  only a sprite whose z is over ZMIN (default 500: the stock park's plateau row, cells (40..64, 58), ground 20.0)
#      riders   only a person whose +0x28 is set (a rider a ride holds, by the decode; the sprite's own +0x28 is a float, not that flag); 'all' every person (a hit holds the game for ~ms: use HITS)
#      putoff   only kids effect 0x80 (a guest put off: a sale of a ride clears the rider's +0x28 BEFORE it sounds, 0x004fb3df, so `riders` cannot see it: Q1005)
# HITS counts the PS and SPE lines together (default 60); SECS default 150. Output on stdout; run it beside the game (brk.sh's rules).
set -euo pipefail
H="$(cd "$(dirname "$0")" && pwd)"; M="${1:-plateau}"; ZMIN="${ZMIN:-500}"
S='*(unsigned*)(*(unsigned*)0x7b49f0 + 4 * *(int*)($ecx + 0xc))'
RET='*(int*)$esp == 0x4faa52'
case "$M" in
plateau) PS="*(float*)($S + 0x90) > $ZMIN"; SPE="*(int*)(\$esp + 24) > $ZMIN";;
riders)  PS="*(unsigned*)(\$ecx + 0x28) != 0"; SPE='!$_isvoid($rf) && $rf == 1';;
putoff)  PS="*(int*)(\$esp + 8) == 0x80"; SPE='!$_isvoid($rf) && $rf == 1';;
all)     PS="1"; SPE="1";;
*) echo "usage: personsound.sh plateau|riders|putoff|all [HITS] [SECS]" >&2; exit 2;;
esac
BRK_INIT='set $rf = 0' BRK_COND="(\$pc == 0x4faa00 && $PS) || (\$pc == 0x51bfc0 && $RET && $SPE)" BRK_PRINT='if $pc == 0x4faa00
set $rf = 1
set $id = *(int*)($ecx+0xc)
set $s = *(unsigned*)(*(unsigned*)0x7b49f0 + 4*$id)
printf "PS id %d cat %x eff %x sprite x %f y %f z %f held %x drawn-y %f\n", $id, *(int*)($esp+4), *(int*)($esp+8), *(float*)($s+0x88), *(float*)($s+0x8c), *(float*)($s+0x90), *(unsigned*)($ecx+0x28), *(float*)($s+0xe8)
else
set $rf = 0
printf "SPE eff %x ints %d %d %d\n", *(int*)($esp+12), *(int*)($esp+16), *(int*)($esp+20), *(int*)($esp+24)
end' exec "$H/brk.sh" 0x004faa00,0x0051bfc0 "${2:-60}" "${3:-150}" 0 | grep -E '^(PS|SPE)'
