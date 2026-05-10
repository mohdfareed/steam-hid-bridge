# Steam

## Active Path

Two input paths exist:

```text
Steam shortcut/profile
  -> Steam legacy mouse output
  -> Windows Raw Input observer
  -> BridgeRuntime
     -> GUI preview
     -> selected output consumer
```

```text
Steam shortcut/profile
  -> Steam Input game actions
  -> Steamworks.NET / ISteamInput
  -> BridgeRuntime
     -> GUI preview
     -> selected output consumer
```

`Virtual Mouse` mode observes mouse movement, buttons, and wheel from Windows Raw Input. Steam can produce that input through normal controller templates and legacy mouse bindings.

`Steam Input` mode loads a minimal action manifest and polls Steamworks `ISteamInput` actions. The manifest defines:

- `Pointer` as a `StickPadGyro` action using `absolute_mouse`.
- `LeftClick`, `RightClick`, `MiddleClick`, `BackClick`, `ForwardClick`, `WheelUp`, and `WheelDown` as digital button actions.

The Settings tab has an explicit `Apply` button for the Steam Input action manifest. Saving general settings also writes the action manifest when Input is set to `Steam Input`.

The app still uses `steam://forceinputappid/<appid>` while a configured receiver owns the foreground window. That keeps Steam's shortcut controller config active for launcher-based games. The app id is read from Steam's launch environment.

## Files

The app project keeps the source manifest next to `appsettings.example.json`:

```text
action_manifest.vdf
```

Published builds copy it under:

```text
Steam\action_manifest.vdf
```

At runtime, the app copies it under:

```text
%LOCALAPPDATA%\SteamHidBridge\steam-input\action_manifest.vdf
```

When Steam provides an app id, the app also writes:

```text
<Steam>\controller_config\game_actions_<appid>.vdf
```

Published builds include `steam_api64.dll` beside `SteamHidBridge.exe`.
