# Steam HID Bridge

Steam HID Bridge forwards input produced by Steam Input to a dedicated USB HID device.

The intended use is to preserve Steam Input as the source of controller configuration, including gyro behavior, sensitivity, curves, and layouts, while emitting the final input through a standard USB HID mouse/keyboard device.

## Architecture

```text
Steam Input -> Windows bridge -> USB transport -> Teensy firmware -> USB HID reports
````

## Components

```text
firmware/   Teensy firmware
app/        Windows bridge application
protocol/   Host-device protocol
docs/       Design and validation notes
tools/      Development and test utilities
```

## Hardware

Target device:

* Teensy 4.0
* USB-A to Micro-USB data cable

## Use

1. Flash the firmware.
2. Connect the device.
3. Start the Windows bridge.
4. Select the target application.
5. Enable forwarding.
