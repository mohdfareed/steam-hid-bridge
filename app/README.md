# Windows App

The Windows app is the Steam-launched bridge host. It keeps Steam focused on the bridge shortcut, stores per-game launch metadata, reads mouse actions from Steam Input, and previews normalized output while the Teensy transport is still pending.

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
- The App section can check GitHub Releases and start an in-place update. Updates close all bridge instances and replace the published app folder.

## Folders

```text
SteamHidBridge.App/
  Views/            WPF presentation
  ViewModels/       profile editing, launch lifetime, foreground gate, output preview
  Profiles/         appsettings schema and Steam ROM Manager export
  Input/            mouse emitter/consumer pipeline
  Steam/            Steam Input emitter and config forcing
  Windows/          Win32 foreground/process helpers
  Startup/          command-line parsing
  Updates/          GitHub Release check and update handoff
  Infrastructure/   tray icon, logging, commands
```

## Profile Schema

```json
{
  "games": {
    "valorant": {
      "title": "Valorant",
      "executable": "C:\\Riot Games\\Riot Client\\RiotClientServices.exe",
      "arguments": "--launch-product=valorant --launch-patchline=live",
      "workingDirectory": "C:\\Riot Games\\Riot Client",
      "receiverProcesses": [ "VALORANT-Win64-Shipping.exe" ]
    }
  }
}
```

The app stores profiles at `%LOCALAPPDATA%\SteamHidBridge\appsettings.json`.

The Steam ROM Manager section writes a manifest JSON file for all profiles. By default this is `%LOCALAPPDATA%\SteamHidBridge\srm\games.json`, but the path is configurable in app settings so SRM configuration can live in a separate dotfiles or cloud-synced setup.

## Steam Notes

When a configured receiver owns the foreground window, the app requests Steam config forcing with `steam://forceinputappid/<appid>`. It resets with `steam://forceinputappid/0` when foreground is lost or the bridge exits. Steam normally provides the app id in the launch environment; `--steam-app-id <id>` is available as a fallback.

The transparent overlay host window exists only in `--launch` mode so Steam has a bridge-owned window while the target game is foreground. Do not put Steam layout editing, VDF rewriting, or automatic layout import/export in v1.

Steam Input currently reads only mouse actions. See [Steam/README.md](Steam/README.md) for the action names and in-game actions file.
