using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using VsDope.Items;
using VsDope.Systems;

namespace VsDope.Client;

/// <summary>
/// Shows a small HUD countdown while Coca Vitae is active. The server writes the
/// expiry in calendar hours, so the display follows the world's in-game clock.
/// </summary>
public class CocaVitaeEffectHudSystem : ModSystem
{
    private ICoreClientAPI capi = null!;
    private GuiDialogCocaVitaeEffect? dialog;
    private long tickId;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        dialog = new GuiDialogCocaVitaeEffect(api);
        tickId = capi.Event.RegisterGameTickListener(OnTick, 250);
        capi.Event.LeftWorld += Close;
    }

    private void OnTick(float dt)
    {
        var entity = capi.World.Player?.Entity;
        if (entity?.Alive != true) { Close(); return; }
        if (dialog == null) return;

        double expires = entity.WatchedAttributes.GetDouble(DrugConsumableItem.SpeedEffectKeyFor("coca-vitae") + "-expires-gamehour");
        double remaining = expires - capi.World.Calendar.TotalHours;
        double crashRemaining = entity.WatchedAttributes.GetDouble(DrugToolEffects.CrashExpiryKey) - capi.World.Calendar.TotalHours;

        if (remaining > 0 || crashRemaining > 0)
        {
            dialog.SetRemaining(remaining, crashRemaining);
            if (!dialog.IsOpened()) dialog.TryOpen();
        }
        else
        {
            Close();
        }
    }

    private void Close() => dialog?.TryClose();

    public override void Dispose()
    {
        if (capi != null)
        {
            if (tickId != 0) capi.Event.UnregisterGameTickListener(tickId);
            capi.Event.LeftWorld -= Close;
        }
        Close();
        dialog?.Dispose();
        base.Dispose();
    }
}

public class GuiDialogCocaVitaeEffect : HudElement
{
    private string remainingText = "";

    public GuiDialogCocaVitaeEffect(ICoreClientAPI capi) : base(capi) { }

    public override string ToggleKeyCombinationCode => null!;

    // A countdown must never release mouse-look or intercept inventory/gameplay input.
    public override bool Focusable => false;
    public override bool ShouldReceiveMouseEvents() => false;

    public void SetRemaining(double gameHours, double crashHours)
    {
        int totalMinutes = Math.Max(0, (int)Math.Ceiling(gameHours * 60));
        string nextText = gameHours > 0 ? Lang.Get("vs-dope:coca-vitae-hud-remaining", totalMinutes / 60, (totalMinutes % 60).ToString("00")) : "";
        if (crashHours > 0)
        {
            int crashMinutes = Math.Max(0, (int)Math.Ceiling(crashHours * 60));
            if (nextText.Length > 0) nextText += "\n";
            nextText += Lang.Get("vs-dope:coca-crash-hud", crashMinutes);
        }
        if (remainingText == nextText) return;
        remainingText = nextText;
        Compose();
    }

    private void Compose()
    {
        SingleComposer?.Dispose();
        ElementBounds textBounds = ElementBounds.FixedSize(250, remainingText.Contains('\n') ? 55 : 30);
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightTop)
            .WithFixedAlignmentOffset(-20, 90);

        SingleComposer = capi.Gui
            .CreateCompo("cocavitaeeffecthud", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill, false)
            .AddStaticText(remainingText, CairoFont.WhiteSmallText(), textBounds)
            .Compose();
    }
}
