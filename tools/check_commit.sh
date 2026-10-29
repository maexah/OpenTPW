#!/bin/bash
# check_commit.sh [commit] [game folder] - AGENTS.md's "build and test alone in a worktree" as one command, for any
# contributor or agent. Checks the commit out in a throwaway worktree, builds it, runs the tests, runs the content guard
# on the files the commit changes, prints one summary and removes the worktree. Never commits, pushes or launches the
# game; the tests read the game folder and write nothing to its save/.
#   [commit]       default HEAD
#   [game folder]  default $OPENTPW_GAME_PATH; without one the tests that need the game skip, and the summary says so
# CHECK_DIR=<folder> puts the worktree there (default a new folder under $TMPDIR or /tmp; a build takes about 1 GB).
# Where systemd-run --user works, the tests run under tools/harness/lib/capped.sh (6 GB), so a runaway test dies alone.
# Needs git, python3 and the .NET 10 SDK (dotnet on PATH, else $DOTNET_ROOT/dotnet or ~/.dotnet/dotnet).
# Exit 0 when the build has no error, no test fails and the guard refuses nothing; 1 otherwise; 2 on a usage error.
set -u
root=$(git rev-parse --show-toplevel 2>/dev/null) || { echo "check_commit: run it inside the repository" >&2; exit 2; }
commit=$(git -C "$root" rev-parse --verify --quiet "${1:-HEAD}^{commit}") || { echo "check_commit: no commit '${1:-HEAD}'" >&2; exit 2; }
game=${2:-${OPENTPW_GAME_PATH:-}}
dotnet=$(command -v dotnet || true)
[ -z "$dotnet" ] && for d in "${DOTNET_ROOT:-}" "$HOME/.dotnet"; do [ -n "$d" ] && [ -x "$d/dotnet" ] && { dotnet="$d/dotnet"; break; }; done
[ -z "$dotnet" ] && { echo "check_commit: no dotnet (PATH, \$DOTNET_ROOT or ~/.dotnet)" >&2; exit 2; }
export DOTNET_ROOT=${DOTNET_ROOT:-$(dirname "$(readlink -f "$dotnet")")} DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
work=${CHECK_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/check_commit.XXXXXX")}
tree="$work/tree"
[ -e "$tree" ] && { echo "check_commit: $tree exists; give a new CHECK_DIR" >&2; exit 2; }
mkdir -p "$work"
cleanup() { git -C "$root" worktree remove --force "$tree" >/dev/null 2>&1; [ -z "${CHECK_DIR:-}" ] && rm -rf "$work"; }
trap cleanup EXIT
git -C "$root" worktree add --quiet --detach "$tree" "$commit" || exit 1
fail=0

echo "== build $(git -C "$root" log --oneline -1 "$commit")"
"$dotnet" build "$tree/source/OpenTPW.sln" --no-incremental >"$work/build.txt" 2>&1
errors=$(grep -Eo '^ *[0-9]+ Error\(s\)' "$work/build.txt" | grep -Eo '[0-9]+' | tail -1)
warnings=$(grep -Eo '^ *[0-9]+ Warning\(s\)' "$work/build.txt" | grep -Eo '[0-9]+' | tail -1)
[ "${errors:-x}" = 0 ] || { fail=1; tail -20 "$work/build.txt"; }

passed=0; failed=0; skipped=0; total=0; tests="not run (the build failed)"
if [ "${errors:-x}" = 0 ]; then
	wrap=()
	systemd-run --user --scope --quiet --collect true >/dev/null 2>&1 && wrap=("$root/tools/harness/lib/capped.sh")
	echo "== test${game:+ with the game at $game}${wrap:+ (capped at 6 GB)}"
	( [ -n "$game" ] && export OPENTPW_GAME_PATH="$game" || unset OPENTPW_GAME_PATH
	  SDL_AUDIODRIVER=dummy "${wrap[@]}" "$dotnet" test "$tree/source/OpenTPW.sln" --no-build ) >"$work/test.txt" 2>&1
	code=$?
	# one line per test project: "Passed!  - Failed: 0, Passed: 1648, Skipped: 2637, Total: 4285, ..."
	while read -r f p s t; do
		failed=$((failed + f)); passed=$((passed + p)); skipped=$((skipped + s)); total=$((total + t))
	done < <(sed -nE 's/.*Failed: *([0-9]+), Passed: *([0-9]+), Skipped: *([0-9]+), Total: *([0-9]+).*/\1 \2 \3 \4/p' "$work/test.txt")
	tests="$passed passed, $failed failed, $skipped skipped, of $total"
	if [ "$code" -ne 0 ] || [ "$failed" -ne 0 ] || [ "$total" -eq 0 ]; then
		fail=1; grep -E 'Failed |error|Exit code|137' "$work/test.txt" | head -20
		[ "$code" = 137 ] && tests="$tests; killed at the memory cap (exit 137)"
	fi
fi

echo "== content guard"
changed=$(git -C "$root" diff-tree --no-commit-id --name-only -r --diff-filter=ACMR --root "$commit")
if [ -z "$changed" ]; then guard="no file added or changed"
else
	# shellcheck disable=SC2086
	if (cd "$root" && python3 tools/content_guard.py paths $changed); then guard="passed ($(echo "$changed" | wc -l) files)"
	else guard="REFUSED"; fail=1; fi
fi

if [ -z "$game" ]; then gameline="not given: the tests that need it skipped, so this is not a full pass"
elif [ ! -d "$game" ]; then gameline="$game is not a folder: the tests that need it skipped"; fail=1
else gameline="$game"; fi

echo
echo "== summary of $(git -C "$root" rev-parse --short "$commit")"
echo "build:   ${errors:-?} errors, ${warnings:-?} warnings"
echo "tests:   $tests"
echo "game:    $gameline"
echo "guard:   $guard"
echo "not done here: the running game (a screenshot and its census line), the original, Ghidra: say which you did and which not"
[ "$fail" = 0 ] && echo "result:  OK" || echo "result:  FAILED"
exit "$fail"
