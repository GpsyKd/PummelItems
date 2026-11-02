#!/usr/bin/env bash
# Builds PummelItems from source on a Steam Deck and puts it into the game - for trying out a
# branch on the Deck before there is a release of it. Run it from Konsole, in Desktop Mode:
#
#   curl -sL https://raw.githubusercontent.com/GpsyKd/PummelItems/main/build-deck.sh | bash -s -- <branch>
#
# With no branch it builds main. Run from a copy of the repository instead, it builds that copy.
#
# Releases are still built on Windows by mod/build.ps1. This compiles the same sources against
# the same references - the game's own assemblies and MelonLoader's - so it makes the same DLL.
# Only Mods/PummelCustomItems.dll is replaced: MelonLoader and the asset bundle have to be in
# place already, which is what install-deck.sh does. Running that again also puts the released
# DLL back.
#
# The compiler comes from the .NET SDK, installed once into ~/.local/share/pummelitems/dotnet -
# about 200 MB to download. Nothing outside that folder and the game's Mods folder is touched.

set -euo pipefail

MOD_REPO="GpsyKd/PummelItems"
HOME_DIR="$HOME/.local/share/pummelitems"
DOTNET_DIR="$HOME_DIR/dotnet"

say()  { printf '\n\033[1;36m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m  ! %s\033[0m\n' "$*"; }
die()  { printf '\033[1;31m  x %s\033[0m\n' "$*" >&2; exit 1; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# ---------------------------------------------------------------- find the game

find_game() {
    if [ -n "${PUMMEL_DIR:-}" ]; then printf '%s' "$PUMMEL_DIR"; return; fi

    # Where install-deck.sh found it.
    local saved
    saved="$(cat "$HOME_DIR/game_dir" 2>/dev/null || true)"
    if [ -n "$saved" ] && [ -f "$saved/PummelParty.exe" ]; then printf '%s' "$saved"; return; fi

    local candidates=(
        "$HOME/.local/share/Steam/steamapps/common/Pummel Party"
        "$HOME/.steam/steam/steamapps/common/Pummel Party"
    )
    # SD cards and external drives: /run/media/<card> on older SteamOS, /run/media/deck/<card>
    # on newer.
    while IFS= read -r d; do candidates+=("$d"); done \
        < <(find /run/media -maxdepth 5 -type d -name "Pummel Party" 2>/dev/null || true)

    local c
    for c in "${candidates[@]}"; do
        [ -f "$c/PummelParty.exe" ] && { printf '%s' "$c"; return; }
    done
    return 1
}

say "Looking for Pummel Party"
GAME="$(find_game)" || die "Not found. Install the mod with install-deck.sh first, or run:
    PUMMEL_DIR=\"/path/to/Pummel Party\" bash build-deck.sh"
echo "    $GAME"

MANAGED="$GAME/PummelParty_Data/Managed"
MELON="$GAME/MelonLoader/net472"

[ -f "$GAME/version.dll" ] && [ -d "$MELON" ] || die "MelonLoader is not installed. Run install-deck.sh first."
[ -f "$GAME/UserData/PummelCustomItems/pciassets" ] \
    || warn "The asset bundle is missing, so the items will be placeholder shapes. install-deck.sh brings it."
[ -w "$GAME" ] || die "No write access to the game folder."

# Wine keeps the DLL open while the game runs, and overwriting it under a running game is how
# it crashes. Under Proton the game's process is named after its exe; matching the name, not
# whole command lines, keeps this from tripping over anything that merely mentions it.
if pgrep -x 'PummelParty.exe' >/dev/null 2>&1; then
    die "Pummel Party is running - quit it first."
fi

# The references mod/build.ps1 uses. Windows does not care about the case of a file name and
# Linux does, so each one is found whatever case it has on disk.
REFS=()
add_ref() {   # add_ref <folder> <file>
    local hit
    hit="$(find "$1" -maxdepth 1 -iname "$2" -print -quit 2>/dev/null || true)"
    [ -n "$hit" ] || die "Missing reference: $1/$2"
    REFS+=("$hit")
}
for f in MelonLoader.dll 0Harmony.dll; do add_ref "$MELON" "$f"; done
for f in Assembly-CSharp.dll UnityEngine.dll UnityEngine.CoreModule.dll \
         UnityEngine.InputLegacyModule.dll UnityEngine.AssetBundleModule.dll Rewired_Core.dll \
         UnityEngine.ImageConversionModule.dll UnityEngine.PhysicsModule.dll \
         UnityEngine.AnimationModule.dll UnityEngine.AudioModule.dll UnityEngine.UI.dll \
         Unity.TextMeshPro.dll Unity.Addressables.dll Unity.ResourceManager.dll \
         mscorlib.dll System.dll System.Core.dll netstandard.dll; do
    add_ref "$MANAGED" "$f"
done

# ---------------------------------------------------------------- the compiler

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# Compiling needs no culture data, and SteamOS does not promise the ICU library .NET wants.
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

find_csc() { ls -d "$DOTNET_DIR"/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | sort -V | tail -n 1; }

CSC="$(find_csc || true)"
if [ -z "$CSC" ] || [ ! -x "$DOTNET_DIR/dotnet" ]; then
    say "Installing the C# compiler: .NET SDK 8, about 200 MB, only this once"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$TMP/dotnet-install.sh" \
        || die "Could not download the .NET installer."
    # The rest of this script may be arriving on stdin (curl | bash), so nothing it starts is
    # allowed to read from there.
    bash "$TMP/dotnet-install.sh" --channel 8.0 --install-dir "$DOTNET_DIR" --no-path < /dev/null \
        || die "The .NET SDK did not install."
    CSC="$(find_csc || true)"
    [ -n "$CSC" ] || die "The .NET SDK installed, but its C# compiler is not where it should be."
fi
echo "    $CSC"

# ---------------------------------------------------------------- the source

HERE="$(cd "$(dirname "${BASH_SOURCE[0]:-.}")" 2>/dev/null && pwd || pwd)"
if [ $# -eq 0 ] && [ -d "$HERE/mod/src" ]; then
    SRC="$HERE"
    say "Building the copy in $SRC"
else
    BRANCH="${1:-main}"
    say "Downloading branch $BRANCH"
    mkdir -p "$TMP/src"
    curl -fL --progress-bar "https://github.com/$MOD_REPO/archive/refs/heads/$BRANCH.tar.gz" < /dev/null \
        | tar xz -C "$TMP/src" --strip-components=1 \
        || die "Could not download branch '$BRANCH'."
    SRC="$TMP/src"
fi
[ -f "$SRC/mod/src/Main.cs" ] || die "No mod sources in $SRC."
VERSION="$(grep -o 'Version = "[^"]*"' "$SRC/mod/src/Main.cs" | head -n 1 | cut -d'"' -f2 || true)"

# ---------------------------------------------------------------- compile

SOURCES=()
while IFS= read -r -d '' f; do SOURCES+=("$f"); done \
    < <(find "$SRC/mod/src" -name '*.cs' -print0 | sort -z)

say "Compiling ${#SOURCES[@]} source file(s) - PummelItems ${VERSION:-of unknown version}"
mkdir -p "$TMP/out"
OUT="$TMP/out/PummelCustomItems.dll"

# As build.ps1 has it, plus -noconfig: the compiler's default response file lists .NET
# Framework assemblies that Linux does not have, and the build needs none of them.
ARGS=(-nologo -noconfig -target:library -platform:x64 -langversion:latest -nostdlib+ -optimize+
      -debug:portable "-out:$OUT")
for r in "${REFS[@]}"; do ARGS+=("-r:$r"); done

"$DOTNET_DIR/dotnet" "$CSC" "${ARGS[@]}" "${SOURCES[@]}" < /dev/null \
    || die "Compilation failed - the errors are above. Nothing in the game was changed."
[ -f "$OUT" ] || die "The compiler reported success but wrote no DLL."

# ---------------------------------------------------------------- install

MODS="$GAME/Mods"
mkdir -p "$MODS"
cp -f "$OUT" "$MODS/PummelCustomItems.dll"
if [ -f "$TMP/out/PummelCustomItems.pdb" ]; then
    cp -f "$TMP/out/PummelCustomItems.pdb" "$MODS/PummelCustomItems.pdb"
fi
printf '    %s bytes -> %s\n' "$(stat -c%s "$MODS/PummelCustomItems.dll")" "$MODS/PummelCustomItems.dll"

cat <<EOF

$(printf '\033[1;32mDone.\033[0m')

Start Pummel Party and choose "Играть с модом". The gold line in the corner of the main menu
should read:

    PummelItems ${VERSION:-?}

To go back to the released version, run install-deck.sh again. The compiler stays in
$DOTNET_DIR for next time; delete that folder to get the space back.
EOF
