# Regenerates deck/picker-art.txt, the launch picker's lettering, from deck/picker-art.cs.
# Windows only: the text is rendered with the system's fonts through System.Drawing.
#
#   powershell -ExecutionPolicy Bypass -File deck\make-picker-art.ps1

$ErrorActionPreference = "Stop"
$source = Join-Path $PSScriptRoot "picker-art.cs"
$output = Join-Path $PSScriptRoot "picker-art.txt"

# Read as UTF-8 explicitly: the strings in it are Cyrillic, and Windows PowerShell would
# otherwise take the file for the ANSI code page.
$code = [IO.File]::ReadAllText($source, [Text.Encoding]::UTF8)
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
[PickerArt]::Write($output)

Write-Host "Written: $output ($((Get-Item $output).Length) bytes)"
