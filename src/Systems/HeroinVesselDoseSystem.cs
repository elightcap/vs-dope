using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VsDope.Systems;

/// <summary>Observe actual heroin consumption through vanilla liquid containers.</summary>
public sealed class HeroinVesselDoseSystem : ModSystem
{
    private Harmony? harmony;

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        harmony = new Harmony("vs-dope.heroin-vessel-dose");
        harmony.Patch(AccessTools.Method(typeof(BlockLiquidContainerBase), "tryEatStop"),
            prefix: new HarmonyMethod(typeof(HeroinVesselDoseSystem), nameof(DrinkHeroin)));
    }

    private static bool DrinkHeroin(BlockLiquidContainerBase __instance, float secondsUsed, ItemSlot slot, EntityAgent byEntity)
    {
        if (byEntity.World.Side != EnumAppSide.Server || slot.Empty) return true;
        ItemStack? content = __instance.GetContent(slot.Itemstack);
        if (content?.Collectible.Code.ToString() != "vs-dope:heroin") return true;
        if (secondsUsed < 0.95f || !byEntity.Alive || byEntity is not EntityPlayer ep) return false;
        IPlayer? player = byEntity.World.PlayerByUid(ep.PlayerUID);
        var props = BlockLiquidContainerBase.GetContainableProps(content);
        if (player == null || props == null || props.ItemsPerLitre <= 0) return false;

        float litres = Math.Max(1f / props.ItemsPerLitre, __instance.DrinkPortionSize);
        int portions = __instance.SplitStackAndPerformAction(byEntity, slot,
            stack => __instance.TryTakeLiquid(stack, litres)?.StackSize ?? 0);
        if (portions <= 0) return false;

        VsDopeModSystem.AddictionSystem.ApplyHeroinDose(player, portions / props.ItemsPerLitre);
        slot.MarkDirty();
        player.InventoryManager.BroadcastHotbarSlot();
        return false;
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll("vs-dope.heroin-vessel-dose");
        base.Dispose();
    }
}
