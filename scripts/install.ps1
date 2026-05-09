param(
    [string]$Version = "",
    [string]$InstallDir = "",
    [switch]$NoDesktopShortcut
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repo = "mohdfareed/steam-hid-bridge"
$assetName = "SteamHidBridge-win-x64.zip"
$defaultInstallDir = Join-Path ([Environment]::GetFolderPath("LocalApplicationData")) "Programs\SteamHidBridge"
$headers = @{
    "Accept" = "application/vnd.github+json"
    "User-Agent" = "SteamHidBridgeInstaller"
}

if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $answer = Read-Host "Install location [$defaultInstallDir]"
    $InstallDir = if ([string]::IsNullOrWhiteSpace($answer)) { $defaultInstallDir } else { $answer.Trim('"') }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $releaseUri = "https://api.github.com/repos/$repo/releases/latest"
} else {
    $tag = if ($Version.StartsWith("v", [StringComparison]::OrdinalIgnoreCase)) { $Version } else { "v$Version" }
    $releaseUri = "https://api.github.com/repos/$repo/releases/tags/$tag"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("SteamHidBridgeInstall-" + [Guid]::NewGuid())
$zipPath = Join-Path $tempRoot $assetName
$extractPath = Join-Path $tempRoot "extract"
$preservedSettingsPath = Join-Path $tempRoot "appsettings.json"

try {
    New-Item -ItemType Directory -Path $tempRoot, $extractPath -Force | Out-Null

    Write-Host "Reading release metadata..."
    $release = Invoke-RestMethod -Uri $releaseUri -Headers $headers
    $asset = $release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if ($null -eq $asset) {
        throw "Release '$($release.tag_name)' does not contain $assetName."
    }

    Write-Host "Downloading $assetName from $($release.tag_name)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers $headers

    Write-Host "Installing to $InstallDir..."
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractPath -Force

    $settingsPath = Join-Path $InstallDir "appsettings.json"
    if (Test-Path -LiteralPath $settingsPath) {
        Copy-Item -LiteralPath $settingsPath -Destination $preservedSettingsPath -Force
    }

    Get-ChildItem -LiteralPath $InstallDir -Force |
        Where-Object { $_.Name -ne "appsettings.json" } |
        Remove-Item -Recurse -Force

    foreach ($item in Get-ChildItem -LiteralPath $extractPath -Force) {
        $destination = Join-Path $InstallDir $item.Name
        if ($item.Name -eq "appsettings.json" -and (Test-Path $destination)) {
            continue
        }

        Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse -Force
    }

    if (Test-Path -LiteralPath $preservedSettingsPath) {
        Copy-Item -LiteralPath $preservedSettingsPath -Destination $settingsPath -Force
    }

    $exampleSettingsPath = Join-Path $InstallDir "appsettings.example.json"
    if (-not (Test-Path $settingsPath) -and (Test-Path $exampleSettingsPath)) {
        Copy-Item -LiteralPath $exampleSettingsPath -Destination $settingsPath
    }

    Get-ChildItem -LiteralPath $InstallDir -Recurse -Force | Unblock-File -ErrorAction SilentlyContinue

    $exePath = Join-Path $InstallDir "SteamHidBridge.exe"
    if (-not (Test-Path $exePath)) {
        throw "Installed files did not include $exePath."
    }

    if (-not $NoDesktopShortcut) {
        $desktop = [Environment]::GetFolderPath("DesktopDirectory")
        $shortcutPath = Join-Path $desktop "Steam HID Bridge.lnk"
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $exePath
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.IconLocation = "$exePath,0"
        $shortcut.Save()
        Write-Host "Desktop shortcut: $shortcutPath"
    }

    Write-Host "Installed Steam HID Bridge $($release.tag_name)."
    Write-Host "Settings: $settingsPath"
} finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
