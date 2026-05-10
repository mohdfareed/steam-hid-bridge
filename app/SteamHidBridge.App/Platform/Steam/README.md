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

Native Steam Input actions are a selectable but inactive input mode. Restoring them requires fixing the Steam app id/native API context and the action manifest/layout registration so Steam's controller UI behaves like normal games/templates.
