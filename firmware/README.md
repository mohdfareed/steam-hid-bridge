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

PlatformIO must be installed separately.
`scripts/check.ps1` and `scripts/publish.ps1` also build this firmware. Publish copies the generated hex to:

```text
artifacts\SteamHidBridge-win-x64\firmware\SteamHidBridge.Teensy40.hex
```

Upload to the board:

```powershell
pio run -d .\firmware -t upload
```

The firmware listens on the Teensy USB serial interface at `115200` baud and emits mouse HID reports for validated bridge frames.

## Responsibilities

- Parse and validate the host protocol.
- Emit keyboard and mouse HID reports.
- Keep the hot path small and allocation-free.
- Keep diagnostics separate from HID emission.
- Provide visible recoverable failure states for bad frames and disconnects.

The firmware must not implement Steam Input behavior, controller interpretation, game-specific behavior, anti-cheat bypasses, or privileged host behavior.
