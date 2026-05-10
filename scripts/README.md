# Scripts

Use the top-level scripts:

- `check.ps1` verifies formatting, builds the app/protocol projects, runs tests, builds the virtual driver, and builds the Teensy firmware.
- `publish.ps1` creates a local self-contained single-file app folder and includes driver plus firmware artifacts.
- `release.ps1` packages or tags a release.
- `install.ps1` installs the latest GitHub Release.

`internal/` contains implementation helpers used by those entrypoints and by the packaged app. Do not call internal scripts directly unless debugging deployment.

The virtual driver package and Teensy firmware are part of the default path. The app decides at runtime which output mode is active.
