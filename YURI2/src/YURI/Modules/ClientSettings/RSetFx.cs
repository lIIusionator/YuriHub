using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.ClientSettings;

/// <summary>
/// EDIT FAST FLAGS (RSetFx*): the page that slides over module 4's rows. The
/// raw flag layer beside the presets - a name field, a value field and ADD,
/// then every flag the module would write: the extras (yours, deletable,
/// editable in place), the preset pairs (from their rows; editing one takes
/// the row over), and rows on their way out. Its second body is the
/// whitelist database: every name this module knows, filtered by a field,
/// a click lands the name in the add field.
/// </summary>
public static class RSetFx
{
    const uint AMBER = RSet.AMBER, C_ON = RSet.C_ON, C_OFF = RSet.C_OFF;
    public const double FXMS = 260, FXRH = 34, FXGONE = 380, FXSLIDE = 0.45, FOCMS = 190, DBRH = 26;
    public const int FXZVAL = 1700, FXZDEL = 1800, FXZMAX = 99, FXZDB = 1900, DBSLOTS = 14;
    public sealed class Row { public string N = "", V = "", Own = ""; public bool Ext; public long Gone; }
    public sealed class DbRow { public string N = "", Own = ""; public int St; }
    public static bool Fx, Db; public static long FxAt, DbAt;
    public static double FxScr, FxScrT, DbScr, DbScrT;
    public static string FxSel = "", FxName = "", FxVal = "", DbQ = "";
    public static long FxAddAt, FxDelAt, LandAt;
    public static readonly Dictionary<string, (long at, string v, string own)> FxGone = new();
    public static readonly Dictionary<string, long> DbIns = new();
    static string _focKey = "", _focOut = ""; static long _focAt;
    static string _sig = ""; static List<Row>? _memo;
    static string _dbCacheQ = "\f"; static List<DbRow>? _dbCache; static List<DbRow>? _dbAll;

    public static void Register()
    {
        RSetPanel.FxOpen = Open;
        RSet.FxCloseHook = () => { if (Fx) { FfmField.End(false); Close(); } FxGone.Clear(); FxSel = ""; FxName = ""; FxVal = ""; FxScr = FxScrT = 0; };
        RSetPanel.PageT = T; RSetPanel.PageDraw = Page; RSetPanel.PageZone = Zone; RSetPanel.PageClick = Click; RSetPanel.PagePress = Press; RSetPanel.PageWheel = Wheel; RSetPanel.PageDrag = Drag;
    }
    public static bool Animating => Fx || Db || (Clock.Tick - FxAt < FXMS + 40) || (Clock.Tick - DbAt < FXMS + 40) || FxGone.Count > 0;
    public static double H(HubLayout HL) => (HL.ffby + RSet.CH) - HL.aby;
    public static double T() { double e = Ease3(Clamp((Clock.Tick - FxAt) / FXMS, 0.0, 1.0)); return Fx ? e : 1 - e; }
    static double DbT() { double e = Ease3(Clamp((Clock.Tick - DbAt) / FXMS, 0.0, 1.0)); return Db ? e : 1 - e; }
    public static void Open()
    {
        FfmField.End(false);
        Fx = true; FxAt = Clock.Tick;
        Db = false; DbAt = 0; DbQ = ""; _dbCacheQ = "\f";
        DbScr = DbScrT = 0; FxScr = FxScrT = 0; FxSel = "";
        RSet.DdClose();
    }
    public static void Close()
    {
        if (!Fx) return;
        FfmField.End(true);
        FocSet("");
        Fx = false; FxAt = Clock.Tick; FxSel = "";
    }
    static string Sig() { var sb = new System.Text.StringBuilder(); sb.Append(RSet.FxVer).Append('/').Append(FxGone.Count); foreach (var o in RSet.Opts) sb.Append('.').Append(RSet.Pick.TryGetValue(o.K, out var v) ? v : 1); return sb.ToString(); }
    public static List<Row> List()
    {
        string sg = Sig();
        if (_memo is not null && _sig == sg) return _memo;
        var pf = RSet.PresetFlags();
        var res = new List<Row>();
        foreach (var (k, v) in RSet.Extra) res.Add(new Row { N = k, V = v, Ext = true, Own = RSet.FlagRowOf(k)?.N ?? "" });
        foreach (var (k, v) in pf) { if (RSet.Extra.ContainsKey(k)) continue; res.Add(new Row { N = k, V = v, Ext = false, Own = RSet.FlagOwner(k) }); }
        foreach (var (k, g) in FxGone) { if (RSet.Extra.ContainsKey(k) || pf.ContainsKey(k)) continue; res.Add(new Row { N = k, V = g.v, Ext = true, Own = g.own, Gone = g.at }); }
        res.Sort((a, b) => string.CompareOrdinal(a.N, b.N));
        _sig = sg; _memo = res;
        return res;
    }
    static double GoneP(Row e, long now) => Clamp((now - e.Gone) / FXGONE, 0.0, 1.0);
    static double RowPitch(Row e, long now)
    {
        if (e.Gone == 0) return FXRH;
        double c = Clamp((GoneP(e, now) - FXSLIDE) / (1 - FXSLIDE), 0.0, 1.0);
        return FXRH * (1 - Ease3(c));
    }
    static double TotalOf(List<Row> rows, long now) { double h = 20; foreach (var e in rows) h += RowPitch(e, now); return h; }
    public static double Total() => TotalOf(List(), Clock.Tick);
    static void Tidy(long now) { foreach (var k in FxGone.Keys.ToList()) if (now - FxGone[k].at > FXGONE + 50) { FxGone.Remove(k); RSet.FxBump(); } }

    public static string BufFor(string mode) => mode == "rfn" ? FxName : mode == "rfv" ? FxVal : mode == "rfe" ? (RSet.Extra.TryGetValue(FxSel, out var v) ? v : "") : DbQ;
    public static void Commit(string mode, string buf)
    {
        if (mode == "rfn") FxName = buf.Trim();
        else if (mode == "rfv") FxVal = buf.Trim();
        else if (mode == "rfe" && FxSel != "")
        {
            string v = buf.Trim().Replace("|", "");
            if (v == "") RSet.Extra.Remove(FxSel); else RSet.Extra[FxSel] = v;
            RSet.FxFlash[FxSel] = Clock.Tick;
            RSet.ExtraSave();
            if (RSet.On) RSet.Say("CHANGED - PRESS APPLY AGAIN TO WRITE IT", AMBER);
        }
        else if (mode == "rfd") DbQ = buf;
    }
    public static void DbSync() { DbQ = FfmField.Buf; DbScr = DbScrT = 0; }
    public static void Enter(string mode)
    {
        if (mode == "rfd") { var dl = DbList(); if (dl.Count > 0) DbPick(dl[0].N); }
        else if (mode == "rfn") { FfmField.End(true); FfmField.Begin("rfv", 0); }
        else if (mode == "rfv") { if (Add()) FfmField.Begin("rfn", 0); }
        else FfmField.End(true);
    }
    public static double TextX(HubLayout HL, string mode)
    {
        double nw = Math.Round((HL.abw - 32) * 0.52);
        return mode == "rfn" ? HL.abx + 16 + 9 : mode == "rfv" ? HL.abx + 16 + nw + 8 + 9 : mode == "rfe" ? HL.abx + HL.abw - 20 - 150 - 34 + 9 : HL.abx + 48;
    }
    public static bool Add()
    {
        FfmField.End(true);
        string n = FxName.Trim(), v = FxVal.Trim();
        if (!RSet.FlagNameOK(n)) { RSet.Say("THAT IS NOT A FLAG NAME", C_OFF); return false; }
        if (v == "") { RSet.Say("GIVE IT A VALUE", C_OFF); return false; }
        v = v.Replace("|", "");
        var o9 = RSet.FlagRowOf(n);
        int took = o9 is not null && RSet.PresetFlags().ContainsKey(n) ? RSet.TakeRow(o9) : 0;
        bool had = RSet.Extra.ContainsKey(n);
        RSet.Extra[n] = v;
        RSet.FxFlash[n] = Clock.Tick; FxAddAt = Clock.Tick;
        FxName = ""; FxVal = "";
        RSet.ExtraSave();
        RSet.Say(took > 0 ? o9!.N.ToUpperInvariant() + " IS NOW YOURS - THE ROW IS OFF" : (had ? "UPDATED" : "ADDED") + (RSet.On ? " - PRESS APPLY AGAIN TO WRITE IT" : ""), RSet.On ? AMBER : 0);
        return true;
    }
    public static void Del(string name)
    {
        if (!RSet.Extra.TryGetValue(name, out var val)) return;
        string own = RSet.FlagRowOf(name)?.N ?? "";
        RSet.Extra.Remove(name);
        FxDelAt = Clock.Tick;
        if (FxSel == name) { FfmField.End(false); FxSel = ""; }
        int also = 0;
        if (RSet.PresetFlags().ContainsKey(name)) { var o = RSet.FlagRowOf(name); if (o is not null) { also = RSet.TakeRow(o); RSet.Extra.Remove(name); } }
        FxGone[name] = (Clock.Tick, val, own);
        RSet.ExtraSave();
        RSet.Say(also > 0 ? "REMOVED - " + own.ToUpperInvariant() + " IS OFF, TURN IT BACK ON IN CLIENT SETTINGS" : RSet.On ? "REMOVED - PRESS APPLY AGAIN TO WRITE IT" : "REMOVED", RSet.On ? AMBER : 0);
    }
    public static void EditRow(string name)
    {
        if (FfmField.Edit == "rfe" && FxSel != "" && FxSel != name) FfmField.End(true);
        if (!RSet.Extra.ContainsKey(name))
        {
            var basePf = RSet.PresetFlags();
            if (!basePf.ContainsKey(name)) return;
            var o = RSet.FlagRowOf(name);
            if (o is not null && RSet.TakeRow(o) > 0) RSet.Say(o.N.ToUpperInvariant() + " IS NOW YOURS - THE ROW IS OFF", RSet.On ? AMBER : 0);
            else { RSet.Extra[name] = basePf[name]; RSet.ExtraSave(); }
            if (!RSet.Extra.ContainsKey(name)) return;
        }
        FxSel = name;
        FfmField.Begin("rfe", 0);
    }
    // ---- the database ----
    public static void DbOpen(bool on)
    {
        if (Db == on) return;
        FfmField.End(true);
        Db = on; DbAt = Clock.Tick; DbScr = DbScrT = 0;
    }
    public static List<DbRow> DbAll()
    {
        if (_dbAll is not null) return _dbAll;
        var seen = new HashSet<string>(); var res = new List<DbRow>();
        foreach (var o in RSet.Opts) { if (o.F is null) continue; foreach (var m in o.F) foreach (var k in m.Keys) { if (!seen.Add(k)) continue; res.Add(new DbRow { N = k, Own = o.N, St = RSet.FlagStatus(k) }); } }
        res.Sort((a, b) => string.CompareOrdinal(a.N, b.N));
        return _dbAll = res;
    }
    public static List<DbRow> DbList()
    {
        string q = DbQ.Trim();
        if (_dbCache is not null && _dbCacheQ == q) return _dbCache;
        var res = DbAll().Where(e => q == "" || e.N.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        _dbCacheQ = q; _dbCache = res;
        return res;
    }
    static int DbHave(string nm, Dictionary<string, string> pf) => RSet.Extra.ContainsKey(nm) ? 2 : pf.ContainsKey(nm) ? 1 : 0;
    public static void DbPick(string nm)
    {
        DbIns[nm.ToLowerInvariant()] = Clock.Tick; LandAt = Clock.Tick;
        FxName = nm;
        DbOpen(false);
        FfmField.Begin("rfv", 0);
        RSet.Say("NAME SET - GIVE IT A VALUE", 0);
    }
    public static void FocSet(string key)
    {
        if (_focKey == key) return;
        if (_focKey != "") _focOut = _focKey;
        _focKey = key; _focAt = Clock.Tick;
    }
    static double FocOf(string key)
    {
        if (key == "") return 0.0;
        double t = Ease3(Clamp((Clock.Tick - _focAt) / FOCMS, 0.0, 1.0));
        return _focKey == key ? t : _focOut == key ? 1 - t : 0.0;
    }
    static string Fit(string s, double wmax, Font f) { while (s.Length > 1 && Fonts.MeasureW(s, f) > wmax) s = s[1..]; return s; }

    // ---- drawing ----
    public static void Page(HubSurface hub, double ax, double ay, double pah, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        if (ff <= 0.01) return;
        Tidy(now);
        int stP = PushG();
        double sl = (1 - ff) * 22, cx = ax + sl;
        FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(0xFF181C34, ff), FA(0xFF12141F, ff)));
        MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, ff, now, 0.9);
        StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, 160), ff), 1.2));
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 200), ff), 1.6);
        Line(ax + 14, ay + 0.6, ax + 54, ay + 0.6, pnG); Line(ax + HL.abw - 54, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        ClipRR(ax, ay, HL.abw, pah, 12);
        double hy = ay + 10;
        double tw0 = Fonts.MeasureW("FAST FLAGS", Fonts.fBadge);
        Txt("FAST FLAGS", cx + 16, hy, tw0 + 4, 18, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.2), 240), ff), Fmt.L);
        int nx = RSet.Extra.Count;
        Txt(nx > 0 ? nx + " custom" : "none yet", cx + 16 + tw0 + 12, hy + 2, 120, 14, HL.fXs, FA(Alpha(acc, 170), ff), Fmt.L);
        double dbT = DbT();
        FFMBtn(1604, cx + HL.abw - 172, hy - 3, 76, 22, Db ? "LIST" : "DATABASE", acc, ff, Db ? 2 : 0);
        FFMBtn(1600, cx + HL.abw - 88, hy - 3, 72, 22, "BACK", acc, ff, 2);
        bool fresh = RSet.MsgAt != 0 && now - RSet.MsgAt < 4000;
        string note = !RSet.SrcExtra ? "FLAG SOURCE is CLIENT SYSTEMS - nothing on this page is being written"
            : Db ? "everything this module writes  \u00B7  green is on roblox's allowlist  \u00B7  click one to add it"
                 : "green is on roblox's allowlist  \u00B7  red is written but ignored  \u00B7  the list can change";
        Txt(FFMElide(fresh ? RSet.Msg : note, HL.fXs, HL.abw - 200), cx + 16, hy + 20, HL.abw - 200, 14, HL.fXs, FA(Alpha(fresh ? RSet.MsgCol : 0xFFC7CBE0, fresh ? 235 : 110), ff), Fmt.L);
        FadeLine(cx + 14, cx + HL.abw - 14, hy + 40, 0x30FFFFFF, ff);
        double ay2 = hy + 48, lTop = ay2 + 36, lBot = ay + pah - 8;
        if (lBot - lTop < 30) { Pop(stP); return; }
        if (dbT < 0.996) Body(hub, ax, ay2, lTop, lBot, ff * (1 - dbT), sl - dbT * 26, now, acc);
        if (dbT > 0.004) DbBody(hub, ax, ay2, lTop, lBot, ff * dbT, sl + (1 - dbT) * 26, now, acc);
        Pop(stP);
    }
    static void Body(HubSurface hub, double ax, double ay2, double lTop, double lBot, double ff, double sld, long now, uint acc)
    {
        var HL = hub.HL;
        double cx = ax + sld;
        bool edN = FfmField.Edit == "rfn", edV = FfmField.Edit == "rfv";
        double nw = Math.Round((HL.abw - 32) * 0.52), vw = HL.abw - 32 - nw - 76;
        double nfx = cx + 16, vfx = cx + 16 + nw + 8;
        string vN = edN ? FfmField.Buf : FxName, vV = edV ? FfmField.Buf : FxVal;
        double fcN = FocOf("rfn"), fcV = FocOf("rfv");
        Field(hub, 1601, nfx, ay2, nw, 26, fcN, acc, ff);
        Field(hub, 1602, vfx, ay2, vw, 26, fcV, acc, ff);
        if (LandAt != 0) { double lt = (now - LandAt) / FfmViews.FFM_INSMS; FfmViews.InsPulse(nfx, ay2, nw, 26, 7, lt, ff); if (lt >= 1) LandAt = 0; }
        Text(nfx, ay2, nw, 26, vN, "flag name", edN, fcN, acc, ff, now);
        Text(vfx, ay2, vw, 26, vV, "value", edV, fcV, acc, ff, now);
        bool okAdd = RSet.FlagNameOK(vN) && vV.Trim() != "";
        FFMBtn(1603, cx + HL.abw - 76, ay2 + 1, 60, 24, "ADD", acc, ff, okAdd ? 1 : 4);
        int st = PushG(); ClipRR(cx + 8, lTop, HL.abw - 16, lBot - lTop, 8);
        var rows = List();
        if (rows.Count == 0) Txt("nothing is being written yet - pick some presets, or add a flag above", cx + 16, lTop + 16, HL.abw - 32, 16, HL.fXs, FA(0x62C7CBE0, ff), Fmt.C);
        var pfB = RSet.PresetFlags();
        double yacc = lTop + 6 - FxScr;
        for (int i2 = 1; i2 <= rows.Count && i2 <= FXZMAX; i2++)
        {
            var e = rows[i2 - 1];
            double ry = yacc, pitch = RowPitch(e, now);
            yacc += pitch;
            if (ry + FXRH < lTop || ry > lBot) continue;
            double cg = Clamp((ff - 0.25 - Math.Min(i2, 5) * 0.05) / 0.5, 0.0, 1.0);
            if (cg <= 0.01) continue;
            double gs = e.Gone != 0 ? Ease3(Clamp(GoneP(e, now) / FXSLIDE, 0.0, 1.0)) : 0.0;
            double fb = cg * (1 - gs);
            if (fb <= 0.01) continue;
            double rx = cx - gs * (HL.abw + 24);
            bool gp = gs > 0;
            double hv = gp ? 0.0 : Math.Max(Math.Max(hub.Hv(FXZVAL + i2), hub.Hv(FXZDEL + i2)), FocOf(e.N) * 0.85);
            long fl = RSet.FxFlash.TryGetValue(e.N, out var fla) ? fla : 0;
            double gl = fl != 0 && now - fl < 900 ? 1 - (now - fl) / 900.0 : 0.0;
            if (hv > 0.01 || gl > 0.01) FillRR(rx + 12, ry, HL.abw - 24, FXRH - 4, 7, HBrush(rx + 12, ry, HL.abw - 24, FXRH - 4, FA(Alpha(gl > 0.01 ? acc : 0xFFFFFF, gl > 0.01 ? 26 * gl : 14 * hv), fb), FA(Alpha(0xFFFFFF, 3 * hv), fb)));
            int st2 = RSet.FlagStatus(e.N);
            uint pc = st2 == 1 ? C_ON : st2 == 2 ? C_OFF : AMBER;
            FillEll(rx + 20, ry + FXRH / 2 - 6, 5, 5, SBrush(FA(Alpha(pc, e.Ext ? 210 : 150), fb)));
            bool edThis = FfmField.Edit == "rfe" && FxSel == e.N;
            double fcR = gp ? 0.0 : FocOf(e.N);
            double vwid = 150, vx = rx + HL.abw - 20 - vwid - (e.Ext ? 34 * fcR : 0), nwid = vx - (rx + 34) - 10;
            Txt(FFMElide(e.N, Fonts.fBadge, nwid), rx + 34, ry + 3, nwid, 15, Fonts.fBadge, FA(Alpha(e.Ext ? Mix(0xFFE8EAF6, acc, 0.2) : 0xFFC7CBE0, e.Ext ? 235 : 170), fb), Fmt.L);
            string sub = !e.Ext ? (e.Own != "" ? "from " + e.Own : "from a preset") : st2 != 1 ? "off roblox's list - ignored in the file" : e.Own != "" ? (pfB.ContainsKey(e.N) ? "yours  \u00B7  overrides " + e.Own : "yours  \u00B7  " + e.Own + " is off") : "yours  \u00B7  on roblox's list";
            Txt(FFMElide(sub, HL.fXs, nwid), rx + 34, ry + 17, nwid, 13, HL.fXs, FA(Alpha(st2 == 2 ? C_OFF : 0xFFC7CBE0, 130), fb), Fmt.L);
            string vRow = edThis ? FfmField.Buf : e.V;
            Field(hub, FXZVAL + i2, vx, ry + 3, vwid, 24, fcR, acc, fb);
            Text(vx, ry + 3, vwid, 24, vRow, "value", edThis, fcR, acc, fb, now);
            if (e.Ext && !gp) DelBtn(hub, FXZDEL + i2, rx + HL.abw - 46, ry + 3, 26, 24, acc, fb, now, pfB.ContainsKey(e.N) ? 2 : 1, fcR);
        }
        double tot = TotalOf(rows, now), vis = lBot - lTop;
        if (tot > vis)
        {
            double sbS = Math.Max(hub.Hv(1699), HL.drag == 21 ? 1.0 : 0.0), sw2 = 3 + 3 * sbS, sxs = cx + HL.abw - 14 - 3 * sbS;
            FillRR(sxs, lTop + 4, sw2, vis - 8, sw2 / 2, SBrush(FA(Alpha(0xFFFFFF, 18 + 26 * sbS), ff)));
            double th2 = Math.Max(22, (vis - 8) * (vis / tot)), ty2 = lTop + 4 + (vis - 8 - th2) * (FxScr / Math.Max(1, tot - vis));
            FillRR(sxs, ty2, sw2, th2, sw2 / 2, VBrush(sxs, ty2, sw2, th2, FA(Alpha(AccHi(acc, 0.35), 190 + 65 * sbS), ff), FA(Alpha(acc, 170 + 60 * sbS), ff)));
        }
        Pop(st);
        FxScrT = Clamp(FxScrT, 0.0, Math.Max(0.0, tot - vis));
        FxScr += (FxScrT - FxScr) * EK(0.3); if (Math.Abs(FxScr - FxScrT) < 0.3) FxScr = FxScrT;
    }
    static void DbBody(HubSurface hub, double ax, double ay2, double lTop, double lBot, double ff, double sld, long now, uint acc)
    {
        var HL = hub.HL;
        double cx = ax + sld;
        bool ed = FfmField.Edit == "rfd";
        double hvq = hub.Hv(1606), fcD = FocOf("rfd"), sy = ay2;
        FillRR(cx + 16, sy, HL.abw - 32, 26, 7, SBrush(FA(0xFF0C0E17, ff)));
        MicroBackdrop(cx + 16, sy, HL.abw - 32, 26, 7, acc, ff, now, 0.75);
        if (fcD > 0.01 && fcD < 0.995 && !HubState.LowPerf) { double pr = Math.Sin(fcD * Math.PI), ex = (1 - fcD) * 11; StrokeRR(cx + 16 - ex, sy - ex * 0.55, HL.abw - 32 + ex * 2, 26 + ex * 1.1, 7 + ex * 0.35, Pen(FA(Alpha(AccHi(acc, 0.4), R(190 * pr)), ff), 1.4 * pr + 0.2)); }
        StrokeRR(cx + 16, sy, HL.abw - 32, 26, 7, PenP(FA(Alpha(THMix(0xFFFFFF, acc, fcD), R((26 + 30 * hvq) + (170 - (26 + 30 * hvq)) * fcD)), ff), 1.2 + 0.1 * fcD));
        var pnM = PenP(FA(Alpha(THMix(0xFF9AA8C0, acc, fcD), R((115 + 60 * hvq) + (190 - (115 + 60 * hvq)) * fcD)), ff), 1.4);
        Ell(cx + 26, sy + 7, 11, 11, pnM); Line(cx + 36, sy + 17, cx + 40, sy + 21, pnM);
        var lst = DbList();
        double qMax = HL.abw - 150;
        string qTxt = ed ? FfmField.Buf : DbQ;
        if (qTxt == "") Txt("type to filter  \u00B7  ENTER takes the first match", cx + 48, sy, qMax, 26, Fonts.fHint, FA(0x5EC7CBE0, ff), Fmt.L);
        else Txt(Fit(qTxt, qMax, HL.fM), cx + 48, sy, qMax, 26, HL.fM, FA(Alpha(0xFFE8EAF6, 235), ff), Fmt.L);
        if (ed) { string qv = Fit(qTxt, qMax, HL.fM); FfmField.Paint(cx + 48, sy, 26, qv, qTxt.Length - qv.Length, acc, ff, now); }
        Txt(lst.Count + " of " + DbAll().Count, cx + HL.abw - 122, sy, 100, 26, HL.fS, FA(0x66C7CBE0, ff), Fmt.R);
        int st = PushG(); ClipRR(cx + 8, lTop, HL.abw - 16, lBot - lTop, 8);
        if (lst.Count == 0) Txt("nothing matches that", cx + 16, lTop + 16, HL.abw - 32, 16, HL.fXs, FA(0x62C7CBE0, ff), Fmt.C);
        var pf = RSet.PresetFlags();
        double ry0 = lTop + 4 - DbScr % DBRH;
        int first = (int)Math.Floor(DbScr / DBRH) + 1;
        for (int k = 1; k <= DBSLOTS; k++)
        {
            int idx = first + k - 1;
            if (idx < 1 || idx > lst.Count) continue;
            var e = lst[idx - 1];
            double ry = ry0 + (k - 1) * DBRH;
            if (ry > lBot || ry + DBRH < lTop) continue;
            double cg = Clamp((ff - 0.25 - Math.Min(k, 5) * 0.05) / 0.5, 0.0, 1.0);
            if (cg <= 0.01) continue;
            double fb = cg, hv = hub.Hv(FXZDB + k);
            if (hv > 0.01)
            {
                FillRR(cx + 12, ry, HL.abw - 30, DBRH - 2, 6, HBrush(cx + 12, ry, HL.abw - 30, DBRH - 2, FA(Alpha(0xFFFFFF, 17 * hv), fb), FA(Alpha(0xFFFFFF, 3 * hv), fb)));
                FillRR(cx + 18, ry + 6, 2.5, DBRH - 14, 1.2, SBrush(FA(Alpha(acc, 200 * hv), fb)));
            }
            FfmViews.InsPulse(cx + 12, ry, HL.abw - 30, DBRH - 2, 6, FfmViews.InsT(DbIns, e.N.ToLowerInvariant(), now), fb);
            int have = DbHave(e.N, pf);
            uint pc = e.St == 1 ? C_ON : e.St == 2 ? C_OFF : AMBER;
            FillEll(cx + 20, ry + DBRH / 2 - 2.5, 5, 5, SBrush(FA(Alpha(pc, 190), fb)));
            double nw2 = (HL.abw - 150) - 42;
            Txt(FFMElide(e.N, HL.fM, nw2), cx + 32, ry, nw2, DBRH - 2, HL.fM, FA(Alpha(have > 0 ? 0xFF6E7590 : 0xFFE8EAF6, 200 + 45 * hv), fb), Fmt.L);
            string tag = have == 2 ? "custom" : have == 1 ? "set" : "";
            if (tag != "") Txt(tag, cx + HL.abw - 122, ry, 46, DBRH - 2, HL.fS, FA(Alpha(have == 2 ? acc : C_ON, 190), fb), Fmt.R);
            Txt(FFMElide(e.Own, HL.fXs, 66), cx + HL.abw - 72, ry, 56, DBRH - 2, HL.fXs, FA(0x62C7CBE0, fb), Fmt.R);
        }
        Pop(st);
        double tot = lst.Count * DBRH, vis = lBot - lTop;
        if (tot > vis)
        {
            double sbS = Math.Max(hub.Hv(1607), HL.drag == 22 ? 1.0 : 0.0), sw2 = 3 + 3 * sbS, sxs = cx + HL.abw - 14 - 3 * sbS;
            FillRR(sxs, lTop + 4, sw2, vis - 8, sw2 / 2, SBrush(FA(Alpha(0xFFFFFF, 18 + 26 * sbS), ff)));
            double th2 = Math.Max(22, (vis - 8) * (vis / tot)), ty2 = lTop + 4 + (vis - 8 - th2) * (DbScr / Math.Max(1, tot - vis));
            FillRR(sxs, ty2, sw2, th2, sw2 / 2, VBrush(sxs, ty2, sw2, th2, FA(Alpha(AccHi(acc, 0.35), 190 + 65 * sbS), ff), FA(Alpha(acc, 170 + 60 * sbS), ff)));
        }
        DbScrT = Clamp(DbScrT, 0.0, Math.Max(0.0, tot - vis));
        DbScr += (DbScrT - DbScr) * EK(0.3); if (Math.Abs(DbScr - DbScrT) < 0.3) DbScr = DbScrT;
    }
    static void DelBtn(HubSurface hub, int z, double bx, double by, double bw, double bh, uint acc, double ff, long now, int kind, double ap)
    {
        if (ap <= 0.01) return;
        ff *= ap; bx += (1 - ap) * 16;
        double hvE = Ease3(hub.Hv(z));
        uint col = kind == 1 ? C_OFF : acc;
        double press = 0, ripple = 0;
        if (hub.ClickAt.TryGetValue(z, out var ca)) { long el = now - ca; if (el < 420) { press = el < 90 ? el / 90.0 : Math.Max(0.0, 1 - EBackOut(Clamp((el - 90) / 330.0, 0.0, 1.0), 1.7)); ripple = Clamp(el / 420.0, 0.0, 1.0); } }
        double cxb = bx + bw / 2, cyb = by + bh / 2;
        int st = PushXform(cxb, cyb, HubState.LowPerf ? 1 : (0.62 + 0.38 * ap) * (1 + 0.06 * hvE - 0.07 * press), 0);
        if (ripple > 0.01 && ripple < 1.0 && !HubState.LowPerf) { double rr = Math.Pow(ripple, 0.55); StrokeRR(bx - 12 * rr, by - 8 * rr, bw + 24 * rr, bh + 16 * rr, 7 + 6 * rr, Pen(FA(Alpha(col, R(190 * Math.Pow(1 - ripple, 1.6))), ff), 1.4 * (1 - ripple) + 0.3)); }
        FillRR(bx, by, bw, bh, 7, SBrush(FA(Alpha(hvE > 0.01 ? col : 0xFFFFFF, R(8 + 44 * hvE)), ff)));
        if (hvE > 0.01) StrokeRR(bx, by, bw, bh, 7, Pen(FA(Alpha(col, R(180 * hvE)), ff), 1.1));
        int stC = PushXform(cxb, cyb, 1 + 0.12 * hvE, HubState.LowPerf ? 0 : 12 * hvE);
        var pn = Pen(FA(Alpha(hvE > 0.01 ? AccHi(col, 0.35) : 0xFF9AA8C0, R(160 + 90 * hvE)), ff), 1.7);
        const double r2 = 4.2;
        Line(cxb - r2, cyb - r2, cxb + r2, cyb + r2, pn); Line(cxb + r2, cyb - r2, cxb - r2, cyb + r2, pn);
        Pop(stC); Pop(st);
    }
    static void Field(HubSurface hub, int z, double fx, double fy, double fw, double fh, double foc, uint acc, double ff)
    {
        double hv = hub.Hv(z);
        int st = PushXform(fx + fw / 2, fy + fh / 2, HubState.LowPerf ? 1 : 1 + 0.014 * foc, 0);
        if (foc > 0.01 && foc < 0.995 && !HubState.LowPerf) { double pr = Math.Sin(foc * Math.PI), ex = (1 - foc) * 11; StrokeRR(fx - ex, fy - ex * 0.55, fw + ex * 2, fh + ex * 1.1, 7 + ex * 0.35, Pen(FA(Alpha(AccHi(acc, 0.4), R(190 * pr)), ff), 1.4 * pr + 0.2)); }
        FillRR(fx, fy, fw, fh, 7, SBrush(FA(Alpha(0xFFFFFF, R((10 + 8 * hv) + (20 - (10 + 8 * hv)) * foc)), ff)));
        StrokeRR(fx, fy, fw, fh, 7, PenP(FA(Alpha(THMix(acc, AccHi(acc, 0.4), foc), R((60 + 60 * hv) + (190 - (60 + 60 * hv)) * foc)), ff), 1 + 0.4 * foc));
        if (foc > 0.01) { double rh = (fh - 10) * foc; FillRR(fx + 2.5, fy + fh / 2 - rh / 2, 2, rh, 1, SBrush(FA(Alpha(AccHi(acc, 0.45), R(210 * foc)), ff))); }
        Pop(st);
    }
    static void Text(double fx, double fy, double fw, double fh, string val, string hint, bool active, double foc, uint acc, double ff, long now)
    {
        var HL = HubSurface.Live!.HL;
        double tx = fx + 9, tw = fw - 18;
        if (val == "") { if (hint != "" && foc < 0.995) Txt(hint, tx, fy, tw, fh, Fonts.fHint, FA(0x55C7CBE0, ff * (1 - foc)), Fmt.L); }
        else { string vis = Fit(val, tw, HL.fM); Txt(vis, tx, fy, tw, fh, HL.fM, FA(Alpha(0xFFE8EAF6, 235), ff), Fmt.L); }
        if (active) { string vis = Fit(val, tw, HL.fM); FfmField.Paint(tx, fy, fh, vis, val.Length - vis.Length, acc, ff, now); }
    }

    // ---- zones and clicks ----
    /// <summary>RSetIsFldZone: the EDIT FAST FLAGS page's text fields.</summary>
    public static bool IsFldZone(int z) => z == 1601 || z == 1602 || z == 1606 || (z > FXZVAL && z <= FXZVAL + FXZMAX);
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double ax = HL.abx, ay = HL.aby, hy = ay + 10;
        if (ux >= ax + HL.abw - 88 && ux <= ax + HL.abw - 16 && uy >= hy - 3 && uy <= hy + 19) return 1600;
        if (ux >= ax + HL.abw - 172 && ux <= ax + HL.abw - 96 && uy >= hy - 3 && uy <= hy + 19) return 1604;
        double ay2 = hy + 48, lTop = ay2 + 36, lBot = ay + H(HL) - 8;
        if (Db)
        {
            if (ux >= ax + 16 && ux <= ax + HL.abw - 16 && uy >= ay2 && uy <= ay2 + 26) return 1606;
            var dl = DbList();
            if (dl.Count * DBRH > lBot - lTop && ux >= ax + HL.abw - 22 && ux <= ax + HL.abw - 8 && uy >= lTop && uy <= lBot) return 1607;
            if (uy >= lTop && uy <= lBot)
            {
                double ry0 = lTop + 4 - DbScr % DBRH;
                int k = (int)Math.Floor((uy - ry0) / DBRH) + 1, idx = (int)Math.Floor(DbScr / DBRH) + k;
                if (k >= 1 && k <= DBSLOTS && idx >= 1 && idx <= dl.Count) return FXZDB + k;
            }
            return 1608;
        }
        double nw = Math.Round((HL.abw - 32) * 0.52), vw = HL.abw - 32 - nw - 76;
        if (uy >= ay2 && uy <= ay2 + 26)
        {
            if (ux >= ax + 16 && ux <= ax + 16 + nw) return 1601;
            if (ux >= ax + 16 + nw + 8 && ux <= ax + 16 + nw + 8 + vw) return 1602;
        }
        if (ux >= ax + HL.abw - 76 && ux <= ax + HL.abw - 16 && uy >= ay2 + 1 && uy <= ay2 + 25) return 1603;
        var rows = List();
        long nw9 = Clock.Tick;
        if (TotalOf(rows, nw9) > lBot - lTop && ux >= ax + HL.abw - 22 && ux <= ax + HL.abw - 8 && uy >= lTop && uy <= lBot) return 1699;
        if (uy >= lTop && uy <= lBot)
        {
            double yacc = lTop + 6 - FxScr;
            for (int i2 = 1; i2 <= rows.Count && i2 <= FXZMAX; i2++)
            {
                var e = rows[i2 - 1];
                double ry = yacc;
                yacc += RowPitch(e, nw9);
                if (e.Gone != 0 || uy < ry || uy > ry + FXRH - 4) continue;
                double fc = FocOf(e.N);
                if (e.Ext && fc > 0.35 && ux >= ax + HL.abw - 46 && ux <= ax + HL.abw - 20) return FXZDEL + i2;
                double vx = ax + HL.abw - 20 - 150 - (e.Ext ? 34 * fc : 0);
                if (ux >= vx && ux <= vx + 150) return FXZVAL + i2;
                return 1609;
            }
        }
        return 1608;
    }
    /// <summary>The press half: the three fields and the row value fields take the caret on the way down; the two scrollbars start their drags.</summary>
    public static bool Press(HubSurface hub, int z)
    {
        if (z == 1606) { if (FfmField.Edit != "rfd") FfmField.Begin("rfd", 0); FfmField.Mouse(hub.PtrX); return true; }
        if (z == 1601) { if (FfmField.Edit != "rfn") FfmField.Begin("rfn", 0); FfmField.Mouse(hub.PtrX); return true; }
        if (z == 1602) { if (FfmField.Edit != "rfv") FfmField.Begin("rfv", 0); FfmField.Mouse(hub.PtrX); return true; }
        if (z > FXZVAL && z <= FXZVAL + FXZMAX)
        {
            var rows = List(); int i = z - FXZVAL;
            if (i >= 1 && i <= rows.Count) { if (!(FfmField.Edit == "rfe" && FxSel == rows[i - 1].N)) EditRow(rows[i - 1].N); FfmField.Mouse(hub.PtrX); }
            return true;
        }
        if (z == 1699) { hub.HL.drag = 21; Drag(hub, hub.PtrY); return true; }
        if (z == 1607) { hub.HL.drag = 22; Drag(hub, hub.PtrY); return true; }
        return false;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (z == 1600) { hub.ClickAt[1600] = Clock.Tick; if (Db) DbOpen(false); else Close(); return true; }
        if (z == 1604) { hub.ClickAt[1604] = Clock.Tick; DbOpen(!Db); return true; }
        if (z > FXZDB && z <= FXZDB + DBSLOTS) { var dl = DbList(); int idx = (int)Math.Floor(DbScr / DBRH) + (z - FXZDB); if (idx >= 1 && idx <= dl.Count) DbPick(dl[idx - 1].N); return true; }
        if (z == 1603) { hub.ClickAt[1603] = Clock.Tick; Add(); return true; }
        if (z > FXZDEL && z <= FXZDEL + FXZMAX) { var rows = List(); int i = z - FXZDEL; hub.ClickAt[z] = Clock.Tick; if (i >= 1 && i <= rows.Count && rows[i - 1].Ext) Del(rows[i - 1].N); return true; }
        return z >= 1600 && z <= FXZDB + DBSLOTS;                              // the page swallows the rest
    }
    public static void Drag(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        double lTop = HL.aby + 10 + 48 + 36, lBot = HL.aby + H(HL) - 8, vis = lBot - lTop;
        if (HL.drag == 22)
        {
            double tot = DbList().Count * DBRH, thmb = Math.Max(22, (vis - 8) * (vis / Math.Max(1, tot)));
            DbScrT = ScrollFromY(HL, uy, lTop + 4, vis - 8, tot, vis, thmb, DbScr); DbScr = DbScrT;
        }
        else
        {
            double tot = Total(), thmb = Math.Max(22, (vis - 8) * (vis / Math.Max(1, tot)));
            FxScrT = ScrollFromY(HL, uy, lTop + 4, vis - 8, tot, vis, thmb, FxScr); FxScr = FxScrT;
        }
        hub.Tim(Pace.TICK_A);
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        double lTop = HL.aby + 10 + 48 + 36, lBot = HL.aby + H(HL) - 8;
        if (uy < lTop || uy > lBot || ux < HL.abx || ux > HL.abx + HL.abw) return true;
        if (Db) DbScrT = Clamp(DbScrT - delta * DBRH * 2, 0.0, Math.Max(0.0, DbList().Count * DBRH - (lBot - lTop)));
        else FxScrT = Clamp(FxScrT - delta * FXRH * 2, 0.0, Math.Max(0.0, Total() - (lBot - lTop)));
        return true;
    }
}
