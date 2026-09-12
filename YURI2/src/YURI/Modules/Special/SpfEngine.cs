using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Special;

/// <summary>One log file's session, as SPFSessGet keeps it.</summary>
public sealed class Sess
{
    public string Place = "", Job = "", Ip = "", JoinTs = "", JoinSrc = "", JoinLn = "", SrvType = "", SrvStart = "";
    public bool InGame, Srv; public long JoinAt, At;
    public void Clear() { InGame = false; Place = ""; Job = ""; Ip = ""; JoinAt = 0; JoinTs = ""; JoinSrc = ""; JoinLn = ""; Srv = false; SrvType = ""; SrvStart = ""; }
}
public sealed record HistEntry(string Place, string Job, DateTime At) { public string Type = ""; }

/// <summary>
/// The tail. Every 1.5 s (300 ms while hunting) it reads what the client
/// appended to its logs since the last look — each file from where it left
/// off, the first look from the last half megabyte, a file idle for ten
/// minutes from its end — and parses joins, leaves, addresses, the server
/// type and start into a session per log. The log owned by the focused
/// client wins; with one client any live session does; with several and no
/// focus, nothing is shown rather than the wrong one. From the shown
/// session flow the region lookup, the game name, the Discord presence, the
/// history and the matchmaker. SPFTick / SPFSync / SPFParse / SPFLogRead /
/// SPFLogOwners / SPFPickSession, with the geoip / rbxgame / rbxuser
/// children folded into Tasks.
/// </summary>
public static class SpfEngine
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    const int LOGN = 8, STALE_MIN = 10, TS_SLACK = 60, GONE_MS = 5000, MM_FAST = 300;

    // ---- the sessions ----
    static readonly Dictionary<string, Sess> SessMap = new();          // SPF.sess
    static readonly Dictionary<string, long> LogPos = new();           // SPF.logPos
    static readonly Dictionary<string, bool> FreshLogs = new();        // SPF.freshLogs
    static Dictionary<string, int> LogOwn = new();                     // SPF.logOwn
    static long _logOwnAt; static string _logOwnKey = "";
    static DateTime? _rbxStartU;                                        // SPF.rbxStartU
    public static string ShownLog = "", ShownPlace = "", ShownJob = "";
    public static string Ip = "", SrvType = "", SrvStart = "";
    public static string UserId = "", UserDsp = "";
    public static long JoinAt, NoRbxAt, RbxGone;
    public static bool AttrOk, WasOn;
    static int _focPid;
    static readonly Dictionary<string, long> IpScanFor = new();

    // ---- the region and the home ----
    static readonly Dictionary<string, string> GeoCache = new();
    public static string RegionIP = ""; public static long RegionAt; static int _geoFail; static int _geoWait = 8000; static bool _geoBusy;
    public static string HomeCC = "", HomeLa = "", HomeLn = "", SrvLa = "", SrvLn = ""; public static int SrvKm = -1; static long _homeAt; static bool _homeBusy;
    static readonly Dictionary<string, int> SrvSeen = new();          // SPF.srvSeen: place|job -> km
    // ---- the name and the user ----
    static string _namePlace = ""; static long _nameAt; static int _nameWait = 6000;
    static string _userFor = ""; static long _userAt; static int _userWait = 6000; static bool _userBusy;
    public static string Creator = "", CreatorId = "", CreatorTy = "";
    public static bool DcSent;                                          // SPF.dcSent: the presence must be re-sent
    static long _rbxNAt; static int _rbxN;
    static DispatcherTimer? _timer;

    static readonly Regex TsRx = new("^(\\d{4})-(\\d{2})-(\\d{2})T(\\d{2}):(\\d{2}):(\\d{2})", RegexOptions.Compiled);
    static readonly Regex PlaceIdWord = new("\\b(?:place|universe)_?id\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex UserIdRx = new("\\buserid\\D{0,4}(\\d{3,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex JoinRx = new("! Joining game '([^']+)' place (\\d+) at ([0-9.]+)", RegexOptions.Compiled);
    static readonly Regex LeaveRx = new("Client:Disconnect|leaveUGCGameInternal|Received disconnect|GameDisconnect|handleGameWillClose|Leaving game|NetworkClient:Disconnect|Disconnect from server|returnToLuaApp|leaveGameInternal|DisconnectReason", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex PrivRx = new("joinGamePostPrivateServer|\"accessCode\"\\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex ResRx = new("TeleportToReservedServer|reservedServer", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex PrefixRx = new("Server Prefix:.*_(\\d{8})T(\\d{6})Z_RCC", RegexOptions.Compiled);
    static readonly Regex UdmuxRx = new("UDMUX Address = ([0-9.]+)", RegexOptions.Compiled);
    static readonly Regex JoinCallRx = new("joinGame(?:Instance|PostPrivateServer|PostStandard|Now)|launchUGCGameInternal|initiateTeleport", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex ServerIdRx = new("serverId:\\s*([0-9.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex ConnWord = new("connect|serveraddress|rcc address|udmux", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex IpPortRx = new("([0-9]{1,3}(?:\\.[0-9]{1,3}){3})\\s*[|:]\\s*[0-9]{4,5}", RegexOptions.Compiled);
    static readonly Regex PlaceRx = new("place[_ ]?id\\D{0,8}(\\d{5,})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
    static SpfEngine() { Http.DefaultRequestHeaders.UserAgent.ParseAdd("YURI"); }

    /// <summary>Set by a host that must not touch the network (the test harness).</summary>
    public static bool NoNetwork;

    public static void Register()
    {
        Spf.SrvHintShort = SrvHintShort;
        Spf.RbxCount = () => RbxCount();
        Spf.SrvChip = SrvChip;
        Spf.SrvHeld = SrvHeld;
        Spf.HistName = HistName;
        Spf.EngineSync = Sync;
        Sync();
    }

    // ---- the engine's clock: SPFSync / SPFTick / SPFKick ----
    static bool AnyOn => Spf.Discord || Spf.Region || Spf.Match || Spf.Odds || Spf.Hunting || Spf.Srv || Spf.NoApp;
    public static void Sync()
    {
        if (AnyOn)
        {
            if (!WasOn) { LogPos.Clear(); SessMap.Clear(); ShownLog = ""; ShownPlace = ""; ShownJob = ""; IpScanFor.Clear(); }
            WasOn = true;
            _timer ??= new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1500) };
            _timer.Interval = TimeSpan.FromMilliseconds(Spf.Hunting ? MM_FAST : 1500);
            _timer.Tick -= OnTick; _timer.Tick += OnTick;
            _timer.Start();
            Dispatcher.UIThread.Post(Tick, DispatcherPriority.Background);       // SPFKick: the first look at once
        }
        else
        {
            WasOn = false;
            _timer?.Stop();
            SpfDiscord.Close();
            ClearGame();
        }
    }
    static void OnTick(object? s, EventArgs e) => Tick();

    /// <summary>SPFTick().</summary>
    /// <summary>Set by the test harness: a fault inside the tick is thrown rather than swallowed.</summary>
    public static bool Rethrow;
    public static void Tick()
    {
        if (!AnyOn) return;
        try
        {
            foreach (var (pth, txt) in LogRead()) Parse(txt, pth);
            string want = PickSession();
            bool blocked = RbxCount() > 1 && !AttrOk;
            if (blocked || want == "")
            {
                ShowNone();
                ShownLog = "";
                if (blocked) SpfDiscord.Close();
            }
            else { ShowSession(want); ShownLog = want; }
            RbxWatch();
            NameFetch();
            UserFetch();
            SpfDiscord.FocusTick();
            HomeFetch();
            if (Spf.Region || Spf.Match || Spf.Odds || Spf.Hunting) RegionFetch();
            if (!blocked) SpfMatch.Tick();
            if (Spf.Discord && !blocked) SpfDiscord.Push();
            else if (SpfDiscord.Open) SpfDiscord.Close();
        }
        catch { if (Rethrow) throw; }
    }

    // ---- the logs ----
    /// <summary>Set by a host that keeps its own logs folder (the test harness).</summary>
    public static string? LogDirOverride;
    public static string LogDir()
    {
        if (LogDirOverride is not null) return LogDirOverride;
        if (Os.IsWin) { string la = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? ""; return la == "" ? "" : Path.Combine(la, "Roblox", "logs"); }
        if (Os.IsMac) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "Roblox");
        return "";
    }
    /// <summary>SPFRecentLogs(n): the n newest logs, newest first.</summary>
    public static List<string> RecentLogs(int n = LOGN)
    {
        var res = new List<string>();
        string dir = LogDir();
        if (dir == "" || !Directory.Exists(dir)) return res;
        try
        {
            res.AddRange(new DirectoryInfo(dir).EnumerateFiles("*.log").OrderByDescending(f => f.LastWriteTimeUtc).Take(n).Select(f => f.FullName));
        }
        catch { }
        return res;
    }
    /// <summary>A live client: its pid and start time (UTC). Set LiveOverride to stand in for the process list (the test harness).</summary>
    public sealed record Client(int Id, DateTime? StartU);
    public static Func<List<Client>>? LiveOverride;
    static List<Client> Live()
    {
        if (LiveOverride is not null) return LiveOverride();
        try { return Roblox.Processes().Select(p => { DateTime? st = null; try { st = p.StartTime.ToUniversalTime(); } catch { } return new Client(p.Id, st); }).ToList(); }
        catch { return new(); }
    }

    /// <summary>SPFLogOwners(): which client each recent log belongs to, by start times; cached eight seconds per client set.</summary>
    static Dictionary<string, int> LogOwners()
    {
        var live = Live();
        DateTime? oldest = null;
        var starts = new Dictionary<int, DateTime>();
        foreach (var p in live)
        {
            var u = p.StartU;
            if (u is null) continue;
            starts[p.Id] = u.Value;
            if (oldest is null || u < oldest) oldest = u;
        }
        _rbxStartU = oldest;
        string key = string.Join(",", live.Select(p => p.Id));
        if (key == _logOwnKey && _logOwnAt != 0 && Clock.Tick - _logOwnAt < 8000) return LogOwn;
        _logOwnAt = Clock.Tick; _logOwnKey = key;
        var files = RecentLogs();
        var own = new Dictionary<string, int>();
        if (live.Count == 1)
        {
            int pid = live[0].Id;
            starts.TryGetValue(pid, out var st1);
            foreach (var f in files)
            {
                try
                {
                    if (st1 != default)
                    {
                        var mt = File.GetLastWriteTimeUtc(f);
                        if ((mt - st1).TotalSeconds < -2) continue;
                    }
                }
                catch { }
                own[f] = pid;
            }
            return LogOwn = own;
        }
        foreach (var f in files)
        {
            DateTime ct;
            try { ct = File.GetCreationTimeUtc(f); } catch { continue; }
            int best = 0; double bestD = 999999;
            foreach (var (pid, st) in starts)
            {
                double d = (ct - st).TotalSeconds;
                if (d >= -2 && d < bestD) { bestD = d; best = pid; }
            }
            if (best != 0) own[f] = best;
        }
        return LogOwn = own;
    }

    /// <summary>SPFLogRead(): what each recent log appended since the last look.</summary>
    static Dictionary<string, string> LogRead()
    {
        var res = new Dictionary<string, string>();
        var files = RecentLogs();
        int cap = LOGN * 3;
        if (LogPos.Count > cap) foreach (var k in LogPos.Keys.Except(files).ToList()) LogPos.Remove(k);
        if (SessMap.Count > cap) foreach (var k in SessMap.Keys.Except(files).ToList()) SessMap.Remove(k);
        foreach (var p in files)
        {
            long sz;
            try { sz = new FileInfo(p).Length; } catch { continue; }
            bool first = !LogPos.ContainsKey(p);
            long pos = first ? -1 : LogPos[p];
            if (pos < 0)
            {
                bool stale = false;
                try { stale = (DateTime.UtcNow - File.GetLastWriteTimeUtc(p)).TotalMinutes >= STALE_MIN; } catch { }
                pos = stale ? sz : Math.Max(0, sz - 524288);
            }
            else if (sz < pos) { pos = 0; first = true; }
            if (sz <= pos) { LogPos[p] = sz; continue; }
            if (sz - pos > 2097152) { pos = sz - 2097152; first = true; }
            try
            {
                using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                fs.Position = pos;
                var buf = new byte[sz - pos];
                int got = 0;
                while (got < buf.Length) { int n = fs.Read(buf, got, buf.Length - got); if (n <= 0) break; got += n; }
                if (got == 0) continue;
                int nl = Array.LastIndexOf(buf, (byte)'\n', got - 1);
                long endPos;
                if (nl >= 0) { endPos = pos + nl + 1; got = nl + 1; }
                else if (got < 65536) continue;
                else endPos = pos + got;
                string txt = Encoding.UTF8.GetString(buf, 0, got);
                if (first)
                {
                    int nl0 = txt.IndexOf('\n');
                    txt = nl0 >= 0 ? txt[(nl0 + 1)..] : "";
                }
                LogPos[p] = endPos;
                FreshLogs[p] = !first;
                if (txt != "") res[p] = txt;
            }
            catch { }
        }
        return res;
    }

    static Sess SessGet(string pth) { if (!SessMap.TryGetValue(pth, out var e)) SessMap[pth] = e = new Sess(); return e; }
    static bool SessStrong(Sess e) => e.Job != "" || e.Ip != "" || e.Srv;
    static bool SessLive(Sess e)
    {
        if (!e.InGame || !SessStrong(e)) return false;
        if (e.JoinTs != "" && _rbxStartU is { } rs && DateTime.TryParseExact(e.JoinTs, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var jt))
            if ((jt - rs).TotalSeconds < -TS_SLACK) return false;
        return true;
    }
    static void SessRaw(string pth)
    {
        var e = SessGet(pth);
        if (SessLive(e)) { Spf.Place = e.Place; Spf.Job = e.Job; Ip = e.Ip; Spf.InGame = true; JoinAt = e.JoinAt; }
        else { Spf.Place = ""; Spf.Job = ""; Ip = ""; Spf.InGame = false; JoinAt = 0; }
    }
    static void ShowSession(string pth)
    {
        var e = SessGet(pth);
        bool live = SessLive(e);
        string nowPl = live ? e.Place : "", nowJb = live ? e.Job : "";
        bool fresh = ShownPlace != nowPl || ShownJob != nowJb;
        SessRaw(pth);
        if (fresh)
        {
            Spf.GameNm = ""; _namePlace = ""; Creator = ""; CreatorId = ""; CreatorTy = "";
            Spf.RegionText = ""; Spf.RegionCC = ""; Spf.RegionIP = "";
            SrvLa = ""; SrvLn = ""; SrvKm = -1;
            ShownPlace = nowPl; ShownJob = nowJb;
        }
        SrvType = live ? e.SrvType : ""; SrvStart = live ? e.SrvStart : "";
        if (Spf.Srv && fresh && live) HistNote(nowPl, nowJb);
        if (Spf.Hist.Count > 0 && SrvType != "" && Spf.Hist[0].Job == nowJb) Spf.Hist[0].Type = SrvType;
    }
    static void ShowNone()
    {
        if (ShownPlace != "" || Spf.InGame) { ClearGame(); ShownPlace = ""; ShownJob = ""; }
    }
    public static void ClearGame()
    {
        Spf.InGame = false; Spf.Place = ""; Spf.Job = ""; Ip = "";
        Spf.GameNm = ""; _namePlace = ""; Creator = ""; CreatorId = ""; CreatorTy = ""; JoinAt = 0;
        Spf.RegionText = ""; Spf.RegionCC = ""; Spf.RegionIP = "";
        SrvLa = ""; SrvLn = ""; SrvKm = -1;
    }
    /// <summary>SPFPurgeSessions(): every remembered session cleared (the client is gone).</summary>
    public static int PurgeSessions()
    {
        int n = 0;
        foreach (var e in SessMap.Values) if (e.InGame || e.Place != "") { e.Clear(); n++; }
        return n;
    }

    /// <summary>SPFParse(chunk, pth): the log lines into the session.</summary>
    public static void Parse(string chunk, string pth)
    {
        if (chunk == "" || pth == "") return;
        var e = SessGet(pth);
        bool changed = false;
        foreach (var raw in chunk.Split('\n'))
        {
            var ln = raw.TrimEnd('\r');
            if (ln == "") continue;
            var tm = TsRx.Match(ln);
            string lnTs = tm.Success ? tm.Groups[1].Value + tm.Groups[2].Value + tm.Groups[3].Value + tm.Groups[4].Value + tm.Groups[5].Value + tm.Groups[6].Value : "";
            if (PlaceIdWord.IsMatch(ln))
            {
                var um = UserIdRx.Match(ln);
                if (um.Success && UserId != um.Groups[1].Value) { UserId = um.Groups[1].Value; Spf.UserNm = ""; UserDsp = ""; _userFor = ""; changed = true; }
            }
            Match m;
            if ((m = JoinRx.Match(ln)).Success)
            {
                e.Job = m.Groups[1].Value; e.Place = m.Groups[2].Value;
                e.Ip = IsPublicIP(m.Groups[3].Value) ? m.Groups[3].Value : "";
                e.Srv = true; e.InGame = true; e.JoinAt = Clock.Tick;
                e.JoinTs = lnTs; e.JoinSrc = "joining-game"; e.JoinLn = ln.Length > 160 ? ln[..160] : ln;
                e.SrvStart = "";
                IpScanFor.Remove(pth);
                changed = true;
                continue;
            }
            if (LeaveRx.IsMatch(ln))
            {
                if (Spf.NoApp && ln.Contains("leaveUGCGameInternal") && FreshLogs.GetValueOrDefault(pth)) NoAppClose(pth);
                if (e.InGame || e.Place != "") { e.Clear(); changed = true; }
                continue;
            }
            if (PrivRx.IsMatch(ln)) { e.SrvType = "private"; changed = true; }
            else if (ResRx.IsMatch(ln)) { e.SrvType = "reserved"; changed = true; }
            if ((m = PrefixRx.Match(ln)).Success) { e.SrvStart = m.Groups[1].Value + m.Groups[2].Value; changed = true; }
            if ((m = UdmuxRx.Match(ln)).Success)
            {
                if (!e.Srv) { e.Srv = true; changed = true; }
                if (e.Ip != m.Groups[1].Value && IsPublicIP(m.Groups[1].Value)) { e.Ip = m.Groups[1].Value; changed = true; }
                continue;
            }
            if (JoinCallRx.IsMatch(ln))
            {
                e.InGame = true;
                if (e.JoinAt == 0) { e.JoinAt = Clock.Tick; e.JoinTs = lnTs; e.JoinSrc = "join-call"; e.JoinLn = ln.Length > 160 ? ln[..160] : ln; }
                changed = true;
                continue;
            }
            if ((m = ServerIdRx.Match(ln)).Success)
            {
                e.InGame = true; e.Srv = true;
                if (e.Ip == "" && IsPublicIP(m.Groups[1].Value)) e.Ip = m.Groups[1].Value;
                if (e.JoinAt == 0) { e.JoinAt = Clock.Tick; e.JoinTs = lnTs; e.JoinSrc = "server-id"; e.JoinLn = ln.Length > 160 ? ln[..160] : ln; }
                changed = true;
                continue;
            }
            if (e.Ip == "" && ConnWord.IsMatch(ln) && (m = IpPortRx.Match(ln)).Success)
            {
                if (IsPublicIP(m.Groups[1].Value)) { e.Ip = m.Groups[1].Value; e.Srv = true; changed = true; }
                continue;
            }
            if ((m = PlaceRx.Match(ln)).Success)
            {
                if (SessStrong(e) && e.Place == "") { e.Place = m.Groups[1].Value; changed = true; }
                continue;
            }
        }
        e.At = Clock.Tick;
        if (changed) { Spf.Pulse = Clock.Tick; Poke(); }
    }

    public static bool IsPublicIP(string ip)
    {
        var p = ip.Split('.');
        if (p.Length != 4) return false;
        var v = new int[4];
        for (int i = 0; i < 4; i++) if (!int.TryParse(p[i], out v[i]) || v[i] < 0 || v[i] > 255) return false;
        int a = v[0], b = v[1];
        if (a == 0 || a == 10 || a == 127 || a >= 224) return false;
        if (a == 169 && b == 254) return false;
        if (a == 172 && b >= 16 && b <= 31) return false;
        if (a == 192 && b == 168) return false;
        if (a == 100 && b >= 64 && b <= 127) return false;
        return true;
    }

    /// <summary>SPFPickSession(): the log to show — the focused client's, else the newest live one.</summary>
    static string PickSession()
    {
        var live = Live();
        if (live.Count == 0)
        {
            AttrOk = true;
            if (NoRbxAt == 0) { NoRbxAt = Clock.Tick; return ShownLog; }
            if (Clock.Tick - NoRbxAt >= GONE_MS) PurgeSessions();
            return "";
        }
        NoRbxAt = 0;
        var own = LogOwners();
        int fpid = FocusPid(live);
        AttrOk = false;
        string want = ""; long bestJoin = 0, bestAt = 0;
        if (fpid != 0)
        {
            bool haveGame = false;
            foreach (var (pth, opid) in own)
            {
                if (opid != fpid || !SessMap.TryGetValue(pth, out var e)) continue;
                if (SessLive(e)) { if (!haveGame || e.JoinAt >= bestJoin) { bestJoin = e.JoinAt; want = pth; haveGame = true; } }
                else if (!haveGame && e.At > bestAt) { bestAt = e.At; want = pth; }
            }
            if (want != "") AttrOk = true;
        }
        if (want != "") return want;
        bestJoin = 0; bool have = false;
        foreach (var (pth, e) in SessMap)
        {
            if (!SessLive(e)) continue;
            if (own.Count > 0 && !own.ContainsKey(pth)) continue;
            if (!have || e.JoinAt >= bestJoin) { bestJoin = e.JoinAt; want = pth; have = true; }
        }
        if (want == "" && live.Count == 1)
        {
            foreach (var (pth, e) in SessMap)
                if (SessLive(e) && (!have || e.JoinAt >= bestJoin)) { bestJoin = e.JoinAt; want = pth; have = true; }
            if (want != "") AttrOk = true;
        }
        return want;
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    /// <summary>SPFFocusPid(): the client in front; on macOS the client is never "in front" here, so the newest session wins.</summary>
    static int FocusPid(List<Client> live)
    {
        if (Os.IsWin)
        {
            try
            {
                var hw = GetForegroundWindow();
                if (hw != IntPtr.Zero && GetWindowThreadProcessId(hw, out uint pid) != 0 && live.Any(p => p.Id == pid)) { _focPid = (int)pid; return _focPid; }
            }
            catch { }
        }
        if (_focPid != 0 && live.Any(p => p.Id == _focPid)) return _focPid;
        _focPid = 0;
        return 0;
    }

    /// <summary>SPFRbxCount(): how many clients run, at most every 1.5 s.</summary>
    public static int RbxCount(bool force = false)
    {
        if (!force && _rbxNAt != 0 && Clock.Tick - _rbxNAt < 1500) return _rbxN;
        _rbxNAt = Clock.Tick;
        _rbxN = Live().Count;
        return _rbxN;
    }
    /// <summary>SPFRbxWatch(): a client gone for five seconds ends the shown session.</summary>
    static void RbxWatch()
    {
        if (!Spf.InGame) { RbxGone = 0; return; }
        if (Live().Count > 0) { RbxGone = 0; return; }
        if (RbxGone == 0) { RbxGone = Clock.Tick; return; }
        if (Clock.Tick - RbxGone < 5000) return;
        RbxGone = 0;
        PurgeSessions();
        ClearGame();
        Spf.MmNote = "";
        Spf.Pulse = Clock.Tick;
        Poke();
    }

    // ---- the region ----
    /// <summary>SPFIpRescan(): no address in the tail yet — read the whole log for one.</summary>
    static bool IpRescan()
    {
        if (!Spf.InGame || Ip != "") return false;
        if (!Spf.Region && !Spf.Match && !Spf.Odds && !Spf.Hunting) return false;
        string key = ShownLog != "" ? ShownLog : "\f";
        if (IpScanFor.TryGetValue(key, out var at) && Clock.Tick - at < 10000) return false;
        IpScanFor[key] = Clock.Tick;
        if (IpScanFor.Count > LOGN * 3) { IpScanFor.Clear(); IpScanFor[key] = Clock.Tick; }
        string found = "";
        var scan = ShownLog != "" && File.Exists(ShownLog) ? new List<string> { ShownLog } : RecentLogs();
        var pats = new[] { new Regex("! Joining game .[^']+. place \\d+ at ([0-9]{1,3}(?:\\.[0-9]{1,3}){3})"), new Regex("UDMUX Address = ([0-9]{1,3}(?:\\.[0-9]{1,3}){3})"),
                           new Regex("serverId:\\s*([0-9]{1,3}(?:\\.[0-9]{1,3}){3})"), new Regex("([0-9]{1,3}(?:\\.[0-9]{1,3}){3})\\s*[|:]\\s*[0-9]{4,5}") };
        foreach (var p in scan)
        {
            string txt = "";
            try
            {
                using var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (fs.Length > 4194304) fs.Position = fs.Length - 4194304;
                using var rd = new StreamReader(fs, Encoding.UTF8);
                txt = rd.ReadToEnd();
            }
            catch { }
            if (txt == "") continue;
            foreach (var rx in pats)
            {
                string hit = "";
                foreach (Match m in rx.Matches(txt)) if (IsPublicIP(m.Groups[1].Value)) hit = m.Groups[1].Value;
                if (hit != "") { found = hit; break; }
            }
            if (found != "") break;
        }
        if (found == "") return false;
        if (ShownLog != "") SessGet(ShownLog).Ip = found;
        else
        {
            long best = 0; string tgt = "";
            foreach (var (pth, se) in SessMap) if (se.InGame && se.JoinAt >= best) { best = se.JoinAt; tgt = pth; }
            if (tgt != "") SessMap[tgt].Ip = found;
        }
        Ip = found; Spf.RegionText = ""; Spf.RegionCC = ""; Spf.RegionIP = ""; _geoFail = 0; _geoWait = 8000;
        Spf.Pulse = Clock.Tick; Poke();
        return true;
    }
    /// <summary>SPFRegionFetch(): where the server is, once per address, backing off on failure.</summary>
    static void RegionFetch()
    {
        if (Ip == "" && Spf.InGame) IpRescan();
        if (Ip == "") return;
        if (Spf.RegionText == "" && GeoCache.TryGetValue(Ip, out var cached)) { RegionIP = Ip; Spf.RegionIP = Ip; RegionGot(cached); return; }
        if (Ip != RegionIP) { _geoFail = 0; _geoWait = 8000; }
        if (Ip == RegionIP && (Spf.RegionText != "" || Clock.Tick - RegionAt < _geoWait)) return;
        if (_geoBusy || NoNetwork) return;
        if (Ip == RegionIP && Spf.RegionText == "") { _geoFail++; if (_geoFail >= 3) _geoWait = Math.Min(_geoWait * 2, 60000); }
        RegionIP = Ip; Spf.RegionIP = Ip; RegionAt = Clock.Tick;
        string target = Ip;
        _geoBusy = true;
        _ = Task.Run(async () =>
        {
            string res = await GeoLookupRaw(target, false);
            Dispatcher.UIThread.Post(() => { _geoBusy = false; if (res != "" && RegionIP == target) RegionGot(res); else if (res == "") _geoWait = Math.Min(_geoWait * 2, 60000); });
        });
    }
    static void RegionGot(string payload)
    {
        var ss = payload.Split('|');
        if (ss.Length < 2) return;
        if (RegionIP != "" && payload.Replace("|", "").Trim() != "") { if (GeoCache.Count >= 256) GeoCache.Clear(); GeoCache[RegionIP] = payload; }
        _geoFail = 0; _geoWait = 8000;
        Spf.RegionText = ss[0]; Spf.RegionCC = ss[1];
        SrvLa = ss.Length >= 3 ? ss[2] : ""; SrvLn = ss.Length >= 4 ? ss[3] : "";
        SrvKm = DistKm(HomeLa, HomeLn, SrvLa, SrvLn);
        SrvRemember();
        RegionAt = Clock.Tick; Spf.Pulse = Clock.Tick;
        if (Spf.RegionText != "") Spf.Say("SERVER REGION - " + Spf.RegionText, C_ON);
        Poke();
    }
    /// <summary>GeoLookupRaw(target): "loc|cc|lat|lng" from the first service that answers.</summary>
    public static async Task<string> GeoLookupRaw(string target, bool fast)
    {
        bool self = target == "self";
        var urls = self ? new[] { "https://ipinfo.io/json", "https://ipwho.is/", "http://ip-api.com/json/" }
                        : new[] { "https://ipinfo.io/" + target + "/json", "https://ipwho.is/" + target, "http://ip-api.com/json/" + target };
        if (fast) urls = new[] { urls[0] };
        foreach (var url in urls)
        {
            string body;
            try
            {
                using var cts = new CancellationTokenSource(fast ? 1400 : 5000);
                using var res = await Http.GetAsync(url, cts.Token);
                if (!res.IsSuccessStatusCode) continue;
                body = await res.Content.ReadAsStringAsync(cts.Token);
            }
            catch { continue; }
            if (Regex.IsMatch(body, "\"status\"\\s*:\\s*\"fail\"", RegexOptions.IgnoreCase)) continue;
            if (Regex.IsMatch(body, "\"success\"\\s*:\\s*false", RegexOptions.IgnoreCase)) continue;
            string city = "", reg = "", cc = "", glat = "", glng = "";
            Match m;
            if ((m = Regex.Match(body, "\"city\"\\s*:\\s*\"([^\"]*)\"")).Success) city = m.Groups[1].Value;
            if ((m = Regex.Match(body, "\"region(?:Name)?\"\\s*:\\s*\"([^\"]*)\"")).Success) reg = m.Groups[1].Value;
            if ((m = Regex.Match(body, "\"country_?[Cc]ode\"\\s*:\\s*\"([^\"]*)\"")).Success) cc = m.Groups[1].Value;
            else if ((m = Regex.Match(body, "\"country\"\\s*:\\s*\"([^\"]*)\"")).Success) cc = m.Groups[1].Value;
            if ((m = Regex.Match(body, "\"loc\"\\s*:\\s*\"(-?[0-9.]+),(-?[0-9.]+)\"")).Success) { glat = m.Groups[1].Value; glng = m.Groups[2].Value; }
            else
            {
                var m1 = Regex.Match(body, "\"lat(?:itude)?\"\\s*:\\s*(-?[0-9.]+)"); var m2 = Regex.Match(body, "\"lon(?:gitude)?\"\\s*:\\s*(-?[0-9.]+)");
                if (m1.Success && m2.Success) { glat = m1.Groups[1].Value; glng = m2.Groups[1].Value; }
            }
            string loc = city != "" ? city : reg;
            if (cc != "") loc = loc == "" ? cc : loc + ", " + cc;
            if (loc == "" && glat == "") continue;
            return loc + "|" + cc + "|" + glat + "|" + glng;
        }
        return "";
    }
    /// <summary>SPFHomeFetch(): where the user is, once, for the matchmaker's distances.</summary>
    static void HomeFetch()
    {
        if ((!Spf.Match && !Spf.Odds && !Spf.Hunting) || HomeCC != "") return;
        if (_homeAt != 0 && Clock.Tick - _homeAt < 60000) return;
        if (_homeBusy || NoNetwork) return;
        _homeAt = Clock.Tick; _homeBusy = true;
        _ = Task.Run(async () =>
        {
            string res = await GeoLookupRaw("self", false);
            Dispatcher.UIThread.Post(() =>
            {
                _homeBusy = false;
                var ss = res.Split('|');
                if (res == "" || ss.Length < 2) return;
                Spf.HomeRegion = ss[0]; HomeCC = ss[1];
                HomeLa = ss.Length >= 3 ? ss[2] : ""; HomeLn = ss.Length >= 4 ? ss[3] : "";
                SrvKm = DistKm(HomeLa, HomeLn, SrvLa, SrvLn);
                Spf.Pulse = Clock.Tick; Poke();
            });
        });
    }
    public static int DistKm(string la1, string ln1, string la2, string ln2)
    {
        if (la1 == "" || ln1 == "" || la2 == "" || ln2 == "") return -1;
        if (!double.TryParse(la1, NumberStyles.Float, CultureInfo.InvariantCulture, out var a1) || !double.TryParse(ln1, NumberStyles.Float, CultureInfo.InvariantCulture, out var b1)
            || !double.TryParse(la2, NumberStyles.Float, CultureInfo.InvariantCulture, out var a2) || !double.TryParse(ln2, NumberStyles.Float, CultureInfo.InvariantCulture, out var b2)) return -1;
        const double Rk = 6371.0, D = Math.PI / 180;
        double p1 = a1 * D, p2 = a2 * D, dp = (a2 - a1) * D, dl = (b2 - b1) * D;
        double h = Math.Pow(Math.Sin(dp / 2), 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Pow(Math.Sin(dl / 2), 2);
        h = Math.Min(1.0, Math.Max(0.0, h));
        return (int)Math.Round(2 * Rk * Math.Asin(Math.Sqrt(h)));
    }
    static void SrvRemember()
    {
        if (!Spf.Odds || Spf.Place == "" || Spf.Job == "" || SrvKm < 0) return;
        string k = Spf.Place + "|" + Spf.Job;
        if (SrvSeen.ContainsKey(k)) return;
        SrvSeen[k] = SrvKm;
        if (SrvSeen.Count > 400) SrvSeen.Clear();
    }
    public static int SrvSeenKm(string place, string job) => SrvSeen.TryGetValue(place + "|" + job, out var km) ? km : -1;

    // ---- the game's name and the user's name ----
    static void NameFetch()
    {
        if (!Spf.InGame || Spf.Place == "" || Spf.GameNm != "") return;
        var r = GameInfo.Get(Spf.Place);
        if (r is not null && !r.Failed && r.Nm != "")
        {
            if (Spf.GameNm != r.Nm) { Spf.GameNm = r.Nm; Spf.Pulse = Clock.Tick; DcSent = false; Poke(); }
            return;
        }
        if (Spf.Place != _namePlace) { _nameWait = 6000; _namePlace = Spf.Place; _nameAt = Clock.Tick; GameInfo.Fetch(Spf.Place); return; }
        if (Clock.Tick - _nameAt < _nameWait) return;
        _nameWait = Math.Min(_nameWait * 2, 60000); _nameAt = Clock.Tick;
        GameInfo.Fetch(Spf.Place);
    }
    static void UserFetch()
    {
        if (!Spf.Discord || !Spf.DcAcct) return;
        if (UserId == "" || Spf.UserNm != "") return;
        if (UserId != _userFor) _userWait = 6000;
        if (UserId == _userFor && Clock.Tick - _userAt < _userWait) return;
        if (UserId == _userFor && _userAt != 0) _userWait = Math.Min(_userWait * 2, 60000);
        if (_userBusy || NoNetwork) return;
        _userFor = UserId; _userAt = Clock.Tick; _userBusy = true;
        string uid = UserId;
        _ = Task.Run(async () =>
        {
            string nm = "", dsp = "";
            try
            {
                using var res = await Http.GetAsync("https://users.roblox.com/v1/users/" + uid);
                if (res.IsSuccessStatusCode)
                {
                    string body = await res.Content.ReadAsStringAsync();
                    var m1 = Regex.Match(body, "\"name\"\\s*:\\s*\"([^\"]*)\""); if (m1.Success) nm = m1.Groups[1].Value;
                    var m2 = Regex.Match(body, "\"displayName\"\\s*:\\s*\"([^\"]*)\""); if (m2.Success) dsp = m2.Groups[1].Value;
                }
            }
            catch { }
            Dispatcher.UIThread.Post(() =>
            {
                _userBusy = false;
                if (nm == "" || uid != UserId) return;
                Spf.UserNm = nm; UserDsp = dsp; Spf.Pulse = Clock.Tick; DcSent = false; Poke();
            });
        });
    }

    // ---- SERVER DETAILS ----
    public static string SrvTypeLabel() => SrvType != "" ? SrvType : "public";
    public static string SrvUptime()
    {
        if (SrvStart == "" || !DateTime.TryParseExact(SrvStart, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var st)) return "";
        double s = (DateTime.UtcNow - st).TotalSeconds;
        if (s < 0) return "";
        long sec = (long)s;
        return sec >= 3600 ? $"{sec / 3600}h {(sec / 60) % 60}m" : $"{sec / 60}m";
    }
    public static string SrvShortId(string j) => j == "" ? "" : j[..Math.Min(8, j.Length)] + "\u2026";
    public static string SrvLink(string pl, string job) => "roblox://experiences/start?placeId=" + pl + "&gameInstanceId=" + job;
    static string SrvHintShort()
    {
        if (Spf.InGame && Spf.Job != "")
        {
            string up = SrvUptime();
            return SrvTypeLabel() + (up != "" ? "  \u00B7  up " + up : "");
        }
        return Spf.Hist.Count > 0 ? $"{Spf.Hist.Count} server{(Spf.Hist.Count == 1 ? "" : "s")} this session" : "waiting for a join";
    }
    static void HistNote(string pl, string job)
    {
        if (pl == "" || job == "") return;
        if (Spf.Hist.Count > 0 && Spf.Hist[0].Job == job) return;
        Spf.Hist.Insert(0, new HistEntry(pl, job, DateTime.Now) { Type = SrvType });
        while (Spf.Hist.Count > 40) Spf.Hist.RemoveAt(Spf.Hist.Count - 1);
    }
    static string HistName(HistEntry h)
    {
        if (Spf.Place == h.Place && Spf.GameNm != "") return Spf.GameNm;
        var r = GameInfo.Get(h.Place);
        if (r is not null && r.Nm != "") return r.Nm;
        return "place " + h.Place;
    }
    /// <summary>SPFSrvChip(k): REJOIN LAST, COPY LINK, COPY ID.</summary>
    static void SrvChip(int k)
    {
        if (k == 1)
        {
            if (Spf.Hist.Count == 0) { Spf.Say("NO SERVER TO REJOIN YET - JOIN ONE FIRST", AMBER); return; }
            var h = Spf.Hist[0];
            bool ok = true;
            try { Roblox.OpenUrl(SrvLink(h.Place, h.Job)); } catch { ok = false; }
            if (ok) Spf.Say("REJOINING " + HistName(h).ToUpperInvariant() + "  \u00B7  " + SrvShortId(h.Job), C_ON);
            else Spf.Say("THE SYSTEM REFUSED THE roblox:// LINK - IS ROBLOX INSTALLED?", C_ACC);
        }
        else if (k == 2)
        {
            if (!Spf.InGame || Spf.Job == "") { Spf.Say("NOT IN A SERVER - NOTHING TO LINK", AMBER); return; }
            Spf.Clip?.Invoke(SrvLink(Spf.Place, Spf.Job));
            Spf.Say("INVITE LINK COPIED - ANYONE WITH ROBLOX CAN OPEN IT", C_ON);
        }
        else
        {
            if (Spf.Job == "") { Spf.Say("NOT IN A SERVER - NO INSTANCE ID", AMBER); return; }
            Spf.Clip?.Invoke(Spf.Job);
            Spf.Say("INSTANCE ID COPIED  \u00B7  " + Spf.Job, C_ON);
        }
    }
    /// <summary>SPFSrvHeld(): the explainer's RIGHT NOW paragraph.</summary>
    static string SrvHeld()
    {
        if (!Spf.Srv) return "  RIGHT NOW: off.";
        var s = new StringBuilder("  RIGHT NOW: ");
        if (Spf.InGame && Spf.Job != "")
        {
            string up = SrvUptime();
            s.Append("a ").Append(SrvTypeLabel()).Append(" server").Append(up != "" ? ", up " + up : ", uptime not reported yet").Append(", instance ").Append(Spf.Job)
             .Append(Spf.RegionText != "" ? ", in " + Spf.RegionText : "").Append('.');
        }
        else s.Append("not in a server.");
        if (Spf.Hist.Count > 0)
        {
            s.Append("  THIS SESSION, newest first:");
            foreach (var h in Spf.Hist) s.Append("  ").Append(h.At.ToString("HH:mm")).Append("  ").Append(HistName(h)).Append(" (").Append(h.Type != "" ? h.Type : "public").Append(", ").Append(SrvShortId(h.Job)).Append(')');
        }
        return s.ToString();
    }

    // ---- NO DESKTOP APP ----
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    static bool NoAppClose(string pth)
    {
        if (!LogOwn.ContainsKey(pth)) LogOwners();
        int pid = LogOwn.TryGetValue(pth, out var p) ? p : 0;
        if (pid == 0) return false;
        try
        {
            var proc = Process.GetProcessById(pid);
            if (Os.IsWin && proc.MainWindowHandle != IntPtr.Zero) PostMessage(proc.MainWindowHandle, 0x10, IntPtr.Zero, IntPtr.Zero);   // WM_CLOSE
            else proc.CloseMainWindow();
            Spf.NoAppN++;
            Spf.Say("LEFT THE GAME - CLOSING THE CLIENT INSTEAD OF THE HOME APP", C_ON);
            return true;
        }
        catch { return false; }
    }

    static void Poke() => HubSurface.Live?.Tim(Pace.TICK_A);
    /// <summary>The test harness's window into the tail.</summary>
    public static string DebugDump()
    {
        var sb = new StringBuilder();
        sb.Append("shown=").Append(ShownLog).Append(" attrOk=").Append(AttrOk).Append(" rbxStart=").Append(_rbxStartU?.ToString("O") ?? "-").Append('\n');
        foreach (var (k, v) in SessMap) sb.Append(k).Append(": in=").Append(v.InGame).Append(" place=").Append(v.Place).Append(" job=").Append(v.Job).Append(" ip=").Append(v.Ip).Append(" srv=").Append(v.Srv).Append(" joinTs=").Append(v.JoinTs).Append(" at=").Append(v.At).Append('\n');
        foreach (var (k, v) in LogPos) sb.Append("pos ").Append(k).Append('=').Append(v).Append('\n');
        foreach (var (k, v) in LogOwn) sb.Append("own ").Append(k).Append('=').Append(v).Append('\n');
        return sb.ToString();
    }
}
