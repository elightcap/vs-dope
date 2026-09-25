using ProtoBuf;

namespace VsDope.Network;

// Vintage Story serializes network packets with protobuf-net 2.4, which refuses any
// type that carries no contract ("Type is not expected, and no contract can be inferred").
// ImplicitFields.AllPublic gives every public field a tag, so these stay plain POCOs.

// Server -> Client: open (or, with Refresh, update) the addict trading window for one addict.
// Parallel arrays keep serialization trivial; index i pairs DrugCodes[i] with GearPrices[i],
// Units[i] and PlayerHeld[i]. Sent on open and again after every sale.
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class OpenAddictTradePacket
{
    public long AddictEntityId;
    public string[] DrugCodes = System.Array.Empty<string>();
    public int[] GearPrices = System.Array.Empty<int>();
    // Price unit per row: "ea" for solid drugs, "dose" for liquids sold by the 0.1 L dose.
    public string[] Units = System.Array.Empty<string>();
    // Units of each offer the player currently carries (items, or whole litres for liquids).
    public int[] PlayerHeld = System.Array.Empty<int>();
    public int PlayerGears;
    // The addict's pockets: its gears, the gear-equivalent value of the goods it can pay with,
    // and every stack it carries (ItemStack.ToBytes(); the client rebuilds with new ItemStack(byte[])).
    public int AddictGears;
    public int AddictGoodsValue;
    public AddictStackData[] AddictStacks = System.Array.Empty<AddictStackData>();
    // Who this addict is and how well it knows the player (lang key); empty for an unknown addict.
    public string AddictName = "";
    public string AddictTierKey = "";
    // True for the post-sale update: only applied to a window that is already open for this addict.
    public bool Refresh;
}

// One serialized ItemStack. Wrapped in a message because protobuf-net can't do byte[][].
// Nested contract only; it is not a message type, so it is not registered on the channel.
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AddictStackData
{
    public byte[] Stack = System.Array.Empty<byte>();
}

// Client -> Server: player sells `Quantity` of `DrugCode` to the addict (Quantity <= 0 means "all").
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SellToAddictPacket
{
    public long AddictEntityId;
    public string DrugCode = "";
    public int Quantity;
}

// Server -> Client: close the trade window for this addict (it died, fled or walked away).
// Registered after the other two on both sides; registration order must match.
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CloseAddictTradePacket
{
    public long AddictEntityId;
}
