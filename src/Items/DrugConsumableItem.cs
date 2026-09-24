using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using VsDope.Systems;

namespace VsDope.Items;

public class DrugConsumableItem : Item
{
    protected virtual float HealAmount => 2f;
    protected virtual float SpeedMultiplier => 0.85f;
    protected virtual int EffectDurationMs => 2500;
    protected virtual double EffectDurationGameHours => 0;
    protected virtual string EffectKey => $"vs-dope-{Code?.Path ?? "drug"}-speed";
    protected virtual string ToleranceProduct => Code?.Path ?? "drug";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        // A right-click aimed at an interactable entity belongs to that entity, not to what
        // we are holding. The client drops the entity interaction entirely once the held item
        // claims the click (SystemMouseInWorldInteractions.HandleMouseInteractionsNoBlockSelected),
        // so leaving `handling` alone here is what lets the addict's trade window open while
        // the player is holding the very drugs they came to sell.
        if (entitySel?.Entity?.IsInteractable == true) return;

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

        // Screen effects are per product (see DrugVisualEffects) and fade through vanilla detox,
        // so they are not removed again when the movement effect expires.
        DrugVisualEffects.Apply(entity, ToleranceProduct, 1f, effectMultiplier);

        // EntityStats movement values are additive: +1.0 doubles speed, -0.5 halves it.
        // Tolerance scales the item's modifier back toward neutral (0).
        float effectiveSpeedModifier = (SpeedMultiplier - 1f) * effectMultiplier;
        string effectKey = EffectKey;
        entity.Stats.Set("walkspeed", effectKey, GameMath.Clamp(effectiveSpeedModifier, -0.7f, 1f));
        if (player != null) VsDopeModSystem.AddictionSystem.RecordDrugDose(player, ToleranceProduct);
        float effectiveHeal = HealAmount * effectMultiplier;
        if (effectiveHeal > 0 && (player == null || !AddictionSystem.IsOverdosing(player)))
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
        }

        entity.World.RegisterCallback(CheckEffectExpiry, EffectDurationGameHours > 0 ? 1000 : EffectDurationMs);
    }
}

public class OpiumItem : DrugConsumableItem
{
    protected override float HealAmount => 2f;
    protected override float SpeedMultiplier => 0.9f;
    protected override int EffectDurationMs => 2500;
    protected override string ToleranceProduct => "opium";
}

public class MorphineItem : DrugConsumableItem
{
    protected override float HealAmount => 5f;
    protected override float SpeedMultiplier => 0.8f;
    protected override int EffectDurationMs => 5000;
    protected override string ToleranceProduct => "morphine";
}

public class CocaVitaeItem : DrugConsumableItem
{
    protected override float HealAmount => 4f;
    protected override float SpeedMultiplier => 2.0f;
    protected override int EffectDurationMs => 30000;
    protected override double EffectDurationGameHours => 1.0;
    protected override string ToleranceProduct => "coca-vitae";
}
