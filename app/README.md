# Windows App

The Windows app is the Steam-launched host side of the bridge. Steam shortcuts pass `--profile <id>` to select the game profile while Steam Input stays responsible for controller layout, gyro tuning, and action configuration.

## Architecture

Keep WPF replaceable. Views should contain presentation only and bind to view models. Protocol, Steam Input, transport, HID report mapping, diagnostics, foreground gating, and startup configuration belong behind services or view models that can be tested without WPF controls.

Current layout:

```text
SteamHidBridge.App/
  Views/            WPF windows and view code-behind
  ViewModels/       bindable app state and commands
  Profiles/         bridge profile schema and loading
  Steam/            Steam Input API boundary
  Transport/        current loopback transport
  Windows/          foreground-window and Win32 boundaries
  Startup/          command-line handling
  Infrastructure/   small reusable app plumbing
```

The current transport is a loopback simulator so host-side UI and protocol validation can continue before hardware arrives. It is not a latency measurement path. The current Steam Input service is a status stub until the non-Steam shortcut/native API spike is complete.

The app always previews the latest output in the UI model. It only forwards output when one of the configured receiver process names owns the foreground window.

`--launch` starts the configured target executable, keeps the bridge UI hidden, and shows a transparent fullscreen overlay host window for Steam. The tray icon for that bridge process can reopen the UI or exit the bridge. A single shared tray icon that lists all bridge processes would require local IPC and is intentionally not in the MVP yet.

Closing the status window hides it back to the tray. Exit from the tray closes the bridge and any launched target process tree.

The bridge owns any process tree it starts. On bridge exit, including Steam Stop when Windows gives us a normal process cleanup path, the launched process tree is stopped. A Windows Job object is also used so hard bridge termination closes assigned child processes when possible.

The app reads and writes `appsettings.json` next to the executable. If it does not exist, the UI starts with a default editable entry. `appsettings.example.json` is copied to the publish output as a reference.

Multiple bridge instances can save settings. Saves use a named mutex, reload the latest file, merge the saved profile, and atomically replace `appsettings.json`.

Each game entry is intentionally small: title, executable, arguments, working directory, and receiver process names. The title is only for generated Steam shortcuts; the profile id remains the launch identity. The receiver process list is what prevents the bridge from forwarding when a different app is foreground.

The UI can copy a Steam ROM Manager manual-parser JSON array for all configured profiles. Those generated entries target the bridge executable and pass `--profile <id> --launch`; the bridge then launches the configured game executable from `appsettings.json`.

## Build

```powershell
dotnet run --project .\app\SteamHidBridge.App -- --profile valorant --launch
dotnet publish .\app\SteamHidBridge.App --configuration Release --runtime win-x64 --self-contained false
```
