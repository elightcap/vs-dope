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
        try
        {
            foreach (string product in new[] { "opium", "morphine", "coca-vitae", "heroin" })
            {
                var p = MakePlayer(api);
                var e = (ProbePlayer)p.Entity;
                void Dose()
                {
                    if (product == "heroin") system.ApplyHeroinSyringeDose(p);
                    else ((DrugConsumableItem)api.World.GetItem(new AssetLocation("vs-dope:" + product))!).ApplyDose(e);
                }
                Dose();
                Check(!AddictionSystem.IsOverdosing(p) && e.Healing > 0, product + " single dose remains effective");
                for (int i = 1; i < 12; i++) Dose();
                Check(AddictionSystem.IsOverdosing(p), product + " repeated doses trigger overdose");
                float healing = e.Healing;
                Dose();
                Check(Near(e.Healing, healing), product + " overdose cannot be healed by further drug doses");
                overdose.Tick(p, api.World.Calendar.TotalHours, 5);
                Check(e.PoisonDamage > 0, product + " severe overdose deals poison damage");
                Check(e.Stats.GetBlended("walkspeed") >= 0.1f - 0.001f && e.Stats.GetBlended("walkspeed") < 1,
                    product + " overdose slows without reversing movement");
                Check(e.Attributes.GetInt("vs-dope-tolerance-uses-" + product) == 13, product + " each dose counted once");
            }

            var mixed = MakePlayer(api);
            overdose.RecordDose(mixed, "opium", 100, 3);
            overdose.RecordDose(mixed, "heroin", 100, 2);
            Check(AddictionSystem.IsOverdosing(mixed), "combined subthreshold products trigger overdose");
            mixed.Entity.Stats.Set("walkspeed", "test-stimulant", 1);
            overdose.Tick(mixed, 100, 0);
            Check(mixed.Entity.Stats.GetBlended("walkspeed") <= 0.75f, "stimulant cannot cancel overdose slowdown");
            mixed.Entity.Stats.Set("walkspeed", "test-opioids", -3);
            overdose.Tick(mixed, 100, 0);
            Check(Near(mixed.Entity.Stats.GetBlended("walkspeed"), 0.1f), "multiple slows cannot reverse movement");

            var recovery = MakePlayer(api);
            overdose.RecordDose(recovery, "heroin", 100, 6);
            overdose.Tick(recovery, 100, 0);
            Check(Near(recovery.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")), 30), "unchanged game clock does not metabolize load");
            overdose.Tick(recovery, 101, 0);
            Check(Near(recovery.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")), 25), "one game hour clears exactly five load units");
            overdose.Tick(recovery, 103, 0);
            Check(!AddictionSystem.IsOverdosing(recovery) && Near(recovery.Entity.Stats.GetBlended("walkspeed"), 1), "threshold recovery clears flag and movement penalty");
            overdose.Tick(recovery, 110, 0);
            Check(!recovery.Entity.WatchedAttributes.HasAttribute(AddictionSystem.LoadKey("heroin")), "full recovery removes stored load");

            var fastTicks = MakePlayer(api);
            var slowTicks = MakePlayer(api);
            overdose.RecordDose(fastTicks, "heroin", 100, 6);
            overdose.RecordDose(slowTicks, "heroin", 100, 6);
            for (int i = 1; i <= 60; i++) overdose.Tick(fastTicks, 100 + i / 60.0, 0);
            overdose.Tick(slowTicks, 101, 0);
            Check(Near(fastTicks.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")),
                slowTicks.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin"))), "metabolism independent of callback frequency");

            var joined = MakePlayer(api);
            overdose.RecordDose(joined, "heroin", 100, 6);
            joined.Entity.WatchedAttributes.SetFloat("psychedelic", 25);
            overdose.OnJoin(joined, 1000);
            overdose.Tick(joined, 1000, 0);
            Check(Near(joined.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")), 30), "relog keeps saved load without phantom dose or offline reset");
            system.ApplyHeroinSyringeDose(joined);
            Check(Near(joined.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")), 35), "dose at capped psychedelic level still counted exactly once");
            Check(Math.Abs(joined.Entity.WatchedAttributes.GetDouble("vs-dope-heroin-slow-expires-gamehour") - api.World.Calendar.TotalHours - 1) < 0.00001,
                "heroin effect retains one game hour duration");
            var clear = typeof(AddictionSystem).GetMethod("OnPlayerRespawn", BindingFlags.Instance | BindingFlags.NonPublic)!;
            clear.Invoke(system, new object[] { joined });
            Check(!AddictionSystem.IsOverdosing(joined) && !joined.Entity.WatchedAttributes.HasAttribute(AddictionSystem.LoadKey("heroin"))
                && Near(joined.Entity.Stats.GetBlended("walkspeed"), 1), "respawn clears acute load and drug movement effects");
            Check(joined.Entity.Attributes.GetInt("vs-dope-tolerance-uses-heroin") == 1, "respawn preserves long-term tolerance history");

            var highTolerance = MakePlayer(api);
            highTolerance.Entity.Attributes.SetFloat("vs-dope-tolerance-heroin", 0.75f);
            overdose.RecordDose(highTolerance, "heroin", 100, 6);
            Check(AddictionSystem.IsOverdosing(highTolerance), "maximum tolerance cannot prevent overdose");
            highTolerance.Entity.Alive = false;
            overdose.Tick(highTolerance, 100, 5);
            Check(!AddictionSystem.IsOverdosing(highTolerance), "dead player has no ongoing overdose");

            var drink = typeof(BlockLiquidContainerBase).GetMethod("tryEatStop", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var bucket = api.World.Blocks.OfType<BlockLiquidContainerBase>().First(b => b.Code.Path == "woodbucket");
            var heroin = api.World.GetItem(new AssetLocation("vs-dope:heroin"));
            var vesselPlayer = MakePlayer(api);
            var vessel = new ItemStack(bucket);
            bucket.SetContent(vessel, new ItemStack(heroin, 137));
            var slot = new DummySlot(vessel);
            drink.Invoke(bucket, new object[] { 0.5f, slot, vesselPlayer.Entity });
            Check(bucket.GetContent(vessel)!.StackSize == 137 && !vesselPlayer.Entity.WatchedAttributes.HasAttribute(AddictionSystem.LoadKey("heroin")),
                "cancelled vessel drink consumes nothing and records no dose");
            drink.Invoke(bucket, new object[] { 2f, slot, vesselPlayer.Entity });
            int left = bucket.GetContent(slot.Itemstack!)?.StackSize ?? 0;
            Check(left < 137 && Near(vesselPlayer.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")), (137 - left) * 0.5f),
                "native patched vessel drink records exact consumed volume");
            Check(vesselPlayer.Entity.Attributes.GetInt("vs-dope-tolerance-uses-heroin") == 1, "native vessel drink counts one consumption event");
            Check(((ProbePlayer)vesselPlayer.Entity).Healing == 0, "large vessel drink cannot bypass overdose healing block");
            bucket.SetContent(slot.Itemstack!, new ItemStack(heroin, 3));
            float previousLoad = vesselPlayer.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin"));
            drink.Invoke(bucket, new object[] { 2f, slot, vesselPlayer.Entity });
            Check(bucket.GetContent(slot.Itemstack!) == null && Near(vesselPlayer.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")), previousLoad + 1.5f),
                "last partial vessel drink counts remaining volume only");

            var stackedPlayer = MakePlayer(api);
            var stacked = new ItemStack(bucket, 2);
            bucket.SetContent(stacked, new ItemStack(heroin, 100));
            drink.Invoke(bucket, new object[] { 2f, new DummySlot(stacked), stackedPlayer.Entity });
            Check(stacked.StackSize == 1 && bucket.GetContent(stacked)!.StackSize == 100,
                "drinking from stacked vessels leaves the other vessel untouched");
            Check(Near(stackedPlayer.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey("heroin")), 50), "stacked vessel records only one vessel volume");

            foreach (string liquid in new[] { "heroin", "morphine" })
            {
                var p = MakePlayer(api);
                var item = (SyringeItem)api.World.GetItem(new AssetLocation("vs-dope:syringe-" + liquid))!;
                var syringeSlot = new DummySlot(new ItemStack(item));
                for (int i = 0; i < 10; i++)
                {
                    syringeSlot.Itemstack!.TempAttributes.SetBool("vs-dope-syringe-applying", true);
                    ((SyringeItem)syringeSlot.Itemstack!.Collectible).OnHeldInteractStop(1.5f, syringeSlot, p.Entity, null!, null!);
                }
                Check(syringeSlot.Itemstack!.Collectible.Code.Path == "syringe-empty", liquid + " ten actual injections empty the syringe");
                Check(p.Entity.Attributes.GetInt("vs-dope-tolerance-uses-" + liquid) == 10, liquid + " ten actual injections record ten doses");
                Check(AddictionSystem.IsOverdosing(p), liquid + " repeated actual syringe applications trigger overdose");
                float load = p.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey(liquid));
                ((SyringeItem)syringeSlot.Itemstack!.Collectible).OnHeldInteractStop(1.5f, syringeSlot, p.Entity, null!, null!);
                Check(Near(p.Entity.WatchedAttributes.GetFloat(AddictionSystem.LoadKey(liquid)), load), liquid + " empty syringe cannot apply another dose");
            }
            api.Logger.Notification("OVERDOSE TEST SUMMARY: " + checks + " checks passed");
        }
        catch (Exception ex) { api.Logger.Error("OVERDOSE TEST FAILED: " + ex); }
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
