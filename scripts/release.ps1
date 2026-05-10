param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$TagRelease,
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (!$TagRelease) {
    & (Join-Path $root "scripts\internal\package.ps1") -Configuration $Configuration -Runtime $Runtime -Version $Version
    exit $LASTEXITCODE
}

& (Join-Path $root "scripts\internal\deploy.ps1") -Runtime $Runtime
