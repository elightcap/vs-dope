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

    // Above-market gear price a stranger pays per sellable unit, in menu order. Known customers
    // pay more (AddictLedger.PriceFor).
    // Solid drugs sell per item; liquid drugs sell per dose (DoseLitres) out of whatever container
    // holds them (heroin is an ItemLiquidPortion and never sits in a slot as a loose stack).
    // Heroin used to sell by the litre at 30: only ~1 in 10 addicts could afford a single litre.
    // At 9 per dose nearly every addict can afford one, and a typical visit buys about two.
    private static readonly (string Code, int GearPrice)[] Offers =
    {
        ("vs-dope:opium", 5),
        ("vs-dope:morphine", 12),
        ("vs-dope:coca-vitae", 16),
        ("vs-dope:heroin", 9),
    };

    // One dose of a liquid drug: the same 0.1 L as a syringe injection (AddictionSystem.ApplyHeroinDose).
    public const float DoseLitres = 0.1f;

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
        channel.RegisterMessageType<CloseAddictTradePacket>();
        channel.SetMessageHandler<SellToAddictPacket>(OnSell);
    }

    // Tell the addict's current customer to close its trade window (the addict died,
    // fled or is walking away). Safe to call when no window is open.
    public void CloseTradeFor(EntityDrugAddict addict)
    {
        if (addict == null || string.IsNullOrEmpty(addict.TargetPlayerUid)) return;
        if (api.World.PlayerByUid(addict.TargetPlayerUid) is not IServerPlayer player) return;
        if (player.ConnectionState != EnumClientState.Playing) return;

        channel.SendPacket(new CloseAddictTradePacket { AddictEntityId = addict.EntityId }, new[] { player });
    }

    public void OpenTradeFor(IServerPlayer player, EntityDrugAddict addict) => SendState(player, addict, refresh: false);

    // Snapshot of both sides of the deal: offers, what the player carries, and the addict's pockets.
    // Sent on open and again after every sale (refresh) so the window stays live.
    private void SendState(IServerPlayer player, EntityDrugAddict addict, bool refresh)
    {
        if (player == null || addict == null) return;
        if (player.ConnectionState != EnumClientState.Playing) return;

        var codes = new string[Offers.Length];
        var prices = new int[Offers.Length];
        var units = new string[Offers.Length];
        var held = new int[Offers.Length];
        for (int i = 0; i < Offers.Length; i++)
        {
            bool liquid = IsLiquid(Offers[i].Code);
            codes[i] = Offers[i].Code;
            prices[i] = PriceFor(addict, player.PlayerUID, Offers[i].Code);
            units[i] = liquid ? "dose" : "ea";
            held[i] = CountUnits(player, new AssetLocation(Offers[i].Code), liquid);
        }

        var pockets = addict.Pockets;
        var stacks = new List<AddictStackData>();
        if (pockets != null)
        {
            foreach (var stack in pockets.Stacks) stacks.Add(new AddictStackData { Stack = stack.ToBytes() });
        }

        channel.SendPacket(new OpenAddictTradePacket
        {
            AddictEntityId = addict.EntityId,
            DrugCodes = codes,
            GearPrices = prices,
            Units = units,
            PlayerHeld = held,
            PlayerGears = CountUnits(player, AddictPockets.GearCode, false),
            AddictGears = pockets?.GearCount ?? 0,
            AddictGoodsValue = pockets?.GoodsValue ?? 0,
            AddictStacks = stacks.ToArray(),
            AddictName = addict.Record is { } record ? AddictReputationSystem.NameOf(record) : "",
            AddictTierKey = addict.Record is { } r ? AddictReputationSystem.TierLangKey(AddictLedger.TierOf(r, player.PlayerUID)) : "",
            Refresh = refresh,
        }, new[] { player });
    }

    private void OnSell(IServerPlayer player, SellToAddictPacket packet)
    {
        if (player?.Entity == null || packet == null) return;

        if (PriceOf(packet.DrugCode) <= 0) return;

        // Every rejection below reports back. Silent returns here are what made a broken
        // trade look like a dead button.
        var addict = api.World.GetEntityById(packet.AddictEntityId) as EntityDrugAddict;
        if (addict == null || !addict.Alive)
        {
            Tell(player, "addict-gone");
            return;
        }
        // Only trade with the addict that is currently dealing with you.
        if (!addict.Friendly || addict.TargetPlayerUid != player.PlayerUID)
        {
            Tell(player, "addict-not-yours");
            return;
        }
        // After a sale the addict only lingers briefly before walking off.
        if (!addict.AcceptsTrade)
        {
            Tell(player, "addict-busy-leaving");
            return;
        }
        var pockets = addict.Pockets;
        if (pockets == null)
        {
            Tell(player, "addict-sell-failed");
            return;
        }
        int pricePerUnit = PriceFor(addict, player.PlayerUID, packet.DrugCode);

        var code = new AssetLocation(packet.DrugCode);
        bool liquid = IsLiquid(packet.DrugCode);
        var drugItem = api.World.GetItem(code);
        if (drugItem == null)
        {
            Tell(player, "addict-sell-failed");
            return;
        }

        int available = CountUnits(player, code, liquid);
        if (available <= 0)
        {
            Tell(player, liquid ? "addict-sell-no-container" : "addict-sell-none");
            return;
        }

        // The addict pays from its own pockets: gears first, scavenged goods for the rest.
        int affordable = pockets.Wealth / pricePerUnit;
        if (affordable <= 0)
        {
            Tell(player, "addict-cant-afford");
            SendState(player, addict, refresh: true);
            return;
        }

        int wanted = packet.Quantity <= 0 ? available : Math.Min(packet.Quantity, available);
        int qty = Math.Min(wanted, affordable);
        int taken = TakeUnits(player, code, liquid, qty);
        if (taken <= 0)
        {
            Tell(player, "addict-sell-failed");
            return;
        }

        // The drugs go into the addict's pockets (and drop if it dies). Liquids are poured into jugs.
        if (liquid) pockets.AddLiquidPortions(api.World, drugItem, taken * DosePortions(drugItem));
        else pockets.Add(api.World, new ItemStack(drugItem, taken));

        var payment = pockets.TakePayment(api.World, taken * pricePerUnit, out int gearsPaid);
        addict.SavePockets();

        // The addict remembers who sold to it; the first sale of a visit counts towards its tier.
        var record = addict.Record;
        if (record != null && AddictReputationSystem.Instance is { } rep)
        {
            rep.Ledger.RecordSale(record, player.PlayerUID, taken, taken * pricePerUnit,
                AddictReputationSystem.ExposureOf(packet.DrugCode), addict.ConsumeFirstSaleOfVisit(), rep.Today);
        }

        var goods = new List<string>();
        foreach (var stack in payment)
        {
            if (!AddictPockets.IsGear(stack))
                goods.Add(Lang.Get("vs-dope:addict-goods-entry", stack.StackSize, stack.GetName()));
            Give(player, stack);
        }

        if (taken < wanted) Tell(player, "addict-afford-partial", taken);
        string unitKey = liquid ? "addict-sold-doses" : "addict-sold";
        if (goods.Count > 0) Tell(player, unitKey + "-goods", taken, gearsPaid, string.Join(", ", goods));
        else Tell(player, unitKey, taken, gearsPaid);

        SendState(player, addict, refresh: true);

        // Thanks the player and starts the leave timer: the addict lingers briefly (each sale
        // resets it) so the player can keep selling, then walks off and despawns out of sight.
        addict.OnPurchaseCompleted();

        // Chance the addict ODs on the high after a deal; it then dies on the spot instead
        // (and drops everything it carries, including what it just bought).
        // A body already wrecked by long use is more likely to give out.
        double odChance = record != null ? AddictLedger.OverdoseChanceFor(OverdoseChancePerSale, record) : OverdoseChancePerSale;
        if (rng.NextDouble() < odChance) addict.Overdose();
    }

    // Hand a stack to the player; whatever doesn't fit lands at their feet.
    private void Give(IServerPlayer player, ItemStack stack)
    {
        int max = Math.Max(1, stack.Collectible.MaxStackSize);
        while (stack.StackSize > 0)
        {
            var part = stack.Clone();
            part.StackSize = Math.Min(max, stack.StackSize);
            stack.StackSize -= part.StackSize;
            // TryGiveItemstack leaves the undelivered remainder in part.StackSize.
            player.InventoryManager.TryGiveItemstack(part, true);
            if (part.StackSize > 0) api.World.SpawnItemEntity(part, player.Entity.Pos.XYZ);
        }
    }

    private static void Tell(IServerPlayer player, string langKey, params object[] args)
        => player.SendLocalisedMessage(0, "vs-dope:" + langKey, args);

    // What this addict pays this player per unit: the base price, raised for known customers.
    private static int PriceFor(EntityDrugAddict addict, string playerUid, string code)
    {
        int basePrice = PriceOf(code);
        var record = addict.Record;
        return record == null || basePrice <= 0 ? basePrice : AddictLedger.PriceFor(basePrice, AddictLedger.TierOf(record, playerUid));
    }

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
            IInventory? inv = player.InventoryManager?.GetOwnInventory(invName);
            if (inv == null) continue;
            for (int i = 0; i < inv.Count; i++)
            {
                var slot = inv[i];
                if (slot?.Itemstack != null) yield return slot;
            }
        }
    }

    private static bool Matches(ItemStack? stack, AssetLocation code)
        => stack?.Collectible?.Code != null && stack.Collectible.Code.Equals(code);

    // Liquid portions in one dose of this liquid (heroin: 100 per litre -> 10).
    private static int DosePortions(CollectibleObject liquid)
    {
        var props = BlockLiquidContainerBase.GetContainableProps(new ItemStack(liquid));
        return props == null || props.ItemsPerLitre <= 0 ? 0 : Math.Max(1, (int)Math.Round(props.ItemsPerLitre * DoseLitres));
    }

    // Whole doses of `code` held in the liquid container in this slot, if any.
    private static int DosesOf(ItemSlot slot, AssetLocation code)
    {
        var stack = slot.Itemstack;
        if (stack?.Block is not BlockLiquidContainerBase container) return 0;
        var content = container.GetContent(stack);
        if (content == null || !Matches(content, code)) return 0;
        int perDose = DosePortions(content.Collectible);
        return perDose <= 0 ? 0 : content.StackSize / perDose;
    }

    private static int CountUnits(IServerPlayer player, AssetLocation code, bool liquid)
    {
        int n = 0;
        foreach (var slot in CarriedSlots(player))
        {
            n += liquid
                ? DosesOf(slot, code)
                : (Matches(slot.Itemstack, code) ? slot.StackSize : 0);
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
                int doses = DosesOf(slot, code);
                if (doses <= 0) continue;

                int move = Math.Min(doses, want - taken);
                // DosesOf > 0 means the container holds this liquid.
                container.TryTakeContent(slot.Itemstack, move * DosePortions(container.GetContent(slot.Itemstack)!.Collectible));
                slot.MarkDirty();
                taken += move;
            }
            else
            {
                if (!Matches(slot.Itemstack, code)) continue;
                int move = Math.Min(slot.StackSize, want - taken);
                slot.TakeOut(move);
                slot.MarkDirty();
                taken += move;
            }
        }
        return taken;
    }
}
