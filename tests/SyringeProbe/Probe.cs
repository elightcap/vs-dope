using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsDope.Items;
using VsDope.Systems;

public class Probe : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => Run(api));
    }
    private void Run(ICoreServerAPI api)
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("SYRINGE TEST FAIL: " + name);
            checks++;
            api.Logger.Notification("SYRINGE TEST PASS: " + name);
        }
        try
        {
            var world = api.World;
            var fill = typeof(SyringeItem).GetMethod("Fill", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var dose = typeof(SyringeItem).GetMethod("ConsumeDoseVolume", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var validate = typeof(SyringeRecipeSystem).GetMethod("ValidateFill", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var recipeSystem = api.ModLoader.GetModSystem<SyringeRecipeSystem>();
            var empty = world.GetItem(new AssetLocation("vs-dope:syringe-empty"));
            foreach (string metal in new[] { "copper", "tin", "brass", "gold", "iron", "steel" })
                Check(world.GetItem(new AssetLocation("game:rod-" + metal)) != null, "rod exists: " + metal);
            var emptyRecipes = world.GridRecipes.Where(r => r.Output?.Code?.ToString() == "vs-dope:syringe-empty").ToArray();
            Check(emptyRecipes.Length > 0, "empty syringe recipe resolved");
            Check(emptyRecipes.All(r => r.Width == 3 && r.Height == 3 && r.ResolvedIngredients![1]?.Code?.Path.StartsWith("rod-") == true && r.ResolvedIngredients[4]?.Code?.Path == "clearquartz" && r.ResolvedIngredients[7]?.Code?.Path.StartsWith("metalplate-") == true), "centre column positions");
            Check(emptyRecipes.Any(r => r.ResolvedIngredients![1]?.Code?.Path == "rod-copper" && r.ResolvedIngredients[7]?.Code?.Path == "metalplate-iron"), "mixed metal recipe resolved");
            foreach (string kind in new[] { "heroin", "morphine" })
            {
                var liquid = world.GetItem(new AssetLocation("vs-dope:" + (kind == "heroin" ? "heroin" : "morphine-solution")));
                var bucket = world.Blocks.OfType<BlockLiquidContainerBase>().First(b => b.Code.Path == "woodbucket");
                var vessel = new ItemStack(bucket);
                bucket.SetContent(vessel, new ItemStack(liquid, 137));
                var slot = new DummySlot(new ItemStack(empty));
                System.Func<int, ItemStack> take = n => bucket.TryTakeContent(vessel, n);
                fill.Invoke(slot.Itemstack.Collectible, new object?[] { slot, bucket.GetContent(vessel), take });
                Check(SyringeItem.Portions(slot.Itemstack) == 100 && bucket.GetContent(vessel)!.StackSize == 37, kind + " fill conserves volume and caps at 1 L");
                Check(slot.Itemstack.Collectible.Code.Path == "syringe-" + kind, kind + " correct filled variant");
                for (int i = 1; i <= 10; i++)
                {
                    bool consumed = (bool)dose.Invoke(slot.Itemstack.Collectible, new object[] { slot })!;
                    Check(consumed && SyringeItem.Portions(slot.Itemstack) == 100 - i * 10, kind + " dose " + i);
                }
                Check(slot.Itemstack.Collectible.Code.Path == "syringe-empty", kind + " tenth dose becomes empty");
                Check(!(bool)dose.Invoke(slot.Itemstack.Collectible, new object[] { slot })!, kind + " no eleventh dose");
                fill.Invoke(slot.Itemstack.Collectible, new object?[] { slot, bucket.GetContent(vessel), take });
                Check(SyringeItem.Portions(slot.Itemstack) == 37 && bucket.GetContent(vessel) == null, kind + " partial refill retained exactly");
                using var reader = new BinaryReader(new MemoryStream(slot.Itemstack.ToBytes()));
                var restored = new ItemStack(reader, world);
                Check(SyringeItem.Portions(restored) == 37 && restored.Collectible.Code.Equals(slot.Itemstack.Collectible.Code), kind + " serialized content and volume survive roundtrip");
                var otherLiquid = world.GetItem(new AssetLocation("vs-dope:" + (kind == "heroin" ? "morphine-solution" : "heroin")));
                bucket.SetContent(vessel, new ItemStack(otherLiquid, 100));
                fill.Invoke(slot.Itemstack.Collectible, new object?[] { slot, bucket.GetContent(vessel), take });
                Check(SyringeItem.Portions(slot.Itemstack) == 37 && bucket.GetContent(vessel)!.StackSize == 100, kind + " mixing rejected without loss");
                bucket.SetContent(vessel, new ItemStack(liquid, 100));
                fill.Invoke(slot.Itemstack.Collectible, new object?[] { slot, bucket.GetContent(vessel), take });
                Check(SyringeItem.Portions(slot.Itemstack) == 100 && bucket.GetContent(vessel)!.StackSize == 37, kind + " top-up removes only missing volume");
                var recipe = world.GridRecipes.First(r => r.Name?.ToString() == "vs-dope:fill-syringe-" + kind + "-game-woodbucket");
                var ingredient = recipe.ResolvedIngredients!.First(i => i?.Code?.Path == "woodbucket")!;
                bucket.SetContent(vessel, new ItemStack(liquid, 200));
                var vesselSlot = new DummySlot(vessel);
                ItemSlot[] grid = { new DummySlot(new ItemStack(empty)), vesselSlot };
                Check((bool)validate.Invoke(recipeSystem, new object?[] { null, recipe, grid, 2 })!, kind + " crafting source accepted");
                Check(bucket.MatchesForCrafting(vessel, recipe, ingredient), kind + " native liquid crafting matches");
                bucket.OnConsumedByCrafting(grid, vesselSlot, recipe, ingredient, null!, 1);
                Check(vesselSlot.Itemstack.Collectible == bucket && bucket.GetContent(vessel)!.StackSize == 100, kind + " crafting retains vessel and consumes exactly 1 L");
                vessel.StackSize = 2;
                Check(!(bool)validate.Invoke(recipeSystem, new object?[] { null, recipe, grid, 2 })!, kind + " stacked vessels rejected");
            }
            api.Logger.Notification("SYRINGE TEST SUMMARY: " + checks + " checks passed");
        }
        catch (Exception ex) { api.Logger.Error("SYRINGE TEST FAILED: " + ex); }
    }
}
