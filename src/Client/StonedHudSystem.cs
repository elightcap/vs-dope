using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using VsDope.Systems;

namespace VsDope.Client;

public sealed class StonedHudSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private StonedHud? hud;
    private long tickId;
    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        hud = new StonedHud(api);
        tickId = api.Event.RegisterGameTickListener(Tick, 250);
        api.Event.LeftWorld += Close;
    }
    private void Tick(float dt)
    {
        var entity = capi!.World.Player?.Entity;
        if (entity == null || !StonedSystem.IsActive(entity, capi.World.Calendar.TotalHours)) { Close(); return; }
        int minutes = (int)Math.Ceiling((entity.WatchedAttributes.GetDouble(StonedSystem.ExpiryKey) - capi.World.Calendar.TotalHours) * 60);
        hud!.Update(Lang.Get("vs-dope:stoned-hud", minutes / 60, (minutes % 60).ToString("00")));
        if (!hud.IsOpened()) hud.TryOpen();
    }
    private void Close() => hud?.TryClose();
    public override void Dispose()
    {
        if (capi != null) { capi.Event.UnregisterGameTickListener(tickId); capi.Event.LeftWorld -= Close; }
        hud?.Dispose();
        base.Dispose();
    }
}
public sealed class StonedHud : HudElement
{
    private string text = "";
    public StonedHud(ICoreClientAPI api) : base(api) { }
    public override string ToggleKeyCombinationCode => null!;
    public void Update(string next)
    {
        if (text == next) return;
        text = next;
        SingleComposer?.Dispose();
        var bounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightTop).WithFixedAlignmentOffset(-20, 220);
        SingleComposer = capi.Gui.CreateCompo("vsdope-stoned", bounds)
            .AddShadedDialogBG(ElementBounds.Fill, false)
            .AddStaticText(text, CairoFont.WhiteSmallText(), ElementBounds.FixedSize(290, 64)).Compose();
    }
}
