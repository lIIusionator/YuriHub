using System.Diagnostics;

namespace Yuri.Platform;

/// <summary>The Roblox client: is it running, and how to start it (HubOpenRoblox).</summary>
public static class Roblox
{
    static bool _live; static long _at;
    /// <summary>The name the client was last found under, for the panel and for anything that needs its path.</summary>
    public static string FoundAs = "";
    public static string FoundPath = "";

    // ---- the names the client runs under ----
    // GetProcessesByName(one name) was the whole test, and on macOS it never
    // matched: the client there is not RobloxPlayerBeta, the newer unified app
    // is not always RobloxPlayer either, and .NET's name comparison is not
    // case-insensitive off Windows. Enumerating once and matching a set - by
    // case-insensitive name, then by the executable's own path - answers for
    // every shape of install rather than for one of them.
    static readonly string[] MacNames = { "RobloxPlayer", "Roblox", "RobloxPlayerBeta", "RobloxPlayerInstaller" };
    static readonly string[] WinNames = { "RobloxPlayerBeta", "RobloxPlayer", "Windows10Universal" };

    /// <summary>FFM.rbx: the client is running; polled at most every 900 ms.</summary>
    public static bool IsRunning()
    {
        long now = Environment.TickCount64;
        if (now - _at < 1200) return _live;
        _at = now;
        // Once the name is known, ask for that one - a full sweep of every
        // process is what this used to cost, and it runs while the hub is idle.
        if (FoundAs != "")
        {
            try
            {
                var quick = Process.GetProcessesByName(FoundAs);
                if (quick.Length > 0) { foreach (var q in quick) q.Dispose(); _live = true; return true; }
            }
            catch { }
        }
        _live = false; FoundAs = ""; FoundPath = "";
        var names = Os.IsMac ? MacNames : WinNames;
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                string n;
                try { n = p.ProcessName; } catch { continue; }
                bool hit = false;
                foreach (var c in names) if (string.Equals(n, c, StringComparison.OrdinalIgnoreCase)) { hit = true; break; }
                // A macOS bundle can be renamed; the path is what it actually is.
                if (!hit && Os.IsMac)
                {
                    try { hit = p.MainModule?.FileName is { } f && f.Contains("/Roblox.app/", StringComparison.OrdinalIgnoreCase); } catch { }
                }
                if (!hit) { p.Dispose(); continue; }
                _live = true; FoundAs = n;
                try { FoundPath = p.MainModule?.FileName ?? ""; } catch { }
                p.Dispose();
                break;
            }
        }
        catch { _live = false; }
        return _live;
    }
    /// <summary>Forget the cached answer: the next IsRunning sweeps. Called after anything that changes what is running.</summary>
    public static void Refresh() { _at = 0; }

    /// <summary>
    /// Every running client, found the way IsRunning finds them. Ten call sites
    /// asked GetProcessesByName for ONE name, which is right on Windows and
    /// finds nothing on macOS - so a kill, a stale-client count or a priority
    /// change there acted on no process at all. Callers dispose what they take.
    /// </summary>
    public static List<Process> Processes()
    {
        var outp = new List<Process>();
        var names = Os.IsMac ? MacNames : WinNames;
        try
        {
            if (!Os.IsMac)
            {
                foreach (var n in names) { var ps = Process.GetProcessesByName(n); if (ps.Length > 0) { outp.AddRange(ps); return outp; } }
                return outp;
            }
            foreach (var p in Process.GetProcesses())
            {
                string n; try { n = p.ProcessName; } catch { p.Dispose(); continue; }
                bool hit = false;
                foreach (var c in names) if (string.Equals(n, c, StringComparison.OrdinalIgnoreCase)) { hit = true; break; }
                if (!hit) { try { hit = p.MainModule?.FileName is { } f && f.Contains("/Roblox.app/", StringComparison.OrdinalIgnoreCase); } catch { } }
                if (hit) outp.Add(p); else p.Dispose();
            }
        }
        catch { }
        return outp;
    }

    /// <summary>
    /// Close every client and say how many actually went. The cached IsRunning
    /// is cleared afterwards: it answers from a sweep up to 1.2 s old, and both
    /// places that judged a close read it straight after the kill - so a client
    /// that had gone was still "running", and a close that worked reported
    /// "could not close roblox - try running as admin".
    /// </summary>
    public static int Kill(int waitMs = 500)
    {
        int killed = 0;
        foreach (var p in Processes())
        {
            try { p.Kill(); if (p.WaitForExit(waitMs)) killed++; } catch { }
            finally { p.Dispose(); }
        }
        Refresh();
        return killed;
    }
    /// <summary>A fresh answer, not the cached one.</summary>
    public static bool IsRunningNow() { Refresh(); return IsRunning(); }

    /// <summary>The .app the running client came out of, or "" - the surest place to put ClientAppSettings.json.</summary>
    public static string MacBundle()
    {
        if (!Os.IsMac) return "";
        if (!IsRunning() || FoundPath == "") return "";
        int i = FoundPath.IndexOf("/Contents/MacOS/", StringComparison.OrdinalIgnoreCase);
        return i > 0 ? FoundPath[..i] : "";
    }

    /// <summary>HubOpenRoblox(): the protocol first, then the installed exe; false when neither works.</summary>
    public static bool Launch()
    {
        try
        {
            if (Os.IsMac)
                Process.Start(new ProcessStartInfo("open", "-a Roblox") { UseShellExecute = false });
            else
                Process.Start(new ProcessStartInfo("roblox-player:1+launchmode:app") { UseShellExecute = true });
            _at = 0;
            return true;
        }
        catch { }
        if (Os.IsWin)
        {
            foreach (var root in new[] { Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", "Roblox", "Versions"),
                                         Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "", "Roblox", "Versions") })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var exe = Path.Combine(dir, "RobloxPlayerBeta.exe");
                    if (!File.Exists(exe)) continue;
                    try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); _at = 0; return true; } catch { }
                    break;
                }
            }
        }
        return false;
    }

    /// <summary>Run(url): the platform's browser.</summary>
    public static void OpenUrl(string url)
    {
        try
        {
            if (Os.IsWin) Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (Os.IsMac) Process.Start("open", url);
            else Process.Start("xdg-open", url);
        }
        catch { }
    }
}
