using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VsDope.Items;

public class DrugConsumableItem : Item
{
    protected virtual float HealAmount => 2f;
    protected virtual float SpeedMultiplier => 0.85f;
    protected virtual float IntoxicationAmount => 4f;
    protected virtual int EffectDurationMs => 2500;
    protected virtual double EffectDurationGameHours => 0;
    protected virtual bool Psychedelic => false;
    protected virtual string EffectKey => $"vs-dope-{Code?.Path ?? "drug"}-speed";
    protected virtual string ToleranceProduct => Code?.Path ?? "drug";

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
        if (byEntity.World.Side != EnumAppSide.Server || !byEntity.Alive || slot.Empty) return;
        ApplyDose(byEntity);
        slot.TakeOut(1);
        slot.MarkDirty();
    }

    // Shared by the original consumable and reusable morphine syringes.
    public void ApplyDose(EntityAgent byEntity)
    {
        if (byEntity.World.Side != EnumAppSide.Server || !byEntity.Alive) return;
        var entity = byEntity;
        float effectMultiplier = 1f;
        IPlayer? player = null;

        if (byEntity is EntityPlayer entityPlayer)
        {
            player = byEntity.World.PlayerByUid(entityPlayer.PlayerUID);
            if (player != null)
                effectMultiplier = VsDopeModSystem.AddictionSystem.GetEffectMultiplier(player, ToleranceProduct);
        }

        float currentIntox = entity.WatchedAttributes.GetFloat("intoxication");
        entity.WatchedAttributes.SetFloat("intoxication", GameMath.Clamp(currentIntox + IntoxicationAmount, 0f, 25f));
        if (Psychedelic)
        {
            float currentPsych = entity.WatchedAttributes.GetFloat("psychedelic");
            entity.WatchedAttributes.SetFloat("psychedelic", GameMath.Clamp(currentPsych + IntoxicationAmount * 1.5f, 0f, 25f));
        }

        // EntityStats movement values are additive: +1.0 doubles speed, -0.5 halves it.
        // Tolerance scales the item's modifier back toward neutral (0).
        float effectiveSpeedModifier = (SpeedMultiplier - 1f) * effectMultiplier;
        string effectKey = EffectKey;
        entity.Stats.Set("walkspeed", effectKey, GameMath.Clamp(effectiveSpeedModifier, -0.7f, 1f));
        if (player != null) VsDopeModSystem.AddictionSystem.RecordDrugDose(player, ToleranceProduct);
        float effectiveHeal = HealAmount * effectMultiplier;
        if (effectiveHeal > 0 && (player == null || !VsDope.Systems.AddictionSystem.IsOverdosing(player)))
            entity.ReceiveDamage(new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Heal }, effectiveHeal);

        long expiresAtMs = EffectDurationGameHours > 0
            ? 0
            : entity.World.ElapsedMilliseconds + EffectDurationMs;
        double expiresAtGameHour = EffectDurationGameHours > 0
            ? entity.World.Calendar.TotalHours + EffectDurationGameHours
            : 0;
        entity.WatchedAttributes.SetLong(effectKey + "-expires", expiresAtMs);
        entity.WatchedAttributes.SetDouble(effectKey + "-expires-gamehour", expiresAtGameHour);

        void CheckEffectExpiry(float _)
        {
            if (!entity.Alive) return;

            bool expired;
            if (EffectDurationGameHours > 0)
            {
                double expiry = entity.WatchedAttributes.GetDouble(effectKey + "-expires-gamehour");
                if (expiry != expiresAtGameHour) return;
                expired = entity.World.Calendar.TotalHours >= expiry;
            }
            else
            {
                long expiry = entity.WatchedAttributes.GetLong(effectKey + "-expires");
                if (expiry != expiresAtMs) return;
                expired = entity.World.ElapsedMilliseconds >= expiry;
            }

            if (!expired)
            {
                entity.World.RegisterCallback(CheckEffectExpiry, 1000);
                return;
            }

            entity.Stats.Remove("walkspeed", effectKey);
            entity.WatchedAttributes.RemoveAttribute(effectKey + "-expires");
            entity.WatchedAttributes.RemoveAttribute(effectKey + "-expires-gamehour");
            float intox = entity.WatchedAttributes.GetFloat("intoxication");
            entity.WatchedAttributes.SetFloat("intoxication", GameMath.Max(0, intox - IntoxicationAmount));
            if (Psychedelic)
            {
                float psych = entity.WatchedAttributes.GetFloat("psychedelic");
                entity.WatchedAttributes.SetFloat("psychedelic", GameMath.Max(0, psych - IntoxicationAmount * 1.5f));
            }
        }

        entity.World.RegisterCallback(CheckEffectExpiry, EffectDurationGameHours > 0 ? 1000 : EffectDurationMs);
    }
}

public class OpiumItem : DrugConsumableItem
{
    protected override float HealAmount => 2f;
    protected override float SpeedMultiplier => 0.9f;
    protected override float IntoxicationAmount => 3f;
    protected override int EffectDurationMs => 2500;
    protected override string ToleranceProduct => "opium";
}

public class MorphineItem : DrugConsumableItem
{
    protected override float HealAmount => 5f;
    protected override float SpeedMultiplier => 0.8f;
    protected override float IntoxicationAmount => 6f;
    protected override int EffectDurationMs => 5000;
    protected override string ToleranceProduct => "morphine";
}

public class CocaVitaeItem : DrugConsumableItem
{
    protected override float HealAmount => 4f;
    protected override float SpeedMultiplier => 2.0f;
    protected override float IntoxicationAmount => 1f;
    protected override int EffectDurationMs => 30000;
    protected override double EffectDurationGameHours => 1.0;
    protected override string ToleranceProduct => "coca-vitae";
}
