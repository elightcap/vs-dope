using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsDope;
using VsDope.Items;
using VsDope.Systems;

// Runs with the actual server registry, native containers and installed Harmony hook.
// Only the player/network endpoints are stand-ins; no account or client is required.
public class OverdoseProbe : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
    public override void StartServerSide(ICoreServerAPI api) =>
        api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () => Run(api));

    private void Run(ICoreServerAPI api)
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("OVERDOSE TEST FAIL: " + name);
            checks++;
            api.Logger.Notification("OVERDOSE TEST PASS: " + name);
        }
        bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
        var system = VsDopeModSystem.AddictionSystem;
        var overdose = system.Overdose;
        var defaultRoll = overdose.Roll;
        void Never() => overdose.Roll = () => 0.999999;
        void Always() => overdose.Roll = () => 0.0;
        int Recent(IPlayer p) => OverdoseSystem.RecentDoseHours(p).Count;
        float Severity(IPlayer p) => p.Entity.WatchedAttributes.GetFloat(OverdoseSystem.WatchSeverity);
        try
        {
            // ---- Chance math -----------------------------------------------------------
            Check(Near(OverdoseSystem.ChanceFor("heroin", 0, 0), 0.02f), "single heroin dose carries a 2% base chance");
            Check(Near(OverdoseSystem.ChanceFor("heroin", 9, 0), 0.47f), "tenth rapid heroin dose rolls 47%");
            Check(Near(OverdoseSystem.ChanceFor("heroin", 9, 0.75f), 0.47f * 0.7f), "maximum tolerance reduces chance by 30%");
            Check(Near(OverdoseSystem.ChanceFor("heroin", 100, 0), OverdoseSystem.MaxChance), "chance capped");
            Check(OverdoseSystem.ChanceFor("unknown", 5, 0) == 0, "unknown products never roll");
            foreach (string product in OverdoseSystem.Risks.Keys)
                Check(OverdoseSystem.ChanceFor(product, 0, 0) <= 0.02f, product + " single dose is a small risk");
            double noOd = 1;
            for (int i = 0; i < 10; i++) noOd *= 1 - OverdoseSystem.ChanceFor("heroin", i, 0);
            Check(noOd < 0.1, "ten rapid heroin shots without tolerance overdose with >90% probability (no-OD " + noOd.ToString("0.000") + ")");

            // ---- Every route rolls once per dose and keeps the overdose outcome ---------
            foreach (string product in new[] { "opium", "morphine", "coca-vitae", "heroin" })
            {
                var p = MakePlayer(api);
                var e = (ProbePlayer)p.Entity;
                void Dose()
                {
                    if (product == "heroin") system.ApplyHeroinSyringeDose(p);
                    else ((DrugConsumableItem)api.World.GetItem(new AssetLocation("vs-dope:" + product))!).ApplyDose(e);
                }
                Never();
                Dose();
                Check(!AddictionSystem.IsOverdosing(p) && e.Healing > 0 && Recent(p) == 1, product + " single dose recorded, heals, no overdose on a failed roll");
                float firstRisk = e.WatchedAttributes.GetFloat(OverdoseSystem.WatchRisk);
                for (int i = 1; i < 12; i++) Dose();
                Check(!AddictionSystem.IsOverdosing(p) && Recent(p) == 12, product + " repeated doses stack recent-dose count");
                float risk = e.WatchedAttributes.GetFloat(OverdoseSystem.WatchRisk);
                Check(risk > firstRisk * 3 && Near(risk, OverdoseSystem.ChanceFor(product, 12, e.Attributes.GetFloat(AddictionSystem.ToleranceKey(product)))),
                    product + " HUD risk grows with recent doses (" + firstRisk.ToString("0.000") + " -> " + risk.ToString("0.000") + ")");
                Always();
                float healing = e.Healing;
                Dose();
                Check(AddictionSystem.IsOverdosing(p), product + " successful roll triggers overdose");
                Check(Near(e.Healing, healing), product + " dose that overdoses does not heal");
                overdose.Tick(p, api.World.Calendar.TotalHours, 5);
                Check(e.PoisonDamage > 0, product + " binge overdose deals poison damage");
                Check(e.Stats.GetBlended("walkspeed") >= 0.1f - 0.001f && e.Stats.GetBlended("walkspeed") < 1,
                    product + " overdose slows without reversing movement");
                Check(e.Attributes.GetInt("vs-dope-tolerance-uses-" + product) == 13, product + " each dose counted once");
            }

            // ---- Roll threshold is the computed chance ----------------------------------
            var below = MakePlayer(api);
            overdose.Roll = () => 0.0199;
            overdose.RecordDose(below, "heroin", 100, 0);
            Check(AddictionSystem.IsOverdosing(below) && Near(Severity(below), 0.55f), "roll below chance overdoses with heroin base severity");
            var above = MakePlayer(api);
            overdose.Roll = () => 0.0201;
            overdose.RecordDose(above, "heroin", 100, 0);
            Check(!AddictionSystem.IsOverdosing(above), "roll above chance does not overdose");
            var tolerant = MakePlayer(api);
            overdose.Roll = () => 0.015;
            overdose.RecordDose(tolerant, "heroin", 100, 0.75f);
            Check(!AddictionSystem.IsOverdosing(tolerant), "tolerance lowers the chance used by the roll");

            // ---- Rolling window, mixed products, severity recovery ----------------------
            var window = MakePlayer(api);
            Never();
            overdose.RecordDose(window, "opium", 100, 0);
            overdose.RecordDose(window, "heroin", 100.5, 0);
            Check(Recent(window) == 2, "mixed products share the recent-dose window");
            overdose.Tick(window, 101.9, 0);
            Check(Recent(window) == 2, "doses inside the window are kept");
            overdose.Tick(window, 102.1, 0);
            Check(Recent(window) == 1, "doses older than the window are dropped");
            overdose.Tick(window, 102.6, 0);
            Check(Recent(window) == 0 && window.Entity.WatchedAttributes.GetFloat(OverdoseSystem.WatchRisk) == 0, "risk clears once the window is empty");

            var recovery = MakePlayer(api);
            Never();
            for (int i = 0; i < 9; i++) overdose.RecordDose(recovery, "heroin", 100, 0);
            Always();
            overdose.RecordDose(recovery, "heroin", 100, 0);
            Check(Near(Severity(recovery), 1f), "tenth-dose overdose is at full severity");
            overdose.Tick(recovery, 100, 0);
            Check(Near(Severity(recovery), 1f), "unchanged game clock does not recover");
            overdose.Tick(recovery, 101, 0);
            Check(Near(Severity(recovery), 0.5f), "one game hour recovers 0.5 severity");
            overdose.Tick(recovery, 102.01, 0);
            Check(!AddictionSystem.IsOverdosing(recovery) && Near(recovery.Entity.Stats.GetBlended("walkspeed"), 1), "recovery clears flag and movement penalty");

            var worse = MakePlayer(api);
            Always();
            overdose.RecordDose(worse, "opium", 100, 0);
            float first = Severity(worse);
            overdose.RecordDose(worse, "opium", 100, 0);
            Check(Severity(worse) > first, "another overdose while overdosing worsens severity");

            var mixed = MakePlayer(api);
            Always();
            overdose.RecordDose(mixed, "heroin", 100, 0);
            mixed.Entity.Stats.Set("walkspeed", "test-stimulant", 1);
            overdose.Tick(mixed, 100, 0);
            Check(mixed.Entity.Stats.GetBlended("walkspeed") <= 0.75f, "stimulant cannot cancel overdose slowdown");
            mixed.Entity.Stats.Set("walkspeed", "test-opioids", -3);
            overdose.Tick(mixed, 100, 0);
            Check(Near(mixed.Entity.Stats.GetBlended("walkspeed"), 0.1f), "multiple slows cannot reverse movement");

            // ---- Join / respawn -----------------------------------------------------------
            var joined = MakePlayer(api);
            Never();
            overdose.RecordDose(joined, "heroin", 100, 0);
            joined.Entity.WatchedAttributes.SetFloat("vs-dope-load-heroin", 30);
            joined.Entity.Attributes.SetDouble("vs-dope-load-gamehour", 90);
            overdose.OnJoin(joined, 100);
            Check(!joined.Entity.WatchedAttributes.HasAttribute("vs-dope-load-heroin") && !joined.Entity.Attributes.HasAttribute("vs-dope-load-gamehour"),
                "join removes legacy load-model keys");
            Check(Recent(joined) == 1, "join keeps recent doses");
            system.ApplyHeroinSyringeDose(joined);
            Check(Math.Abs(joined.Entity.WatchedAttributes.GetDouble("vs-dope-heroin-slow-expires-gamehour") - api.World.Calendar.TotalHours - 1) < 0.00001,
                "heroin effect retains one game hour duration");
            Always();
            system.ApplyHeroinSyringeDose(joined);
            var clear = typeof(AddictionSystem).GetMethod("OnPlayerRespawn", BindingFlags.Instance | BindingFlags.NonPublic)!;
            clear.Invoke(system, new object[] { joined });
            Check(!AddictionSystem.IsOverdosing(joined) && Recent(joined) == 0 && Near(joined.Entity.Stats.GetBlended("walkspeed"), 1),
                "respawn clears overdose, recent doses and drug movement effects");
            Check(joined.Entity.Attributes.GetInt("vs-dope-tolerance-uses-heroin") == 2, "respawn preserves long-term tolerance history");

            var dead = MakePlayer(api);
            Always();
            overdose.RecordDose(dead, "heroin", 100, 0);
            dead.Entity.Alive = false;
            overdose.Tick(dead, 100, 5);
            Check(!AddictionSystem.IsOverdosing(dead), "dead player has no ongoing overdose");

            // ---- Screen-effect balance (#29) --------------------------------------------
            Never();
            foreach (string product in new[] { "opium", "morphine", "coca-vitae" })
            {
                var p = MakePlayer(api);
                ((DrugConsumableItem)api.World.GetItem(new AssetLocation("vs-dope:" + product))!).ApplyDose(p.Entity);
                var s = DrugVisualEffects.PerDose[product];
                Check(Near(p.Entity.WatchedAttributes.GetFloat("intoxication"), s.Intoxication)
                    && Near(p.Entity.WatchedAttributes.GetFloat("psychedelic"), s.Psychedelic), product + " applies its tiered screen effect");
            }
            var visuals = MakePlayer(api);
            for (int i = 0; i < 20; i++) system.ApplyHeroinSyringeDose(visuals);
            Check(visuals.Entity.WatchedAttributes.GetFloat("intoxication") <= DrugVisualEffects.IntoxicationCap + 0.001f
                && visuals.Entity.WatchedAttributes.GetFloat("psychedelic") <= DrugVisualEffects.PsychedelicCap + 0.001f, "screen effects never exceed vanilla caps");

            // ---- Native vessel drinking ---------------------------------------------------
            var drink = typeof(BlockLiquidContainerBase).GetMethod("tryEatStop", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var bucket = api.World.Blocks.OfType<BlockLiquidContainerBase>().First(b => b.Code.Path == "woodbucket");
            var heroin = api.World.GetItem(new AssetLocation("vs-dope:heroin"));
            var vesselPlayer = MakePlayer(api);
            var vessel = new ItemStack(bucket);
            bucket.SetContent(vessel, new ItemStack(heroin, 137));
            var slot = new DummySlot(vessel);
            drink.Invoke(bucket, new object[] { 0.5f, slot, vesselPlayer.Entity });
            Check(bucket.GetContent(vessel)!.StackSize == 137 && Recent(vesselPlayer) == 0,
                "cancelled vessel drink consumes nothing and records no dose");
            drink.Invoke(bucket, new object[] { 2f, slot, vesselPlayer.Entity });
            int left = bucket.GetContent(slot.Itemstack!)?.StackSize ?? 0;
            Check(left < 137 && Recent(vesselPlayer) == Math.Max(1, (int)Math.Round((137 - left) / 10.0)),
                "native patched vessel drink rolls once per 0.1 L consumed");
            Check(vesselPlayer.Entity.Attributes.GetInt("vs-dope-tolerance-uses-heroin") == 1, "native vessel drink counts one consumption event");

            // ---- Syringes -----------------------------------------------------------------
            foreach (string liquid in new[] { "heroin", "morphine" })
            {
                var p = MakePlayer(api);
                var item = (SyringeItem)api.World.GetItem(new AssetLocation("vs-dope:syringe-" + liquid))!;
                var syringeSlot = new DummySlot(new ItemStack(item));
                Never();
                for (int i = 0; i < 10; i++)
                {
                    syringeSlot.Itemstack!.TempAttributes.SetBool("vs-dope-syringe-applying", true);
                    ((SyringeItem)syringeSlot.Itemstack!.Collectible).OnHeldInteractStop(1.5f, syringeSlot, p.Entity, null!, null!);
                }
                Check(syringeSlot.Itemstack!.Collectible.Code.Path == "syringe-empty", liquid + " ten actual injections empty the syringe");
                Check(p.Entity.Attributes.GetInt("vs-dope-tolerance-uses-" + liquid) == 10 && Recent(p) == 10, liquid + " ten injections roll ten times");
                Always();
                ((SyringeItem)syringeSlot.Itemstack!.Collectible).OnHeldInteractStop(1.5f, syringeSlot, p.Entity, null!, null!);
                Check(!AddictionSystem.IsOverdosing(p) && Recent(p) == 10, liquid + " empty syringe cannot apply another dose");
            }
            api.Logger.Notification("OVERDOSE TEST SUMMARY: " + checks + " checks passed");
        }
        catch (Exception ex) { api.Logger.Error("OVERDOSE TEST FAILED: " + ex); }
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
    public float Healing;
    public float PoisonDamage;
    public override bool ReceiveDamage(DamageSource source, float amount)
    {
        if (source.Type == EnumDamageType.Heal) Healing += amount;
        if (source.Type == EnumDamageType.Poison) PoisonDamage += amount;
        return true;
    }
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
