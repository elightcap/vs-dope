# Repository Structure

## Quick orientation

- Vintage Story target: 1.22.7
- .NET target: net10.0
- Mod ID: `vs-dope`
- Namespace: `VsDope`
- Entry point: `src/VsDopeModSystem.cs`
- Build project: `vs-dope.csproj`
- Gameplay assets: `assets/vs-dope/`
- Runtime systems: `src/Systems/`
- Consumables: `src/Items/`
- Client UI: `src/Client/`

## Tree

```text
vs-dope/
├── AGENTS.md
├── README.md
├── modinfo.json
├── vs-dope.csproj
├── docs/
│   ├── COCA_VITAE_FIXES.md
│   ├── OVERDOSE.md
│   ├── SYRINGES.md
│   └── repo/
│       ├── README.md
│       ├── STRUCTURE.md
│       ├── RUNTIME.md
│       ├── ASSETS.md
│       ├── PROCESSING.md
│       ├── DEPENDENCIES.md
│       └── MAINTENANCE.md
├── src/
│   ├── VsDopeModSystem.cs
│   ├── Items/DrugConsumableItem.cs
│   ├── Items/SyringeItem.cs
│   ├── Systems/AddictionSystem.cs
│   ├── Systems/OverdoseSystem.cs
│   ├── Systems/DrugVisualEffects.cs
│   ├── Systems/HeroinVesselDoseSystem.cs
│   ├── Systems/SyringeRecipeSystem.cs
│   └── Client/
│       ├── AddictionCharacterTabSystem.cs
│       ├── CocaVitaeEffectHudSystem.cs
│       └── OverdoseHudSystem.cs
└── assets/vs-dope/
    ├── blocktypes/
    ├── entities/          drugaddict.json (vanilla seraph shape + our skin)
    ├── itemtypes/
    ├── lang/
    ├── recipes/barrel/
    ├── shapes/block/
    ├── shapes/plant/
    ├── statuseffects/
    └── textures/
```

## Build

`vs-dope.csproj` references Vintage Story DLLs through the `VINTAGE_STORY` environment variable. Local API-facing changes should be built against the target 1.22.x install.

When changing gameplay, first determine whether the behavior is JSON-driven, C#-driven, or both.

`tests/OverdoseProbe` and `tests/SyringeProbe` are separate server test mods, excluded from production compilation. Overdose uses the game-bundled `Lib/0Harmony.dll`; liquid APIs use `Mods/VSSurvivalMod.dll`.
