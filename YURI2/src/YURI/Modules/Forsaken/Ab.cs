using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Forsaken;

/// <summary>
/// EXTERNAL BLOCK REBIND (abOn / enabled): armed from the hub's switch, the
/// bind key toggles it in game; while it is on and Roblox is in front, the
/// left button is swallowed and held as Q (Guest 1337's block) for as long
/// as it is down. Windows-only - it is a keyboard and mouse hook.
/// </summary>
public static class Ab
{
    public static bool On, Enabled; public static long FlashAt, TogAt, LastClick; public static int QCount, TogCount; public static double StateT;
    const int VK_Q = 0x51, VK_LBUTTON = 1;
    static bool _hooked, _qHeld;
    public static void Register()
    {
        Keys.Load();
        if (Os.IsWin && OperatingSystem.IsWindows()) { WinHooks.OnKey = OnKey; WinHooks.OnMouse = OnMouse; }
    }
    public static void Enable()
    {
        if (On) return;
        if (!Os.IsWin) { Puz.Say("EXTERNAL BLOCK REBIND IS A WINDOWS FEATURE", false); return; }
        string ck = Puz.ClashOn(1);
        if (ck != "") { Puz.ClashRaise("EXTERNAL BLOCK REBIND", "PUZZLE AI", ck); return; }
        On = true; FlashAt = Clock.Tick; Enabled = false; StateT = 0;
        if (OperatingSystem.IsWindows()) { WinHooks.Ensure(); _hooked = true; FskOverlays.AbSync(); }
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Disable()
    {
        if (!On) return;
        On = false; FlashAt = Clock.Tick;
        if (Enabled) { Enabled = false; ReleaseQ(); }
        StateT = 0;
        if (OperatingSystem.IsWindows()) FskOverlays.AbSync();
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Toggle() { if (On) Disable(); else Enable(); }
    /// <summary>ToggleBind: the bind key in game arms or disarms the block.</summary>
    public static void ToggleBind()
    {
        if (!On || Keys.RebindOn) return;
        TogCount++;
        Enabled = !Enabled; TogAt = Clock.Tick;
        if (!Enabled) ReleaseQ();
        // The card owns the state ease, the ripple and the sweep. Without this
        // its TogAt / AnimStart were never set, so a toggle played no animation
        // at all - the label switched and nothing else moved.
        if (OperatingSystem.IsWindows()) FskOverlays.AbToggled(Enabled ? 1 : -1, Enabled ? 1.0 : 0.0);
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    static void ReleaseQ() { if (_qHeld && OperatingSystem.IsWindows()) { WinHooks.KeyUp(VK_Q); } _qHeld = false; }
    static bool OnKey(int vk, bool down, bool injected)
    {
        if (injected) return false;
        if (Keys.RebindOn)
        {
            if (!down) return true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => Keys.Feed(Keys.VkName(vk)));
            return true;
        }
        int bindVk = Keys.VkOf(Keys.BindKey);
        if (On && bindVk != 0 && vk == bindVk) { if (down) Avalonia.Threading.Dispatcher.UIThread.Post(ToggleBind); return false; }   // `*key::` with the key passed on ('~' is not in the .ahk's bind, but swallowing a game key would be worse than the .ahk's behaviour) 
        return Puz.OnKey(vk, down);
    }
    static bool OnMouse(int msg, int x, int y, int xb, bool injected)
    {
        if (Puz.Cal != 0 && Puz.OnMouse(msg, x, y, injected)) return true;
        if (injected) return false;
        string nm = Keys.BtnName(msg, xb);
        bool nmDown = Keys.BtnDown(msg);
        if (Keys.RebindOn)
        {
            if (nm != "" && nmDown) { Avalonia.Threading.Dispatcher.UIThread.Post(() => Keys.Feed(nm)); return true; }
            return nm != "";                                                    // swallow the release of a button we just took
        }
        // ---- a bind on a side or middle mouse button ----
        // The rebind chip accepts these, so they have to reach the same dispatch
        // a keyboard bind does. Both are `*key::` with the press passed on.
        if (nm != "")
        {
            if (On && Keys.BindKey == nm) { if (nmDown) Avalonia.Threading.Dispatcher.UIThread.Post(ToggleBind); return false; }
            if (Puz.OnBtn(nm, nmDown)) return true;
        }
        if (!On || !Enabled || Puz.Cal != 0 || Puz.Solving) return false;
        if (msg == 0x201 && OperatingSystem.IsWindows())
        {
            if (!WinHooks.RobloxInFront()) return false;
            // `!HitZoneAtCursor()` in the .ahk's HotIf. The cards are
            // WS_EX_NOACTIVATE, so clicking one over a focused client leaves
            // Roblox in front - without this the block eats the press and the
            // card's own buttons stop responding while it is active.
            if (FskOverlays.HitAt(x, y)) return false;
            WinHooks.KeyDown(VK_Q); _qHeld = true; LastClick = Clock.Tick; QCount++;
            return true;                                                        // the click itself is swallowed: `*LButton::` without `~`
        }
        if (msg == 0x202 && _qHeld) { ReleaseQ(); return false; }
        return false;
    }
    /// <summary>The hook handlers, reachable without a live Windows hook so the harness can drive them.</summary>
    public static bool FeedKey(int vk, bool down, bool injected = false) => OnKey(vk, down, injected);
    public static bool FeedMouse(int msg, int xb, bool down) => OnMouse(down ? msg : msg + 1, 0, 0, xb, false);
    public static void Exiting() { if (Enabled) ReleaseQ(); if (OperatingSystem.IsWindows()) { FskOverlays.CloseAll(); if (_hooked) WinHooks.Release(); } }
}
