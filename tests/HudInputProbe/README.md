# Coca Vitae HUD input regression

Run against the actual Vintage Story 1.22.7 installation:

```bash
export VINTAGE_STORY=/path/to/vintagestory
dotnet run --project tests/HudInputProbe
```

This console probe uses the compiled production HUD and native API dialog methods. Only `ICoreClientAPI`/`IGuiAPI` hosting is stubbed. The native `TryOpen(bool)` overload avoids graphics composition, so it can run without a game account, world or graphics context. Runtime dependencies resolve from `VINTAGE_STORY`, `Lib` and `Mods`.

It exercises opening, registration, focus, mouse-grab preferences, input routing, Escape, close and reopen. The old `GuiDialog` implementation fails 12 of 21 checks; the fixed HUD passes all 21. It does not simulate physical mouse movement or render text, and is excluded from the shipped mod.

## In-game acceptance

1. Fully restart the game with the rebuilt DLL and assets. Consume Coca Vitae while looking at empty space. The countdown should appear while mouse-look, mining and held use continue working.
2. Open inventory and the Escape menu, then close each. Their cursor behavior should remain normal; the countdown stays visible without reopening a dialog or stealing focus.
3. Verify the crash countdown after the active effect expires. In a disposable test world, `/time add 1` advances one game hour. Check mouse-look again, including after another dose while crashing.
4. Let both timers expire; the HUD disappears. Leave/rejoin while active and test death/respawn: the HUD must close when leaving/dying and reflect saved or cleared timers on return.
5. Repeat mouse-look checks in first/third person and with immersive mouse mode both enabled and disabled.
