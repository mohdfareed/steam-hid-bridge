param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& (Join-Path $root "scripts\internal\dotnet-build.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $root "scripts\internal\dotnet-test.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $root "scripts\internal\driver-build.ps1") -Configuration $Configuration -Platform x64
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $root "scripts\internal\firmware-build.ps1") -Environment teensy40
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
