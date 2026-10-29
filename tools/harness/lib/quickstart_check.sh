#!/bin/bash
# quickstart_check.sh <source tree> <out dir> [private game folder] - CONTRIBUTING.md's Linux quick start, run from an
# empty home folder with a cleaned environment (a new HOME and NuGet cache, a new TMPDIR, a fresh copy of the tree; NOT
# a container or a fresh distribution image: the system's packages, the Vulkan driver and the .NET SDK are the
# machine's own): git init and the hooks path, build, the tests with no game, a staged .wad refused by the hook, and
# (with a game folder) a launch under Xvfb and lavapipe whose console.log must reach the lobby.
#   <source tree>   a folder holding the repository's files (a git clone or working tree: the files git tracks or would track)
#   <out dir>       made fresh; holds home/ (the run's HOME), game/, the transcript in quickstart.log
#   [game folder]   a PRIVATE game folder (docs/TOOLING.md, "Off-screen runs": ~/.cache/tpw-harnesses/lib/game-off); it
#                   is copied (its links stay links into the game) and its save/ replaced by an empty one in the copy.
#                   Without it the launch step is skipped.
# Needs: rsync, the .NET SDK at $DOTNET_ROOT (default ~/.dotnet), Xvfb ($OPENTPW_XVFB, else on PATH), python3, git.
# Exit 0 when every step it ran passed. The network is used (the NuGet restore).
set -u
src=$(readlink -f "${1:?source tree}"); out=${2:?out dir}; game=${3:-}
dotnet_root=${DOTNET_ROOT:-$HOME/.dotnet}
xvfb=${OPENTPW_XVFB:-$(command -v Xvfb || true)}
[ -e "$out" ] && { echo "quickstart_check: $out exists; give a new folder" >&2; exit 2; }
mkdir -p "$out/home/tmp" && out=$(readlink -f "$out")
# the files a clone would hold (tracked, plus new ones git does not ignore): the maintainer's untracked bits are left out
mkdir -p "$out/home/OpenTPW"
git -C "$src" ls-files -z --cached --others --exclude-standard | rsync -a --from0 --files-from=- "$src/" "$out/home/OpenTPW/"
if [ -n "$game" ]; then
	mkdir -p "$out/game" && cp -a "$(readlink -f "$game")/." "$out/game/" && rm -rf "$out/game/save" && mkdir "$out/game/save"
fi
fail=0
step() { echo; echo "== $1"; }
{
	export HOME="$out/home" TMPDIR="$out/home/tmp" DOTNET_ROOT="$dotnet_root" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
	export PATH="$dotnet_root:/usr/bin:/bin"
	unset OPENTPW_GAME_PATH XDG_DATA_HOME XDG_CONFIG_HOME XDG_CACHE_HOME
	cd "$HOME/OpenTPW" || exit 2
	step "git init and the hooks path (CONTRIBUTING: Quick start), HOME=$HOME"
	git init -q . && git config user.email a@b.c && git config user.name clean && git config core.hooksPath tools/hooks && git config core.hooksPath || fail=1
	step "dotnet build source/OpenTPW.sln --no-incremental"
	dotnet build source/OpenTPW.sln --no-incremental 2>&1 | tail -4 | tee "$TMPDIR/build.txt"; grep -q ' 0 Error(s)' "$TMPDIR/build.txt" || fail=1
	step "dotnet test source/OpenTPW.sln --no-build   (no game)"
	SDL_AUDIODRIVER=dummy dotnet test source/OpenTPW.sln --no-build 2>&1 | grep -E 'Passed!|Failed!' | tee "$TMPDIR/test.txt"; grep -q 'Passed!' "$TMPDIR/test.txt" || fail=1
	step "the pre-commit hook on a staged .wad"
	echo x > evil.wad; git add evil.wad
	if tools/hooks/pre-commit; then echo "NOT REFUSED"; fail=1; else echo "refused, as it should be"; fi
	git reset -q evil.wad; rm -f evil.wad
	if [ -d "$out/game" ]; then
		step "launch under Xvfb and lavapipe (20 s), then console.log"
		"$xvfb" :99 -screen 0 1280x720x24 -nolisten tcp >/dev/null 2>&1 & xpid=$!; sleep 2
		mkdir -p "$HOME/logs"
		DISPLAY=:99 SDL_VIDEODRIVER=x11 VK_DRIVER_FILES=/usr/share/vulkan/icd.d/lvp_icd.json VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/lvp_icd.json \
			SDL_AUDIODRIVER=dummy OPENTPW_LOG_DIR="$HOME/logs" timeout 20 dotnet source/OpenTPW/bin/Debug/net10.0/OpenTPW.dll --game "$out/game" >"$HOME/logs/stdout.txt" 2>&1
		kill $xpid 2>/dev/null
		grep -E 'Game data|GPU:|Loading the lobby' "$HOME/logs/console.log" || fail=1
		grep -q 'Loading the lobby' "$HOME/logs/console.log" || fail=1
	fi
	echo; [ $fail = 0 ] && echo "QUICKSTART OK" || echo "QUICKSTART FAILED"
} 2>&1 | tee "$out/quickstart.log"
grep -q 'QUICKSTART OK' "$out/quickstart.log"
