# Steam Input Validation Spike

The next implementation work should prove Steam runtime behavior before building more UI.

## Checklist

- [ ] Publish or run a minimal bridge executable.
- [ ] Add it to Steam as a non-Steam shortcut.
- [ ] Launch it from Steam with `--profile spike`.
- [ ] Confirm Steam shows the shortcut as Running while the process is alive.
- [ ] Confirm Steam Stop terminates the process or sends a close request that the app can handle cleanly.
- [ ] Add a second Steam shortcut pointing to the same executable with a different `--profile`.
- [ ] Confirm each shortcut retains a distinct Steam Input configuration.
- [ ] Confirm command-line arguments identify the intended profile.
- [ ] Confirm the single-instance guard prevents two bridge processes from owning output at the same time.
- [ ] Confirm whether `SteamAPI_Init` succeeds for this shortcut mode without a published bridge AppID.
- [ ] If `SteamAPI_Init` succeeds, confirm `ISteamInput::Init`, action manifest setup, action handles, `RunFrame`, `GetConnectedControllers`, `GetAnalogActionData`, and `GetDigitalActionData`.
- [ ] Confirm an `absolute_mouse` analog action returns usable x/y deltas.
- [ ] Confirm gyro can be bound to that action and produces stable deltas.
- [ ] Convert deltas/buttons/keys into host-device frames.
- [ ] Forward synthetic packets to Teensy once hardware is available.

## Expected Native Actions

Start with one gameplay action set:

```text
gameplay
  camera_delta       StickPadGyro, absolute_mouse
  wheel              AnalogTrigger or buttons, as needed after spike
  mouse_left         Button
  mouse_right        Button
  mouse_middle       Button
  mouse_back         Button
  mouse_forward      Button
  key_*              Button actions for the first keyboard binds we support
```

Keep action names unique across action sets because Steam's action handle lookup is name-based.

See `docs/steam_input_manifest.example.vdf` for the first action-manifest sketch. Treat it as spike input, not a final supported Steam layout.

## Open Risk

Official Steamworks docs say `SteamAPI_Init` can fail when Steam cannot determine the AppID. Non-Steam shortcuts clearly support Steam controller configurations, but native `ISteamInput` access from an unsigned bridge shortcut still needs proof. If this fails, the project must decide between obtaining a proper AppID, using a supported wrapper/target pattern, or falling back to a legacy-mode capture approach.

References:

- ISteamInput API: https://partner.steamgames.com/doc/api/isteaminput
- Steamworks API initialization: https://partner.steamgames.com/doc/sdk/api
- Steam Input action manifest files: https://partner.steamgames.com/doc/features/steam_controller/action_manifest_file
