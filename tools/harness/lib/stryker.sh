#!/usr/bin/env bash
# stryker.sh - Stryker.NET's mechanical bugs (a comparison turned, a statement dropped, a string emptied) on the files
# an item changed, in a throwaway copy of the tree; prints the run's counts, then each bug that survived, that no test
# covers or that timed out. It finds the boundary and the
# uncounted line a hand-written list does not think of; it does not replace that list, which holds the bugs that mean
# something (a field's offset, an order, a hand-over).
#   stryker.sh <OpenTPW|OpenTPW.Files|OpenTPW.Common> <File.cs> [File.cs ...]     one project a run
# About 6 minutes for the first file and little more for each after it (the copy, the build and the coverage are the
# cost): give it every changed file of the project at once. Never run Stryker in the main tree.
# One run at a time: the copy is always <work>/stryker/tree ($OPENTPW_STRYKER_WORK/tree if set), removed at the end; Stryker's own output is run.log beside it.
set -euo pipefail
. "$(dirname "$0")/paths.sh"
: "${OPENTPW_GAME_PATH:?set OPENTPW_GAME_PATH}"
P="$1"; shift; W="${OPENTPW_STRYKER_WORK:-$WORK/stryker}"; T="$W/tree"; O="$W/out-$$"
mkdir -p "$W"; rsync -a --delete --exclude .git --exclude '*.iso' --exclude ReferenceScreenshots --exclude /shots --exclude .claude "$REPO/" "$T/"
M=(); for f in "$@"; do M+=(--mutate "**/$f"); done
export OPENTPW_GAME_PATH PATH="$DOTNET_ROOT:$PATH"
( cd "$T/source/OpenTPW.Tests" && "${OPENTPW_STRYKER:-dotnet-stryker}" --project "$P.csproj" "${M[@]}" --reporter json -O "$O" > "$W/run.log" 2>&1 ) || { tail -5 "$W/run.log"; rm -rf "$T"; exit 1; }
grep -E "mutants will be tested|final mutation score|Time Elapsed" "$W/run.log" | sed 's/^\[[^]]*\] //'
python3 - "$O" "$@" <<'PY'
import json, glob, os, sys, collections
r = json.load(open(sorted(glob.glob(sys.argv[1] + '/reports/*.json'))[-1])); names = sys.argv[2:]
for path, d in r['files'].items():
    if os.path.basename(path) not in names: continue
    print(os.path.basename(path), dict(collections.Counter(m['status'] for m in d['mutants']))); src = d['source'].split('\n')
    for m in d['mutants']:
        if m['status'] in ('Survived', 'NoCoverage', 'Timeout'):
            l = m['location']['start']['line']; print(f"  {m['status']:10} line {l}: {src[l-1].strip()[:90]}  ->  {m.get('replacement', '')[:60]}")
PY
rm -rf "$T" "$O"
