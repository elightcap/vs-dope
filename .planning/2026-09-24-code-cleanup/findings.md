# Findings

- `ICoreServerAPI.RegisterCommand` is [Obsolete] in 1.22.7. Replacement: `api.ChatCommands.Create(name).WithDescription().RequiresPrivilege().RequiresPlayer().WithArgs(api.ChatCommands.Parsers.OptionalIntRange(...)).HandleWith(args => TextCommandResult.Success/Error(...))`. `OnCommandDelegate = TextCommandResult (TextCommandCallingArgs)`; `args[0]` is the parsed value, `args.Caller.Player` is IPlayer. (Decompiled VintagestoryAPI + ModSystemRifts usage.)
- `Entity.GetName()` uses lang `<domain>:item-creature-<path>` (alive) and `item-dead-creature-<path>` (dead). Our `entity-type-drugaddict`/`drugaddict-name` keys were never read.
- `TranslationService.KeyWithDomain` keeps keys that already contain `:`, so `"vs-dope:addict-greet"` in en.json worked, but it's inconsistent with the unprefixed convention. Normalized.
- Orphaned assets: `tools/build_coca_models.py` emits `-v4` textures; `-v3` set and `coca_atlas.png` are referenced nowhere. `shapes/item/opium-pile.json` unreferenced (opium uses another shape).
- Kept on purpose: OverdoseSystem legacy-key removal and the intoxication/psychedelic clamp on join. They migrate old saves and are cheap.
