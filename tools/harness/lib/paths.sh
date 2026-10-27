# paths.sh - the shell scripts' copy of paths.py: `. "$(dirname "$0")/../lib/paths.sh"` (from original/) or
# `. "$(dirname "$0")/paths.sh"` (from lib/). The variables are the ones paths.py's docstring lists.
HLIB="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HARNESS="$(dirname "$HLIB")"
HORIG="$HARNESS/original"
REPO="$(dirname "$(dirname "$HARNESS")")"
WORK="${OPENTPW_HARNESS_WORK:-$HOME/.cache/opentpw-harness}"
TPW_ORIGINAL="${TPW_ORIGINAL:-$HOME/Games/TPW-original}"
OSAVE="$TPW_ORIGINAL/save"
DOTNET="${OPENTPW_DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"
export DOTNET_ROOT="${DOTNET_ROOT:-$(dirname "$(readlink -f "$DOTNET")")}"
OLOGS="$WORK/original/logs"; mkdir -p "$OLOGS"
XVFB="${OPENTPW_XVFB:-$(command -v Xvfb || echo Xvfb)}"
