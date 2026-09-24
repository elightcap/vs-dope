using ProtoBuf;

namespace VsDope.Network;

// Vintage Story serializes network packets with protobuf-net 2.4, which refuses any
// type that carries no contract ("Type is not expected, and no contract can be inferred").
// ImplicitFields.AllPublic gives every public field a tag, so these stay plain POCOs.

// Server -> Client: open the addict trading window for a specific addict entity.
// Parallel arrays keep serialization trivial; index i pairs DrugCodes[i] with GearPrices[i].
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class OpenAddictTradePacket
{
    public long AddictEntityId;
    public string[] DrugCodes = System.Array.Empty<string>();
    public int[] GearPrices = System.Array.Empty<int>();
    // Price unit per row: "ea" for solid drugs, "/L" for liquids sold by the litre.
    public string[] Units = System.Array.Empty<string>();
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
