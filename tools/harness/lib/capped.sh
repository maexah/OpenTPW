#!/bin/bash
# capped.sh [-m SIZE] CMD... - run CMD in its own systemd user scope with a memory cap (default 6G, no swap).
# A runaway (a mutant that loops for ever and allocates) is killed alone inside its own scope, so the machine never
# runs out of memory and systemd never stops the terminal's scope, which holds Claude and every session (2026-10-21:
# one dotnet test grew to 12 GB, the global OOM kill stopped the whole Konsole scope). Exit 137 = killed at the cap.
CAP=${CAPPED_MAX:-6G}
if [ "$1" = "-m" ]; then CAP=$2; shift 2; fi
exec systemd-run --user --scope --quiet --collect -p MemoryMax=$CAP -p MemorySwapMax=0 -- "$@"
