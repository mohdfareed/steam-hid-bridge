param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.0.0",
    [switch]$SkipDriver
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts\SteamHidBridge-$Runtime"

if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

$oldDriverStaging = Join-Path $root "artifacts\driver"
if (Test-Path $oldDriverStaging) {
    Remove-Item -LiteralPath $oldDriverStaging -Recurse -Force
}

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

if (-not $SkipDriver) {
    & (Join-Path $root "scripts\internal\driver-build.ps1") -Configuration $Configuration -Platform x64

    $driverSource = Join-Path $root "driver\obj\SteamHidBridge.VirtualMouse\x64\$Configuration"
    $senderSource = Join-Path $root "driver\obj\SteamHidBridge.VirtualMouse.TestSender\x64\$Configuration"
    $driverDest = Join-Path $output "driver"
    New-Item -ItemType Directory -Force -Path $driverDest | Out-Null

    Copy-Item -LiteralPath (Join-Path $driverSource "SteamHidBridge.VirtualMouse.inf") -Destination $driverDest -Force
    Copy-Item -LiteralPath (Join-Path $driverSource "SteamHidBridge.VirtualMouse.sys") -Destination $driverDest -Force
    Copy-Item -LiteralPath (Join-Path $driverSource "steamhidbridge.virtualmouse.cat") -Destination (Join-Path $driverDest "SteamHidBridge.VirtualMouse.cat") -Force
    Copy-Item -LiteralPath (Join-Path $senderSource "SteamHidBridge.VirtualMouse.TestSender.exe") -Destination $driverDest -Force
    Copy-Item -LiteralPath (Join-Path $root "scripts\internal\driver-install.ps1") -Destination (Join-Path $driverDest "install.ps1") -Force
}

Write-Host "Published to $output"
