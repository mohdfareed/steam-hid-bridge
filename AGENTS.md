# AGENTS.md

## Objective

Build a minimal Steam-launched host-to-HID bridge that can emit both keyboard and mouse HID reports.

Steam Input remains responsible for input interpretation and controller behavior. The bridge is responsible only for transport, validation, and HID emission.

Codex manages this `AGENTS.md` file as the living project instruction file. Keep it current when project scope, architecture rules, tooling choices, or validation policy changes.

## Scope

The bridge MVP covers:

- The bridge app as the Steam-launched application.
- One Steam shortcut per target profile, each pointing to the same bridge executable with a `--profile <id>` argument.
- Keyboard HID output.
- Mouse HID output, including pointer movement, left/right/middle buttons, two side buttons, and vertical wheel.
- A Windows host app that can be configured to start automatically.
- A host-to-device protocol with explicit validation and visible recoverable failures.

Target games should not need to be added to Steam directly, and they should not need to be launched through Steam. The user launches the bridge shortcut from Steam, then launches the target game normally.

Do not build around Steam Desktop Configuration. Do not require manual Steam Desktop config switching. Do not add controller interpretation, gameplay automation, privileged hooks, anti-cheat bypasses, kernel drivers, or game-specific behavior.

The v1 output scope is keyboard and mouse HID. Native controller or DualSense-style gyro emulation is outside scope unless explicitly added later after evaluating firmware complexity, device identity, and anti-cheat risk.

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
- Current stable .NET
- WPF

Use current official Microsoft tooling and formats for new C# work. Prefer the latest stable .NET SDK/TFM, `.slnx` solution files, C# language features supported by that SDK, SDK-style project files, and current Microsoft test tooling. Use preview SDKs or preview language features only when explicitly requested.

Do not enable C# implicit usings. Keep dependencies explicit and visible.

Do not add `global.json` by default. Let the .NET CLI use the latest installed compatible SDK unless the project later needs a documented reproducible-build or CI reason to pin an SDK.

Do not pin NuGet package versions unless there is a documented compatibility or reproducibility reason. Prefer floating current stable versions for early project scaffolding.

Keep `Directory.Build.props` unless there is a strong reason to move settings into individual project files. It is the central policy for C# language version, explicit usings, nullable analysis, warnings, and build analysis.

Use UTF-8 and LF line endings for repository text files so the project is comfortable to edit on Windows and macOS. Keep `.editorconfig` and `.gitattributes` aligned.

## Design Principles

Keep the hot path small.

Keep diagnostics separate from the hot path.

Keep business logic decoupled from WPF views. UI should bind to view models and services; protocol, transport, validation, startup behavior, and HID mapping logic must remain testable without constructing WPF controls. The WPF UI should be replaceable later without rewriting app logic.

Prefer small interfaces at boundaries that will change: Steam Input integration, transport, device discovery, app startup registration, foreground-window detection, diagnostics, and HID report mapping.

Autostart must be opt-in and visibly configurable. It should use the least surprising Windows mechanism available for a normal per-user desktop app, and it must be easy to disable from the app.

Multiple-instance behavior must be deliberate. Never allow two bridge processes to write to the Teensy simultaneously. Use a named mutex or equivalent single-owner mechanism before opening the HID transport. Use local IPC only if profile switching between Steam shortcuts is needed.

The profile model must distinguish:

- Steam shortcut/profile: the Steam-side configuration selected by launching a specific Steam shortcut.
- Bridge profile: app-side metadata such as target process names, forwarding rules, Teensy device selection, and optional auto-exit behavior.
- Steam Input action sets: in-profile modes such as gameplay, menu, buy menu, or shop; do not repurpose these as separate games.

Use current official documentation for platform APIs, libraries, and tooling.

## Baseline Decisions

- `.slnx` solution format.
- Current stable .NET project files with latest C# language selection and explicit usings.
- WPF shell with profile startup, single-instance guard, foreground target gate, loopback transport, forwarding toggle, keyboard and mouse synthetic input controls, and diagnostics.
- Shared C# protocol library for frame encoding, validation, and HID report payloads.
- MSTest protocol tests for synthetic input and malformed-frame handling.
- PlatformIO firmware placeholder for Teensy 4.0.
- Build, test, and publish scripts under `scripts/`.

## Steam Input Research Order

Validate Steam shortcut/runtime behavior before building more UI:

1. Create a Steam-launched spike executable.
2. Add it to Steam as a non-Steam shortcut.
3. Confirm Steam keeps the shortcut in the Running state while the bridge process is alive.
4. Confirm Steam's Stop button terminates or requests termination of the bridge process.
5. Confirm multiple Steam shortcuts can point to the same executable while retaining distinct Steam Input configurations.
6. Confirm launch arguments identify the intended profile.
7. Confirm only one bridge instance owns the Teensy device at a time.
8. Confirm the app can initialize Steamworks/Steam Input from this shortcut mode.
9. Confirm the app can read Steam Input analog actions, including `absolute_mouse` mouse-like delta data.
10. Confirm Steam Controller, DualSense, or other gyro-capable controllers can be configured through Steam Input for that action and produce usable delta data.
11. Forward those deltas to the Teensy and verify HID mouse output.

Do not implement Steam config editing, Steam VDF rewriting, or automatic Steam layout import/export in v1. Those are future research items.

## Repository Layout

```text
firmware/
app/
protocol/
docs/
tests/
scripts/
README.md
AGENTS.md
```

## Validation

The project should include synthetic input tests for protocol and HID mapping behavior before performance claims are made.

Latency-sensitive behavior should be measured, not inferred.

Do not display or document latency numbers from loopback-only validation as real device latency. Real latency requires measurement across the host transport and firmware HID emission path.

Failures should be visible and recoverable without requiring firmware re-flashing.
