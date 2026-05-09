

# AGENTS.md

## Objective

Build a minimal host-to-HID bridge.

Steam Input remains responsible for input interpretation and controller behavior. The bridge is responsible only for transport, validation, and HID emission.

## Constraints

The implementation must be simple, observable, and low-latency.

The project must not interfere with anti-cheat systems, platform security controls, software licenses, or game terms of service.

Avoid privileged system components unless there is a documented requirement and no simpler alternative.

## Stack

Firmware:

- Teensy 4.0
- PlatformIO
- Arduino/Teensyduino framework
- C-style C++

Windows application:

- C#
- Current .NET
- WPF

## Design Principles

Keep the hot path small.

Keep diagnostics separate from the hot path.

Use current official documentation for platform APIs, libraries, and tooling.

## Repository Layout

```text
firmware/
app/
protocol/
docs/
tools/
README.md
AGENTS.md
````

## Validation

The project should include synthetic input tests before performance claims are made.

Latency-sensitive behavior should be measured, not inferred.

Failures should be visible and recoverable without requiring firmware re-flashing.
