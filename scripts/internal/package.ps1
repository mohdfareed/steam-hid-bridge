<#
.SYNOPSIS
Builds the publish folder, zips it, and emits the standalone updater asset.
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$publishDir = Join-Path $root "artifacts\SteamHidBridge-$Runtime"
$packagePath = Join-Path $root "artifacts\SteamHidBridge-$Runtime.zip"
$updaterPath = Join-Path $root "artifacts\SteamHidBridge-updater.ps1"

& (Join-Path $root "scripts\publish.ps1") -Configuration $Configuration -Runtime $Runtime -Version $Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (Test-Path $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    Set-Content -LiteralPath (Join-Path $publishDir "VERSION") -Value $Version -Encoding utf8
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $packagePath -CompressionLevel Optimal
Copy-Item -LiteralPath (Join-Path $root "scripts\internal\app-update.ps1") -Destination $updaterPath -Force
Write-Host "Packaged to $packagePath"
Write-Host "Updater asset at $updaterPath"
