# Windows App

The Windows app is the Steam-launched host side of the bridge. Steam shortcuts pass `--profile <id>` to select the bridge profile while Steam Input stays responsible for controller layout, gyro tuning, and action configuration.

## Architecture

Keep WPF replaceable. Views should contain presentation only and bind to view models. Protocol, Steam Input, transport, HID report mapping, diagnostics, foreground gating, and startup configuration belong behind services or view models that can be tested without WPF controls.

Current layout:

```text
SteamHidBridge.App/
  Views/            WPF windows and view code-behind
  ViewModels/       bindable app state and commands
  Profiles/         bridge profile schema and loading
  Input/            Steam Input boundary
  Transport/        host transport interfaces and implementations
  Windows/          foreground-window and Win32 boundaries
  Startup/          command-line and single-instance handling
  Infrastructure/   small reusable app plumbing
```

The current transport is a loopback simulator so host-side UI and protocol validation can continue before hardware arrives. It is not a latency measurement path. The current Steam Input service is a status stub until the non-Steam shortcut/native API spike is complete.

## Build

```powershell
dotnet run --project .\app\SteamHidBridge.App -- --profile valorant
dotnet publish .\app\SteamHidBridge.App --configuration Release --runtime win-x64 --self-contained false
```

Autostart should be added as an opt-in per-user setting, visible in the UI, and implemented outside the views so it can be changed or removed without redesigning the UI.
