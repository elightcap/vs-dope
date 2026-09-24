using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsDope;
using VsDope.Items;
using VsDope.Systems;

internal static class ToolEffectChecks
{
    internal static void Run(ICoreServerAPI api, Action<bool, string> check, System.Func<IServerPlayer> makePlayer)
    {
        bool Near(double a, double b) => Math.Abs(a - b) < 0.0001;
        double now = api.World.Calendar.TotalHours;
        var addiction = VsDopeModSystem.AddictionSystem;
        void Dose(IPlayer p, string product) => ((DrugConsumableItem)api.World.GetItem(new AssetLocation("vs-dope:" + product))!).ApplyDose(p.Entity);
        EntityBehaviorHunger Hunger(IPlayer p)
        {
            ((ProbePlayer)p.Entity).SetProperties(api.World.GetEntityType(new AssetLocation("game:player"))!.Clone());
            var health = new EntityBehaviorHealth(p.Entity);
            health.Initialize(p.Entity.Properties, JsonObject.FromJson("{}"));
            p.Entity.AddBehavior(health);
            var hunger = new EntityBehaviorHunger(p.Entity);
            hunger.Initialize(p.Entity.Properties, JsonObject.FromJson("{}"));
            p.Entity.AddBehavior(hunger);
            return hunger;
        }

        var coca = makePlayer();
        var h = Hunger(coca);
        h.Saturation = 1000;
        var stone = api.World.Blocks.First(b => b.Code?.Path == "rock-granite");
        var pick = api.World.Items.First(i => i.Code?.Path == "pickaxe-copper");
        var pickStack = new ItemStack(pick);
        var selection = new BlockSelection { Position = new BlockPos(0, 1, 0) };
        float miningBefore = pick.GetMiningSpeed(pickStack, selection, stone, coca);
        Dose(coca, "coca-vitae");
        check(Near(coca.Entity.Stats.GetBlended("walkspeed"), 2), "tools: coca retains double speed");
        check(Near(pick.GetMiningSpeed(pickStack, selection, stone, coca), miningBefore * 1.25), "tools: native pickaxe mines stone 25% faster");
        check(Near(coca.Entity.Stats.GetBlended("hungerrate"), 0.8), "tools: coca hunger rate is 80%");
        check(Near(coca.Entity.WatchedAttributes.GetDouble(DrugToolEffects.ExpiryKey("coca-vitae")), now + 1), "tools: coca uses one game hour");
        Dose(coca, "coca-vitae");
        check(Near(coca.Entity.Stats.GetBlended("miningSpeedMul"), 1.25), "tools: repeated coca refreshes without stacking");
        DrugToolEffects.Tick(coca.Entity, now);
        check(Near(h.Saturation, 1000), "tools: paused active clock charges no crash cost");
        DrugToolEffects.Tick(coca.Entity, now + 1);
        check(Near(h.Saturation, 850) && Near(coca.Entity.Stats.GetBlended("walkspeed"), 0.8), "tools: coca expiration charges 150 satiety and starts 20% slow");
        check(Near(coca.Entity.Stats.GetBlended("miningSpeedMul"), 1) && Near(coca.Entity.Stats.GetBlended("hungerrate"), 1), "tools: coca benefits removed at expiry");
        DrugToolEffects.Tick(coca.Entity, now + 1.1);
        DrugToolEffects.Resume(coca.Entity, now + 1.1);
        check(Near(h.Saturation, 850), "tools: repeat tick and reconnect cannot charge crash twice");
        DrugToolEffects.Tick(coca.Entity, now + 1.5);
        check(Near(coca.Entity.Stats.GetBlended("walkspeed"), 1) && !coca.Entity.WatchedAttributes.HasAttribute(DrugToolEffects.CrashExpiryKey), "tools: crash ends after half a game hour");

        var tolerant = makePlayer();
        var th = Hunger(tolerant);
        th.Saturation = 1000;
        tolerant.Entity.Attributes.SetFloat(AddictionSystem.ToleranceKey("coca-vitae"), 0.75f);
        Dose(tolerant, "coca-vitae");
        check(Near(tolerant.Entity.Stats.GetBlended("miningSpeedMul"), 1.0625) && Near(tolerant.Entity.Stats.GetBlended("hungerrate"), 0.95), "tools: coca utility uses pre-dose tolerance");
        tolerant.Entity.Stats = new EntityStats(tolerant.Entity);
        DrugToolEffects.Resume(tolerant.Entity, now + 0.5);
        check(Near(tolerant.Entity.Stats.GetBlended("walkspeed"), 1.25) && Near(tolerant.Entity.Stats.GetBlended("miningSpeedMul"), 1.0625), "tools: active reconnect restores original strength and coca speed");
        DrugToolEffects.Tick(tolerant.Entity, now + 1);
        check(Near(th.Saturation, 962.5) && Near(tolerant.Entity.Stats.GetBlended("walkspeed"), 0.95), "tools: tolerance scales both crash cost and slow");

        var offline = makePlayer();
        var oh = Hunger(offline);
        oh.Saturation = 100;
        Dose(offline, "coca-vitae");
        DrugToolEffects.Resume(offline.Entity, now + 10);
        check(Near(oh.Saturation, 0) && Near(offline.Entity.Stats.GetBlended("walkspeed"), 1), "tools: offline expiry settles one bounded cost without restarting crash");
        oh.Saturation = 50;
        DrugToolEffects.Resume(offline.Entity, now + 11);
        check(Near(oh.Saturation, 50), "tools: completed offline crash cannot charge again");

        var redose = makePlayer();
        var rh = Hunger(redose);
        rh.Saturation = 1000;
        Dose(redose, "coca-vitae");
        redose.Entity.WatchedAttributes.SetDouble(DrugToolEffects.ExpiryKey("coca-vitae"), now - 0.1);
        Dose(redose, "coca-vitae");
        check(Near(rh.Saturation, 850) && Near(redose.Entity.Stats.GetBlended("walkspeed"), 1.8), "tools: dose after unprocessed expiry pays the crash and retains its slow");

        foreach (string product in new[] { "opium", "morphine" })
        {
            var p = makePlayer();
            bool morphine = product == "morphine";
            Dose(p, product);
            double duration = morphine ? 1 : 0.5;
            check(Near(p.Entity.WatchedAttributes.GetDouble(DrugToolEffects.ExpiryKey(product)), now + duration), "tools: " + product + " calendar duration");
            check(Near(p.Entity.Stats.GetBlended("healingeffectivness"), morphine ? 1.3 : 1.15)
                && Near(p.Entity.Stats.GetBlended("rangedWeaponsAcc"), morphine ? 0.7 : 0.85)
                && Near(p.Entity.Stats.GetBlended("animalSeekingRange"), morphine ? 1.25 : 1.15), "tools: " + product + " healing, accuracy and detection trade-offs");
            var poultice = api.World.GetItem(new AssetLocation("game:poultice-reed-horsetail"))!;
            var healingBehavior = poultice.GetBehavior<CollectibleBehaviorHealingItem>();
            var nativeHealth = new EntityBehaviorHealth(p.Entity);
            ((ProbePlayer)p.Entity).NativeHealth = nativeHealth;
            var poulticeSlot = new DummySlot(new ItemStack(poultice));
            poultice.OnHeldInteractStop(20, poulticeSlot, p.Entity, null!, null!);
            ((ProbePlayer)p.Entity).NativeHealth = null;
            check(poulticeSlot.Empty && nativeHealth.ActiveDoTEffects.Count == 1
                && Near(nativeHealth.ActiveDoTEffects[0].Damage * nativeHealth.ActiveDoTEffects[0].TicksLeft,
                    healingBehavior.Health * (morphine ? 1.3 : 1.15)), "tools: native 1.22.7 poultice schedules " + product + " healing bonus");
            Dose(p, product);
            check(Near(p.Entity.Stats.GetBlended("rangedWeaponsAcc"), morphine ? 0.7 : 0.85), "tools: " + product + " penalty does not stack on refresh");
            p.Entity.Stats = new EntityStats(p.Entity);
            DrugToolEffects.Resume(p.Entity, now + 0.1);
            check(Near(p.Entity.Stats.GetBlended("healingeffectivness"), morphine ? 1.3 : 1.15), "tools: " + product + " restored on reconnect");
            DrugToolEffects.Tick(p.Entity, now + duration);
            check(Near(p.Entity.Stats.GetBlended("healingeffectivness"), 1) && Near(p.Entity.Stats.GetBlended("rangedWeaponsAcc"), 1)
                && Near(p.Entity.Stats.GetBlended("animalSeekingRange"), 1), "tools: " + product + " all modifiers clear at expiry");
        }

        var protectedPlayer = makePlayer();
        Dose(protectedPlayer, "opium");
        var health = new EntityBehaviorHealth(protectedPlayer.Entity);
        float observed = -1;
        // Native method invokes its onDamaged delegate after our installed prefix.
        // Stop at the delegate so no simulated player/network health plumbing is needed.
        health.onDamaged += (damage, source) => { observed = damage; return 0; };
        void Attack(EnumDamageSource source, EnumDamageType type)
        {
            float damage = 10;
            health.OnEntityReceiveDamage(new DamageSource { Source = source, Type = type }, ref damage);
        }
        Attack(EnumDamageSource.Entity, EnumDamageType.BluntAttack);
        check(Near(observed, 9), "tools: native health pipeline mitigates opium physical damage by 10%");
        Dose(protectedPlayer, "morphine");
        Attack(EnumDamageSource.Player, EnumDamageType.PiercingAttack);
        check(Near(observed, 8), "tools: mixed opiates choose strongest protection, including player attacks");
        foreach (var type in new[] { EnumDamageType.Poison, EnumDamageType.Hunger, EnumDamageType.Fire, EnumDamageType.Gravity })
        {
            Attack(EnumDamageSource.Internal, type);
            check(Near(observed, 10), "tools: protection does not reduce " + type);
        }
        DrugToolEffects.Clear(protectedPlayer.Entity);
        protectedPlayer.Entity.Attributes.SetFloat(AddictionSystem.ToleranceKey("morphine"), 0.75f);
        Dose(protectedPlayer, "morphine");
        Attack(EnumDamageSource.Entity, EnumDamageType.SlashingAttack);
        check(Near(observed, 9.5), "tools: tolerance scales native damage protection");
        DrugToolEffects.Clear(protectedPlayer.Entity);
        Attack(EnumDamageSource.Entity, EnumDamageType.BluntAttack);
        check(Near(observed, 10), "tools: clearing protection restores native damage");

        var injected = makePlayer();
        var syringe = (SyringeItem)api.World.GetItem(new AssetLocation("vs-dope:syringe-morphine"))!;
        var syringeSlot = new DummySlot(new ItemStack(syringe));
        syringeSlot.Itemstack!.TempAttributes.SetBool("vs-dope-syringe-applying", true);
        syringe.OnHeldInteractStop(1.5f, syringeSlot, injected.Entity, null!, null!);
        check(Near(injected.Entity.Stats.GetBlended("healingeffectivness"), 1.3), "tools: morphine syringe enters the shared tool-effect path");

        var marijuana = makePlayer();
        StonedSystem.Apply(marijuana.Entity, now);
        check(Near(marijuana.Entity.Stats.GetBlended("hungerrate"), 1.25) && Near(marijuana.Entity.Stats.GetBlended("animalSeekingRange"), 0.75), "tools: marijuana hunger and detection trade-offs");
        check(OverdoseSystem.RecentDoseHours(marijuana).Count == 0 && marijuana.Entity.Attributes.GetInt("vs-dope-days-used") == 0, "tools: marijuana does not create overdose or opiate addiction");
        check(marijuana.Entity.Attributes.GetInt("vs-dope-tolerance-uses-marijuana") == 1, "tools: marijuana records product tolerance once");
        marijuana.Entity.Attributes.SetFloat(AddictionSystem.ToleranceKey("marijuana"), 0.75f);
        StonedSystem.Apply(marijuana.Entity, now);
        StonedSystem.Tick(marijuana.Entity, now + 1.0 / 60);
        check(Near(marijuana.Entity.Stats.GetBlended("hungerrate"), 1.0625) && Near(marijuana.Entity.Stats.GetBlended("walkspeed"), 0.95)
            && Near(((ProbePlayer)marijuana.Entity).Healing, 0.125), "tools: marijuana tolerance scales hunger, movement and healing");
        marijuana.Entity.Stats = new EntityStats(marijuana.Entity);
        StonedSystem.Resume(marijuana.Entity, now + 0.5);
        check(Near(marijuana.Entity.Stats.GetBlended("animalSeekingRange"), 0.9375), "tools: marijuana reconnect restores dose strength");
        StonedSystem.Tick(marijuana.Entity, now + 2);
        check(Near(marijuana.Entity.Stats.GetBlended("hungerrate"), 1) && Near(marijuana.Entity.Stats.GetBlended("animalSeekingRange"), 1), "tools: marijuana expiry cleans up utility stats");

        var smoking = makePlayer();
        var bong = (BongItem)api.World.GetItem(new AssetLocation("vs-dope:bong-loaded"))!;
        var joint = (JointItem)api.World.GetItem(new AssetLocation("vs-dope:joint"))!;
        for (int dose = 0; dose < 4; dose++)
        {
            Item item = dose % 2 == 0 ? bong : joint;
            var slot = new DummySlot(new ItemStack(item));
            var handling = EnumHandHandling.NotHandled;
            item.OnHeldInteractStart(slot, smoking.Entity, null!, null!, true, ref handling);
            item.OnHeldInteractStop(5, slot, smoking.Entity, null!, null!);
            item.OnHeldInteractStop(5, slot, smoking.Entity, null!, null!);
            if (dose % 2 == 0) check(slot.Itemstack?.Collectible.Code.Path == "bong-empty", "tools: tolerant smoking still returns one empty bong");
        }
        check(smoking.Entity.Attributes.GetInt("vs-dope-tolerance-uses-marijuana") == 4
            && Near(StonedSystem.Strength(smoking.Entity), 0.92), "tools: joints and bongs share tolerance and duplicate stops never count twice");

        var death = makePlayer();
        death.Entity.Stats.Set("hungerrate", "other-mod", 0.1f);
        Dose(death, "coca-vitae"); Dose(death, "opium"); Dose(death, "morphine");
        StonedSystem.Apply(death.Entity, now);
        death.Entity.Alive = false;
        DrugToolEffects.Tick(death.Entity, now);
        StonedSystem.Tick(death.Entity, now);
        check(Near(death.Entity.Stats.GetBlended("hungerrate"), 1.1), "tools: death cleanup preserves other mods' modifiers");
        check(!death.Entity.WatchedAttributes.HasAttribute(DrugToolEffects.CrashExpiryKey)
            && !death.Entity.WatchedAttributes.HasAttribute(DrugToolEffects.ExpiryKey("opium"))
            && !death.Entity.WatchedAttributes.HasAttribute(StonedSystem.StrengthKey), "tools: death clears tool timers without triggering crash");
        check(death.Entity.Attributes.GetInt("vs-dope-tolerance-uses-marijuana") == 1, "tools: death preserves long-term tolerance");
    }
}
