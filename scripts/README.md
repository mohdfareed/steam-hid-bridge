# Scripts

Use the top-level scripts:

- `check.ps1` verifies formatting, builds the app/protocol projects, and runs tests.
- `publish.ps1` creates a local self-contained single-file app folder and includes the virtual mouse driver package by default.
- `release.ps1` packages or tags a release.
- `install.ps1` installs the latest GitHub Release.

`internal/` contains implementation helpers used by those entrypoints and by the packaged app. Do not call internal scripts directly unless debugging deployment.
