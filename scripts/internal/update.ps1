param(
    [Parameter(Mandatory = $true)]
    [string] $PackageUrl,

    [Parameter(Mandatory = $true)]
    [string] $InstallDir,

    [Parameter(Mandatory = $true)]
    [int] $CurrentProcessId
)

$ErrorActionPreference = "Stop"

$processName = "SteamHidBridge"
$assetName = "SteamHidBridge-win-x64.zip"
$signalName = "Local\SteamHidBridge.ShutdownForUpdate"
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("SteamHidBridgeUpdate-" + [guid]::NewGuid())
$zipPath = Join-Path $tempRoot $assetName
$extractPath = Join-Path $tempRoot "package"

function Get-BridgeProcesses {
    Get-Process -Name $processName -ErrorAction SilentlyContinue |
    Where-Object { $_.Id -ne $PID }
}

function Wait-ForBridgeExit {
    param([int] $TimeoutSeconds = 30)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $running = @(Get-BridgeProcesses)
        if ($running.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)

    Get-BridgeProcesses | Stop-Process -Force
}

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

try {
    Write-Host "Downloading $PackageUrl"
    Invoke-WebRequest -Uri $PackageUrl -OutFile $zipPath

    New-Item -ItemType Directory -Path $extractPath -Force | Out-Null
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force

    $shutdownEvent = [Threading.EventWaitHandle]::new($false, [Threading.EventResetMode]::ManualReset, $signalName)
    $shutdownEvent.Reset() | Out-Null
    $shutdownEvent.Set() | Out-Null

    Write-Host "Waiting for Steam HID Bridge instances to close"
    Wait-ForBridgeExit
    $shutdownEvent.Reset() | Out-Null
    $shutdownEvent.Dispose()

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

    Get-ChildItem -LiteralPath $InstallDir -Force |
    Remove-Item -Recurse -Force

    Copy-Item -Path (Join-Path $extractPath "*") -Destination $InstallDir -Recurse -Force
    Write-Host "Steam HID Bridge updated."
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
