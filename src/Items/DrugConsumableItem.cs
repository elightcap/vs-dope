using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsDope.Items;

public class DrugConsumableItem : Item
{
    protected virtual float HealAmount => 2f;
    protected virtual float SpeedMultiplier => 0.85f;
    protected virtual float IntoxicationAmount => 4f;
    protected virtual int EffectDurationMs => 2500;
    protected virtual bool Psychedelic => false;
    protected virtual string EffectKey => $"vs-dope-{Code?.Path ?? "drug"}-speed";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (byEntity.World.Side == EnumAppSide.Client) byEntity.AnimManager.StartAnimation("eat");
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client && secondsUsed < 1.5f) return true;
        if (secondsUsed >= 1.5f) { Consume(slot, byEntity); return false; }
        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client) byEntity.AnimManager.StopAnimation("eat");
    }

    protected virtual void Consume(ItemSlot slot, EntityAgent byEntity)
    {
        if (byEntity.World.Side != EnumAppSide.Server) return;
        var entity = byEntity;
        if (HealAmount > 0) entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, HealAmount);

        float currentIntox = entity.WatchedAttributes.GetFloat("intoxication");
        entity.WatchedAttributes.SetFloat("intoxication", GameMath.Clamp(currentIntox + IntoxicationAmount, 0f, 25f));
        if (Psychedelic)
        {
            float currentPsych = entity.WatchedAttributes.GetFloat("psychedelic");
            entity.WatchedAttributes.SetFloat("psychedelic", GameMath.Clamp(currentPsych + IntoxicationAmount * 1.5f, 0f, 25f));
        }

        string effectKey = EffectKey;
        entity.Stats.Set("walkspeed", effectKey, GameMath.Clamp(SpeedMultiplier, 0.3f, 2f));
        entity.WatchedAttributes.SetLong(effectKey + "-expires", entity.World.ElapsedMilliseconds + EffectDurationMs);

        byEntity.World.RegisterCallback(_ =>
        {
            if (!entity.Alive) return;
            long expires = entity.WatchedAttributes.GetLong(effectKey + "-expires");
            if (entity.World.ElapsedMilliseconds < expires) return; // a later dose refreshed this effect

            entity.Stats.Remove("walkspeed", effectKey);
            entity.WatchedAttributes.RemoveAttribute(effectKey + "-expires");
            float intox = entity.WatchedAttributes.GetFloat("intoxication");
            entity.WatchedAttributes.SetFloat("intoxication", GameMath.Max(0, intox - IntoxicationAmount));
            if (Psychedelic)
            {
                float psych = entity.WatchedAttributes.GetFloat("psychedelic");
                entity.WatchedAttributes.SetFloat("psychedelic", GameMath.Max(0, psych - IntoxicationAmount * 1.5f));
            }
        }, EffectDurationMs);

        if (byEntity is EntityPlayer entityPlayer)
        {
            var player = byEntity.World.PlayerByUid(entityPlayer.PlayerUID);
            if (player != null) VsDopeModSystem.AddictionSystem.RecordUse(player);
        }

        slot.TakeOut(1);
        slot.MarkDirty();
    }
}

public class OpiumItem : DrugConsumableItem
{
    protected override float HealAmount => 2f;
    protected override float SpeedMultiplier => 0.9f;
    protected override float IntoxicationAmount => 3f;
    protected override int EffectDurationMs => 2500;
}

public class MorphineItem : DrugConsumableItem
{
    protected override float HealAmount => 5f;
    protected override float SpeedMultiplier => 0.8f;
    protected override float IntoxicationAmount => 6f;
    protected override int EffectDurationMs => 5000;
}

public class CocaVitaeItem : DrugConsumableItem
{
    protected override float HealAmount => 4f;
    protected override float SpeedMultiplier => 1.15f;
    protected override float IntoxicationAmount => 1f;
    protected override int EffectDurationMs => 30000;
}
