using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Special;

/// <summary>
/// The three Windows-only SPECIAL rows that need no scheduler surgery:
/// MULTI-ROBLOX INSTANCES holds the two named objects the client uses to
/// find an earlier copy of itself, so every launch starts its own client;
/// DISABLE CRASH HANDLER closes RobloxCrashHandler.exe as the FAST FLAG
/// MANAGER's poll sees it; the MEMORY TRIMMER empties each client's working
/// set on a schedule, only above a size you pick. SPFMultiHold / Release /
/// MultiStale, SPFCrashSweep, SPFMemManage / MemTick / MemSubToggle.
/// </summary>
public static class SpfWin
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    static readonly string[] SINGLETON = { "ROBLOX_singletonMutex", "ROBLOX_singletonEvent" };
    public static readonly int[] MemInts = { 5, 10, 30, 60, 300 };
    public static readonly int[] MemThrs = { 0, 1024, 2048, 3072, 4096, 6144, 8192 };
    static readonly List<IntPtr> MultiH = new();
    public static DateTime? MultiAt;                                     // SPF.multiAt: when the switch went on
    static int _staleN; static long _staleAt;
    public static long CrashSaidAt;
    public static int MemLive, MemN, MemRefused; public static double MemWs = -1.0, MemFreed, MemLast; public static long MemAt;
    static DispatcherTimer? _mem;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateMutexW(IntPtr attr, bool owned, string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateEventW(IntPtr attr, bool manual, bool initial, string name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("psapi.dll", SetLastError = true)] static extern bool EmptyWorkingSet(IntPtr h);
    [DllImport("psapi.dll", SetLastError = true)] static extern bool GetProcessMemoryInfo(IntPtr h, out PROCESS_MEMORY_COUNTERS c, uint cb);
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_MEMORY_COUNTERS { public uint cb, PageFaultCount; public nuint PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage; }

    public static void Register()
    {
        Spf.MultiStale = MultiStale;
        Spf.MultiBlock = () => SpfEngine.RbxCount() > 1 && !SpfEngine.AttrOk;
        Spf.MemHintShort = MemHintShort;
        Spf.MemHeld = MemHeld;
        Spf.CrashSweep = pids => { if (Spf.Crash) CrashSweep(pids, true); };
        Spf.EngineToggle = Toggle;
        if (Spf.Multi && Os.IsWin) { if (MultiHold() > 0) MultiAt = DateTime.UtcNow; else Spf.Multi = false; }
        MemManage();
    }

    // ---- MULTI-ROBLOX INSTANCES ----
    /// <summary>SPFMultiHold(): create the client's two named objects first, so no client can own them.</summary>
    public static int MultiHold()
    {
        if (!Os.IsWin) return 0;
        if (MultiH.Count > 0) return MultiH.Count;
        foreach (var nm in SINGLETON)
        {
            var h = nm.EndsWith("Event") ? CreateEventW(IntPtr.Zero, false, false, nm) : CreateMutexW(IntPtr.Zero, false, nm);
            if (h != IntPtr.Zero && h != new IntPtr(-1)) MultiH.Add(h);
        }
        return MultiH.Count;
    }
    public static void MultiRelease() { foreach (var h in MultiH) { try { CloseHandle(h); } catch { } } MultiH.Clear(); }
    /// <summary>SPFMultiStale(): clients started before the switch went on - not covered by it.</summary>
    static int MultiStale()
    {
        if (!Spf.Multi || MultiAt is null) return 0;
        if (_staleAt != 0 && Clock.Tick - _staleAt < 1500) return _staleN;
        _staleAt = Clock.Tick; _staleN = 0;
        try { foreach (var p in Roblox.Processes()) { try { if (p.StartTime.ToUniversalTime() < MultiAt) _staleN++; } catch { } } } catch { }
        return _staleN;
    }

    // ---- DISABLE CRASH HANDLER ----
    public static int CrashSweep(List<int> pids, bool say)
    {
        int n = 0;
        foreach (var pid in pids) { try { var p = Process.GetProcessById(pid); p.Kill(); n++; } catch { } }
        long now = Clock.Tick;
        if (n > 0)
        {
            Spf.CrashN += n;
            if (say && (CrashSaidAt == 0 || now - CrashSaidAt > 10000)) { CrashSaidAt = now; Spf.Say("ROBLOX CRASH HANDLER CLOSED" + (Spf.CrashN > n ? "  \u00B7  " + Spf.CrashN + " THIS SESSION" : ""), C_ON); }
            else Poke();
        }
        else if (say && pids.Count > 0 && (CrashSaidAt == 0 || now - CrashSaidAt > 10000)) { CrashSaidAt = now; Spf.Say("COULD NOT CLOSE THE ROBLOX CRASH HANDLER - TRY RUNNING AS ADMIN", C_ACC); }
        return n;
    }
    static List<int> CrashPids() { try { return Process.GetProcessesByName("RobloxCrashHandler").Select(p => p.Id).ToList(); } catch { return new(); } }

    // ---- MEMORY TRIMMER ----
    public static void MemManage()
    {
        _mem ??= new DispatcherTimer();
        _mem.Tick -= OnMem; _mem.Tick += OnMem;
        _mem.Stop();
        if (Spf.Mem && Os.IsWin) { _mem.Interval = TimeSpan.FromSeconds(Math.Max(1, Spf.MemInt)); _mem.Start(); Dispatcher.UIThread.Post(MemTick, DispatcherPriority.Background); }
    }
    static void OnMem(object? s, EventArgs e) { try { MemTick(); } catch { } }
    static double MemWsOf(IntPtr h)
    {
        var c = new PROCESS_MEMORY_COUNTERS { cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS>() };
        return GetProcessMemoryInfo(h, out c, c.cb) ? c.WorkingSetSize / 1048576.0 : -1.0;
    }
    /// <summary>SPFMemTick(): every client opened with only the two rights the call needs, trimmed when over the limit, the handle closed at once.</summary>
    public static void MemTick()
    {
        if (!Spf.Mem || !Os.IsWin) { _mem?.Stop(); return; }
        int[] live; try { live = Roblox.Processes().Select(p => p.Id).ToArray(); } catch { live = Array.Empty<int>(); }
        MemLive = live.Length;
        if (live.Length == 0) { if (MemWs != -1.0) { MemWs = -1.0; MemRefused = 0; Poke(); } return; }
        int trimmed = 0, refused = 0; double freed = 0, biggest = -1.0;
        foreach (var pid in live)
        {
            var h = OpenProcess(0x1000 | 0x0100, false, (uint)pid);                     // PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA
            if (h == IntPtr.Zero || h == new IntPtr(-1)) { refused++; continue; }
            try
            {
                double ws = MemWsOf(h);
                if (ws > biggest) biggest = ws;
                if (ws >= 0 && (Spf.MemThr <= 0 || ws >= Spf.MemThr))
                {
                    if (EmptyWorkingSet(h)) { trimmed++; double after = MemWsOf(h); if (after >= 0) freed += Math.Max(0.0, ws - after); }
                }
            }
            finally { CloseHandle(h); }
        }
        bool moved = Math.Abs(biggest - MemWs) > 1.0 || refused != MemRefused || trimmed > 0;
        MemWs = biggest; MemRefused = refused;
        if (trimmed > 0) { MemN += trimmed; MemFreed += freed; MemLast = freed; MemAt = Clock.Tick; }
        if (moved) Poke();
    }
    public static string MemFmt(double mb) => mb < 1024 ? Math.Round(mb) + " MB" : (Math.Round(mb / 1024.0, 1) % 1 == 0 ? ((int)Math.Round(mb / 1024.0)).ToString() : Math.Round(mb / 1024.0, 1).ToString("0.0")) + " GB";
    public static string MemIntLabel() { int s = Spf.MemInt; return "every " + (s >= 60 && s % 60 == 0 ? s / 60 + " min" : s + " s"); }
    public static string MemThrLabel() => Spf.MemThr > 0 ? "over " + MemFmt(Spf.MemThr) : "no limit";
    static string MemHintShort()
    {
        if (MemLive == 0) return "waiting";
        if (MemWs < 0) return MemRefused > 0 ? "no handle" : "reading...";
        return MemFmt(MemWs) + (MemN > 0 ? "  \u00B7  " + MemN + "\u00D7" : "");
    }
    static string MemHeld()
    {
        if (!Spf.Mem) return "  RIGHT NOW: off.";
        string s = "  RIGHT NOW: " + MemIntLabel() + ", " + MemThrLabel() + ". ";
        if (MemLive == 0) s += "No client running.";
        else if (MemWs < 0) s += MemLive + " client" + (MemLive == 1 ? "" : "s") + " running, size not readable" + (MemRefused > 0 ? " - the handle was refused; try running as administrator." : ".");
        else s += MemLive + " client" + (MemLive == 1 ? "" : "s") + " running, the largest at " + MemFmt(MemWs) + ".";
        if (MemN > 0) s += " Trimmed " + MemN + " time" + (MemN == 1 ? "" : "s") + " this run, " + MemFmt(MemFreed) + " freed in total, " + MemFmt(MemLast) + " the last time.";
        return s;
    }
    static int Next(int cur, int[] tbl) { foreach (var v in tbl) if (v > cur) return v; return tbl[0]; }
    /// <summary>SPFMemSubToggle(k): the interval chip (1) and the size chip (2).</summary>
    public static void MemSubToggle(int k)
    {
        if (k == 1) { Spf.MemInt = Next(Spf.MemInt, MemInts); MemManage(); Spf.Say("TRIM " + MemIntLabel().ToUpperInvariant(), C_ON); Spf.Save("memint", Spf.MemInt); }
        else { Spf.MemThr = Next(Spf.MemThr, MemThrs); Spf.Say(Spf.MemThr > 0 ? "TRIM ONLY A CLIENT OVER " + MemFmt(Spf.MemThr) : "TRIM EVERY CLIENT, WHATEVER ITS SIZE", Spf.MemThr > 0 ? C_ON : AMBER); Spf.Save("memthr", Spf.MemThr); }
        Spf.FlashAt[30 + k] = Clock.Tick;
        Poke();
    }

    /// <summary>The engine hook for rows 5, 7 and 8 (FPS BOOST, row 6, has its own file).</summary>
    static void Toggle(int row, bool on)
    {
        switch (row)
        {
            case 5:
                if (on)
                {
                    if (MultiHold() > 0) { MultiAt = DateTime.UtcNow; Spf.Say("MULTI-INSTANCE ON - LAUNCH EACH CLIENT WITH PLAY ON THE WEBSITE, NOT THE SHORTCUT", C_ON); }
                    else { Spf.Multi = false; Spf.Say("COULD NOT TAKE THE ROBLOX SINGLETON - TRY RUNNING AS ADMIN", C_ACC); }
                }
                else { MultiRelease(); MultiAt = null; Spf.Say("MULTI-INSTANCE OFF - CLIENTS ALREADY OPEN STAY OPEN", 0xFFC7CBE0); }
                break;
            case 7:
                if (on)
                {
                    var cp = CrashPids();
                    int n = cp.Count > 0 ? CrashSweep(cp, false) : 0;
                    Spf.Say(n > 0 ? "DISABLE CRASH HANDLER ON - " + n + " CLOSED NOW, AND AT EVERY LAUNCH FROM HERE" : "DISABLE CRASH HANDLER ON - CLOSES IT WITHIN SECONDS OF EACH LAUNCH", C_ON);
                }
                else Spf.Say("DISABLE CRASH HANDLER OFF - THE NEXT LAUNCH KEEPS IT", 0xFFC7CBE0);
                break;
            case 8:
                if (on) { MemN = 0; MemFreed = 0; MemLast = 0; MemAt = 0; }
                MemManage();
                if (on) Spf.Say("MEMORY TRIMMER ON - " + MemIntLabel().ToUpperInvariant() + ", " + MemThrLabel().ToUpperInvariant() + "  \u00B7  EXPECT A HITCH AT EACH TRIM", AMBER);
                else Spf.Say("MEMORY TRIMMER OFF - THE CLIENT KEEPS WHAT IT HAS", 0xFFC7CBE0);
                break;
            case 6:
                SpfFps.Toggle(on);
                break;
        }
    }
    static void Poke() => HubSurface.Live?.Tim(Pace.TICK_A);
    /// <summary>ExitApp: the singleton handles go with the process anyway; released here so the .ahk's order holds.</summary>
    public static void Exiting() { MultiRelease(); }
}
