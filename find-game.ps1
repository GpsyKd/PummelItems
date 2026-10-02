# Finds the Pummel Party install. Dot-source this file, then call Find-PummelParty.
#
#   . (Join-Path $PSScriptRoot "find-game.ps1")
#   $game = Find-PummelParty -Hint $Game -Also $PSScriptRoot
#
# Shared by the build scripts and the mod switch, so there is one copy of the search to get
# right. It reads Steam's own index of library folders, which covers whatever drive the game
# was installed on, instead of guessing drive letters.
#
# Latin only: Windows PowerShell 5.1 misreads a BOM-less .ps1 that contains Cyrillic.

function Find-PummelParty {
    param(
        [string]$Hint,       # an explicit path, tried first
        [string[]]$Also      # more places worth a look, e.g. the calling script's own folder
    )

    $candidates = New-Object System.Collections.Generic.List[string]
    if ($Hint) { $candidates.Add($Hint) }
    foreach ($a in $Also) { if ($a) { $candidates.Add($a) } }

    # Where Steam itself is: the registry first, then its usual homes.
    $steamRoots = New-Object System.Collections.Generic.List[string]
    try {
        $reg = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction Stop).SteamPath
        if ($reg) { $steamRoots.Add(($reg -replace '/', '\')) }
    } catch { }
    $steamRoots.Add((Join-Path ${env:ProgramFiles(x86)} "Steam"))
    $steamRoots.Add((Join-Path $env:ProgramFiles "Steam"))

    # Every library Steam knows about. The file stores paths with doubled backslashes
    # ("F:\\SteamLibrary"), so those are collapsed back to single ones.
    foreach ($root in $steamRoots) {
        $vdf = Join-Path $root "steamapps\libraryfolders.vdf"
        if (-not (Test-Path $vdf)) { continue }

        foreach ($m in (Select-String -Path $vdf -Pattern '"path"\s+"(.+?)"' -AllMatches).Matches) {
            $lib = $m.Groups[1].Value -replace '\\\\', '\'
            $candidates.Add((Join-Path $lib "steamapps\common\Pummel Party"))
        }
    }

    foreach ($c in $candidates) {
        if ($c -and (Test-Path (Join-Path $c "PummelParty.exe"))) { return (Resolve-Path $c).Path }
    }
    return $null
}
