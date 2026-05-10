param(
    [string] $Environment = "teensy40"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$firmwareDir = Join-Path $root "firmware"

function Find-PlatformIO {
    $command = Get-Command "pio" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $extensionPath = Join-Path $env:USERPROFILE ".platformio\penv\Scripts\platformio.exe"
    if (Test-Path -LiteralPath $extensionPath) {
        return $extensionPath
    }

    throw "PlatformIO CLI was not found. Install PlatformIO or run the VS Code PlatformIO extension once so it creates $extensionPath."
}

$platformio = Find-PlatformIO
Write-Host "Building Teensy firmware"
& $platformio run -d $firmwareDir -e $Environment --silent
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
