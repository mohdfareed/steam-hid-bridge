# Scripts

Scripts are grouped by audience and lifecycle stage.

## dev

Local development checks.

- `dev/build.ps1` builds the app/protocol projects and verifies formatting.
- `dev/test.ps1` runs protocol tests.
- `dev/publish.ps1` publishes the self-contained app for local testing and includes the driver package unless `-SkipDriver` is passed.

## driver

Virtual HID driver build/install helpers.

- `driver/build.ps1` builds the KMDF/VHF driver and test sender with Visual Studio MSBuild.
- `driver/install.ps1` installs the packaged or locally built driver from elevated PowerShell.

## release

Release creation and packaging.

- `release/package.ps1` creates `artifacts/SteamHidBridge-win-x64.zip`.
- `release/deploy.ps1` runs the local release gate and pushes the version tag.

## install

Scripts meant to be downloaded or run by installed app/update flows.

- `install/app.ps1` installs the latest GitHub Release locally.
- `install/update.ps1` replaces an existing installation during app update.
