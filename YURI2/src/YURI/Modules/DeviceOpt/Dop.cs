using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.DeviceOpt;

/// <summary>One setting inside a group: k is reg / bcd / tcp / nic / pwr.</summary>
public sealed record DopItem(string K, string P = "", string V = "", string T = "", string D = "", string E = "");
public sealed record DopGroup(string Id, string N, bool Rb, string H, DopItem[] It);

/// <summary>
/// DEVICE OPTIMIZATIONS - module 6, Windows only. Five groups of machine
/// settings — input latency, system timers, the network stack, power and
/// USB, GPU and scheduling. Switching one on captures every value it will
/// touch first (per adapter for the NIC items), then writes, then verifies;
/// switching it off restores the capture — the values that were there, or
/// removal for what did not exist. HKLM, bcdedit, netsh and powercfg work
/// runs in an elevated PowerShell child the user approves; HKCU values are
/// written by the hub itself. A read-only check every five minutes counts
/// what has drifted; RE-APPLY rewrites those groups. State lives in
/// YURI\devopt: a snapshot per applied group, the user snapshot, a verify
/// note, the last exit code. DOPGroups / Build / Run / Poll / UserApply /
/// UserRevert / UserDrift / CheckRun / Reapply / RevertAll / Status.
/// </summary>
public static class Dop
{
    public const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_BAD = 0xFFF04438, C_ACC = 0xFFFB7185;
    public const int SYSN = 5, RH = 42, RG = 52;
    const int JOBMS = 600000, DRIFTMS = 300000;
    public static string Dir => Path.Combine(Paths.Root, "devopt");
    public static string Msg = "NOT APPLIED"; public static uint MsgCol = 0xFFC7CBE0; public static long MsgAt;
    public static double Scr, ScrT;
    public static readonly Dictionary<int, long> FlashAt = new();
    public static readonly Dictionary<int, double> T = new();
    public static string Busy = "", Mode = "", QMode = "revert";
    public static readonly List<string> Queue = new();
    static readonly Dictionary<string, bool> OnCache = new(); static long OnAt;
    public static long FlashLast, BusyAt;
    static Process? _child;
    public static readonly Dictionary<string, int> Drift = new();
    static Process? _chk; static long _chkAt; static DispatcherTimer? _poll, _chkPoll, _driftTimer;

    // ---- DOPGroups ----
    const string MOU = "HKCU\\Control Panel\\Mouse", DSK = "HKCU\\Control Panel\\Desktop", SVC = "HKLM\\SYSTEM\\CurrentControlSet\\Services",
                 MMP = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile", CTL = "HKLM\\SYSTEM\\CurrentControlSet\\Control",
                 IFE = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options";
    static DopItem Reg(string p, string v, string t, string d) => new("reg", p, v, t, d);
    public static readonly DopGroup[] Groups =
    {
        new("input", "INPUT LATENCY", false, "1:1 pointer, no acceleration, shorter mouse and keyboard queues", new[]
        {
            Reg(SVC + "\\mouclass\\Parameters", "MouseDataQueueSize", "DWord", "40"),
            Reg(SVC + "\\kbdclass\\Parameters", "KeyboardDataQueueSize", "DWord", "40"),
            Reg(SVC + "\\mouhid\\Parameters", "UseOnlyMice", "DWord", "1"),
            Reg(MOU, "MouseSpeed", "String", "0"), Reg(MOU, "MouseThreshold1", "String", "0"), Reg(MOU, "MouseThreshold2", "String", "0"),
            Reg(MOU, "SmoothMouseXCurve", "Binary", "0,0,0,0,0,0,0,0,192,204,12,0,0,0,0,0,128,153,25,0,0,0,0,0,64,102,38,0,0,0,0,0,0,51,51,0,0,0,0,0"),
            Reg(MOU, "SmoothMouseYCurve", "Binary", "0,0,0,0,0,0,0,0,0,0,56,0,0,0,0,0,0,0,112,0,0,0,0,0,0,0,168,0,0,0,0,0,0,0,224,0,0,0,0,0"),
            Reg(DSK, "MenuShowDelay", "String", "0"), Reg(DSK, "ActiveWindowTracking", "DWord", "0"),
        }),
        new("timers", "SYSTEM TIMERS", true, "dynamic tick off, platform clock off - needs a restart", new[]
        {
            new DopItem("bcd", E: "disabledynamictick", D: "yes"), new DopItem("bcd", E: "useplatformclock", D: "no"), new DopItem("bcd", E: "tscsyncpolicy", D: "enhanced"),
        }),
        new("network", "NETWORK STACK", false, "throttling off, Nagle off, adapter offloads off - NIC part needs a reconnect", new[]
        {
            Reg(MMP, "NetworkThrottlingIndex", "DWord", "4294967295"),
            new DopItem("tcp", V: "TcpAckFrequency", D: "1"), new DopItem("tcp", V: "TCPNoDelay", D: "1"), new DopItem("tcp", V: "TcpDelAckTicks", D: "0"),
            new DopItem("nic", V: "Interrupt Moderation", D: "Disabled"), new DopItem("nic", V: "Flow Control", D: "Disabled"), new DopItem("nic", V: "Energy Efficient Ethernet", D: "Disabled"),
            new DopItem("nic", V: "Advanced EEE", D: "Disabled"), new DopItem("nic", V: "Green Ethernet", D: "Disabled"), new DopItem("nic", V: "Auto Disable Gigabit", D: "Disabled"),
            new DopItem("nic", V: "Gigabit Lite", D: "Disabled"), new DopItem("nic", V: "Jumbo Frame", D: "Disabled"), new DopItem("nic", V: "ARP Offload", D: "Disabled"),
            new DopItem("nic", V: "NS Offload", D: "Disabled"), new DopItem("nic", V: "Wake on Magic Packet", D: "Disabled"), new DopItem("nic", V: "Wake on pattern match", D: "Disabled"),
            new DopItem("nic", V: "Receive Side Scaling", D: "Enabled"), new DopItem("nic", V: "Receive Buffers", D: "512"), new DopItem("nic", V: "Transmit Buffers", D: "128"),
        }),
        new("power", "POWER & USB", false, "power throttling off, USB selective suspend off, high performance plan", new[]
        {
            Reg(CTL + "\\Power\\PowerThrottling", "PowerThrottlingOff", "DWord", "1"), Reg(SVC + "\\USB", "DisableSelectiveSuspend", "DWord", "1"),
            new DopItem("pwr", V: "usbsuspend", D: "0"), new DopItem("pwr", V: "scheme", D: "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
        }),
        new("gpu", "GPU & SCHEDULING", true, "hardware scheduling, game priority, foreground bias - needs a restart", new[]
        {
            Reg(CTL + "\\GraphicsDrivers", "HwSchMode", "DWord", "2"), Reg(CTL + "\\GraphicsDrivers", "DisablePreemption", "DWord", "1"),
            Reg(CTL + "\\PriorityControl", "Win32PrioritySeparation", "DWord", "38"), Reg(MMP, "SystemResponsiveness", "DWord", "0"),
            Reg(MMP + "\\Tasks\\Games", "GPU Priority", "DWord", "8"), Reg(MMP + "\\Tasks\\Games", "Priority", "DWord", "6"),
            Reg(MMP + "\\Tasks\\Games", "Scheduling Category", "String", "High"), Reg(MMP + "\\Tasks\\Games", "SFIO Priority", "String", "High"),
            Reg(IFE + "\\csrss.exe\\PerfOptions", "CpuPriorityClass", "DWord", "3"), Reg(IFE + "\\csrss.exe\\PerfOptions", "IoPriority", "DWord", "3"),
        }),
    };
    public static int Idx(string id) { for (int i = 0; i < Groups.Length; i++) if (Groups[i].Id == id) return i + 1; return 0; }
    static string PS(string p) => p.StartsWith("HKLM\\") ? "HKLM:\\" + p[5..] : p.StartsWith("HKCU\\") ? "HKCU:\\" + p[5..] : p.StartsWith("HKCR\\") ? "HKCR:\\" + p[5..] : p.StartsWith("HKU\\") ? "HKU:\\" + p[4..] : p.StartsWith("HKCC\\") ? "HKCC:\\" + p[5..] : p;
    static bool IsUser(DopItem it) => it.K == "reg" && it.P.StartsWith("HKCU\\");
    public static string SnapPath(string id) => Path.Combine(Dir, id + ".snapshot.json");
    static string UserPath(string id) => Path.Combine(Dir, id + ".user.snapshot");
    static string VerPath(string id) => Path.Combine(Dir, id + ".verify.txt");
    static string ChkPath() => Path.Combine(Dir, "check.txt");
    static string LogPath(string id) => Path.Combine(Dir, id + ".last.log");
    static string Q(string s) => "'" + s.Replace("'", "''") + "'";

    public static int OnCount() { int n = 0; foreach (var g in Groups) if (IsOn(g.Id)) n++; return n; }
    public static bool IsOn(string id)
    {
        if (Clock.Tick - OnAt > 1000 || OnCache.Count == 0)
        {
            OnAt = Clock.Tick;
            foreach (var g in Groups) OnCache[g.Id] = File.Exists(SnapPath(g.Id));
        }
        return OnCache.TryGetValue(id, out var v) && v;
    }
    public static void OnInvalidate() { OnAt = 0; OnCache.Clear(); }
    public static void Say(string msg, uint col = 0)
    {
        Msg = msg; MsgCol = col != 0 ? col : C_ON; MsgAt = Clock.Tick;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static int DriftOf(string id) => Drift.TryGetValue(id, out var d) ? d : 0;
    public static int DriftN() { int n = 0; foreach (var g in Groups) if (IsOn(g.Id) && DriftOf(g.Id) > 0) n++; return n; }
    public static bool OwnsSpf(string k) => k == "sysresp" ? IsOn("gpu") : k == "netthr" ? IsOn("network") : k == "power" && IsOn("power");

    // ---- the elevated script: DOPBuild(g, mode) ----
    static string Header()
    {
        var sb = new StringBuilder();
        void L(string s) => sb.Append(s).Append('\n');
        L("$ErrorActionPreference = 'SilentlyContinue'");
        L("function Cap-Reg($p, $v) {"); L("  $rec = @{ k='reg'; p=$p; v=$v; had=0; val=$null; type=$null }"); L("  if (-not (Test-Path $p)) { return $rec }"); L("  $it = Get-Item -LiteralPath $p");
        L("  if ($it.GetValueNames() -contains $v) {"); L("    $rec.had = 1"); L("    $rec.val = $it.GetValue($v, $null, 'DoNotExpandEnvironmentNames')"); L("    $rec.type = $it.GetValueKind($v).ToString()"); L("  }"); L("  return $rec"); L("}");
        L("function Put-Reg($p, $v, $d, $t) {"); L("  if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null }"); L("  if ($t -eq 'Binary') { $d = [byte[]]($d -split ',') }");
        L("  if ($t -eq 'DWord') { $d = [BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]$d), 0) }"); L("  New-ItemProperty -LiteralPath $p -Name $v -Value $d -PropertyType $t -Force | Out-Null"); L("}");
        L("function Coerce($v, $t) {"); L("  if ($t -eq 'Binary') { return ,[byte[]]@($v) }"); L("  if ($t -eq 'MultiString') { return ,[string[]]@($v) }"); L("  return $v"); L("}");
        L("function Undo-Reg($r) {"); L("  if ($r.had -eq 1) {"); L("    if (-not (Test-Path $r.p)) { New-Item -Path $r.p -Force | Out-Null }"); L("    $vv = Coerce $r.val $r.type");
        L("    New-ItemProperty -LiteralPath $r.p -Name $r.v -Value $vv -PropertyType $r.type -Force | Out-Null"); L("  } elseif (Test-Path $r.p) {"); L("    Remove-ItemProperty -LiteralPath $r.p -Name $r.v -Force"); L("  }"); L("}");
        L("function Cap-Bcd($e) {"); L("  $rec = @{ k='bcd'; e=$e; had=0; val=$null }"); L("  $out = (bcdedit /enum '{current}' | Out-String)"); L("  foreach ($line in $out.Split([char]10)) {");
        L("    $m = [regex]::Match($line.Trim(), '^' + [regex]::Escape($e) + '\\s+(\\S+)$')"); L("    if ($m.Success) { $rec.had = 1; $rec.val = $m.Groups[1].Value }"); L("  }"); L("  return $rec"); L("}");
        L("function Undo-Bcd($r) {"); L("  if ($r.had -eq 1) { bcdedit /set $r.e $r.val | Out-Null }"); L("  else { bcdedit /deletevalue $r.e | Out-Null }"); L("}");
        L("function Ups() { return (Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }) }");
        L("function Cap-Nic($disp) {"); L("  $recs = @()"); L("  foreach ($a in (Ups)) {"); L("    $pr = Get-NetAdapterAdvancedProperty -Name $a.Name -DisplayName $disp -ErrorAction SilentlyContinue");
        L("    if ($pr) { $recs += @{ k='nic'; a=$a.Name; v=$disp; had=1; val=$pr.DisplayValue } }"); L("  }"); L("  return $recs"); L("}");
        L("function Undo-Nic($r) {"); L("  Set-NetAdapterAdvancedProperty -Name $r.a -DisplayName $r.v -DisplayValue $r.val -NoRestart -ErrorAction SilentlyContinue"); L("}");
        L("function Ifaces() {"); L("  $b = 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces'"); L("  if (Test-Path $b) { return (Get-ChildItem -Path $b | ForEach-Object { $_.PSPath }) }"); L("  return @()"); L("}");
        L("$USBSUB = '2a737441-1930-440a-9177-e0e642756c44'"); L("$USBSET = '48e6b7a6-50f5-4786-a146-e18e473b3632'");
        L("function Cur-Scheme() {"); L("  $o = (powercfg /getactivescheme | Out-String)"); L("  $m = [regex]::Match($o, '[0-9a-fA-F-]{36}')"); L("  if ($m.Success) { return $m.Value }"); L("  return $null"); L("}");
        L("function Pwr-Idx($sch, $rail) {"); L("  $pat = '*' + $sch + '*\\' + $rail + '\\*' + $USBSET + '*'");
        L("  $o = Get-CimInstance -Namespace 'root\\cimv2\\power' -ClassName Win32_PowerSettingDataIndex -ErrorAction SilentlyContinue | Where-Object { $_.InstanceID -like $pat }");
        L("  if ($o) { return [int64](@($o)[0].SettingIndexValue) }"); L("  return $null"); L("}");
        L("function Cap-Usb() {"); L("  $s = Cur-Scheme"); L("  $ac = Pwr-Idx $s 'AC'"); L("  $dc = Pwr-Idx $s 'DC'"); L("  if ($ac -eq $null -or $dc -eq $null) {");
        L("    $o = (powercfg /query $s $USBSUB $USBSET | Out-String)"); L("    $m1 = [regex]::Match($o, 'AC Power Setting Index:\\s*0x([0-9a-fA-F]+)')"); L("    $m2 = [regex]::Match($o, 'DC Power Setting Index:\\s*0x([0-9a-fA-F]+)')");
        L("    if ($ac -eq $null -and $m1.Success) { $ac = [Convert]::ToInt64($m1.Groups[1].Value, 16) }"); L("    if ($dc -eq $null -and $m2.Success) { $dc = [Convert]::ToInt64($m2.Groups[1].Value, 16) }"); L("  }");
        L("  return @{ k='pwr'; v='usbsuspend'; scheme=$s; ac=$ac; dc=$dc }"); L("}");
        L("function Chk-Reg($p, $v, $d, $t) {"); L("  if (-not (Test-Path $p)) { return 0 }"); L("  $it = Get-Item -LiteralPath $p"); L("  if (-not ($it.GetValueNames() -contains $v)) { return 0 }");
        L("  $now = $it.GetValue($v, $null, 'DoNotExpandEnvironmentNames')"); L("  if ($t -eq 'Binary') {"); L("    $want = [byte[]]($d -split ',')"); L("    if (($now -join ',') -eq ($want -join ',')) { return 1 }"); L("    return 0"); L("  }");
        L("  if ($t -eq 'DWord') {"); L("    $want = [BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]$d), 0)"); L("    if ([int]$now -eq $want) { return 1 }"); L("    return 0"); L("  }");
        L("  if ([string]$now -eq [string]$d) { return 1 }"); L("  return 0"); L("}");
        L("function Chk-Nic($disp, $d) {"); L("  foreach ($a in (Ups)) {"); L("    $pr = Get-NetAdapterAdvancedProperty -Name $a.Name -DisplayName $disp -ErrorAction SilentlyContinue"); L("    if ($pr -and $pr.DisplayValue -ne $d) { return 0 }"); L("  }"); L("  return 1"); L("}");
        L("function Chk-Bcd($e, $d) {"); L("  $r = Cap-Bcd $e"); L("  if ($r.had -eq 1 -and $r.val -eq $d) { return 1 }"); L("  return 0"); L("}");
        L("function Chk-Usb() {"); L("  $r = Cap-Usb"); L("  if ($r.ac -eq $null -or $r.dc -eq $null) { return 0 }"); L("  if ($r.ac -eq 0 -and $r.dc -eq 0) { return 1 }"); L("  return 0"); L("}");
        L("function Chk-Scheme($d) {"); L("  if ((Cur-Scheme) -eq $d) { return 1 }"); L("  return 0"); L("}");
        L("function Undo-Pwr($r) {"); L("  if ($r.v -eq 'usbsuspend') {"); L("    if ($r.ac -ne $null) { powercfg /setacvalueindex $r.scheme $USBSUB $USBSET $r.ac | Out-Null }");
        L("    if ($r.dc -ne $null) { powercfg /setdcvalueindex $r.scheme $USBSUB $USBSET $r.dc | Out-Null }"); L("    powercfg /setactive $r.scheme | Out-Null"); L("  } elseif ($r.v -eq 'scheme') {"); L("    if ($r.val) { powercfg /setactive $r.val | Out-Null }"); L("  }"); L("}");
        L("");
        return sb.ToString();
    }
    static string Chk(DopItem it) => it.K switch
    {
        "reg" => "if ((Chk-Reg " + Q(PS(it.P)) + " " + Q(it.V) + " " + Q(it.D) + " " + Q(it.T) + ") -ne 1) { $fail += " + Q(it.V) + " }",
        "bcd" => "if ((Chk-Bcd " + Q(it.E) + " " + Q(it.D) + ") -ne 1) { $fail += " + Q(it.E) + " }",
        "nic" => "if ((Chk-Nic " + Q(it.V) + " " + Q(it.D) + ") -ne 1) { $fail += " + Q(it.V) + " }",
        "tcp" => "foreach ($i in (Ifaces)) { if ((Chk-Reg $i " + Q(it.V) + " " + Q(it.D) + " 'DWord') -ne 1) { $fail += " + Q(it.V) + "; break } }",
        "pwr" when it.V == "usbsuspend" => "if ((Chk-Usb) -ne 1) { $fail += 'usb selective suspend' }",
        "pwr" => "if ((Chk-Scheme " + Q(it.D) + ") -ne 1) { $fail += 'power plan' }",
        _ => "",
    };
    public static string Build(DopGroup g, string mode)
    {
        var sb = new StringBuilder(Header());
        void L(string s) => sb.Append(s).Append('\n');
        L("$snapPath = " + Q(SnapPath(g.Id))); L("$snap = @()"); L("");
        if (mode is "apply" or "reapply")
        {
            if (mode == "apply")
            {
                L("# ---------- CAPTURE ----------");
                foreach (var it in g.It)
                {
                    if (IsUser(it)) continue;
                    switch (it.K)
                    {
                        case "reg": L("$snap += Cap-Reg " + Q(PS(it.P)) + " " + Q(it.V)); break;
                        case "bcd": L("$snap += Cap-Bcd " + Q(it.E)); break;
                        case "nic": L("$snap += Cap-Nic " + Q(it.V)); break;
                        case "tcp": L("foreach ($i in (Ifaces)) { $snap += Cap-Reg $i " + Q(it.V) + " }"); break;
                        case "pwr" when it.V == "usbsuspend": L("$snap += Cap-Usb"); break;
                        case "pwr": L("$snap += @{ k='pwr'; v='scheme'; val=(Cur-Scheme) }"); break;
                    }
                }
                L(""); L("$snap | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $snapPath -Encoding UTF8"); L("if (-not (Test-Path $snapPath)) { exit 2 }");
            }
            else L("if (-not (Test-Path $snapPath)) { exit 3 }");
            L(""); L("# ---------- APPLY ----------");
            foreach (var it in g.It)
            {
                if (IsUser(it)) continue;
                switch (it.K)
                {
                    case "reg": L("Put-Reg " + Q(PS(it.P)) + " " + Q(it.V) + " " + Q(it.D) + " " + Q(it.T)); break;
                    case "bcd": L("bcdedit /set " + Q(it.E) + " " + Q(it.D) + " | Out-Null"); break;
                    case "nic": L("foreach ($a in (Ups)) { Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName " + Q(it.V) + " -DisplayValue " + Q(it.D) + " -NoRestart -ErrorAction SilentlyContinue }"); break;
                    case "tcp": L("foreach ($i in (Ifaces)) { New-ItemProperty -LiteralPath $i -Name " + Q(it.V) + " -Value " + it.D + " -PropertyType DWord -Force | Out-Null }"); break;
                    case "pwr" when it.V == "usbsuspend": L("$cs = Cur-Scheme"); L("powercfg /setacvalueindex $cs $USBSUB $USBSET 0 | Out-Null"); L("powercfg /setdcvalueindex $cs $USBSUB $USBSET 0 | Out-Null"); L("powercfg /setactive $cs | Out-Null"); break;
                    case "pwr": L("powercfg /setactive " + Q(it.D) + " | Out-Null"); break;
                }
            }
            L(""); L("# ---------- VERIFY ----------"); L("$fail = @()");
            foreach (var it in g.It) if (!IsUser(it)) L(Chk(it));
            L("($fail -join '; ') | Set-Content -LiteralPath " + Q(VerPath(g.Id)) + " -Encoding UTF8");
            L("if ($fail.Count -gt 0) { exit 4 }"); L("exit 0");
        }
        else
        {
            L("# ---------- REVERT ----------");
            L("if (-not (Test-Path $snapPath)) { exit 3 }");
            L("$recs = Get-Content -LiteralPath $snapPath -Raw | ConvertFrom-Json"); L("if ($recs -isnot [array]) { $recs = @($recs) }");
            L("for ($i = $recs.Count - 1; $i -ge 0; $i--) {"); L("  $r = $recs[$i]");
            L("  if ($r.k -eq 'reg') { Undo-Reg $r }"); L("  elseif ($r.k -eq 'bcd') { Undo-Bcd $r }"); L("  elseif ($r.k -eq 'nic') { Undo-Nic $r }"); L("  elseif ($r.k -eq 'pwr') { Undo-Pwr $r }"); L("}");
            L("Remove-Item -LiteralPath $snapPath -Force"); L("exit 0");
        }
        return sb.ToString();
    }
    /// <summary>DOPCheckBuild(ids): the read-only drift count per applied group, one "id TAB count" line each ("?" when it cannot be read).</summary>
    public static string CheckBuild(IEnumerable<string> ids)
    {
        var sb = new StringBuilder(Header());
        void L(string s) => sb.Append(s).Append('\n');
        L("$out = @()");
        foreach (var id in ids)
        {
            int i = Idx(id); if (i == 0) continue;
            var g = Groups[i - 1];
            if (g.It.Any(it => it.K == "bcd")) { L("$out += (" + Q(id) + " + \"`t?\")"); continue; }         // bcdedit needs elevation to read
            L("$fail = @()");
            foreach (var it in g.It) if (!IsUser(it)) L(Chk(it));
            L("$out += (" + Q(id) + " + \"`t\" + $fail.Count)");
        }
        L("$out | Set-Content -LiteralPath " + Q(ChkPath()) + " -Encoding UTF8");
        return sb.ToString();
    }

    // ---- DOPRun / DOPPoll ----
    public static bool Run(string id, string mode)
    {
        if (!Os.IsWin) { Say("DEVICE OPTIMIZATIONS IS A WINDOWS FEATURE", AMBER); return false; }
        if (Busy != "") { Say("ALREADY RUNNING - GIVE IT A MOMENT", AMBER); return false; }
        int i = Idx(id); if (i == 0) return false;
        var g = Groups[i - 1];
        try { Directory.CreateDirectory(Dir); } catch { }
        if (!Directory.Exists(Dir)) { Say("CANNOT CREATE " + Dir, C_BAD); return false; }
        if (mode == "revert" && !IsOn(id)) { Say("NOTHING TO REVERT", AMBER); return false; }
        if (mode == "reapply" && !IsOn(id)) { Say("NOTHING TO RE-APPLY", AMBER); return false; }
        string work = Path.Combine(Dir, id + "." + mode + ".ps1"), lau = Path.Combine(Dir, id + "." + mode + ".run.ps1"), log = LogPath(id);
        foreach (var f in new[] { VerPath(id), work, lau, log }) try { File.Delete(f); } catch { }
        try { File.WriteAllText(work, Build(g, mode), new UTF8Encoding(true)); } catch { }
        if (!File.Exists(work)) { Say("COULD NOT WRITE THE SCRIPT", C_BAD); return false; }
        string lp = "$c = 1\ntry {\n  $p = Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -Wait -PassThru -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File'," + Q(work) + "\n  $c = $p.ExitCode\n} catch { $c = 1 }\n$c | Out-File -LiteralPath " + Q(log) + " -Encoding ASCII\n";
        try { File.WriteAllText(lau, lp, new UTF8Encoding(true)); } catch { }
        if (!File.Exists(lau)) { Say("COULD NOT WRITE THE LAUNCHER", C_BAD); return false; }
        try { _child = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + lau + "\"") { UseShellExecute = false, CreateNoWindow = true }); }
        catch { Say("COULD NOT START THE HELPER", C_BAD); return false; }
        Busy = id; BusyAt = Clock.Tick; Mode = mode;
        FlashAt[i] = Clock.Tick; FlashLast = Clock.Tick;
        Say((mode == "revert" ? "REVERTING " : mode == "reapply" ? "RE-APPLYING " : "APPLYING ") + g.N + " - APPROVE THE PROMPT", AMBER);
        _poll ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _poll.Tick -= OnPoll; _poll.Tick += OnPoll; _poll.Start();
        return true;
    }
    static void OnPoll(object? s, EventArgs e) => Poll();
    static void Poll()
    {
        if (Busy == "") { _poll?.Stop(); return; }
        string id = Busy; int i = Idx(id); var g = i != 0 ? Groups[i - 1] : null;
        string code = "";
        if (File.Exists(LogPath(id))) { try { code = File.ReadAllText(LogPath(id)).Trim(' ', '\t', '\r', '\n', '\uFEFF'); } catch { } }
        if (code == "")
        {
            if (_child is { HasExited: true }) { Busy = ""; _child = null; Queue.Clear(); _poll?.Stop(); OnInvalidate(); Say("THE HELPER EXITED WITHOUT FINISHING", C_BAD); return; }
            if (Clock.Tick - BusyAt > JOBMS) { Busy = ""; _child = null; Queue.Clear(); _poll?.Stop(); OnInvalidate(); Say("TIMED OUT - CHECK " + Dir, C_BAD); return; }
            return;
        }
        string mode = Mode;
        Busy = ""; _child = null; Mode = "";
        _poll?.Stop();
        OnInvalidate();
        if (i != 0) { FlashAt[i] = Clock.Tick; FlashLast = Clock.Tick; }
        if (g is not null && (code == "0" || code == "4"))
        {
            if (mode == "revert") UserRevert(g); else UserApply(g, mode == "apply");
        }
        if (code == "0" && g is not null)
        {
            bool on = IsOn(id);
            Drift[id] = 0;
            Say(g.N + " " + (on ? (mode == "reapply" ? "RE-APPLIED" : "APPLIED") : "REVERTED") + (on && g.Rb ? " - RESTART TO TAKE EFFECT" : ""), on ? C_ON : 0xFF38BDF8);
        }
        else if (code == "4" && g is not null)
        {
            string det = ""; try { det = File.ReadAllText(VerPath(id)).Trim(' ', '\t', '\r', '\n', '\uFEFF'); } catch { }
            Queue.Clear();
            Say(g.N + " - PARTLY APPLIED" + (det != "" ? ": " + det : ""), AMBER);
            CheckRun();
        }
        else if (code == "1") { Queue.Clear(); Say("CANCELLED - NOTHING WAS CHANGED", AMBER); }
        else { Queue.Clear(); Say("FAILED (code " + code + ") - SEE " + Dir, C_BAD); }
        if (code == "0" && Queue.Count > 0)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            t.Tick += (s2, e2) => { t.Stop(); QueueNext(); };
            t.Start();
        }
        HubSurface.Live?.Tim(Pace.TICK_A);
    }

    // ---- the HKCU items, written by the hub ----
    static string UEsc(string s) => s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    static string UUne(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length) { char d = s[++i]; sb.Append(d == 'n' ? '\n' : d == 'r' ? '\r' : d == 't' ? '\t' : d); continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }
    static string HexOf(string csv) { var sb = new StringBuilder(); foreach (var b in csv.Split(',')) if (b.Trim() != "") sb.Append((int.Parse(b.Trim()) & 0xFF).ToString("X2")); return sb.ToString(); }
    sealed record URec(bool Had, string Ty, string P, string V, string Val);
    static List<URec> UserRead(string path)
    {
        var a = new List<URec>();
        if (!File.Exists(path)) return a;
        string body = ""; try { body = File.ReadAllText(path); } catch { }
        foreach (var raw in body.TrimStart('\uFEFF').Split('\n'))
        {
            var ln = raw.TrimEnd('\r'); if (ln.Trim() == "") continue;
            var f = ln.Split('\t', 5); if (f.Length < 5) continue;
            a.Add(new URec(f[0] == "1", f[1], UUne(f[2]), UUne(f[3]), UUne(f[4])));
        }
        return a;
    }
    [SupportedOSPlatform("windows")]
    static (string ty, string val, bool had) RegGet(string p, string v)
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(p[5..], false);
            if (k is null) return ("", "", false);
            var o = k.GetValue(v, null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (o is null) return ("", "", false);
            var kind = k.GetValueKind(v);
            string ty = kind switch { Microsoft.Win32.RegistryValueKind.DWord => "REG_DWORD", Microsoft.Win32.RegistryValueKind.QWord => "REG_QWORD", Microsoft.Win32.RegistryValueKind.Binary => "REG_BINARY", Microsoft.Win32.RegistryValueKind.ExpandString => "REG_EXPAND_SZ", Microsoft.Win32.RegistryValueKind.MultiString => "REG_MULTI_SZ", _ => "REG_SZ" };
            string val = o is byte[] bb ? Convert.ToHexString(bb) : o is string[] ss ? string.Join("\n", ss) : Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";
            return (ty, val, true);
        }
        catch { return ("", "", false); }
    }
    [SupportedOSPlatform("windows")]
    static void RegPut(string p, string v, string ty, string val)
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(p[5..], true) ?? throw new IOException(p);
        switch (ty)
        {
            case "REG_DWORD": k.SetValue(v, unchecked((int)uint.Parse(val)), Microsoft.Win32.RegistryValueKind.DWord); break;
            case "REG_QWORD": k.SetValue(v, unchecked((long)ulong.Parse(val)), Microsoft.Win32.RegistryValueKind.QWord); break;
            case "REG_BINARY": k.SetValue(v, Convert.FromHexString(val), Microsoft.Win32.RegistryValueKind.Binary); break;
            case "REG_EXPAND_SZ": k.SetValue(v, val, Microsoft.Win32.RegistryValueKind.ExpandString); break;
            case "REG_MULTI_SZ": k.SetValue(v, val.Split('\n'), Microsoft.Win32.RegistryValueKind.MultiString); break;
            default: k.SetValue(v, val, Microsoft.Win32.RegistryValueKind.String); break;
        }
    }
    [SupportedOSPlatform("windows")]
    static void RegDel(string p, string v) { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(p[5..], true); k?.DeleteValue(v, false); }
    static string RegTy(string t) => t switch { "DWord" => "REG_DWORD", "Binary" => "REG_BINARY", "QWord" => "REG_QWORD", "ExpandString" => "REG_EXPAND_SZ", "MultiString" => "REG_MULTI_SZ", _ => "REG_SZ" };

    [DllImport("user32.dll", SetLastError = true)] static extern bool SystemParametersInfoW(uint act, uint p1, IntPtr p2, uint fl);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SystemParametersInfoW(uint act, uint p1, int[] p2, uint fl);
    static int RegNum(string p, string v, int def) { if (!Os.IsWin) return def; var (_, val, had) = RegGet(p, v); return had && int.TryParse(val, out var n) ? n : def; }
    /// <summary>DOPUserLive: the pointer settings take effect without a re-login.</summary>
    static void UserLive(string id)
    {
        if (id != "input" || !Os.IsWin) return;
        try
        {
            SystemParametersInfoW(0x0004, 0, new[] { RegNum(MOU, "MouseThreshold1", 6), RegNum(MOU, "MouseThreshold2", 10), RegNum(MOU, "MouseSpeed", 1) }, 2);
            SystemParametersInfoW(0x006B, (uint)RegNum(DSK, "MenuShowDelay", 400), IntPtr.Zero, 2);
            SystemParametersInfoW(0x1003, 0, (IntPtr)RegNum(DSK, "ActiveWindowTracking", 0), 2);
        }
        catch { }
    }
    static int UserApply(DopGroup g, bool capture)
    {
        if (!Os.IsWin || !g.It.Any(IsUser)) return 0;
        if (capture)
        {
            var sb = new StringBuilder();
            foreach (var it in g.It) if (IsUser(it)) { var (ty, val, had) = RegGet(it.P, it.V); sb.Append(had ? 1 : 0).Append('\t').Append(ty).Append('\t').Append(UEsc(it.P)).Append('\t').Append(UEsc(it.V)).Append('\t').Append(UEsc(val)).Append('\n'); }
            try { File.WriteAllText(UserPath(g.Id), sb.ToString(), new UTF8Encoding(false)); } catch { }
            if (!File.Exists(UserPath(g.Id))) return 0;
        }
        int n = 0;
        foreach (var it in g.It)
        {
            if (!IsUser(it)) continue;
            string ty = RegTy(it.T), d = ty == "REG_BINARY" ? HexOf(it.D) : it.D;
            try { RegPut(it.P, it.V, ty, d); n++; } catch { }
        }
        UserLive(g.Id);
        return n;
    }
    static int UserRevert(DopGroup g)
    {
        if (!Os.IsWin) return 0;
        var recs = UserRead(UserPath(g.Id));
        int n = 0;
        for (int i = recs.Count - 1; i >= 0; i--)
        {
            var r = recs[i];
            try { if (r.Had && r.Ty != "") RegPut(r.P, r.V, r.Ty, r.Val); else RegDel(r.P, r.V); n++; } catch { }
        }
        try { File.Delete(UserPath(g.Id)); } catch { }
        UserLive(g.Id);
        return n;
    }
    static int UserDrift(DopGroup g)
    {
        if (!Os.IsWin) return 0;
        int bad = 0;
        foreach (var it in g.It)
        {
            if (!IsUser(it)) continue;
            var (ty, cur, had) = RegGet(it.P, it.V);
            if (!had || ty == "") { bad++; continue; }
            string want = ty == "REG_BINARY" ? HexOf(it.D) : it.D;
            if (ty == "REG_DWORD") { if (!long.TryParse(cur, out var a) || !long.TryParse(want, out var b) || a != b) bad++; }
            else if (!string.Equals(cur.Trim(), want.Trim(), StringComparison.OrdinalIgnoreCase)) bad++;
        }
        return bad;
    }

    // ---- the drift check ----
    public static bool CheckRun()
    {
        if (!Os.IsWin || Busy != "" || _chk is not null) return false;
        var ids = Groups.Where(g => IsOn(g.Id)).Select(g => g.Id).ToList();
        if (ids.Count == 0) { Drift.Clear(); return false; }
        foreach (var id in ids) { int i = Idx(id); if (i != 0) Drift[id] = UserDrift(Groups[i - 1]); }
        if (!Directory.Exists(Dir)) return false;
        string ps = Path.Combine(Dir, "check.ps1");
        try { File.Delete(ps); File.Delete(ChkPath()); } catch { }
        try { File.WriteAllText(ps, CheckBuild(ids), new UTF8Encoding(true)); } catch { }
        if (!File.Exists(ps)) return false;
        try { _chk = Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ps + "\"") { UseShellExecute = false, CreateNoWindow = true }); }
        catch { return false; }
        _chkAt = Clock.Tick;
        _chkPoll ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _chkPoll.Tick -= OnChkPoll; _chkPoll.Tick += OnChkPoll; _chkPoll.Start();
        return true;
    }
    static void OnChkPoll(object? s, EventArgs e)
    {
        if (_chk is null) { _chkPoll?.Stop(); return; }
        if (!File.Exists(ChkPath()))
        {
            if (_chk.HasExited || Clock.Tick - _chkAt > 20000) { _chk = null; _chkPoll?.Stop(); }
            return;
        }
        string body = ""; try { body = File.ReadAllText(ChkPath()); } catch { }
        _chk = null; _chkPoll?.Stop();
        foreach (var raw in body.TrimStart('\uFEFF').Split('\n'))
        {
            var f = raw.TrimEnd('\r').Split('\t'); if (f.Length < 2) continue;
            string id = f[0].Trim(), val = f[1].Trim();
            if (Idx(id) == 0) continue;
            int b = Drift.TryGetValue(id, out var d) ? d : 0;
            Drift[id] = val == "?" ? -1 : b + (int.TryParse(val, out var n) ? n : 0);
        }
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Boot()
    {
        if (!Os.IsWin) return;
        CheckRun();
        _driftTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DRIFTMS) };
        _driftTimer.Tick -= OnDrift; _driftTimer.Tick += OnDrift; _driftTimer.Start();
    }
    static void OnDrift(object? s, EventArgs e) => CheckRun();

    // ---- the buttons ----
    public static void Toggle(int i) { if (i < 1 || i > Groups.Length) return; var g = Groups[i - 1]; Run(g.Id, IsOn(g.Id) ? "revert" : "apply"); }
    public static bool Reapply()
    {
        if (Busy != "") { Say("ALREADY RUNNING - GIVE IT A MOMENT", AMBER); return false; }
        Queue.Clear();
        foreach (var g in Groups) if (IsOn(g.Id) && DriftOf(g.Id) > 0) Queue.Add(g.Id);
        if (Queue.Count == 0) { Say(OnCount() > 0 ? "NOTHING HAS DRIFTED" : "NOTHING IS APPLIED", AMBER); return false; }
        QMode = "reapply";
        return QueueNext();
    }
    public static bool RevertAll()
    {
        if (Busy != "") { Say("ALREADY RUNNING - GIVE IT A MOMENT", AMBER); return false; }
        Queue.Clear();
        foreach (var g in Groups) if (IsOn(g.Id)) Queue.Add(g.Id);
        if (Queue.Count == 0) { Say("NOTHING IS APPLIED", AMBER); return false; }
        QMode = "revert";
        return QueueNext();
    }
    static bool QueueNext()
    {
        string md = QMode != "" ? QMode : "revert";
        while (Queue.Count > 0)
        {
            string id = Queue[0]; Queue.RemoveAt(0);
            if (IsOn(id)) return Run(id, md);
        }
        return false;
    }
    public static string Status()
    {
        if (!Os.IsWin) return "a windows feature - the registry, bcdedit and powercfg have no macOS equivalent";
        if (Busy != "") return "working - approve the elevation prompt if it is waiting";
        int n = OnCount();
        if (n == 0) return "nothing applied - every setting is as Windows left it";
        bool rb = Groups.Any(g => IsOn(g.Id) && g.Rb);
        int dn = DriftN();
        return n + " of " + SYSN + " applied" + (dn > 0 ? "  \u00B7  " + dn + " no longer set - RE-APPLY" : "") + (rb ? "  \u00B7  a restart is pending" : "");
    }
}
