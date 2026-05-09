# AGENTS.md

## Objective

Build a minimal Steam-launched host-to-HID bridge that can emit both keyboard and mouse HID reports.

Steam Input remains responsible for input interpretation and controller behavior. The bridge is responsible only for transport, validation, and HID emission.

Codex manages this `AGENTS.md` file as the living project instruction file. Keep it current when project scope, architecture rules, tooling choices, or validation policy changes.

## Scope

The bridge MVP covers:

- The bridge app as the Steam-launched application.
- One Steam shortcut per target profile, each pointing to the same bridge executable with a `--profile <id>` argument. Use `--launch` when the bridge should start the configured target executable.
- Keyboard HID output.
- Mouse HID output, including pointer movement, left/right/middle buttons, two side buttons, and vertical wheel.
- A host-to-device protocol with explicit validation and visible recoverable failures.

Target games should not need to be added to Steam directly, and they should not need to be launched through Steam. In `--launch` mode, the bridge launches the configured target executable as a normal child process while Steam tracks the bridge process.

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

C# analyzer suggestion categories are build warnings through `.editorconfig`. Prefer category-level warning policy over individual diagnostic IDs, and do not promote these warnings to errors by default. Apply analyzer suggestions instead of leaving suggestion squiggles for later unless the suggestion would make the code worse for this MVP and is explicitly suppressed with a narrow reason.

Do not add `global.json` by default. Let the .NET CLI use the latest installed compatible SDK unless the project later needs a documented reproducible-build or CI reason to pin an SDK.

Do not pin NuGet package versions unless there is a documented compatibility or reproducibility reason. Prefer floating current stable versions for early project scaffolding.

Keep `Directory.Build.props` unless there is a strong reason to move settings into individual project files. It is the central policy for C# language version, explicit usings, nullable analysis, warnings, and build analysis.

Use UTF-8 and LF line endings for repository text files so the project is comfortable to edit on Windows and macOS. Keep `.editorconfig` and `.gitattributes` aligned.

## Design Principles

Keep the hot path small.

Keep diagnostics separate from the hot path.

Keep business logic decoupled from WPF views. UI should bind to view models and services; protocol, transport, validation, startup behavior, and HID mapping logic must remain testable without constructing WPF controls. The WPF UI should be replaceable later without rewriting app logic.

Do not add abstractions, settings, modes, services, or configuration switches unless they are required by the MVP or the user explicitly asks for them. When a use case is unclear, ask before implementing.

Prefer a few focused files over many tiny abstractions. Keep the code easy for a person to read and change.

Multiple bridge instances are supported. Each Steam shortcut may launch its own process so Steam Play/Stop state can track that shortcut. Forwarding must still be gated so only the instance whose receiver game is foreground emits output.

The profile model must distinguish:

- Steam shortcut/profile: the Steam-side configuration selected by launching a specific Steam shortcut.
- Game profile: app-side metadata stored in `appsettings.json`, keyed by id, with executable, arguments, working directory, and receiver process names.
- Steam Input action sets: in-profile modes such as gameplay, menu, buy menu, or shop; do not repurpose these as separate games.

Use current official documentation for platform APIs, libraries, and tooling.

## Baseline Decisions

- `.slnx` solution format.
- Current stable .NET project files with latest C# language selection and explicit usings.
- WPF shell with editable game settings, profile startup, optional target launch, foreground receiver gate, Steam config forcing, and output preview.
- Launch mode (`--launch`) starts the configured target, keeps the bridge UI hidden behind a tray icon, and shows only a transparent Steam overlay host window. The current tray behavior is per bridge process; do not add a shared tray host or cross-process instance list without an explicit IPC decision.
- If the bridge starts a target process, it owns that process lifetime. Steam stopping the bridge should close the launched process tree, and the bridge should exit when the launched receiver exits.
- In normal interactive mode, closing the status window exits the app. In `--launch` mode, closing the status window hides it back to the tray. Launch-mode instances exit through the tray Exit command, launched receiver exit, or Steam/process termination.
- Multiple instances may save `appsettings.json`; writes must use the settings-store mutex and atomic write path so profile saves merge with the latest file contents.
- Output is always previewed in the app model. When real Steam Input and Teensy transport are added, forwarding must only run while a configured receiver process is the foreground process. Do not add manual output-destination modes for v1.
- Steam ROM Manager export should generate bridge-targeted shortcuts, not game-targeted shortcuts. Each generated entry should launch the bridge with `--profile <id> --launch`; the selected profile then launches the configured game executable.
- Do not inject a synthetic `default` profile. If no profile is requested, select an existing profile; create a local `new-game` entry only when there are no profiles loaded.
- Publish output should be self-contained for the selected Windows runtime unless the user asks for framework-dependent deployment.
- Release tags use `vMAJOR.MINOR.PATCH`, for example `v0.1.1`. Tag pushes matching that shape build, test, package, and create a GitHub Release with `SteamHidBridge-win-x64.zip`.
- The installer script lives at `scripts/install.ps1`, downloads from GitHub Releases, preserves existing `appsettings.json`, and creates a Desktop shortcut.
- App updates use GitHub Releases. The app checks the latest release, asks for confirmation, launches `update.ps1`, closes all bridge instances, preserves `appsettings.json`, and replaces the published app folder. Do not make the running process overwrite its own executable directly.
- Steam Input config forcing uses Steam's official `steam://forceinputappid/<appid>` URL only while the configured receiver is foreground, and resets with `steam://forceinputappid/0` when foreground is lost or the bridge exits.
- Steam Input integration belongs under `app/SteamHidBridge.App/Steam/`. Keep the real Steamworks API reader isolated there; do not let WPF focus state drive input capture.
- Shared C# protocol library for frame encoding, validation, and the HID input payload.
- MSTest protocol tests for synthetic input and malformed-frame handling.
- PlatformIO firmware placeholder for Teensy 4.0.
- Build, test, and publish scripts under `scripts/`.
- `scripts/deploy.ps1` is the local release gate. It prompts for the next version after printing the latest tag, runs formatting/build/test/package checks, requires a clean working tree, then creates and pushes the version tag that triggers the GitHub Release workflow.

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

Do not display or document synthetic-only latency numbers as real device latency. Real latency requires measurement across the host transport and firmware HID emission path.

Failures should be visible and recoverable without requiring firmware re-flashing.
