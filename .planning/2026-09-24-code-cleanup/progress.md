# Progress

## 2026-09-24
- Audited codebase; baseline build 0 errors / 31 warnings.
- Replaced RegisterCommand with ChatCommands, ServerPos with Pos. Removed DumpRegistry startup/diagnostic dump.
- Removed dead AddictionSystem members (GetAddictionLevel, IsAddicted, GetWithdrawalSeverity, WatchOverdose, TolKey) and narrowed internal-only members to private.
- Moved hard-coded English (trade chat/UI, character tab, coca HUD, spawn command) to lang keys; fixed addict name keys; dropped duplicate/unused lang keys.
- Build error hit: `Func<,>` ambiguous (System vs Vintagestory.API.Common) after dropping `System.` prefix; reverted that line.
- Build: 0 errors / 0 warnings (was 31 warnings). Both test probes compile.
