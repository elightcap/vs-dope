using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using VsDope.Systems;

namespace VsDope.Client;

public class AddictionCharacterTabSystem : ModSystem
{
    private const string TabName = "addiction";

    private ICoreClientAPI capi = null!;
    private long patchCallback;
    private int attempts;
    private readonly HashSet<GuiDialogCharacterBase> patched = new();

    public override void StartClientSide(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.LeftWorld += OnLeftWorld;
        ArmPatchLoop();
    }

    private void ArmPatchLoop()
    {
        UnregisterPatchCallback();
        attempts = 0;
        patchCallback = capi.Event.RegisterCallback(TryPatch, 1000);
    }

    private void OnLeftWorld()
    {
        patched.Clear();
        ArmPatchLoop();
    }

    private void UnregisterPatchCallback()
    {
        if (patchCallback != 0)
        {
            capi.Event.UnregisterCallback(patchCallback);
            patchCallback = 0;
        }
    }

    private void TryPatch(float dt)
    {
        bool foundAny = false;
        foreach (var gui in capi.Gui.LoadedGuis)
        {
            if (gui is GuiDialogCharacterBase dlg && !patched.Contains(dlg))
            {
                AddAddictionTab(dlg);
                patched.Add(dlg);
                foundAny = true;
            }
        }

        attempts++;
        if (foundAny || attempts >= 60)
            UnregisterPatchCallback();
    }

    private void AddAddictionTab(GuiDialogCharacterBase dlg)
    {
        var tabs = dlg.Tabs;
        foreach (var t in tabs)
            if (t.Name == TabName) return;

        int index = tabs.Count;
        tabs.Add(new GuiTab { Name = TabName, DataInt = index });
        dlg.RenderTabHandlers.Add(RenderAddiction);
    }

    private void RenderAddiction(GuiComposer composer)
    {
        var player = capi.World.Player;
        if (player?.Entity == null) return;

        var wa = player.Entity.WatchedAttributes;
        int level = wa.GetInt(AddictionSystem.WatchAddictionLevel);
        int daysUsed = wa.GetInt(AddictionSystem.WatchDaysUsed);
        bool withdrawal = wa.GetBool(AddictionSystem.WatchWithdrawal);

        string status;
        double[] statusColor;
        if (level <= 0)
        {
            status = Lang.Get("vs-dope:addiction-status-none");
            statusColor = ColorUtil.Hex2Doubles("#7fbf7f");
        }
        else if (withdrawal)
        {
            status = Lang.Get("vs-dope:addiction-status-withdrawal");
            statusColor = ColorUtil.Hex2Doubles("#e05c5c");
        }
        else
        {
            status = Lang.Get("vs-dope:addiction-status-dependent");
            statusColor = ColorUtil.Hex2Doubles("#e0a94f");
        }

        var titleBounds = ElementBounds.FixedSize(300, 28)
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedOffset(15, 15);
        composer.AddStaticText(Lang.Get("vs-dope:addiction-tab-title"), CairoFont.WhiteMediumText(), titleBounds);

        var statusBounds = ElementBounds.FixedSize(300, 24)
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedOffset(15, 50);
        composer.AddStaticText(status, CairoFont.WhiteSmallishText().WithColor(statusColor), statusBounds);

        var levelBounds = ElementBounds.FixedSize(300, 24)
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedOffset(15, 78);
        composer.AddStaticText(Lang.Get("vs-dope:addiction-tab-level", level), CairoFont.WhiteSmallishText(), levelBounds);

        var daysBounds = ElementBounds.FixedSize(300, 24)
            .WithAlignment(EnumDialogArea.LeftTop)
            .WithFixedOffset(15, 106);
        composer.AddStaticText(Lang.Get("vs-dope:addiction-tab-days", daysUsed), CairoFont.WhiteSmallishText(), daysBounds);
    }

    public override void Dispose()
    {
        if (capi?.Event != null) UnregisterPatchCallback();
        base.Dispose();
    }
}
