# Software Output Driver

This folder is reserved for a Windows virtual HID output path.

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

1. Install Visual Studio driver tooling and the Windows Driver Kit.
2. Create a minimal KMDF/VHF mouse-only driver.
3. Expose a private control device or device interface for user-mode reports.
4. Submit fixed-size mouse reports from a tiny console test client.
5. Add the app-side output consumer only after the console client works.
6. Measure latency and CPU before adding keyboard support.

## Non-Goals

- Do not hook or inject into games.
- Do not hide devices from anti-cheat.
- Do not add filter drivers unless VHF cannot satisfy the device model.
- Do not merge driver install/update into the normal app installer until the driver is proven useful.

## Tooling Requirement

This cannot be built with only the .NET SDK. It requires Windows Driver Kit/KMDF tooling and a driver signing plan.
