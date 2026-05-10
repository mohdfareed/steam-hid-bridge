param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

dotnet restore (Join-Path $root "app\SteamHidBridge.App\SteamHidBridge.App.csproj")
dotnet restore (Join-Path $root "protocol\SteamHidBridge.Protocol.Tests\SteamHidBridge.Protocol.Tests.csproj")

dotnet format (Join-Path $root "app\SteamHidBridge.App\SteamHidBridge.App.csproj") --verify-no-changes --no-restore
dotnet format (Join-Path $root "protocol\SteamHidBridge.Protocol.Tests\SteamHidBridge.Protocol.Tests.csproj") --verify-no-changes --no-restore

dotnet build (Join-Path $root "app\SteamHidBridge.App\SteamHidBridge.App.csproj") --configuration $Configuration --no-restore
