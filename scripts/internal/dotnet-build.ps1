param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Invoke-DotNet {
    dotnet @args
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Invoke-DotNet restore (Join-Path $root "app\SteamHidBridge.App\SteamHidBridge.App.csproj")
Invoke-DotNet restore (Join-Path $root "protocol\SteamHidBridge.Protocol.Tests\SteamHidBridge.Protocol.Tests.csproj")

Invoke-DotNet format (Join-Path $root "app\SteamHidBridge.App\SteamHidBridge.App.csproj") --verify-no-changes --no-restore
Invoke-DotNet format (Join-Path $root "protocol\SteamHidBridge.Protocol.Tests\SteamHidBridge.Protocol.Tests.csproj") --verify-no-changes --no-restore

Invoke-DotNet build (Join-Path $root "app\SteamHidBridge.App\SteamHidBridge.App.csproj") --configuration $Configuration --no-restore
