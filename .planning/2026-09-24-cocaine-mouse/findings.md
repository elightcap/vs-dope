# Findings

- User reports mouse control breaking after using cocaine (Coca Vitae).
- Fresh worktree `fix/cocaine-mouse-control` starts at master `e994694`.
- `GuiDialogCocaVitaeEffect` inherits `GuiDialog`. Native 1.22.7 `TryOpen()` requests focus for Dialog types; `PrefersUngrabbedMouse` defaults true.
- Decompiled `ClientMain.UpdateFreeMouse()` in the installed 1.22.7 runtime confirms open Dialogs prevent normal mouse grab and can suppress world interactions. Both active and crash countdowns open the same dialog.
- Native `HudElement` changes `DialogType` to HUD and `PrefersUngrabbedMouse` to false. It still inherits `Focusable = true` and `ShouldReceiveMouseEvents() = IsOpened()`, so explicitly disable those for a passive countdown.
- Match the other status HUDs' world-exit cleanup and text-change composition. Dispose the old composer before rebuilding; the current countdown replaces it every 250ms without disposal.
- Runtime and SDK are available at `/workspace/scratch/25ddf310c8d1/vs-runtime` and `/workspace/scratch/25ddf310c8d1/dotnet`. API signatures verified by decompilation, not older online documentation.
- A rendered client is unavailable. The regression probe will exercise real API dialog opening, focus, event-routing decisions and close/reopen behavior with only the GUI host stubbed.
