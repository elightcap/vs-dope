using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using VsDope.Systems;

namespace VsDope.Items;

public sealed class JointItem : Item
{
    public const float SmokeSeconds = 5f;
    private const string UsingKey = "vs-dope-joint-using";
    private const string DragKey = "vs-dope-joint-drag";
    public const string AnimationCode = "vsdope-smoke";
    private static readonly AssetLocation DragSound = new("vs-dope:sounds/player/joint-drag");

    public override string GetHeldTpUseAnimation(ItemSlot slot, Entity entity) =>
        slot.Itemstack?.TempAttributes.GetBool(UsingKey) == true ? AnimationCode : null!;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (entitySel?.Entity?.IsInteractable == true) return;
        handling = EnumHandHandling.PreventDefault;
        if (!firstEvent || slot.Empty || !byEntity.Alive) return;
        slot.Itemstack.TempAttributes.SetBool(UsingKey, true);
        slot.Itemstack.TempAttributes.SetBool(DragKey, false);
        byEntity.AnimManager.StartAnimation(AnimationCode);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        if (!byEntity.Alive || slot.Itemstack?.Collectible != this || !slot.Itemstack.TempAttributes.GetBool(UsingKey)) return false;
        // Server broadcasts once after the hand reaches the face. Do not also play on the owner client.
        if (secondsUsed >= .9f && byEntity.World.Side == EnumAppSide.Server && !slot.Itemstack.TempAttributes.GetBool(DragKey))
        {
            slot.Itemstack.TempAttributes.SetBool(DragKey, true);
            byEntity.World.PlaySoundAt(DragSound, byEntity, null, false, 12, .6f);
        }
        return secondsUsed < SmokeSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        bool started = slot.Itemstack?.Collectible == this && slot.Itemstack.TempAttributes.GetBool(UsingKey);
        Stop(slot, byEntity);
        if (!started || secondsUsed < SmokeSeconds || !byEntity.Alive || byEntity.World.Side != EnumAppSide.Server) return;
        slot.TakeOut(1);
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
        entity.AnimManager.StopAnimation(AnimationCode);
        slot.Itemstack?.TempAttributes.RemoveAttribute(UsingKey);
        slot.Itemstack?.TempAttributes.RemoveAttribute(DragKey);
    }

    public override void GetHeldItemInfo(ItemSlot slot, StringBuilder description, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(slot, description, world, withDebugInfo);
        description.AppendLine(Lang.Get("vs-dope:joint-tooltip", SmokeSeconds));
    }
}
