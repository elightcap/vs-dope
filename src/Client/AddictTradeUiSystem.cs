using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using VsDope.Network;

namespace VsDope.Client;

/// <summary>
/// Client half of the addict trading window. Listens for the server's open/refresh packets
/// and shows a trader-style dialog: the addict's pockets on top (like the vanilla trader's
/// goods grid and "{0} has {1} Gears" line), the drugs it buys below with prices and how much
/// the player carries, and the player's own gear count at the bottom.
/// </summary>
public class AddictTradeUiSystem : ModSystem
{
    private GuiDialogAddictTrade dialog = null!;

    public override void StartClientSide(ICoreClientAPI api)
    {
        var channel = api.Network.RegisterChannel("vs-dope.addicttrade");
        channel.RegisterMessageType<SellToAddictPacket>();
        channel.RegisterMessageType<OpenAddictTradePacket>();
        channel.RegisterMessageType<CloseAddictTradePacket>();
        channel.SetMessageHandler<OpenAddictTradePacket>((packet) => dialog.Receive(packet));
        channel.SetMessageHandler<CloseAddictTradePacket>((packet) => dialog.CloseFor(packet.AddictEntityId));

        dialog = new GuiDialogAddictTrade(api);
    }
}

public class GuiDialogAddictTrade : GuiDialog
{
    private const float Width = 470f;
    private const float Pad = 10f;
    private const float TitleHeight = 34f;
    private const float LineHeight = 22f;
    private const float RowHeight = 44f;
    private const int PocketColumns = 8;
    private const float PocketSlotSize = 44f;
    private const float PocketSlotSpacing = 52f;

    private OpenAddictTradePacket? data;
    private InventoryBase[] offerInventories = Array.Empty<InventoryBase>();
    private InventoryBase? pocketInventory;

    public GuiDialogAddictTrade(ICoreClientAPI capi) : base(capi) { }

    public override string ToggleKeyCombinationCode => null!;

    public void Receive(OpenAddictTradePacket packet)
    {
        if (!packet.Refresh) { Open(packet); return; }

        // Post-sale update: redraw in place, but never pop a window the player already closed.
        if (!IsOpened() || data == null || data.AddictEntityId != packet.AddictEntityId) return;
        data = packet;
        BuildDisplayInventories();
        Compose();
    }

    private void Open(OpenAddictTradePacket packet)
    {
        data = packet;
        BuildDisplayInventories();

        // Force a false->true transition on the open flag. GuiDialog.TryOpen() notifies the
        // GUI manager (which is what actually puts the dialog in the rendered list) only on
        // that transition, while ClientMain.UnregisterDialog() drops a dialog from the list
        // without clearing the flag. Once those two disagree the window can never be shown
        // again, which is what made opening any other UI kill this one permanently.
        if (IsOpened()) TryClose();

        Compose();
        TryOpen();
    }

    // Server says this addict is done trading; close only if the window belongs to it.
    public void CloseFor(long addictEntityId)
    {
        if (IsOpened() && data != null && data.AddictEntityId == addictEntityId) TryClose();
    }

    private void BuildDisplayInventories()
    {
        if (data == null) return;

        int count = data.DrugCodes.Length;
        offerInventories = new InventoryBase[count];
        for (int i = 0; i < count; i++)
        {
            var inv = new InventoryGeneric(1, "vs-dope:addictdisplay" + i, capi);
            var item = capi.World.GetItem(new AssetLocation(data.DrugCodes[i]));
            inv[0].Itemstack = item == null ? null : new ItemStack(item, 1);
            offerInventories[i] = inv;
        }

        var stacks = new List<ItemStack>();
        foreach (var entry in data.AddictStacks)
        {
            if (entry?.Stack == null || entry.Stack.Length == 0) continue;
            try
            {
                var stack = new ItemStack(entry.Stack);
                if (stack.ResolveBlockOrItem(capi.World)) stacks.Add(stack);
            }
            catch (Exception e)
            {
                capi.Logger.Warning("[vs-dope] could not read addict pocket stack: {0}", e.Message);
            }
        }

        pocketInventory = null;
        if (stacks.Count > 0)
        {
            var inv = new InventoryGeneric(stacks.Count, "vs-dope:addictpockets", capi);
            for (int i = 0; i < stacks.Count; i++) inv[i].Itemstack = stacks[i];
            pocketInventory = inv;
        }
    }

    private void Compose()
    {
        if (data == null) return;

        int offers = data.DrugCodes.Length;
        int pocketCount = pocketInventory?.Count ?? 0;
        int pocketRows = Math.Max(1, (pocketCount + PocketColumns - 1) / PocketColumns);
        float pocketHeight = pocketCount > 0 ? pocketRows * PocketSlotSpacing : LineHeight;

        // Vertical layout, top to bottom.
        float yPocketsTitle = TitleHeight;
        float yWealth = yPocketsTitle + LineHeight;
        float yPockets = yWealth + LineHeight + 4f;
        float yBuyTitle = yPockets + pocketHeight + 8f;
        float yPaysNote = yBuyTitle + LineHeight;
        float yRows = yPaysNote + LineHeight + 4f;
        float yFooter = yRows + offers * RowHeight + 6f;
        float bodyHeight = yFooter + 32f;

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);
        ElementBounds fill = ElementBounds.Fill.WithFixedSize(Width, bodyHeight);

        var header = CairoFont.WhiteSmallishText();
        var small = CairoFont.WhiteSmallText();
        var detail = CairoFont.WhiteDetailText();

        var compo = capi.Gui.CreateCompo("addicttrade", dialogBounds)
            .AddShadedDialogBG(fill, false)
            .AddDialogTitleBarWithBg(
                string.IsNullOrEmpty(data.AddictName)
                    ? Lang.Get("vs-dope:item-creature-drugaddict")
                    : Lang.Get("vs-dope:addict-trade-title", data.AddictName, Lang.Get(data.AddictTierKey)),
                OnClose,
                CairoFont.WhiteDetailText().WithFontSize(16),
                ElementBounds.Fixed(0, 0, Width, 30),
                "titlebar")
            // ---- their pockets ----
            .AddStaticText(Lang.Get("vs-dope:addict-trade-pockets-title"), header, EnumTextOrientation.Left,
                ElementBounds.Fixed(Pad, yPocketsTitle, Width - 2 * Pad, LineHeight), "pocketsTitle")
            .AddStaticText(Lang.Get("vs-dope:addict-trade-addict-wealth", data.AddictGears, data.AddictGoodsValue), detail, EnumTextOrientation.Left,
                ElementBounds.Fixed(Pad, yWealth, Width - 2 * Pad, LineHeight), "wealth");

        if (pocketInventory != null)
        {
            for (int i = 0; i < pocketInventory.Count; i++)
            {
                float x = Pad + (i % PocketColumns) * PocketSlotSpacing;
                float y = yPockets + (i / PocketColumns) * PocketSlotSpacing;
                compo = compo.AddPassiveItemSlot(ElementBounds.Fixed(x, y, PocketSlotSize, PocketSlotSize),
                    pocketInventory, pocketInventory[i], true, "pocket" + i);
            }
        }
        else
        {
            compo = compo.AddStaticText(Lang.Get("vs-dope:addict-trade-pockets-empty"), small, EnumTextOrientation.Left,
                ElementBounds.Fixed(Pad, yPockets, Width - 2 * Pad, LineHeight), "pocketsEmpty");
        }

        // ---- what they buy ----
        compo = compo
            .AddStaticText(Lang.Get("vs-dope:addict-trade-buying-title"), header, EnumTextOrientation.Left,
                ElementBounds.Fixed(Pad, yBuyTitle, Width - 2 * Pad, LineHeight), "buyTitle")
            .AddStaticText(Lang.Get("vs-dope:addict-trade-pays-note"), detail, EnumTextOrientation.Left,
                ElementBounds.Fixed(Pad, yPaysNote, Width - 2 * Pad, LineHeight), "paysNote");

        for (int i = 0; i < offers; i++)
        {
            float y = yRows + i * RowHeight;
            string code = data.DrugCodes[i];
            int price = data.GearPrices[i];
            bool perLitre = i < data.Units.Length && data.Units[i] == "/L";
            int held = i < data.PlayerHeld.Length ? data.PlayerHeld[i] : 0;
            string drugName = offerInventories[i][0]?.Itemstack?.GetName() ?? code;

            compo = compo
                .AddPassiveItemSlot(ElementBounds.Fixed(Pad, y, 32, 32), offerInventories[i], offerInventories[i][0], false, "slot" + i)
                .AddStaticText(drugName, small, EnumTextOrientation.Left, ElementBounds.Fixed(52, y + 6, 128, 20), "name" + i)
                .AddStaticText(Lang.Get(perLitre ? "vs-dope:addict-trade-held-litre" : "vs-dope:addict-trade-held", held), detail,
                    EnumTextOrientation.Left, ElementBounds.Fixed(184, y + 8, 88, 20), "held" + i)
                .AddStaticText(Lang.Get(perLitre ? "vs-dope:addict-trade-price-litre" : "vs-dope:addict-trade-price", price), small,
                    EnumTextOrientation.Right, ElementBounds.Fixed(272, y + 6, 70, 20), "price" + i)
                .AddSmallButton(Lang.Get("vs-dope:addict-trade-sell-one"), OnSellOne(code), ElementBounds.Fixed(348, y + 4, 50, 24), EnumButtonStyle.Small, "sell1_" + i)
                .AddSmallButton(Lang.Get("vs-dope:addict-trade-sell-all"), OnSellAll(code), ElementBounds.Fixed(402, y + 4, 58, 24), EnumButtonStyle.Small, "sellall_" + i);
        }

        // ---- footer ----
        compo = compo
            .AddStaticText(Lang.Get("vs-dope:addict-trade-player-gears", data.PlayerGears), small, EnumTextOrientation.Left,
                ElementBounds.Fixed(Pad, yFooter + 4, 260, 20), "playerGears")
            .AddSmallButton(Lang.Get("vs-dope:addict-trade-goodbye"), () => { TryClose(); return true; },
                ElementBounds.Fixed(Width - Pad - 90, yFooter, 90, 26), EnumButtonStyle.Normal, "goodbye");

        SingleComposer = compo.Compose();
    }

    private ActionConsumable OnSellOne(string code)
        => () => { SendSell(code, 1); return true; };

    private ActionConsumable OnSellAll(string code)
        => () => { SendSell(code, -1); return true; };

    private void SendSell(string code, int quantity)
    {
        if (data == null) return;
        capi.Network.GetChannel("vs-dope.addicttrade")
            .SendPacket(new SellToAddictPacket
            {
                AddictEntityId = data.AddictEntityId,
                DrugCode = code,
                Quantity = quantity,
            });
    }

    private void OnClose() => TryClose();
}
