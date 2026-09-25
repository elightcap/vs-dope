using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsDope.Entities;

// Test-only server mod: asserts AddictPockets against the live 1.22.7 registry
// (junk codes resolve, starting stock, payment math, heroin into jugs, persistence).
public class Probe : ModSystem
{
    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => Run(api));
    }

    private void Run(ICoreServerAPI api)
    {
        int checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("ADDICT TEST FAIL: " + name);
            checks++;
            api.Logger.Notification("ADDICT TEST PASS: " + name);
        }
        try
        {
            var world = api.World;
            var junk = (Array)typeof(AddictPockets).GetField("Junk", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            foreach (object entry in junk)
            {
                string code = (string)entry.GetType().GetField("Item1")!.GetValue(entry)!;
                Check(world.GetItem(new AssetLocation(code)) != null, "junk item exists: " + code);
            }
            Check(world.GetItem(AddictPockets.GearCode) != null, "gear item exists");
            Check(world.GetBlock(AddictPockets.LiquidJugCode) is BlockLiquidContainerBase, "jug is a liquid container");

            var rng = new Random(1234);
            for (int i = 0; i < 50; i++)
            {
                var roll = new AddictPockets();
                roll.RollStartingStock(world, rng);
                Check(roll.GearCount >= AddictPockets.MinStartingGears && roll.GearCount <= AddictPockets.MaxStartingGears, "roll " + i + " gear budget in range");
                Check(roll.Stacks.Count(s => !AddictPockets.IsGear(s)) >= AddictPockets.MinJunkKinds, "roll " + i + " has junk");
            }

            // Payment: gears first, then junk.
            var p = new AddictPockets();
            p.Add(world, new ItemStack(world.GetItem(AddictPockets.GearCode), 10));
            p.Add(world, new ItemStack(world.GetItem(new AssetLocation("game:rope")), 2));      // 3 each
            p.Add(world, new ItemStack(world.GetItem(new AssetLocation("game:flint")), 3));     // 1 each
            p.Add(world, new ItemStack(world.GetItem(new AssetLocation("game:rot")), 4));       // worthless
            Check(p.GearCount == 10 && p.GoodsValue == 9 && p.Wealth == 19, "wealth = gears + goods");
            var paid = p.TakePayment(world, 8, out int gearsPaid);
            Check(gearsPaid == 8 && p.GearCount == 2 && paid.Count == 1, "gear-only payment");
            paid = p.TakePayment(world, 9, out gearsPaid);
            int paidGoods = paid.Where(s => !AddictPockets.IsGear(s)).Sum(s => AddictPockets.PaymentValueOf(s) * s.StackSize);
            Check(gearsPaid == 2 && paidGoods == 7 && p.GearCount == 0, "remainder paid in goods (2 gears + 7 in goods)");
            Check(p.GoodsValue == 2 && p.Stacks.Any(s => s.Collectible.Code.Path == "rot" && s.StackSize == 4), "rot never used as payment");

            // Liquids go into jugs, topping up first.
            var heroin = world.GetItem(new AssetLocation("vs-dope:heroin"))!;
            var liquid = new AddictPockets();
            Check(liquid.AddLiquid(world, heroin, 5) == 5, "5 L heroin stored");
            var jugs = liquid.Stacks.Where(s => s.Block is BlockLiquidContainerBase).ToList();
            var jugBlock = (BlockLiquidContainerBase)jugs[0].Block;
            Check(jugs.Count == 2 && jugs.Sum(j => jugBlock.GetCurrentLitres(j)) == 5, "5 L = 2 jugs");
            Check(liquid.AddLiquid(world, heroin, 1) == 1 && liquid.Stacks.Count == 2 && liquid.Stacks.Sum(j => jugBlock.GetCurrentLitres(j)) == 6, "1 L tops up existing jug");
            Check(AddictPockets.PaymentValueOf(jugs[0]) == 0 && liquid.Wealth == 0, "bought drugs are never payment");
            var doses = new AddictPockets();
            Check(doses.AddLiquidPortions(world, heroin, 30) == 30 && doses.AddLiquidPortions(world, heroin, 10) == 10, "heroin doses (portions) stored");
            Check(doses.Stacks.Count == 1 && Math.Abs(jugBlock.GetCurrentLitres(doses.Stacks[0]) - 0.4f) < 1e-3, "4 doses = 0.4 L in one jug");

            // Persistence + packet roundtrip.
            var tree = p.ToTree();
            var bytes = tree.ToBytes();
            var reread = new TreeAttribute();
            reread.FromBytes(bytes);
            var loaded = new AddictPockets();
            loaded.Load(reread, world);
            Check(loaded.Stacks.Count == p.Stacks.Count && loaded.GoodsValue == p.GoodsValue, "tree roundtrip");
            var jugBack = new ItemStack(liquid.Stacks[0].ToBytes());
            Check(jugBack.ResolveBlockOrItem(world) && jugBlock.GetCurrentLitres(jugBack) == jugBlock.GetCurrentLitres(liquid.Stacks[0]), "jug ToBytes roundtrip keeps litres");

            var all = liquid.TakeAll();
            Check(all.Count == 2 && liquid.Stacks.Count == 0, "TakeAll empties pockets");

            api.Logger.Notification("ADDICT TEST POCKETS: " + checks + " checks passed");

            LedgerChecks.Rules(Check);
            api.Logger.Notification("ADDICT TEST LEDGER: " + checks + " checks passed");

            // Entity half needs a loaded chunk: load the spawn column, then spawn real addicts.
            var spawn = world.DefaultSpawnPosition;
            int size = GlobalConstants.ChunkSize;
            api.WorldManager.LoadChunkColumnPriority((int)spawn.X / size, (int)spawn.Z / size, new ChunkLoadOptions
            {
                KeepLoaded = true,
                OnLoaded = () => api.Event.RegisterCallback(_ => EntityPhase(api, checks), 500),
            });
        }
        catch (Exception ex) { api.Logger.Error("ADDICT TEST FAILED: " + ex); }
    }

    private static int ItemsNear(ICoreServerAPI api, Vec3d pos)
        => api.World.GetEntitiesAround(pos, 3, 3, e => e is EntityItem && e.Alive).Length;

    private void EntityPhase(ICoreServerAPI api, int checks)
    {
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("ADDICT TEST FAIL: " + name);
            checks++;
            api.Logger.Notification("ADDICT TEST PASS: " + name);
        }
        try
        {
            var world = api.World;
            var props = world.GetEntityType(new AssetLocation("vs-dope", "drugaddict"))!;
            var spawn = world.DefaultSpawnPosition;

            EntityDrugAddict SpawnAt(double dx)
            {
                int x = (int)(spawn.X + dx), z = (int)spawn.Z;
                var addict = (EntityDrugAddict)api.ClassRegistry.CreateEntity(props);
                addict.Pos.SetPos(new Vec3d(x + 0.5, world.BlockAccessor.GetRainMapHeightAt(x, z) + 1, z + 0.5));
                world.SpawnEntity(addict);
                return addict;
            }

            int lx = (int)spawn.X + 24, lz = (int)spawn.Z;
            LedgerChecks.Entity(api, props, new Vec3d(lx + 0.5, world.BlockAccessor.GetRainMapHeightAt(lx, lz) + 1, lz + 0.5), Check);

            var killed = SpawnAt(0);
            var despawned = SpawnAt(12);
            Check(killed.Pockets != null && killed.Pockets.GearCount >= AddictPockets.MinStartingGears, "spawned addict rolls pockets");
            killed.Pockets!.AddLiquid(world, world.GetItem(new AssetLocation("vs-dope:heroin"))!, 2);
            killed.SavePockets();
            int expected = killed.Pockets.Stacks.Count;

            // Persistence through the save format.
            var ms = new MemoryStream();
            killed.ToBytes(new BinaryWriter(ms), false);
            var reloaded = api.ClassRegistry.CreateEntity(props);
            ms.Position = 0;
            reloaded.FromBytes(new BinaryReader(ms), false);
            Check(reloaded.Attributes.GetTreeAttribute("vs-dope-addict-inv")?.GetInt("count") == expected, "pockets survive entity ToBytes/FromBytes");

            Vec3d killPos = killed.Pos.XYZ, despawnPos = despawned.Pos.XYZ;
            int beforeKill = ItemsNear(api, killPos), beforeDespawn = ItemsNear(api, despawnPos);
            Check(despawned.Pockets!.Stacks.Count > 0, "second addict has pockets");
            killed.Die(EnumDespawnReason.Death, new DamageSource { Source = EnumDamageSource.Internal, Type = EnumDamageType.Poison });
            api.World.DespawnEntity(despawned, new EntityDespawnData { Reason = EnumDespawnReason.Removed });

            api.Event.RegisterCallback(_ =>
            {
                try
                {
                    Check(ItemsNear(api, killPos) - beforeKill == expected, "death drops every pocket stack (" + expected + ")");
                    Check(killed.Pockets!.Stacks.Count == 0, "pockets emptied after drop");
                    Check(ItemsNear(api, despawnPos) == beforeDespawn, "despawn drops nothing");
                    api.Logger.Notification("ADDICT TEST SUMMARY: " + checks + " checks passed");
                }
                catch (Exception ex) { api.Logger.Error("ADDICT TEST FAILED: " + ex); }
            }, 1000);
        }
        catch (Exception ex) { api.Logger.Error("ADDICT TEST FAILED: " + ex); }
    }
}
