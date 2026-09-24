using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsDope.Items;
using VsDope.Systems;

namespace VsDope.Tests;

internal static class BongChecks
{
    public static void Verify(ICoreServerAPI api, IServerPlayer player, Action<bool, string> check)
    {
        var world = api.World;
        var entity = (ProbePlayer)player.Entity;
        var empty = world.GetItem(new AssetLocation("vs-dope:bong-empty")) as BongItem;
        var loaded = world.GetItem(new AssetLocation("vs-dope:bong-loaded")) as BongItem;
        var quartz = world.GetItem(new AssetLocation("game:clearquartz"));
        var buds = world.GetItem(new AssetLocation("vs-dope:marijuana-buds"));
        check(empty != null && loaded != null && quartz != null && buds != null, "bong variants and ingredients registered");
        check(empty!.MaxStackSize == 1 && loaded!.MaxStackSize == 1 && !empty.Loaded && loaded.Loaded,
            "empty and loaded bongs are non-stackable and have distinct states");
        var craft = world.GridRecipes.Single(r => r.Output?.Code?.ToString() == "vs-dope:bong-empty");
        var load = world.GridRecipes.Single(r => r.Output?.Code?.ToString() == "vs-dope:bong-loaded");
        check(!craft.Shapeless && craft.Width == 3 && craft.Height == 3 && craft.Output!.StackSize == 1,
            "bong recipe occupies the full 3x3 grid and outputs one bong");
        ItemSlot[] Grid() => Enumerable.Range(0, 9).Select(_ => (ItemSlot)new DummySlot()).ToArray();
        var grid = Grid();
        grid[4] = new DummySlot(new ItemStack(quartz, 3));
        grid[7] = new DummySlot(new ItemStack(quartz, 4));
        check(craft.Matches(player, world, grid, 3), "clear quartz in centre and bottom-centre crafts a bong");
        check(craft.ConsumeInput(player, grid, 3) && grid[4].StackSize == 2 && grid[7].StackSize == 3,
            "bong crafting consumes exactly two quartz, one from each specified slot");
        // Exhaustively reject every other pair of occupied slots, including shifted/mirrored layouts.
        bool onlyRequestedSlots = true;
        for (int first = 0; first < 9; first++)
        for (int second = first + 1; second < 9; second++)
        {
            grid = Grid();
            grid[first] = new DummySlot(new ItemStack(quartz));
            grid[second] = new DummySlot(new ItemStack(quartz));
            onlyRequestedSlots &= craft.Matches(player, world, grid, 3) == (first == 4 && second == 7);
        }
        check(onlyRequestedSlots, "all 36 two-quartz placements accept only centre plus bottom-centre");
        grid = Grid();
        grid[4] = new DummySlot(new ItemStack(quartz, 2));
        check(!craft.Matches(player, world, grid, 3), "two quartz stacked in one slot cannot craft bong");
        grid[7] = new DummySlot(new ItemStack(quartz));
        grid[0] = new DummySlot(new ItemStack(buds));
        check(!craft.Matches(player, world, grid, 3), "extra ingredients cannot sneak into bong recipe");

        check(load.Shapeless && load.Output!.StackSize == 1, "loading recipe is shapeless and outputs one loaded bong");
        bool everyPlacement = true;
        for (int vessel = 0; vessel < 9; vessel++)
        for (int bud = 0; bud < 9; bud++)
        {
            if (vessel == bud) continue;
            grid = Grid();
            grid[vessel] = new DummySlot(new ItemStack(empty));
            grid[bud] = new DummySlot(new ItemStack(buds, 3));
            everyPlacement &= load.Matches(player, world, grid, 3);
        }
        check(everyPlacement, "bong and buds load in all 72 distinct grid positions");
        grid = Grid();
        grid[0] = new DummySlot(new ItemStack(loaded));
        grid[8] = new DummySlot(new ItemStack(buds));
        check(!load.Matches(player, world, grid, 3), "already loaded bong cannot be loaded again");
        grid[0] = new DummySlot();
        check(!load.Matches(player, world, grid, 3), "buds alone cannot craft a loaded bong");
        grid[0] = new DummySlot(new ItemStack(empty));
        grid[8] = new DummySlot();
        check(!load.Matches(player, world, grid, 3), "empty bong alone cannot load itself");

        var inventory = new InventoryGeneric(9, "bongprobe-full", api);
        for (int i = 1; i < inventory.Count; i++) inventory[i].Itemstack = new ItemStack(buds, 64);
        var slot = inventory[0];
        var handling = EnumHandHandling.NotHandled;
        void Start(BongItem item)
        {
            handling = EnumHandHandling.NotHandled;
            item.OnHeldInteractStart(slot, entity, null!, null!, true, ref handling);
        }
        void Finish(BongItem item, float seconds = 5) => item.OnHeldInteractStop(seconds, slot, entity, null!, null!);
        bool NoEffect() => !entity.WatchedAttributes.HasAttribute(StonedSystem.ExpiryKey);

        slot.Itemstack = new ItemStack(empty);
        Start(empty);
        Finish(empty);
        check(handling == EnumHandHandling.NotHandled && NoEffect() && slot.Itemstack.Collectible == empty,
            "empty bong cannot smoke or grant Stoned");
        slot.Itemstack = new ItemStack(loaded);
        Finish(loaded!);
        check(slot.Itemstack.Collectible == loaded && NoEffect(), "stop without start cannot consume loaded bowl");
        Start(loaded!);
        check(handling == EnumHandHandling.PreventDefault && entity.LastAnimation == JointItem.AnimationCode,
            "loaded bong starts the tested smoking animation");
        Finish(loaded!, 4.99f);
        check(slot.Itemstack.Collectible == loaded && NoEffect(), "early release preserves loaded bong and grants no effect");
        Start(loaded!);
        loaded!.OnHeldInteractCancel(2, slot, entity, null!, null!, EnumItemUseCancelReason.Death);
        Finish(loaded);
        check(slot.Itemstack.Collectible == loaded && NoEffect(), "cancel followed by stop preserves loaded bong");
        Start(loaded);
        entity.Alive = false;
        Finish(loaded);
        check(slot.Itemstack.Collectible == loaded && NoEffect(), "death during smoking preserves bowl and grants no effect");
        entity.Alive = true;
        Start(loaded);
        slot.Itemstack = new ItemStack(empty);
        Finish(loaded);
        check(slot.Itemstack.Collectible == empty && NoEffect(), "changing the held item cannot complete smoking");
        slot.Itemstack = new ItemStack(loaded, 2);
        Start(loaded);
        Finish(loaded);
        check(slot.StackSize == 2 && slot.Itemstack.Collectible == loaded && NoEffect(),
            "invalid oversized bong stack cannot consume or duplicate vessels");

        // Actual recipe input consumption followed by item use, repeated with the same returned vessel.
        slot.Itemstack = new ItemStack(empty);
        var supply = new DummySlot(new ItemStack(buds, 5));
        for (int cycle = 1; cycle <= 3; cycle++)
        {
            grid = Grid();
            grid[1] = slot;
            grid[8] = supply;
            check(load.Matches(player, world, grid, 3) && load.ConsumeInput(player, grid, 3) && slot.Empty && supply.StackSize == 5 - cycle,
                $"reuse cycle {cycle}: native loading consumes one empty vessel and one bud");
            slot.Itemstack = load.Output!.ResolvedItemStack!.Clone();
            Start(loaded);
            check(loaded.OnHeldInteractStep(2, slot, entity, null!, null!) &&
                loaded.OnHeldInteractStep(4.99f, slot, entity, null!, null!) &&
                !loaded.OnHeldInteractStep(5, slot, entity, null!, null!), $"reuse cycle {cycle}: smoking ends at five seconds");
            Finish(loaded);
            check(slot.StackSize == 1 && slot.Itemstack?.Collectible == empty &&
                Enumerable.Range(1, 8).All(i => inventory[i].StackSize == 64 && inventory[i].Itemstack?.Collectible == buds),
                $"reuse cycle {cycle}: exactly one empty bong returns in-place with all other slots full");
            double expiry = entity.WatchedAttributes.GetDouble(StonedSystem.ExpiryKey);
            check(Math.Abs(expiry - world.Calendar.TotalHours - 2) < .0001 &&
                Math.Abs(entity.Stats.GetBlended("walkspeed") - .8) < .0001,
                $"reuse cycle {cycle}: shared Stoned effect lasts two game hours without stacking speed");
            var returned = slot.Itemstack;
            Finish(loaded);
            check(ReferenceEquals(returned, slot.Itemstack) && entity.WatchedAttributes.GetDouble(StonedSystem.ExpiryKey) == expiry,
                $"reuse cycle {cycle}: duplicate stop cannot return another bong or refresh effect");
        }
        double now = world.Calendar.TotalHours;
        StonedSystem.Tick(entity, now + 1.0 / 60);
        check(Math.Abs(entity.Healing - .5) < .0001, "bong Stoned heals 0.5 HP in one game minute");
        StonedSystem.Clear(entity);

        foreach (var item in new[] { empty, loaded })
        {
            var shapePath = item.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            var shape = api.Assets.Get(shapePath).ToObject<Shape>();
            // These models use only local mod textures. Raw shape JSON defaults bare locations
            // to game:, while the itemtype supplies vs-dope aliases when the client loads it.
            check(shape.Elements.Length > 0 && shape.Textures.Values.All(texture =>
                api.Assets.TryGet(new AssetLocation(item.Code.Domain, texture.Path)
                    .WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png")) != null),
                item.Code + " native model and all textures resolve");
        }
        check(api.Assets.TryGet(new AssetLocation("vs-dope:sounds/player/bong-bubbles.ogg")) != null,
            "distinct bong bubbling sound is packaged");
    }
}
