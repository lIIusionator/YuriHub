using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.FastFlags;

/// <summary>
/// The module's overlays. FFM.view names the one open ("db", "log", "comm");
/// each eases in over 280 ms from viewAt and the previous one eases out.
/// The FLAG DATABASE is the tracker's live name list (three sources,
/// fetched at launch and when the view asks), searched by the "db" field,
/// a click or Enter staging a name. The LOG view has three tabs: INJECTION
/// (what the injector did), UPDATES (what the UPDATE pass decided) and
/// HISTORY (the snapshots, each with RESTORE). The DETAIL sheet is the
/// explainer every row opens (FFMDetail here; other modules use the same
/// sheet).
/// </summary>
public static class FfmViews
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185;
    public const double FFM_TABMS = 260.0, FFM_INSMS = 620.0;
    const int FFM_LGRH = 20, FFM_LGROWS = 16, FFM_LGMAX = 2000, FFM_UPRH = 32, FFM_UPROWS = 10, FFM_UPMAX = 500, FFM_HSRH = 32, FFM_HSROWS = 10, FFM_TABW = 92;

    // ---- the view ----
    public static string View = "", ViewPrev = "";                      // FFM.view / viewPrev
    public static long ViewAt; public static double ViewT;             // FFM.viewAt / viewT
    public static bool Open => View != "";

    /// <summary>FFMOpenView(v).</summary>
    public static void OpenView(string v)
    {
        ViewPrev = View;
        View = v; ViewAt = Clock.Tick;
        if (v == "db")
        {
            Ffm.Q = ""; DbScrT = 0.0; _dbCacheQ = "\f";
            FfmField.Begin("db", 0);
        }
        else if (v == "log")
        {
            FfmField.End(false);
            LogScrT = 0.0; LogScr = 0.0; UpdScrT = 0.0; UpdScr = 0.0; HsScrT = 0.0; HsScr = 0.0;
        }
        else
        {
            FfmField.End(false);
            FfmCommView.OnOpen();
        }
        Poke();
    }
    /// <summary>FFMCloseView().</summary>
    public static void CloseView()
    {
        if (View == "") return;
        FfmField.End(false);
        ViewPrev = View;
        View = ""; ViewAt = Clock.Tick;
        Poke();
    }
    static void Poke() => HubSurface.Live?.Tim(Pace.TICK_A);

    /// <summary>The per-frame eases the views need.</summary>
    public static void Tick(HubLayout HL)
    {
        ViewT += ((View != "" ? 1.0 : 0.0) - ViewT) * EK(0.24);
        if (View == "" && ViewT < 0.004) ViewT = 0.0; else if (View != "" && ViewT > 0.996) ViewT = 1.0;
        DbScrT = Clamp(DbScrT, 0.0, FfmPanel.MaxScr(HL, DbList().Count, 11));
        DbScr += (DbScrT - DbScr) * EK(0.3); if (Math.Abs(DbScr - DbScrT) < 0.3) DbScr = DbScrT;
        LogScr += (LogScrT - LogScr) * EK(0.3); if (Math.Abs(LogScr - LogScrT) < 0.3) LogScr = LogScrT;
        UpdScr += (UpdScrT - UpdScr) * EK(0.3); if (Math.Abs(UpdScr - UpdScrT) < 0.3) UpdScr = UpdScrT;
        HsScr += (HsScrT - HsScr) * EK(0.3); if (Math.Abs(HsScr - HsScrT) < 0.3) HsScr = HsScrT;
        FfmCommView.Tick();
    }
    public static bool Animating => (ViewT > 0 && ViewT < 1) || (ViewAt != 0 && Clock.Tick - ViewAt < 320) || Math.Abs(DbScr - DbScrT) > 0.3
        || Math.Abs(LogScr - LogScrT) > 0.3 || Math.Abs(UpdScr - UpdScrT) > 0.3 || Math.Abs(HsScr - HsScrT) > 0.3 || DbIns.Count > 0
        || (LogTabAt != 0 && Clock.Tick - LogTabAt < FFM_TABMS + 20) || FfmCommView.Animating;

    /// <summary>HubViewChrome: the overlay's plate, its corner arcs and the spark running its edge.</summary>
    public static void HubViewChrome(double x, double y, double w, double h, double fo, long now, uint acc)
    {
        FillRR(x, y, w, h, 12, VBrush(x, y, w, h, FA(0xFF171A30, fo), FA(0xFF0D0F1B, fo)));
        int cl = PushG();
        ClipRR(x, y, w, h, 12);
        PanelBackdrop(x, y, w, h, acc, fo, now, 0.92);
        Pop(cl);
        StrokeRR(x, y, w, h, 12, Pen(FA(Alpha(acc, 150), fo), 1.3));
        if (HubState.LowPerf) return;
        for (int k = 0; k < 4; k++)
        {
            double sh2 = (Math.Sin(DecT(now) * 0.0032 + k * 1.5708) + 1) / 2;
            double cxk = (k == 1 || k == 2) ? x + w - 24 : x;
            double cyk = k >= 2 ? y + h - 24 : y;
            double ang = k == 0 ? 195 : k == 1 ? 285 : k == 2 ? 15 : 105;
            Arc(cxk, cyk, 24, 24, ang, 60, Pen(FA(Alpha(acc, R(45 + 45 * sh2)), fo), 1.5));
        }
        RectPerim((DecT(now) * 0.00035) % 1.0, x, y, w, h, out double glx, out double gly);
        FillEll(glx - 7, gly - 7, 14, 14, SBrush(FA(Alpha(acc, 60), fo)));
        FillEll(glx - 2.2, gly - 2.2, 4.4, 4.4, SBrush(FA(Alpha(AccHi(acc, 0.6), 210), fo)));
    }
    public static void RectPerim(double t, double x0, double y0, double w, double h, out double px, out double py)
    {
        double d = (((t % 1.0) + 1.0) % 1.0) * 2 * (w + h);
        if (d < w) { px = x0 + d; py = y0; }
        else if (d < w + h) { px = x0 + w; py = y0 + (d - w); }
        else if (d < 2 * w + h) { px = x0 + w - (d - w - h); py = y0 + h; }
        else { px = x0; py = y0 + h - (d - 2 * w - h); }
    }

    // =====================================================================
    //  the FLAG DATABASE
    // =====================================================================
    public static readonly List<string> Db = new();                    // FFM.db
    public static bool Loading;                                        // FFM.loading
    public static double DbScr, DbScrT;
    public static readonly Dictionary<string, long> DbIns = new(StringComparer.OrdinalIgnoreCase);   // FFM.dbIns: name -> tick it was taken
    static string _dbCacheQ = "\f"; static List<int> _dbCache = new();
    static readonly string[] DbSources =
    {
        "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/main/PCDesktopClient.json",
        "https://raw.githubusercontent.com/MaximumADHD/Roblox-FFlag-Tracker/main/PCClientBootstrapper.json",
        "https://raw.githubusercontent.com/MaximumADHD/Roblox-Client-Tracker/roblox/FVariables.txt",
    };
    static readonly Regex JsonKey = new("[{,]\\s*\"([A-Za-z0-9_]+)\"\\s*:", RegexOptions.Compiled);
    /// <summary>The prefixes Roblox reads. F / DF / SF for local, dynamic and synchronised, then the type.</summary>
    static readonly string[] Prefixes =
    {
        "FFlag", "DFFlag", "SFFlag", "FInt", "DFInt", "SFInt",
        "FString", "DFString", "SFString", "FLog", "DFLog", "SFLog",
    };
    public static bool Typed(string nm)
    {
        foreach (var p in Prefixes) if (nm.Length > p.Length && nm.StartsWith(p, StringComparison.Ordinal)) return true;
        return false;
    }
    static readonly Regex FVar = new("^\\[[^\\]]+\\]\\s+([A-Za-z0-9_]+)", RegexOptions.Compiled | RegexOptions.Multiline);
    static Task? _fetch;

    /// <summary>FFMFetchDb + the ffdb child + FFMDbLoaded: the three sources, the names deduplicated and sorted.</summary>
    public static Task FetchDb()
    {
        if (_fetch is { IsCompleted: false }) return _fetch;
        Loading = true;
        _fetch = Task.Run(async () =>
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var names = new List<string>();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Velostrap");
            foreach (var src in DbSources)
            {
                string body;
                try
                {
                    using var res = await http.GetAsync(src);
                    if (!res.IsSuccessStatusCode) continue;
                    body = await res.Content.ReadAsStringAsync();
                }
                catch { continue; }
                if (body == "") continue;
                var rx = src.Contains("FVariables") ? FVar : JsonKey;
                foreach (Match m in rx.Matches(body))
                {
                    string nm = m.Groups[1].Value;
                    // A flag IS its prefix - it is what tells the client the type
                    // and the scope. About a hundred keys in the JSON dumps carry
                    // none, and staging one of those produces a name the client
                    // will never read, sitting in the list looking like a flag.
                    if (!Typed(nm)) continue;
                    if (seen.Add(nm)) names.Add(nm);
                }
            }
            names.Sort(StringComparer.Ordinal);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Loading = false;
                if (names.Count == 0) { Ffm.Say("DATABASE UNAVAILABLE - CHECK CONNECTION", C_ACC); return; }
                Db.Clear(); Db.AddRange(names);
                _dbCacheQ = "\f";
                Ffm.Say("DATABASE READY - " + Db.Count, C_ON);
            });
        });
        return _fetch;
    }

    /// <summary>FFMDbList(): the names matching the search, cached per query.</summary>
    public static List<int> DbList()
    {
        string q = Ffm.Q.Trim();
        if (_dbCacheQ == q) return _dbCache;
        var res = new List<int>();
        for (int i = 0; i < Db.Count; i++)
            if (q == "" || Db[i].Contains(q, StringComparison.OrdinalIgnoreCase)) res.Add(i);
        _dbCache = res; _dbCacheQ = q;
        return res;
    }
    public static void DbSync() { DbScrT = 0.0; _dbCacheQ = "\f"; }

    /// <summary>Stage a database row: FFMAdd with the default, the insert pulse, the message.</summary>
    public static void DbStage(string nm)
    {
        int r = Ffm.Add(nm, "");
        DbIns[nm] = Clock.Tick;
        if (r != 0) Ffm.Say(r == 2 ? "ALREADY STAGED" : Ffm.StagedMsg(r, nm), r == 2 ? AMBER : C_ON);
    }
    /// <summary>Enter in the "db" field: the first match.</summary>
    public static void DbEnter()
    {
        var lst = DbList();
        if (lst.Count > 0) { DbStage(Db[lst[0]]); return; }
        // Nothing matched. The client does not care whether this list has heard
        // of the flag, so a properly-prefixed name is staged as typed - which is
        // the only way to reach a flag newer than the tracker.
        string q = Ffm.Q.Trim();
        if (q == "") return;
        if (!Typed(q)) { Ffm.Say("\"" + q + "\" HAS NO FLAG PREFIX - FFlag, DFInt, FString AND SO ON", AMBER); return; }
        DbStage(q);
    }

    public static double InsT(Dictionary<string, long> ins, string key, long now)
    {
        if (!ins.TryGetValue(key, out var at)) return 1.0;
        double t = (now - at) / FFM_INSMS;
        if (t >= 1) { ins.Remove(key); return 1.0; }
        return t;
    }
    public static void InsPulse(double x, double y, double w, double h, double r, double t, double ff)
    {
        if (t >= 1 || t < 0) return;
        double pp = 1 - t;
        FillRR(x, y, w, h, r, SBrush(FA(Alpha(C_ON, R(85 * pp)), ff)));
        FillRR(x + 6, y + 4, 2.5, h - 8, 1.2, SBrush(FA(Alpha(C_ON, R(215 * pp)), ff)));
        if (HubState.LowPerf) return;
        double ex = Ease3(t) * 15;
        StrokeRR(x - ex, y - ex * 0.55, w + ex * 2, h + ex * 1.1, r + ex * 0.35, Pen(FA(Alpha(C_ON, R(200 * pp)), ff), 1.6 * pp + 0.2));
    }

    /// <summary>FFMDbView.</summary>
    public static void DbView(HubSurface hub, double ff, long now, uint acc, double dx, double dy2)
    {
        var HL = hub.HL;
        double e = Ease3(Clamp((now - ViewAt) / 280.0, 0.0, 1.0));
        double t = View == "db" ? e : ViewPrev == "db" ? 1 - e : 0;
        if (t <= 0.01) return;
        double fo = ff * t;
        double x = HL.ctx + dx, y = HL.cty + dy2 + 30, w = HL.ctw;
        double lh = 11 * HL.ffrh;
        double h = 74 + lh + 42;
        FillRR(x - 10, HL.cty + dy2 - 6, w + 20, h + 54, 14, SBrush(FA(Alpha(0x06070E, R(215 * t)), ff)));
        int st = PushXform(x + w / 2, y + h / 2, 0.96 + 0.04 * t, 0);
        HubViewChrome(x, y, w, h, fo, now, acc);
        FillRR(x, y + 10, 3, 22, 1.5, SBrush(FA(Alpha(acc, 200), fo)));
        Txt("FLAG DATABASE", x + 16, y + 10, 140, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 235), fo), Fmt.L);
        var lst = DbList();
        Txt($"{lst.Count} of {Db.Count}", x + 162, y + 12, 200, 16, HL.fS, FA(0x66C7CBE0, fo), Fmt.L);
        double hvX = hub.Hv(430);
        var pnX = Pen(FA(Alpha(hvX > 0.2 ? acc : 0xFFC7CBE0, 130 + 120 * hvX), fo), 1.6);
        double cxx = x + w - 18, cyy = y + 14;
        Line(cxx - 5, cyy - 5, cxx + 5, cyy + 5, pnX); Line(cxx + 5, cyy - 5, cxx - 5, cyy + 5, pnX);
        double sy = y + 38;
        bool dbEd = FfmField.Edit == "db"; double dbHv = hub.Hv(437);
        FillRR(x + 14, sy, w - 28, 26, 7, SBrush(FA(0xFF0C0E17, fo)));
        MicroBackdrop(x + 14, sy, w - 28, 26, 7, acc, fo, now, 0.75);
        StrokeRR(x + 14, sy, w - 28, 26, 7, Pen(FA(dbEd ? Alpha(acc, 170) : Alpha(0xFFFFFF, 26 + 30 * dbHv), fo), dbEd ? 1.3 : 1.2));
        var pnS = Pen(FA(Alpha(dbEd ? acc : 0xFF9AA8C0, dbEd ? 190 : 115 + 60 * dbHv), fo), 1.4);
        Ell(x + 24, sy + 7, 11, 11, pnS); Line(x + 34, sy + 17, x + 38, sy + 21, pnS);
        double qMax = w - 76;
        // While it is being edited the field's own buffer is the truth. Drawing
        // the module's copy meant anything that changed the buffer without the
        // write-back landing - a cut, most visibly - left the old text on screen
        // over a filter that had already moved on.
        string qSrc = dbEd ? FfmField.Buf : Ffm.Q;
        string qVis = qSrc;
        while (Fonts.MeasureW(qVis, Fonts.fHint) > qMax && qVis.Length > 1) qVis = qVis[1..];
        int qOff = qSrc.Length - qVis.Length;
        if (qSrc == "") Txt("type to filter  \u00B7  ENTER stages the first match", x + 46, sy, qMax, 26, Fonts.fHint, FA(0x5EC7CBE0, fo), Fmt.L);
        if (dbEd) FfmField.Paint(x + 46, sy, 26, qVis, qOff, acc, fo, now);
        if (qSrc != "") Txt(qVis, x + 46, sy, qMax, 26, Fonts.fHint, FA(Alpha(0xFFE8EAF6, 235), fo), Fmt.L);
        double ly = y + 74;
        int lc = PushG();
        ClipRR(x + 10, ly, w - 20, lh, 8);
        int first = (int)Math.Floor(DbScr / HL.ffrh) + 1;
        for (int k = 1; k <= 12; k++)
        {
            int idx = first + k - 1;
            if (idx < 1 || idx > lst.Count) continue;
            string nm = Db[lst[idx - 1]];
            double ry = ly + (idx - 1) * HL.ffrh - DbScr;
            double hv = hub.Hv(600 + k);
            bool staged = Ffm.Staged(nm);
            if (hv > 0.01)
            {
                FillRR(x + 14, ry, w - 46, HL.ffrh - 2, 6, HBrush(x + 14, ry, w - 46, HL.ffrh - 2, FA(Alpha(0xFFFFFF, 17 * hv), fo), FA(Alpha(0xFFFFFF, 3 * hv), fo)));
                FillRR(x + 20, ry + 6, 2.5, HL.ffrh - 14, 1.2, SBrush(FA(Alpha(acc, 200 * hv), fo)));
            }
            InsPulse(x + 14, ry, w - 46, HL.ffrh - 2, 6, InsT(DbIns, nm, now), fo);
            string pfx = Ffm.PfxOf(nm);
            string bare = pfx != "" ? Ffm.BareOf(nm) : nm;
            double nx = x + 32;
            if (pfx != "")
            {
                Txt(pfx, nx, ry, 60, HL.ffrh - 2, HL.fM, FA(Alpha(acc, staged ? 90 : 150), fo), Fmt.L);
                nx += Fonts.MeasureW(pfx, HL.fM) + 1;
            }
            Txt(FFMElide(bare, HL.fM, (x + w - 118) - nx), nx, ry, (x + w - 118) - nx, HL.ffrh - 2, HL.fM, FA(Alpha(staged ? 0xFF6E7590 : 0xFFE8EAF6, 200 + 45 * hv), fo), Fmt.L);
            if (staged) Txt("staged", x + w - 108, ry, 62, HL.ffrh - 2, HL.fS, FA(Alpha(C_ON, 190), fo), Fmt.R);
        }
        Pop(lc);
        double tot = lst.Count * HL.ffrh;
        if (tot > lh)
        {
            double sbA = Math.Max(hub.Hv(435), HL.drag == 5 ? 1.0 : 0.0);
            double sw = 3 + 4 * sbA;
            double sx2 = x + w - 16 - 3 * sbA;
            FillRR(sx2, ly + 2, sw, lh - 4, sw / 2, SBrush(FA(Alpha(0xFFFFFF, 20 + 28 * sbA), fo)));
            double thmb = Math.Max(22, (lh - 4) * (lh / tot));
            double ty = ly + 2 + (lh - 4 - thmb) * (DbScr / Math.Max(1, tot - lh));
            FillRR(sx2, ty, sw, thmb, sw / 2, VBrush(sx2, ty, sw, thmb, FA(Alpha(AccHi(acc, 0.35), 195 + 60 * sbA), fo), FA(Alpha(acc, 175 + 60 * sbA), fo)));
        }
        if (Loading)
        {
            var pnL = Pen(FA(Alpha(acc, 200), fo), 1.6);
            PenDash(pnL, 1); PenDashOff(pnL, (DecT(now) * 0.06) % 1000);
            Ell(x + w / 2 - 7, ly + lh / 2 - 18, 14, 14, pnL);
            Txt("loading database", x, ly + lh / 2 + 2, w, 16, HL.fS, FA(0x60C7CBE0, fo), Fmt.C);
        }
        else if (Db.Count == 0)
            Txt("database unavailable - check the connection and press DATABASE again", x, ly + lh / 2 - 8, w, 16, Fonts.fHint, FA(0x74C7CBE0, fo), Fmt.C);
        else if (DbList().Count == 0)
        {
            // An empty list read exactly like a database that had failed to
            // load, so a name the tracker does not carry looked like a broken
            // hub. The tracker is a public dump of what Roblox has shipped: it
            // lags new flags by days and never had the ones that were pulled.
            // Typing the name stages it regardless - the client does not care
            // whether this list has heard of it.
            Txt("no flag here matches \u201C" + FFMElide(Ffm.Q.Trim(), Fonts.fHint, w - 120) + "\u201D",
                x, ly + lh / 2 - 20, w, 16, Fonts.fHint, FA(0xA8C7CBE0, fo), Fmt.C);
            Txt(Db.Count + " flags listed  \u00B7  the tracker lags new ones by a few days",
                x, ly + lh / 2, w, 15, HL.fXs, FA(0x6EC7CBE0, fo), Fmt.C);
            Txt("press ENTER to stage it anyway", x, ly + lh / 2 + 18, w, 15, HL.fXs, FA(Alpha(AccHi(acc, 0.35), 190), fo), Fmt.C);
        }
        FadeLine(x + 14, x + w - 14, ly + lh + 6, 0x1CFFFFFF, fo);
        FFMBtn(431, x + 14, ly + lh + 14, 96, 24, "CLOSE", acc, fo, 0);
        Txt("click a row to stage  \u00B7  ESC closes", x + 120, ly + lh + 14, w - 140, 24, Fonts.fHint, FA(0x5AC7CBE0, fo), Fmt.L);
        Pop(st);
    }

    /// <summary>FFMDbZone.</summary>
    public static int DbZone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x = HL.ctx, y = HL.cty + 30, w = HL.ctw;
        if (ux >= x + w - 32 && ux <= x + w && uy >= y && uy <= y + 28) return 430;
        if (ux >= x + 14 && ux <= x + w - 14 && uy >= y + 38 && uy <= y + 64) return 437;
        double ly = y + 74;
        if (DbList().Count * HL.ffrh > 11 * HL.ffrh && ux >= x + w - 26 && ux <= x + w - 6 && uy >= ly && uy <= ly + 11 * HL.ffrh) return 435;
        if (uy >= ly && uy <= ly + 11 * HL.ffrh && ux >= x + 14 && ux <= x + w - 32)
        {
            int vis = (int)Math.Floor((uy - ly) / HL.ffrh) + 1;
            if (vis >= 1 && vis <= 11) return 600 + vis;
        }
        if (uy >= ly + 11 * HL.ffrh + 14 && uy <= ly + 11 * HL.ffrh + 38 && ux >= x + 14 && ux <= x + 110) return 431;
        double h = 74 + 11 * HL.ffrh + 42;
        if (ux >= x && ux <= x + w && uy >= y && uy <= y + h) return 444;
        return 0;
    }

    // =====================================================================
    //  the LOG view
    // =====================================================================
    public sealed record LogEntry(string T, string Method, string Flag, string Status);
    public sealed record UpdEntry(string T, string Old, string New, string Kind, double Score, string Note);
    public static readonly List<LogEntry> Logs = new();               // FFM.logs, newest first
    public static readonly List<UpdEntry> Upd = new();                // FFM.upd
    public static int LogTab = 1, LogTabPrev = 1; public static long LogTabAt;
    public static double LogScr, LogScrT, UpdScr, UpdScrT, HsScr, HsScrT;
    public static bool CanUndo => LogTab == 2 && FfmUpdate.CanUndo;  // FFMCanUndo

    /// <summary>FFMLog(method, flag, status).</summary>
    public static void Log(string method, string flag, string status)
    {
        Logs.Insert(0, new LogEntry(DateTime.Now.ToString("HH:mm:ss"), method, flag, status));
        if (Logs.Count > FFM_LGMAX) Logs.RemoveAt(Logs.Count - 1);
    }
    public static void UpdLog(string old, string nw, string kind, double score = 0.0, string note = "")
    {
        Upd.Insert(0, new UpdEntry(DateTime.Now.ToString("HH:mm:ss"), old, nw, kind, score, note));
        if (Upd.Count > FFM_UPMAX) Upd.RemoveAt(Upd.Count - 1);
    }
    static double LogTot() => LogTab == 2 ? Upd.Count * FFM_UPRH : LogTab == 3 ? Ffm.Hist.Count * FFM_HSRH : Logs.Count * FFM_LGRH;
    public static void LogDragScroll(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        double lh = FFM_LGROWS * FFM_LGRH, tot = LogTot(), ly = HL.cty + 30 + 38;
        double thmb = Math.Max(22, (lh - 4) * (lh / Math.Max(1, tot)));
        if (LogTab == 2) { UpdScrT = HubUI.ScrollFromY(HL, uy, ly + 2, lh - 4, tot, lh, thmb, UpdScr); UpdScr = UpdScrT; }
        else if (LogTab == 3) { HsScrT = HubUI.ScrollFromY(HL, uy, ly + 2, lh - 4, tot, lh, thmb, HsScr); HsScr = HsScrT; }
        else { LogScrT = HubUI.ScrollFromY(HL, uy, ly + 2, lh - 4, tot, lh, thmb, LogScr); LogScr = LogScrT; }
    }
    public static void LogTabSet(int nt)
    {
        if (nt == LogTab) return;
        long now = Clock.Tick;
        if (!(LogTabAt != 0 && now - LogTabAt < FFM_TABMS && Ease3((now - LogTabAt) / FFM_TABMS) < 0.5)) LogTabPrev = LogTab;
        LogTab = nt; LogTabAt = now;
    }

    /// <summary>FFMLogView.</summary>
    public static void LogView(HubSurface hub, double ff, long now, uint acc, double dx, double dy2)
    {
        var HL = hub.HL;
        double e = Ease3(Clamp((now - ViewAt) / 280.0, 0.0, 1.0));
        double t = View == "log" ? e : ViewPrev == "log" ? 1 - e : 0;
        if (t <= 0.01) return;
        double fo = ff * t;
        double x = HL.ctx + dx, y = HL.cty + dy2 + 30, w = HL.ctw;
        double lh = FFM_LGROWS * FFM_LGRH;
        double h = 38 + lh + 48;
        bool upTab = LogTab == 2;
        FillRR(x - 10, HL.cty + dy2 - 6, w + 20, h + 54, 14, SBrush(FA(Alpha(0x06070E, R(215 * t)), ff)));
        int st = PushXform(x + w / 2, y + h / 2, 0.96 + 0.04 * t, 0);
        HubViewChrome(x, y, w, h, fo, now, acc);
        double lt = (LogTabAt != 0 && now - LogTabAt < FFM_TABMS) ? Ease3((now - LogTabAt) / FFM_TABMS) : 1.0;
        bool swp = lt < 1 && LogTabPrev != LogTab;
        double uTo = LogTab - 1.0;
        double u = swp ? Lerp(LogTabPrev - 1.0, uTo, lt) : uTo;
        double t1x = x + 14, t2x = x + 18 + FFM_TABW, t3x = x + 22 + FFM_TABW * 2;
        LogTabChip(hub, 475, t1x, y + 7, "INJECTION", Clamp(1 - Math.Abs(u - 0), 0.0, 1.0), acc, fo);
        LogTabChip(hub, 476, t2x, y + 7, "UPDATES", Clamp(1 - Math.Abs(u - 1), 0.0, 1.0), acc, fo);
        LogTabChip(hub, 478, t3x, y + 7, "HISTORY", Clamp(1 - Math.Abs(u - 2), 0.0, 1.0), acc, fo);
        double ubx = t1x + u * (FFM_TABW + 4) + 8;
        double ubw = (FFM_TABW - 16) * (1 + 0.35 * Math.Sin(3.14159 * (u - Math.Floor(u))));
        FillRR(ubx - (ubw - (FFM_TABW - 16)) / 2, y + 26, ubw, 2, 1, SBrush(FA(Alpha(acc, 210), fo)));
        int cnt = upTab ? Upd.Count : LogTab == 3 ? Ffm.Hist.Count : Logs.Count;
        Txt(cnt + " entries", x + 30 + FFM_TABW * 3, y + 12, 120, 16, HL.fS, FA(0x66C7CBE0, fo), Fmt.L);
        double hvX = hub.Hv(440);
        var pnX = Pen(FA(Alpha(hvX > 0.2 ? acc : 0xFFC7CBE0, 130 + 120 * hvX), fo), 1.6);
        double cxx = x + w - 18, cyy = y + 14;
        Line(cxx - 5, cyy - 5, cxx + 5, cyy + 5, pnX); Line(cxx + 5, cyy - 5, cxx - 5, cyy + 5, pnX);
        double ly = y + 38;
        int lc = PushG();
        ClipRR(x + 10, ly, w - 20, lh, 8);
        if (swp)
        {
            int dir = LogTab > LogTabPrev ? 1 : -1;
            double oq = 1 - lt;
            if (oq > 0.01) LogDrawTab(hub, LogTabPrev, x - dir * 34 * lt, w, ly, lh, t, fo * oq, acc);
            LogDrawTab(hub, LogTab, x + dir * 34 * (1 - lt), w, ly, lh, t, fo * lt, acc);
        }
        else LogDrawTab(hub, LogTab, x, w, ly, lh, t, fo, acc);
        Pop(lc);
        double tot = LogTot();
        if (tot > lh)
        {
            double scr = upTab ? UpdScr : LogTab == 3 ? HsScr : LogScr;
            double sbA = Math.Max(hub.Hv(441), HL.drag == 11 ? 1.0 : 0.0);
            double sw = 3 + 4 * sbA;
            double sx2 = x + w - 16 - 3 * sbA;
            FillRR(sx2, ly + 2, sw, lh - 4, sw / 2, SBrush(FA(Alpha(0xFFFFFF, 20 + 28 * sbA), fo)));
            double thmb = Math.Max(22, (lh - 4) * (lh / tot));
            double ty = ly + 2 + (lh - 4 - thmb) * (scr / Math.Max(1, tot - lh));
            FillRR(sx2, ty, sw, thmb, sw / 2, VBrush(sx2, ty, sw, thmb, FA(Alpha(AccHi(acc, 0.35), 195 + 60 * sbA), fo), FA(Alpha(acc, 175 + 60 * sbA), fo)));
        }
        FadeLine(x + 14, x + w - 14, ly + lh + 6, 0x1CFFFFFF, fo);
        FFMBtn(440, x + 14, ly + lh + 14, 74, 24, "CLOSE", acc, fo, 0);
        FFMBtn(439, x + 100, ly + lh + 14, 74, 24, "CLEAR", acc, fo, 3);
        double ftx = x + 190, ftw = w - 210;
        if (CanUndo) { FFMBtn(477, x + 186, ly + lh + 14, 74, 24, "UNDO", acc, fo, 2); ftx = x + 276; ftw = w - 296; }
        Txt(LogFoot(), ftx, ly + lh + 16, ftw, 20, Fonts.fHint, FA(0x5AC7CBE0, fo), Fmt.L);
        Pop(st);
    }

    static string LogFoot()
    {
        if (LogTab == 3) return Ffm.Hist.Count == 0 ? "no snapshots" : $"{Ffm.Hist.Count} snapshot{(Ffm.Hist.Count == 1 ? "" : "s")}  -  newest first, {Ffm.HSMAX} kept";
        if (LogTab != 2) return Logs.Count == 0 ? "no entries" : Logs.Count + " entries";
        if (Upd.Count == 0) return "no update yet - press UPDATE to check these names against the live list";
        int rev = 0, ren = 0, skip = 0;
        foreach (var u in Upd) { if (u.Kind == "REVIEW") rev++; else if (u.Kind == "SKIPPED") skip++; else ren++; }
        return $"{ren} changed  -  {rev} need review  -  {skip} left alone";
    }

    static void LogTabChip(HubSurface hub, int z, double bx, double by, string label, double sel, uint acc, double ff)
    {
        double hv = hub.Hv(z);
        double a = Lerp(0.35 + 0.35 * hv, 1.0, sel);
        FillRR(bx, by, FFM_TABW, 22, 6, SBrush(FA(Alpha(Mix(0xFF8A90A6, acc, sel), R(Lerp(16 + 14 * hv, 34, sel))), ff)));
        Txt(label, bx, by + 4, FFM_TABW, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFC7CBE0, 0xFFE8EAF6, sel), R(255 * a)), ff), Fmt.C);
    }

    static void LogDrawTab(HubSurface hub, int tab, double x, double w, double ly, double lh, double t, double fo, uint acc)
    {
        if (tab == 2) LogDrawUpd(hub, x, w, ly, lh, t, fo, acc);
        else if (tab == 3) LogDrawHist(hub, x, w, ly, lh, t, fo, acc);
        else LogDrawInj(hub, x, w, ly, lh, t, fo, acc);
    }

    static void LogDrawInj(HubSurface hub, double x, double w, double ly, double lh, double t, double fo, uint acc)
    {
        var HL = hub.HL;
        const uint COL_INJ = 0xFF38BDF8, COL_REA = 0xFFA78BFA;
        double rowR = x + w - 32;
        double stW = Clamp((w - 190) * 0.46, 130, 300);
        double stX = rowR - stW;
        double flX = x + 142;
        double flW = Math.Max(60, stX - flX - 12);
        int first = (int)Math.Floor(LogScr / FFM_LGRH) + 1;
        int visCount = Math.Max(0, Math.Min(FFM_LGROWS + 1, Logs.Count - first + 1));
        for (int k = 1; k <= visCount; k++)
        {
            int idx = first + k - 1;
            if (idx < 1 || idx > Logs.Count) continue;
            var entry = Logs[idx - 1];
            double ry = ly + (idx - 1) * FFM_LGRH - LogScr;
            double rowF = fo * Clamp((t - (k - 1) * 0.04) / 0.6, 0.0, 1.0);
            if (rowF < 0.01) continue;
            uint mc = entry.Method.Contains("Re") ? COL_REA : entry.Status.Contains("FAIL") ? acc : COL_INJ;
            FillRR(x + 14, ry + 1, w - 46, FFM_LGRH - 3, 4, SBrush(FA(Alpha(mc, R(38 * rowF)), fo)));
            FillRR(x + 14, ry + 3, 2.5, FFM_LGRH - 7, 1.2, SBrush(FA(Alpha(mc, R(200 * rowF)), fo)));
            Txt(entry.T, x + 22, ry, 52, FFM_LGRH - 2, HL.fS, FA(Alpha(0xFFC7CBE0, R(160 * rowF)), fo), Fmt.L);
            Txt(entry.Method, x + 78, ry, 58, FFM_LGRH - 2, HL.fS, FA(Alpha(mc, R(200 * rowF)), fo), Fmt.L);
            Txt(FFMElide(entry.Flag, HL.fM, flW), flX, ry, flW, FFM_LGRH - 2, HL.fM, FA(Alpha(0xFFE8EAF6, R(200 * rowF)), fo), Fmt.L);
            uint sc = entry.Status.Contains("OK") ? C_ON : entry.Status.Contains("FAIL") ? acc : 0xFFC7CBE0;
            Txt(FFMElide(entry.Status, HL.fS, stW), stX, ry, stW, FFM_LGRH - 2, HL.fS, FA(Alpha(sc, R(190 * rowF)), fo), Fmt.R);
        }
        if (Logs.Count == 0)
        {
            Txt("Nothing yet. Every flag the injector writes, holds or refuses lands here,", x + 22, ly + lh / 2 - 22, w - 44, 18, HL.fS, FA(0x8AC7CBE0, fo), Fmt.C);
            Txt("with the method that touched it and how it went.", x + 22, ly + lh / 2 - 4, w - 44, 18, HL.fS, FA(0x66C7CBE0, fo), Fmt.C);
        }
    }

    static void LogDrawUpd(HubSurface hub, double x, double w, double ly, double lh, double t, double fo, uint acc)
    {
        var HL = hub.HL;
        if (Upd.Count == 0)
        {
            Txt("Nothing yet. UPDATE checks every staged flag against the live list", x + 22, ly + lh / 2 - 22, w - 44, 18, HL.fS, FA(0x8AC7CBE0, fo), Fmt.C);
            Txt("and records what it changed, what it left alone, and what it is unsure about.", x + 22, ly + lh / 2 - 4, w - 44, 18, HL.fS, FA(0x66C7CBE0, fo), Fmt.C);
            return;
        }
        double rowR = x + w - 32;
        double kX = x + 78, kW = 68;
        double nmX = x + 152;
        double nmW = Math.Max(80, rowR - nmX - 52);
        double scX = rowR - 46;
        int first = (int)Math.Floor(UpdScr / FFM_UPRH) + 1;
        int visCount = Math.Max(0, Math.Min(FFM_UPROWS + 1, Upd.Count - first + 1));
        for (int k = 1; k <= visCount; k++)
        {
            int idx = first + k - 1;
            if (idx < 1 || idx > Upd.Count) continue;
            var u = Upd[idx - 1];
            double ry = ly + (idx - 1) * FFM_UPRH - UpdScr;
            double rowF = fo * Clamp((t - (k - 1) * 0.04) / 0.6, 0.0, 1.0);
            if (rowF < 0.01) continue;
            uint mc = u.Kind == "REVIEW" ? AMBER : u.Kind == "SKIPPED" ? 0xFF8A90A6 : u.Kind == "PREFIX" ? 0xFF38BDF8 : C_ON;
            FillRR(x + 14, ry + 1, w - 46, FFM_UPRH - 3, 5, SBrush(FA(Alpha(mc, R((u.Kind == "REVIEW" ? 52 : 34) * rowF)), fo)));
            FillRR(x + 14, ry + 3, 2.5, FFM_UPRH - 7, 1.2, SBrush(FA(Alpha(mc, R(215 * rowF)), fo)));
            Txt(u.T, x + 22, ry + 1, 52, 14, HL.fS, FA(Alpha(0xFFC7CBE0, R(150 * rowF)), fo), Fmt.L);
            FillRR(kX, ry + 3, kW, 12, 3, SBrush(FA(Alpha(mc, R(60 * rowF)), fo)));
            Txt(u.Kind, kX, ry + 2, kW, 12, HL.fXs, FA(Alpha(mc, R(245 * rowF)), fo), Fmt.C);
            Txt(FFMElide(u.Old, HL.fMs, nmW + 46), nmX, ry + 1, nmW + 46, 14, HL.fMs, FA(Alpha(0xFFC7CBE0, R(150 * rowF)), fo), Fmt.L);
            if (u.New != "" && u.Kind != "SKIPPED")
            {
                Txt("\u2192", nmX - 14, ry + 15, 14, 15, HL.fS, FA(Alpha(mc, R(220 * rowF)), fo), Fmt.L);
                Txt(FFMElide(u.New, HL.fM, nmW), nmX, ry + 15, nmW, 15, HL.fM, FA(Alpha(0xFFE8EAF6, R(230 * rowF)), fo), Fmt.L);
                if (u.Score != 0) Txt(Math.Round(u.Score, 2).ToString("0.##"), scX, ry + 16, 42, 13, HL.fS, FA(Alpha(mc, R(190 * rowF)), fo), Fmt.R);
            }
            else Txt(FFMElide(u.Note != "" ? u.Note : "no match in the live list", HL.fS, nmW + 40), nmX, ry + 15, nmW + 40, 15, HL.fS, FA(Alpha(0x9AC7CBE0, R(230 * rowF)), fo), Fmt.L);
            if (u.Kind == "REVIEW" && u.Note != "") Txt(FFMElide(u.Note, HL.fXs, 220), scX - 224, ry + 3, 220, 12, HL.fXs, FA(Alpha(AMBER, R(190 * rowF)), fo), Fmt.R);
        }
    }

    static void LogDrawHist(HubSurface hub, double x, double w, double ly, double lh, double t, double fo, uint acc)
    {
        var HL = hub.HL;
        if (Ffm.Hist.Count == 0)
        {
            Txt("Nothing yet. A snapshot of the staged list is taken before CLEAR, an import,", x + 22, ly + lh / 2 - 22, w - 44, 18, HL.fS, FA(0x8AC7CBE0, fo), Fmt.C);
            Txt("a preset, a community set, PRUNE, UPDATE and a delete - and any one can be put back.", x + 22, ly + lh / 2 - 4, w - 44, 18, HL.fS, FA(0x66C7CBE0, fo), Fmt.C);
            return;
        }
        double bX = x + w - 116, bW = 74;
        double nmX = x + 150;
        double nmW = Math.Max(80, bX - 12 - nmX);
        int first = (int)Math.Floor(HsScr / FFM_HSRH) + 1;
        int visCount = Math.Max(0, Math.Min(FFM_HSROWS + 1, Ffm.Hist.Count - first + 1));
        for (int k = 1; k <= visCount; k++)
        {
            int idx = first + k - 1;
            if (idx < 1 || idx > Ffm.Hist.Count) continue;
            var sn = Ffm.Hist[idx - 1];
            double ry = ly + (idx - 1) * FFM_HSRH - HsScr;
            double rowF = fo * Clamp((t - (k - 1) * 0.04) / 0.6, 0.0, 1.0);
            if (rowF < 0.01) continue;
            double hv = hub.Hv(619 + k);
            uint mc = sn.Act == "restore" ? 0xFF38BDF8 : (sn.Act == "clear" || sn.Act == "delete") ? AMBER : C_ON;
            FillRR(x + 14, ry + 1, w - 46, FFM_HSRH - 3, 5, SBrush(FA(Alpha(mc, R((34 + 14 * hv) * rowF)), fo)));
            FillRR(x + 14, ry + 3, 2.5, FFM_HSRH - 7, 1.2, SBrush(FA(Alpha(mc, R(215 * rowF)), fo)));
            Txt(sn.T, x + 22, ry + 1, 52, 14, HL.fS, FA(Alpha(0xFFC7CBE0, R(150 * rowF)), fo), Fmt.L);
            FillRR(x + 78, ry + 3, 66, 12, 3, SBrush(FA(Alpha(mc, R(60 * rowF)), fo)));
            Txt("before " + sn.Act, x + 78, ry + 2, 66, 12, HL.fXs, FA(Alpha(mc, R(245 * rowF)), fo), Fmt.C);
            Txt($"{sn.Flags.Count} flag{(sn.Flags.Count == 1 ? "" : "s")}", nmX, ry + 1, nmW, 14, HL.fMs, FA(Alpha(0xFFE8EAF6, R(220 * rowF)), fo), Fmt.L);
            var names = new System.Text.StringBuilder();
            foreach (var fl in sn.Flags) { names.Append(names.Length > 0 ? ", " : "").Append(fl.Name); if (names.Length > 120) break; }
            Txt(FFMElide(names.ToString(), HL.fS, nmW), nmX, ry + 15, nmW, 15, HL.fS, FA(Alpha(0x9AC7CBE0, R(220 * rowF)), fo), Fmt.L);
            FFMBtn(619 + k, bX, ry + 4, bW, 24, "RESTORE", acc, rowF, 0);
        }
    }

    /// <summary>FFMLogZone.</summary>
    public static int LogZone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x = HL.ctx, y = HL.cty + 30, w = HL.ctw;
        double lh = FFM_LGROWS * FFM_LGRH, ly = y + 38;
        if (ux >= x + w - 32 && ux <= x + w && uy >= y && uy <= y + 28) return 440;
        if (uy >= y + 7 && uy <= y + 29)
        {
            if (ux >= x + 14 && ux <= x + 14 + FFM_TABW) return 475;
            if (ux >= x + 18 + FFM_TABW && ux <= x + 18 + FFM_TABW * 2) return 476;
            if (ux >= x + 22 + FFM_TABW * 2 && ux <= x + 22 + FFM_TABW * 3) return 478;
        }
        if (LogTab == 3 && uy >= ly && uy <= ly + lh && ux >= x + w - 116 && ux <= x + w - 42)
        {
            int k = (int)Math.Floor((uy - ly + HsScr) / FFM_HSRH) - (int)Math.Floor(HsScr / FFM_HSRH) + 1;
            if (k >= 1 && k <= FFM_HSROWS + 1) return 619 + k;
        }
        if (LogTot() > lh && ux >= x + w - 26 && ux <= x + w - 6 && uy >= ly && uy <= ly + lh) return 441;
        double footerY = ly + lh + 14;
        if (uy >= footerY && uy <= footerY + 24)
        {
            if (ux >= x + 14 && ux <= x + 88) return 440;
            if (ux >= x + 100 && ux <= x + 174) return 439;
            if (CanUndo && ux >= x + 186 && ux <= x + 260) return 477;
        }
        double h = 38 + lh + 48;
        if (ux >= x && ux <= x + w && uy >= y && uy <= y + h) return 443;
        return 0;
    }

    // ---- the views' clicks and wheel, routed from FfmPanel ----
    public static bool Click(HubSurface hub, int z)
    {
        if (View == "comm" && FfmCommView.Click(hub, z)) return true;
        switch (z)
        {
            case 430: case 431: case 440: case 446: CloseView(); return true;
            case 437:
                if (FfmField.Edit != "db") FfmField.Begin("db", 0);
                FfmField.Mouse(hub.PtrX); return true;
            case 439:
                if (LogTab == 2) { Upd.Clear(); UpdScr = 0; UpdScrT = 0; Ffm.Say("UPDATE RECORD CLEARED", 0); }
                else if (LogTab == 3) { Ffm.Hist.Clear(); HsScr = 0; HsScrT = 0; Ffm.HistSave(); Ffm.Say("HISTORY CLEARED", 0); }
                else { Logs.Clear(); LogScr = 0; LogScrT = 0; Ffm.Say("LOGS CLEARED", 0); }
                return true;
            case 475: LogTabSet(1); return true;
            case 476: LogTabSet(2); return true;
            case 478: LogTabSet(3); return true;
            case 477: FfmUpdate.UpdateUndo(); return true;
            case 443: case 444: return true;                                   // the overlay's body: inert
        }
        if (z >= 601 && z <= 611)
        {
            var lst = DbList();
            int idx = (int)Math.Floor(DbScr / hub.HL.ffrh) + (z - 600);
            if (idx >= 1 && idx <= lst.Count) DbStage(Db[lst[idx - 1]]);
            return true;
        }
        if (z >= 620 && z <= 619 + FFM_HSROWS + 1)
        {
            hub.ClickAt[z] = Clock.Tick;
            Ffm.HistRestore((int)Math.Floor(HsScr / FFM_HSRH) + (z - 620));
            return true;
        }
        return false;
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (View == "comm") return FfmCommView.Wheel(hub, ux, uy, delta);
        if (View == "db")
        {
            DbScrT = Clamp(DbScrT - delta * 3 * HL.ffrh, 0.0, FfmPanel.MaxScr(HL, DbList().Count, 11));
            return true;
        }
        if (View == "log")
        {
            double lh = FFM_LGROWS * FFM_LGRH;
            if (LogTab == 2) UpdScrT = Clamp(UpdScrT - delta * 3 * FFM_UPRH, 0.0, Math.Max(0.0, Upd.Count * FFM_UPRH - lh));
            else if (LogTab == 3) HsScrT = Clamp(HsScrT - delta * 3 * FFM_HSRH, 0.0, Math.Max(0.0, Ffm.Hist.Count * FFM_HSRH - lh));
            else LogScrT = Clamp(LogScrT - delta * 3 * FFM_LGRH, 0.0, Math.Max(0.0, Logs.Count * FFM_LGRH - lh));
            return true;
        }
        return false;
    }
}

/// <summary>
/// DET: the explainer sheet a system row opens — a title, a hint, a body
/// wrapped to the sheet, scrollable when long, a close button; the card
/// behind it dims. Zones 396 close, 399 the scrollbar, 397 the sheet
/// (inert), 398 outside (swallowed).
/// </summary>
public static class Detail
{
    public static bool On; public static long At, XAt; public static double T;
    public static string Title = "", Hint = "", Body = "", Tag = "";
    public static double Scr, ScrT, Max;
    static List<string> _lines = new(); static string _wrapFor = "\f";
    const double DET_LH = 18, DET_MAXH = 330;

    static List<string> Lines()
    {
        if (_wrapFor != Body) { _wrapFor = Body; _lines = Body == "" ? new() : WrapText(Body, Fonts.fHint, 430 - 56, 400); }
        return _lines;
    }
    /// <summary>WrapText(txt, font, maxw, maxLines): greedy word wrap; the last allowed line elided.</summary>
    public static List<string> WrapText(string txt, Font font, double maxw, int maxLines)
    {
        var res = new List<string>(); string cur = "";
        foreach (var word in txt.Trim().Split(' '))
        {
            if (word == "") continue;
            string try2 = cur == "" ? word : cur + " " + word;
            if (Fonts.MeasureW(try2, font) <= maxw) cur = try2;
            else
            {
                if (cur != "") res.Add(cur);
                cur = word;
                if (res.Count >= maxLines) break;
            }
        }
        if (cur != "" && res.Count < maxLines) res.Add(cur);
        if (res.Count > maxLines) res.RemoveRange(maxLines, res.Count - maxLines);
        if (res.Count == maxLines && Fonts.MeasureW(txt.Trim(), font) > maxw * maxLines) res[maxLines - 1] = FFMElide(res[maxLines - 1] + " ...", font, maxw);
        return res;
    }

    /// <summary>DetailOpen(title, hint, body, tag).</summary>
    public static void OpenSheet(string title, string hint, string body, string tag = "")
    {
        FfmField.End(false);
        On = true; At = Clock.Tick;
        Title = title; Hint = hint; Body = body; Tag = tag;
        Scr = 0; ScrT = 0; Max = 0;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Close()
    {
        if (!On) return;
        XAt = Clock.Tick;
        On = false; At = Clock.Tick;
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static bool Live => On || T > 0.004;
    /// <summary>The ease: quick in, quicker out; snapped in LOW PERFORMANCE MODE.</summary>
    public static void Tick()
    {
        T += ((On ? 1.0 : 0.0) - T) * (HubState.LowPerf ? 1.0 : (On ? 0.26 : 0.55));
        if (!On && T < 0.06) T = 0.0;
    }
    public static void Geom(out double dx, out double dy, out double dw, out double dh)
    {
        dw = 430;
        int n = Lines().Count;
        dh = Clamp(96 + 26 + n * DET_LH + 26, 150, DET_MAXH);
        dx = HubLayout.pd + HubLayout.cw / 2 - dw / 2;
        dy = HubLayout.pd + HubLayout.ch / 2 - dh / 2;
    }
    static double BodyTop(double dy) => dy + 102;
    static double BodyH(double dh) => dh - 102 - 16;
    static double ScrollMax(double dh) => Math.Max(0, Lines().Count * DET_LH - BodyH(dh));
    public static bool ScrollBy(double d)
    {
        Geom(out _, out _, out _, out double dh);
        double mx = ScrollMax(dh); Max = mx;
        if (mx <= 0) return false;
        ScrT = Clamp(ScrT + d, 0, mx);
        return true;
    }
    public static bool WheelNotches(double delta) => ScrollBy(-delta * 3 * DET_LH);
    public static void DragScroll(double uy)
    {
        Geom(out _, out double dy, out _, out double dh);
        double mx = ScrollMax(dh);
        if (mx <= 0) return;
        double by = BodyTop(dy), bh = BodyH(dh);
        double tot = Lines().Count * DET_LH;
        double th = Math.Max(24, bh * bh / tot);
        double trav = bh - th;
        if (trav <= 0) return;
        ScrT = HubUI.ScrollFromY(HubSurface.Live!.HL, uy, by, bh, tot, bh, th, Scr);
        Scr = ScrT;
    }
    public static int Zone(double ux, double uy)
    {
        if (!Live) return 0;
        Geom(out double dx, out double dy, out double dw, out double dh);
        if ((ux - (dx + dw - 30)) * (ux - (dx + dw - 30)) + (uy - (dy + 28)) * (uy - (dy + 28)) <= 324) return 396;
        if (ScrollMax(dh) > 0 && ux >= dx + dw - 20 && ux <= dx + dw - 8 && uy >= BodyTop(dy) && uy <= BodyTop(dy) + BodyH(dh)) return 399;
        if (ux >= dx && ux <= dx + dw && uy >= dy && uy <= dy + dh) return 397;
        return 398;
    }

    /// <summary>DetailDraw(f, now, acc).</summary>
    public static void Draw(HubSurface hub, double f, long now, uint acc)
    {
        if (!Live) return;
        double t = Ease3(T);
        if (t <= 0.004) return;
        bool cl = !On;
        Geom(out double dx, out double dy, out double dw, out double dh);
        FillRR(HubLayout.pd, HubLayout.pd, HubLayout.cw, HubLayout.ch, 20, SBrush(FA(Alpha(0x06070E, R(190 * t)), f)));
        double oy = cl ? -(1 - t) * 20 : (1 - t) * 14;
        double sc = cl ? 0.99 + 0.01 * t : 0.94 + 0.06 * t;
        int st = PushXform(dx + dw / 2, dy + dh / 2, sc, 0);
        ShadowDraw(dx, dy - oy + 4, dw, dh, 14, 10, 8, 8, f * t);
        FillRR(dx, dy - oy, dw, dh, 14, BgV(dx, dy - oy, dw, dh, 0xF6222438, 0xFA121423));
        double yy = dy - oy;
        int pc = PushG();
        ClipRR(dx, yy, dw, dh, 14);
        PanelBackdrop(dx, yy, dw, dh, acc, f * t, now, 0.85);
        FillRect(dx, yy, dw, 74, VBrush(dx, yy, dw, 74, FA(Alpha(acc, R(26 * t)), f), Alpha(acc, 0)));
        Pop(pc);
        StrokeRR(dx, yy, dw, dh, 14, Pen(FA(Alpha(acc, R(110 * t)), f), 1.2));
        FillRR(dx + 18, yy + 18, 3, 22, 1.5, SBrush(FA(Alpha(acc, R(230 * t)), f)));
        Txt(Title, dx + 32, yy + 17, dw - 96, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, R(240 * t)), f), Fmt.L);
        if (Tag != "") Txt(Tag, dx + 32, yy + 38, dw - 96, 15, hub.HL.fXs, FA(Alpha(acc, R(190 * t)), f), Fmt.L);
        double rw = (dw - 36) * t;
        FillRect(dx + 18, yy + 62, rw, 1, HBrush(dx + 18, yy + 62, rw, 1, FA(Alpha(acc, R(120 * t)), f), FA(Alpha(0xFFFFFF, R(16 * t)), f)));
        CloseBtn(hub, dx + dw - 30, yy + 28, t, f, now, acc);
        Txt(Hint, dx + 22, yy + 76, dw - 44, 16, Fonts.fHint, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.25), R(225 * t)), f), Fmt.L);
        var L = Lines();
        double bTop = BodyTop(yy), bH = BodyH(dh);
        double mx = ScrollMax(dh); Max = mx;
        if (mx <= 0) { ScrT = 0; Scr = 0; }
        else
        {
            ScrT = Clamp(ScrT, 0, mx);
            Scr += (ScrT - Scr) * EK(0.24);
            if (Math.Abs(ScrT - Scr) < 0.4) Scr = ScrT;
        }
        double tw = mx > 0 ? dw - 66 : dw - 44;
        int pb = PushG();
        ClipRR(dx + 18, bTop, dw - 36, bH, 4);
        double ty = bTop - Scr;
        foreach (var wl in L)
        {
            if (ty > bTop + bH) break;
            if (ty > bTop - DET_LH) Txt(wl, dx + 22, ty, tw, 17, Fonts.fHint, FA(Alpha(0xFFC7CBE0, R(175 * t)), f), Fmt.L);
            ty += DET_LH;
        }
        Pop(pb);
        if (mx > 0)
        {
            double fTop = Clamp(Scr / 24.0, 0.0, 1.0), fBot = Clamp((mx - Scr) / 24.0, 0.0, 1.0);
            if (fTop > 0.01) FillRect(dx + 18, bTop, dw - 36, 16, VBrush(dx + 18, bTop, dw - 36, 16, FA(Alpha(0xFF1A1E33, R(210 * t * fTop)), f), Alpha(0xFF1A1E33, 0)));
            if (fBot > 0.01) FillRect(dx + 18, bTop + bH - 18, dw - 36, 18, VBrush(dx + 18, bTop + bH - 18, dw - 36, 18, Alpha(0xFF15182A, 0), FA(Alpha(0xFF15182A, R(225 * t * fBot)), f)));
            double sbX = dx + dw - 20;
            double hvS = Math.Max(hub.Hv(399), hub.HL.drag == 18 ? 1.0 : 0.0);
            FillRR(sbX + 4, bTop, 4, bH, 2, SBrush(FA(Alpha(0xFFFFFF, R((12 + 10 * hvS) * t)), f)));
            double tot = L.Count * DET_LH;
            double th = Math.Max(24, bH * bH / tot);
            double tyS = bTop + (bH - th) * (mx > 0 ? Scr / mx : 0);
            FillRR(sbX + 3, tyS, 6, th, 3, VBrush(sbX + 3, tyS, 6, th, FA(Alpha(AccHi(acc, 0.35), R((170 + 70 * hvS) * t)), f), FA(Alpha(acc, R((120 + 70 * hvS) * t)), f)));
        }
        Pop(st);
    }

    static void CloseBtn(HubSurface hub, double cx, double cy, double t, double f, long now, uint acc)
    {
        double hv = Ease3(hub.Hv(396));
        double pr = (XAt != 0 && now - XAt < 420) ? 1 - Clamp((now - XAt) / 420.0, 0.0, 1.0) : 0.0;
        double r = 13 + 2 * hv + 1.5 * pr;
        int st = PushXform(cx, cy, 1 + 0.06 * hv - 0.05 * pr, 0);
        FillEll(cx - r, cy - r, r * 2, r * 2, SBrush(FA(Alpha(acc, R((16 + 46 * hv + 40 * pr) * t)), f)));
        Ell(cx - r, cy - r, r * 2, r * 2, Pen(FA(Alpha(acc, R((60 + 120 * hv) * t)), f), 1.1));
        double a2 = 0.0174533 * (hv * 18);
        double k = 5.2 + 0.8 * hv;
        double c2 = Math.Cos(a2), s2 = Math.Sin(a2);
        var pn = Pen(FA(Alpha(Mix(0xFFC7CBE0, AccHi(acc, 0.55), hv), R((170 + 85 * hv) * t)), f), 1.7);
        Line(cx - k * c2 + k * s2, cy - k * s2 - k * c2, cx + k * c2 - k * s2, cy + k * s2 + k * c2, pn);
        Line(cx + k * c2 + k * s2, cy + k * s2 - k * c2, cx - k * c2 - k * s2, cy - k * s2 + k * c2, pn);
        Pop(st);
        if (pr > 0.01)
        {
            double ex = (1 - pr) * 14;
            Ell(cx - r - ex, cy - r - ex, (r + ex) * 2, (r + ex) * 2, Pen(FA(Alpha(acc, R(190 * pr * t)), f), 1.6 * pr + 0.3));
        }
    }

    // ---- FFMDetail(i): the nine rows' explainers ----
    static readonly string[] DetTitles = { "INJECT LIVE", "SINGLETON", "RE-APPLY", "AUTO INJECT", "CLEAN", "UPDATE FLAGS", "INSERT HITBOX", "INSERT 30HZ HITBOX", "COMMUNITY FLAGS" };
    static readonly string[] DetHints = { "write flags into Roblox memory", "find the flag table by code signature", "hold what you injected - rewrite it every 2 s",
        "inject 4 s after Roblox is detected", "export your flags without the failed ones", "rename outdated flags to their current names",
        "replace the staged set with HITBOX", "replace the staged set with 30HZ HITBOX", "browse flag sets shared by other players" };
    static readonly string[] DetBodies =
    {
        "Walks the running client's memory, finds the flag table by hashing each name, and writes your staged values straight into it. Nothing is written to disk, so a flag set this way lasts until the client closes. Flags the client does not carry are reported as failed rather than silently dropped.",
        "Chooses how the flag table is FOUND - injection works either way. On, it matches a code signature that references the table: fast, and the right default. Off, it sweeps the client's data for a structure that reads like the table and proves it by reading real flag names out of it - slower, but it survives a Roblox update that moved the code. Turn it off if injection starts failing after an update.",
        "Roblox rewrites some flags back to their defaults while you play. This checks every two seconds - faster for a few seconds after a teleport rebuilds the table, and easing out to five while nothing has moved for a while - and rewrites any flag YOU INJECTED that has drifted, so those values stay where you put them. A check reads the flags' memory a page at a time, so a hundred held flags cost a handful of reads, not a hundred. It never injects on its own: a client you have not pressed INJECT on keeps Roblox's values with this switched on, and a flag added to the list after the last INJECT is not touched until you inject again. AUTO INJECT is the switch that injects for you; this one only holds. Costs a little CPU for as long as it is on.",
        "Waits four seconds after the client appears, then injects on its own. The delay is deliberate - injecting before the flag table is built is what produces a run of failures.",
        "Exports the staged set as the last injection left it: every flag that went in and stayed in is kept, every flag the injector refused is dropped, and a flag it never tried - added since that INJECT, or switched off when it ran - is dropped too, each with its reason in the INJECTION log. The staged list itself is not touched. It needs an injection to judge by, and UNINJECT or a closed client clears that verdict, so press it while the panel still shows the result. Cancelling the save dialog copies the same cleaned set to the clipboard. A bare name that injected is written to the file in its prefixed live form, because that is the only form the settings file reads.",
        "Fetches the current live flag list and renames any staged flag whose name Roblox has retired to its closest current name. Non-destructive: valid names are left alone and names with no match are kept as-is. Pairs with CLEAN, which drops the ones that failed.",
        "Replaces the staged set with the built-in HITBOX preset - a replication and input group aimed at making hits register closer to what you see. Replaces, not merges: whatever is staged now is gone.",
        "The same idea tuned around interpolation and a lower effective replication rate. It shares most of its entries with HITBOX and adds the interpolation group. Also replaces the staged set.",
        "Opens the community browser: the flag sets published to the community repository, one card per game plus GENERAL, drawn from a cache in the YURI folder so it opens instantly and works offline. The cache is checked against the repository when the panel opens and at launch, and REFRESH checks it right now; a set is fetched again only when its content hash on the repository has changed, so an edit to a published set arrives on the next refresh. INSERT replaces the staged list with a set, after a HISTORY snapshot.",
    };
    public static void FfmDetail(int i)
    {
        if (i < 1 || i > DetBodies.Length) return;
        OpenSheet(DetTitles[i - 1], DetHints[i - 1], DetBodies[i - 1], "FAST FLAGS");
    }
}
