using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsDope.Items;

namespace VsDope.Systems;

// Register exact vessel codes, avoiding a wildcard recipe that could accept non-vessels.
public class SyringeRecipeSystem : ModSystem
{
    private ICoreAPI? coreApi;
    public override double ExecuteOrder() => 1.1;

    public override void Start(ICoreAPI api)
    {
        coreApi = api;
        api.Event.MatchesGridRecipe += ValidateFill;
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is not ICoreServerAPI server) return;
        foreach (Block vessel in api.World.Blocks.Where(b => b is BlockLiquidContainerBase))
        {
            // Portable barrels disallow held liquid transfer; use placed, unsealed barrels instead.
            if (!((BlockLiquidContainerBase)vessel).AllowHeldLiquidTransfer) continue;
            foreach (string kind in new[] { "heroin", "morphine" })
            {
                string liquid = kind == "morphine" ? "morphine-solution" : "heroin";
                var recipe = new GridRecipe
                {
                    Name = new AssetLocation("vs-dope", "fill-syringe-" + kind + "-" + vessel.Code.Domain + "-" + vessel.Code.Path),
                    Width = 2, Height = 1, Shapeless = true, IngredientPattern = "SV",
                    Ingredients = new()
                    {
                        ["S"] = new CraftingRecipeIngredient { Type = EnumItemClass.Item, Code = new AssetLocation("vs-dope:syringe-empty"), Quantity = 1 },
                        ["V"] = new CraftingRecipeIngredient
                        {
                            Type = EnumItemClass.Block, Code = vessel.Code.Clone(), Quantity = 1,
                            RecipeAttributes = JsonObject.FromJson("{\"requiresContent\":{\"type\":\"item\",\"code\":\"vs-dope:" + liquid + "\"},\"requiresLitres\":1,\"consumeContainer\":false}")
                        }
                    },
                    Output = new CraftingRecipeIngredient
                    {
                        Type = EnumItemClass.Item, Code = new AssetLocation("vs-dope", "syringe-" + kind), Quantity = 1,
                        Attributes = JsonObject.FromJson("{\"vs-dope-syringe-portions\":100}")
                    }
                };
                recipe.OnParsed(api.World);
                if (recipe.Resolve(api.World, "vs-dope syringe filling")) server.RegisterCraftingRecipe(recipe);
            }
        }
    }

    private bool ValidateFill(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth)
    {
        if (recipe.Name?.Domain != "vs-dope" || !recipe.Name.Path.StartsWith("fill-syringe-")) return true;
        var vessels = ingredients.Where(s => !s.Empty && s.Itemstack.Collectible is BlockLiquidContainerBase).ToArray();
        // Vanilla liquid crafting divides portions across stacked vessels. Require one
        // vessel so integer division cannot round down and create free liquid.
        if (vessels.Length != 1 || vessels[0].StackSize != 1) return false;
        var source = (BlockLiquidContainerBase)vessels[0].Itemstack.Collectible;
        var content = source.GetContent(vessels[0].Itemstack);
        string kind = recipe.Output!.Code!.Path == "syringe-heroin" ? "heroin" : "morphine";
        var props = BlockLiquidContainerBase.GetContainableProps(content);
        return props != null && System.Math.Abs(props.ItemsPerLitre - SyringeItem.Capacity) < 0.001f
            && source.AllowHeldLiquidTransfer && SyringeItem.LiquidKind(content) == kind
            && content!.StackSize >= SyringeItem.Capacity;
    }

    public override void Dispose()
    {
        if (coreApi != null) coreApi.Event.MatchesGridRecipe -= ValidateFill;
        base.Dispose();
    }
}
