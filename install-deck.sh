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
MOD_REPO="GpsyKd/PummelItems"

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

# Ask which asset the newest release actually carries, rather than guessing its filename.
# Pinning the name meant every version bump silently broke this script.
say "Finding the latest PummelItems release"
MOD_URL="$(curl -sfL "https://api.github.com/repos/$MOD_REPO/releases/latest"            | grep -o '"browser_download_url"[^,]*\.zip"' | head -1 | cut -d'"' -f4 || true)"
[ -n "$MOD_URL" ] || die "No release asset found. Is a release published with a .zip attached?"
echo "    $(basename "$MOD_URL")"

fetch "$MOD_URL" "$TMP/mod.zip" "PummelItems"
say "Installing PummelItems"
unpack "$TMP/mod.zip" "$GAME"

chmod +x "$GAME/switch-mod.sh" 2>/dev/null || true

# ---------------------------------------------------------------- the launch choice

# One game in the library, with the choice made as it starts: a small dialog with two
# buttons, "with the mod" and "without", and the last choice preselected - left alone for a
# few seconds, it starts the game the way it did last time.
#
# "Without" is the stock game, not a dormant mod. The wrapper simply does not set
# WINEDLLOVERRIDES, so Proton uses its own built-in version.dll and never loads MelonLoader
# at all. That is why this sits in the launch options rather than in the game: anything
# inside the game would itself be the mod running.
say "Installing the launch choice"
HOME_DIR="$HOME/.local/share/pummelitems"
mkdir -p "$HOME_DIR"
printf '%s\n' "$GAME" > "$HOME_DIR/game_dir"
[ -f "$HOME_DIR/mode" ] || printf 'modded\n' > "$HOME_DIR/mode"

cat > "$HOME_DIR/launch.sh" <<'LAUNCH'
#!/usr/bin/env bash
# Steam launch wrapper for Pummel Party: asks whether to start with PummelItems or without.
# Steam runs it as   launch.sh <the game's own command...>   and it ends by running that
# command, with or without the setting that lets MelonLoader load.

DIR="$HOME/.local/share/pummelitems"
LAST="$(cat "$DIR/mode" 2>/dev/null || echo modded)"
[ "$LAST" = vanilla ] || LAST=modded
WAIT=5

if [ "$LAST" = modded ]; then LAST_TEXT="с модом"; else LAST_TEXT="без мода"; fi
TEXT="Как запустить Pummel Party?

Если ничего не нажимать, через $WAIT с запустится как в прошлый раз: $LAST_TEXT."

choice=""
# zenity: OK = with the mod, Cancel = without, exit 5 = timed out.
ZENITY="$(command -v zenity || ls "$HOME"/.local/share/Steam/ubuntu12_32/steam-runtime/usr/bin/zenity "$HOME"/.local/share/Steam/ubuntu12_32/steam-runtime/*/usr/bin/zenity 2>/dev/null | head -1)"
if [ -n "$ZENITY" ] && [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    "$ZENITY" --question --title="Pummel Party" --text="$TEXT" \
              --ok-label="С модом" --cancel-label="Без мода" --timeout="$WAIT" --width=420 2>/dev/null
    case $? in
        0) choice=modded ;;
        1) choice=vanilla ;;
    esac
elif command -v kdialog >/dev/null 2>&1 && [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    # kdialog has no timeout of its own, so one is supplied.
    kdialog --title "Pummel Party" --yes-label "С модом" --no-label "Без мода" --yesno "$TEXT" 2>/dev/null &
    pid=$!
    ( sleep "$WAIT"; kill "$pid" 2>/dev/null ) &
    timer=$!
    wait "$pid"; code=$?
    kill "$timer" 2>/dev/null
    case $code in
        0) choice=modded ;;
        1) choice=vanilla ;;
    esac
fi

# No dialog, no answer, or no screen to show one on: the last choice. The game always starts.
[ -n "$choice" ] || choice="$LAST"
printf '%s\n' "$choice" > "$DIR/mode"

if [ "$choice" = modded ]; then
    export WINEDLLOVERRIDES="version=n,b${WINEDLLOVERRIDES:+;$WINEDLLOVERRIDES}"
    # An old manual switch may have left MelonLoader turned off; with the mod chosen, on it goes.
    GAME="$(cat "$DIR/game_dir" 2>/dev/null)"
    CFG="$GAME/UserData/Loader.cfg"
    [ -f "$CFG" ] && sed -i -E '0,/^[[:space:]]*disable[[:space:]]*=.*/s//disable = false/' "$CFG"
elif [ -n "${WINEDLLOVERRIDES:-}" ]; then
    # The old setup put WINEDLLOVERRIDES="version=n,b" in the launch options themselves. If it
    # is still there, take that one entry out, or "without" would load MelonLoader regardless.
    WINEDLLOVERRIDES="$(printf '%s' "$WINEDLLOVERRIDES" | tr ';' '\n' | grep -v '^version=' | paste -sd ';' -)"
    if [ -n "$WINEDLLOVERRIDES" ]; then export WINEDLLOVERRIDES; else unset WINEDLLOVERRIDES; fi
fi

exec "$@"
LAUNCH
chmod +x "$HOME_DIR/launch.sh"
echo "    $HOME_DIR/launch.sh"

# The two library entries an earlier version made, in case they are still there.
rm -f "$HOME/.local/share/applications/pummelitems-modded.desktop" \
      "$HOME/.local/share/applications/pummelitems-vanilla.desktop" \
      "$HOME_DIR/pummel-modded.sh" "$HOME_DIR/pummel-vanilla.sh"

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
[ -x "$HOME_DIR/launch.sh" ] && printf '    ok      launch.sh\n' || ok=0
[ "$ok" = 1 ] || die "Something did not land. Nothing has been broken - just run this again."

printf '\n\033[1;32mDone.\033[0m\n'

cat <<EOF

One thing to do in Steam by hand, once - and again if you set it up before this version:

    Pummel Party -> gear icon -> Properties -> Launch Options, replace whatever is there with

        $HOME_DIR/launch.sh %command%

(Select the line above here in Konsole, copy it, and paste it into Steam.)

From then on, every time Pummel Party starts, it asks:

    [ С модом ]   our items, for local play
    [ Без мода ]  the stock game, for playing online - MelonLoader does not load at all

Tap one, or leave it and in 5 seconds it starts the way it did last time.
EOF

# Loader.cfg only exists once the game has run with MelonLoader, so its absence means this is
# a first install.
if [ ! -f "$GAME/UserData/Loader.cfg" ]; then
    cat <<EOF

First install: start the game once "С модом" and quit, so MelonLoader writes its settings.
EOF
fi
echo
