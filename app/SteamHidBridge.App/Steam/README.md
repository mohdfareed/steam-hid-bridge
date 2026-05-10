# Steam Input

Steam HID Bridge reads Steam Input as mouse actions only for now.

The runtime shape is:

```text
SteamMouseInputEmitter
  -> MouseInputPipeline
     -> MouseVisualizerConsumer
     -> TeensyMouseOutputConsumer
```

The visualizer consumer always receives frames so the GUI can show what Steam is producing. The Teensy consumer receives frames only when the configured receiver process owns the foreground window.

## Action Contract

The bridge expects one action set named `bridge`.

Analog actions:

- `pointer`: `absolute_mouse`

Digital actions:

- `mouse_left`
- `mouse_right`
- `mouse_middle`
- `mouse_back`
- `mouse_forward`
- `wheel_up`
- `wheel_down`

Keyboard actions are intentionally not part of this first Steam Input pass.

## In-Game Actions File

Steam's in-game actions file must be named for the Steam app id that Steam uses for the bridge shortcut:

```text
game_actions_<appid>.vdf
```

Place it under Steam's `controller_config` folder. Use [steam-input-actions.vdf](steam-input-actions.vdf) as the source content and rename the file for the app id being tested.

The file declares the actions Steam can expose in the controller layout editor and that the app reads by name through the Steam Input API. It does not create the user's controller layout by itself.

## Native Steamworks Runtime

The managed app uses Steamworks.NET to call the Steamworks API from C#. Steamworks.NET does not ship Valve's native `steam_api64.dll` in its NuGet package, so the official Steamworks redistributable still needs to be placed next to `SteamHidBridge.exe` before Steam API calls can succeed.

Until release packaging handles that file explicitly, a missing native DLL will show as Steam Input unavailable in the app log/status instead of crashing the bridge.
