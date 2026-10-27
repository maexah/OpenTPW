#!/bin/bash
# funcnames.sh - export every function's entry, end and name from the private Ghidra copy to <work>/funcnames.tsv
# (citecheck.py reads it). Run after the private copy is refreshed from the real project, so the names are ours.
. "$(dirname "$0")/paths.sh"
O=$WORK/funcnames.tsv; mkdir -p "$WORK"
exec "${OPENTPW_GHIDRA_CLIENT:?set OPENTPW_GHIDRA_CLIENT (paths.py)}" run_python "code=OUT=\"$O\"; exec(open(\"$HLIB/funcnames_ghidra.py\").read(), globals())"
