# Firmware

The firmware is the Teensy-side HID emitter. It should validate host frames, reject malformed input, emit keyboard and mouse HID reports, and expose recoverable diagnostics without requiring a reflash.

## Target

- Teensy 4.0
- PlatformIO
- Arduino/Teensyduino framework
- C-style C++

## Build

```powershell
pio run -d .\firmware
```

PlatformIO must be installed separately. Until hardware arrives, this directory contains only a placeholder sketch and PlatformIO configuration.

## Responsibilities

- Parse and validate the host protocol.
- Emit keyboard and mouse HID reports.
- Keep the hot path small and allocation-free.
- Keep diagnostics separate from HID emission.
- Provide visible recoverable failure states for bad frames, disconnects, and reset commands.

The firmware must not implement Steam Input behavior, controller interpretation, game-specific behavior, anti-cheat bypasses, or privileged host behavior.
