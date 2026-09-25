# Findings: addict reputation (#51)

## Requirements (issue #51 + triage comment)
- Stable addict IDs and per-player relationship records in world save data (entities despawn).
- Sales, muggings, death and return selection connect to the ledger.
- Death consequences recorded exactly once. Existing spawned NPCs need migration.
- Regulars return more often, pay better, eventually share rumours (e.g. a ruin they scavenged).
- Muggings spread: other addicts get warier or stop coming for a while.
- A regular dying from your product lowers demand for a while (bad batch needs #49: hook only).
- Visible decline for long-term regulars; cutting them off may or may not let them recover.
- Prerequisite for #52 (runners).

## API research (1.22.7, verified by decompile/XML)
- `sapi.WorldManager.SaveGame.StoreData<T>(key, T)` / `GetData<T>(key)` persist mod data in the save (SerializerUtil = protobuf-net). Save on `Event.GameWorldSave`, load on `Event.SaveGameLoaded`.
- Persisted protobuf types use explicit `[ProtoMember(n)]`: `ImplicitFields.AllPublic` numbers fields alphabetically, so adding a field later would shift tags and corrupt old saves.
- Per-entity texture: `Entity.GetTextureSource` reads `WatchedAttributes["textureIndex"]`; `TextureSource` picks `BakedVariants[textureIndex % n]` where 0 = base, 1.. = JSON `alternates`. `Entity.Initialize` (server) sets a *random* textureIndex when the type has alternates and the key is missing. So: set it before spawn, and default missing (legacy) entities to 0 before `base.Initialize`.
- Textures without alternates have `BakedVariants == null` and ignore textureIndex (the "hair" texture).
- Nametag: `EntityBehaviorNameTag` (VSEssentials) reads `WatchedAttributes["nametag"]["name"]`; its constructor only creates the tree when missing, so a tree set before spawn survives. JSON `showtagonlywhentargeted`. Vanilla trader has it on client and server.
- Structures: `BlockAccessor.GetMapRegion(rx, rz)` (null if not loaded) `.GeneratedStructures`; `GenStructures` stores `Code = "<schematic>/<structure code>"` (e.g. `.../surrfaceruins`, vanilla spelling). Same scan pattern as vanilla `ModSystemStructureLocator.FindStructureLocation`.
- `DamageSource.GetCauseEntity()` resolves the thrower for projectiles.
- `api.ChatCommands.Parsers.OptionalBool(name)` exists.

## Decisions
- "Muggings spread" = the *player* hurting/killing addicts (the only direction where "others get warier" makes sense). Killing an addict that mugged you this visit is self-defence: no heat. Heat cuts spawn chances and stops them entirely at the threshold; decays daily.
- An addict mugging the player is recorded on the relation (TimesMuggedPlayer), and that addict won't return to that player.
- Tiers from visits-with-a-sale per player: Stranger 0, Customer 1-2, Regular 3-5, Trusted 6+. Counting visits (not sale clicks) prevents farming by selling one unit at a time.
- Returning addicts get a fresh entity each visit (new pockets: they've been scavenging). The identity, name, decline and relations persist.
- Duplicate protection: record.ActiveEntityId; an entity whose record points elsewhere despawns silently. Stale active flags are released after a day.
- Decline = exposure from drugs bought (weighted by product), stages at thresholds; cut off for 3+ days → exposure decays, unless the addict rolled "stubborn" at creation.
