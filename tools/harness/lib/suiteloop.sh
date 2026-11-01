#!/bin/bash
# suiteloop.sh OUTDIR [N] - run the whole suite with the game up to N times (default 10), each run's whole output kept
# in OUTDIR/run-NN.out and one line per run in OUTDIR/summary.txt; stops at the first run that fails, then writes "STOP at NN".
# The last line is "DONE". It changes to the repo root itself; run it after a build; it does not build. For a one-off failure (Q942): the
# failing run's file names the test. Needs OPENTPW_GAME_PATH, DOTNET_ROOT where dotnet is not on PATH, and sets
# SDL_AUDIODRIVER=dummy. Start it with `nohup setsid ... &` and watch summary.txt: a foreground call ends at ~600 s.
set -u
OUT=${1:?usage: suiteloop.sh OUTDIR [N]}
N=${2:-10}
case $N in ''|*[!0-9]*) echo "usage: suiteloop.sh OUTDIR [N]  (N a whole number)" >&2; exit 2;; esac
: "${OPENTPW_GAME_PATH:?name the game folder}"
export SDL_AUDIODRIVER=dummy
mkdir -p "$OUT"
OUT=$(readlink -f "$OUT")
: > "$OUT/summary.txt"
HERE=$(dirname "$(readlink -f "$0")")
cd "$HERE/../../.." || exit 1
DOTNET=${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}
for ((i = 1; i <= N; i++)); do
	n=$(printf '%02d' "$i")
	"$HERE/capped.sh" "$DOTNET" test source/OpenTPW.sln --no-build > "$OUT/run-$n.out" 2>&1
	rc=$?
	echo "run $n rc=$rc $(date -u +%H:%M:%S) $(grep -E '^(Passed|Failed)!' "$OUT/run-$n.out" | tr '\n' ' ')" >> "$OUT/summary.txt"
	if [ $rc -ne 0 ]; then echo "STOP at $n" >> "$OUT/summary.txt"; break; fi
done
echo DONE >> "$OUT/summary.txt"
