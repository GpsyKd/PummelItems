# Rebuilds the AssetBundle in Unity (batch mode, no GUI) and copies it into the game.
#
#   powershell -File build-assets.ps1
#
# Unity must be the SAME build the game runs: 2021.3.45f2 (changeset 88f88f591b2e).

param(
    [string]$Game   = "F:\SteamLibrary\steamapps\common\Pummel Party",
    [string]$Unity  = "C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Unity.exe",
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"
$proj = Join-Path $PSScriptRoot "unity\PPAssets"
$log  = Join-Path $PSScriptRoot "unity\build.log"

if (-not (Test-Path $Unity)) { throw "Unity not found: $Unity" }
if (-not (Test-Path $proj))  { throw "Unity project not found: $proj" }

# A batch-mode run that was killed leaves this behind and every later run then dies
# with "project already open in another instance".
$lock = Join-Path $proj "Temp\UnityLockfile"
if (Test-Path $lock) {
    $running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
               Where-Object { $_.CommandLine -like "*$proj*" }
    if ($running) {
        foreach ($p in $running) {
            Write-Host "Stopping stale Unity batch process $($p.ProcessId)..."
            Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Milliseconds 1500
    }
    Remove-Item $lock -Force -ErrorAction SilentlyContinue
}

Remove-Item $log -Force -ErrorAction SilentlyContinue
Write-Host "Building asset bundle (this takes a minute)..."

$pr = Start-Process -FilePath $Unity -PassThru -Wait -ArgumentList @(
    "-batchmode", "-quit", "-nographics"
    "-projectPath", $proj
    "-executeMethod", "BundleBuilder.All"
    "-logFile", $log
)

if ($pr.ExitCode -ne 0) {
    Write-Host "--- Unity log tail ---"
    if (Test-Path $log) { Get-Content $log -Tail 30 }
    throw "Unity exited with $($pr.ExitCode)"
}

Select-String -Path $log -Pattern "\[PCI\]" | ForEach-Object { Write-Host $_.Line }

$bundle = Join-Path $proj "BuiltBundles\pciassets"
if (-not (Test-Path $bundle)) { throw "Bundle missing after build: $bundle" }
Write-Host "Bundle: $bundle ($((Get-Item $bundle).Length) bytes)"

if (-not $NoDeploy) {
    if (Get-Process -Name "PummelParty" -ErrorAction SilentlyContinue) {
        throw "Game is running - close it before deploying."
    }
    $dest = Join-Path $Game "UserData\PummelCustomItems"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item $bundle -Destination $dest -Force
    Copy-Item "$bundle.manifest" -Destination $dest -Force -ErrorAction SilentlyContinue
    Write-Host "Deployed to $dest"
}
