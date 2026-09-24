using System.Reflection;
using System.Runtime.Loader;
using Vintagestory.API.Client;
using VsDope.Client;

string gamePath = Environment.GetEnvironmentVariable("VINTAGE_STORY")
    ?? throw new InvalidOperationException("Set VINTAGE_STORY to the 1.22.7 install directory.");
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    foreach (string folder in new[] { "", "Lib", "Mods" })
    {
        string path = Path.Combine(gamePath, folder, name.Name + ".dll");
        if (File.Exists(path)) return context.LoadFromAssemblyPath(Path.GetFullPath(path));
    }
    return null;
};

// Actual mod HUD and native dialog lifecycle; only the GUI host is stubbed.
// Use the bool overload to omit text composition, which requires a graphics context.
var loaded = new List<GuiDialog>();
int focusRequests = 0, checks = 0, failures = 0;
var gui = HostProxy.Create<IGuiAPI>((method, args) =>
{
    switch (method)
    {
        case "get_LoadedGuis": return loaded;
        case "RegisterDialog": loaded.AddRange((GuiDialog[])args![0]!); return null;
        case "RequestFocus": focusRequests++; ((GuiDialog)args![0]!).Focus(); return null;
        case "TriggerDialogOpened":
        case "TriggerDialogClosed": return null;
        default: throw new NotSupportedException(method);
    }
});
var api = HostProxy.Create<ICoreClientAPI>((method, _) => method == "get_Gui"
    ? gui : throw new NotSupportedException(method));
using var hud = new GuiDialogCocaVitaeEffect(api);

void Check(bool ok, string name)
{
    checks++;
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}: {name}");
}

for (int cycle = 0; cycle < 2; cycle++)
{
    hud.TryOpen(true);
    Check(hud.IsOpened() && hud.ShouldReceiveRenderEvents(), "countdown opens and renders");
    Check(hud.DialogType == EnumDialogType.HUD, "countdown is excluded from the engine's open-dialog count");
    Check(!hud.PrefersUngrabbedMouse && !hud.DisableMouseGrab, "countdown preserves mouse grab");
    Check(focusRequests == 0, "opening countdown does not request focus");
    hud.Focus();
    Check(!hud.Focused && !hud.ShouldReceiveKeyboardEvents(), "countdown cannot steal keyboard focus");
    Check(!hud.ShouldReceiveMouseEvents(), "mouse events pass to gameplay or interactive dialogs");
    Check(!hud.CaptureAllInputs() && !hud.CaptureRawMouse(), "countdown does not capture inputs");
    Check(!hud.OnEscapePressed() && hud.IsOpened(), "Escape passes through without closing countdown");
    hud.TryClose();
    Check(!hud.IsOpened() && !hud.ShouldReceiveRenderEvents(), "closing hides countdown");
    Check(!hud.Focused && !hud.ShouldReceiveMouseEvents(), "closing releases all input participation");
}
Check(loaded.Count == 1, "reopening does not duplicate dialog registration");
Console.WriteLine($"{checks - failures}/{checks} checks passed");
return failures == 0 ? 0 : 1;

public class HostProxy : DispatchProxy
{
    public System.Func<string, object?[]?, object?> Handler = null!;
    public static T Create<T>(System.Func<string, object?[]?, object?> handler) where T : class
    {
        var proxy = Create<T, HostProxy>();
        ((HostProxy)(object)proxy).Handler = handler;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!.Name, args);
}
