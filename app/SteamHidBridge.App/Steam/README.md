# Steam Input

Steam HID Bridge reads Steam Input as mouse actions only for now.

The runtime shape is:

```text
SteamMouseInputEmitter
  -> BridgeRuntime
     -> GUI preview
     -> future Teensy mouse output consumer
```

The GUI preview receives every frame so the app can show what Steam is producing. Future physical output consumers must receive frames only when the configured receiver process owns the foreground window.

## Action Contract

The bridge loads [steam-input-manifest.vdf](steam-input-manifest.vdf) with `SetInputActionManifestFilePath` before initializing Steam Input.

The bridge expects one action set named `mouse`.

Analog actions:

- `mouse_move`: `absolute_mouse`

Digital actions:

- `mouse_left`
- `mouse_right`
- `mouse_middle`
- `mouse_back`
- `mouse_forward`
- `wheel_up`
- `wheel_down`

Keyboard actions are intentionally not part of this first Steam Input pass.

Steam's generic mouse or keyboard outputs are legacy bindings. They can move the OS cursor or click the focused app, but they are not returned by the Steam Input API calls used here. The bridge preview only changes when the controller layout binds inputs to the named actions above.

## Action Manifest

Valve's native Steam Input path has two pieces:

- An action manifest that defines the API-readable actions.
- Official/default controller configuration VDF files exported from Steam's configurator.

The repository currently has the action manifest only. Its `configurations` section declares empty buckets for the Steam Controller family (`controller_steamcontroller_gordon`), Steam Deck (`controller_neptune`), and generic gamepads. Add real `path` entries only after exporting working layouts from Steam's configurator; do not hand-write fake controller configuration files.

Valve's current public action-manifest docs do not list a separate 2026 Steam Controller type string. Until Valve documents one, treat the new Steam Controller as part of the Steam Controller family and keep the generic-gamepad bucket as a fallback.

Do not copy `game_actions_<appid>.vdf` files or scan Steam shortcuts for generated app ids. That path created stale Steam-side cache files and confusing app-id behavior. The app should load its own manifest directly through Steamworks instead.

## Native Steamworks Runtime

The managed app uses Steamworks.NET to call the Steamworks API from C#. The app project also references `Facepunch.Steamworks.Dll` so build and publish output includes Valve's native `steam_api64.dll` beside `SteamHidBridge.exe`.

A missing native DLL shows as Steam Input unavailable in the app log/status instead of crashing the bridge. If this appears after a code change, rebuild or republish and confirm `steam_api64.dll` exists in the same folder as `SteamHidBridge.exe`.

Launching the bridge as a Steam non-Steam shortcut can still return `false` from `SteamAPI.Init()`. Steam may apply overlay/controller profile behavior to the shortcut without providing a Steamworks app context that the API can initialize against.

Do not place `steam_appid.txt` beside the bridge executable for normal testing. Valve's documentation says that file overrides the app id Steam provides; using the public test id `480` makes Steam identify the bridge as Spacewar.
