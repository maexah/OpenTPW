#!/usr/bin/env bash
# tpw-setup.sh - sets up the original Sim Theme Park / Theme Park World (1999) to run on Linux with Proton.
#
# What it does, in order:
#   1. Copies your game disc (or its .iso) into ~/Games/TPW.
#   2. Copies your no-CD .exe in beside it, as TP-nodisc.exe.
#   3. Downloads GE-Proton 10-34 and MangoHud 0.8.4 from their official GitHub pages into ~/Games/TPW/.tools,
#      and checks each download against a fixed checksum. (If GE-Proton 10-34 is already in Steam's folder, it uses that.)
#   4. Makes a Proton prefix in ~/Games/TPW/.prefix, writes the three settings the game's own installer would have
#      written, and adds a pretend CD drive (D:) that points at ~/Games/TPW.
#   5. Writes ~/Games/TPW/play.sh (starts the game through Proton, capped at 30 frames a second) and puts
#      "Sim Theme Park" in your app menu.
#
# It never asks for your password and never uses sudo. Outside ~/Games/TPW it adds one menu entry,
# ~/.local/share/applications/sim-theme-park.desktop, and Proton and your graphics driver keep their usual caches in
# ~/.cache. To remove the game, delete ~/Games/TPW and that menu entry.
#
# It does NOT download the game or a no-CD .exe. You need your own disc (or an .iso of it) and your own no-CD .exe.
#
# Usage:
#   bash tpw-setup.sh                                  asks you for the disc and the .exe
#   bash tpw-setup.sh --disc PATH --exe PATH           PATH for the disc can be a folder or an .iso
#   options: --dir PATH (install somewhere other than ~/Games/TPW), --no-menu (no app menu entry)

set -euo pipefail

GE_NAME="GE-Proton10-34"
GE_URL="https://github.com/GloriousEggroll/proton-ge-custom/releases/download/GE-Proton10-34/GE-Proton10-34.tar.gz"
GE_SHA512="9fd0b2cfbd501c0b5c892239c392c7283a029b5e5d5a77d3f85b0ce190d555456241a18eebca16b53f094b403499201c13550a3f0b9b365e1a5eb5737cbb7303"
MH_URL="https://github.com/flightlessmango/MangoHud/releases/download/v0.8.4/MangoHud-0.8.4.r0.g992103e.tar.gz"
MH_SHA256="aa0b2a009552f5e7c50baabf361267a79ea5e5c0fea440786f3b202d51f57753"

DEST="$HOME/Games/TPW"
DISC=""
EXE=""
MENU=1

say()  { printf '\n\033[1m%s\033[0m\n' "$*"; }
info() { printf '   %s\n' "$*"; }
exec 3>&2   # a copy of the terminal, so errors show even while other output goes to the log
die()  { printf '\n\033[1;31mStopped: %s\033[0m\n' "$*" >&3; exit 1; }
trap 'die "Something unexpected went wrong (line $LINENO). Nothing outside $DEST was changed."' ERR

while [ $# -gt 0 ]; do
	case "$1" in
		--disc) DISC="$2"; shift 2 ;;
		--exe) EXE="$2"; shift 2 ;;
		--dir) DEST="$2"; shift 2 ;;
		--no-menu) MENU=0; shift ;;
		-h|--help) sed -n '2,30p' "$0"; exit 0 ;;
		*) die "I don't know the option '$1'. Try: bash tpw-setup.sh --help" ;;
	esac
done

# A path dragged into a terminal can arrive in quotes, or as a file:// link. Turn it back into a plain path.
clean_path() {
	local p="$1"
	p="${p#"${p%%[![:space:]]*}"}"; p="${p%"${p##*[![:space:]]}"}"
	case "$p" in \'*\') p="${p:1:${#p}-2}" ;; \"*\") p="${p:1:${#p}-2}" ;; esac
	case "$p" in file://*) p="${p#file://}"; p="$(printf '%b' "${p//%/\\x}")" ;; esac
	p="${p/#\~/$HOME}"
	printf '%s' "$p"
}

ask_path() {
	local reply
	printf '\n%s\n> ' "$1" >&2
	IFS= read -r reply || die "No answer given."
	clean_path "$reply"
}

has_disc_files() { [ -n "$(find "$1" -maxdepth 1 -iname 'tp.icd' 2>/dev/null | head -1)" ]; }

sha_ok() { # sha_ok FILE ALGO SUM
	local got
	got="$("${2}sum" "$1" | cut -d' ' -f1)"
	[ "$got" = "$3" ]
}

download() { # download URL FILE
	if command -v curl >/dev/null; then curl -fL --retry 3 --progress-bar -o "$2" "$1"
	elif command -v wget >/dev/null; then wget -q --show-progress -O "$2" "$1"
	else die "I need curl or wget to download things. Install one of them and run me again."; fi
}

# ---------------------------------------------------------------------------------------------------------------------
say "Sim Theme Park setup"
info "The game will go in: $DEST"

for tool in python3 tar sha256sum sha512sum find; do
	command -v "$tool" >/dev/null || die "I need '$tool', and it isn't installed. Install it and run me again."
done

# 1. The disc -----------------------------------------------------------------------------------------------------------
if [ -z "$DISC" ]; then
	for d in /run/media/"$USER"/* /media/"$USER"/* /media/* /mnt/*; do
		if [ -d "$d" ] && has_disc_files "$d"; then
			printf '\nI found the game disc at %s. Use it? [Y/n] ' "$d"
			read -r yn || true
			case "$yn" in [Nn]*) ;; *) DISC="$d"; break ;; esac
		fi
	done
fi
if [ -z "$DISC" ]; then
	DISC="$(ask_path "Drag your game disc's folder, or your .iso file, into this window, then press Enter:")"
fi

# Ask for the .exe now and check it, so nothing is copied if it's the wrong file.
if [ -z "$EXE" ] && [ ! -f "$DEST/TP-nodisc.exe" ]; then
	EXE="$(ask_path "Now drag your no-CD .exe into this window, then press Enter:")"
fi
if [ -n "$EXE" ]; then
	[ -f "$EXE" ] || die "I can't find '$EXE'."
	[ "$(head -c 2 "$EXE")" = "MZ" ] || die "'$EXE' isn't a Windows program."
	if grep -aq 'BoG_ \*90.0&!!  Yy>' "$EXE" && [ "$(stat -c %s "$EXE")" -lt 1000000 ]; then
		die "That's the disc's own TP.exe. It's copy-protected (SafeDisc) and won't run under Proton. You need a no-CD .exe."
	fi
fi

mkdir -p "$DEST"
if has_disc_files "$DEST"; then
	say "1/5  The game is already copied into $DEST, so I'm leaving it as it is."
elif [ -d "$DISC" ]; then
	has_disc_files "$DISC" || die "$DISC doesn't look like the game disc (there's no TP.ICD in it)."
	say "1/5  Copying the disc (about 500 MB)..."
	cp -r "$DISC"/. "$DEST"/
elif [ -f "$DISC" ]; then
	say "1/5  Unpacking the .iso (about 500 MB)..."
	if command -v 7z >/dev/null; then 7z x -y -o"$DEST" "$DISC" >/dev/null
	elif command -v bsdtar >/dev/null; then bsdtar -xf "$DISC" -C "$DEST"
	else die "I can't open .iso files on this computer. Double-click your .iso to open it like a disc, then run me again and drag the opened disc's folder in instead."; fi
	has_disc_files "$DEST" || die "That .iso doesn't look like the game disc (there's no TP.ICD in it)."
else
	die "I can't find '$DISC'."
fi
chmod -R u+w "$DEST"
echo TPWORLD > "$DEST/.windows-label"

# 2. The no-CD .exe -----------------------------------------------------------------------------------------------------
if [ -n "$EXE" ]; then
	say "2/5  Copying your no-CD .exe..."
	cp "$EXE" "$DEST/TP-nodisc.exe.part" && mv "$DEST/TP-nodisc.exe.part" "$DEST/TP-nodisc.exe"
	# Some no-CD .exes load USP11.dll, a renamed copy of the game's own usp10.dll.
	if grep -aqi 'usp11\.dll' "$DEST/TP-nodisc.exe" && [ -z "$(find "$DEST" -maxdepth 1 -iname usp11.dll)" ]; then
		near="$(find "$(dirname "$EXE")" -maxdepth 1 -iname usp11.dll | head -1)"
		if [ -n "$near" ]; then cp "$near" "$DEST/USP11.dll"
		else cp "$(find "$DEST" -maxdepth 1 -iname usp10.dll | head -1)" "$DEST/USP11.dll"; fi
		info "Added USP11.dll, which your .exe needs."
	fi
else
	say "2/5  Your no-CD .exe is already there."
fi

# 3. GE-Proton and MangoHud ---------------------------------------------------------------------------------------------
TOOLS="$DEST/.tools"
mkdir -p "$TOOLS"
free_kb="$(df -Pk "$DEST" | awk 'NR==2 {print $4}')"

PROTON_DIR=""
for d in "$HOME/.local/share/Steam/compatibilitytools.d/$GE_NAME" "$HOME/.steam/root/compatibilitytools.d/$GE_NAME" \
	"$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/compatibilitytools.d/$GE_NAME" "$TOOLS/$GE_NAME"; do
	if [ -x "$d/proton" ]; then PROTON_DIR="$d"; break; fi
done
if [ -n "$PROTON_DIR" ]; then
	say "3/5  Using the $GE_NAME that's already on this computer."
else
	[ "$free_kb" -gt 3500000 ] || die "I need about 3.5 GB of free space in $DEST, and there's less than that."
	say "3/5  Downloading $GE_NAME (about 500 MB)..."
	download "$GE_URL" "$TOOLS/ge.tar.gz"
	sha_ok "$TOOLS/ge.tar.gz" sha512 "$GE_SHA512" || { rm -f "$TOOLS/ge.tar.gz"; die "The GE-Proton download didn't match its checksum, so I deleted it. Try again later."; }
	info "Checksum OK. Unpacking..."
	tar -xzf "$TOOLS/ge.tar.gz" -C "$TOOLS"
	rm -f "$TOOLS/ge.tar.gz"
	PROTON_DIR="$TOOLS/$GE_NAME"
fi

MH="$TOOLS/mangohud"
if [ ! -f "$MH/usr/lib/mangohud/lib32/libMangoHud_shim.so" ]; then
	info "Downloading MangoHud (about 10 MB), which caps the game at 30 frames a second..."
	download "$MH_URL" "$TOOLS/mh.tar.gz"
	sha_ok "$TOOLS/mh.tar.gz" sha256 "$MH_SHA256" || { rm -f "$TOOLS/mh.tar.gz"; die "The MangoHud download didn't match its checksum, so I deleted it. Try again later."; }
	mkdir -p "$MH" "$TOOLS/mh-unpack"
	tar -xzf "$TOOLS/mh.tar.gz" -C "$TOOLS/mh-unpack"
	tar -xf "$TOOLS/mh-unpack/MangoHud/MangoHud-package.tar" -C "$MH"
	rm -rf "$TOOLS/mh.tar.gz" "$TOOLS/mh-unpack"
	info "Checksum OK."
fi

# 4. The Proton prefix and the game's settings --------------------------------------------------------------------------
STEAM_DIR="$TOOLS/steam"   # Proton wants a Steam folder; an empty one is enough, and keeps your real Steam untouched.
mkdir -p "$STEAM_DIR"
export STEAM_COMPAT_DATA_PATH="$DEST/.prefix" STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAM_DIR"
export SteamGameId=0 SteamAppId=0 PROTON_USE_XALIA=0
export UMU_ID=umu-tpw   # tells GE-Proton it is running outside Steam, so it does not look for a Steam client
PFX="$DEST/.prefix/pfx"
mkdir -p "$DEST/.prefix"
LOG="$TOOLS/setup.log"

say "4/5  Setting up Proton for the game (this takes a minute)..."
python3 "$PROTON_DIR/proton" run wineboot >>"$LOG" 2>&1 || die "Proton couldn't start. Details are in $LOG."
# The three values the game's installer writes (the 32-bit view, where the game looks), and D: as a CD drive.
cat > "$PFX/drive_c/tpw-setup.reg" <<'EOF2'
REGEDIT4

[HKEY_LOCAL_MACHINE\Software\Wow6432Node\Bullfrog Productions Ltd\Theme Park World]
"Language"=dword:00000409
"Version"="1.1"
"BuildTypeCode"=dword:00000000

[HKEY_LOCAL_MACHINE\Software\Wine\Drives]
"d:"="cdrom"
EOF2
python3 "$PROTON_DIR/proton" run regedit /S 'C:\tpw-setup.reg' >>"$LOG" 2>&1 || die "Proton couldn't save the game's settings. Details are in $LOG."
WINEPREFIX="$PFX" WINEFSYNC=1 timeout 120 "$PROTON_DIR/files/bin/wineserver" -w >>"$LOG" 2>&1 || true
rm -f "$PFX/drive_c/tpw-setup.reg"
ln -sfn "$DEST" "$PFX/dosdevices/d:"
grep -q '"Language"=dword:00000409' "$PFX/system.reg" || die "The game's settings didn't save. Details are in $LOG."
info "Done."

# 5. The launcher and the menu entry ------------------------------------------------------------------------------------
say "5/5  Making the launcher..."
cat > "$DEST/play.sh" <<EOF
#!/usr/bin/env bash
# Starts Sim Theme Park through Proton, capped at 30 frames a second by MangoHud. Made by tpw-setup.sh.
D="$DEST"
export STEAM_COMPAT_DATA_PATH="\$D/.prefix" STEAM_COMPAT_CLIENT_INSTALL_PATH="$STEAM_DIR"
export SteamGameId=0 SteamAppId=0 PROTON_USE_XALIA=0 UMU_ID=umu-tpw
export MANGOHUD=1 MANGOHUD_CONFIG="fps_limit=30,no_display"
M="\$D/.tools/mangohud/usr/lib/mangohud"
export LD_PRELOAD="\${LD_PRELOAD:+\$LD_PRELOAD:}\$M/lib32/libMangoHud_shim.so:\$M/lib64/libMangoHud_shim.so"
days=\$(( \$(cut -d. -f1 /proc/uptime) / 86400 ))
if [ "\$days" -ge 5 ] && command -v notify-send >/dev/null; then
	notify-send "Sim Theme Park" "Your computer has been on for \$days days. If the park runs too fast or freezes, restart your computer and play again."
fi
cd "\$D"
exec python3 "$PROTON_DIR/proton" waitforexitandrun "\$D/TP-nodisc.exe"
EOF
chmod +x "$DEST/play.sh"

if [ "$MENU" = 1 ]; then
	APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
	mkdir -p "$APPS"
	cat > "$APPS/sim-theme-park.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Sim Theme Park
Comment=The original 1999 game, through Proton
Exec="$DEST/play.sh"
Path=$DEST
Icon=$DEST/TP.ico
Categories=Game;
Terminal=false
EOF
	info "Added \"Sim Theme Park\" to your app menu."
fi

say "All done."
info "To play: open your app menu and click Sim Theme Park (or run $DEST/play.sh)."
info "If it stops on the \"Welcome to Sim Theme Park\" picture, click once."
days=$(( $(cut -d. -f1 /proc/uptime) / 86400 ))
if [ "$days" -ge 5 ]; then
	info ""
	info "Your computer has been on for $days days. The game's clock goes wrong after about six,"
	info "so restart your computer before you play."
fi
