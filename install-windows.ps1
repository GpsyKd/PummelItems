# Installs MelonLoader and PummelItems into Pummel Party on Windows. In PowerShell:
#
#   [Net.ServicePointManager]::SecurityProtocol = 'Tls12'; irm https://raw.githubusercontent.com/GpsyKd/PummelItems/main/install-windows.ps1 | iex
#
# It finds the game through Steam's own list of library folders, downloads MelonLoader
# (only if it is not there yet) and the latest PummelItems release, and unpacks both into the
# game folder. Run it again to update. It touches nothing outside the game folder; the one
# thing it cannot do for you - Steam's launch options - it checks and explains.
#
# Overrides, mostly for testing: $env:PUMMEL_DIR (game folder), $env:PUMMELITEMS_ZIP (a local
# release archive instead of downloading one), $env:PUMMELITEMS_NOCLIP (leave the clipboard alone).

& {
    $ErrorActionPreference = "Stop"
    $ProgressPreference = "SilentlyContinue"      # the progress bar makes downloads crawl in PowerShell 5
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    $MelonUrl = "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip"
    $Repo = "GpsyKd/PummelItems"

    function Say([string]$m)  { Write-Host ""; Write-Host "==> $m" -ForegroundColor Cyan }
    function Note([string]$m) { Write-Host "    $m" }
    function Fail([string]$m) { Write-Host ""; Write-Host "  x $m" -ForegroundColor Red; throw "PummelItems installer stopped." }

    # ------------------------------------------------------------ find the game

    function Find-Game {
        if ($env:PUMMEL_DIR) { return $env:PUMMEL_DIR }
        $roots = @()
        try { $roots += (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction Stop).SteamPath } catch { }
        $roots += "${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam"
        $libraries = @()
        foreach ($r in $roots | Where-Object { $_ } | Select-Object -Unique) {
            $r = $r -replace '/', '\'
            $libraries += $r
            $vdf = Join-Path $r "steamapps\libraryfolders.vdf"
            if (Test-Path $vdf) {
                foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
                    $libraries += ($m.Groups[1].Value -replace '\\\\', '\')
                }
            }
        }
        foreach ($l in $libraries | Select-Object -Unique) {
            $g = Join-Path $l "steamapps\common\Pummel Party"
            if (Test-Path (Join-Path $g "PummelParty.exe")) { return $g }
        }
        return $null
    }

    Say "Looking for Pummel Party"
    $Game = Find-Game
    if (-not $Game -or -not (Test-Path (Join-Path $Game "PummelParty.exe"))) {
        Fail "Not found. Install the game from Steam first, or set the folder by hand:`n      `$env:PUMMEL_DIR = 'D:\SteamLibrary\steamapps\common\Pummel Party'   and run this again."
    }
    Note $Game
    if (Get-Process PummelParty -ErrorAction SilentlyContinue) { Fail "Pummel Party is running. Close it and run this again." }

    $tmp = Join-Path ([IO.Path]::GetTempPath()) ("pummelitems-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $tmp | Out-Null

    try {
        function Fetch([string]$url, [string]$file, [string]$what) {
            Say "Downloading $what"
            try { Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing } catch { Fail "Download failed: $url`n      $($_.Exception.Message)" }
            # An error page is also a download; a zip starts with PK.
            $head = [IO.File]::ReadAllBytes($file)[0..1]
            if ($head[0] -ne 0x50 -or $head[1] -ne 0x4B) { Fail "That is not a zip file - the download went wrong." }
            Note ("{0:N0} bytes" -f (Get-Item $file).Length)
        }

        # -------------------------------------------------------- MelonLoader

        if (Test-Path (Join-Path $Game "version.dll")) {
            Say "MelonLoader is already installed - leaving it alone"
        } else {
            Fetch $MelonUrl (Join-Path $tmp "melon.zip") "MelonLoader 0.7.3"
            Say "Installing MelonLoader"
            Expand-Archive -Path (Join-Path $tmp "melon.zip") -DestinationPath $Game -Force
        }

        # -------------------------------------------------------- PummelItems

        if ($env:PUMMELITEMS_ZIP) {
            $zip = $env:PUMMELITEMS_ZIP
            Say "Using the local archive $zip"
        } else {
            Say "Finding the latest PummelItems release"
            try { $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -UseBasicParsing }
            catch { Fail "Could not ask GitHub for the latest release: $($_.Exception.Message)" }
            $asset = $release.assets | Where-Object { $_.name -like "*.zip" } | Select-Object -First 1
            if (-not $asset) { Fail "The latest release ($($release.tag_name)) has no .zip attached." }
            Note "$($release.tag_name): $($asset.name)"
            $zip = Join-Path $tmp "mod.zip"
            Fetch $asset.browser_download_url $zip "PummelItems"
        }
        Say "Installing PummelItems"
        Expand-Archive -Path $zip -DestinationPath $Game -Force

        # The launch menu replaced these; they would only confuse.
        foreach ($old in "Play Modded.bat", "Play Vanilla.bat", "switch-mod.ps1", "switch-mod.sh", "find-game.ps1") {
            $p = Join-Path $Game $old
            if (Test-Path $p) { Remove-Item -LiteralPath $p -Force; Note "removed the old $old" }
        }
    } finally {
        Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
    }

    # ------------------------------------------------------------ check

    Say "Checking"
    $ok = $true
    foreach ($f in "version.dll", "Mods\PummelCustomItems.dll", "UserData\PummelCustomItems\pciassets", "PummelItemsLauncher.exe") {
        if (Test-Path (Join-Path $Game $f)) { Note "ok       $f" } else { Note "MISSING  $f"; $ok = $false }
    }
    if (-not $ok) { Fail "Something did not land. Nothing is broken - just run this again." }

    # ------------------------------------------------------------ Steam's launch options

    # Read from Steam's own settings. Writing them is not safe while Steam runs - it rewrites
    # the file on exit - so this only checks, and says what to paste.
    $line = "`"$(Join-Path $Game 'PummelItemsLauncher.exe')`" %command%"
    $current = $null
    $steamRoot = $null
    try { $steamRoot = ((Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction Stop).SteamPath) -replace '/', '\' } catch { }
    if ($steamRoot) {
        foreach ($cfg in Get-ChildItem (Join-Path $steamRoot "userdata\*\config\localconfig.vdf") -ErrorAction SilentlyContinue) {
            $text = Get-Content $cfg.FullName -Raw -Encoding UTF8
            foreach ($m in [regex]::Matches($text, '"880940"\s*\{')) {
                $depth = 1; $i = $m.Index + $m.Length
                while ($i -lt $text.Length -and $depth -gt 0) {
                    if ($text[$i] -eq '{') { $depth++ } elseif ($text[$i] -eq '}') { $depth-- }
                    $i++
                }
                $lo = [regex]::Match($text.Substring($m.Index, $i - $m.Index), '"LaunchOptions"\s+"((?:[^"\\]|\\.)*)"')
                if ($lo.Success) { $current = $lo.Groups[1].Value -replace '\\"', '"' -replace '\\\\', '\'; break }
            }
            if ($current -ne $null) { break }
        }
    }

    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
    if ($current -and $current -like "*PummelItemsLauncher.exe*") {
        Write-Host ""
        Write-Host "Steam's launch options for Pummel Party are already right."
    } else {
        if (-not $env:PUMMELITEMS_NOCLIP) { try { Set-Clipboard -Value $line } catch { } }
        Write-Host ""
        Write-Host "ONE THING LEFT TO DO IN STEAM. Pummel Party -> right click -> Properties -> Launch Options:" -ForegroundColor Yellow
        if ($current) {
            Write-Host "  now:      $current"
            Write-Host "  replace it with (already on the clipboard - just paste):"
        } else {
            Write-Host "  paste this (already on the clipboard):"
        }
        Write-Host ""
        Write-Host "    $line"
    }
    Write-Host ""
    Write-Host "Every time Pummel Party starts, it then asks: play with the mod, without it (the stock"
    Write-Host "game, for playing online), or cancel - with the mouse, the keyboard or a controller."
    if (-not (Test-Path (Join-Path $Game "UserData\Loader.cfg"))) {
        Write-Host ""
        Write-Host "First install: start the game once with the mod and quit, so MelonLoader sets itself up."
    }
}
