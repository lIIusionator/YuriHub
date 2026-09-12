using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Platform;

namespace Yuri.Modules.Special;

/// <summary>
/// SPECIAL FEATURES - built-in module 5. Seventeen switches, each persisted
/// under [special] in zeal.ini with the .ahk's own keys. This file is the
/// state and the switches (SPFToggle); the panel is SpfPanel. Three
/// back-ends live here because they are files and a registry key on any
/// platform: the CLEANER (Roblox's logs and cache older than an age), LAUNCH
/// ON STARTUP (the Run key on Windows, a LaunchAgent on macOS) and the APP
/// THEME (appStorage.json's DeviceLevelTheme). The log tail, the region
/// lookup, Discord, the matchmaker, the multi-instance mutex, FPS BOOST, the
/// crash handler, the memory trimmer and the content mods are engines that
/// plug into the hooks below; a switch whose engine is not attached says so
/// on its hint line instead of claiming to work.
/// </summary>
public static class Spf
{
    public const int SYSN = 17;
    public const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185, GREY = 0xFFC7CBE0;
    public static readonly int[] CleanAges = { 1, 7, 30, 60 };
    public static readonly (string n, string f)[] Emoji = { ("CATMOJI", "Catmoji.ttf"), ("WINDOWS 11", "Win1122H2SegoeUIEmoji.ttf"), ("WINDOWS 10", "Win10April2018SegoeUIEmoji.ttf"), ("WINDOWS 8", "Win8.1SegoeUIEmoji.ttf") };
    public const int SAV_MAX = 8, ACCT_MAX = 8;

    // ---- the switches (spfDiscord ... spfEmoji) ----
    public static bool Discord, Region, Match, Odds, Multi, Fps, Crash, Mem, Srv, NoApp, Clean, Start, Theme, Snd, AvBg, Death, EmojiOn;
    public static int MemInt = 10, MemThr, CleanAge = 7, EmojiK = 1;
    public static bool ThemeDark = true;
    public static bool FpsGpu, FpsQuiet, FpsSys, DcAcct, DcJoin, DcFocus;
    public static string AcctTgt = "", MmLink = "", MmPriv = "";
    /// <summary>The eased switches, SPF.t1..t17, and the chips' eases.</summary>
    public static readonly double[] T = new double[SYSN + 1];
    public static double U1, U2, U3, Q1, Q2, Q3, M1, M2;
    public static readonly Dictionary<int, long> FlashAt = new();
    public static double SysScr, SysScrT;
    public static bool SaveOk = true;
    public static string Msg = ""; public static uint MsgCol; public static long MsgAt; public static long Pulse;

    // ---- the live session (what the log tail fills) ----
    public static bool InGame; public static string Place = "", Job = "", GameNm = "", RegionText = "", RegionCC = "", RegionIP = "";
    public static string HomeRegion = "", MmNote = ""; public static int MmTries; public static bool Hunting; public static int HuntWhich;
    public static int Dc;                                       // 0 off, 1 connecting, 2 linked, 3 failed
    public static string DcErr = "", UserNm = "";
    public static bool FocOn;
    public static readonly List<HistEntry> Hist = new();        // the server history (SERVER DETAILS)
    public static int CrashN, NoAppN, CleanN; public static double CleanMb; public static long CleanAt; static bool _cleanBusy;
    public static string ThemeState = "";
    /// <summary>SPFAcctN: the saved accounts. Reads the list that is actually loaded - there used to be a second, empty one here, and the row's chip read that one, so it said 0/8 whatever was saved.</summary>
    public static int AcctCount => SpfAcct.Acct.Count;
    public static int SavedCount => SpfSaved.Saved.Count;
    // ---- the views: "" / "sav" / "acct" ----
    public static string View = "", ViewPrev = ""; public static long ViewAt;
    public static void ViewOpen(string v)
    {
        ViewPrev = View; View = v; ViewAt = Clock.Tick;
        if (v == "sav")
        {
            SpfSaved.SavSel = Math.Clamp(SpfSaved.SavSel, 1, Math.Max(1, SpfSaved.Saved.Count));
            // Every saved place, not only the selected one. The list draws each
            // row's name from this, so a place that had never been selected sat
            // there as its own id. The fetches queue behind GameInfo's own gate.
            if (SpfSaved.Saved.Count > 0) Platform.GameInfo.Fetch(SpfSaved.Saved[SpfSaved.SavSel - 1].Id);
            foreach (var sv in SpfSaved.Saved) Platform.GameInfo.Fetch(sv.Id);
        }
        else if (v == "acct")
        {
            SpfAcct.Sel = Math.Clamp(SpfAcct.Sel, 1, Math.Max(1, SpfAcct.Acct.Count));
            for (int k = 1; k <= SpfAcct.Acct.Count; k++) if (SpfAcct.Acct[k - 1].Id == "") SpfAcct.NameFetch(k); else SpfAcct.ProfFetch(k);
        }
        Yuri.Shell.Hub.HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void ViewClose()
    {
        if (View == "") return;
        if (SpfSaved.RenOpen) SpfSaved.RenClose();
        if (SpfSaved.AddOpen) SpfSaved.AddClose();
        if (FastFlags.FfmField.Edit == "at") FastFlags.FfmField.End(true);
        ViewPrev = View; View = ""; ViewAt = Clock.Tick;
        Yuri.Shell.Hub.HubSurface.Live?.Tim(Pace.TICK_A);
    }

    // ---- engine hooks: attached by the engines as they land; null means "not in this build" ----
    public static Func<string>? FpsHintShort, MemHintShort, SrvHintShort;
    public static Func<int>? MultiStale, RbxCount;
    public static Func<string, string>? ModHintShort;
    public static Func<string, bool>? ModHave;
    public static Func<bool>? MultiBlock;
    public static Action<int, bool>? EngineToggle;               // (row, on): the engine's own work on a flip
    public static Action<int, bool>? ModToggle;                  // rows 14-17: the content mods
    public static Action? EngineSync;                            // SPFSync(): the log tail's clock follows the switches
    public static Action<int>? SrvChip;                          // SERVER DETAILS' REJOIN LAST / COPY LINK / COPY ID
    public static Func<string>? SrvHeld;                         // the explainer's RIGHT NOW paragraph
    public static Func<string>? MemHeld, FpsHeld;                // the same for MEMORY TRIMMER and FPS BOOST
    public static Func<HistEntry, string>? HistName;
    public static Action<string>? Clip;                          // the clipboard, set by the panel
    public static Action<List<int>>? CrashSweep;                 // DISABLE CRASH HANDLER: the engine's poll hands over the handler pids

    public static void Load()
    {
        var f = Paths.IniFile;
        int I(string k, int d) => (int)Ini.ReadInt(f, "special", k, d);
        Discord = I("discord", 0) != 0; Region = I("region", 0) != 0; Match = I("match", 0) != 0; Odds = I("odds", 0) != 0;
        Multi = I("multi", 0) != 0; Fps = I("fps", 0) != 0; Crash = I("crash", 0) != 0; Mem = I("memtrim", 0) != 0;
        MemInt = Math.Max(1, I("memint", 10)); MemThr = Math.Max(0, I("memthr", 0));
        Srv = I("srv", 0) != 0; NoApp = I("noapp", 0) != 0; Clean = I("clean", 0) != 0; CleanAge = Math.Max(1, I("cleanage", 7));
        Start = I("startup", 0) != 0; Theme = I("theme", 0) != 0; ThemeDark = I("themedark", 1) != 0;
        Snd = I("oldsnd", 0) != 0; AvBg = I("avbg", 0) != 0; Death = I("death", 0) != 0; EmojiOn = I("emoji", 0) != 0;
        EmojiK = Math.Clamp(I("emojik", 1), 1, Emoji.Length);
        FpsGpu = I("fpsgpu", 0) != 0; FpsQuiet = I("fpsquiet", 0) != 0; FpsSys = I("fpssys", 0) != 0;
        AcctTgt = Ini.Read(f, "special", "accttarget", "");
        DcAcct = I("dcacct", 0) != 0; DcJoin = I("dcjoin", 0) != 0; DcFocus = I("dcfocus", 0) != 0;
        MmLink = Ini.Read(f, "special", "gamelink", ""); MmPriv = Ini.Read(f, "special", "privlink", "");
        for (int i = 1; i <= SYSN; i++) T[i] = RowOn(i) ? 1.0 : 0.0;
        U1 = DcAcct ? 1 : 0; U2 = DcJoin ? 1 : 0; U3 = DcFocus ? 1 : 0;
        Q1 = FpsGpu ? 1 : 0; Q2 = FpsQuiet ? 1 : 0; Q3 = FpsSys ? 1 : 0;
        M1 = 1; M2 = 1;
        if (Clean) Task.Run(() => CleanRun(false));                  // SPFCleanBoot
    }

    /// <summary>SPFSave(key, val): write and read back; a failed write is what the status shows.</summary>
    public static bool Save(string key, object val)
    {
        string s = val is bool b ? (b ? "1" : "0") : Convert.ToString(val, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        bool ok = Ini.Write(Paths.IniFile, "special", key, s) && Ini.Read(Paths.IniFile, "special", key, "\u0001") == s;
        SaveOk = ok;
        if (!ok) Say("COULD NOT WRITE zeal.ini - setting will not survive a restart", C_ACC);
        return ok;
    }
    /// <summary>SPFSay(msg, col).</summary>
    public static void Say(string msg, uint col = 0)
    {
        Msg = msg; MsgCol = col != 0 ? col : C_ON; MsgAt = Clock.Tick;
        Yuri.Shell.Hub.HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static bool RowOn(int i) => i switch
    {
        1 => Discord, 2 => Region, 3 => Match, 4 => Odds, 5 => Multi, 6 => Fps, 7 => Crash, 8 => Mem, 9 => Srv, 10 => NoApp,
        11 => Clean, 12 => Start, 13 => Theme, 14 => Snd, 15 => AvBg, 16 => Death, 17 => EmojiOn, _ => false,
    };
    public static int OnCount() { int n = 0; for (int i = 1; i <= SYSN; i++) if (RowOn(i)) n++; return n; }
    /// <summary>The per-frame eases.</summary>
    public static void Tick()
    {
        for (int i = 1; i <= SYSN; i++) T[i] += ((RowOn(i) ? 1.0 : 0.0) - T[i]) * Gfx.Ease.EK(0.2);
        U1 += ((DcAcct ? 1.0 : 0.0) - U1) * Gfx.Ease.EK(0.2); U2 += ((DcJoin ? 1.0 : 0.0) - U2) * Gfx.Ease.EK(0.2); U3 += ((DcFocus ? 1.0 : 0.0) - U3) * Gfx.Ease.EK(0.2);
        Q1 += ((FpsGpu ? 1.0 : 0.0) - Q1) * Gfx.Ease.EK(0.2); Q2 += ((FpsQuiet ? 1.0 : 0.0) - Q2) * Gfx.Ease.EK(0.2); Q3 += ((FpsSys ? 1.0 : 0.0) - Q3) * Gfx.Ease.EK(0.2);
        SysScr += (SysScrT - SysScr) * Gfx.Ease.EK(0.3); if (Math.Abs(SysScr - SysScrT) < 0.3) SysScr = SysScrT;
    }
    public static bool Animating
    {
        get
        {
            for (int i = 1; i <= SYSN; i++) { double want = RowOn(i) ? 1.0 : 0.0; if (Math.Abs(T[i] - want) > 0.004) return true; }
            long now = Clock.Tick;
            foreach (var at in FlashAt.Values) if (now - at < 440) return true;
            return Math.Abs(SysScr - SysScrT) > 0.3 || (MsgAt != 0 && now - MsgAt < 4300) || (Pulse != 0 && now - Pulse < 900);
        }
    }

    // ---- SPFToggle(i) ----
    public static void Toggle(int i)
    {
        switch (i)
        {
            case 1:
                if (!Discord && MultiBlock?.Invoke() == true) { Say("DISCORD ACTIVITY UNAVAILABLE WITH SEVERAL ROBLOX CLIENTS OPEN", AMBER); return; }
                Discord = !Discord;
                Dc = !Discord ? 0 : EngineSync is null ? 3 : 1;
                DcErr = Discord && EngineSync is null ? "engine arrives with the next build" : "";
                Say(Discord ? "DISCORD ACTIVITY ON" : "DISCORD ACTIVITY OFF", Discord ? C_ON : GREY);
                Save("discord", Discord); break;
            case 2:
                Region = !Region;
                if (!Region && !Match) { RegionText = ""; RegionCC = ""; RegionIP = ""; }
                Say(Region ? "SERVER REGION ON" : "SERVER REGION OFF", Region ? C_ON : GREY);
                Save("region", Region); break;
            case 3:
                Match = !Match; MmTries = 0; MmNote = "";
                Say(Match ? "AUTO-REGION FINDER ON - rerolls wrong-region servers" : "AUTO-REGION FINDER OFF", Match ? C_ON : GREY);
                Save("match", Match); break;
            case 4:
                Odds = !Odds;
                Say(Odds ? "BETTER MATCHMAKING ON - experimental, picks from servers it has measured" : "BETTER MATCHMAKING OFF", Odds ? AMBER : GREY);
                Save("odds", Odds); break;
            case 5:
                if (!Os.IsWin) { Say("MULTI-ROBLOX INSTANCES IS A WINDOWS FEATURE", AMBER); return; }
                if (EngineToggle is null) { Say("MULTI-ROBLOX INSTANCES ARRIVES WITH THE NEXT BUILD", AMBER); return; }
                Multi = !Multi; EngineToggle(5, Multi); Save("multi", Multi); break;
            case 6:
                if (!Os.IsWin) { Say("FPS BOOST IS A WINDOWS FEATURE", AMBER); return; }
                if (EngineToggle is null) { Say("FPS BOOST ARRIVES WITH THE NEXT BUILD", AMBER); return; }
                Fps = !Fps; EngineToggle(6, Fps); Save("fps", Fps); break;
            case 7:
                if (!Os.IsWin) { Say("THE CRASH HANDLER IS A WINDOWS PROCESS", AMBER); return; }
                if (EngineToggle is null) { Say("DISABLE CRASH HANDLER ARRIVES WITH THE NEXT BUILD", AMBER); return; }
                Crash = !Crash; EngineToggle(7, Crash); Save("crash", Crash); break;
            case 8:
                if (!Os.IsWin) { Say("THE MEMORY TRIMMER IS A WINDOWS FEATURE", AMBER); return; }
                if (EngineToggle is null) { Say("MEMORY TRIMMER ARRIVES WITH THE NEXT BUILD", AMBER); return; }
                Mem = !Mem; EngineToggle(8, Mem); Save("memtrim", Mem); break;
            case 9:
                Srv = !Srv;
                if (!Srv) Hist.Clear();
                Say(Srv ? "SERVER DETAILS ON - TYPE, UPTIME, ID AND A HISTORY OF EVERY SERVER YOU JOIN" : "SERVER DETAILS OFF - THE HISTORY IS CLEARED", Srv ? C_ON : GREY);
                Save("srv", Srv); break;
            case 10:
                NoApp = !NoApp;
                Say(NoApp ? "NO DESKTOP APP ON - LEAVING A GAME CLOSES THE CLIENT INSTEAD OF SHOWING THE HOME APP" : "NO DESKTOP APP OFF", NoApp ? C_ON : GREY);
                Save("noapp", NoApp); break;
            case 11:
                Clean = !Clean;
                if (Clean)
                {
                    Say("ROBLOX CLEANER ON - LOGS AND CACHE " + CleanAgeLabel().ToUpperInvariant() + " OLD GO AT EVERY LAUNCH  \u00B7  SWEEPING NOW", C_ON);
                    Task.Run(() => CleanRun(false));
                }
                else Say("ROBLOX CLEANER OFF", GREY);
                Save("clean", Clean); break;
            case 12:
            {
                Start = !Start;
                bool ok = StartupSet(Start);
                if (!ok) Start = false;
                Say(!ok ? "COULD NOT WRITE THE STARTUP ENTRY" : Start ? (Os.IsMac ? "YURI WILL START WHEN YOU LOG IN" : "YURI WILL START WITH WINDOWS") : (Os.IsMac ? "YURI NO LONGER STARTS AT LOGIN" : "YURI NO LONGER STARTS WITH WINDOWS"),
                    !ok ? C_ACC : Start ? C_ON : GREY);
                Save("startup", Start); break;
            }
            case 13:
                Theme = !Theme;
                if (Theme) ThemeApply(true); else Say("ROBLOX APP THEME OFF - ROBLOX KEEPS WHATEVER IT HAS NOW", GREY);
                Save("theme", Theme); break;
            case >= 14 and <= 17:
            {
                if (ModToggle is null) { Say("THE CONTENT MODS ARRIVE WITH THE NEXT BUILD", AMBER); return; }
                string kind = i == 14 ? "oldsnd" : i == 15 ? "avbg" : i == 16 ? "death" : "emoji";
                bool v = !RowOn(i);
                if (i == 14) Snd = v; else if (i == 15) AvBg = v; else if (i == 16) Death = v; else EmojiOn = v;
                ModToggle(i, v);
                Save(kind, v); break;
            }
        }
        FlashAt[i] = Clock.Tick;
        EngineSync?.Invoke();
    }

    // ---- SPFCleanRun: Roblox's logs and cache older than the age ----
    public static List<string> CleanDirs()
    {
        var res = new List<string>();
        if (Os.IsWin)
        {
            string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", tmp = Environment.GetEnvironmentVariable("TEMP") ?? "";
            if (lad != "") res.Add(Path.Combine(lad, "Roblox", "logs"));
            if (tmp != "") res.Add(Path.Combine(tmp, "Roblox"));
        }
        else if (Os.IsMac)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            res.Add(Path.Combine(home, "Library", "Logs", "Roblox"));
            res.Add(Path.Combine(home, "Library", "Caches", "com.Roblox.Roblox"));
        }
        return res;
    }
    public static string CleanAgeLabel()
    {
        int d = CleanAge;
        return d >= 60 ? "over 2 months" : d >= 30 ? "over 1 month" : d >= 7 ? "over 1 week" : "over " + d + " day" + (d == 1 ? "" : "s");
    }
    public static int CleanRun(bool say)
    {
        if (_cleanBusy) return 0;
        _cleanBusy = true;
        int n = 0; double freed = 0;
        var cut = DateTime.Now.AddDays(-Math.Max(1, CleanAge));
        foreach (var d in CleanDirs())
        {
            if (!Directory.Exists(d)) continue;
            int here = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
                {
                    if (here >= 200) break;
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.LastWriteTime >= cut) continue;
                        long sz = fi.Length;
                        fi.Delete();
                        n++; here++; freed += sz;
                    }
                    catch { }
                }
            }
            catch { }
        }
        _cleanBusy = false;
        CleanN = n; CleanMb = freed / 1048576.0; CleanAt = Clock.Tick;
        if (say) Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            Say(n != 0 ? $"CLEANED {n} FILE{(n == 1 ? "" : "S")}  \u00B7  {MemFmt(CleanMb)} FREED" : "NOTHING " + CleanAgeLabel().ToUpperInvariant() + " TO CLEAN", n != 0 ? C_ON : GREY));
        return n;
    }
    public static string CleanHintShort()
    {
        if (CleanAt == 0) return "runs at every launch";
        return (CleanN != 0 ? $"{CleanN} file{(CleanN == 1 ? "" : "s")}, {MemFmt(CleanMb)}" : "nothing to clean") + "  \u00B7  last run";
    }
    public static string MemFmt(double mb) => mb >= 1024 ? (mb / 1024).ToString("0.0") + " GB" : mb.ToString("0.0") + " MB";
    public static int Next(int cur, int[] tbl) { foreach (var v in tbl) if (v > cur) return v; return tbl[0]; }

    // ---- LAUNCH ON STARTUP ----
    static string StartupCmd()
    {
        string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "YURI");
        return "\"" + exe + "\"";
    }
    static string LaunchAgentPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", "com.yurihub.yuri.plist");
    public static bool StartupSet(bool on)
    {
        try
        {
            if (Os.IsWin)
            {
#pragma warning disable CA1416
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (key is null) return false;
                if (on) key.SetValue("YURI", StartupCmd()); else key.DeleteValue("YURI", false);
#pragma warning restore CA1416
                return true;
            }
            if (Os.IsMac)
            {
                if (!on) { if (File.Exists(LaunchAgentPath)) File.Delete(LaunchAgentPath); return true; }
                string exe = Environment.ProcessPath ?? "";
                // launch the bundle when there is one, so macOS treats it as the app
                var m = Regex.Match(exe, "^(.*\\.app)/Contents/MacOS/");
                string prog = m.Success ? "/usr/bin/open" : exe;
                string args = m.Success ? "<string>-a</string><string>" + System.Security.SecurityElement.Escape(m.Groups[1].Value) + "</string>" : "";
                Directory.CreateDirectory(Path.GetDirectoryName(LaunchAgentPath)!);
                File.WriteAllText(LaunchAgentPath,
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                    "<plist version=\"1.0\"><dict><key>Label</key><string>com.yurihub.yuri</string><key>ProgramArguments</key><array><string>" +
                    System.Security.SecurityElement.Escape(prog) + "</string>" + args + "</array><key>RunAtLoad</key><true/></dict></plist>\n");
                return true;
            }
        }
        catch { }
        return false;
    }
    public static bool StartupHas()
    {
        try
        {
            if (Os.IsWin)
            {
#pragma warning disable CA1416
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                return key?.GetValue("YURI") is string s && s != "";
#pragma warning restore CA1416
            }
            if (Os.IsMac) return File.Exists(LaunchAgentPath);
        }
        catch { }
        return false;
    }

    // ---- ROBLOX APP THEME: appStorage.json's DeviceLevelTheme ----
    public static string ThemeName() => ThemeDark ? "dark" : "light";
    static string AppStoragePath()
    {
        if (Os.IsWin) return Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", "Roblox", "appStorage.json");
        if (Os.IsMac) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Roblox", "appStorage.json");
        return "";
    }
    public static bool ThemeApply(bool say)
    {
        string p = AppStoragePath(), txt = "";
        try { if (p != "" && File.Exists(p)) txt = File.ReadAllText(p); } catch { }
        var m = Regex.Match(txt, "\"DeviceLevelTheme\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (txt == "" || !m.Success)
        {
            ThemeState = "no app storage yet";
            if (say) Say("ROBLOX HAS NOT WRITTEN ITS APP SETTINGS YET - OPEN THE APP ONCE, THEN TRY AGAIN", AMBER);
            return false;
        }
        string inner = m.Groups[1].Value, want = ThemeName();
        string nw = Regex.Replace(inner, "\\\\\"(light|dark)\\\\\"", "\\\"" + want + "\\\"", RegexOptions.IgnoreCase);
        if (nw == inner)
        {
            ThemeState = "already " + want;
            if (say) Say("ROBLOX APP ALREADY " + want.ToUpperInvariant(), C_ON);
            return true;
        }
        string outTxt = txt.Substring(0, m.Index) + "\"DeviceLevelTheme\":\"" + nw + "\"" + txt.Substring(m.Index + m.Length);
        bool ok = false;
        try { File.WriteAllText(p, outTxt, new System.Text.UTF8Encoding(false)); ok = true; } catch { }
        ThemeState = ok ? "set to " + want + " - takes effect at the next roblox start" : "could not write appStorage.json";
        if (say) Say(ok ? "ROBLOX APP THEME SET TO " + want.ToUpperInvariant() + "  \u00B7  TAKES EFFECT AT THE NEXT ROBLOX START" : "COULD NOT WRITE appStorage.json", ok ? C_ON : C_ACC);
        return ok;
    }

    /// <summary>SPFStatus(): the panel's header line.</summary>
    public static string Status()
    {
        if (!SaveOk) return "settings are not saving - check YURI\\config\\zeal.ini";
        if (!Discord && !Region && !Match && !Odds && !Multi) return "all features off";
        if (!InGame) return "waiting for a roblox game";
        if (RegionText != "") return RegionText;
        return Region ? "locating server..." : "in a game";
    }
    /// <summary>SPFRegionWhy(): what the REGION line of the LIVE card says.</summary>
    public static string RegionWhy()
    {
        if (!Region && !Match) return "off";
        if (!InGame) return "no game";
        if (RegionText != "") return RegionText;
        return "locating...";
    }
    public static string EmojiLabel() => Emoji[EmojiK - 1].n;
    public static string JoinUrl() => Job != "" && Place != "" ? "roblox://experiences/start?placeId=" + Place + "&gameInstanceId=" + Job : "";
}
