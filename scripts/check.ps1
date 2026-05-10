param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& (Join-Path $root "scripts\internal\dotnet-build.ps1") -Configuration $Configuration
& (Join-Path $root "scripts\internal\dotnet-test.ps1") -Configuration $Configuration
