# Windows App

The Windows app is the Steam-launched bridge host. It keeps Steam focused on the bridge shortcut, stores per-game launch metadata, observes Steam input, previews normalized output, and forwards gated mouse frames to the selected output mode.

## Current Behavior

- `--profile <id>` selects a profile from `%LOCALAPPDATA%\SteamHidBridge\appsettings.json`.
- `--launch` starts that profile's configured executable, hides the main window, and leaves a tray icon for that bridge instance.
- Normal interactive windows exit on close. Launch-mode windows hide to tray on close.
- Exiting the bridge stops any process tree it launched. If a configured receiver process appears and later exits, the bridge exits.
- Multiple bridge instances are allowed. Each instance has its own tray icon and selected profile.
- Settings saves use a named mutex and atomic replace so concurrent profile edits merge with the latest `appsettings.json`.
- The UI can copy Steam ROM Manager JSON for all profiles. Generated entries target the bridge executable and pass `--profile <id> --launch`.
- Published builds are self-contained for the selected Windows runtime.
- User data lives under `%LOCALAPPDATA%\SteamHidBridge`, outside the install folder.
- The UI is organized into native WPF tabs for profile editing, general settings, output preview, and diagnostics.
- The Settings tab stores input mode, output mode, Teensy serial port, Steam ROM Manager manifest path, app data access, driver install status, and release update actions.

## Folders

```text
SteamHidBridge.App/
  Ui/               WPF views and binding view models only
  Core/             input frames, runtime loop, foreground gate, output routing
  Configuration/    appsettings schema and Steam ROM Manager export
  Platform/         OS, Steam, tray, logging, startup, and theme adapters
  Update/           GitHub Release check and update handoff
```

## Profile Schema

```json
{
  "general": {
    "theme": "system",
    "inputMode": "legacyMouse",
    "outputMode": "teensy",
    "teensyPort": "auto",
    "srmManifestPath": "%LOCALAPPDATA%\\SteamHidBridge\\srm\\games.json"
  },
  "games": {
    "game-profile": {
      "title": "Game Profile",
      "executable": "C:\\Games\\Example\\ExampleLauncher.exe",
      "arguments": "--launch-example",
      "workingDirectory": "C:\\Games\\Example",
      "receiverProcesses": [ "ExampleGame.exe" ]
    }
  }
}
```

The app stores profiles at `%LOCALAPPDATA%\SteamHidBridge\appsettings.json`.

`inputMode` is currently `legacyMouse` or `steamInputActions`. Legacy mouse is active. Steam Input actions are selectable in settings, but the native action reader still needs the app id/action manifest path repaired before it can emit frames.

`outputMode` is `visualizeOnly`, `teensy`, or `virtualMouseDriver`. The driver mode uses the packaged KMDF/VHF driver device interface when installed. Driver installation is disabled in the UI until the signing/test-mode path is resolved.

`teensyPort` may be a COM port such as `COM7`, or `auto` to try available serial ports.

The General section writes a Steam ROM Manager manifest JSON file for all profiles. By default this is `%LOCALAPPDATA%\SteamHidBridge\srm\games.json`, but the path is configurable in app settings so SRM configuration can live in a separate dotfiles or cloud-synced setup.

## Steam Notes

When a configured receiver owns the foreground window, the app requests Steam config forcing with `steam://forceinputappid/<appid>`. It resets with `steam://forceinputappid/0` when foreground is lost or the bridge exits. Steam normally provides the app id in the launch environment.

Launch mode keeps the main window hidden behind the tray icon. Do not put Steam layout editing, VDF rewriting, automatic layout import/export, or overlay-host workarounds in v1.

Input currently observes legacy mouse output through Windows Raw Input. See [Platform/Steam/README.md](SteamHidBridge.App/Platform/Steam/README.md) for the active Steam integration notes.
