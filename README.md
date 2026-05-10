# Steam HID Bridge

Steam HID Bridge is a small Windows app that wraps non-Steam games, enhancing support of Steam Input.
It allows users to manage non-Steam games using Steam Input configurations, forwarding non-supported events to the game.

Steam Input remains the configuration layer; this project handles profile launch state, foreground gating, and keyboard/mouse HID emission.

## Features

* Profiles are JSON-based, allowing external management and version control.
* Integrates with Steam Rom Manager for the management of non-Steam game shortcuts.
  * The app exports a manifest file of the defined profiles, which is consumed by a manual parser created in SRM.
* Games define separate launched and receiving process names, allowing:
  * Accurate launch state tracking allows Steam to reliably start and stop games.
  * Foreground gating such that input is only sent when the game is active.
  * Steam Input configurations to reliably activate when the game is active.
* Reads mouse events from either Steam's virtual mouse output or Steam Input game actions.
* Forwards mouse events to a selected output mode: none, physical board HID, or the packaged virtual mouse driver.
* Consistent shortcuts allows for Steam Cloud sync of Steam Input configurations (*undocumented/unreliable*).

## Install

The release installer downloads the latest GitHub Release, asks for an install folder, replaces that folder with the self-contained app, and adds a Desktop shortcut.

```powershell
irm https://raw.githubusercontent.com/mohdfareed/steam-hid-bridge/main/scripts/install.ps1 | iex
```

Default install location:

```text
%LOCALAPPDATA%\Programs\SteamHidBridge
```

The app can check GitHub Releases for updates from its App section. Updating closes all bridge instances and replaces the self-contained app folder with the latest release package.

User data is stored outside the install folder:

```text
%LOCALAPPDATA%\SteamHidBridge\appsettings.json
%LOCALAPPDATA%\SteamHidBridge\logs\*.log
```

Run the app directly:

```powershell
dotnet run --project .\app\SteamHidBridge.App -- --profile game-profile
dotnet run --project .\app\SteamHidBridge.App -- --profile game-profile --launch
```

## Steam Use

1. Install or publish the Windows app.
2. Start the app from the Desktop shortcut.
3. Create game profiles and copy the manifest path to the clipboard.
4. Create a Steam Rom Manager manual parser, providing the app's manifest path.
   * Parser type: `Manual`
   * Steam directory: `${steamdirglobal}`
   * Manifests directory: Paste from the app's settings (e.g. `~\AppData\Local\SteamHidBridge\srm`)
5. Launch a generated Steam shortcut.

## Layout

```text
app/        Windows bridge application
protocol/   Host-device frame and HID report payloads
firmware/   Board firmware; current target is Teensy 4.0
driver/     VHF/KMDF virtual mouse driver package
tests/      Protocol tests
scripts/    Build, check, publish, release, install helpers
```

## Build

Prerequisites:

- Latest stable .NET SDK that supports the target framework.
- Visual Studio C++ and WDK components for the virtual driver.
- PlatformIO CLI for firmware build/upload. The VS Code PlatformIO extension works after it has created its local CLI environment.

```powershell
.\scripts\check.ps1
.\scripts\publish.ps1
```

`check.ps1` verifies formatting, builds the app/protocol projects, runs tests, builds the virtual driver, and builds the board firmware.
`publish.ps1` creates a self-contained single-file Windows app under `artifacts/SteamHidBridge-win-x64` and includes `Driver`, `Firmware`, and `Steam` artifacts.
`release.ps1` creates the release zip at `artifacts/SteamHidBridge-win-x64.zip`.
`release.ps1 -TagRelease` runs the release checks, creates a version tag, and pushes it to trigger the release workflow.

Firmware build/upload:

```powershell
pio run -d .\firmware\SteamHidBridge.Firmware
pio run -d .\firmware\SteamHidBridge.Firmware -t upload
```

## Releases

Run the release script from a clean working tree:

```powershell
.\scripts\release.ps1
```

It prints the latest version tag, prompts for the next version, formats the solution, verifies the tree is still clean, builds, tests, packages, then creates and pushes a `vMAJOR.MINOR.PATCH` tag.
The tag push triggers the release workflow, which builds, tests, packages `SteamHidBridge-win-x64.zip`, and attaches it to the GitHub Release.
