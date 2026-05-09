param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts\SteamHidBridge-$Runtime"

dotnet publish "$root\app\SteamHidBridge.App\SteamHidBridge.App.csproj" `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    --output $output
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Published to $output"
