#!/bin/bash
# run.sh profile OUT | run.sh sweep LO HI OUT [ONLYFILE] [CLASSES] - after apply.py and a build (Q1023).
#   profile: every test of the sweep's classes once at seed 0; OUT.profile has a line a test: draws, the draws by generator, ms.
#            SWEEP_ALL=1 in the environment takes every test class, not the nine files and ScriptSoundTests.
#            The tests with draws > 0 are the only ones a seed can change: `grep -v 'draws 0 ' OUT.profile`.
#   sweep:   those tests once per seed LO..HI in one process (about 3 s a seed for 26 tests); OUT has a line a failure
#            (seed, test, first line of the message) and DONE last; OUT.progress a line a seed. ONLYFILE lists Class.Method lines.
# Time.Now is set to 0 before each test: left to grow, a float stops advancing by 1/60 s after about eight passes
# and five tests fail on the clock, not on a seed. Run from anywhere; set OPENTPW_GAME_PATH.
root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel)
export DOTNET_ROOT=${DOTNET_ROOT:-$HOME/.dotnet} SDL_AUDIODRIVER=dummy
dotnet=${DOTNET:-$DOTNET_ROOT/dotnet}
capped=${OPENTPW_HARNESS_WORK:-$HOME/.cache/tpw-harnesses}/lib/capped.sh
[ -x "$capped" ] || capped=$root/tools/harness/lib/capped.sh
case "$1" in
	profile) shift; rm -f "$1.profile"; SWEEP_PROFILE=1 SWEEP_LO=0 SWEEP_HI=0 SWEEP_LOG=$1 "$capped" "$dotnet" test "$root/source/OpenTPW.sln" --no-build --filter "Name=Sweep" ;;
	sweep) shift; env ${4:+SWEEP_ONLY=$4} ${5:+SWEEP_CLASSES=$5} SWEEP_LO=$1 SWEEP_HI=$2 SWEEP_LOG=$3 "$capped" "$dotnet" test "$root/source/OpenTPW.sln" --no-build --filter "Name=Sweep" ;;
	*) sed -n 2,8p "$0"; exit 2 ;;
esac
