<#
.SYNOPSIS
Formats and builds the app, then builds the firmware.
#>

param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& (Join-Path $root "scripts\internal\app-build.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $root "scripts\internal\firmware-build.ps1") -Environment teensy40
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
