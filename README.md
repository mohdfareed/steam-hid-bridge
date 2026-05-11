# Steam HID Bridge

Steam HID Bridge is a small Windows app that wraps non-Steam games, enhancing support of Steam Input.
It allows users to manage non-Steam games using Steam Input configurations, forwarding non-supported events to the game.

Steam Input remains the configuration layer; this project handles profile launch state, foreground gating, and mouse HID emission.

## Features

- Profiles are JSON-based, allowing external management and version control.
- Integrates with Steam Rom Manager for the management of non-Steam game shortcuts.
  - The app exports a manifest file of the defined profiles, which is consumed by a manual parser created in SRM.
- Games define separate launched and receiving process names, allowing:
  - Accurate launch state tracking allows Steam to reliably start and stop games.
  - Foreground gating such that input is only sent when the game is active.
  - Steam Input configurations to reliably activate when the game is active.
- Reads mouse events from either Steam's virtual mouse output or Steam Input game actions.
- Forwards mouse events to a physical board over serial, emulating a physical mouse HID device.
- Consistent shortcuts allows for Steam Cloud sync of Steam Input configurations (*undocumented/unreliable*).

## Install

```powershell
irm https://raw.githubusercontent.com/mohdfareed/steam-hid-bridge/main/scripts/install.ps1 | iex
```

Default install folder:

```text
%LOCALAPPDATA%\Programs\SteamHidBridge
```

User data lives outside the install folder:

```text
%LOCALAPPDATA%\SteamHidBridge\appsettings.json
%LOCALAPPDATA%\SteamHidBridge\logs\*.log
```

## Steam setup

1. Start the app and create profiles.
2. Save the profiles.
3. Point a Steam ROM Manager manual parser at the SRM manifest path shown in the app.
  - Parser type: `Manual`
  - Steam directory: `${steamdirglobal}`
  - Manifests directory: Paste from the app's settings (e.g. `~\AppData\Local\SteamHidBridge\srm`)
5. Parse the entries in SRM.
6. Restart Steam after changing Steam Input action definitions.
7. Launch the generated Steam shortcut for the profile you want.
  - If a game is using Steam Input mode, the first launch registers the mouse game actions.
  - Restarting the game will display the game actions in Steam Input's configuration screen.

## Build

Prerequisites:

- Current stable .NET SDK
- PlatformIO CLI

```powershell
.\scripts\check.ps1
.\scripts\publish.ps1
```

- `check.ps1` formats, builds the app, and builds the firmware.
- `publish.ps1` creates `artifacts/SteamHidBridge-win-x64`.
- `release.ps1` packages `artifacts/SteamHidBridge-win-x64.zip`.
- `release.ps1 -TagRelease` runs the local release gate and pushes a `vMAJOR.MINOR.PATCH` tag.
