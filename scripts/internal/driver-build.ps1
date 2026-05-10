param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

function Find-MSBuild {
    $vswherePaths = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe"
    )

    foreach ($vswhere in $vswherePaths) {
        if (-not (Test-Path -LiteralPath $vswhere)) {
            continue
        }

        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\Current\Bin\MSBuild.exe" | Select-Object -First 1
        if ($path -and (Test-Path -LiteralPath $path)) {
            return $path
        }
    }

    $knownPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2026\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($path in $knownPaths) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    throw "MSBuild.exe was not found. Install Visual Studio with C++ tooling."
}

function Find-VcTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

    $tool = Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio" -Recurse -Filter $ToolName -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\VC\\Tools\\MSVC\\.*\\bin\\HostX86\\x64\\" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $tool) {
        throw "$ToolName was not found. Install Visual Studio C++ build tools."
    }

    return $tool.FullName
}

if ($Platform -ne "x64") {
    throw "Only x64 driver builds are currently supported."
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$driverDir = Join-Path $repoRoot "driver\SteamHidBridge.VirtualMouse"
$driverProject = Join-Path $driverDir "SteamHidBridge.VirtualMouse.vcxproj"
$senderProject = Join-Path $repoRoot "driver\SteamHidBridge.VirtualMouse.TestSender\SteamHidBridge.VirtualMouse.TestSender.vcxproj"
$driverOut = Join-Path $repoRoot "artifacts\driver\SteamHidBridge.VirtualMouse\$Platform\$Configuration"
$driverObj = Join-Path $repoRoot "obj\driver\SteamHidBridge.VirtualMouse\$Platform\$Configuration"
$wdkRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.wdk.x64\10.0.28000.1839\c"
$sdkRoot = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.windows.sdk.cpp\10.0.28000.1721\c"
$kitVersion = "10.0.28000.0"
$kmdfVersion = "1.35"

$cl = Find-VcTool "cl.exe"
$link = Find-VcTool "link.exe"
$vcToolsRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $cl)))
$vcInclude = Join-Path $vcToolsRoot "include"
$stampinf = Join-Path $wdkRoot "bin\$kitVersion\x64\stampinf.exe"
$inf2cat = Join-Path $wdkRoot "bin\$kitVersion\x86\Inf2Cat.exe"
$msbuild = Find-MSBuild

& $msbuild $driverProject /t:Restore /p:Configuration=$Configuration /p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "Driver dependency restore failed."
}

foreach ($path in @($wdkRoot, $sdkRoot, $stampinf, $inf2cat)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required driver build dependency not found: $path"
    }
}

New-Item -ItemType Directory -Force -Path $driverOut, $driverObj | Out-Null

$driverObjPath = Join-Path $driverObj "Driver.obj"
$sysPath = Join-Path $driverOut "SteamHidBridge.VirtualMouse.sys"
$pdbPath = Join-Path $driverOut "SteamHidBridge.VirtualMouse.pdb"
$infPath = Join-Path $driverOut "SteamHidBridge.VirtualMouse.inf"
$kmLibPath = Join-Path $wdkRoot "Lib\$kitVersion\km\x64"
$wdfLibPath = Join-Path $wdkRoot "Lib\wdf\kmdf\x64\$kmdfVersion"

Copy-Item -LiteralPath (Join-Path $driverDir "SteamHidBridge.VirtualMouse.inf") -Destination $infPath -Force

& $cl /nologo /c /TP /W4 /WX /kernel /GS /GR- /EHsc- /Zl /Zc:wchar_t /Zc:forScope /D _AMD64_ /D AMD64 /D _WIN64 /D _WIN32_WINNT=0x0A00 /D WINVER=0x0A00 /I $driverDir /I $vcInclude /I (Join-Path $wdkRoot "Include\$kitVersion\km") /I (Join-Path $wdkRoot "Include\wdf\kmdf\$kmdfVersion") /I (Join-Path $sdkRoot "Include\$kitVersion\shared") /I (Join-Path $sdkRoot "Include\$kitVersion\ucrt") /Fo$driverObjPath (Join-Path $driverDir "Driver.cpp")
if ($LASTEXITCODE -ne 0) {
    throw "Driver compile failed."
}

& $link /nologo /driver /subsystem:native /machine:x64 /entry:DriverEntry /nodefaultlib /out:$sysPath /pdb:$pdbPath "/libpath:$kmLibPath" "/libpath:$wdfLibPath" $driverObjPath ntoskrnl.lib hal.lib VhfKm.lib wdfdriverentry.lib wdfldr.lib BufferOverflowK.lib
if ($LASTEXITCODE -ne 0) {
    throw "Driver link failed."
}

& $stampinf -f $infPath -d "*" -v "0.1.0.0"
if ($LASTEXITCODE -ne 0) {
    throw "stampinf failed."
}

& $inf2cat /driver:$driverOut /os:10_X64
if ($LASTEXITCODE -ne 0) {
    throw "inf2cat failed."
}

& $msbuild $senderProject /restore /m /p:Configuration=$Configuration /p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "Driver test sender build failed."
}
