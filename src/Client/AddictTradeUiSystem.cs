using Vintagestory.API.Client;
using Vintagestory.API.Common;
using VsDope.Network;

namespace VsDope.Client;

/// <summary>
/// Client half of the addict trading window. Listens for the server's open request
/// and pops up a trader-style dialog where the player sells drugs for rusty gears.
/// </summary>
public class AddictTradeUiSystem : ModSystem
{
    private ICoreClientAPI capi;
    private GuiDialogAddictTrade dialog;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;

        var channel = api.Network.RegisterChannel("vs-dope.addicttrade");
        channel.RegisterMessageType<SellToAddictPacket>();
        channel.RegisterMessageType<OpenAddictTradePacket>();
        channel.RegisterMessageType<CloseAddictTradePacket>();
        channel.SetMessageHandler<OpenAddictTradePacket>((packet) => dialog.Open(packet));
        channel.SetMessageHandler<CloseAddictTradePacket>((packet) => dialog.CloseFor(packet.AddictEntityId));

        dialog = new GuiDialogAddictTrade(api);
    }
}

public class GuiDialogAddictTrade : GuiDialog
{
    private const float RowHeight = 44f;
    private const float Width = 390f;

    private OpenAddictTradePacket data;
    private InventoryBase[] displayInventories;

    public GuiDialogAddictTrade(ICoreClientAPI capi) : base(capi) { }

    public override string ToggleKeyCombinationCode => null;

    public void Open(OpenAddictTradePacket packet)
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
        int count = data?.DrugCodes?.Length ?? 0;
        displayInventories = new InventoryBase[count];
        for (int i = 0; i < count; i++)
        {
            var inv = new InventoryGeneric(1, "vs-dope:addictdisplay" + i, capi);
            var item = capi.World.GetItem(new AssetLocation(data.DrugCodes[i]));
            inv[0].Itemstack = item == null ? null : new ItemStack(item, 1);
            displayInventories[i] = inv;
        }
    }

    private void Compose()
    {
        int rows = data?.DrugCodes?.Length ?? 0;
        float bodyHeight = 34f + rows * RowHeight + 8f;

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);

        ElementBounds fill = ElementBounds.Fill.WithFixedSize(Width, bodyHeight);
        ElementBounds inner = ElementBounds.Fixed(0, 0, Width, bodyHeight);

        var compo = capi.Gui.CreateCompo("addicttrade", dialogBounds)
            .AddShadedDialogBG(fill, false)
            .AddDialogTitleBarWithBg(
                "Drug Addict",
                OnClose,
                CairoFont.WhiteDetailText().WithFontSize(16),
                ElementBounds.Fixed(0, 0, Width, 30),
                "titlebar");

        for (int i = 0; i < rows; i++)
        {
            float y = 34f + i * RowHeight;
            string code = data.DrugCodes[i];
            int price = data.GearPrices[i];
            string unit = i < (data.Units?.Length ?? 0) ? data.Units[i] : "ea";

            ElementBounds slotBounds = ElementBounds.Fixed(10, y, 32, 32);
            ElementBounds nameBounds = ElementBounds.Fixed(52, y + 6, 150, 20);
            ElementBounds priceBounds = ElementBounds.Fixed(205, y + 6, 78, 20);
            ElementBounds sell1Bounds = ElementBounds.Fixed(286, y + 4, 46, 24);
            ElementBounds sellAllBounds = ElementBounds.Fixed(334, y + 4, 50, 24);

            string drugName = displayInventories[i][0]?.Itemstack?.GetName() ?? code;

            compo = compo
                .AddPassiveItemSlot(slotBounds, displayInventories[i], displayInventories[i][0], false, "slot" + i)
                .AddStaticText(drugName, CairoFont.WhiteSmallText(), EnumTextOrientation.Left, nameBounds, "name" + i)
                .AddStaticText(price + "g " + unit, CairoFont.WhiteSmallText(), EnumTextOrientation.Right, priceBounds, "price" + i)
                .AddSmallButton("Sell 1", OnSellOne(code), sell1Bounds, EnumButtonStyle.Small, "sell1_" + i)
                .AddSmallButton("Sell all", OnSellAll(code), sellAllBounds, EnumButtonStyle.Small, "sellall_" + i);
        }

        SingleComposer = compo.Compose();
    }

    private ActionConsumable OnSellOne(string code)
        => () => { SendSell(code, 1); return true; };

    private ActionConsumable OnSellAll(string code)
        => () => { SendSell(code, -1); return true; };

    private void SendSell(string code, int quantity)
    {
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
