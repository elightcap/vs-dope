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
    }

    private void OnTick(float dt)
    {
        var entity = capi.World.Player?.Entity;
        if (entity == null || dialog == null) return;

        double expires = entity.WatchedAttributes.GetDouble(DrugConsumableItem.SpeedEffectKeyFor("coca-vitae") + "-expires-gamehour");
        double remaining = expires - capi.World.Calendar.TotalHours;
        double crashRemaining = entity.WatchedAttributes.GetDouble(DrugToolEffects.CrashExpiryKey) - capi.World.Calendar.TotalHours;

        if (remaining > 0 || crashRemaining > 0)
        {
            dialog.SetRemaining(remaining, crashRemaining);
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
    private string remainingText = "";

    public GuiDialogCocaVitaeEffect(ICoreClientAPI capi) : base(capi) { }

    public override string ToggleKeyCombinationCode => null!;

    public void SetRemaining(double gameHours, double crashHours)
    {
        int totalMinutes = Math.Max(0, (int)Math.Ceiling(gameHours * 60));
        remainingText = gameHours > 0 ? Lang.Get("vs-dope:coca-vitae-hud-remaining", totalMinutes / 60, (totalMinutes % 60).ToString("00")) : "";
        if (crashHours > 0)
        {
            int crashMinutes = Math.Max(0, (int)Math.Ceiling(crashHours * 60));
            if (remainingText.Length > 0) remainingText += "\n";
            remainingText += Lang.Get("vs-dope:coca-crash-hud", crashMinutes);
        }
        Compose();
    }

    private void Compose()
    {
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

    public override bool TryOpen()
    {
        Compose();
        return base.TryOpen();
    }
}
