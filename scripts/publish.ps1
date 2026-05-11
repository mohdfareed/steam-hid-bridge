<#
.SYNOPSIS
Publishes the Windows app and copies bundled firmware artifacts into the publish folder.
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts\SteamHidBridge-$Runtime"

if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

$restoreArgs = @(
    "restore",
    "$root\app\SteamHidBridge.App\SteamHidBridge.App.csproj",
    "--runtime", $Runtime
)

dotnet @restoreArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$publishArgs = @(
    "publish",
    "$root\app\SteamHidBridge.App\SteamHidBridge.App.csproj",
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", "true",
    "--output", $output,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=embedded",
    "-p:DebugSymbols=false"
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishArgs += "-p:Version=$Version"
    $publishArgs += "-p:InformationalVersion=$Version"
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $root "scripts\internal\firmware-build.ps1") -Environment teensy40
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$firmwareSource = Join-Path $root "firmware\.pio\build\teensy40\firmware.hex"
$firmwareDest = Join-Path $output "Firmware"
New-Item -ItemType Directory -Force -Path $firmwareDest | Out-Null
Copy-Item -LiteralPath $firmwareSource -Destination (Join-Path $firmwareDest "SteamHidBridge.Board.hex") -Force

$teensyToolDir = Join-Path $env:USERPROFILE ".platformio\packages\tool-teensy"
foreach ($toolName in @("teensy.exe", "teensy_post_compile.exe", "teensy_reboot.exe")) {
    $toolPath = Join-Path $teensyToolDir $toolName
    if (Test-Path -LiteralPath $toolPath) {
        Copy-Item -LiteralPath $toolPath -Destination $firmwareDest -Force
    }
}

Write-Host "Published to $output"
