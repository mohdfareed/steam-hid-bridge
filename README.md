# Steam HID Bridge

Steam HID Bridge is a small Steam-launched Windows app plus a future Teensy HID firmware path. Steam Input remains the controller configuration layer; this project handles profile launch state, foreground gating, and eventually keyboard/mouse HID emission.

Current MVP status: the Windows tray app works for profile launch, target process lifetime, settings editing, output preview, and Steam ROM Manager export. Real Steam Input reading and Teensy output are the next implementation steps.

## Layout

```text
app/        Windows bridge application
protocol/   Host-device frame and HID report payloads
firmware/   Teensy 4.0 placeholder firmware
tests/      Protocol tests
scripts/    Build, test, publish, release helpers
```

## Build

Prerequisites:

- Latest stable .NET SDK that supports the target framework.
- PlatformIO CLI for firmware work.

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\publish.ps1
```

`publish.ps1` creates a self-contained Windows build under `artifacts/SteamHidBridge-win-x64`.
`package-release.ps1` creates the release zip at `artifacts/SteamHidBridge-win-x64.zip`.
`deploy.ps1` runs the release checks, creates a version tag, and pushes it to trigger the release workflow.

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
%LOCALAPPDATA%\SteamHidBridge\logs\app.log
%LOCALAPPDATA%\SteamHidBridge\logs\update.log
```

Run the app directly:

```powershell
dotnet run --project .\app\SteamHidBridge.App -- --profile game-profile
dotnet run --project .\app\SteamHidBridge.App -- --profile game-profile --launch
```

Firmware build, once PlatformIO is installed:

```powershell
pio run -d .\firmware
```

## Steam Use

1. Install or publish the Windows app.
2. Start the app from the Desktop shortcut.
3. Create game profiles.
4. Copy the Steam ROM Manager JSON from the app and import it into Steam ROM Manager.
5. Launch a generated Steam shortcut. The shortcut passes `--profile <id> --launch`.

## Releases

Run the deploy script from a clean working tree:

```powershell
.\scripts\deploy.ps1
```

It prints the latest version tag, prompts for the next version, formats the solution, verifies the tree is still clean, builds, tests, packages, then creates and pushes a `vMAJOR.MINOR.PATCH` tag.

The tag push triggers the release workflow, which builds, tests, packages `SteamHidBridge-win-x64.zip`, and attaches it to the GitHub Release.
