# Windows App

The Windows app is the Steam-launched bridge host. It keeps Steam focused on the bridge shortcut, stores per-game launch metadata, and previews normalized keyboard/mouse output while the real Steam Input reader and Teensy transport are still pending.

## Current Behavior

- `--profile <id>` selects a profile from `appsettings.json`.
- `--launch` starts that profile's configured executable, hides the main window, and leaves a tray icon for that bridge instance.
- Normal interactive windows exit on close. Launch-mode windows hide to tray on close.
- Exiting the bridge stops any process tree it launched. If a configured receiver process appears and later exits, the bridge exits.
- Multiple bridge instances are allowed. Each instance has its own tray icon and selected profile.
- Settings saves use a named mutex and atomic replace so concurrent profile edits merge with the latest `appsettings.json`.
- The UI can copy Steam ROM Manager JSON for all profiles. Generated entries target the bridge executable and pass `--profile <id> --launch`.
- Published builds are self-contained for the selected Windows runtime.
- The release installer creates `appsettings.json` from `appsettings.example.json` only when the user does not already have settings.
- The App section can check GitHub Releases and start an in-place update. Updates preserve `appsettings.json`, close all bridge instances, and replace the published app folder.

## Folders

```text
SteamHidBridge.App/
  Views/            WPF presentation
  ViewModels/       profile editing, launch lifetime, foreground gate, output preview
  Profiles/         appsettings schema and Steam ROM Manager export
  Steam/            Steam Input boundary and config forcing
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

Place `appsettings.json` next to the published executable. `appsettings.example.json` is copied to publish output as a reference.

## Steam Notes

When a configured receiver owns the foreground window, the app requests Steam config forcing with `steam://forceinputappid/<appid>`. It resets with `steam://forceinputappid/0` when foreground is lost or the bridge exits. Steam normally provides the app id in the launch environment; `--steam-app-id <id>` is available as a fallback.

The transparent overlay host window exists only in `--launch` mode so Steam has a bridge-owned window while the target game is foreground. Do not put Steam layout editing, VDF rewriting, or automatic layout import/export in v1.
