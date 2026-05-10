# Steam HID Bridge Virtual Mouse

Minimal KMDF/VHF mouse-only driver spike.

This project is intentionally separate from the WPF app. It creates a virtual HID mouse and accepts fixed-size mouse reports through a private device interface. The app should not depend on this until the driver is proven with a tiny sender.

## Current Scope

- Mouse only.
- Relative X/Y movement.
- Five buttons.
- Vertical wheel.
- One IOCTL for submitting a complete mouse report.

Keyboard support and app integration come later.

## Build

Open `SteamHidBridge.VirtualMouse.vcxproj` in Visual Studio 2026 with WDK tooling installed.

The project uses the VS 2026 C++ toolset and the WDK 28000 package line:

```xml
<PackageReference Include="Microsoft.Windows.WDK.x64" Version="10.0.28000.1839" />
```

This is a driver project, so `dotnet build` is not the build path.

After the driver is installed and started, build and run `SteamHidBridge.VirtualMouse.TestSender`. It opens the driver device interface and sends a small movement, click, release, and wheel report.

## Install Notes

This is a root-enumerated test driver:

```text
Root\SteamHidBridgeVirtualMouse
```

Expect test signing / driver signing friction. Do not include this in the normal app installer until install, uninstall, signing, and anti-cheat implications are understood.

There are two install paths:

- Local development: sign with a local test certificate and enable Windows test mode. Secure Boot blocks local test mode, so this path requires a test machine or VM where Secure Boot is disabled.
- Secure Boot enabled: use Microsoft Hardware Dev Center signing. Preproduction signing is for provisioned test machines; attestation or WHCP signing is the release-style path.

The packaged installer supports both shapes:

```powershell
# Local development package.
.\install.ps1 -EnableTestSigning -Sign

# Microsoft-signed package.
.\install.ps1 -MicrosoftSigned
```
