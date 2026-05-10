param(
    [string]$Version = "",
    [string]$InstallDir = "",
    [switch]$NoDesktopShortcut
)

$script = Join-Path $PSScriptRoot "internal\app-install.ps1"
& $script -Version $Version -InstallDir $InstallDir -NoDesktopShortcut:$NoDesktopShortcut
exit $LASTEXITCODE
