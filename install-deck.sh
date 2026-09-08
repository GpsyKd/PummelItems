#!/usr/bin/env bash
# Installs MelonLoader and PummelItems onto a Steam Deck, in Desktop Mode.
#
#   curl -sL https://raw.githubusercontent.com/GpsyKd/PummelItems/main/install-deck.sh | bash
#
# The Deck downloads everything itself, so nothing has to be copied over from another
# machine. Run it from Konsole; it touches only the game's own folder.
#
# The game is a Windows build running under Proton, so the WINDOWS MelonLoader is the correct
# one - the Linux build is for native Linux games and would do nothing here.

set -euo pipefail

MELON_URL="https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip"
MOD_URL="https://github.com/GpsyKd/PummelItems/releases/latest/download/PummelItems-v0.46.zip"

say()  { printf '\n\033[1;36m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m  ! %s\033[0m\n' "$*"; }
die()  { printf '\033[1;31m  x %s\033[0m\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------- find the game

find_game() {
    if [ -n "${PUMMEL_DIR:-}" ]; then printf '%s' "$PUMMEL_DIR"; return; fi

    local candidates=(
        "$HOME/.local/share/Steam/steamapps/common/Pummel Party"
        "$HOME/.steam/steam/steamapps/common/Pummel Party"
        "/run/media/mmcblk0p1/steamapps/common/Pummel Party"
    )
    # Any SD card or external drive currently mounted.
    while IFS= read -r d; do candidates+=("$d"); done \
        < <(find /run/media -maxdepth 4 -type d -name "Pummel Party" 2>/dev/null || true)

    for c in "${candidates[@]}"; do
        [ -f "$c/PummelParty.exe" ] && { printf '%s' "$c"; return; }
    done
    return 1
}

say "Looking for Pummel Party"
GAME="$(find_game)" || die "Not found. Install the game first, or run:
    PUMMEL_DIR=\"/path/to/Pummel Party\" bash install-deck.sh"
echo "    $GAME"

[ -w "$GAME" ] || die "No write access to the game folder."

# ---------------------------------------------------------------- fetch and unpack

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

unpack() {   # unpack <zip> <destination>
    if command -v unzip >/dev/null 2>&1; then
        unzip -oq "$1" -d "$2"
    else
        # SteamOS always ships python3, unzip is not guaranteed.
        python3 -c "import sys,zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$1" "$2"
    fi
}

fetch() {    # fetch <url> <file> <description>
    say "Downloading $3"
    curl -fL --progress-bar -o "$2" "$1" || die "Download failed: $1"
    # A GitHub error page is also a successful HTTP response, so check it is really a zip.
    head -c 2 "$2" | grep -q PK || die "That is not a zip file - the download went wrong."
    printf '    %s bytes\n' "$(stat -c%s "$2")"
}

if [ -f "$GAME/version.dll" ]; then
    say "MelonLoader is already installed - leaving it alone"
else
    fetch "$MELON_URL" "$TMP/melon.zip" "MelonLoader 0.7.3 (Windows x64)"
    say "Installing MelonLoader"
    unpack "$TMP/melon.zip" "$GAME"
fi

fetch "$MOD_URL" "$TMP/mod.zip" "PummelItems"
say "Installing PummelItems"
unpack "$TMP/mod.zip" "$GAME"

chmod +x "$GAME/switch-mod.sh" 2>/dev/null || true

# ---------------------------------------------------------------- check and report

say "Checking"
ok=1
for f in "version.dll" "Mods/PummelCustomItems.dll" "UserData/PummelCustomItems/pciassets"; do
    if [ -e "$GAME/$f" ]; then
        printf '    ok      %s\n' "$f"
    else
        printf '    MISSING %s\n' "$f"; ok=0
    fi
done
[ "$ok" = 1 ] || die "Something did not land. Nothing has been broken - just run this again."

cat <<EOF

$(printf '\033[1;32mInstalled.\033[0m') Two things left, and both are done in Steam by hand:

1. Set the launch options. Steam -> Pummel Party -> gear icon -> Properties ->
   Launch Options, and paste exactly this:

       WINEDLLOVERRIDES="version=n,b" %command%

   Without it Proton ignores version.dll and MelonLoader never loads. Leave it there
   permanently - it only allows loading; the switch below decides whether it happens.

2. Start the game once and quit. MelonLoader writes UserData/Loader.cfg on that first
   run, and the switch needs that file to exist.

After that, switching between the modded and the stock game:

    "$GAME/switch-mod.sh"            # which mode am I in?
    "$GAME/switch-mod.sh" vanilla    # stock game, for playing online
    "$GAME/switch-mod.sh" modded     # our items, for local play

To reach it from Game Mode without a keyboard, add switch-mod.sh to Steam twice as a
non-Steam game, with "modded --launch" and "vanilla --launch" as its arguments.

EOF
