# Software Output Driver

This folder contains the Windows virtual HID output path.

The intended implementation is a KMDF HID source driver using Microsoft's Virtual HID Framework (VHF). That is the closest clean Windows equivalent to a reWASD-style software output path for mouse and keyboard reports.

## Why This Is Separate

The WPF app can observe and route input in user mode, but a real virtual mouse/keyboard device requires driver-level work. `SendInput` is not enough for this project because games that accept it usually already accept Steam's legacy mouse output.

The driver path should stay separate from the app for install, signing, admin permissions, uninstall, and anti-cheat risk review.

## Target Shape

```text
Steam legacy mouse output
  -> Windows Raw Input observer
  -> BridgeRuntime foreground gate
  -> user-mode driver client
  -> KMDF/VHF virtual HID driver
  -> Windows HID mouse/keyboard device
  -> receiver game
```

## First Milestones

1. Build `SteamHidBridge.VirtualMouse`.
2. Install the root-enumerated test driver.
3. Use the app's Virtual Mouse output mode to send mouse frames to the driver.
4. Measure latency and CPU before adding keyboard support.

## Non-Goals

- Do not hook or inject into games.
- Do not hide devices from anti-cheat.
- Do not add filter drivers unless VHF cannot satisfy the device model.
- Do not merge driver install/update into the normal app installer until the driver is proven useful.

## Tooling Requirement

This cannot be built with only the .NET SDK. It targets Visual Studio 2026 with Windows Driver Kit 28000 tooling and requires a driver signing plan.

## Signing And Install

Windows x64 kernel drivers must be signed. This is not the same as a normal unsigned app warning that the user can click through.

Development flow:

```powershell
.\artifacts\SteamHidBridge-win-x64\Driver\install.ps1 -EnableTestSigning -Sign
```

`-EnableTestSigning` runs `bcdedit /set testsigning on`; reboot after enabling it. `-Sign` creates/reuses a local test code-signing certificate, trusts it on the local machine, signs the driver catalog, adds the driver package with `pnputil`, and creates the root-enumerated test device with `devcon`.

Release flow requires Microsoft-trusted driver signing, for example attestation signing through Partner Center. Until then, the packaged driver install script supports local test signing for development builds.

## Project

The driver project lives under:

```text
driver\SteamHidBridge.VirtualMouse\
```

It currently exposes a mouse-only VHF device with relative X/Y movement, five buttons, vertical wheel, and one IOCTL for submitting a complete mouse report. Keyboard support is not implemented yet.
