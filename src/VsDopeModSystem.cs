using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VsDope.Entities;
using VsDope.Items;
using VsDope.Systems;

namespace VsDope;

public class VsDopeModSystem : ModSystem
{
    public static AddictionSystem AddictionSystem = null!;
    private ICoreServerAPI? sapi;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterItemClass("vs-dope.drugconsumable", typeof(DrugConsumableItem));
        api.RegisterItemClass("vs-dope.opiumitem", typeof(OpiumItem));
        api.RegisterItemClass("vs-dope.morphineitem", typeof(MorphineItem));
        api.RegisterItemClass("vs-dope.cocavitaeitem", typeof(CocaVitaeItem));
        api.RegisterItemClass("vs-dope.syringe", typeof(SyringeItem));
        api.RegisterEntity("vs-dope.drugaddict", typeof(EntityDrugAddict));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        sapi = api;
        AddictionSystem = new AddictionSystem();
        AddictionSystem.Initialize(api);

        var spawnSystem = new DrugAddictSpawnSystem();
        spawnSystem.Initialize(api);

        var tradeSystem = new AddictTradeSystem();
        tradeSystem.Initialize(api);
    }
}
