# Findings and decisions

- User recipe: clear quartz in centre and bottom-centre of the 3x3 grid creates one Bong. A Bong plus one Marijuana buds creates one Loaded up Bong. Smoking returns the empty bong for reloading and needs distinct bubbling audio.
- Retain the existing marijuana balance: five seconds of held use, two in-game hours of Stoned, -20% base movement and +0.5 HP/game minute. No additional water/refill ingredient was requested.
- Use two variants (`bong-empty`, `bong-loaded`), each stack size one. Replace the loaded item in its current slot after successful server-side use; this works with a full inventory and avoids any item-drop fallback.
- Keep JointItem unchanged. Reuse its five-second constant and the native-frame-tested smoking animations; adjust the bong's hand transforms for its taller shape. Cancel, early release, death and duplicate stops must not consume buds or duplicate the vessel.
- Existing syringe code verifies `ItemStack.Attributes.Clone`, direct `ItemSlot.Itemstack` replacement, variant lookup and `MarkDirty` on 1.22.7. Existing joint code verifies held-use and sound APIs.
- Native GridRecipe matching respects declared width/height, so use a full 3x3 `___,_Q_,_Q_` pattern to require the exact requested slots. Ingredient is verified `game:clearquartz`, used by the existing syringe recipe.
- New native JSON models and generated glass texture will match existing item art; loaded bowl reuses the marijuana bud atlas. Audio is original synthesized bubbling/inhalation with no external samples.
- Branch `feat/reusable-bong` starts from master `ecb2edb`, which includes the smoking crash fix. No graphical client is available; rendered pose/audio require manual acceptance.
- Native `CraftingRecipeIngredient.ResolvedItemStack` uses a capital S in Stack (unlike JsonItemStack's compatibility field). Verified by decompiling 1.22.7 after the probe compilation caught the casing mismatch.
- Shape textures deserialize as `AssetLocation`, not strings. Bare shape references default to game:; the itemtype supplies the mod aliases on clients. The asset probe explicitly resolves local shape texture paths against the item's domain. `Item.Textures` is released on servers after asset loading, so tests do not depend on that field remaining populated.
- Native `InventoryGeneric(9, "bongprobe-full", api)` supports the full-inventory return regression with real slots; all 3 load/smoke/reload cycles preserve surrounding full stacks.
- Original generated glass texture retained with selected quiet UV regions. An attempted simplification produced nearly empty alpha and was discarded. Glass render pass 3 follows the vanilla clutter bottle. Bud texture is reused from the existing plant atlas.
