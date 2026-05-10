# Steam

Steam HID Bridge currently uses Steam as the shortcut/profile owner, not as a native Steamworks Input provider.

## Active Path

```text
Steam shortcut/profile
  -> Steam legacy mouse output
  -> Windows Raw Input observer
  -> BridgeRuntime
     -> GUI preview
     -> future Teensy mouse output consumer
```

The app observes mouse movement, buttons, and wheel from Windows Raw Input. Steam can produce that input through normal controller templates and legacy mouse bindings.

The app still uses `steam://forceinputappid/<appid>` while a configured receiver owns the foreground window. That keeps Steam's shortcut controller config active for launcher-based games. The app id is read from Steam's launch environment.

## Deferred Native Path

Native Steam Input actions are not active in the MVP. Do not add Steamworks.NET, ship `steam_appid.txt`, write `game_actions_<appid>.vdf`, or scan Steam shortcut databases unless this path is explicitly reopened.

The native action manifest was removed from publish output because loading actions without exported controller layouts made Steam's controller UI behave differently from normal games/templates.
