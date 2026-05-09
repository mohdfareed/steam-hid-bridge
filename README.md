# Steam HID Bridge

Steam HID Bridge forwards input produced by Steam Input to a dedicated USB HID device.

The intended use is to preserve Steam Input as the source of controller configuration, including gyro behavior, sensitivity, curves, and layouts, while emitting the final input through a standard USB HID mouse/keyboard device.

## Architecture

```text
Steam shortcut -> Windows bridge -> Steam Input API -> USB transport -> Teensy firmware -> USB HID reports -> target game
```

## Components

```text
firmware/   Teensy firmware
app/        Windows bridge application
protocol/   Host-device protocol
docs/       Steam shortcut and spike notes
tests/      Protocol and synthetic input tests
scripts/    Development and test utilities
```

## Hardware

Target device:

* Teensy 4.0
* USB-A to Micro-USB data cable

## Build

Prerequisites:

- Latest stable .NET SDK that supports the project target framework.
- PlatformIO CLI for firmware work.

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\publish.ps1
```

Run the app during pre-hardware development:

```powershell
dotnet run --project .\app\SteamHidBridge.App
dotnet run --project .\app\SteamHidBridge.App -- --profile valorant --launch
```

Firmware build, once PlatformIO is installed:

```powershell
pio run -d .\firmware
```

## Use

1. Publish the Windows bridge.
2. Put an `appsettings.json` next to the executable.
3. Add one Steam shortcut per profile, passing `--profile <id> --launch`. The app can copy Steam ROM Manager manual-parser JSON for these shortcuts.
4. Launch that shortcut from Steam.
5. The bridge previews output and only forwards while the configured receiver process is foreground.
