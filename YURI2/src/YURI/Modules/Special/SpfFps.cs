using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Special;

/// <summary>
/// FPS BOOST (SPFFps*): gets Windows out of the client's way and changes
/// nothing else. Per client: the priority class is raised (High with eight
/// logical processors or more, Above Normal below — never lower than found),
/// power throttling is opted out for the client in front, the fine-timer
/// bit is granted (Windows 11), the GPU scheduling priority is raised a
/// notch, the performance cores are preferred only while the client
/// measures CPU-bound, and memory priority is held at normal. Everything
/// found is remembered and put back on the way off. The tick runs every
/// 6 s (1.5 s with several clients); the probe measures the busiest thread.
/// </summary>
public static class SpfFps
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    const uint PRI_HIGH = 0x80, PRI_ABOVE = 0x8000, PRI_NORM = 0x20;
    const int GPU_NORM = 2, GPU_ABOVE = 3, MEMPRI_NORMAL = 5, PIC_MEMPRI = 0, PIC_THROTTLE = 4, FAST = 1500, SLOW = 6000, PROBE_FULL = 10000;
    public static readonly string[] Levers = { "priority class", "power throttling", "timer resolution", "gpu priority", "performance cores", "memory priority" };
    static readonly Dictionary<int, uint> Pri = new(); static readonly Dictionary<int, int> GpuPri = new(), Mem = new(); static readonly Dictionary<int, (uint mask, uint state)> Thr = new(); static readonly Dictionary<int, int> Seen = new();
    public static int[] Took = new int[6]; public static string Why = "";
    public static int LiveN, Foc, N; public static string Bound = ""; public static double Busy = -1.0;
    public static string Power = "", Cap = "", PwrNote = ""; static long _powerAt;
    static bool _period; static DispatcherTimer? _tick;
    static Dictionary<uint, long> _probe = new(); static int _probePid; static long _probeAt, _probeScan; static uint _probeTid;
    static int[]? _pcores;

    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint GetPriorityClass(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetPriorityClass(IntPtr h, uint cls);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetProcessInformation(IntPtr h, int cls, IntPtr info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetProcessInformation(IntPtr h, int cls, IntPtr info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint GetActiveProcessorCount(ushort group);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetSystemCpuSetInformation(IntPtr info, uint len, out uint ret, IntPtr proc, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetProcessDefaultCpuSets(IntPtr h, uint[]? ids, uint count);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool Thread32First(IntPtr snap, ref THREADENTRY32 te);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool Thread32Next(IntPtr snap, ref THREADENTRY32 te);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenThread(uint access, bool inherit, uint tid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetThreadTimes(IntPtr h, out long ct, out long xt, out long kt, out long ut);
    [DllImport("gdi32.dll")] static extern int D3DKMTGetProcessSchedulingPriorityClass(IntPtr h, out int cls);
    [DllImport("gdi32.dll")] static extern int D3DKMTSetProcessSchedulingPriorityClass(IntPtr h, int cls);
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [StructLayout(LayoutKind.Sequential)] struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }
    [StructLayout(LayoutKind.Sequential)] struct THREADENTRY32 { public uint dwSize, cntUsage, th32ThreadID, th32OwnerProcessID; public int tpBasePri, tpDeltaPri; public uint dwFlags; }

    public static void Register()
    {
        try { SpfFpsChips.JnlRecover(); } catch { }
        Spf.FpsHintShort = HintShort;
        Spf.FpsHeld = Held;
        if (Spf.Fps && Os.IsWin) { TimerRes(true); Apply(true); Manage(); }
    }
    static int CpuCount() { try { uint n = GetActiveProcessorCount(0xFFFF); return n > 0 ? (int)n : 4; } catch { return 4; } }
    static int PriRank(uint c) => c switch { 0x40 => 1, 0x4000 => 2, 0x20 => 3, 0x8000 => 4, 0x80 => 5, 0x100 => 6, _ => 0 };
    public static string PriName(uint c) => c switch { 0x40 => "idle", 0x4000 => "below normal", 0x20 => "normal", 0x8000 => "above normal", 0x80 => "high", 0x100 => "realtime", _ => "unknown" };
    public static void SetWhy(string msg) { if (msg == "" || Why.Contains(msg)) return; Why = Why == "" ? msg : Why + "  \u00B7  " + msg; }
    public static void ClearWhy() => Why = "";

    /// <summary>SPFPCoreIds(): the CPU-set ids of the highest efficiency class; empty on a CPU whose cores are all alike.</summary>
    static int[] PCoreIds()
    {
        if (_pcores is not null) return _pcores;
        var ids = new List<int>();
        try
        {
            GetSystemCpuSetInformation(IntPtr.Zero, 0, out uint len, new IntPtr(-1), 0);
            if (len > 0)
            {
                var buf = Marshal.AllocHGlobal((int)len);
                try
                {
                    if (GetSystemCpuSetInformation(buf, len, out len, new IntPtr(-1), 0))
                    {
                        var all = new List<(int id, byte ec)>(); byte best = 0; int off = 0;
                        while (off + 32 <= len)
                        {
                            uint sz = (uint)Marshal.ReadInt32(buf, off);
                            if (sz < 32) break;
                            if (Marshal.ReadInt32(buf, off + 4) == 0) { byte ec = Marshal.ReadByte(buf, off + 18); all.Add((Marshal.ReadInt32(buf, off + 8), ec)); if (ec > best) best = ec; }
                            off += (int)sz;
                        }
                        if (all.Count > 0 && all.Count(c => c.ec == best) != all.Count) foreach (var c in all) if (c.ec == best) ids.Add(c.id);
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        catch { }
        return _pcores = ids.ToArray();
    }
    static (uint mask, uint state)? ThrGet(IntPtr h)
    {
        var st = Marshal.AllocHGlobal(12);
        try { Marshal.WriteInt32(st, 0, 1); Marshal.WriteInt32(st, 4, 0); Marshal.WriteInt32(st, 8, 0); return GetProcessInformation(h, PIC_THROTTLE, st, 12) ? ((uint)Marshal.ReadInt32(st, 4), (uint)Marshal.ReadInt32(st, 8)) : null; }
        catch { return null; }
        finally { Marshal.FreeHGlobal(st); }
    }
    static bool ThrSet(IntPtr h, uint mask, uint state)
    {
        var st = Marshal.AllocHGlobal(12);
        try { Marshal.WriteInt32(st, 0, 1); Marshal.WriteInt32(st, 4, (int)mask); Marshal.WriteInt32(st, 8, (int)state); return SetProcessInformation(h, PIC_THROTTLE, st, 12); }
        catch { return false; }
        finally { Marshal.FreeHGlobal(st); }
    }
    static int? MemGet(IntPtr h) { var p = Marshal.AllocHGlobal(4); try { return GetProcessInformation(h, PIC_MEMPRI, p, 4) ? Marshal.ReadInt32(p) : null; } catch { return null; } finally { Marshal.FreeHGlobal(p); } }
    static bool MemSet(IntPtr h, int v) { var p = Marshal.AllocHGlobal(4); try { Marshal.WriteInt32(p, v); return SetProcessInformation(h, PIC_MEMPRI, p, 4); } catch { return false; } finally { Marshal.FreeHGlobal(p); } }
    static int GpuPrioGet(IntPtr h) { try { return D3DKMTGetProcessSchedulingPriorityClass(h, out int v) == 0 ? v : -1; } catch { return -1; } }
    static int GpuPrioSet(IntPtr h, int cls) { try { return D3DKMTSetProcessSchedulingPriorityClass(h, cls) == 0 ? 1 : 0; } catch { return 0; } }
    static int CpuSet(IntPtr h, bool on)
    {
        var ids = PCoreIds();
        if (ids.Length == 0) return 0;
        try { return SetProcessDefaultCpuSets(h, on ? ids.Select(i => (uint)i).ToArray() : null, on ? (uint)ids.Length : 0) ? 1 : 0; } catch { return 0; }
    }

    /// <summary>SPFFpsTune(pid, on, top): the six levers on one client; top is the client in front.</summary>
    static bool Tune(int pid, bool on, bool top)
    {
        var h = OpenProcess(0x0200 | 0x1000 | 0x2000, false, (uint)pid);              // SET_INFORMATION | QUERY_LIMITED | SET_QUOTA
        if (h == IntPtr.Zero || h == new IntPtr(-1)) { SetWhy("no handle - OpenProcess refused"); return false; }
        try
        {
            uint was = GetPriorityClass(h), want;
            if (on)
            {
                if (was != 0 && !Pri.ContainsKey(pid)) Pri[pid] = was;
                uint hiCls = CpuCount() >= 8 ? PRI_HIGH : PRI_ABOVE, loCls = CpuCount() >= 8 ? PRI_ABOVE : PRI_NORM;
                want = top ? hiCls : loCls;
                if (PriRank(was) > PriRank(want)) want = was;
            }
            else want = Pri.TryGetValue(pid, out var w) ? w : PRI_NORM;
            SetPriorityClass(h, want);
            bool ok = GetPriorityClass(h) == want;
            if (on) Took[0] += ok ? 1 : 0; else Pri.Remove(pid);
            bool thrOff = on && top;
            if (on && !Thr.ContainsKey(pid)) { var pv = ThrGet(h); if (pv is not null) Thr[pid] = pv.Value; }
            uint mask, state;
            if (thrOff) { mask = 1; state = 0; }
            else if (Thr.TryGetValue(pid, out var tv)) { mask = tv.mask; state = tv.state; }
            else { mask = 0; state = 0; }
            bool r2 = ThrSet(h, mask, state);
            if (thrOff) Took[1] += r2 ? 1 : 0;
            bool r3 = ThrSet(h, thrOff ? (1u | 4u) : mask, state);                     // + IGNORE_TIMER_RESOLUTION (Windows 11)
            if (thrOff) Took[2] += r3 ? 1 : 0;
            if (on)
            {
                int gw = top ? GPU_ABOVE : GPU_NORM, gp = GpuPrioGet(h);
                if (gp >= 0 && !GpuPri.ContainsKey(pid)) GpuPri[pid] = gp;
                if (gp > gw) gw = gp;
                Took[3] += GpuPrioSet(h, gw);
            }
            else { GpuPrioSet(h, GpuPri.TryGetValue(pid, out var g0) ? g0 : GPU_NORM); GpuPri.Remove(pid); }
            int cw = on && top && Bound == "cpu" ? 1 : 0;
            if (!Seen.TryGetValue(pid, out var sv) || sv != cw) { Took[4] += CpuSet(h, cw == 1); Seen[pid] = cw; }
            else if (sv == 1) Took[4] += 1;
            if (on && !Mem.ContainsKey(pid)) { var mv = MemGet(h); if (mv is not null) Mem[pid] = mv.Value; }
            bool r4 = MemSet(h, on ? MEMPRI_NORMAL : Mem.TryGetValue(pid, out var m0) ? m0 : MEMPRI_NORMAL);
            if (on) Took[5] += r4 ? 1 : 0; else { Thr.Remove(pid); Mem.Remove(pid); }
            return ok;
        }
        finally { CloseHandle(h); }
    }
    static int FocusPid(int[] live)
    {
        try { var hw = GetForegroundWindow(); if (hw != IntPtr.Zero && GetWindowThreadProcessId(hw, out uint pid) != 0 && live.Contains((int)pid)) return (int)pid; } catch { }
        return 0;
    }
    /// <summary>SPFFpsApply(on): every client, the probe, the power reading.</summary>
    public static int Apply(bool on)
    {
        if (!Os.IsWin) return 0;
        int n = 0;
        int[] live; try { live = Roblox.Processes().Select(p => p.Id).ToArray(); } catch { live = Array.Empty<int>(); }
        foreach (var k in Seen.Keys.Where(k => !live.Contains(k)).ToList()) Seen.Remove(k);
        foreach (var k in Pri.Keys.Where(k => !live.Contains(k)).ToList()) Pri.Remove(k);
        foreach (var k in GpuPri.Keys.Where(k => !live.Contains(k)).ToList()) GpuPri.Remove(k);
        foreach (var k in Thr.Keys.Where(k => !live.Contains(k)).ToList()) Thr.Remove(k);
        foreach (var k in Mem.Keys.Where(k => !live.Contains(k)).ToList()) Mem.Remove(k);
        Took = new int[6]; Why = "";
        int foc = on && live.Length > 1 ? FocusPid(live) : 0;
        foreach (var pid in live) if (Tune(pid, on, foc == 0 || pid == foc)) n++;
        LiveN = live.Length; Foc = foc; N = on ? n : 0;
        if (!on) Seen.Clear();
        if (on) Probe(foc != 0 ? foc : live.Length > 0 ? live[0] : 0); else Probe(0);
        try { SpfFpsChips.ApplyChips(on); } catch { }
        if (_powerAt == 0 || Clock.Tick - _powerAt > 20000)
        {
            _powerAt = Clock.Tick;
            Power = on ? PowerRead() : "";
            Cap = "";
        }
        return n;
    }
    static string PowerRead()
    {
        try
        {
            if (!GetSystemPowerStatus(out var sp)) return "";
            if (sp.SystemStatusFlag == 1) return "battery saver is on - it holds clocks below anything here can lift";
            if (sp.ACLineStatus == 0) return "on battery - windows caps clocks whatever this switch does";
        }
        catch { }
        return "";
    }
    /// <summary>SPFFpsProbe(pid): the busiest thread's share of one core; CPU-bound at 85 %.</summary>
    static void Probe(int pid)
    {
        if (pid == 0) { Bound = ""; Busy = -1.0; _probe = new(); _probePid = 0; _probeAt = 0; _probeTid = 0; _probeScan = 0; return; }
        long now = Clock.Tick;
        if (_probePid != pid) { _probe = new(); _probePid = pid; _probeAt = 0; Busy = -1.0; Bound = ""; _probeTid = 0; _probeScan = 0; }
        bool full = _probeTid == 0 || _probeScan == 0 || now - _probeScan > PROBE_FULL;
        long best = 0; var cur = new Dictionary<uint, long>();
        if (!full)
        {
            var th = OpenThread(0x0800, false, _probeTid);
            if (th != IntPtr.Zero)
            {
                if (GetThreadTimes(th, out _, out _, out long kt, out long ut)) { long tot = kt + ut; cur[_probeTid] = tot; if (_probe.TryGetValue(_probeTid, out var prev)) best = tot - prev; }
                else full = true;
                CloseHandle(th);
            }
            else full = true;
        }
        if (full)
        {
            var snap = CreateToolhelp32Snapshot(0x04, 0);
            if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return;
            cur = new(); best = 0; uint bestTid = 0;
            try
            {
                var te = new THREADENTRY32 { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };
                if (Thread32First(snap, ref te))
                {
                    int guard = 0;
                    do
                    {
                        if (te.th32OwnerProcessID == pid)
                        {
                            var th = OpenThread(0x0800, false, te.th32ThreadID);
                            if (th != IntPtr.Zero)
                            {
                                if (GetThreadTimes(th, out _, out _, out long kt, out long ut))
                                {
                                    long tot = kt + ut; cur[te.th32ThreadID] = tot;
                                    if (_probe.TryGetValue(te.th32ThreadID, out var prev)) { long d = tot - prev; if (d > best) { best = d; bestTid = te.th32ThreadID; } }
                                }
                                CloseHandle(th);
                            }
                        }
                    } while (Thread32Next(snap, ref te) && ++guard < 1024);
                }
            }
            finally { CloseHandle(snap); }
            _probeScan = now;
            if (bestTid != 0) _probeTid = bestTid;
        }
        long el = now - _probeAt;
        if (_probeAt != 0 && el >= 400 && _probe.Count > 0) { Busy = Math.Clamp(best / (el * 10000.0), 0.0, 1.0); Bound = Busy >= 0.85 ? "cpu" : "other"; }
        _probe = cur; _probeAt = now;
    }
    public static void TimerRes(bool on)
    {
        if (!Os.IsWin) return;
        try { if (on && !_period) { timeBeginPeriod(1); _period = true; } else if (!on && _period) { timeEndPeriod(1); _period = false; } } catch { }
    }
    public static void Manage()
    {
        _tick ??= new DispatcherTimer();
        _tick.Tick -= OnTick; _tick.Tick += OnTick;
        _tick.Stop();
        if (Spf.Fps && Os.IsWin) { _tick.Interval = TimeSpan.FromMilliseconds(LiveN > 1 ? FAST : SLOW); _tick.Start(); }
    }
    static void OnTick(object? s, EventArgs e)
    {
        if (!Spf.Fps) { _tick?.Stop(); return; }
        int before = N; string boundB = Bound;
        try { Apply(true); } catch { }
        if (N != before || Bound != boundB) HubSurface.Live?.Tim(Pace.TICK_A);
        _tick!.Interval = TimeSpan.FromMilliseconds(LiveN > 1 ? FAST : SLOW);
    }
    public static int TookN() => Took.Count(t => t != 0);
    public static List<string> Missing()
    {
        var res = new List<string>();
        for (int i = 0; i < Took.Length; i++)
        {
            if (Took[i] != 0) continue;
            string why = i == 2 ? " (windows 11 only)" : i == 4 ? (PCoreIds().Length == 0 ? " (cpu is not hybrid)" : Bound != "cpu" ? " (client is not cpu-bound - windows knows best here)" : " (refused)") : i == 3 ? " (driver refused)" : "";
            res.Add(Levers[i] + why);
        }
        return res;
    }
    public static string Report()
    {
        if (!Spf.Fps) return "";
        if (N == 0) return "armed  \u00B7  no client to boost yet";
        string o = N + " client" + (N == 1 ? "" : "s") + "  \u00B7  " + TookN() + " of " + Levers.Length + " levers";
        if (Busy >= 0) o += "  \u00B7  busiest thread " + Math.Round(Busy * 100) + "% of a core";
        if (Power != "") o += "  \u00B7  " + Power;
        return o;
    }
    static string HintShort()
    {
        if (!Os.IsWin) return "windows only";
        if (Power != "") return "power capped";
        if (Bound == "other") return "gpu-bound";
        if (N == 0) return "waiting";
        return TookN() + "/" + Levers.Length + (Busy >= 0 ? "  " + Math.Round(Busy * 100) + "%" : "");
    }
    /// <summary>SPFFpsHeld(): the explainer's CURRENTLY HELD paragraph.</summary>
    static string Held()
    {
        var L = new List<string>();
        foreach (var (pid, was) in Pri)
        {
            uint cur = 0;
            var h = OpenProcess(0x1000, false, (uint)pid);
            if (h != IntPtr.Zero) { cur = GetPriorityClass(h); CloseHandle(h); }
            L.Add("roblox client " + pid + ":  " + (cur != 0 ? PriName(cur) : "?") + "   ->  back to " + PriName(was));
        }
        if (L.Count == 0) return "\n\nHOLDING NOTHING RIGHT NOW. Nothing has been changed that would need putting back.";
        string o = "\n\nCURRENTLY HELD - " + L.Count + " thing" + (L.Count == 1 ? "" : "s") + ", and what each goes back to:\n";
        foreach (var ln in L) o += "\n   " + ln;
        return o;
    }
    /// <summary>The row's toggle (SPFToggle, i = 6).</summary>
    public static void Toggle(bool on)
    {
        TimerRes(on);
        int n = Apply(on);
        Manage();
        if (on)
        {
            string msg = n > 0 ? "FPS BOOST ON - " + Report().ToUpperInvariant() : "FPS BOOST ON - WAITING FOR ROBLOX TO START";
            if (n > 0 && TookN() < Levers.Length) { var ms = Missing(); if (ms.Count > 0) msg += "  \u00B7  NOT: " + string.Join(", ", ms); }
            if (Power != "") msg += "  \u00B7  " + Power.ToUpperInvariant();
            Spf.Say(msg, C_ON);
        }
        else Spf.Say(n > 0 ? "FPS BOOST OFF - " + n + " CLIENT" + (n == 1 ? "" : "S") + " PUT BACK TO WHAT WAS FOUND" : "FPS BOOST OFF", 0xFFC7CBE0);
    }
    public static void Exiting() { if (Spf.Fps && Os.IsWin) { try { Apply(false); } catch { } TimerRes(false); } SpfFpsChips.Exiting(); }
}
