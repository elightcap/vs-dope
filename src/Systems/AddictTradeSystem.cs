using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VsDope.Entities;
using VsDope.Network;

namespace VsDope.Systems;

// Server-authoritative trading backend for the drug addict's trader-style window.
// Owns the offer table, the network channel, and all item/gear transfers.
public class AddictTradeSystem
{
    public static AddictTradeSystem Instance = null!;

    // Above-market gear price the addict pays per sellable unit, in menu order.
    // Solid drugs sell per item; liquid drugs sell per litre out of whatever container
    // holds them (heroin is an ItemLiquidPortion and never sits in a slot as a loose stack).
    private static readonly (string Code, int GearPrice)[] Offers =
    {
        ("vs-dope:opium", 12),
        ("vs-dope:morphine", 30),
        ("vs-dope:coca-vitae", 40),
        ("vs-dope:heroin", 75),
    };

    private const double OverdoseChancePerSale = 0.12;

    private ICoreServerAPI api = null!;
    private IServerNetworkChannel channel = null!;
    private readonly Random rng = new();

    // Whether each offer is a liquid. Resolved lazily: item types are not registered yet
    // when Initialize() runs during mod startup.
    private Dictionary<string, bool>? liquidByCode;

    public void Initialize(ICoreServerAPI serverApi)
    {
        api = serverApi;
        Instance = this;

        channel = api.Network.RegisterChannel("vs-dope.addicttrade");
        channel.RegisterMessageType<SellToAddictPacket>();
        channel.RegisterMessageType<OpenAddictTradePacket>();
        channel.SetMessageHandler<SellToAddictPacket>(OnSell);
    }

    public void OpenTradeFor(IServerPlayer player, EntityDrugAddict addict)
    {
        if (player == null || addict == null) return;

        var codes = new string[Offers.Length];
        var prices = new int[Offers.Length];
        var units = new string[Offers.Length];
        for (int i = 0; i < Offers.Length; i++)
        {
            codes[i] = Offers[i].Code;
            prices[i] = Offers[i].GearPrice;
            units[i] = IsLiquid(Offers[i].Code) ? "/L" : "ea";
        }

        channel.SendPacket(new OpenAddictTradePacket
        {
            AddictEntityId = addict.EntityId,
            DrugCodes = codes,
            GearPrices = prices,
            Units = units,
        }, new[] { player });
    }

    private void OnSell(IServerPlayer player, SellToAddictPacket packet)
    {
        if (player?.Entity == null || packet == null) return;

        int pricePerUnit = PriceOf(packet.DrugCode);
        if (pricePerUnit <= 0) return;

        // Every rejection below reports back. Silent returns here are what made a broken
        // trade look like a dead button.
        var addict = api.World.GetEntityById(packet.AddictEntityId) as EntityDrugAddict;
        if (addict == null || !addict.Alive)
        {
            Tell(player, "The addict is gone.");
            return;
        }
        // Only trade with the addict that is currently dealing with you.
        if (!addict.Friendly || addict.TargetPlayerUid != player.PlayerUID)
        {
            Tell(player, "They're not dealing with you.");
            return;
        }

        var code = new AssetLocation(packet.DrugCode);
        bool liquid = IsLiquid(packet.DrugCode);

        int available = CountUnits(player, code, liquid);
        if (available <= 0)
        {
            Tell(player, liquid
                ? "You have no container holding that."
                : "You have none of that to sell.");
            return;
        }

        int qty = packet.Quantity <= 0 ? available : Math.Min(packet.Quantity, available);
        int taken = TakeUnits(player, code, liquid, qty);
        if (taken <= 0)
        {
            Tell(player, "The deal fell through.");
            return;
        }

        long gears = (long)taken * pricePerUnit;
        var gearItem = api.World.GetItem(new AssetLocation("game:gear-rusty"));
        if (gearItem != null)
        {
            // Pay in chunks of up to the gear stack limit.
            long remaining = gears;
            while (remaining > 0)
            {
                int n = (int)Math.Min(remaining, gearItem.MaxStackSize);
                player.InventoryManager.TryGiveItemstack(new ItemStack(gearItem, n), true);
                remaining -= n;
            }
        }

        string unit = liquid ? (taken == 1 ? " litre" : " litres") : "";
        Tell(player, $"Sold {taken}{unit} for {gears} rusty gears.");
        addict.OnPurchaseCompleted();

        // Chance the addict ODs on the high after a deal. Otherwise it stays put so the
        // player can keep selling from the same window.
        if (rng.NextDouble() < OverdoseChancePerSale) addict.Overdose();
    }

    private static void Tell(IServerPlayer player, string message)
        => player.SendMessage(0, message, EnumChatType.Notification, null);

    private static int PriceOf(string code)
    {
        foreach (var offer in Offers)
        {
            if (offer.Code == code) return offer.GearPrice;
        }
        return 0;
    }

    // A drug is "liquid" if its item carries waterTightContainerProps, i.e. it only ever
    // exists inside a liquid container rather than as a loose inventory stack.
    private bool IsLiquid(string code)
    {
        if (liquidByCode == null)
        {
            liquidByCode = new Dictionary<string, bool>();
            foreach (var offer in Offers)
            {
                var item = api.World.GetItem(new AssetLocation(offer.Code));
                liquidByCode[offer.Code] =
                    item != null && BlockLiquidContainerBase.GetContainableProps(new ItemStack(item)) != null;
            }
        }
        return liquidByCode.TryGetValue(code, out bool isLiquid) && isLiquid;
    }

    // The only places a player actually carries goods. Deliberately excludes the creative
    // inventory (which holds one of everything) and the mouse/ground/crafting slots.
    private static IEnumerable<ItemSlot> CarriedSlots(IServerPlayer player)
    {
        foreach (string invName in new[] { GlobalConstants.hotBarInvClassName, GlobalConstants.backpackInvClassName })
        {
            IInventory inv = player.InventoryManager?.GetOwnInventory(invName);
            if (inv == null) continue;
            for (int i = 0; i < inv.Count; i++)
            {
                var slot = inv[i];
                if (slot?.Itemstack != null) yield return slot;
            }
        }
    }

    private static bool Matches(ItemStack stack, AssetLocation code)
        => stack?.Collectible?.Code != null && stack.Collectible.Code.Equals(code);

    // Whole litres of `code` held in the liquid container in this slot, if any.
    private static int LitresOf(ItemSlot slot, AssetLocation code)
    {
        var stack = slot.Itemstack;
        if (stack?.Block is not BlockLiquidContainerBase container) return 0;
        if (!Matches(container.GetContent(stack), code)) return 0;
        return (int)container.GetCurrentLitres(stack);
    }

    private static int CountUnits(IServerPlayer player, AssetLocation code, bool liquid)
    {
        int n = 0;
        foreach (var slot in CarriedSlots(player))
        {
            n += liquid
                ? LitresOf(slot, code)
                : (Matches(slot.Itemstack, code) ? slot.Itemstack.StackSize : 0);
        }
        return n;
    }

    private static int TakeUnits(IServerPlayer player, AssetLocation code, bool liquid, int want)
    {
        int taken = 0;
        foreach (var slot in CarriedSlots(player))
        {
            if (taken >= want) break;

            if (liquid)
            {
                if (slot.Itemstack?.Block is not BlockLiquidContainerBase container) continue;
                int litres = LitresOf(slot, code);
                if (litres <= 0) continue;

                var props = BlockLiquidContainerBase.GetContainableProps(container.GetContent(slot.Itemstack));
                if (props == null || props.ItemsPerLitre <= 0) continue;

                int move = Math.Min(litres, want - taken);
                container.TryTakeContent(slot.Itemstack, (int)Math.Round(move * props.ItemsPerLitre));
                slot.MarkDirty();
                taken += move;
            }
            else
            {
                if (!Matches(slot.Itemstack, code)) continue;
                int move = Math.Min(slot.Itemstack.StackSize, want - taken);
                slot.TakeOut(move);
                slot.MarkDirty();
                taken += move;
            }
        }
        return taken;
    }
}
