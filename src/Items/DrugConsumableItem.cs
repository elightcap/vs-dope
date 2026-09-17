using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsDope.Items;

public class DrugConsumableItem : Item
{
    protected virtual float HealAmount => 2f;
    protected virtual float SlowFactor => 0.15f;
    protected virtual float IntoxicationAmount => 4f;
    protected virtual int DurationHours => 1;
    protected virtual bool Psychedelic => false;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (byEntity.World.Side == EnumAppSide.Client)
        {
            byEntity.AnimManager.StartAnimation("eat");
        }
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client && secondsUsed < 1.5f)
        {
            return true;
        }

        if (secondsUsed >= 1.5f)
        {
            Consume(slot, byEntity);
            return false;
        }

        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.World.Side == EnumAppSide.Client)
        {
            byEntity.AnimManager.StopAnimation("eat");
        }
    }

    protected virtual void Consume(ItemSlot slot, EntityAgent byEntity)
    {
        if (byEntity.World.Side != EnumAppSide.Server) return;

        var entity = byEntity;

        // Heal via damage system
        if (HealAmount > 0)
        {
            entity.ReceiveDamage(new DamageSource()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal
            }, HealAmount);
        }

        // Intoxication (drunk visual effect) — synced to client via WatchedAttributes
        float currentIntox = entity.WatchedAttributes.GetFloat("intoxication");
        entity.WatchedAttributes.SetFloat("intoxication", GameMath.Clamp(currentIntox + IntoxicationAmount, 0f, 25f));

        // Psychedelic effect (for stronger drugs)
        if (Psychedelic)
        {
            float currentPsych = entity.WatchedAttributes.GetFloat("psychedelic");
            entity.WatchedAttributes.SetFloat("psychedelic", GameMath.Clamp(currentPsych + IntoxicationAmount * 1.5f, 0f, 25f));
        }

        // Movement slowdown via walkspeed stat modifier (multiplier: 1 = normal)
        float speedMultiplier = 1f - SlowFactor;
        entity.Stats.Set("walkspeed", "vs-dope-slow", GameMath.Clamp(speedMultiplier, 0.3f, 1f));

        // Schedule effect removal after duration passes
        int durationMs = DurationHours * 2500;
        byEntity.World.RegisterCallback((_) =>
        {
            if (entity.Alive)
            {
                entity.Stats.Remove("walkspeed", "vs-dope-slow");
                float intox = entity.WatchedAttributes.GetFloat("intoxication");
                entity.WatchedAttributes.SetFloat("intoxication", GameMath.Max(0, intox - IntoxicationAmount));
                if (Psychedelic)
                {
                    float psych = entity.WatchedAttributes.GetFloat("psychedelic");
                    entity.WatchedAttributes.SetFloat("psychedelic", GameMath.Max(0, psych - IntoxicationAmount * 1.5f));
                }
            }
        }, durationMs);

        // Record addiction use
        if (byEntity is EntityPlayer entityPlayer)
        {
            var player = byEntity.World.PlayerByUid(entityPlayer.PlayerUID);
            if (player != null)
            {
                VsDopeModSystem.AddictionSystem.RecordUse(player);
            }
        }

        slot.TakeOut(1);
        slot.MarkDirty();
    }
}

public class OpiumItem : DrugConsumableItem
{
    protected override float HealAmount => 2f;
    protected override float SlowFactor => 0.1f;
    protected override float IntoxicationAmount => 3f;
    protected override int DurationHours => 1;
    protected override bool Psychedelic => false;
}

public class MorphineItem : DrugConsumableItem
{
    protected override float HealAmount => 5f;
    protected override float SlowFactor => 0.2f;
    protected override float IntoxicationAmount => 6f;
    protected override int DurationHours => 2;
    protected override bool Psychedelic => false;
}

public class HeroinItem : DrugConsumableItem
{
    protected override float HealAmount => 8f;
    protected override float SlowFactor => 0.35f;
    protected override float IntoxicationAmount => 10f;
    protected override int DurationHours => 3;
    protected override bool Psychedelic => true;
}
