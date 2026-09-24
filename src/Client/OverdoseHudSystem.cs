using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using VsDope.Systems;

namespace VsDope.Client;

public sealed class OverdoseHudSystem : ModSystem
{
    private ICoreClientAPI capi = null!;
    private OverdoseHud? dialog;
    private long tickId;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        dialog = new OverdoseHud(api);
        tickId = api.Event.RegisterGameTickListener(OnTick, 250);
        api.Event.LeftWorld += Close;
    }

    private void OnTick(float dt)
    {
        var entity = capi.World.Player?.Entity;
        if (entity?.Alive != true) { Close(); return; }
        float risk = entity.WatchedAttributes.GetFloat(OverdoseSystem.WatchRisk);
        bool active = entity.WatchedAttributes.GetBool(OverdoseSystem.WatchActive);
        if (!active && risk < OverdoseSystem.WarningChance) { Close(); return; }
        float severity = entity.WatchedAttributes.GetFloat(OverdoseSystem.WatchSeverity);
        string text = !active
            ? Lang.Get("vs-dope:overdose-warning", (int)Math.Round(risk * 100))
            : Lang.Get("vs-dope:" + (severity > OverdoseSystem.DamageSeverityGate ? "overdose-severe" : "overdose-active"));
        dialog!.Update(text, active);
        if (!dialog.IsOpened()) dialog.TryOpen();
    }

    private void Close() => dialog?.TryClose();

    public override void Dispose()
    {
        if (capi != null)
        {
            capi.Event.UnregisterGameTickListener(tickId);
            capi.Event.LeftWorld -= Close;
        }
        dialog?.Dispose();
        base.Dispose();
    }
}

public sealed class OverdoseHud : HudElement
{
    private string text = "";
    private bool active;
    public OverdoseHud(ICoreClientAPI api) : base(api) { }
    public override string ToggleKeyCombinationCode => null!;

    public void Update(string nextText, bool nextActive)
    {
        if (text == nextText && active == nextActive) return;
        text = nextText;
        active = nextActive;
        SingleComposer?.Dispose();
        var bounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.RightTop)
            .WithFixedAlignmentOffset(-20, 140);
        SingleComposer = capi.Gui.CreateCompo("vsdope-overdose", bounds)
            .AddShadedDialogBG(ElementBounds.Fill, false)
            .AddStaticText(text, CairoFont.WhiteSmallText().WithColor(ColorUtil.Hex2Doubles(active ? "#ff7777" : "#ffd070")),
                ElementBounds.FixedSize(300, 62))
            .Compose();
    }
}
