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
* Forwards mouse events as a virtual HID device, adding Steam Input support to games that only support raw mouse input.
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

Driver install requires an elevated PowerShell session after publish (can also be installed from within the app):

```powershell
.\artifacts\SteamHidBridge-win-x64\driver\install.ps1 -EnableTestSigning -Sign
```

Reboot after enabling test signing.

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
firmware/   Teensy 4.0 placeholder firmware
driver/     VHF/KMDF virtual HID software-output spike
tests/      Protocol tests
scripts/    Build, test, publish, release helpers
```

## Build

Prerequisites:

- Latest stable .NET SDK that supports the target framework.
- PlatformIO CLI for firmware work.
- Visual Studio with C++ workload for the virtual HID driver.

```powershell
.\scripts\check.ps1
.\scripts\publish.ps1
```

`publish.ps1` creates a self-contained single-file Windows app under `artifacts/SteamHidBridge-win-x64` and copies the virtual mouse driver package into `artifacts/SteamHidBridge-win-x64/driver`.
`release.ps1 -PackageOnly` creates the release zip at `artifacts/SteamHidBridge-win-x64.zip`.
`release.ps1` without `-PackageOnly` runs the release checks, creates a version tag, and pushes it to trigger the release workflow.

Firmware build, once PlatformIO is installed:

```powershell
pio run -d .\firmware
```

## Releases

Run the release script from a clean working tree:

```powershell
.\scripts\release.ps1
```

It prints the latest version tag, prompts for the next version, formats the solution, verifies the tree is still clean, builds, tests, packages, then creates and pushes a `vMAJOR.MINOR.PATCH` tag.
The tag push triggers the release workflow, which builds, tests, packages `SteamHidBridge-win-x64.zip`, and attaches it to the GitHub Release.
