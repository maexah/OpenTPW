#!/bin/bash
# gdump.sh [--print] <out-dir> <addr>... - dump each address's function (disassembly, decompile, who refers to it) to
# files in one Ghidra call, through the private copy (`gd run_python`), so the reading is grep, not one tool call per
# function. --print prints the code for the real project's mcp__ghidra__run_python instead (lane A: paste it as the
# `code` argument, one call). Lane B and reviewers use the private copy only. See gdump_ghidra.py for the files.
. "$(dirname "$0")/paths.sh"
P=0; [ "$1" = "--print" ] && { P=1; shift; }
OUT=$(realpath -m "$1"); shift
A=$(printf '"%s",' "$@"); A="[${A%,}]"
CODE="ARGS={\"out\": \"$OUT\", \"addrs\": $A}; exec(open(\"$HLIB/gdump_ghidra.py\").read(), globals())"
if [ $P = 1 ]; then echo "$CODE"; exit 0; fi
exec "${OPENTPW_GHIDRA_CLIENT:?set OPENTPW_GHIDRA_CLIENT (paths.py)}" run_python "code=$CODE"
