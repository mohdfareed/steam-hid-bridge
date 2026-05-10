param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipDriver,
    [switch]$PackageOnly,
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if ($PackageOnly) {
    & (Join-Path $root "scripts\internal\package.ps1") -Configuration $Configuration -Runtime $Runtime -Version $Version -SkipDriver:$SkipDriver
    exit $LASTEXITCODE
}

& (Join-Path $root "scripts\internal\deploy.ps1") -Runtime $Runtime -SkipDriver:$SkipDriver
