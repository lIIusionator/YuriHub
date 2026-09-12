using Avalonia.Controls;
using Avalonia.Media;
using Yuri.Gfx;

namespace Yuri.Shell;

/// <summary>
/// uiRes / scaleF. The .ahk drew at (A_ScreenDPI/96) * uiRes physical pixels
/// per logical unit; Avalonia applies the DPI itself, so the surfaces scale
/// by uiRes alone — a gentle fourth-root of the screen's size over 1080p,
/// clamped to 0.85..1.25 — and by the hub's own magnifier on top of that.
/// </summary>
public static class UiScale
{
    public static double Factor { get; private set; } = 1.0;          // uiRes
    public static double WorkW { get; private set; } = 1920;           // the primary work area, in logical units
    public static double WorkH { get; private set; } = 1040;

    public static void Refresh(Window w)
    {
        var s = w.Screens.Primary ?? w.Screens.All.FirstOrDefault();
        if (s is null) return;
        double rw = s.Bounds.Width, rh = s.Bounds.Height;             // physical, like GetSystemMetrics
        Factor = (rw < 320 || rh < 240) ? 1.0 : Math.Clamp(Math.Pow(Math.Min(rw / 1920.0, rh / 1080.0), 0.25), 0.85, 1.25);
        WorkW = s.WorkingArea.Width / s.Scaling;
        WorkH = s.WorkingArea.Height / s.Scaling;
    }
}

/// <summary>
/// The .ahk's `Gui("-Caption +E0x80000 ...")` layered window: no frame, no
/// background, the surface is the whole window and every pixel of it is
/// drawn by the surface's frame. Transparent on Windows (DWM) and macOS.
/// Centred on the primary screen's work area, as
/// MonitorGetWorkArea(MonitorGetPrimary()) did.
/// </summary>
public class LayeredWindow : Window
{
    public readonly Surface Surface;

    // ---- the app's mark ----
    // One per accent, because the mark IS the accent - the same reasoning the
    // .ahk's tray icon is drawn from `hubAccent` rather than shipped as a file.
    // A frameless window shows no title bar, but the taskbar, the switcher and
    // the window list all read Icon, and without it they fall back to a blank
    // sheet. The EXECUTABLE's icon cannot follow anything at run time, so that
    // one carries the default accent; this is everything that can.
    static readonly Dictionary<int, WindowIcon?> _icons = new();
    static readonly List<LayeredWindow> _live = new();
    public static int IconIndex()
    {
        for (int i = 0; i < Gfx.HubState.Accents.Length; i++) if (Gfx.HubState.Accents[i] == Gfx.HubState.Accent) return i + 1;
        return 1;
    }
    public static WindowIcon? AppIcon(int idx)
    {
        if (_icons.TryGetValue(idx, out var have)) return have;
        WindowIcon? ic = null;
        try { if (Gfx.Img.Asset("appicon_" + idx + ".png") is { } b) ic = new WindowIcon(b); } catch { }
        _icons[idx] = ic;
        return ic;
    }
    /// <summary>The accent changed: every open window takes the new mark.</summary>
    public static void IconRefresh()
    {
        var ic = AppIcon(IconIndex());
        if (ic is null) return;
        foreach (var w in _live) { try { w.Icon = ic; } catch { } }
    }


    public LayeredWindow(Surface surface, string title, bool topmost, bool toolWindow, Func<double, double>? scale = null)
    {
        Surface = surface;
        Title = title;
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = topmost;
        ShowInTaskbar = !toolWindow;
        if (AppIcon(IconIndex()) is { } ic) Icon = ic;
        _live.Add(this);
        Closed += (_, _) => _live.Remove(this);
        UiScale.Refresh(this);
        surface.SetScale(scale is null ? UiScale.Factor : scale(UiScale.Factor));
        Content = surface;
        Closed += (_, _) => surface.Stop();
    }
}
