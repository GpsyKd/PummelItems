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

# One game in the library, with the choice made as it starts: deck/picker.py, a menu in the
# style of Steam's own launch-option one - "with the mod", "without", "cancel" - with the last
# choice preselected. Left alone for a few seconds, it starts the game the way it did last time.
#
# "Without" is the stock game, not a dormant mod. The wrapper simply does not set
# WINEDLLOVERRIDES, so Proton uses its own built-in version.dll and never loads MelonLoader
# at all. That is why this sits in the launch options rather than in the game: anything
# inside the game would itself be the mod running.
say "Installing the launch choice"
HOME_DIR="$HOME/.local/share/pummelitems"
mkdir -p "$HOME_DIR"
printf '%s\n' "$GAME" > "$HOME_DIR/game_dir"

# The picker is plain Python on libX11, both always on SteamOS; zenity is not.
RAW="https://raw.githubusercontent.com/$MOD_REPO/main/deck"
for f in picker.py picker-art.txt; do
    if curl -sfL "$RAW/$f" -o "$HOME_DIR/$f.new" && [ -s "$HOME_DIR/$f.new" ]; then
        mv -f "$HOME_DIR/$f.new" "$HOME_DIR/$f"
    else
        rm -f "$HOME_DIR/$f.new"
        warn "Could not download $f - without it the game starts without asking."
    fi
done
[ -f "$HOME_DIR/mode" ] || printf 'modded\n' > "$HOME_DIR/mode"

cat > "$HOME_DIR/launch.sh" <<'LAUNCH'
#!/usr/bin/env bash
# Steam launch wrapper for Pummel Party: asks whether to start with PummelItems or without.
# Steam runs it as   launch.sh <the game's own command...>   and it ends by running that
# command, with or without the setting that lets MelonLoader load.

DIR="$HOME/.local/share/pummelitems"
LOG="$DIR/launch.log"
LAST="$(cat "$DIR/mode" 2>/dev/null || echo modded)"
[ "$LAST" = vanilla ] || LAST=modded
WAIT=12
APPID="${SteamGameId:-${SteamAppId:-880940}}"

log() { printf '%s %s\n' "$(date '+%F %T')" "$*" >> "$LOG" 2>/dev/null; }
# Kept short: the last couple of hundred lines are plenty to see what a launch did.
if [ -f "$LOG" ] && [ "$(wc -l < "$LOG")" -gt 400 ]; then
    tail -n 200 "$LOG" > "$LOG.tmp" && mv -f "$LOG.tmp" "$LOG"
fi

# Game Mode is a gamescope session; Desktop Mode is Plasma.
GAMEMODE=0
if [ "${XDG_CURRENT_DESKTOP:-}" = gamescope ] || pgrep -x gamescope >/dev/null 2>&1 \
   || pgrep -x gamescope-wl >/dev/null 2>&1; then
    GAMEMODE=1
fi
log "launch: game_mode=$GAMEMODE DISPLAY=${DISPLAY:-} WAYLAND_DISPLAY=${WAYLAND_DISPLAY:-} appid=$APPID last=$LAST"

# In Game Mode gamescope only puts a window on screen if it carries the STEAM_GAME property.
# The game's own windows get it; a dialog does not, so it used to run unseen while the timeout
# ticked away and the game then started the way it did last time. Given the game's id, the
# dialog is shown as part of the game being launched.
mark_for_gamescope() {   # mark_for_gamescope <pid of the dialog>
    command -v python3 >/dev/null 2>&1 || { log "mark: no python3"; return; }
    python3 - "$1" "$APPID" >> "$LOG" 2>&1 <<'PY'
import ctypes, ctypes.util, sys, time
pid, appid = int(sys.argv[1]), int(sys.argv[2])
x = ctypes.CDLL(ctypes.util.find_library('X11') or 'libX11.so.6')
V, UL, L, I, UI, UC, P = (ctypes.c_void_p, ctypes.c_ulong, ctypes.c_long, ctypes.c_int,
                          ctypes.c_uint, ctypes.c_ubyte, ctypes.POINTER)
x.XOpenDisplay.restype = V;        x.XOpenDisplay.argtypes = [ctypes.c_char_p]
x.XDefaultRootWindow.restype = UL; x.XDefaultRootWindow.argtypes = [V]
x.XInternAtom.restype = UL;        x.XInternAtom.argtypes = [V, ctypes.c_char_p, I]
x.XQueryTree.argtypes = [V, UL, P(UL), P(UL), P(P(UL)), P(UI)]
x.XGetWindowProperty.argtypes = [V, UL, UL, L, L, I, UL, P(UL), P(I), P(UL), P(UL), P(P(UC))]
x.XChangeProperty.argtypes = [V, UL, UL, UL, I, I, V, I]
x.XFree.argtypes = [V]; x.XFlush.argtypes = [V]; x.XCloseDisplay.argtypes = [V]
x.XSetErrorHandler.restype = V; x.XSetErrorHandler.argtypes = [V]
# A window can vanish while it is being looked at; Xlib's default handler would end the script.
HANDLER = ctypes.CFUNCTYPE(I, V, V)(lambda d, e: 0)
x.XSetErrorHandler(ctypes.cast(HANDLER, V))

d = x.XOpenDisplay(None)
if not d:
    print('mark: cannot open the X display'); sys.exit(0)
root = x.XDefaultRootWindow(d)
NET_WM_PID = x.XInternAtom(d, b'_NET_WM_PID', 0)
STEAM_GAME = x.XInternAtom(d, b'STEAM_GAME', 0)
CARDINAL = 6

def children(w):
    r, p, n, kids = UL(), UL(), UI(), P(UL)()
    if not x.XQueryTree(d, w, ctypes.byref(r), ctypes.byref(p), ctypes.byref(kids), ctypes.byref(n)):
        return []
    out = [kids[i] for i in range(n.value)]
    if kids: x.XFree(ctypes.cast(kids, V))
    return out

def owner(w):
    t, f, n, left, data = UL(), I(), UL(), UL(), P(UC)()
    if x.XGetWindowProperty(d, w, NET_WM_PID, 0, 1, 0, CARDINAL, ctypes.byref(t), ctypes.byref(f),
                            ctypes.byref(n), ctypes.byref(left), ctypes.byref(data)) != 0:
        return None
    v = ctypes.cast(data, P(L))[0] if (data and n.value == 1 and f.value == 32) else None
    if data: x.XFree(ctypes.cast(data, V))
    return v

found, end = [], time.time() + 5
while not found and time.time() < end:
    todo = [root]
    while todo:
        for c in children(todo.pop()):
            if owner(c) == pid: found.append(c)
            todo.append(c)
    if not found: time.sleep(0.1)

value = (L * 1)(appid)
for w in found:
    x.XChangeProperty(d, w, STEAM_GAME, CARDINAL, 32, 0, ctypes.cast(value, V), 1)
x.XFlush(d)
print('mark: %d window(s) of the dialog given STEAM_GAME=%d' % (len(found), appid))
x.XCloseDisplay(d)
PY
}

choice=""
code=2
PICKER="$DIR/picker.py"
if [ -f "$PICKER" ] && command -v python3 >/dev/null 2>&1 && [ -n "${DISPLAY:-}" ]; then
    # In Game Mode it covers the whole screen, as Steam's own menu does; on the desktop it is
    # a window. timeout is only a guard against a hang - the picker has its own countdown.
    FULL=""; [ "$GAMEMODE" = 1 ] && FULL="--fullscreen"
    answer="$(timeout $((WAIT + 30)) python3 "$PICKER" --last "$LAST" --wait "$WAIT" --appid "$APPID" $FULL 2>>"$LOG")"
    code=$?
    log "dialog: picker exit $code, answer '${answer}'"
    case "$answer" in
        modded|vanilla) choice="$answer" ;;
        cancel) log "cancelled - the game is not started"; exit 0 ;;
    esac
else
    log "dialog: picker not available (file: $([ -f "$PICKER" ] && echo yes || echo no), python3: $(command -v python3 || echo none), DISPLAY: ${DISPLAY:-none})"
fi

# Should the picker itself fail, kdialog where there is one - marked for gamescope too.
if [ -z "$choice" ] && [ "$code" != 0 ] && command -v kdialog >/dev/null 2>&1 && [ -n "${DISPLAY:-}" ]; then
    if [ "$LAST" = modded ]; then LAST_TEXT="с модом"; else LAST_TEXT="без мода"; fi
    TEXT="Как запустить Pummel Party?

Если ничего не нажимать, через $WAIT с запустится как в прошлый раз: $LAST_TEXT."
    SCALE=1; [ "$GAMEMODE" = 1 ] && SCALE=2
    QT_SCALE_FACTOR=$SCALE kdialog --title "Pummel Party" --yes-label "С модом" --no-label "Без мода" \
        --yesno "$TEXT" 2>>"$LOG" &
    pid=$!
    [ "$GAMEMODE" = 1 ] && mark_for_gamescope "$pid" &
    ( sleep "$WAIT"; kill "$pid" 2>/dev/null ) &
    timer=$!
    wait "$pid"; code=$?
    kill "$timer" 2>/dev/null
    log "dialog: kdialog exit $code"
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
log "start: $choice, WINEDLLOVERRIDES=${WINEDLLOVERRIDES:-<unset>}"

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
# Drawing its menu into a picture needs no screen, so this checks the picker can run at all.
if python3 "$HOME_DIR/picker.py" --preview "$TMP/picker.png" >/dev/null 2>&1; then
    printf '    ok      picker\n'
else
    printf '    PROBLEM picker - the game will start without asking\n'
fi
[ "$ok" = 1 ] || die "Something did not land. Nothing has been broken - just run this again."

printf '\n\033[1;32mDone.\033[0m\n'

# What Steam has in the launch options right now, read from its own settings file. Writing it
# from here is not safe while Steam runs - it rewrites the file when it exits - so this only
# checks, and says what to paste.
LAUNCH_LINE="$HOME_DIR/launch.sh %command%"
CURRENT="$(python3 - 2>/dev/null <<'PY'
import glob, os, re
for path in glob.glob(os.path.expanduser('~/.local/share/Steam/userdata/*/config/localconfig.vdf')):
    try:
        text = open(path, encoding='utf-8', errors='replace').read()
    except OSError:
        continue
    for m in re.finditer(r'"880940"\s*\{', text):
        depth, i = 1, m.end()
        while i < len(text) and depth:
            depth += {'{': 1, '}': -1}.get(text[i], 0)
            i += 1
        lo = re.search(r'"LaunchOptions"\s+"((?:[^"\\]|\\.)*)"', text[m.end():i])
        if lo:
            print(lo.group(1).replace('\\"', '"').replace('\\\\', '\\'))
            raise SystemExit
PY
)"

if printf '%s' "$CURRENT" | grep -q 'pummelitems/launch.sh'; then
    cat <<EOF

Steam's launch options for Pummel Party are already right:

    $CURRENT
EOF
else
    cat <<EOF

ONE THING LEFT TO DO IN STEAM. Pummel Party's launch options are now:

    ${CURRENT:-(empty)}

Pummel Party -> gear icon -> Properties -> Launch Options: delete that and paste instead

    $LAUNCH_LINE

(Select the line above here in Konsole, copy it, and paste it into Steam. Steam saves the
setting with a delay, so if you have just changed it, this check may still show the old one.)
EOF
fi

cat <<EOF

Every time Pummel Party starts, it then asks:

    Играть с модом    our items, for local play
    Играть без мода   the stock game, for playing online - MelonLoader does not load at all
    Отмена            back to the library

Choose with the controller (D-pad, A) or tap it. Left alone, it starts the way it did last
time once the countdown under the menu runs out.
If the question never shows up, the log of every launch is here:

    $HOME_DIR/launch.log
EOF

# Loader.cfg only exists once the game has run with MelonLoader, so its absence means this is
# a first install.
if [ ! -f "$GAME/UserData/Loader.cfg" ]; then
    cat <<EOF

First install: start the game once "С модом" and quit, so MelonLoader writes its settings.
EOF
fi
echo
