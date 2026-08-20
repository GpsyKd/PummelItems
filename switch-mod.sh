#!/usr/bin/env bash
# Switches Pummel Party between the modded and the stock game. Steam Deck / Linux twin of
# switch-mod.ps1 - same mechanism, same wording, so the two machines behave alike.
#
#   ./switch-mod.sh              show which mode is active
#   ./switch-mod.sh modded       our items on   (local play)
#   ./switch-mod.sh vanilla      stock game     (online play)
#   ./switch-mod.sh toggle       flip whatever it is now
#
# Add --launch to start the game straight after switching. Adding this script to Steam as a
# non-Steam shortcut - once per mode, with the mode as its argument - puts both switches in
# Game Mode where they can be reached without a keyboard.
#
# Leave WINEDLLOVERRIDES="version=n,b" %command% in the game's launch options permanently:
# that only lets MelonLoader load, and this script decides whether it does anything.

set -euo pipefail

APPID=880940
MODE="${1:-status}"
LAUNCH=0
for arg in "$@"; do
    [ "$arg" = "--launch" ] && LAUNCH=1
done

find_game() {
    if [ -n "${PUMMEL_DIR:-}" ]; then echo "$PUMMEL_DIR"; return; fi

    local candidates=(
        "$HOME/.local/share/Steam/steamapps/common/Pummel Party"
        "$HOME/.steam/steam/steamapps/common/Pummel Party"
        "/run/media/mmcblk0p1/steamapps/common/Pummel Party"
    )
    # Any other drive or SD card the user has mounted.
    while IFS= read -r d; do candidates+=("$d"); done \
        < <(find /run/media -maxdepth 3 -type d -name "Pummel Party" 2>/dev/null || true)

    for c in "${candidates[@]}"; do
        [ -f "$c/UserData/Loader.cfg" ] && { echo "$c"; return; }
    done
    return 1
}

GAME="$(find_game)" || {
    echo "Could not find the game. Set it explicitly, for example:" >&2
    echo "  PUMMEL_DIR=\"/path/to/Pummel Party\" $0 $MODE" >&2
    exit 1
}

CFG="$GAME/UserData/Loader.cfg"

# Anchored: the file also has disable_start_screen, disable_subfolder_load and friends.
current_line="$(grep -E '^[[:space:]]*disable[[:space:]]*=' "$CFG" || true)"
[ -n "$current_line" ] || { echo "No 'disable = true/false' line in $CFG" >&2; exit 1; }

if echo "$current_line" | grep -q true; then CURRENT=vanilla; else CURRENT=modded; fi

case "$MODE" in
    status)  TARGET="$CURRENT"; echo "Current mode: $CURRENT" ;;
    toggle)  if [ "$CURRENT" = vanilla ]; then TARGET=modded; else TARGET=vanilla; fi ;;
    modded)  TARGET=modded ;;
    vanilla) TARGET=vanilla ;;
    *) echo "Usage: $0 [status|modded|vanilla|toggle] [--launch]" >&2; exit 1 ;;
esac

if [ "$TARGET" != "$CURRENT" ]; then
    if pgrep -f PummelParty.exe >/dev/null 2>&1; then
        echo "Warning: the game is running. The switch takes effect next launch." >&2
    fi

    if [ "$TARGET" = vanilla ]; then NEW="disable = true"; else NEW="disable = false"; fi
    sed -i -E "0,/^[[:space:]]*disable[[:space:]]*=.*/s||$NEW|" "$CFG"

    echo "Switched: $CURRENT -> $TARGET"
elif [ "$MODE" != status ]; then
    echo "Already in $TARGET mode."
fi

if [ "$TARGET" = modded ]; then
    echo "  Our items are on. Fine for local play; do not join strangers with this."
else
    echo "  Stock game. Safe for online - MelonLoader will not load at all."
fi

if [ "$LAUNCH" = 1 ]; then
    echo "Launching Pummel Party..."
    steam "steam://rungameid/$APPID" >/dev/null 2>&1 &
fi
