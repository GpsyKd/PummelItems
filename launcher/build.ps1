# Builds PummelItemsLauncher.exe, the launch menu for Windows, with the same standalone Roslyn
# as the mod (tools\roslyn). It targets the .NET Framework 4 that every Windows 10 and 11 has.
#
#   powershell -ExecutionPolicy Bypass -File launcher\build.ps1 [-Out <folder>]

param([string]$Out)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$csc  = Join-Path $root "tools\roslyn\tasks\net472\csc.exe"
if (-not (Test-Path $csc)) { throw "Roslyn compiler not found at $csc" }
if (-not $Out) { $Out = Join-Path $PSScriptRoot "bin" }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$exe = Join-Path $Out "PummelItemsLauncher.exe"

$fx = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$refs = "mscorlib.dll", "System.dll", "System.Core.dll", "System.Drawing.dll", "System.Windows.Forms.dll" |
        ForEach-Object { "/r:" + (Join-Path $fx $_) }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /nostdlib+ /utf8output /codepage:65001 `
       "/out:$exe" $refs (Join-Path $PSScriptRoot "PummelItemsLauncher.cs")
if ($LASTEXITCODE -ne 0) { throw "Compilation failed (exit $LASTEXITCODE)" }
Write-Host "Built: $exe ($((Get-Item $exe).Length) bytes)"
