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

`Steam Input` mode loads a minimal bundled action manifest and polls Steamworks `ISteamInput` actions. The manifest defines:

- `Pointer` as a `StickPadGyro` action using `absolute_mouse`.
- `LeftClick`, `RightClick`, `MiddleClick`, `BackClick`, `ForwardClick`, `WheelUp`, and `WheelDown` as digital button actions.

There is no separate Steam Input export/apply step in the app anymore. Saving a profile writes the SRM manifest only; Steam Input uses the bundled manifest when the shortcut launches the bridge.

The app still uses `steam://forceinputappid/<appid>` while a configured receiver owns the foreground window. That keeps Steam's shortcut controller config active for launcher-based games. The app id is only used for this force-input path.

## Files

The app project keeps the source manifest next to `appsettings.example.json`:

```text
action_manifest.vdf
```

Published builds copy it under:

```text
Steam\action_manifest.vdf
Steam\steam_api64.dll
```

At runtime, the app copies it under:

```text
%LOCALAPPDATA%\SteamHidBridge\steam-input\action_manifest.vdf
```

The app registers the published `Steam\` folder as a native DLL search path before Steamworks initialization.
