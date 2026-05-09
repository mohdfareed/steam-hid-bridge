# Steam Input

This folder is the boundary for native Steam Input work.

The current app only has a placeholder reader so the rest of the bridge can stay small while the Steamworks spike is validated. Real input reading should land here and expose already-normalized keyboard/mouse HID intent to the view model and transport path.

Do not put Steam layout editing, VDF rewriting, or target-game overlay workarounds here for v1.
