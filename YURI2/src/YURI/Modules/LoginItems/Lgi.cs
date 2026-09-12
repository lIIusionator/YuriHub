using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.LoginItems;

/// <summary>One listed program: its path, display name, the executable that runs it, the two switches, its icon.</summary>
public sealed class LgiItem
{
    public string Path = "", Name = "", Exe = "";
    public bool Open = true, Close = true;
    public Bitmap? Bmp; public bool BmpTried;
}

/// <summary>
/// LOGIN ITEMS - module 7. Up to eight programs strung onto Roblox: OPENS
/// starts one when a client appears, CLOSES ends it once the last client
/// is gone. A change has to hold — 2.5 s up, 6 s gone — so a client
/// restarting mid-update never closes anything. The list lives in
/// YURI\login_items.txt in the .ahk's line format; the master switch in
/// zeal.ini [lgi] on. A gentle close first, a forced end four seconds later
/// for whatever refused. LGILoad / Save / AddItem / Remove / Toggle /
/// Master / Observe / OpenAll / CloseAll / ForceClose / IconBmp.
/// </summary>
public static class Lgi
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399;
    public const int MAX = 8, HOLD_OPEN = 2500, HOLD_CLOSE = 6000, TICK = 1500;
    public static readonly List<LgiItem> Items = new();
    public static bool On = true;
    public static string Msg = ""; public static uint MsgCol; public static long MsgAt;
    public static double Scr, ScrT;
    public static readonly Dictionary<string, double> T = new();       // "i|open" / "i|close" / "master" -> eased switch travel
    public static readonly Dictionary<int, long> FlashAt = new();
    public static long AnimAt, ActAt;
    public static int Rbx = -1, RbxSeen = -1; public static long RbxSince;
    static readonly Dictionary<string, long> Launched = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<string> Pending = new();
    static HashSet<string> Running = new(StringComparer.OrdinalIgnoreCase); static long ScanAt;
    static DispatcherTimer? _timer;
    public static string File => Path.Combine(Paths.Root, "login_items.txt");
    static readonly Regex Line = new("^\\{\"path\":\"((?:[^\"\\\\]|\\\\.)+)\",\"open\":\"([01])\",\"close\":\"([01])\"\\}$", RegexOptions.Compiled);
    /// <summary>Set by a host that must not start or end programs (the test harness).</summary>
    public static bool Dry;

    public static void Say(string msg, uint col = 0)
    {
        Msg = msg; MsgCol = col != 0 ? col : C_ON; MsgAt = Clock.Tick;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }

    // ---- LGILoad / LGISave ----
    public static void Load()
    {
        Items.Clear();
        On = Ini.ReadInt(Paths.IniFile, "lgi", "on", 1) != 0;
        if (!System.IO.File.Exists(File)) return;
        string body = "";
        try { body = System.IO.File.ReadAllText(File, Encoding.UTF8); } catch { }
        foreach (var raw in body.Split('\n'))
        {
            var m = Line.Match(raw.TrimEnd('\r'));
            if (!m.Success) continue;
            AddItem(FastFlags.Ffm.Unesc(m.Groups[1].Value), m.Groups[2].Value == "1", m.Groups[3].Value == "1", false);
            if (Items.Count >= MAX) break;
        }
    }
    public static void Save()
    {
        var sb = new StringBuilder();
        foreach (var it in Items) sb.Append("{\"path\":\"").Append(FastFlags.Ffm.Esc(it.Path)).Append("\",\"open\":\"").Append(it.Open ? 1 : 0).Append("\",\"close\":\"").Append(it.Close ? 1 : 0).Append("\"}\n");
        try { Directory.CreateDirectory(Paths.Root); System.IO.File.WriteAllText(File, sb.ToString(), new UTF8Encoding(false)); } catch { }
    }

    /// <summary>LGIAddItem(path, open, close, say): 1 added, 0 refused (listed already, full, empty).</summary>
    static string FileOf(string p) { int i = Math.Max(p.LastIndexOf('\\'), p.LastIndexOf('/')); return i >= 0 ? p[(i + 1)..] : p; }
    static string ExtOf(string p) { string f = FileOf(p); int i = f.LastIndexOf('.'); return i > 0 ? f[(i + 1)..].ToLowerInvariant() : ""; }
    static string StemOf(string p) { string f = FileOf(p); int i = f.LastIndexOf('.'); return i > 0 ? f[..i] : f; }
    public static int AddItem(string path, bool open = true, bool close = true, bool say = true)
    {
        path = path.Trim();
        if (path == "") return 0;
        string ext = ExtOf(path);
        string tgt = path;
        if (ext == "lnk" && Os.IsWin) { var t = ShortcutTarget(path); if (t != "") tgt = t; }
        string exe = FileOf(tgt);
        string text = ExtOf(tgt);
        string nameNoExt = StemOf(tgt);
        if (text != "" && text != "exe" && text != "app") exe = HandlerExe(tgt);
        if (text == "app") exe = StemOf(tgt);                                        // macOS: the bundle's name is the process name
        foreach (var it in Items)
            if (it.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) { if (say) Say("ALREADY LISTED - " + it.Name, AMBER); return 0; }
        if (Items.Count >= MAX) { if (say) Say("LIST IS FULL (" + MAX + ")", AMBER); return 0; }
        Items.Add(new LgiItem { Path = path, Name = ext == "lnk" ? StemOf(path) : nameNoExt, Exe = exe, Open = open, Close = close });
        if (say)
        {
            Save();
            FlashAt[Items.Count] = Clock.Tick;
            Say("ADDED " + nameNoExt.ToUpperInvariant() + "  -  OPENS AND CLOSES WITH ROBLOX", C_ON);
        }
        return 1;
    }
    /// <summary>LGIHandlerExe(path): the process a document opens under; scripts under cmd; links under a browser, which is not closed.</summary>
    static string HandlerExe(string path)
    {
        string fn = FileOf(path);
        string ext = ExtOf(path);
        if (ext == "") return fn;
        if (ext == "bat" || ext == "cmd") return "cmd.exe";
        if (ext == "url" || ext == "website") return "";
        if (Os.IsWin)
        {
            try
            {
                uint n = 0;
                AssocQueryStringW(0, 2, "." + ext, null, null, ref n);
                if (n >= 2 && n <= 2048)
                {
                    var sb = new StringBuilder((int)n);
                    if (AssocQueryStringW(0, 2, "." + ext, null, sb, ref n) == 0) { string hx = Path.GetFileName(sb.ToString()); if (hx != "") return hx; }
                }
            }
            catch { }
        }
        return fn;
    }
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] static extern uint AssocQueryStringW(uint flags, int str, string assoc, string? extra, StringBuilder? outBuf, ref uint outLen);

    static string ShortcutTarget(string lnk)
    {
        try
        {
            // the IShellLink route needs COM; the target sits in the link's own string table as a rule
            var bytes = System.IO.File.ReadAllBytes(lnk);
            var txt = Encoding.Unicode.GetString(bytes);
            var m = Regex.Match(txt, "[A-Za-z]:\\\\[^\\0\"<>|]+?\\.(?:exe|bat|cmd|url|lnk)", RegexOptions.IgnoreCase);
            if (m.Success) return m.Value;
            var a = Encoding.Default.GetString(bytes);
            m = Regex.Match(a, "[A-Za-z]:\\\\[^\\0\"<>|]+?\\.(?:exe|bat|cmd)", RegexOptions.IgnoreCase);
            return m.Success ? m.Value : "";
        }
        catch { return ""; }
    }

    public static void Remove(int i)
    {
        if (i < 0 || i >= Items.Count) return;
        var it = Items[i];
        Items.RemoveAt(i);
        T.Clear(); FlashAt.Clear();
        Save();
        ScrT = Math.Clamp(ScrT, 0.0, Math.Max(0.0, LgiPanel.Total() - LgiPanel.Vis()));
        Say("REMOVED " + it.Name.ToUpperInvariant(), 0xFFC7CBE0);
    }
    public static void Toggle(int i, string which)
    {
        if (i < 0 || i >= Items.Count) return;
        var it = Items[i];
        if (which == "open") it.Open = !it.Open; else it.Close = !it.Close;
        FlashAt[i + 1] = Clock.Tick; AnimAt = Clock.Tick;
        Save();
        Say(it.Name.ToUpperInvariant() + "  -  " + (which == "open" ? (it.Open ? "OPENS WITH ROBLOX" : "STAYS CLOSED") : (it.Close ? "CLOSES WITH ROBLOX" : "STAYS OPEN")), 0);
    }
    public static void Master()
    {
        On = !On; AnimAt = Clock.Tick;
        Ini.Write(Paths.IniFile, "lgi", "on", On ? 1 : 0);
        Say(On ? "ARMED - WATCHING FOR ROBLOX" : "OFF - NOTHING OPENS OR CLOSES", On ? C_ON : 0xFFC7CBE0);
    }

    // ---- the watcher ----
    public static void WatchStart()
    {
        _timer ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(TICK) };
        _timer.Tick -= OnTick; _timer.Tick += OnTick;
        _timer.Start();
    }
    static void OnTick(object? s, EventArgs e) { try { Observe(Roblox.IsRunning()); } catch { } }

    /// <summary>LGIScan(force): the running process names, at most every two seconds.</summary>
    public static void Scan(bool force = false)
    {
        long now = Clock.Tick;
        if (!force && ScanAt != 0 && now - ScanAt < 2000) return;
        ScanAt = now;
        var run = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try { foreach (var p in Process.GetProcesses()) { try { run.Add(p.ProcessName); if (Os.IsWin) run.Add(p.ProcessName + ".exe"); } catch { } } } catch { }
        Running = run;
    }
    public static bool IsRunning(LgiItem it) => it.Exe != "" && (Running.Contains(it.Exe) || Running.Contains(StemOf(it.Exe)));

    /// <summary>LGIObserve(up): the debounced edge — held 2.5 s up, 6 s gone — then everything opens or closes.</summary>
    public static void Observe(bool upB)
    {
        long now = Clock.Tick;
        int up = upB ? 1 : 0;
        if (up != RbxSeen) { RbxSeen = up; RbxSince = now; }
        if (Rbx == -1) { Rbx = up; return; }
        if (up == Rbx) return;
        long held = now - RbxSince;
        if (up == 1 && held < HOLD_OPEN) return;
        if (up == 0 && held < HOLD_CLOSE) return;
        Rbx = up;
        if (!On) return;
        Scan(true);
        if (up == 1) OpenAll("roblox opened"); else CloseAll("roblox closed");
    }
    public static void OpenAll(string why = "")
    {
        if (why == "") Scan(true);
        int n = 0, skip = 0;
        foreach (var it in Items)
        {
            if (!it.Open) continue;
            if (IsRunning(it)) { skip++; continue; }
            bool ok = false;
            try
            {
                if (!Dry) Process.Start(new ProcessStartInfo(it.Path) { UseShellExecute = true });
                ok = true;
            }
            catch { }
            if (ok) { n++; Launched[it.Exe] = Clock.Tick; }
        }
        ActAt = Clock.Tick;
        if (n != 0 || skip != 0) Say("OPENED " + n + " APP" + (n == 1 ? "" : "S") + (skip != 0 ? "  -  " + skip + " ALREADY RUNNING" : "") + (why != "" ? "  -  " + why.ToUpperInvariant() : ""), n != 0 ? C_ON : 0xFFC7CBE0);
        else if (why == "") Say("NOTHING SET TO OPEN", 0xFFC7CBE0);
    }
    public static void CloseAll(string why = "")
    {
        if (why == "") Scan(true);
        int n = 0;
        Pending.Clear();
        foreach (var it in Items)
        {
            if (!it.Close || !IsRunning(it)) continue;
            if (!Dry) CloseGently(it.Exe);
            Pending.Add(it.Exe);
            n++;
        }
        if (n != 0)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(4000) };
            t.Tick += (s, e) => { t.Stop(); ForceClose(); };
            t.Start();
        }
        ActAt = Clock.Tick;
        Say(n != 0 ? "CLOSING " + n + " APP" + (n == 1 ? "" : "S") + (why != "" ? "  -  " + why.ToUpperInvariant() : "") : (why == "" ? "NOTHING TO CLOSE" : "NOTHING OPEN TO CLOSE"), n != 0 ? AMBER : 0xFFC7CBE0);
    }
    static IEnumerable<Process> ProcsOf(string exe)
    {
        string nm = StemOf(exe);
        try { return Process.GetProcessesByName(nm); } catch { return Array.Empty<Process>(); }
    }
    static void CloseGently(string exe)
    {
        foreach (var p in ProcsOf(exe)) { try { p.CloseMainWindow(); } catch { } }
    }
    static void ForceClose()
    {
        int k = 0;
        foreach (var exe in Pending)
        {
            foreach (var p in ProcsOf(exe)) { try { if (!p.HasExited) { if (!Dry) p.Kill(); k++; } } catch { } }
        }
        Pending.Clear();
        if (k != 0) Say(k + " APP" + (k == 1 ? "" : "S") + " HAD TO BE ENDED", AMBER);
    }

    // ---- the icon: the shell's, on Windows ----
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint attr; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string display; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string type; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfoW(string path, uint attr, ref SHFILEINFO sfi, uint size, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    public static Bitmap? IconBmp(LgiItem it)
    {
        if (it.BmpTried) return it.Bmp;
        it.BmpTried = true;
        if (!Os.IsWin) return null;
        try { it.Bmp = WinIcon(it.Path); } catch { it.Bmp = null; }
        return it.Bmp;
    }
    [SupportedOSPlatform("windows")]
    public static Bitmap? WinIcon(string path)
    {
        var sfi = new SHFILEINFO();
        if (SHGetFileInfoW(path, 0, ref sfi, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100) == IntPtr.Zero || sfi.hIcon == IntPtr.Zero) return null;
        try
        {
            using var ico = System.Drawing.Icon.FromHandle(sfi.hIcon);
            using var gb = ico.ToBitmap();
            using var ms = new MemoryStream();
            gb.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return new Bitmap(ms);
        }
        finally { DestroyIcon(sfi.hIcon); }
    }
}
