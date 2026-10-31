#!/bin/bash
# gclosure.sh [--print] <start-addr> [<watch-addr>...] - what a function can reach: walks the call graph from one function
# through direct calls and says of each watched address whether it was reached (a function) or who refers to it (data), so
# "this path never writes X or calls Y" is measured. --print prints the code for the real project's mcp__ghidra__run_python
# (lane A); otherwise it runs on the private copy (`gd run_python`). See gclosure_ghidra.py.
P=0; [ "$1" = "--print" ] && { P=1; shift; }
S=$1; shift
A=$(printf '"%s",' "$@"); A="[${A%,}]"; [ $# -eq 0 ] && A="[]"
HERE=$(cd "$(dirname "$0")" && pwd)
CODE="ARGS={\"start\": \"$S\", \"watch\": $A}; exec(open(\"$HERE/gclosure_ghidra.py\").read(), globals())"
if [ $P = 1 ]; then echo "$CODE"; exit 0; fi
exec $HOME/.cache/tpw-harnesses/ghidra-private/gd run_python "code=$CODE"
