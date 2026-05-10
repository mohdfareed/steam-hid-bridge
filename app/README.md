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
- The Profile tab stores each game's input mode, output mode, launch data, and receiver processes. The Settings tab stores app-wide theme, board serial port, Steam ROM Manager manifest path, app data access, firmware update, and release update actions.
- Startup does not rewrite settings, SRM manifests, or Steam Input files. Those changes happen only on explicit save/export/apply actions.

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
    "boardPort": "auto",
    "srmManifestPath": "%LOCALAPPDATA%\\SteamHidBridge\\srm\\games.json"
  },
  "games": {
    "game-profile": {
      "title": "Game Profile",
      "executable": "C:\\Games\\Example\\ExampleLauncher.exe",
      "arguments": "--launch-example",
      "workingDirectory": "C:\\Games\\Example",
      "inputMode": "legacyMouse",
      "outputMode": "board",
      "receiverProcesses": [ "ExampleGame.exe" ]
    }
  }
}
```

The app stores profiles at `%LOCALAPPDATA%\SteamHidBridge\appsettings.json`.

Each profile has an `inputMode` of `legacyMouse` or `steamInputActions`. The UI labels these as `Virtual Mouse` and `Steam Input`. `legacyMouse` observes Steam's virtual mouse output through Raw Input. `steamInputActions` polls the app's bundled Steam Input action manifest through Steamworks.NET; Steam binds those actions through the shortcut's controller layout UI when the app is launched from Steam.

Each profile has an `outputMode` of `none`, `board`, or `virtualMouse`. The driver mode uses the packaged KMDF/VHF driver device interface when installed through the packaged driver script.

`boardPort` is a COM port number such as `7`, or `auto` to try available serial ports. In the UI, leave the field empty for auto.

The General section writes a Steam ROM Manager manifest JSON file for all profiles when you save settings or use `Export SRM`. By default this is `%LOCALAPPDATA%\SteamHidBridge\srm\games.json`, but the path is configurable in app settings so SRM configuration can live in a separate dotfiles or cloud-synced setup.

## Steam Notes

When a configured receiver owns the foreground window, the app requests Steam config forcing with `steam://forceinputappid/<appid>`. It resets with `steam://forceinputappid/0` when foreground is lost or the bridge exits. The app id is only used for this config-forcing path.

Launch mode keeps the main window hidden behind the tray icon. Do not put Steam layout editing, VDF rewriting, automatic layout import/export, or overlay-host workarounds in v1.

Input currently observes legacy mouse output through Windows Raw Input. See [Platform/Steam/README.md](SteamHidBridge.App/Platform/Steam/README.md) for the active Steam integration notes.
