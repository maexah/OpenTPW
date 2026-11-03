#!/usr/bin/env bash
# researchpark.sh <in park file> <out park file> <item>... : a copy of a jungle park file with each item marked researched, by original/research.py one item at a
# time (Q931). `researchpark.sh Easymode.TPWI out.TPWS 1100 1101 ...`; the Lost Kingdom's twelve rides are 1100 1101 1102 1104 1108 1109 1110 1113 1140 1150 1160 1180
# and its nineteen features 1400 1401 1402 1403 1406 1407 1411 1413 1414 1417 1418 1419 1422 1423 1424 1425 1428 1429 1431 (the console's `catalogue` lists them with
# their type). Each item must be in the file's table once (research.py asserts). <in> is only read.
set -euo pipefail
R="$(cd "$(dirname "$0")/../original" && pwd)/research.py"; IN="$1"; OUT="$2"; shift 2
T="$(mktemp)"; U=""; trap 'rm -f "$T" "$U"' EXIT; cp "$IN" "$T"
for item in "$@"; do U="$(mktemp)"; python3 "$R" "$T" "$U" "$item" >/dev/null; mv "$U" "$T"; done
mv "$T" "$OUT"; chmod 644 "$OUT"; md5sum "$OUT"
