param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

dotnet test (Join-Path $root "protocol\SteamHidBridge.Protocol.Tests\SteamHidBridge.Protocol.Tests.csproj") --configuration $Configuration
