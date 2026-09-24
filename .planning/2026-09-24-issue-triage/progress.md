# Progress

2026-09-24: Reviewed current remote master, AGENTS.md, runtime/dependency maps,
all seven open issues and comments. Created isolated worktree; unrelated bong
worktree contains ongoing work and is untouched. Posted status on all issues.
Decompiled installed 1.22.7 stat, hunger, health, AI, poultice, bed, and stability
code. Starting implementation and native server regression coverage for #55.

2026-09-24: Implemented tool effects, crash lifecycle, tolerance-aware marijuana,
HUD/tooltips and runtime docs. Builds have zero warnings/errors. Native probe
setup needed complete test player properties plus health before hunger. The
first poultice lookup then exposed that live 1.22.7 uses HealingItem instead of
ItemPoultice, so added scheduled healing support and tested the actual live item.
Final isolated run: OverdoseProbe **121 checks passed**, MarijuanaProbe **61
checks passed**; 58 JSON patches applied without errors. No mod exceptions in
the passing run. Existing aquavitae recipe and vanilla shape warnings remain.
Remote master advanced to `af7bc88` (reusable bong); integrating before delivery.

2026-09-24: Rebased onto `af7bc88`, preserving all bong assets, language keys,
documentation and tests. Resolved overlapping language/runtime/active-plan edits.
Final deployment and integration run: **124 OverdoseProbe checks + 101
MarijuanaProbe checks = 225 passed**. New shared-route tests confirm four uses
across joints/bongs count exactly four doses despite duplicate stop callbacks,
and bongs still return empty in-place. Builds have zero warnings/errors;
changed JSON parses; `git diff --check` passes. The isolated server was stopped
cleanly. No graphical client was available; manual HUD/aiming/detection checks
are in `docs/DRUG_TOOLS.md`. No push, PR, merge or issue closure performed.

Follow-up: user approved pushing and opening the PR. Refetched master; it remains
`af7bc88`, with no additional integration changes. Production/test code is
unchanged from the passing 225-check run. Publish the branch and link the PR
from #55; leave merging and manual client acceptance for review.
