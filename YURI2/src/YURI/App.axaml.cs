using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell;

namespace Yuri;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The hub decides when the process ends (the close button, the tray),
            // not the last window: the loading splash closes long before the hub opens.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // ---- the macOS application menu ----
            // Every window here is frameless, so without this the app has no menu
            // bar at all and Cmd-Q does nothing - the only way out would be the
            // hub's own close button. The items are the ones macOS expects to
            // find; the hub still decides when the process actually ends.
            if (OperatingSystem.IsMacOS())
            {
                var about = new NativeMenuItem("About " + Core.AppInfo.AppName);
                about.Click += (_, _) => { var h = Shell.Hub.HubSurface.Live; if (h is not null) { h.Tab = 5; h.Tim(Core.Pace.TICK_A); } };
                var hide = new NativeMenuItem("Hide " + Core.AppInfo.AppName) { Gesture = new KeyGesture(Key.H, KeyModifiers.Meta) };
                hide.Click += (_, _) => Shell.Hub.HubSurface.Live?.HubHide();
                var quit = new NativeMenuItem("Quit " + Core.AppInfo.AppName) { Gesture = new KeyGesture(Key.Q, KeyModifiers.Meta) };
                quit.Click += (_, _) => { var h = Shell.Hub.HubSurface.Live; if (h is not null) h.HubClose(); else desktop.Shutdown(); };
                var app = new NativeMenuItem(Core.AppInfo.AppName)
                {
                    Menu = new NativeMenu { about, new NativeMenuItemSeparator(), hide, new NativeMenuItemSeparator(), quit }
                };
                NativeMenu.SetMenu(this, new NativeMenu { app });
            }
            Paths.InitDirs();                       // YuriInitDirs()
            HubState.Load();                        // [hub] + [profile] from zeal.ini
            Fonts.Init(HubState.ProfileFont);       // fam / fStatus / fKey / fBadge / fHint / fBrand
            Boot.Run(desktop);                      // loading -> (gate) -> hub
        }
        base.OnFrameworkInitializationCompleted();
    }
}
