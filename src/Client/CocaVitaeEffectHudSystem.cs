using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsDope.Client;

/// <summary>
/// Shows a small HUD countdown while Coca Vitae is active. The server writes the
/// expiry in calendar hours, so the display follows the world's in-game clock.
/// </summary>
public class CocaVitaeEffectHudSystem : ModSystem
{
    private ICoreClientAPI capi;
    private GuiDialogCocaVitaeEffect dialog;
    private long tickId;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;
        dialog = new GuiDialogCocaVitaeEffect(api);
        tickId = capi.Event.RegisterGameTickListener(OnTick, 250);
    }

    private void OnTick(float dt)
    {
        var entity = capi.World.Player?.Entity;
        if (entity == null) return;

        double expires = entity.WatchedAttributes.GetDouble("vs-dope-coca-vitae-speed-expires-gamehour");
        double remaining = expires - capi.World.Calendar.TotalHours;

        if (remaining > 0)
        {
            dialog.SetRemaining(remaining);
            if (!dialog.IsOpened()) dialog.TryOpen();
        }
        else if (dialog.IsOpened())
        {
            dialog.TryClose();
        }
    }

    public override void Dispose()
    {
        if (capi?.Event != null && tickId != 0) capi.Event.UnregisterGameTickListener(tickId);
        dialog?.Dispose();
        base.Dispose();
    }
}

public class GuiDialogCocaVitaeEffect : GuiDialog
{
    private string remainingText = "Coca Vitae: 1h 00m";

    public GuiDialogCocaVitaeEffect(ICoreClientAPI capi) : base(capi) { }

    public override string ToggleKeyCombinationCode => null;

    public void SetRemaining(double gameHours)
    {
        int totalMinutes = System.Math.Max(0, (int)System.Math.Ceiling(gameHours * 60));
        remainingText = $"Coca Vitae: {totalMinutes / 60}h {totalMinutes % 60:00}m";
        Compose();
    }

    private void Compose()
    {
        ElementBounds textBounds = ElementBounds.FixedSize(220, 30);
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightTop)
            .WithFixedAlignmentOffset(-20, 90);

        SingleComposer = capi.Gui
            .CreateCompo("cocavitaeeffecthud", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill, false)
            .AddStaticText(remainingText, CairoFont.WhiteSmallText(), textBounds)
            .Compose();
    }

    public override bool TryOpen()
    {
        Compose();
        return base.TryOpen();
    }
}
