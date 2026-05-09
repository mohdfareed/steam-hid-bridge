# Protocol

The initial wire format is deliberately small and versioned. It exists so the host app, tests, and future Teensy firmware can agree on validation before HID emission is implemented.

```text
byte 0..2   magic: "SHB"
byte 3      protocol version: 1
byte 4      command
byte 5      sequence
byte 6      payload length, max 64 bytes
byte 7..n   payload
last 2      little-endian additive checksum over header + payload
```

Command:

- `0x10` HID input

The first HID input payload covers the keyboard and mouse MVP:

```text
int16  pointer delta x
int16  pointer delta y
int8   vertical wheel
uint16 mouse buttons: left, right, middle, back, forward
uint8  keyboard modifiers: left/right ctrl, shift, alt, gui
uint8  keyboard usage id
```
