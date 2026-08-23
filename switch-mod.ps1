# Switches Pummel Party between the modded and the stock game.
#
#   powershell -File switch-mod.ps1              # show which mode is active
#   powershell -File switch-mod.ps1 modded       # our items on   (local play)
#   powershell -File switch-mod.ps1 vanilla      # stock game     (online play)
#   powershell -File switch-mod.ps1 toggle       # flip whatever it is now
#
# Add -Launch to start the game straight after switching.
#
# This flips MelonLoader's own "disable" flag in UserData\Loader.cfg rather than moving
# files about. Nothing gets renamed, deleted or reinstalled, so there is no state to get
# out of step and no way to lose the mod by switching at a bad moment - and the stock game
# really is stock, because MelonLoader bails out before it loads anything of ours.

param(
    [ValidateSet("status", "modded", "vanilla", "toggle")]
    [string]$Mode = "status",

    [string]$Game,
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$AppId = 880940

# Where the game is depends on which drive Steam put it on, so it is looked for rather than
# assumed. Dropping this script into the game folder makes the first check succeed and the
# rest moot.
function Find-Game {
    if ($Game) { return $Game }

    $candidates = @($PSScriptRoot)

    # Every Steam library on this machine, read from Steam's own index.
    $vdf = Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) {
        Select-String -Path $vdf -Pattern '"path"\s+"(.+?)"' -AllMatches |
            ForEach-Object { $_.Matches } |
            ForEach-Object {
                $candidates += Join-Path ($_.Groups[1].Value -replace '\\', '') "steamapps\common\Pummel Party"
            }
    }

    foreach ($d in "$env:ProgramFiles(x86)", "C:", "D:", "E:", "F:") {
        $candidates += "$d\SteamLibrary\steamapps\common\Pummel Party"
    }

    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c "UserData\Loader.cfg"))) { return $c }
    }
    return $null
}

$Game = Find-Game
if (-not $Game) {
    throw "Could not find Pummel Party with MelonLoader installed. Pass it explicitly:`n" +
          "  switch-mod.ps1 $Mode -Game 'D:\SteamLibrary\steamapps\common\Pummel Party'"
}

$cfg = Join-Path $Game "UserData\Loader.cfg"

$lines = Get-Content $cfg

# Anchored: the file also has disable_start_screen, disable_subfolder_load and friends.
$idx = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^\s*disable\s*=\s*(true|false)\s*$') { $idx = $i; break }
}
if ($idx -lt 0) { throw "No 'disable = true/false' line in $cfg" }

$currentlyDisabled = $lines[$idx] -match 'true'
$currentMode = if ($currentlyDisabled) { "vanilla" } else { "modded" }

if ($Mode -eq "status") {
    Write-Host "Current mode: $currentMode" -ForegroundColor Cyan
    if (-not $Launch) { exit 0 }
    $target = $currentMode
}
elseif ($Mode -eq "toggle") {
    $target = if ($currentlyDisabled) { "modded" } else { "vanilla" }
}
else {
    $target = $Mode
}

if ($target -ne $currentMode) {
    if (Get-Process -Name "PummelParty" -ErrorAction SilentlyContinue) {
        Write-Warning "The game is running. The switch will only take effect next launch."
    }

    $lines[$idx] = if ($target -eq "vanilla") { "disable = true" } else { "disable = false" }
    Set-Content -Path $cfg -Value $lines -Encoding utf8

    $colour = if ($target -eq "modded") { "Green" } else { "Yellow" }
    Write-Host "Switched: $currentMode -> $target" -ForegroundColor $colour
}
else {
    Write-Host "Already in $target mode." -ForegroundColor Cyan
}

if ($target -eq "modded") {
    Write-Host "  Our items are on. Fine for local play; do not join strangers with this." -ForegroundColor DarkGray
} else {
    Write-Host "  Stock game. Safe for online - MelonLoader will not load at all." -ForegroundColor DarkGray
}

if ($Launch) {
    Write-Host "Launching Pummel Party..."
    Start-Process "steam://rungameid/$AppId"
}
