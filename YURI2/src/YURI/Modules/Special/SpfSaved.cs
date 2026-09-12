using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Platform;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.Special;

public sealed class SavedEntry { public string Id = "", Nm = "", Code = "", Url = ""; }

/// <summary>
/// SAVED PLACES (SPFSavedView and its data): up to eight places and eight
/// private servers, kept in zeal.ini as sv1..sv8 ("id|name") and pv1..pv8
/// ("id|code|name|url"). USE puts the entry into the GAME LINK or PRIVATE
/// SERVER LINK field; COPY copies the id or the private link; the detail
/// column shows the game's banners in rotation with the stats GameInfo
/// fetched. The ADD and RENAME sheets edit through the shared field editor
/// (modes "sa", "sl", "rn"). Zone ids are the .ahk's (990-1016).
/// </summary>
public static class SpfSaved
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185, C_BAD = 0xFFF04438;
    const int BN_HOLD = 4600, BN_FADE = 720;
    public static readonly List<SavedEntry> Saved = new(), SavedP = new();      // SPF.saved / SPF.savedP
    public static int SavTab = 1, SavSel = 1, SavSelP = 1;
    public static double TabT;
    public static string AddTxt = "", AddLink = "", AddErr = "", RenTxt = "", RenErr = "";
    public static bool AddOpen, RenOpen; public static double AddT, RenT; public static int RenIdx;
    public static long SavAt, CopyAt;
    public static int Dscr; public static double DscrT; public static int DscrMax;
    static string _bnFor = ""; static int _bnI = 1, _bnNext = 1; static long _bnFadeAt, _bnAt;
    sealed class Shown { public double Playing, Visits, Up, Down; }
    static readonly Dictionary<string, Shown> ShownStats = new();
    public static Action<string>? Clip;

    public static List<SavedEntry> List() => SavTab == 2 ? SavedP : Saved;
    public static int SelIdx() => SavTab == 2 ? SavSelP : SavSel;
    public static void SelSet(int i) { if (SavTab == 2) SavSelP = i; else SavSel = i; }
    static void StoreCur() { if (SavTab == 2) PrivStore(); else SavedStore(); }
    public static string PrivUrl(SavedEntry e) => e.Url != "" ? e.Url : "https://www.roblox.com/games/" + e.Id + "?privateServerLinkCode=" + e.Code;

    // ---- SPFSavedLoad / Store / PrivStore ----
    public static void Load()
    {
        Saved.Clear(); SavedP.Clear();
        for (int i = 1; i <= Spf.SAV_MAX; i++)
        {
            string v = Ini.Read(Paths.IniFile, "special", "sv" + i, "");
            if (v != "")
            {
                var pp = v.Split('|', 2);
                if (pp[0] != "") Saved.Add(new SavedEntry { Id = pp[0], Nm = pp.Length >= 2 ? pp[1] : "" });
            }
            v = Ini.Read(Paths.IniFile, "special", "pv" + i, "");
            if (v != "")
            {
                var pp = v.Split('|', 4);
                if (pp[0] != "" || (pp.Length >= 4 && pp[3] != ""))
                    SavedP.Add(new SavedEntry { Id = pp[0], Code = pp.Length >= 2 ? pp[1] : "", Nm = pp.Length >= 3 ? pp[2] : "", Url = pp.Length >= 4 ? pp[3] : "" });
            }
        }
    }
    static void SavedStore() { for (int i = 1; i <= Spf.SAV_MAX; i++) Spf.Save("sv" + i, i <= Saved.Count ? Saved[i - 1].Id + "|" + Saved[i - 1].Nm : ""); }
    static void PrivStore() { for (int i = 1; i <= Spf.SAV_MAX; i++) { var e = i <= SavedP.Count ? SavedP[i - 1] : null; Spf.Save("pv" + i, e is not null ? e.Id + "|" + e.Code + "|" + e.Nm + "|" + e.Url : ""); } }

    static string KnownName(string pl)
    {
        if (Spf.Place == pl && Spf.GameNm != "") return Spf.GameNm;
        var r = GameInfo.Get(pl);
        return r is not null ? r.Nm : "";
    }
    /// <summary>SPFSavedAdd(pl): a link or a bare id; the error goes to AddErr.</summary>
    public static bool SavedAdd(string pl)
    {
        pl = pl.Trim();
        var m = Regex.Match(pl, "(?:places|games)/(\\d+)", RegexOptions.IgnoreCase);
        if (m.Success) pl = m.Groups[1].Value; else { var m2 = Regex.Match(pl, "(\\d{5,})"); if (m2.Success) pl = m2.Groups[1].Value; }
        if (!Regex.IsMatch(pl, "^\\d{5,}$")) { AddErr = "no place id in that"; return false; }
        if (Saved.Any(e => e.Id == pl)) { AddErr = "already saved"; return false; }
        if (Saved.Count >= Spf.SAV_MAX) { AddErr = "the list is full (" + Spf.SAV_MAX + ")"; return false; }
        string nm = KnownName(pl);
        Saved.Add(new SavedEntry { Id = pl, Nm = nm.Replace("|", " ") });
        SavedStore();
        SavAt = Clock.Tick; SavSel = Saved.Count; AddErr = "";
        GameInfo.Fetch(pl);
        Spf.Say("SAVED " + (nm != "" ? nm : "place " + pl), C_ON);
        return true;
    }
    public static bool PrivAdd(string pid, string link)
    {
        pid = pid.Trim(); link = link.Trim();
        var m = Regex.Match(pid, "(?:places|games)/(\\d+)", RegexOptions.IgnoreCase);
        if (m.Success) pid = m.Groups[1].Value; else { var m2 = Regex.Match(pid, "(\\d{5,})"); if (m2.Success) pid = m2.Groups[1].Value; }
        if (!Regex.IsMatch(pid, "^\\d{5,}$")) { AddErr = "enter the place id of the game"; return false; }
        string code = "", url = "";
        string shr = SpfMatch.ShareOf(link);
        if (shr != "") { code = shr; url = link; }
        else { var c = Regex.Match(link, "(?:privateServerLinkCode|linkCode|accessCode)=([A-Za-z0-9_\\-]+)", RegexOptions.IgnoreCase); if (c.Success) code = c.Groups[1].Value; }
        if (code == "") { AddErr = "that link has no private server code"; return false; }
        for (int i = 0; i < SavedP.Count; i++)
        {
            var e = SavedP[i];
            if (e.Code != code) continue;
            if (e.Id == "")
            {
                e.Id = pid;
                if (e.Nm == "") e.Nm = KnownName(pid).Replace("|", " ");
                PrivStore(); AddErr = ""; SavSelP = i + 1; GameInfo.Fetch(pid);
                Spf.Say("PLACE ID ADDED - RESOLVING", C_ON);
                return true;
            }
            AddErr = "already saved"; return false;
        }
        if (SavedP.Count >= Spf.SAV_MAX) { AddErr = "the list is full (" + Spf.SAV_MAX + ")"; return false; }
        string nm = KnownName(pid);
        SavedP.Add(new SavedEntry { Id = pid, Code = code, Nm = nm.Replace("|", " "), Url = url });
        PrivStore();
        SavAt = Clock.Tick; SavSelP = SavedP.Count; AddErr = "";
        GameInfo.Fetch(pid);
        Spf.Say("SAVED PRIVATE SERVER" + (nm != "" ? " - " + nm : ""), C_ON);
        return true;
    }
    public static void Del(int i)
    {
        var L = List();
        if (i < 1 || i > L.Count) return;
        L.RemoveAt(i - 1);
        StoreCur();
        SelSet(Math.Clamp(i, 1, Math.Max(1, L.Count)));
        SavAt = Clock.Tick;
    }
    public static void Copy()
    {
        var L = List(); int i = SelIdx();
        if (i < 1 || i > L.Count) return;
        string txt = SavTab == 2 ? PrivUrl(L[i - 1]) : L[i - 1].Id;
        bool ok = Clip is not null;
        if (ok) Clip!(txt);
        CopyAt = ok ? Clock.Tick : 0;
        Spf.Say(ok ? "COPIED " + (SavTab == 2 ? "PRIVATE SERVER LINK" : txt) : "COULD NOT REACH THE CLIPBOARD", ok ? C_ON : C_ACC);
    }
    /// <summary>SPFSavedUse(i): the entry into the matching link field.</summary>
    public static void Use(int i)
    {
        var L = List();
        if (i < 1 || i > L.Count) return;
        var e = L[i - 1];
        if (SavTab == 2) { Spf.MmPriv = PrivUrl(e); Spf.Save("privlink", Spf.MmPriv); }
        else { Spf.MmLink = e.Id; Spf.Save("gamelink", Spf.MmLink); }
        Spf.Say("LOADED " + (e.Nm != "" ? e.Nm : e.Id) + (SavTab == 2 ? " (private)" : ""), C_ON);
    }
    public static void TabSet(int t)
    {
        if (SavTab == t) return;
        SavTab = t;
        if (RenOpen) RenClose();
        AddTxt = ""; AddLink = ""; Dscr = 0; DscrT = 0; AddErr = "";
        var L = List();
        SelSet(Math.Clamp(SelIdx(), 1, Math.Max(1, L.Count)));
        if (L.Count > 0) GameInfo.Fetch(L[SelIdx() - 1].Id);
    }
    // ---- the sheets ----
    public static void AddOpenSheet() { AddOpen = true; AddTxt = ""; AddLink = ""; AddErr = ""; FfmField.Begin("sa", 0); }
    public static void AddClose() { AddOpen = false; AddErr = ""; if (FfmField.Edit == "sa" || FfmField.Edit == "sl") FfmField.End(true); }
    public static void AddConfirm()
    {
        if (FfmField.Edit == "sa" || FfmField.Edit == "sl") FfmField.End(true);
        bool ok = SavTab == 2 ? PrivAdd(AddTxt, AddLink) : SavedAdd(AddTxt);
        if (ok) AddClose();
    }
    public static void RenOpenSheet()
    {
        if (SavTab != 2) return;
        var L = List(); int i = SelIdx();
        if (i < 1 || i > L.Count) return;
        RenIdx = i; RenTxt = L[i - 1].Nm; RenErr = ""; RenOpen = true;
        FfmField.Begin("rn", 0);
        FfmField.Sel = 0;                                                       // whole name selected - typing replaces it
    }
    public static void RenClose() { RenOpen = false; if (FfmField.Edit == "rn") FfmField.End(true); }
    public static void RenConfirm()
    {
        if (FfmField.Edit == "rn") FfmField.End(true);
        if (SavTab != 2) { RenClose(); return; }
        int i = RenIdx;
        if (i < 1 || i > SavedP.Count) { RenClose(); return; }
        string nm = RenTxt.Replace("|", " ").Trim();
        if (nm == "") { RenErr = "the name cannot be empty"; return; }
        SavedP[i - 1].Nm = nm;
        PrivStore();
        Spf.Say("RENAMED TO " + nm, C_ON);
        RenClose();
    }
    /// <summary>The field editor's buffer for the sheet modes.</summary>
    public static string BufFor(string mode) => mode == "sa" ? AddTxt : mode == "sl" ? AddLink : mode == "rn" ? RenTxt : "";
    public static void Sync(string mode, string buf) { if (mode == "sa") AddTxt = buf; else if (mode == "sl") AddLink = buf; else if (mode == "rn") RenTxt = buf; }
    public static void Enter(string mode)
    {
        if (mode == "sa" && SavTab == 2) { FfmField.End(true); FfmField.Begin("sl", 0); }
        else if (mode == "sa" || mode == "sl") AddConfirm();
        else if (mode == "rn") RenConfirm();
    }
    public static void Escape(string mode) { if (mode == "rn") RenClose(); else AddClose(); }
    public static double FieldX(HubLayout HL, string mode) { Geom(HL, mode == "rn", out var sx, out _, out _, out _); return sx + 30; }
    static void Geom(HubLayout HL, bool ren, out double sx, out double sy, out double sw, out double shh)
    {
        sw = 310; shh = ren ? 156 : SavTab == 2 ? 210 : 156;
        sx = HL.ctx + HL.ctw / 2 - sw / 2;
        sy = HL.cty + 30 + 414 / 2.0 - shh / 2;
    }
    static double AddFldY(HubLayout HL, int which) { Geom(HL, false, out _, out var sy, out _, out _); return which == 2 ? sy + 116 : sy + 64; }
    static double RenFldY(HubLayout HL) { Geom(HL, true, out _, out var sy, out _, out _); return sy + 70; }

    // ---- SPFSavedView ----
    public static void Draw(HubSurface hub, double ff, long now, uint acc, double dx0, double dy2)
    {
        var HL = hub.HL;
        double e = Ease3(Clamp((now - Spf.ViewAt) / 300.0, 0.0, 1.0));
        double t = Spf.View == "sav" ? e : Spf.ViewPrev == "sav" ? 1 - e : 0;
        if (t <= 0.01) return;
        double fo = ff * t;
        double x = HL.ctx + dx0, y = HL.cty + dy2 + 30, w = HL.ctw, h = 414;
        FillRR(x - 10, HL.cty + dy2 - 6, w + 20, h + 54, 14, SBrush(FA(Alpha(0x06070E, R(220 * t)), ff)));
        int st = PushXform(x + w / 2, y + h / 2, 0.96 + 0.04 * t, 0);
        FfmViews.HubViewChrome(x, y, w, h, fo, now, acc);
        FillRR(x, y + 10, 3, 22, 1.5, SBrush(FA(Alpha(acc, 200), fo)));
        TabT += ((SavTab == 2 ? 1.0 : 0.0) - TabT) * EK(0.26);
        string[] tabs = { "PLACES", "PRIVATE SERVERS" }; int[] cnts = { Saved.Count, SavedP.Count };
        double[] tbw = { Fonts.MeasureW(tabs[0], Fonts.fBadge) + 46, Fonts.MeasureW(tabs[1], Fonts.fBadge) + 46 };
        double tbx = x + 16;
        for (int i = 1; i <= 2; i++)
        {
            double tw3 = tbw[i - 1], hvT = hub.Hv(1006 + i);
            bool selT = SavTab == i;
            if (hvT > 0.01 && !selT) FillRR(tbx - 8, y + 8, tw3 + 6, 24, 7, SBrush(FA(Alpha(0xFFFFFF, R(16 * hvT)), fo)));
            Txt(tabs[i - 1], tbx, y + 11, tw3 - 30, 18, Fonts.fBadge, FA(Alpha(selT ? 0xFFE8EAF6 : 0xFFC7CBE0, selT ? 240 : 120 + 70 * hvT), fo), Fmt.L);
            FillRR(tbx + tw3 - 34, y + 13, 26, 15, 7, SBrush(FA(Alpha(cnts[i - 1] > 0 ? acc : 0xFFFFFF, cnts[i - 1] > 0 ? (selT ? 60 : 34) : 14), fo)));
            Txt(cnts[i - 1].ToString(), tbx + tw3 - 34, y + 13, 26, 14, HL.fXs, FA(Alpha(cnts[i - 1] > 0 ? AccHi(acc, 0.5) : 0xFFC7CBE0, 220), fo), Fmt.C);
            tbx += tw3 + 18;
        }
        double ux0 = x + 16, ux1 = x + 16 + tbw[0] + 18, eT = Ease3(Clamp(TabT, 0.0, 1.0));
        double uxa = ux0 + (ux1 - ux0) * eT, uwa = tbw[0] + (tbw[1] - tbw[0]) * eT;
        FillRR(uxa - 8, y + 32, uwa + 6, 2, 1, VBrush(uxa - 8, y + 32, uwa + 6, 2, FA(Alpha(AccHi(acc, 0.5), 235), fo), FA(Alpha(acc, 150), fo)));
        double hvA = hub.Hv(1003), abx2 = x + w - 122, aby2 = y + 8;
        FillRR(abx2, aby2, 88, 22, 7, SBrush(FA(Alpha(acc, R(22 + 34 * hvA)), fo)));
        MicroBackdrop(abx2, aby2, 88, 22, 7, acc, fo, now, 0.9);
        StrokeRR(abx2, aby2, 88, 22, 7, Pen(FA(Alpha(acc, R(90 + 90 * hvA)), fo), 1));
        var bP = SBrush(FA(Alpha(AccHi(acc, 0.4), 210 + 45 * hvA), fo));
        FillRR(abx2 + 11, aby2 + 10, 10, 2, 1, bP); FillRR(abx2 + 15, aby2 + 6, 2, 10, 1, bP);
        Txt("ADD CURRENT", abx2 + 26, aby2 + 4, 58, 14, HL.fXs, FA(Alpha(0xFFE8EAF6, 190 + 60 * hvA), fo), Fmt.L);
        double hvX = hub.Hv(990);
        if (hvX > 0.01) FillEll(x + w - 18 - 11, y + 14 - 11, 22, 22, SBrush(FA(Alpha(acc, R(40 * hvX)), fo)));
        var pnX = Pen(FA(Alpha(hvX > 0.2 ? acc : 0xFFC7CBE0, 130 + 120 * hvX), fo), 1.6);
        double cxx = x + w - 18, cyy = y + 14;
        Line(cxx - 5, cyy - 5, cxx + 5, cyy + 5, pnX); Line(cxx + 5, cyy - 5, cxx - 5, cyy + 5, pnX);
        Line(x + 14, y + 38, x + w - 14, y + 38, Pen(FA(Alpha(0xFFFFFF, 20), fo), 1));
        if (t < 0.85) { Pop(st); return; }
        double lx = x + 14, ly = y + 48, lw = 192;
        var LST = List();
        bool priv = SavTab == 2;
        if (LST.Count == 0)
        {
            Txt(priv ? "no private servers saved" : "nothing saved yet", x + 16, y + h / 2 - 22, w - 32, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 200), fo), Fmt.C);
            Txt(priv ? "use ADD and paste a private server link" : "use ADD to enter a place id", x + 16, y + h / 2 + 2, w - 32, 18, Fonts.fHint, FA(0x72C7CBE0, fo), Fmt.C);
            AddSheet(hub, x, y, w, h, fo, now, acc);
            Pop(st);
            return;
        }
        SelSet(Math.Clamp(SelIdx(), 1, LST.Count));
        var sel = LST[SelIdx() - 1];
        if (sel.Id != "") GameInfo.Fetch(sel.Id);
        for (int i = 1; i <= LST.Count; i++)
        {
            var en = LST[i - 1];
            double ry = ly + (i - 1) * 38;
            bool on = i == SelIdx();
            double hv = hub.Hv(990 + i);
            if (on || hv > 0.01) FillRR(lx, ry, lw, 34, 8, HBrush(lx, ry, lw, 34, FA(Alpha(on ? acc : 0xFFFFFF, on ? 38 : 14 * hv), fo), FA(Alpha(on ? acc : 0xFFFFFF, on ? 8 : 3 * hv), fo)));
            if (on)
            {
                FillRR(lx, ry + 6, 3, 22, 1.5, VBrush(lx, ry + 6, 3, 22, FA(Alpha(AccHi(acc, 0.4), 240), fo), FA(Alpha(acc, 160), fo)));
                StrokeRR(lx, ry, lw, 34, 8, Pen(FA(Alpha(acc, 70), fo), 1));
            }
            var ico = en.Id != "" ? GameInfo.Ico(en.Id) : null;
            double ixp = lx + 8 + 2 * hv;
            if (ico is not null) Img.FitRR(ico, ixp, ry + 3, 28, 28, 7, fo);
            else
            {
                FillRR(ixp, ry + 3, 28, 28, 7, VBrush(ixp, ry + 3, 28, 28, FA(Alpha(acc, 60), fo), FA(Alpha(acc, 22), fo)));
                MicroBackdrop(ixp, ry + 3, 28, 28, 7, acc, fo, now, 0.7);
                Txt((en.Nm != "" ? en.Nm : "?")[..1], ixp, ry + 7, 28, 20, Fonts.fBadge, FA(Alpha(AccHi(acc, 0.5), 210), fo), Fmt.C);
            }
            StrokeRR(ixp, ry + 3, 28, 28, 7, Pen(FA(Alpha(on ? acc : 0xFFFFFF, on ? 150 : 40), fo), 1));
            var rs = en.Id != "" ? GameInfo.Get(en.Id) : null;
            bool hasN = rs is not null && rs.Playing > 0;
            double tw9 = (hasN ? lx + lw - 72 : lx + lw - 22) - (ixp + 36);
            string nm = en.Nm != "" ? en.Nm : "place " + en.Id;
            Txt(FFMElide(nm, Fonts.fHint, tw9), ixp + 36, ry + 2, tw9, 16, Fonts.fHint, FA(Alpha(on ? 0xFFE8EAF6 : 0xFFC7CBE0, on ? 240 : 165 + 55 * hv), fo), Fmt.L);
            string sub = !priv ? en.Id : en.Id == "" ? "place id missing" : "code " + en.Code[..Math.Min(10, en.Code.Length)];
            Txt(FFMElide(sub, HL.fXs, tw9), ixp + 36, ry + 17, tw9, 13, HL.fXs, FA(0x5EC7CBE0, fo), Fmt.L);
            if (hasN)
            {
                FillEll(lx + lw - 16, ry + 14, 5, 5, SBrush(FA(Alpha(C_ON, 150 + 60 * hv), fo)));
                Txt(FfmCommView.Num(rs!.Playing), lx + lw - 66, ry + 10, 44, 14, HL.fXs, FA(Alpha(C_ON, 175 + 60 * hv), fo), Fmt.R);
            }
        }
        double fy = ly + LST.Count * 38;
        if (fy < ly + 300 && !HubState.LowPerf)
            for (int k = 1; k <= 5; k++)
            {
                double mp = (DecT(now) * 0.00014 + k * 0.2) % 1.0;
                double my = fy + (1 - mp) * (ly + 300 - fy), mx = lx + 24 + (k * 53) % (lw - 48) + 8 * Math.Sin(DecT(now) * 0.0011 + k);
                double mr = 1.1 + 0.7 * Math.Sin(DecT(now) * 0.0026 + k * 1.7);
                FillEll(mx - mr, my - mr, mr * 2, mr * 2, SBrush(FA(Alpha(acc, R(38 * Math.Sin(Math.PI * mp))), fo)));
            }
        double by = ly + 8 * 38 + 6;
        bool cpFl = CopyAt != 0 && now - CopyAt < 1100;
        FFMBtn(1002, lx, by, 62, 26, cpFl ? "COPIED" : "COPY", acc, fo, cpFl ? 3 : 0);
        FFMBtn(1000, lx + 65, by, 62, 26, "USE", acc, fo, 1);
        FFMBtn(999, lx + 130, by, 62, 26, "REMOVE", acc, fo, 0);
        if (priv) FFMBtn(1012, lx, by + 28, 192, 24, "RENAME", acc, fo, 0);
        var pnD = Pen(FA(Alpha(0xFFFFFF, 16), fo), 1); PenDash(pnD, 1); PenDashOff(pnD, (DecT(now) * 0.012) % 1000);
        Line(x + 214, ly, x + 214, by + 26, pnD);
        // ---- the detail column ----
        double dx = x + 228, dw = w - 242;
        var r = sel.Id != "" ? GameInfo.Get(sel.Id) : null;
        bool have = r is not null;
        double ih = Math.Round(dw * 9 / 16);
        if (_bnFor != sel.Id) { _bnFor = sel.Id; _bnI = 1; _bnNext = 1; _bnFadeAt = 0; _bnAt = now; }
        int bnN = sel.Id != "" ? GameInfo.ThumbN(sel.Id) : 0;
        if (_bnI > bnN) { _bnI = 1; _bnNext = 1; _bnFadeAt = 0; _bnAt = now; }
        if (_bnFadeAt != 0 && now - _bnFadeAt >= BN_FADE) { _bnI = _bnNext; _bnFadeAt = 0; _bnAt = now; }
        if (bnN > 1 && _bnFadeAt == 0 && now - _bnAt >= BN_HOLD) { _bnNext = _bnI % bnN + 1; _bnFadeAt = now; }
        double bnT = _bnFadeAt != 0 ? Clamp(Ease3((now - _bnFadeAt) / (double)BN_FADE), 0.0, 1.0) : 1.0;
        var th = bnN > 0 ? GameInfo.ThumbAt(sel.Id, _bnI) : null;
        var thN = _bnFadeAt != 0 && _bnNext != _bnI ? GameInfo.ThumbAt(sel.Id, _bnNext) : null;
        FillRR(dx, ly, dw, ih, 10, SBrush(FA(0xFF0C0E17, fo)));
        if (th is not null)
        {
            double held = Clamp((now - _bnAt) / (BN_HOLD + BN_FADE + 0.0), 0.0, 1.0);
            Img.FitRR(th, dx, ly, dw, ih, 10, fo, 1.0 + 0.06 * held, 0.46 + 0.08 * held, 0.5);
            if (thN is not null) Img.FitRR(thN, dx, ly, dw, ih, 10, fo * bnT, 1.06 - 0.06 * bnT, 0.54 - 0.08 * bnT, 0.5);
            int stC = PushG(); ClipRR(dx, ly, dw, ih, 10);
            double bandY = ly + (DecT(now) * 0.05) % (ih + 60) - 30;
            FillRect(dx, bandY, dw, 22, VBrush(dx, bandY, dw, 22, Alpha(0xFFFFFF, 0), FA(Alpha(0xFFFFFF, 16), fo)));
            if (_bnFadeAt != 0 && thN is not null) { double wsh = Math.Sin(Math.PI * bnT); FillRect(dx, ly, dw, ih, VBrush(dx, ly, dw, ih, FA(Alpha(acc, R(26 * wsh)), fo), Alpha(acc, 0))); }
            FillRect(dx, ly + ih - 44, dw, 44, VBrush(dx, ly + ih - 44, dw, 44, Alpha(0x000000, 0), FA(0xB4000000, fo)));
            Pop(stC);
            BannerDots(dx, ly, dw, ih, bnN, fo, now, acc);
            BannerHover(hub, dx, ly, dw, ih, bnN, fo, now, acc);
        }
        else
        {
            double ph = (Math.Sin(DecT(now) * 0.005) + 1) / 2;
            Txt(have ? "no image" : "loading image...", dx, ly + ih / 2 - 9, dw, 18, Fonts.fHint, FA(Alpha(0xC7CBE0, R(70 + 60 * ph)), fo), Fmt.C);
            Arc(dx + dw / 2 - 13, ly + ih / 2 - 34, 26, 26, (DecT(now) * 0.14) % 360, 80, Pen(FA(Alpha(acc, R(40 + 60 * ph)), fo), 1.4));
        }
        StrokeRR(dx, ly, dw, ih, 10, Pen(FA(Alpha(acc, 80), fo), 1));
        string nmTxt = FfmCommView.StripEmoji(r is not null ? (r.Nm != "" ? r.Nm : sel.Id) : sel.Id);
        Txt(FFMElide(nmTxt, Fonts.fBadge, dw - 24), dx + 12, ly + ih - 32, dw - 24, 22, Fonts.fBadge, FA(Alpha(th is not null ? 0xFFFFFF : 0xFFE8EAF6, th is not null ? 245 : 205), fo), Fmt.L);
        double ty = ly + ih + 12;
        if (priv)
        {
            double pw3 = Fonts.MeasureW("PRIVATE SERVER", HL.fXs) + 20;
            FillRR(dx + dw - pw3, ty - 26, pw3, 18, 8, SBrush(FA(Alpha(acc, 46), fo)));
            StrokeRR(dx + dw - pw3, ty - 26, pw3, 18, 8, Pen(FA(Alpha(acc, 110), fo), 1));
            Txt("PRIVATE SERVER", dx + dw - pw3, ty - 24, pw3, 14, HL.fXs, FA(Alpha(AccHi(acc, 0.45), 225), fo), Fmt.C);
        }
        double idw = Fonts.MeasureW("ID " + sel.Id, HL.fXs) + 20;
        FillRR(dx, ty, idw, 20, 6, SBrush(FA(Alpha(0xFFFFFF, 12), fo)));
        StrokeRR(dx, ty, idw, 20, 6, Pen(FA(Alpha(acc, 55), fo), 1));
        Txt("ID " + sel.Id, dx, ty + 3, idw, 14, HL.fXs, FA(Alpha(0xFFC7CBE0, 210), fo), Fmt.C);
        if (have)
        {
            double age = Clamp((now - r!.At) / 4000.0, 0.0, 1.0), pr = 1 - age;
            FillEll(dx + idw + 10, ty + 5, 10, 10, SBrush(FA(Alpha(C_ON, R(45 + 30 * pr)), fo)));
            FillEll(dx + idw + 12.5, ty + 7.5, 5, 5, SBrush(FA(Alpha(C_ON, R(150 + 105 * pr)), fo)));
            Txt("LIVE", dx + idw + 26, ty + 4, 60, 13, HL.fXs, FA(Alpha(C_ON, 130 + 80 * pr), fo), Fmt.L);
        }
        string dsc = r?.Desc ?? "";
        if (dsc == "") dsc = have ? "no description" : "fetching details...";
        double dy = ty + 28; int dvis = 4; double dlh = 16, dh2 = dvis * dlh;
        var dlines = new List<string>();
        foreach (var para in dsc.Replace("\r", "").Split('\n'))
        {
            if (para.Trim() == "") { dlines.Add(""); continue; }
            dlines.AddRange(Detail.WrapText(para, Fonts.fHint, dw - 12, 40));
        }
        DscrMax = Math.Max(0, dlines.Count - dvis);
        Dscr = Math.Clamp(Dscr, 0, DscrMax);
        DscrT += (Dscr - DscrT) * 0.3;
        int stD = PushG(); ClipRR(dx, dy - 2, dw, dh2 + 4, 6);
        for (int i3 = 1; i3 <= dlines.Count; i3++)
        {
            double lyy = dy + (i3 - 1) * dlh - DscrT * dlh;
            if (lyy < dy - dlh || lyy > dy + dh2) continue;
            Txt(dlines[i3 - 1], dx, lyy, dw - 12, 15, Fonts.fHint, FA(0x8EC7CBE0, fo), Fmt.L);
        }
        Pop(stD);
        if (DscrMax > 0)
        {
            double trh = dh2, thh = Math.Max(18, trh * (dvis / (double)dlines.Count)), tyy = dy + (trh - thh) * (DscrT / DscrMax);
            FillRR(dx + dw - 4, dy, 3, trh, 1.5, SBrush(FA(Alpha(0xFFFFFF, 16), fo)));
            FillRR(dx + dw - 4, tyy, 3, thh, 1.5, VBrush(dx + dw - 4, tyy, 3, thh, FA(Alpha(AccHi(acc, 0.35), 200), fo), FA(Alpha(acc, 150), fo)));
            if (DscrT < 0.3) { int fadeA = R(90 * (1 - DscrT / 0.3)); FillRect(dx, dy + dh2 - 14, dw - 8, 14, VBrush(dx, dy + dh2 - 14, dw - 8, 14, Alpha(0x0F1120, 0), FA(Alpha(0x0F1120, fadeA), fo))); }
        }
        if (!ShownStats.TryGetValue(sel.Id, out var sh)) ShownStats[sel.Id] = sh = new Shown();
        double tp = r?.Playing ?? 0, tv = r?.Visits ?? 0, tu = r?.Up ?? 0, td = r?.Down ?? 0;
        sh.Playing += (tp - sh.Playing) * EK(0.18); sh.Visits += (tv - sh.Visits) * EK(0.18); sh.Up += (tu - sh.Up) * EK(0.18); sh.Down += (td - sh.Down) * EK(0.18);
        string[] labs = { "PLAYING", "VISITS", "LIKES", "DISLIKES" };
        double[] vals = { sh.Playing, sh.Visits, sh.Up, sh.Down }, tgts = { tp, tv, tu, td };
        uint[] cols = { C_ON, 0xFFC7CBE0, C_ON, acc };
        double tw2 = (dw - 12) / 4, sy2 = dy + dh2 + 10;
        double ratio = sh.Up + sh.Down > 1 ? sh.Up / (sh.Up + sh.Down) : 0.0;
        for (int i = 1; i <= 4; i++)
        {
            double tx2 = dx + (i - 1) * (tw2 + 4);
            bool mv = Math.Abs(vals[i - 1] - tgts[i - 1]) > 0.5;
            uint cc2 = cols[i - 1];
            FillRR(tx2, sy2, tw2, 50, 8, VBrush(tx2, sy2, tw2, 50, FA(Mix(0xFF1B2038, cc2, 0.10), fo), FA(Mix(0xFF12141F, cc2, 0.05), fo)));
            MiniBackdrop(tx2, sy2, tw2, 50, 8, cc2, fo, now, 0.8);
            StrokeRR(tx2, sy2, tw2, 50, 8, Pen(FA(Alpha(mv ? acc : cc2, mv ? 170 : 46), fo), 1));
            StatGlyph(i, tx2 + 14, sy2 + 12, FA(Alpha(cc2, mv ? 235 : 165), fo));
            Txt(labs[i - 1], tx2 + 24, sy2 + 5, tw2 - 28, 13, HL.fXs, FA(Alpha(0xFFC7CBE0, 130), fo), Fmt.L);
            Txt(FfmCommView.Num((long)Math.Round(vals[i - 1])), tx2, sy2 + 20, tw2, 22, Fonts.fBadge, FA(Alpha(cc2, 245), fo), Fmt.C);
            if (i >= 3 && sh.Up + sh.Down > 1)
            {
                double frac = i == 3 ? ratio : 1 - ratio;
                FillRR(tx2 + 10, sy2 + 42, tw2 - 20, 3, 1.5, SBrush(FA(Alpha(0xFFFFFF, 16), fo)));
                FillRR(tx2 + 10, sy2 + 42, Math.Max(2, (tw2 - 20) * frac), 3, 1.5, VBrush(tx2 + 10, sy2 + 42, (tw2 - 20) * frac, 3, FA(Alpha(AccHi(cc2, 0.4), 220), fo), FA(Alpha(cc2, 170), fo)));
            }
            else if (i == 1 && tgts[0] > 0) { double pp = (Math.Sin(DecT(now) * 0.004) + 1) / 2; FillEll(tx2 + tw2 / 2 - 2, sy2 + 42, 4, 4, SBrush(FA(Alpha(cc2, R(80 + 110 * pp)), fo))); }
            if (mv)
            {
                double pr2 = (DecT(now) % 900) / 900.0, ex2 = pr2 * 7;
                StrokeRR(tx2 - ex2, sy2 - ex2 * 0.6, tw2 + ex2 * 2, 50 + ex2 * 1.2, 8 + ex2 * 0.3, Pen(FA(Alpha(acc, R(120 * (1 - pr2))), fo), 1.6 * (1 - pr2) + 0.3));
            }
        }
        AddSheet(hub, x, y, w, h, fo, now, acc);
        RenSheet(hub, x, y, w, h, fo, now, acc);
        Pop(st);
    }
    static void StatGlyph(int kind, double cx, double cy, uint col)
    {
        var pn = Pen(col, 1.4);
        if (kind == 1) { Ell(cx - 3, cy - 6, 6, 6, pn); Arc(cx - 5, cy - 1, 10, 10, 200, 140, pn); }
        else if (kind == 2) { Arc(cx - 6, cy - 5, 12, 10, 200, 140, pn); Arc(cx - 6, cy - 5, 12, 10, 20, 140, pn); FillEll(cx - 1.6, cy - 1.6, 3.2, 3.2, SBrush(col)); }
        else
        {
            int sy3 = kind == 3 ? 1 : -1;
            Line(cx - 5, cy + 5 * sy3, cx - 5, cy - 1 * sy3, pn); Line(cx - 5, cy - 1 * sy3, cx - 1, cy - 1 * sy3, pn); Line(cx - 1, cy - 1 * sy3, cx - 1, cy - 6 * sy3, pn);
            Line(cx - 1, cy - 6 * sy3, cx + 2, cy - 6 * sy3, pn); Line(cx + 2, cy - 6 * sy3, cx + 5, cy - 1 * sy3, pn); Line(cx + 5, cy - 1 * sy3, cx + 5, cy + 5 * sy3, pn); Line(cx + 5, cy + 5 * sy3, cx - 5, cy + 5 * sy3, pn);
        }
    }
    static void BannerDots(double dx, double ly, double dw, double ih, int n, double fo, long now, uint acc)
    {
        if (n < 2) return;
        n = Math.Min(n, 10);
        double dr = 3.0, gp = 7, aw = 16, tot = aw + (n - 1) * (dr * 2 + gp), bx0 = dx + dw - 12 - tot, by0 = ly + 12;
        FillRR(bx0 - 7, by0 - dr - 5, tot + 14, dr * 2 + 10, (dr * 2 + 10) / 2, SBrush(FA(Alpha(0x000000, 92), fo)));
        double cx0 = bx0;
        for (int k = 1; k <= n; k++)
        {
            bool act = k == _bnI, nxt = _bnFadeAt != 0 && k == _bnNext;
            double t = _bnFadeAt != 0 ? Clamp(Ease3((now - _bnFadeAt) / (double)BN_FADE), 0.0, 1.0) : 0.0;
            double gw = act ? aw * (1 - t) + dr * 2 * t : nxt ? dr * 2 * (1 - t) + aw * t : dr * 2;
            double al = act ? 215 * (1 - t) + 90 * t : nxt ? 90 * (1 - t) + 215 * t : 90;
            FillRR(cx0, by0 - dr, gw, dr * 2, dr, SBrush(FA(Alpha(0xFFFFFF, R(al * 0.42)), fo)));
            if (act || nxt)
            {
                double pr = act ? (_bnFadeAt != 0 ? 1.0 : Clamp((now - _bnAt) / (BN_HOLD + 0.0), 0.0, 1.0)) : t;
                if (pr > 0.01) FillRR(cx0, by0 - dr, Math.Max(gw * pr, dr * 2), dr * 2, dr, SBrush(FA(Alpha(AccHi(acc, 0.45), R(al)), fo)));
            }
            cx0 += gw + gp;
        }
    }
    static void BannerHover(HubSurface hub, double dx, double ly, double dw, double ih, int n, double fo, long now, uint acc)
    {
        if (n < 2) return;
        double hv = Ease3(hub.Hv(1010)), pr = _bnFadeAt != 0 ? Clamp(1 - (now - _bnFadeAt) / 320.0, 0.0, 1.0) : 0.0;
        if (hv < 0.004 && pr < 0.004) return;
        double e = Math.Max(hv, pr);
        int stC = PushG(); ClipRR(dx, ly, dw, ih, 10);
        FillRR(dx, ly, dw, ih, 10, SBrush(FA(Alpha(0x05060C, R(120 * hv + 60 * pr)), fo)));
        double cx = dx + dw / 2, cy = ly + ih / 2 - 6, sc = 0.88 + 0.12 * Ease3(hv) + 0.06 * pr;
        int st = PushXform(cx, cy + 6, sc, 0);
        double pw2 = 96, ph2 = 30;
        FillRR(cx - pw2 / 2, cy - ph2 / 2, pw2, ph2, ph2 / 2, SBrush(FA(Alpha(0x10131F, R(150 * e)), fo)));
        StrokeRR(cx - pw2 / 2, cy - ph2 / 2, pw2, ph2, ph2 / 2, Pen(FA(Alpha(acc, R(150 * e)), fo), 1.2));
        uint ink = HubState.ThT > 0.5 ? Mix(acc, 0xFF23242B, 0.55) : AccHi(acc, 0.45);
        double gx = cx - 30, gy = cy;
        var pn = Pen(FA(Alpha(ink, R(235 * e)), fo), 1.8);
        for (int i = 1; i <= 2; i++)
        {
            double ph = Clamp(Math.Sin(DecT(now) * 0.005 - (i - 1) * 0.7), 0.0, 1.0), ox = gx + (i - 1) * 6 + ph * 2.2;
            Line(ox - 3, gy - 5, ox + 2, gy, pn); Line(ox + 2, gy, ox - 3, gy + 5, pn);
        }
        Line(gx + 11, gy - 5.5, gx + 11, gy + 5.5, pn);
        Txt("SKIP", cx - 8, cy - 8, 44, 16, Fonts.fBadge, FA(Alpha(ink, R(240 * e)), fo), Fmt.L);
        Pop(st);
        if (pr > 0.01) { double ex = (1 - pr) * 26; StrokeRR(cx - pw2 / 2 - ex, cy - ph2 / 2 - ex * 0.6, pw2 + ex * 2, ph2 + ex * 1.2, ph2 / 2 + ex * 0.3, Pen(FA(Alpha(acc, R(170 * pr)), fo), 1.6 * pr + 0.3)); }
        Pop(stC);
    }
    public static void BannerSkip()
    {
        if (_bnFor == "" ) return;
        int bnN = GameInfo.ThumbN(_bnFor);
        if (bnN < 2 || _bnFadeAt != 0) return;
        _bnNext = _bnI % bnN + 1; _bnFadeAt = Clock.Tick;
    }

    // ---- the sheets ----
    static void SheetFrame(double sx, double sy, double sw, double shh, double fsh, double e, double x, double y, double w, double h, double fo, long now, uint acc, string title, string hint)
    {
        FillRR(x, y, w, h, 12, SBrush(FA(Alpha(0x06070E, R(180 * e)), fo)));
        ShadowDraw(sx, sy + 4, sw, shh, 14, 6, 18, 18, fsh);
        FillRR(sx, sy, sw, shh, 12, VBrush(sx, sy, sw, shh, FA(0xFF1B2038, fsh), FA(0xFF10121C, fsh)));
        MiniBackdrop(sx, sy, sw, shh, 12, acc, fsh, now, 0.85);
        StrokeRR(sx, sy, sw, shh, 12, Pen(FA(Alpha(acc, 170), fsh), 1.3));
        for (int k = 0; k < 4; k++)
        {
            double cxk = (k == 1 || k == 2) ? sx + sw - 20 : sx, cyk = k >= 2 ? sy + shh - 20 : sy;
            double ang = k == 0 ? 195 : k == 1 ? 285 : k == 2 ? 15 : 105;
            Arc(cxk, cyk, 20, 20, ang, 60, Pen(FA(Alpha(acc, 90), fsh), 1.4));
        }
        FillRR(sx, sy + 12, 3, 18, 1.5, SBrush(FA(Alpha(acc, 210), fsh)));
        Txt(title, sx + 16, sy + 12, 260, 18, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 235), fsh), Fmt.L);
        Txt(hint, sx + 16, sy + 32, sw - 32, 14, HubSurface.Live!.HL.fXs, FA(0x72C7CBE0, fsh), Fmt.L);
    }
    static void SheetField(HubSurface hub, string mode, int zid, double fx, double fy, double fw, string label, string ph, string val, string err, uint acc, double fsh, long now)
    {
        bool foc = FfmField.Edit == mode;
        double hvF = hub.Hv(zid);
        Txt(label, fx, fy - 16, fw, 13, hub.HL.fXs, FA(Alpha(0xFFC7CBE0, 140), fsh), Fmt.L);
        FillRR(fx, fy, fw, 26, 7, SBrush(FA(Mix(0xFF141728, 0xFF1B2038, Math.Max(hvF, foc ? 1.0 : 0.0)), fsh)));
        MicroBackdrop(fx, fy, fw, 26, 7, acc, fsh, now, 0.75);
        StrokeRR(fx, fy, fw, 26, 7, Pen(FA(Alpha(err != "" ? C_BAD : foc ? acc : 0xFFFFFF, err != "" ? 190 : foc ? 190 : 30 + 50 * hvF), fsh), 1));
        if (foc) FillRR(fx, fy + 6, 2.6, 14, 1.3, SBrush(FA(Alpha(acc, 210), fsh)));
        string shown = foc ? FfmField.Buf : val;
        string vv = shown; int vo = 0;
        while (Fonts.MeasureW(vv, Fonts.fHint) > fw - 20 && vv.Length > 1) { vv = vv[1..]; vo++; }
        if (foc) FfmField.Paint(fx + 10, fy + 5, 16, vv, vo, acc, fsh, now, Fonts.fHint);
        if (vv == "") Txt(ph, fx + 10, fy + 5, fw - 20, 16, Fonts.fHint, FA(0x58C7CBE0, fsh), Fmt.L);
        else Txt(vv, fx + 10, fy + 5, fw - 20, 16, Fonts.fHint, FA(Alpha(0xFFE8EAF6, 235), fsh), Fmt.L);
    }
    static void AddSheet(HubSurface hub, double x, double y, double w, double h, double fo, long now, uint acc)
    {
        var HL = hub.HL;
        AddT += ((AddOpen ? 1.0 : 0.0) - AddT) * EK(0.3);
        if (!AddOpen && AddT < 0.004) AddT = 0.0;
        if (AddT <= 0.004) return;
        double e = Ease3(Clamp(AddT, 0.0, 1.0));
        Geom(HL, false, out _, out var syG, out var sw, out var shh);
        double sx = x + w / 2 - sw / 2, sy = y + 414 / 2.0 - shh / 2, sdy = sy - syG, fsh = fo * e;
        int sts = PushXform(sx + sw / 2, sy + shh / 2, 0.9 + 0.1 * e, 0);
        SheetFrame(sx, sy, sw, shh, fsh, e, x, y, w, h, fo, now, acc, SavTab == 2 ? "ADD A PRIVATE SERVER" : "ADD A PLACE", SavTab == 2 ? "the place id lets it resolve the game" : "paste a game link or type the place id");
        bool two = SavTab == 2;
        SheetField(hub, "sa", 1006, sx + 20, AddFldY(HL, 1) + sdy, sw - 40, "PLACE ID", "e.g. 5938036553", AddTxt, AddErr, acc, fsh, now);
        if (two) SheetField(hub, "sl", 1009, sx + 20, AddFldY(HL, 2) + sdy, sw - 40, "PRIVATE SERVER LINK", "paste the link or share link", AddLink, AddErr, acc, fsh, now);
        if (AddErr != "") Txt(AddErr, sx + 20, sy + (two ? 146 : 94), sw - 40, 14, HL.fXs, FA(Alpha(C_BAD, 215), fsh), Fmt.L);
        FFMBtn(1005, sx + 20, sy + shh - 40, 124, 26, "CANCEL", 0xFFC7CBE0, fsh, 0);
        FFMBtn(1004, sx + sw - 144, sy + shh - 40, 124, 26, "ADD", acc, fsh, Regex.IsMatch(AddTxt, "\\d{5,}") && (SavTab != 2 || AddLink != "") ? 1 : 0);
        Pop(sts);
    }
    static void RenSheet(HubSurface hub, double x, double y, double w, double h, double fo, long now, uint acc)
    {
        var HL = hub.HL;
        RenT += ((RenOpen ? 1.0 : 0.0) - RenT) * EK(0.3);
        if (!RenOpen && RenT < 0.004) RenT = 0.0;
        if (RenT <= 0.004) return;
        double e = Ease3(Clamp(RenT, 0.0, 1.0));
        Geom(HL, true, out _, out var syG, out var sw, out var shh);
        double sx = x + w / 2 - sw / 2, sy = y + 414 / 2.0 - shh / 2, sdy = sy - syG, fsh = fo * e;
        int sts = PushXform(sx + sw / 2, sy + shh / 2, 0.9 + 0.1 * e, 0);
        SheetFrame(sx, sy, sw, shh, fsh, e, x, y, w, h, fo, now, acc, "RENAME PRIVATE SERVER", "your label for this server - the game itself is unchanged");
        SheetField(hub, "rn", 1013, sx + 20, RenFldY(HL) + sdy, sw - 40, "NAME", "e.g. main server, friends only", RenTxt, RenErr, acc, fsh, now);
        if (RenErr != "") Txt(RenErr, sx + 20, sy + 100, sw - 40, 14, HL.fXs, FA(Alpha(C_BAD, 215), fsh), Fmt.L);
        FFMBtn(1014, sx + 20, sy + shh - 40, 124, 26, "CANCEL", 0xFFC7CBE0, fsh, 0);
        FFMBtn(1015, sx + sw - 144, sy + shh - 40, 124, 26, "SAVE", acc, fsh, RenTxt.Trim() != "" ? 1 : 0);
        Pop(sts);
    }

    // ---- SPFSavZone ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x = HL.ctx, y = HL.cty + 30, w = HL.ctw, h = 414;
        if (RenOpen)
        {
            Geom(HL, true, out var sx, out var sy, out var sw, out var shh);
            if (ux >= sx + 20 && ux <= sx + sw - 20 && uy >= RenFldY(HL) && uy <= RenFldY(HL) + 26) return 1013;
            if (uy >= sy + shh - 40 && uy <= sy + shh - 14) { if (ux >= sx + 20 && ux <= sx + 20 + 124) return 1014; if (ux >= sx + sw - 144 && ux <= sx + sw - 20) return 1015; }
            return 1016;
        }
        if (AddOpen)
        {
            Geom(HL, false, out var sx, out var sy, out var sw, out var shh);
            if (ux >= sx + 20 && ux <= sx + sw - 20)
            {
                if (uy >= AddFldY(HL, 1) && uy <= AddFldY(HL, 1) + 26) return 1006;
                if (SavTab == 2 && uy >= AddFldY(HL, 2) && uy <= AddFldY(HL, 2) + 26) return 1009;
            }
            if (uy >= sy + shh - 40 && uy <= sy + shh - 14) { if (ux >= sx + 20 && ux <= sx + 20 + 124) return 1005; if (ux >= sx + sw - 144 && ux <= sx + sw - 20) return 1004; }
            return 1001;
        }
        if (ux >= x + w - 32 && ux <= x + w && uy >= y && uy <= y + 28) return 990;
        if (uy >= y + 8 && uy <= y + 32)
        {
            double[] tbw = { Fonts.MeasureW("PLACES", Fonts.fBadge) + 46, Fonts.MeasureW("PRIVATE SERVERS", Fonts.fBadge) + 46 };
            double tbx = x + 16;
            for (int i = 1; i <= 2; i++) { if (ux >= tbx - 8 && ux <= tbx + tbw[i - 1]) return 1006 + i; tbx += tbw[i - 1] + 18; }
        }
        if (ux >= x + w - 122 && ux <= x + w - 34 && uy >= y + 8 && uy <= y + 30) return 1003;
        double lx = x + 14, ly = y + 48, lw = 192;
        if (ux >= lx && ux <= lx + lw)
        {
            for (int i = 1; i <= List().Count; i++) { double ry = ly + (i - 1) * 38; if (uy >= ry && uy <= ry + 34) return 990 + i; }
            double by = ly + 8 * 38 + 6;
            if (uy >= by && uy <= by + 26) { if (ux <= lx + 62) return 1002; if (ux >= lx + 65 && ux <= lx + 127) return 1000; if (ux >= lx + 130) return 999; }
            if (SavTab == 2 && uy >= by + 28 && uy <= by + 52) return 1012;
        }
        double dxz = x + 228, dwz = w - 242, ihz = Math.Round(dwz * 9 / 16);
        if (ux >= dxz && ux <= dxz + dwz && uy >= ly && uy <= ly + ihz) return 1010;
        if (ux >= x && ux <= x + w && uy >= y && uy <= y + h) return 1001;
        return 0;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (FfmField.Edit == "sa" && z != 1006) FfmField.End(true);
        else if (FfmField.Edit == "sl" && z != 1009) FfmField.End(true);
        else if (FfmField.Edit == "rn" && z != 1013) FfmField.End(true);
        switch (z)
        {
            case 990: Spf.ViewClose(); return true;
            case >= 991 and <= 998:
            {
                int i = z - 990;
                if (i <= List().Count) { SelSet(i); Dscr = 0; DscrT = 0.0; if (List()[i - 1].Id != "") GameInfo.Fetch(List()[i - 1].Id); }
                return true;
            }
            case 999: Del(SelIdx()); return true;
            case 1000: Use(SelIdx()); Spf.ViewClose(); return true;
            case 1007: TabSet(1); return true;
            case 1008: TabSet(2); return true;
            case 1002: Copy(); return true;
            case 1003: AddOpenSheet(); return true;
            case 1004: AddConfirm(); return true;
            case 1005: AddClose(); return true;
            case 1006: if (FfmField.Edit != "sa") FfmField.Begin("sa", 0); FfmField.Mouse(hub.PtrX); return true;
            case 1009: if (FfmField.Edit != "sl") FfmField.Begin("sl", 0); FfmField.Mouse(hub.PtrX); return true;
            case 1012: RenOpenSheet(); return true;
            case 1013: if (FfmField.Edit != "rn") FfmField.Begin("rn", 0); FfmField.Mouse(hub.PtrX); return true;
            case 1014: RenClose(); return true;
            case 1015: RenConfirm(); return true;
            case 1016 or 1001: return true;
            case 1010: BannerSkip(); return true;
        }
        return false;
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        if (AddOpen || RenOpen) return true;
        if (DscrMax > 0) { Dscr = Math.Clamp(Dscr - (int)Math.Sign(delta), 0, DscrMax); return true; }
        return true;
    }
    public static bool Animating => (Spf.View == "sav" || Spf.ViewPrev == "sav") && (Clock.Tick - Spf.ViewAt < 400 || Spf.View == "sav");
}
