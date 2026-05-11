# Steam HID Bridge

Steam HID Bridge is a small Windows app that wraps non-Steam games, enhancing support of Steam Input.
It allows users to manage non-Steam games using Steam Input configurations, forwarding non-supported events to the game.

Steam Input remains the configuration layer; this project handles profile launch state, foreground gating, and mouse HID emission.

## Features

- JSON-based profiles in `%LOCALAPPDATA%\SteamHidBridge\appsettings.json`
- Steam ROM Manager manifest export for bridge-targeted shortcuts
- Foreground gating so output is only forwarded when the configured receiver process is active
- Optional game launch ownership so Steam start/stop tracks the bridge while the bridge manages the launched game
- Serial mouse forwarding to the board firmware bundled with the app

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
   - Manifests directory: Paste the directory that contains the exported manifest file
4. Parse the entries in SRM and restart Steam.
6. Launch the generated Steam shortcut for the profile you want.

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
