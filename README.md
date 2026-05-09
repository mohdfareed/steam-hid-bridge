# Steam HID Bridge

Steam HID Bridge forwards input produced by Steam Input to a dedicated USB HID device.

The intended use is to preserve Steam Input as the source of controller configuration, including gyro behavior, sensitivity, curves, and layouts, while emitting the final input through a standard USB HID mouse/keyboard device.

## Architecture

```text
Steam shortcut -> Windows bridge -> Steam Input -> USB transport -> Teensy firmware -> USB HID reports -> target game
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
dotnet run --project .\app\SteamHidBridge.App -- --profile valorant
```

Firmware build, once PlatformIO is installed:

```powershell
pio run -d .\firmware
```

## Use

1. Flash the firmware.
2. Connect the device.
3. Start the Windows bridge.
4. Select the target application.
5. Enable forwarding.
