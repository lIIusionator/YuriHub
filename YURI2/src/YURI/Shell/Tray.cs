using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Yuri.Core;
using Yuri.Gfx;

namespace Yuri.Shell;

/// <summary>
/// TrayInit / TrayIcoBuild: the system tray entry. Not optional decoration once
/// TRUE MINIMISE exists - it is the only route back to a hidden window, so it is
/// set up unconditionally and stays whatever the setting is.
///
/// The mark is drawn rather than shipped, as in the .ahk: the accent as a filled
/// rounded square with the Y knocked out of it in the panel's own near-black.
/// Contrast that heavy is the only thing that survives being scaled to sixteen
/// pixels. It is rebuilt when the accent changes, and keyed on it so a launch
/// that has not changed the accent does no drawing at all.
///
/// ClickCount 1 in the .ahk makes the default item fire on a SINGLE click, which
/// is what every other tray application does and therefore the first thing
/// anybody tries; Avalonia's Clicked event is already the single click.
/// </summary>
public static class Tray
{
    static TrayIcon? _icon;
    static uint _drawnFor;
    static bool _warned;
    static NativeMenuItem? _openItem, _hideItem;

    public static void Init()
    {
        if (_icon is not null || Application.Current is null) return;
        try
        {
            _openItem = new NativeMenuItem("OPEN");
            _openItem.Click += (_, _) => Show();
            _hideItem = new NativeMenuItem("HIDE TO TRAY");
            _hideItem.Click += (_, _) => Hide();
            var exit = new NativeMenuItem("EXIT");
            exit.Click += (_, _) => Hub.HubSurface.Live?.HubClose();

            var menu = new NativeMenu();
            menu.Add(_openItem);
            menu.Add(_hideItem);
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(exit);

            _icon = new TrayIcon { ToolTipText = AppInfo.AppName, Menu = menu, IsVisible = true };
            _icon.Clicked += (_, _) => Show();
            TrayIcon.SetIcons(Application.Current, new TrayIcons { _icon });
            Apply(true);
        }
        catch { _icon = null; }                    // no tray on this desktop: TRUE MINIMISE stays off, see Available
    }

    /// <summary>Whether hiding the hub is safe - with no tray there is no way back, so the setting must not hide.</summary>
    public static bool Available => _icon is not null;

    /// <summary>The mark IS the accent, so a change redraws it. Keyed, so an unchanged accent costs nothing.</summary>
    public static void Apply(bool force = false)
    {
        if (_icon is null) return;
        if (!force && _drawnFor == HubState.Accent) return;
        // The shipped mark for this accent. The drawn square below is the
        // fallback for an accent with no artwork - it is the .ahk's own mark and
        // scales to sixteen pixels, so nothing is left without a tray icon.
        try
        {
            var ic = LayeredWindow.AppIcon(LayeredWindow.IconIndex());
            _icon.Icon = ic ?? new WindowIcon(Mark(HubState.Accent));
            _drawnFor = HubState.Accent;
        }
        catch { }
        _ = _warned;
    }

    public static void Tip(string s) { if (_icon is not null) try { _icon.ToolTipText = s; } catch { } }

    static void Show()
    {
        var h = Hub.HubSurface.Live;
        if (h is null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { h.HubShow(); Tip(AppInfo.AppName); });
    }
    static void Hide()
    {
        var h = Hub.HubSurface.Live;
        if (h is null) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { h.HubHide(); Tip(AppInfo.AppName + " - hidden (click to open)"); });
    }

    /// <summary>
    /// The 64 px mark, downscaled by the platform: a filled rounded square in the
    /// accent with a heavy Y cut out of it in 0x0B0C14. The Y is three strokes
    /// rather than glyph text - a font-rendered letter turns to mush at sixteen
    /// pixels, which is the size that actually matters here.
    /// </summary>
    static Bitmap Mark(uint accent)
    {
        const int S = 64;
        var rtb = new RenderTargetBitmap(new PixelSize(S, S), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            var acc = new SolidColorBrush(Col.ToColor(accent | 0xFF000000));
            ctx.DrawRectangle(acc, null, new RoundedRect(new Rect(2, 2, S - 4, S - 4), 14));
            var ink = new Avalonia.Media.Pen(new SolidColorBrush(Col.ToColor(0xFF0B0C14)), 9,
                                             lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            ctx.DrawLine(ink, new Point(19, 19), new Point(32, 34));
            ctx.DrawLine(ink, new Point(45, 19), new Point(32, 34));
            ctx.DrawLine(ink, new Point(32, 34), new Point(32, 46));
        }
        using var ms = new MemoryStream();
        rtb.Save(ms);
        rtb.Dispose();
        ms.Position = 0;
        return new Bitmap(ms);
    }
}
