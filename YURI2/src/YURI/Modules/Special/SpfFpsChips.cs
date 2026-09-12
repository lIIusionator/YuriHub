using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Yuri.Core;
using Yuri.Modules.DeviceOpt;
using Yuri.Platform;

namespace Yuri.Modules.Special;

/// <summary>
/// The three opt-in chips on FPS BOOST — GPU PREF, QUIET, SYSTEM — and the
/// restore journal they share (YURI\config\fpsrestore.ini). Every machine
/// setting is recorded before it is written; a value that did not exist is
/// recorded as "~" so putting it back deletes it. The journal is stamped
/// with the writer's pid, so a second running copy of YURI leaves it alone,
/// and every launch restores whatever the last session left before applying
/// anything. SPFFpsGpuPref / QuietApply / QuietFocus / QuietRestore /
/// SysApply / SysRevert / JnlRecover.
/// </summary>
public static class SpfFpsChips
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399;
    const uint PRI_BELOW = 0x4000, PRI_NORM = 0x20;
    const string GPUPREF_KEY = @"Software\Microsoft\DirectX\UserGpuPreferences";
    static readonly string[] QuietNames = { "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "opera_gx.exe", "vivaldi.exe", "iexplore.exe", "steamwebhelper.exe", "EpicGamesLauncher.exe", "RiotClientServices.exe", "Battle.net.exe", "SearchIndexer.exe", "SearchProtocolHost.exe", "OneDrive.exe", "Dropbox.exe", "GoogleDriveFS.exe" };
    const string PWR_HIGH = "{8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c}", PWR_ULT = "{e9a42b02-d5df-448d-aa00-03f14749eb61}", PWR_SUBPROC = "{54533251-82be-4824-96c1-47b60b740d00}", PWR_MINSTATE = "{893dee8e-2bef-41e0-89c6-b55d0929964c}";
    sealed record RegT(string K, bool Hklm, string Key, string V, uint Want, int Dir, bool Adm, bool AbsOnly, string N);
    static readonly RegT[] FpsReg =
    {
        new("gamedvr", false, @"System\GameConfigStore", "GameDVR_Enabled", 0, -1, false, false, "game dvr"),
        new("appcap", false, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, -1, false, false, "background capture"),
        new("gamemode", false, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, 1, false, true, "game mode"),
        new("sysresp", true, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 10, -1, true, false, "mmcss reserve"),
        new("netthr", true, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 0xFFFFFFFF, 1, true, false, "network throttle"),
    };
    public static bool GpuOn; public static string GpuPath = "", GpuNote = "", PwrNote = ""; public static int SysN;
    static readonly Dictionary<int, uint> Quiet = new(); static readonly Dictionary<int, string> QuietNm = new(); static bool _quietDn;
    static string _clientExe = ""; static long _clientAt; static int _hybrid = -1;

    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint GetPriorityClass(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool SetPriorityClass(IntPtr h, uint cls);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(IntPtr h, uint flags, System.Text.StringBuilder buf, ref uint size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr h);
    [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr guid);
    [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid g);
    [DllImport("powrprof.dll")] static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint v);
    [DllImport("powrprof.dll")] static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint v);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [StructLayout(LayoutKind.Sequential)] struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public uint BatteryLifeTime, BatteryFullLifeTime; }

    // ---- the journal ----
    public static string JnlPath { get { try { Directory.CreateDirectory(Paths.Cfg); } catch { } return Path.Combine(Paths.Cfg, "fpsrestore.ini"); } }
    const int JnlVer = 1;
    static void JnlStamp() { Ini.Write(JnlPath, "meta", "ver", (long)JnlVer); Ini.Write(JnlPath, "meta", "pid", (long)Environment.ProcessId); Ini.Write(JnlPath, "meta", "at", DateTime.Now.ToString("yyyyMMddHHmmss")); }
    static bool JnlForeign()
    {
        long op = Ini.ReadInt(JnlPath, "meta", "pid", 0);
        if (op == 0 || op == Environment.ProcessId) return false;
        try
        {
            var p = Process.GetProcessById((int)op);
            return p.ProcessName.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    static bool JnlSet(string k, string v)
    {
        bool ok = Ini.Write(JnlPath, "prior", k, v) && Ini.Read(JnlPath, "prior", k, "\u0001") == v;
        if (!ok) SpfFps.SetWhy("could not write the restore journal - system tweaks skipped");
        else JnlStamp();
        return ok;
    }
    static string JnlGet(string k) => Ini.Read(JnlPath, "prior", k, "");
    static void JnlClear(string k) => Ini.Delete(JnlPath, "prior", k);
    static void JnlTidy()
    {
        var keys = Ini.ReadSection(JnlPath, "prior");
        if (keys.Count == 0) { try { File.Delete(JnlPath); } catch { } }
    }
    /// <summary>SPFFpsJnlRecover(): at launch, whatever a previous session left on the machine is put back first.</summary>
    public static int JnlRecover()
    {
        if (!Os.IsWin || !File.Exists(JnlPath)) return 0;
        if (JnlForeign()) return 0;
        int n = QuietRecover();
        n += GpuRevert() ? 1 : 0;
        n += SysRevert();
        return n;
    }

    // ---- GPU PREF ----
    [SupportedOSPlatform("windows")]
    public static bool GpuHybrid()
    {
        if (_hybrid >= 0) return _hybrid == 1;
        var seen = new HashSet<string>();
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (k is not null)
                foreach (var sub in k.GetSubKeyNames())
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(sub, "^\\d{4}$")) continue;
                    using var sk = k.OpenSubKey(sub);
                    string dsc = sk?.GetValue("DriverDesc") as string ?? "";
                    if (dsc == "" || dsc.Contains("Microsoft Basic") || dsc.Contains("Remote Display") || dsc.Contains("Microsoft Remote")) continue;
                    seen.Add(dsc);
                }
        }
        catch { }
        _hybrid = seen.Count >= 2 ? 1 : 0;
        return _hybrid == 1;
    }
    public static string ClientExe()
    {
        if (_clientExe != "" && Clock.Tick - _clientAt < 30000) return _clientExe;
        try
        {
            foreach (var p in Roblox.Processes())
            {
                var h = OpenProcess(0x1000, false, (uint)p.Id);
                if (h == IntPtr.Zero) continue;
                var sb = new System.Text.StringBuilder(1024); uint sz = 1024;
                bool ok = QueryFullProcessImageNameW(h, 0, sb, ref sz);
                CloseHandle(h);
                if (ok) { _clientExe = sb.ToString(); _clientAt = Clock.Tick; return _clientExe; }
            }
        }
        catch { }
        return _clientExe;
    }
    [SupportedOSPlatform("windows")]
    static string GpuPrefGet(string exe) { try { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(GPUPREF_KEY); return k?.GetValue(exe) as string ?? ""; } catch { return ""; } }
    [SupportedOSPlatform("windows")]
    static bool GpuRevert()
    {
        string pth = JnlGet("gpupath"), v = JnlGet("gpupref");
        GpuOn = false;
        if (v == "" || pth == "") return false;
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(GPUPREF_KEY, true);
            if (k is not null) { if (v == "~") k.DeleteValue(pth, false); else k.SetValue(pth, v, Microsoft.Win32.RegistryValueKind.String); }
        }
        catch { }
        JnlClear("gpupref"); JnlClear("gpupath");
        GpuPath = "";
        JnlTidy();
        return true;
    }
    /// <summary>SPFFpsGpuPref(on): 1 set, 2 already high performance (left alone), 0 nothing done.</summary>
    [SupportedOSPlatform("windows")]
    public static int GpuPref(bool on)
    {
        string exe = ClientExe();
        if (!on) return GpuRevert() ? 1 : 0;
        if (exe == "") { GpuNote = "no client seen yet, so no install path to key it on"; return 0; }
        string oldP = JnlGet("gpupath");
        if (oldP != "" && oldP != exe) GpuRevert();
        if (JnlGet("gpupref") != "") { GpuPath = exe; GpuOn = true; return 1; }
        string cur = GpuPrefGet(exe);
        if (cur != "")
        {
            if (cur == "GpuPreference=2;") { GpuNote = "already set to high performance - left alone"; GpuPath = exe; GpuOn = true; return 2; }
            GpuNote = "windows already has a preference set for roblox";
            return 0;
        }
        if (!JnlSet("gpupref", "~")) return 0;
        if (!JnlSet("gpupath", exe)) { JnlClear("gpupref"); return 0; }
        bool ok = false;
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(GPUPREF_KEY, true);
            k?.SetValue(exe, "GpuPreference=2;", Microsoft.Win32.RegistryValueKind.String);
            ok = GpuPrefGet(exe) == "GpuPreference=2;";
        }
        catch { }
        if (!ok) { JnlClear("gpupref"); JnlClear("gpupath"); JnlTidy(); return 0; }
        GpuPath = exe; GpuOn = true;
        GpuNote = "set to high performance - restart roblox for it to take";
        return 1;
    }

    // ---- QUIET ----
    static bool ClientInFront()
    {
        try
        {
            var hw = GetForegroundWindow();
            if (hw == IntPtr.Zero || GetWindowThreadProcessId(hw, out uint pid) == 0) return false;
            return Process.GetProcessById((int)pid).ProcessName.Equals(Os.RobloxProcess, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    static bool QuietOwns(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            string nm = QuietNm.TryGetValue(pid, out var n) ? n : "";
            string cur = p.ProcessName + ".exe";
            return nm == "" || cur == "" || cur.Equals(nm, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    /// <summary>SPFFpsQuietFocus(): the list is demoted only while the client is the window in front.</summary>
    public static int QuietFocus()
    {
        bool want = Spf.Fps && Spf.FpsQuiet && ClientInFront();
        if (Quiet.Count == 0) { _quietDn = false; return 0; }
        if (want == _quietDn) return 0;
        int n = 0;
        foreach (var (pid, was) in Quiet)
        {
            if (!QuietOwns(pid)) continue;
            var h = OpenProcess(0x0200, false, (uint)pid);
            if (h != IntPtr.Zero) { try { SetPriorityClass(h, want ? PRI_BELOW : was); } catch { } CloseHandle(h); n++; }
        }
        _quietDn = want;
        return n;
    }
    static void QuietJnl()
    {
        if (Quiet.Count == 0) { JnlClear("quiet"); JnlTidy(); return; }
        JnlSet("quiet", string.Join(";", Quiet.Select(kv => kv.Key + "|" + (QuietNm.TryGetValue(kv.Key, out var n) ? n : "") + "|" + kv.Value)));
    }
    static int QuietRecover()
    {
        string v = JnlGet("quiet");
        if (v == "") return 0;
        int n = 0;
        foreach (var ent in v.Split(';'))
        {
            var p = ent.Split('|');
            if (p.Length < 3 || !int.TryParse(p[0], out var pid) || !uint.TryParse(p[2], out var was)) continue;
            try
            {
                var pr = Process.GetProcessById(pid);
                if (p[1] != "" && !(pr.ProcessName + ".exe").Equals(p[1], StringComparison.OrdinalIgnoreCase)) continue;
            }
            catch { continue; }
            var h = OpenProcess(0x0200 | 0x1000, false, (uint)pid);
            if (h == IntPtr.Zero) continue;
            if (GetPriorityClass(h) == PRI_BELOW) { try { SetPriorityClass(h, was); } catch { } n++; }
            CloseHandle(h);
        }
        JnlClear("quiet"); JnlTidy();
        return n;
    }
    /// <summary>SPFFpsQuietApply(on): everything on the list found at normal, remembered and demoted.</summary>
    public static int QuietApply(bool on)
    {
        if (!on) { QuietRestore(); return 0; }
        var want = new HashSet<string>(QuietNames, StringComparer.OrdinalIgnoreCase);
        foreach (var pid in Quiet.Keys.ToList()) { try { Process.GetProcessById(pid); } catch { Quiet.Remove(pid); QuietNm.Remove(pid); } }
        bool added = false;
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                string nm; try { nm = p.ProcessName + ".exe"; } catch { continue; }
                if (!want.Contains(nm) || Quiet.ContainsKey(p.Id)) continue;
                var h = OpenProcess(0x0200 | 0x1000, false, (uint)p.Id);
                if (h == IntPtr.Zero) continue;
                uint was = GetPriorityClass(h);
                if (was == PRI_NORM) { Quiet[p.Id] = was; QuietNm[p.Id] = nm; added = true; }
                CloseHandle(h);
            }
        }
        catch { }
        if (added) QuietJnl();
        QuietFocus();
        return Quiet.Count;
    }
    public static int QuietRestore()
    {
        if (Quiet.Count == 0) { JnlClear("quiet"); return 0; }
        int n = 0;
        foreach (var (pid, was) in Quiet)
        {
            if (!QuietOwns(pid)) continue;
            var h = OpenProcess(0x0200, false, (uint)pid);
            if (h != IntPtr.Zero) { try { SetPriorityClass(h, was); } catch { } CloseHandle(h); n++; }
        }
        Quiet.Clear(); QuietNm.Clear(); _quietDn = false;
        JnlClear("quiet"); JnlTidy();
        return n;
    }
    public static List<string> QuietList() => QuietNm.Values.ToList();
    public static int QuietCount => Quiet.Count;

    // ---- SYSTEM ----
    static string PwrActive()
    {
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out var p) != 0 || p == IntPtr.Zero) return "";
            var g = Marshal.PtrToStructure<Guid>(p);
            LocalFree(p);
            return g.ToString("B");
        }
        catch { return ""; }
    }
    /// <summary>SPFPwrFree(scheme): a plan whose minimum processor state is 100 % is already unrestricted, whatever it is called.</summary>
    static bool PwrFree(string scheme)
    {
        if (scheme == "") return false;
        try
        {
            var g = Guid.Parse(scheme); var sg = Guid.Parse(PWR_SUBPROC); var st = Guid.Parse(PWR_MINSTATE);
            bool onAC = true;
            if (GetSystemPowerStatus(out var sp)) onAC = sp.ACLineStatus == 1;
            uint r = onAC ? PowerReadACValueIndex(IntPtr.Zero, ref g, ref sg, ref st, out var v) : PowerReadDCValueIndex(IntPtr.Zero, ref g, ref sg, ref st, out v);
            if (r != 0) return false;
            return v >= 100;
        }
        catch { return false; }
    }
    static bool PwrSet(string guidStr)
    {
        if (guidStr == "") return false;
        try { var g = Guid.Parse(guidStr); return PowerSetActiveScheme(IntPtr.Zero, ref g) == 0; } catch { return false; }
    }
    static bool IsAdmin()
    {
        try { using var id = System.Security.Principal.WindowsIdentity.GetCurrent(); return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator); } catch { return false; }
    }
    [SupportedOSPlatform("windows")]
    static (bool found, uint val) RegPeek(RegT t)
    {
        try
        {
            using var k = (t.Hklm ? Microsoft.Win32.Registry.LocalMachine : Microsoft.Win32.Registry.CurrentUser).OpenSubKey(t.Key);
            var o = k?.GetValue(t.V);
            if (o is null) return (false, 0);
            return (true, unchecked((uint)Convert.ToInt64(o)));
        }
        catch { return (false, 0); }
    }
    [SupportedOSPlatform("windows")]
    static bool RegPut(RegT t, uint v)
    {
        try
        {
            using var k = (t.Hklm ? Microsoft.Win32.Registry.LocalMachine : Microsoft.Win32.Registry.CurrentUser).CreateSubKey(t.Key, true);
            if (k is null) return false;
            k.SetValue(t.V, unchecked((int)v), Microsoft.Win32.RegistryValueKind.DWord);
            return true;
        }
        catch { return false; }
    }
    [SupportedOSPlatform("windows")]
    static void RegDel(RegT t) { try { using var k = (t.Hklm ? Microsoft.Win32.Registry.LocalMachine : Microsoft.Win32.Registry.CurrentUser).OpenSubKey(t.Key, true); k?.DeleteValue(t.V, false); } catch { } }

    /// <summary>SPFFpsSysApply(on): the plan and the four/six values, each journaled first, each skipped where already as wanted or owned by DEVICE OPTIMIZATIONS.</summary>
    [SupportedOSPlatform("windows")]
    public static int SysApply(bool on)
    {
        if (!on) return SysRevert();
        if (JnlForeign()) { SpfFps.SetWhy("another running copy of YURI owns the restore journal"); return 0; }
        int n = 0;
        if (JnlGet("power") != "") n++;
        else
        {
            string cur = PwrActive();
            if (cur == "") PwrNote = "could not read the active power plan";
            else if (PwrFree(cur)) { PwrNote = "power plan already unrestricted - left alone"; n++; }
            else if (JnlSet("power", cur))
            {
                if (PwrSet(PWR_ULT)) { PwrNote = "switched to ultimate performance"; n++; }
                else if (PwrSet(PWR_HIGH)) { PwrNote = "switched to high performance"; n++; }
                else { JnlClear("power"); PwrNote = "no performance plan available to switch to"; }
            }
        }
        bool adm = IsAdmin();
        foreach (var t in FpsReg)
        {
            if (t.Adm && !adm) continue;
            if (Dop.OwnsSpf(t.K)) { n++; continue; }
            if (JnlGet(t.K) != "") { n++; continue; }
            var (found, prior) = RegPeek(t);
            if (found && t.AbsOnly) { n++; continue; }
            if (found)
            {
                uint wv = t.Want;
                if ((t.Dir < 0 && prior <= wv) || (t.Dir > 0 && prior >= wv) || (t.Dir == 0 && prior == wv)) { n++; continue; }
            }
            if (!JnlSet(t.K, found ? prior.ToString() : "~")) continue;
            if (RegPut(t, t.Want)) n++; else JnlClear(t.K);
        }
        SysN = n;
        return n;
    }
    [SupportedOSPlatform("windows")]
    public static int SysRevert()
    {
        int n = 0;
        string p = JnlGet("power");
        if (p != "") { if (!Dop.OwnsSpf("power") && PwrSet(p)) n++; JnlClear("power"); }
        foreach (var t in FpsReg)
        {
            string v = JnlGet(t.K);
            if (v == "") continue;
            if (Dop.OwnsSpf(t.K)) { JnlClear(t.K); continue; }
            if (v == "~") RegDel(t); else if (uint.TryParse(v, out var uv) && RegPut(t, uv)) { }
            n++;
            JnlClear(t.K);
        }
        var mine = new HashSet<string>(FpsReg.Select(t => t.K)) { "power" };
        foreach (var kv in Ini.ReadSection(JnlPath, "prior")) { string k = kv.Key; if (mine.Contains(k) || (k != "gpupref" && k != "gpupath" && k != "quiet")) JnlClear(k); }
        SysN = 0; PwrNote = "";
        JnlTidy();
        return n;
    }
    public static int SysMax() { int n = 1; bool adm = IsAdmin(); foreach (var t in FpsReg) n += t.Adm && !adm ? 0 : 1; return n; }

    /// <summary>SPFFpsSubToggle(i): 1 GPU PREF, 2 QUIET, 3 SYSTEM.</summary>
    public static void SubToggle(int i)
    {
        if (!Os.IsWin) { Spf.Say("FPS BOOST'S CHIPS ARE WINDOWS FEATURES", AMBER); return; }
        if (i == 3)
        {
            Spf.FpsSys = !Spf.FpsSys;
            if (Spf.FpsSys)
            {
                SpfFps.ClearWhy();
                int n = SysApply(true), mx = SysMax();
                if (n > 0)
                {
                    string msg = "SYSTEM TWEAKS ON - " + n + " OF " + mx + " APPLIED, ALL RECORDED FOR UNDO";
                    if (PwrNote != "") msg += "  \u00B7  " + PwrNote.ToUpperInvariant();
                    if (!IsAdmin()) msg += "  \u00B7  RUN AS ADMIN FOR THE TWO UNDER HKLM";
                    Spf.Say(msg, C_ON);
                }
                else { Spf.FpsSys = false; Spf.Say(SpfFps.Why != "" ? SpfFps.Why.ToUpperInvariant() : "NOTHING TO CHANGE - ALREADY SET THAT WAY", AMBER); }
            }
            else { int n = SysRevert(); Spf.Say(n > 0 ? "SYSTEM TWEAKS OFF - " + n + " SETTING(S) PUT BACK EXACTLY AS FOUND" : "SYSTEM TWEAKS OFF", 0xFFC7CBE0); }
            Spf.Save("fpssys", Spf.FpsSys);
        }
        else if (i == 1)
        {
            if (!Spf.FpsGpu && !GpuHybrid()) { Spf.Say("ONE GPU ON THIS MACHINE - NOTHING TO PREFER", AMBER); return; }
            Spf.FpsGpu = !Spf.FpsGpu;
            if (Spf.FpsGpu)
            {
                if (ClientExe() == "") { Spf.FpsGpu = false; Spf.Say("START ROBLOX ONCE SO YURI CAN SEE WHERE IT IS INSTALLED", AMBER); }
                else
                {
                    int r = GpuPref(true);
                    if (r == 1) Spf.Say("GPU PREFERENCE SET TO HIGH PERFORMANCE - RESTART ROBLOX FOR IT TO TAKE", C_ON);
                    else if (r == 2) Spf.Say("ALREADY SET TO HIGH PERFORMANCE - LEFT EXACTLY AS IT IS", C_ON);
                    else { Spf.FpsGpu = false; Spf.Say(GpuNote != "" ? GpuNote.ToUpperInvariant() : "WINDOWS ALREADY HAS A GPU PREFERENCE SET FOR ROBLOX - LEFT ALONE", AMBER); }
                }
            }
            else Spf.Say(GpuPref(false) != 0 ? "GPU PREFERENCE CLEARED - BACK TO WINDOWS' OWN CHOICE" : "GPU PREFERENCE OFF - NOTHING TO CLEAR, IT WAS NOT OURS", 0xFFC7CBE0);
            Spf.Save("fpsgpu", Spf.FpsGpu);
        }
        else
        {
            Spf.FpsQuiet = !Spf.FpsQuiet;
            if (Spf.FpsQuiet)
            {
                int n = QuietApply(true);
                if (n > 0)
                {
                    var lst = QuietList();
                    string nm = string.Join(", ", lst.Take(3)) + (lst.Count > 3 ? " +" + (lst.Count - 3) : "");
                    Spf.Say("QUIET MODE ON - " + n + " DEMOTED: " + nm.ToUpperInvariant(), C_ON);
                }
                else Spf.Say("QUIET MODE ON - NOTHING WORTH DEMOTING IS RUNNING", C_ON);
            }
            else { int n = QuietRestore(); Spf.Say(n > 0 ? "QUIET MODE OFF - " + n + " RESTORED" : "QUIET MODE OFF", 0xFFC7CBE0); }
            Spf.Save("fpsquiet", Spf.FpsQuiet);
        }
        Spf.FlashAt[20 + i] = Clock.Tick;
    }

    /// <summary>The part of SPFFpsApply that drives the chips, called from SpfFps.Apply after the per-client levers.</summary>
    [SupportedOSPlatform("windows")]
    public static void ApplyChips(bool on)
    {
        if (on && Spf.FpsGpu && GpuHybrid()) GpuPref(true);
        else if (GpuOn && (!on || !Spf.FpsGpu)) GpuPref(false);
        if (on && Spf.FpsQuiet) QuietApply(true);
        else if (Quiet.Count > 0) QuietRestore();
        else QuietFocus();
        if (on && Spf.FpsSys) SysApply(true);
        else if (SysN > 0) SysRevert();
    }
    public static void Exiting()
    {
        if (!Os.IsWin) return;
        try { QuietRestore(); } catch { }
        try { if (GpuOn) GpuPref(false); } catch { }
        try { if (SysN > 0) SysRevert(); } catch { }
    }
}
