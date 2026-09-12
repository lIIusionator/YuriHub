using Avalonia;

namespace Yuri;

internal static class Program
{
    // AutoHotkey ran the picker / fetch / update helpers as short-lived child
    // copies of itself ("if A_Args[1] = ..." at the top of YURI.ahk). Those are
    // Tasks now, so there is one process and #SingleInstance Off is not needed.
    [STAThread]
    public static int Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
