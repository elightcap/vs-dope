using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using VsDope.Systems;

namespace VsDope.Items;

public sealed class BongItem : Item
{
    public const float SmokeSeconds = JointItem.SmokeSeconds;
    private const float BubbleStartSeconds = 0.9f;
    private const string UsingKey = "vs-dope-bong-using";
    private const string SoundKey = "vs-dope-bong-bubbling";
    private static readonly AssetLocation BubbleSound = new("vs-dope:sounds/player/bong-bubbles");
    private static readonly AssetLocation EmptyCode = new("vs-dope:bong-empty");
    public bool Loaded => Variant["contents"] == "loaded";

    private bool CanSmoke(ItemSlot slot, EntityAgent entity) =>
        Loaded && entity.Alive && slot.StackSize == 1 && slot.Itemstack?.Collectible == this;

    public override string GetHeldTpUseAnimation(ItemSlot slot, Entity entity) =>
        Loaded && slot.Itemstack?.TempAttributes.GetBool(UsingKey) == true ? JointItem.AnimationCode : null!;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        var stack = slot.Itemstack;
        if (entitySel?.Entity?.IsInteractable == true || stack == null || !CanSmoke(slot, byEntity)) return;
        handling = EnumHandHandling.PreventDefault;
        if (!firstEvent) return;
        stack.TempAttributes.SetBool(UsingKey, true);
        stack.TempAttributes.SetBool(SoundKey, false);
        byEntity.AnimManager.StartAnimation(JointItem.AnimationCode);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        var stack = slot.Itemstack;
        if (stack == null || !CanSmoke(slot, byEntity) || !stack.TempAttributes.GetBool(UsingKey)) return false;
        if (secondsUsed >= BubbleStartSeconds && byEntity.World.Side == EnumAppSide.Server &&
            !stack.TempAttributes.GetBool(SoundKey))
        {
            stack.TempAttributes.SetBool(SoundKey, true);
            byEntity.World.PlaySoundAt(BubbleSound, byEntity, null, false, 12, 0.6f);
        }
        return secondsUsed < SmokeSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        var stack = slot.Itemstack;
        bool completed = stack != null && CanSmoke(slot, byEntity) && stack.TempAttributes.GetBool(UsingKey);
        Stop(slot, byEntity);
        if (!completed || stack == null || secondsUsed < SmokeSeconds || byEntity.World.Side != EnumAppSide.Server) return;
        Item? empty = byEntity.World.GetItem(EmptyCode);
        if (empty == null) return;

        // Both variants are non-stackable: use the existing inventory slot, even when all others are full.
        var returned = new ItemStack(empty) { Attributes = stack.Attributes.Clone() };
        slot.Itemstack = returned;
        slot.MarkDirty();
        StonedSystem.Apply(byEntity, byEntity.World.Calendar.TotalHours);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        Stop(slot, byEntity);
        return true;
    }

    private static void Stop(ItemSlot slot, EntityAgent entity)
    {
        entity.AnimManager.StopAnimation(JointItem.AnimationCode);
        slot.Itemstack?.TempAttributes.RemoveAttribute(UsingKey);
        slot.Itemstack?.TempAttributes.RemoveAttribute(SoundKey);
    }

    public override void GetHeldItemInfo(ItemSlot slot, StringBuilder description, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(slot, description, world, withDebugInfo);
        if (Loaded)
        {
            description.AppendLine(Lang.Get("vs-dope:joint-tooltip", SmokeSeconds));
            description.AppendLine(Lang.Get("vs-dope:bong-return-tooltip"));
        }
        else description.AppendLine(Lang.Get("vs-dope:bong-empty-tooltip"));
    }
}
