# Findings
- Vanilla barrel recipes accept solid `quantity` > 1 (e.g. `pulp-linen`: 4 flaxfibers + 2 L water -> 1). Ratios scale per multiple.
- Vanilla tradelist prices are integers; kept integers, var = avg/4.
- "Reduce cost by 60%" read as price x0.4 on every gear price (traders both ways + addict). Recipes read as "less output per crop".
- Yield: user found 50 poppies/L too harsh, asked for ~10 (4x the original 2.5). Now 2 opium -> 1 morphine, 1 morphine -> 1 L solution: ~10 poppies per litre of heroin (10 doses); ~2 coca plants per Coca Vitae. Previously 1 poppy = 0.4 L heroin, 1 coca = 5 Vitae.
- Marijuana chain untouched (not named in request); only its seed price scaled.
- Bug (pre-existing): coca recipe used nonexistent `game:aquavitaeportion`; server logged `failed to resolve 1 recipes`. Aqua Vitae = `game:alcoholportion`.
- Bug (pre-existing): morphine solution / heroin missing from handbook "created by": liquids lacked `handbook.ignoreCreativeInvStacks`, so handbook pages were bucket stacks (decompiled `GetHandBookStacks`, `CollectibleBehaviorHandbookTextAndExtraInfo`).
- User asked for coca: quern -> barrel with aqua vitae -> dry. Added `coca-leaf-ground` item (recoloured opium icon).
