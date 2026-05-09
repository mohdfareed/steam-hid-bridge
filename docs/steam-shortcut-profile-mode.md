# Steam Shortcut Profile Mode

The bridge app should be launched by Steam, not the target game. Steam ROM Manager can create one Steam shortcut per target profile, all pointing to the same bridge executable with different arguments.

```text
SteamHidBridge.exe --profile valorant
SteamHidBridge.exe --profile league-of-legends
SteamHidBridge.exe --profile cs2
```

Each Steam shortcut owns its own Steam-side controller configuration. The bridge profile is separate app metadata: target process names, forwarding gate behavior, device selection, and optional auto-exit rules.

## Current Profile Schema

```json
{
  "profiles": [
    {
      "id": "valorant",
      "displayName": "Valorant",
      "targetProcesses": [ "VALORANT-Win64-Shipping.exe" ],
      "autoEnableWhenForeground": true,
      "autoExitWhenTargetExits": false
    }
  ]
}
```

Place a `profiles.json` next to the published executable. The app includes `profiles.example.json` as a starting point.

## Steam ROM Manager Shape

Use one parser entry per profile or one parser template expanded over profile data. The exact final JSON depends on the published executable path, but the important fields are:

```json
{
  "title": "Steam HID Bridge - Valorant",
  "target": "C:\\Path\\To\\SteamHidBridge.App.exe",
  "startIn": "C:\\Path\\To",
  "launchOptions": "--profile valorant"
}
```

## Do Not Do In v1

- Do not require target games to be added to Steam.
- Do not launch target games through Steam.
- Do not depend on Steam Desktop Configuration.
- Do not rewrite Steam VDF files.
- Do not import or export Steam layouts automatically.

## Native Controller/Gyro Gap

The v1 bridge emits keyboard and mouse HID only. Games that require native controller or native DualSense gyro input are outside the current MVP unless we deliberately expand firmware and protocol scope to emulate a gamepad-class USB device. For now, gyro should be configured in Steam Input as mouse-like `absolute_mouse` data and forwarded as HID mouse movement.

## Research Notes

Steam's official docs confirm that the Steam Input Configurator sits between the controller and the running application, and that legacy mode can map controller input to keyboard, mouse, or gamepad-style output. The native Steam Input API path is action-based and can report `absolute_mouse` analog deltas, but official initialization docs assume Steam can identify an AppID. That makes native API behavior for a plain non-Steam shortcut the first spike to validate.

References:

- Steam Input general concepts: https://partner.steamgames.com/doc/features/steam_controller/concepts
- Steam Input developer guide: https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs
- ISteamInput API: https://partner.steamgames.com/doc/api/isteaminput
- Steamworks API initialization: https://partner.steamgames.com/doc/sdk/api
- Steam overlay behavior: https://partner.steamgames.com/doc/features/overlay
- SISR GlosSI-like shortcut pattern: https://alia5.github.io/SISR/stable/guides/glossi_like/
