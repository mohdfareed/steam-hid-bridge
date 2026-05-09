# Steam Shortcut Profile Mode

The bridge app should be launched by Steam, not the target game. Steam ROM Manager can create one Steam shortcut per target profile, all pointing to the same bridge executable with different arguments.

```text
SteamHidBridge.App.exe --profile valorant --launch
SteamHidBridge.App.exe --profile league-of-legends --launch
SteamHidBridge.App.exe --profile cs2 --launch
```

Each Steam shortcut owns its own Steam-side controller configuration. The game profile is separate app metadata: executable, arguments, working directory, and receiver process names.

## Current Profile Schema

```json
{
  "games": {
    "valorant": {
      "executable": "C:\\Riot Games\\Riot Client\\RiotClientServices.exe",
      "arguments": "--launch-product=valorant --launch-patchline=live",
      "workingDirectory": "C:\\Riot Games\\Riot Client",
      "receiverProcesses": [ "VALORANT-Win64-Shipping.exe" ]
    }
  }
}
```

Place an `appsettings.json` next to the published executable. The app includes `appsettings.example.json` as a starting point, and the UI can save game entries back to `appsettings.json`.

In `--launch` mode the bridge starts the configured executable, keeps its own status UI hidden, and opens a transparent fullscreen overlay host window for Steam. Use the tray icon for that bridge process to open the status UI. The bridge always previews output in its UI model, but forwarding is automatic and only allowed while a configured receiver process is foreground.

The bridge owns the lifetime of the process tree it starts. Steam Stop should stop the bridge, and bridge exit should stop the launched target process tree. If a configured receiver process has appeared and later exits, the bridge exits too.

Closing the bridge status window hides it back to the tray. Use tray Exit when you intentionally want to close the bridge and any launched target process tree.

## Steam ROM Manager Shape

Use one parser entry per profile or one parser template expanded over profile data. The exact final JSON depends on the published executable path, but the important fields are:

```json
{
  "title": "Steam HID Bridge - Valorant",
  "target": "C:\\Path\\To\\SteamHidBridge.App.exe",
  "startIn": "C:\\Path\\To",
  "launchOptions": "--profile valorant --launch"
}
```

## Do Not Do In v1

- Do not require target games to be added to Steam.
- Do not launch target games through Steam. If the bridge launches the target executable, it should do so as a normal child process while the bridge remains the Steam-tracked process.
- Do not depend on Steam Desktop Configuration.
- Do not rewrite Steam VDF files.
- Do not import or export Steam layouts automatically.
- Do not add global single-instance locking. Multiple Steam shortcuts may map to multiple bridge processes.
- Do not add manual output destination modes. Foreground receiver gating decides whether output is forwarded.

## Native Controller/Gyro Gap

The v1 bridge emits keyboard and mouse HID only. Games that require native controller or native DualSense gyro input are outside the current MVP unless we deliberately expand firmware and protocol scope to emulate a gamepad-class USB device. For now, gyro should be configured in Steam Input as mouse-like `absolute_mouse` data and forwarded as HID mouse movement.

## Research Notes

Steam's official docs confirm that the Steam Input Configurator sits between the controller and the running application, and that legacy mode can map controller input to keyboard, mouse, or gamepad-style output. The native Steam Input API path is action-based and can report `absolute_mouse` analog deltas, but official initialization docs assume Steam can identify an AppID. That makes native API behavior for a plain non-Steam shortcut the first spike to validate.

The overlay docs say the overlay hooks applications launched through Steam. In this project shape, the bridge is the Steam-launched process. Do not assume the overlay will attach to the separately launched target process unless the spike proves Steam treats that launch relationship as supported.

SISR/GlosSI-like behavior does not make the target game process become Steam's process. SISR launches a Steam-tracked overlay/window host and redirects Steam Input to system-level emulated devices. Its docs recommend a fullscreen transparent window (`-w -f`) for per-shortcut configurations, and continuous drawing (`--wcd true`) when overlay/touch/radial menus fail.

References:

- Steam Input general concepts: https://partner.steamgames.com/doc/features/steam_controller/concepts
- Steam Input developer guide: https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs
- ISteamInput API: https://partner.steamgames.com/doc/api/isteaminput
- Steamworks API initialization: https://partner.steamgames.com/doc/sdk/api
- Steam overlay behavior: https://partner.steamgames.com/doc/features/overlay
- SISR GlosSI-like shortcut pattern: https://alia5.github.io/SISR/stable/guides/glossi_like/
