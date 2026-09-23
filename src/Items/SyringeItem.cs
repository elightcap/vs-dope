using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace VsDope.Items;

/// <summary>A non-stackable 1 L syringe. Both supported liquids have 100 portions/L.</summary>
public class SyringeItem : Item
{
    public const string PortionsKey = "vs-dope-syringe-portions";
    private const string ApplyingKey = "vs-dope-syringe-applying";
    public const int Capacity = 100;
    public const int Dose = 10;
    public string Contents => Variant["contents"];

    public static int Portions(ItemStack stack)
    {
        if (stack?.Collectible is not SyringeItem syringe || syringe.Contents == "empty") return 0;
        return Math.Clamp(stack.Attributes.GetInt(PortionsKey, Capacity), 0, Capacity);
    }

    public static string? LiquidKind(ItemStack? liquid)
    {
        if (liquid?.Collectible.Code.Domain != "vs-dope") return null;
        return liquid.Collectible.Code.Path switch
        {
            "heroin" => "heroin",
            "morphine-solution" => "morphine",
            _ => null
        };
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
        if (!firstEvent || slot.Empty || slot.StackSize != 1 || byEntity is not EntityPlayer ep) return;
        slot.Itemstack.TempAttributes.SetBool(ApplyingKey, false);
        IPlayer player = byEntity.World.PlayerByUid(ep.PlayerUID);
        if (player == null) return;

        // A container interaction must never fall through to injecting the player.
        if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is ILiquidSource source)
        {
            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.Use)) return;
            if (byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel barrel && barrel.Sealed) return;
            if (byEntity.World.Side == EnumAppSide.Server)
                Fill(slot, source.GetContent(blockSel.Position), count => source.TryTakeContent(blockSel.Position, count));
            return;
        }

        ItemSlot vessel = byEntity.LeftHandItemSlot;
        if (byEntity.Controls.ShiftKey && vessel?.Itemstack?.Collectible is ILiquidSource heldSource)
        {
            // Never edit every vessel in a stack or extract from sealed portable barrels.
            if (!heldSource.AllowHeldLiquidTransfer || vessel.StackSize != 1) return;
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                Fill(slot, heldSource.GetContent(vessel.Itemstack), count => heldSource.TryTakeContent(vessel.Itemstack, count));
                vessel.MarkDirty();
            }
            return;
        }

        if (Portions(slot.Itemstack) < Dose) return;
        slot.Itemstack.TempAttributes.SetBool(ApplyingKey, true);
        byEntity.AnimManager.StartAnimation("interactstatic");
    }

    private void Fill(ItemSlot slot, ItemStack? liquid, Func<int, ItemStack> take)
    {
        string? kind = LiquidKind(liquid);
        if (kind == null || liquid == null) return;
        int current = Portions(slot.Itemstack);
        if (current > 0 && Contents != kind) return;
        var props = BlockLiquidContainerBase.GetContainableProps(liquid);
        if (props == null || Math.Abs(props.ItemsPerLitre - Capacity) > 0.001f) return;
        Item target = api.World.GetItem(new AssetLocation("vs-dope", "syringe-" + kind));
        if (target == null) return;
        int requested = Math.Min(Capacity - current, liquid.StackSize);
        if (requested <= 0) return;
        ItemStack taken = take(requested);
        if (taken == null || taken.StackSize <= 0) return;
        var filled = new ItemStack(target);
        filled.Attributes = slot.Itemstack.Attributes.Clone();
        filled.Attributes.SetInt(PortionsKey, current + taken.StackSize);
        slot.Itemstack = filled;
        slot.MarkDirty();
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
        => slot.Itemstack?.TempAttributes.GetBool(ApplyingKey) == true && secondsUsed < 1.5f;

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        byEntity.AnimManager.StopAnimation("interactstatic");
        if (slot.Itemstack?.TempAttributes.GetBool(ApplyingKey) != true) return;
        slot.Itemstack.TempAttributes.SetBool(ApplyingKey, false);
        if (secondsUsed < 1.5f || byEntity.World.Side != EnumAppSide.Server || !byEntity.Alive) return;
        if (byEntity is not EntityPlayer ep || slot.Itemstack.Collectible != this) return;
        int remaining = Portions(slot.Itemstack);
        if (remaining < Dose) return;
        IPlayer player = byEntity.World.PlayerByUid(ep.PlayerUID);
        Item empty = api.World.GetItem(new AssetLocation("vs-dope:syringe-empty"));
        if (player == null || empty == null) return;

        if (Contents == "heroin") VsDopeModSystem.AddictionSystem.ApplyHeroinSyringeDose(player);
        else if (Contents == "morphine" && api.World.GetItem(new AssetLocation("vs-dope:morphine")) is MorphineItem morphine)
            morphine.ApplyDose(byEntity);
        else return;

        remaining -= Dose;
        if (remaining == 0)
        {
            var emptyStack = new ItemStack(empty);
            emptyStack.Attributes = slot.Itemstack.Attributes.Clone();
            emptyStack.Attributes.RemoveAttribute(PortionsKey);
            slot.Itemstack = emptyStack;
        }
        else slot.Itemstack.Attributes.SetInt(PortionsKey, remaining);
        slot.MarkDirty();
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        slot.Itemstack?.TempAttributes.SetBool(ApplyingKey, false);
        byEntity.AnimManager.StopAnimation("interactstatic");
        return true;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        int portions = Portions(inSlot.Itemstack);
        dsc.AppendLine(Lang.Get("vs-dope:syringe-volume", portions / 100.0, portions / Dose));
        dsc.AppendLine(Lang.Get("vs-dope:syringe-help"));
    }
}
