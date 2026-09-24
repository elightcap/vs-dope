using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsDope;
using VsDope.Items;
using VsDope.Systems;

// Runs against the real server registry. Only the player/network endpoints are stand-ins.
public class DrugStatsProbe : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
    public override void StartServerSide(ICoreServerAPI api) =>
        api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () => Run(api));

    private void Run(ICoreServerAPI api)
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("DRUGSTATS TEST FAIL: " + name);
            checks++;
            api.Logger.Notification("DRUGSTATS TEST PASS: " + name);
        }
        bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
        float Stat(IPlayer p, string stat) => p.Entity.Stats.GetBlended(stat);
        var overdose = VsDopeModSystem.AddictionSystem.Overdose;
        var defaultRoll = overdose.Roll;
        overdose.Roll = () => 0.999999; // never overdose
        const string Mining = DrugStatEffectSystem.MiningSpeed, Hunger = DrugStatEffectSystem.HungerRate,
            Healing = DrugStatEffectSystem.HealingEffectiveness, Ranged = DrugStatEffectSystem.RangedAccuracy,
            Seeking = DrugStatEffectSystem.AnimalSeekingRange, Walk = DrugStatEffectSystem.WalkSpeed;
        var hit = new DamageSource { Source = EnumDamageSource.Entity, Type = EnumDamageType.BluntAttack };
        var poison = new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison };
        try
        {
            // ---- Every dose route applies its profile ------------------------------------
            double now = api.World.Calendar.TotalHours;
            foreach (string product in new[] { "opium", "morphine", "coca-vitae", "heroin" })
            {
                var p = MakePlayer(api);
                if (product == "heroin") VsDopeModSystem.AddictionSystem.ApplyHeroinSyringeDose(p);
                else ((DrugConsumableItem)api.World.GetItem(new AssetLocation("vs-dope:" + product))!).ApplyDose(p.Entity);
                var profile = DrugStatEffectSystem.Profiles[product];
                foreach (var stat in profile.Stats)
                    Check(Near(Stat(p, stat.Stat), 1 + stat.Value), product + " sets " + stat.Stat + " to " + (1 + stat.Value));
                Check(Math.Abs(p.Entity.WatchedAttributes.GetDouble(DrugStatEffectSystem.ExpiryKey(product)) - now - profile.DurationHours) < 0.001,
                    product + " lasts " + profile.DurationHours + " game hours");
                Check(Near(DrugStatEffectSystem.Mitigate(p.Entity, 10, hit, now), 10 * (1 - profile.DamageMitigation)),
                    product + " mitigates outside damage by " + profile.DamageMitigation);
                Check(Near(DrugStatEffectSystem.Mitigate(p.Entity, 10, poison, now), 10), product + " never mitigates internal poison");
            }
            Check(DrugStatEffectSystem.Profiles.ContainsKey("heroin") && !DrugStatEffectSystem.Profiles.ContainsKey("marijuana"),
                "marijuana stats are owned by StonedSystem");

            // ---- Tolerance scaling, refresh without stacking, expiry ---------------------
            var opium = MakePlayer(api);
            DrugStatEffectSystem.Apply(opium.Entity, "opium", 0.5f, 100);
            Check(Near(Stat(opium, Healing), 1.10f) && Near(Stat(opium, Ranged), 0.90f), "tolerance multiplier scales stat values");
            Check(Near(DrugStatEffectSystem.Mitigate(opium.Entity, 10, hit, 100), 9.5f), "tolerance multiplier scales mitigation");
            DrugStatEffectSystem.Apply(opium.Entity, "opium", 1f, 100.5);
            DrugStatEffectSystem.Apply(opium.Entity, "opium", 1f, 100.5);
            Check(Near(Stat(opium, Healing), 1.20f), "re-dosing refreshes without stacking");
            DrugStatEffectSystem.Apply(opium.Entity, "heroin", 1f, 100.5);
            Check(Near(DrugStatEffectSystem.Mitigate(opium.Entity, 10, hit, 100.6), 7f), "mixed opioids use the strongest mitigation, not the sum");
            DrugStatEffectSystem.Tick(opium.Entity, 101.4);
            Check(DrugStatEffectSystem.IsActive(opium.Entity, "opium", 101.4), "opium active before its refreshed expiry");
            DrugStatEffectSystem.Tick(opium.Entity, 101.6);
            Check(!DrugStatEffectSystem.IsActive(opium.Entity, "opium", 101.6) && Near(Stat(opium, Healing), 1.5f),
                "expired opium is removed while heroin remains");
            DrugStatEffectSystem.Tick(opium.Entity, 102.6);
            Check(Near(Stat(opium, Healing), 1f) && Near(Stat(opium, Ranged), 1f) && Near(DrugStatEffectSystem.Mitigate(opium.Entity, 10, hit, 102.6), 10),
                "all opioid effects gone after expiry");

            // ---- Coca high -> crash ------------------------------------------------------
            var coca = MakePlayer(api);
            DrugStatEffectSystem.Apply(coca.Entity, "coca-vitae", 1f, 200);
            Check(Near(Stat(coca, Mining), 1.4f) && Near(Stat(coca, Hunger), 0.7f), "coca high: faster mining, lower hunger");
            DrugStatEffectSystem.Tick(coca.Entity, 201.2);
            Check(Near(Stat(coca, Mining), 1f) && Near(Stat(coca, Hunger), 1.4f) && Near(Stat(coca, Walk), 0.85f),
                "coca crash: hunger up, slower movement");
            Check(Math.Abs(coca.Entity.WatchedAttributes.GetDouble(DrugStatEffectSystem.ExpiryKey("coca-crash")) - 202) < 0.001,
                "crash runs from the end of the high, not from the late tick");
            DrugStatEffectSystem.Apply(coca.Entity, "coca-vitae", 1f, 201.5);
            Check(Near(Stat(coca, Hunger), 0.7f) && Near(Stat(coca, Walk), 1f), "a new dose replaces the crash");
            DrugStatEffectSystem.Tick(coca.Entity, 205);
            Check(Near(Stat(coca, Hunger), 1f) && Near(Stat(coca, Walk), 1f) && !DrugStatEffectSystem.IsActive(coca.Entity, "coca-crash", 205),
                "a high that expired long ago (offline) does not leave a crash running");

            // ---- Resume / clear ----------------------------------------------------------
            var relog = MakePlayer(api);
            DrugStatEffectSystem.Apply(relog.Entity, "morphine", 0.5f, 300);
            relog.Entity.Stats.Remove(Healing, DrugStatEffectSystem.StatKey("morphine"));
            relog.Entity.Stats.Remove(Ranged, DrugStatEffectSystem.StatKey("morphine"));
            DrugStatEffectSystem.Resume(relog.Entity, 300.5);
            Check(Near(Stat(relog, Healing), 1.175f), "resume restores dose-time strength");
            DrugStatEffectSystem.Clear(relog.Entity);
            Check(Near(Stat(relog, Healing), 1f) && !DrugStatEffectSystem.IsActive(relog.Entity, "morphine", 300.5), "clear removes everything");

            // ---- Marijuana (StonedSystem) ------------------------------------------------
            var stoned = MakePlayer(api);
            StonedSystem.Apply(stoned.Entity, 400);
            Check(Near(Stat(stoned, Hunger), 1.3f) && Near(Stat(stoned, Seeking), 0.65f) && Near(Stat(stoned, Walk), 0.8f),
                "stoned: hunger up, mobs notice later, slower");
            StonedSystem.Tick(stoned.Entity, 402.1);
            Check(Near(Stat(stoned, Hunger), 1f) && Near(Stat(stoned, Seeking), 1f) && Near(Stat(stoned, Walk), 1f), "stoned stats end on expiry");

            api.Logger.Notification("DRUGSTATS TEST SUMMARY: " + checks + " checks passed");
        }
        catch (Exception ex) { api.Logger.Error("DRUGSTATS TEST FAILED: " + ex); }
        finally { overdose.Roll = defaultRoll; }
    }

    private static IServerPlayer MakePlayer(ICoreServerAPI api)
    {
        var entity = new ProbePlayer { Api = api, Alive = true };
        entity.WatchedAttributes.SetString("playerUID", Guid.NewGuid().ToString());
        entity.Stats = new EntityStats(entity);
        entity.AnimManager = Proxy<IAnimationManager>.Create((m, args) => Default(m.ReturnType));
        var inventory = Proxy<IPlayerInventoryManager>.Create((m, args) => m.Name switch
        {
            "get_Inventories" => new Dictionary<string, IInventory>(),
            "get_InventoriesOrdered" => Array.Empty<InventoryBase>(),
            "TryGiveItemstack" => true,
            _ => Default(m.ReturnType)
        });
        var player = TestServerPlayer.Create(entity, inventory);
        entity.World = Proxy<IServerWorldAccessor>.Create((m, args) => m.Name switch
        {
            "PlayerByUid" => player,
            "RegisterCallback" => 0L,
            _ => m.Invoke(api.World, args)
        });
        return player;
    }

    private static object? Default(Type t) => t == typeof(void) || !t.IsValueType ? null : Activator.CreateInstance(t);
}

public class ProbePlayer : EntityPlayer
{
    public override IAnimationManager AnimManager { get; set; } = null!;
    public override bool ReceiveDamage(DamageSource source, float amount) => true;
    // No entity type is loaded, so there are no behaviors (the crash's hunger cost is skipped).
    public override TEntityBehavior? GetBehavior<TEntityBehavior>() where TEntityBehavior : class => null;
}

public class Proxy<T> : DispatchProxy where T : class
{
    private System.Func<MethodInfo, object?[]?, object?> handler = null!;
    public static T Create(System.Func<MethodInfo, object?[]?, object?> handler)
    {
        var proxy = Create<T, Proxy<T>>();
        ((Proxy<T>)(object)proxy).handler = handler;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => handler(targetMethod!, args);
}
