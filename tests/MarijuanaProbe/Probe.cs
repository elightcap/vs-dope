using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsDope.Items;
using VsDope.Systems;

public sealed class MarijuanaProbe : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
    public override void StartServerSide(ICoreServerAPI api) =>
        api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, () => Run(api));

    private void Run(ICoreServerAPI api)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
            api.Logger.Notification("MARIJUANA PASS: " + message);
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .0001;
        try
        {
            var world = api.World;
            var buds = world.GetItem(new AssetLocation("vs-dope:marijuana-buds"));
            var paper = world.GetItem(new AssetLocation("game:paper-parchment"));
            var joint = (JointItem)world.GetItem(new AssetLocation("vs-dope:joint"))!;
            var seeds = world.GetItem(new AssetLocation("vs-dope:seeds-marijuana"));
            Check(buds != null && paper != null && joint != null && seeds != null, "buds, parchment, joint and seeds registered");
            Check(seeds!.Attributes["plantBlockCode"].AsString() == "vs-dope:crop-marijuana-1", "seeds plant stage one");
            for (int stage = 1; stage <= 9; stage++)
            {
                var crop = world.GetBlock(new AssetLocation("vs-dope:crop-marijuana-" + stage));
                Check(crop != null && crop.CropProps != null, "crop stage " + stage + " registered with growth properties");
                Check(api.Assets.TryGet(crop!.Shape!.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")) != null, "stage " + stage + " shape resolves");
                Check(crop.Drops!.Any(d => d.Code?.ToString() == "vs-dope:marijuana-buds") == (stage == 9), "buds drop only at maturity: " + stage);
            }
            var trades = api.Assets.Get(new AssetLocation("game:config/tradelists/trader-agriculture.json")).ToObject<Newtonsoft.Json.Linq.JObject>();
            Check(trades["selling"]!["list"]!.Any(entry => entry["code"]?.ToString() == "vs-dope:seeds-marijuana"), "native agriculture trader can supply marijuana seeds");
            var recipe = world.GridRecipes.Single(r => r.Output?.Code?.ToString() == "vs-dope:joint");
            Check(recipe.Shapeless && recipe.Output!.StackSize == 1, "one-joint shapeless recipe loaded");
            var player = MakePlayer(api);
            ItemSlot[] grid = Enumerable.Range(0,9).Select(_ => (ItemSlot)new DummySlot()).ToArray();
            grid[1] = new DummySlot(new ItemStack(paper, 3)); grid[8] = new DummySlot(new ItemStack(buds, 5));
            Check(recipe.Matches(player, world, grid, 3), "native recipe matches separated, reversed ingredients");
            Check(recipe.ConsumeInput(player, grid, 3) && grid[1].StackSize == 2 && grid[8].StackSize == 4, "native crafting consumes exactly one parchment and one bud");
            grid[1] = new DummySlot();
            Check(!recipe.Matches(player, world, grid, 3), "buds alone cannot craft joint");

            var e = (ProbePlayer)player.Entity;
            var slot = new DummySlot(new ItemStack(joint, 3));
            var handling = EnumHandHandling.NotHandled;
            joint!.OnHeldInteractStart(slot,e,null!,null!,true,ref handling);
            Check(handling == EnumHandHandling.PreventDefault && e.LastAnimation == JointItem.AnimationCode, "smoking starts dedicated animation");
            joint.OnHeldInteractCancel(1,slot,e,null!,null!,EnumItemUseCancelReason.Death);
            joint.OnHeldInteractStop(5,slot,e,null!,null!);
            Check(slot.StackSize == 3 && !e.WatchedAttributes.HasAttribute(StonedSystem.ExpiryKey), "cancel followed by stop consumes nothing and applies no effect");
            joint.OnHeldInteractStart(slot,e,null!,null!,true,ref handling);
            joint.OnHeldInteractStop(4.99f,slot,e,null!,null!);
            Check(slot.StackSize == 3, "early release consumes nothing");
            joint.OnHeldInteractStart(slot,e,null!,null!,true,ref handling);
            Check(joint.OnHeldInteractStep(2,slot,e,null!,null!) && !joint.OnHeldInteractStep(5,slot,e,null!,null!), "hold finishes at five seconds");
            joint.OnHeldInteractStop(5,slot,e,null!,null!);
            Check(slot.StackSize == 2 && Near(e.WatchedAttributes.GetDouble(StonedSystem.ExpiryKey),world.Calendar.TotalHours+2), "completed smoking consumes one joint and applies two game hours");
            joint.OnHeldInteractStop(5,slot,e,null!,null!);
            Check(slot.StackSize == 2, "duplicate stop cannot consume twice");
            StonedSystem.Clear(e);

            StonedSystem.Apply(e,100);
            Check(Near(e.Stats.GetBlended("walkspeed"),.8), "stoned movement is 80 percent of base");
            StonedSystem.Tick(e,100);
            Check(Near(e.Healing,0), "paused calendar heals nothing");
            StonedSystem.Tick(e,100+1.0/60);
            Check(Near(e.Healing,.5), "one game minute heals 0.5 HP");
            StonedSystem.Tick(e,101);
            Check(Near(e.Healing,30), "one game hour heals 30 HP including tick catch-up");
            StonedSystem.Tick(e,102.25);
            Check(Near(e.Healing,60) && !e.WatchedAttributes.HasAttribute(StonedSystem.ExpiryKey) && Near(e.Stats.GetBlended("walkspeed"),1), "expiry clips healing at exactly 60 HP and removes slowdown");
            StonedSystem.Tick(e,104);
            Check(Near(e.Healing,60), "no healing after expiry");

            e = (ProbePlayer)MakePlayer(api).Entity;
            StonedSystem.Apply(e,200); StonedSystem.Tick(e,200.5); StonedSystem.Apply(e,200.5);
            StonedSystem.Tick(e,201);
            Check(Near(e.Healing,30) && Near(e.WatchedAttributes.GetDouble(StonedSystem.ExpiryKey),202.5) && Near(e.Stats.GetBlended("walkspeed"),.8), "repeat smoking refreshes but never stacks healing or speed");
            StonedSystem.Resume(e,201.5); StonedSystem.Tick(e,201.5);
            Check(Near(e.Healing,30), "rejoin grants no offline healing");
            StonedSystem.Tick(e,201.5+1.0/60);
            Check(Near(e.Healing,30.5), "healing resumes normally after rejoin");
            StonedSystem.Resume(e,203);
            Check(!e.WatchedAttributes.HasAttribute(StonedSystem.ExpiryKey) && Near(e.Stats.GetBlended("walkspeed"),1), "expired offline effect clears on rejoin");

            e = (ProbePlayer)MakePlayer(api).Entity;
            e.Stats.Set("walkspeed","other-mod",.25f);
            StonedSystem.Apply(e,300); StonedSystem.Clear(e);
            Check(Near(e.Stats.GetBlended("walkspeed"),1.25), "cleanup preserves other movement modifiers");
            StonedSystem.Apply(e,300); e.Alive=false; StonedSystem.Tick(e,300.5);
            Check(Near(e.Healing,0) && !e.WatchedAttributes.HasAttribute(StonedSystem.ExpiryKey), "death stops healing and clears effect");

            var shapeAsset=api.Assets.Get(new AssetLocation("game:shapes/entity/humanoid/seraph-faceless.json"));
            var shape=shapeAsset.ToObject<Shape>();
            Check(shape.Animations.Any(a=>a.Code==JointItem.AnimationCode) && shape.Animations.Any(a=>a.Code==JointItem.AnimationCode+"-fp"), "actual patched player shape contains first- and third-person smoking animations");
            Check(api.Assets.TryGet(new AssetLocation("vs-dope:sounds/player/joint-drag.ogg"))!=null, "drag sound packaged");
            api.Logger.Notification("MARIJUANA TEST SUMMARY: " + checks + " checks passed");
        }
        catch (Exception ex) { api.Logger.Error("MARIJUANA TEST FAILED: " + ex); }
    }
    private static IServerPlayer MakePlayer(ICoreServerAPI api)
    {
        var e=new ProbePlayer { Api=api, World=api.World, Alive=true };
        e.Stats=new EntityStats(e);
        e.AnimManager=Proxy<IAnimationManager>.Create((m,args)=>{
            if (m.Name=="StartAnimation" && args?[0] is string code) e.LastAnimation=code;
            return m.ReturnType==typeof(void)||!m.ReturnType.IsValueType ? null : Activator.CreateInstance(m.ReturnType);
        });
        var inventory=Proxy<IPlayerInventoryManager>.Create((m,args)=>m.ReturnType==typeof(void)||!m.ReturnType.IsValueType ? null : Activator.CreateInstance(m.ReturnType));
        return TestServerPlayer.Create(e,inventory);
    }
}
public class ProbePlayer : EntityPlayer
{
    public override IAnimationManager AnimManager {get;set;}=null!;
    public string LastAnimation="";
    public float Healing;
    public override bool ReceiveDamage(DamageSource source,float amount) {if(source.Type==EnumDamageType.Heal) Healing+=amount;return true;}
}
public class Proxy<T> : DispatchProxy where T:class
{
    private System.Func<MethodInfo,object?[]?,object?> handler=null!;
    public static T Create(System.Func<MethodInfo,object?[]?,object?> f) {var p=Create<T,Proxy<T>>();((Proxy<T>)(object)p).handler=f;return p;}
    protected override object? Invoke(MethodInfo? method,object?[]? args)=>handler(method!,args);
}
