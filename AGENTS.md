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

Software virtual HID output may be investigated while Teensy hardware is delayed, but it must live behind a separate driver boundary. Do not use `SendInput` as a serious output backend. A reWASD-class software option means a signed virtual HID driver, preferably KMDF/VHF, with explicit install/uninstall and risk documentation.

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

The Windows app runtime belongs outside WPF views and view models. `Runtime/` owns process lifetime, foreground gating, Steam config forcing, and input routing. View models should only expose editable state and commands for WPF binding.

Do not add abstractions, settings, modes, services, or configuration switches unless they are required by the MVP or the user explicitly asks for them. When a use case is unclear, ask before implementing.

Prefer a few focused files over many tiny abstractions. Keep the code easy for a person to read and change.

Multiple bridge instances are supported. Each Steam shortcut may launch its own process so Steam Play/Stop state can track that shortcut. Forwarding must still be gated so only the instance whose receiver game is foreground emits output.

The profile model must distinguish:

- Steam shortcut/profile: the Steam-side configuration selected by launching a specific Steam shortcut.
- Game profile: app-side metadata stored in `%LOCALAPPDATA%\SteamHidBridge\appsettings.json`, keyed by id, with executable, arguments, working directory, and receiver process names.
- Steam Input action sets: in-profile modes such as gameplay, menu, buy menu, or shop; do not repurpose these as separate games.

Use current official documentation for platform APIs, libraries, and tooling.

## Baseline Decisions

- `.slnx` solution format.
- Current stable .NET project files with latest C# language selection and explicit usings.
- WPF shell with editable game settings, profile startup, optional target launch, foreground receiver gate, Steam config forcing, and output preview.
- Launch mode (`--launch`) starts the configured target and keeps the bridge UI hidden behind a tray icon. Do not add overlay-host workarounds back without a new validated reason. The current tray behavior is per bridge process; do not add a shared tray host or cross-process instance list without an explicit IPC decision.
- If the bridge starts a target process, it owns that process lifetime. Steam stopping the bridge should close the launched process tree, and the bridge should exit when the launched receiver exits.
- In normal interactive mode, closing the status window exits the app. In `--launch` mode, closing the status window hides it back to the tray. Launch-mode instances exit through the tray Exit command, launched receiver exit, or Steam/process termination.
- Multiple instances may save `appsettings.json`; writes must use the settings-store mutex and atomic write path so profile saves merge with the latest file contents.
- Output is always previewed in the app model. Future Teensy transport must only run while a configured receiver process is the foreground process. Do not add manual output-destination modes for v1.
- Steam ROM Manager export should generate bridge-targeted shortcuts, not game-targeted shortcuts. Each generated entry should launch the bridge with `--profile <id> --launch`; the selected profile then launches the configured game executable.
- General app settings live under the `general` JSON object. It contains `theme` (`system`, `light`, or `dark`) and `srmManifestPath`, which defaults under `%LOCALAPPDATA%\SteamHidBridge\srm\games.json`. Theme selection must use WPF's built-in Fluent `ThemeMode` API, not custom control templates. Do not mutate SRM's own parser configuration unless explicitly requested.
- Do not inject a synthetic `default` profile. If no profile is requested, select an existing profile; create a local `new-game` entry only when there are no profiles loaded.
- Publish output should be self-contained single-file for the selected Windows runtime unless the user asks for framework-dependent deployment. Current publish packaging includes the virtual mouse driver package and driver install script when driver tooling is available.
- Release tags use `vMAJOR.MINOR.PATCH`, for example `v0.1.1`. Tag pushes matching that shape build, test, package, and create a GitHub Release with `SteamHidBridge-win-x64.zip`.
- The installer script lives at `scripts/install.ps1`, downloads from GitHub Releases, replaces the install folder, and creates a Desktop shortcut. User data must live outside the install folder.
- App updates use GitHub Releases. The app checks the latest release, asks for confirmation, downloads the versioned `SteamHidBridge-update.ps1` release asset, closes all bridge instances, and replaces the published app folder. Do not bundle updater logic in the app package, and do not make the running process overwrite its own executable directly.
- Runtime settings and logs belong under `%LOCALAPPDATA%\SteamHidBridge\`. Keep app lifecycle/error logging in `logs/app.log` and updater wrapper output in `logs/update.log`; do not add new ad hoc log files without a documented need.
- Startup may refresh app-owned derived files: normalize/write `appsettings.json` through the settings store and regenerate the configured SRM manifest. Startup must not mutate Steam caches, SRM parser config, controller layouts, or Steam shortcut databases.
- Steam Input config forcing uses Steam's official `steam://forceinputappid/<appid>` URL only while the configured receiver is foreground, and resets with `steam://forceinputappid/0` when foreground is lost or the bridge exits.
- Active MVP input uses Steam legacy mouse output observed through Windows Raw Input. Do not ship `steam_appid.txt`, write `game_actions_<appid>.vdf`, scan Steam shortcut databases, or add Steamworks.NET/native Steam Input API dependencies unless the user explicitly reopens native Steam Input with a documented reason.
- Windows Raw Input is the active input emitter for Steam legacy mouse output. Mouse frames flow through `BridgeRuntime` to the GUI preview at a throttled display rate and to output consumers per input event after foreground receiver gating passes.
- Shared C# protocol library for frame encoding, validation, and the HID input payload.
- MSTest protocol tests for synthetic input and malformed-frame handling.
- PlatformIO firmware placeholder for Teensy 4.0.
- Scripts expose a small top-level command surface: `scripts/check.ps1`, `scripts/publish.ps1`, `scripts/release.ps1`, and `scripts/install.ps1`. Helper scripts live under `scripts/internal/`. Do not add new public scripts without a strong workflow reason.
- `scripts/release.ps1` is the local release gate. It prompts for the next version after printing the latest tag, runs formatting/build/test/package checks, requires a clean working tree, then creates and pushes the version tag that triggers the GitHub Release workflow.

## Steam Input Research Order

Validate Steam shortcut/runtime behavior before building more UI:

1. Create a Steam-launched spike executable.
2. Add it to Steam as a non-Steam shortcut.
3. Confirm Steam keeps the shortcut in the Running state while the bridge process is alive.
4. Confirm Steam's Stop button terminates or requests termination of the bridge process.
5. Confirm multiple Steam shortcuts can point to the same executable while retaining distinct Steam Input configurations.
6. Confirm launch arguments identify the intended profile.
7. Confirm only one bridge instance owns the Teensy device at a time.
8. Confirm Steam legacy mouse output is observable through Windows Raw Input while the bridge shortcut config is forced.
9. Confirm Steam Controller, DualSense, or other gyro-capable controllers can be configured through Steam legacy mouse bindings and produce usable mouse deltas.
10. Forward those deltas to the Teensy and verify HID mouse output.

Do not implement native Steam Input action manifests, Steam config editing, Steam VDF rewriting, or automatic Steam layout import/export in v1. Those are future research items.

## Repository Layout

```text
firmware/
driver/
app/
app/SteamHidBridge.App/Runtime/
protocol/
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
