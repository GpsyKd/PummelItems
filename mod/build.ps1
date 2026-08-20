# Builds PummelCustomItems.dll and copies it into the game's Mods folder.
# No .NET SDK required - uses the standalone Roslyn compiler in tools\roslyn.

param(
    [string]$Game = "F:\SteamLibrary\steamapps\common\Pummel Party",
    [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent          # ...\PummelPartyMod
$csc  = Join-Path $root "tools\roslyn\tasks\net472\csc.exe"
$out  = Join-Path $PSScriptRoot "bin\PummelCustomItems.dll"

if (-not (Test-Path $csc))  { throw "Roslyn compiler not found at $csc" }
if (-not (Test-Path $Game)) { throw "Game folder not found: $Game" }

$managed = Join-Path $Game "PummelParty_Data\Managed"
$ml      = Join-Path $Game "MelonLoader\net472"

$refs = @(
    "$ml\MelonLoader.dll"
    "$ml\0Harmony.dll"
    "$managed\Assembly-CSharp.dll"
    "$managed\UnityEngine.dll"
    "$managed\UnityEngine.CoreModule.dll"
    "$managed\UnityEngine.InputLegacyModule.dll"
    "$managed\UnityEngine.AssetBundleModule.dll"
    "$managed\Rewired_Core.dll"
    "$managed\UnityEngine.ImageConversionModule.dll"
    "$managed\UnityEngine.PhysicsModule.dll"
    "$managed\UnityEngine.AnimationModule.dll"
    "$managed\UnityEngine.AudioModule.dll"
    "$managed\UnityEngine.UI.dll"
    "$managed\Unity.Addressables.dll"
    "$managed\Unity.ResourceManager.dll"
    "$managed\mscorlib.dll"
    "$managed\System.dll"
    "$managed\System.Core.dll"
    "$managed\netstandard.dll"
)

foreach ($r in $refs) {
    if (-not (Test-Path $r)) { throw "Missing reference: $r" }
}

New-Item -ItemType Directory -Force -Path (Split-Path $out -Parent) | Out-Null
$sources = Get-ChildItem (Join-Path $PSScriptRoot "src") -Recurse -Filter *.cs | ForEach-Object { $_.FullName }

$cscArgs = @(
    "/nologo"
    "/target:library"
    "/platform:x64"
    "/langversion:latest"
    "/nostdlib+"
    "/optimize+"
    "/debug:portable"
    "/out:$out"
)
$cscArgs += ($refs | ForEach-Object { "/r:$_" })
$cscArgs += $sources

Write-Host "Compiling $($sources.Count) source file(s)..."
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed (exit $LASTEXITCODE)" }

$size = (Get-Item $out).Length
Write-Host "Built: $out  ($size bytes)"

if (-not $NoDeploy) {
    $mods = Join-Path $Game "Mods"
    New-Item -ItemType Directory -Force -Path $mods | Out-Null
    $running = Get-Process -Name "PummelParty" -ErrorAction SilentlyContinue
    if ($running) { throw "Game is running - close it before deploying." }
    Copy-Item $out -Destination $mods -Force
    $pdb = [IO.Path]::ChangeExtension($out, ".pdb")
    if (Test-Path $pdb) { Copy-Item $pdb -Destination $mods -Force }
    Write-Host "Deployed to $mods"
}
