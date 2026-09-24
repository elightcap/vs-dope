using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Server;

// Inherit the game's implementation of the internal IPlayer member; replace only
// public endpoints. No real player connection is created or added to the server.
public sealed class TestServerPlayer : ServerPlayer, IPlayer
{
    private EntityPlayer testEntity = null!;
    private IPlayerInventoryManager testInventory = null!;
    private TestServerPlayer() : base(null!, null!) { }
    public static TestServerPlayer Create(EntityPlayer entity, IPlayerInventoryManager inventory)
    {
        var player = (TestServerPlayer)RuntimeHelpers.GetUninitializedObject(typeof(TestServerPlayer));
        player.testEntity = entity;
        player.testInventory = inventory;
        return player;
    }
    EntityPlayer IPlayer.Entity => testEntity;
    IPlayerInventoryManager IPlayer.InventoryManager => testInventory;
    public override string PlayerUID => testEntity.PlayerUID;
    public override EnumClientState ConnectionState => EnumClientState.Playing;
}
