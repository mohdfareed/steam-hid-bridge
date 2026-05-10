param(
    [switch]$EnableTestSigning,
    [switch]$Sign,
    [switch]$MicrosoftSigned,
    [string]$Configuration = "Debug",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run this script from an elevated PowerShell session."
    }
}

function Find-WindowsKitTool {
    param([string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $toolsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\Tools"
    if (-not (Test-Path -LiteralPath $toolsRoot)) {
        return $null
    }

    $candidate = Get-ChildItem -LiteralPath $toolsRoot -Recurse -Filter $Name -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match "\\$Platform\\" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

    return $candidate.FullName
}

function Enable-TestSigningIfRequested {
    if (-not $EnableTestSigning) {
        return
    }

    & bcdedit /set testsigning on
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enable test signing."
    }

    Write-Host "Test signing is enabled. Reboot before installing the test driver if it was not already enabled."
}

function Invoke-DriverPackageSigning {
    param([string]$CatalogPath)

    if ($MicrosoftSigned) {
        return
    }

    if (-not $Sign) {
        return
    }

    $signtool = Find-WindowsKitTool "signtool.exe"
    if (-not $signtool) {
        throw "signtool.exe was not found. Install the Windows SDK/WDK signing tools."
    }

    $subject = "CN=Steam HID Bridge Test Driver"
    $cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

    if (-not $cert) {
        $cert = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $subject `
            -CertStoreLocation Cert:\CurrentUser\My `
            -KeyUsage DigitalSignature `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(3)
    }

    $rootStore = [Security.Cryptography.X509Certificates.X509Store]::new("Root", "LocalMachine")
    $publisherStore = [Security.Cryptography.X509Certificates.X509Store]::new("TrustedPublisher", "LocalMachine")
    try {
        $rootStore.Open("ReadWrite")
        $publisherStore.Open("ReadWrite")
        $rootStore.Add($cert)
        $publisherStore.Add($cert)
    }
    finally {
        $rootStore.Close()
        $publisherStore.Close()
    }

    & $signtool sign /v /fd SHA256 /s My /n "Steam HID Bridge Test Driver" $CatalogPath
    if ($LASTEXITCODE -ne 0) {
        throw "Driver catalog signing failed."
    }
}

Assert-Admin

if ($MicrosoftSigned -and $Sign) {
    throw "Use either -MicrosoftSigned or -Sign, not both."
}

if ($MicrosoftSigned -and $EnableTestSigning) {
    throw "Use either -MicrosoftSigned or -EnableTestSigning, not both."
}

$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $scriptDir)
$packagedInf = Join-Path $scriptDir "SteamHidBridge.VirtualMouse.inf"
$driverOutput = if (Test-Path -LiteralPath $packagedInf) {
    $scriptDir
}
else {
    Join-Path $repoRoot "driver\SteamHidBridge.VirtualMouse\obj\$Platform\$Configuration"
}

$infPath = Join-Path $driverOutput "SteamHidBridge.VirtualMouse.inf"
$catPath = Join-Path $driverOutput "SteamHidBridge.VirtualMouse.cat"

if (-not (Test-Path -LiteralPath $infPath)) {
    throw "Driver INF not found: $infPath. Build SteamHidBridge.VirtualMouse first."
}

if ($MicrosoftSigned -and -not (Test-Path -LiteralPath $catPath)) {
    throw "Driver catalog not found: $catPath. A Microsoft-signed driver package must include the signed CAT file."
}

Enable-TestSigningIfRequested

if (Test-Path -LiteralPath $catPath) {
    Invoke-DriverPackageSigning -CatalogPath $catPath
}
elseif ($Sign) {
    throw "Driver catalog not found: $catPath. Build the driver package first."
}

Write-Host "Adding driver package..."
& pnputil /add-driver $infPath /install
if ($LASTEXITCODE -ne 0) {
    throw "pnputil failed."
}

$devcon = Find-WindowsKitTool "devcon.exe"
if (-not $devcon) {
    throw "devcon.exe was not found. Install WDK tools or add devcon.exe to PATH."
}

Write-Host "Creating/updating root-enumerated device..."
& $devcon install $infPath "Root\SteamHidBridgeVirtualMouse"
if ($LASTEXITCODE -ne 0) {
    throw "devcon install failed."
}

Write-Host "Driver install completed."
Write-Host "Use the app's Virtual Mouse output mode to verify mouse reports."
