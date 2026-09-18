# Findings & Decisions

## Requirements

- Add an overdose mechanic to vs-dope (Vintage Story 1.22.7 mod).
- Overdose should be driven by drug consumption / intoxication accumulation.
- Must integrate with existing tolerance + addiction systems and follow repo conventions (`vs-dope` modid, ModSystem class registration, docs/repo map updates).

## Research Findings

Source: `src/Items/DrugConsumableItem.cs`, `src/Systems/AddictionSystem.cs`, `src/VsDopeModSystem.cs`, `docs/repo/RUNTIME.md`.

- **Intoxication storage:** `entity.WatchedAttributes` key `"intoxication"`, clamped 0–25 (`GameMath.Clamp(..., 0f, 25f)`). A parallel `"psychedelic"` attribute (clamped 0–25) is used by Coca Vitae and inferred heroin use.
- **No cumulative metabolism today:** each consumable adds `IntoxicationAmount` on consume and subtracts it back when its effect expires (`CheckEffectExpiry`). So intoxication only accumulates if a player re-doses within an active effect window — overdose is inherently a "binge / rapid redosing" event.
- **Consumption hook:** `DrugConsumableItem.Consume()` runs server-side (`World.Side != Server` returns early). Already reads tolerance multiplier and addiction level via `VsDopeModSystem.AddictionSystem`. Best place for an immediate on-dose overdose check.
- **Ticked watcher pattern:** `AddictionSystem.WatchHeroinEffects()` (5s timer, online players) already demonstrates a per-tick server watcher that mutates `entity.Stats` and watched attributes — the model to follow for sustained/lingering overdose effects.
- **Effect application patterns available:** additive movement via `entity.Stats.Set("walkspeed", key, value)`; damage/heal via `entity.ReceiveDamage(new DamageSource { ... }, amount)` (see withdrawal poison at AddictionSystem.cs:232 and heal in Consume).
- **Addiction/tolerance signals:** addiction level attr `vs-dope-addiction-level`; per-product tolerance attrs (`vs-dope-tolerance-<product>`); `GetEffectMultiplier(player, product)` returns effectiveness multiplier (lower = more tolerant).
- **Death handling:** no existing death/kill hook in the mod; VS default applies when health reaches 0 via damage.

## Technical Decisions

| Decision | Rationale |
|----------|-----------|
| Per-product "load" watched attribute `vs-dope-load-<product>` accumulates each dose (raw IntoxicationAmount, not tolerance-scaled) | User chose per-product concentration; keeps binge state separate from the shared 0–25 intoxication display stat. Heroin (not a DrugConsumableItem) increments its load in the psychedelic watcher with `HeroinDoseLoad`. |
| Threshold = `15 + min(tolerance, 0.5) * 20` → rises with tolerance then plateaus at +10 (tolerance ≥ 0.5 adds nothing) | Matches user requirement: threshold scales with tolerance up to a plateau point, then stays constant even as tolerance keeps climbing. |
| Severity = `clamp((load - threshold)/threshold, 0..1)`; effects = additive walkspeed slow (`-min(0.85, sev)`) + poison damage only when `sev > 0.35` at `sev * 1.0`/tick | Near-death but recoverable: mild OD is debuff-only; severe OD drains HP that can kill if ignored, but stopping dosing drops load fast enough to survive. Reuses proven Poison ReceiveDamage pattern (AddictionSystem.cs:232). |
| Natural metabolism only — `MetabolismPerTick = 1.5` load/tick (5s) in a per-tick watcher; no antidote | User chose natural metabolism, no intervention item. Watcher runs from `OnTick` for online players, mirroring `WatchHeroinEffects`. |
| Sync bool watched attr `vs-dope-overdose` each tick | Cheap client signal for future UI without extra round-trips. |

## Resolved Questions (from user)

1. Trigger basis: **per-product concentration** → new per-product load attrs.
2. Lethality: **near-death, recoverable** → escalating DoT + heavy slow, survivable if you stop dosing/rest.
3. Tolerance effect: threshold scales with tolerance but **plateaus** — beyond a point more tolerance doesn't raise it (implemented via `min(tolerance, 0.5)`).
4. Intervention: **natural metabolism only**, no antidote.

## Open Questions (need user input before Phase 3)

(none — resolved above)

## Resources

- `src/Items/DrugConsumableItem.cs` — Consume(), intoxication clamp lines 53–59, expiry 75–111.
- `src/Systems/AddictionSystem.cs` — watcher pattern 115–155, damage example 232.
- `docs/repo/RUNTIME.md`, `docs/repo/DEPENDENCIES.md`.

---

*Update this file regularly during research so important evidence remains available after context changes.*
