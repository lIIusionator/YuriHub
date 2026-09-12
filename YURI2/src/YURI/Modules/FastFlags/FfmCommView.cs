using System.Text.RegularExpressions;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Platform;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.FastFlags;

/// <summary>
/// COMMUNITY FLAGS: the banner reel over the eight pieces of art the .ahk
/// carried, then a card per folder in the cache — GENERAL with its sets, and
/// one per game with the game's own pictures cycling, its icon, name, PLACE
/// chip, the playing / visits / likes / dislikes chips, the like bar, the
/// three-line scrolling description and its sets, each with INSERT. Every
/// list scrolls with the wheel and its own thumb (zones 577, 580+card,
/// 1900+card); the body scrolls as a whole. Zone ids are the .ahk's.
/// </summary>
public static class FfmCommView
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185, GOLD = 0xFFE8B93A, GOLD_HI = 0xFFFFE27A;
    const int HOLD = 5200, FADE = 900;                                   // FFM_COMM_HOLD / FADE
    const int DSCLH = 15, DSCVIS = 3, SETRH = 44, MAXC = 8, BANNERH = 236, GAP = 14;
    const int CMZ_SBAR = 1900, CMZ_INS = 1930;
    public static long CommAt;                                          // FFM.commAt
    public static double CmScr, CmScrT;                                 // FFM.cmScr / cmScrT
    public static long ReelOff, ReelSkipAt;                             // FFM.cmReelOff / cmReelSkipAt
    /// <summary>FFM.cm: per folder — the description scroll, the set scroll, the wrapped lines, the reel offset.</summary>
    public sealed class DS { public int D, DMax, S; public List<string> Wrap = new(); public string WrapKey = ""; public long Off, SkipAt; }
    static readonly Dictionary<string, DS> Cm = new();
    public static DS State(string key) { if (!Cm.TryGetValue(key, out var ds)) Cm[key] = ds = new DS(); return ds; }

    // ---- geometry ----
    static List<CommEntry> Comm => FfmComm.Entries;
    static double DscY() => 112;
    static double DscH() => DSCVIS * DSCLH;
    static double BarY() => DscY() + DscH() + 8;
    static int SetCap(CommEntry ent) => ent.Kind == "general" ? 6 : 2;
    static int SetVis(CommEntry ent) => Math.Clamp(ent.Sets.Count, 1, SetCap(ent));
    static double SetY(CommEntry ent) => ent.Kind == "general" ? 44 : BarY();
    static double CardH(CommEntry ent) => SetY(ent) + SetVis(ent) * SETRH + 10;
    static double ViewH() => 423;
    static double BodyY() => 38;
    static double BodyH() => ViewH() - 38 - 14;
    static double CardTop(int k)
    {
        double t = BANNERH + GAP;
        for (int i = 0; i < Math.Min(k - 1, Comm.Count); i++) t += CardH(Comm[i]) + GAP;
        return t;
    }
    static double ContentH() => Comm.Count == 0 ? BANNERH + GAP + 120 : CardTop(Comm.Count) + CardH(Comm[^1]);
    public static double ScrMax() => Math.Max(0.0, ContentH() - BodyH());
    static double BodyTrackH() => BodyH() - 8;
    static double BodyThumb() => Math.Max(24, BodyTrackH() * (BodyH() / ContentH()));
    static double DscThumb(int n) => Math.Max(16, DscH() * (DSCVIS / (double)Math.Max(1, n)));
    static double SetListH(CommEntry ent) => SetVis(ent) * SETRH;
    static double SetRowW(CommEntry ent, double lw) => lw - (SetMax(ent) > 0 ? 10 : 0);
    static double SetThumb(CommEntry ent, int n) => Math.Max(16, SetListH(ent) * (SetVis(ent) / (double)Math.Max(1, n)));
    static int SetMax(CommEntry ent) => Math.Max(0, ent.Sets.Count - SetVis(ent));

    /// <summary>FFMOpenView("comm"): the reset the .ahk did, the cache read, the sync, the game lookups.</summary>
    public static void OnOpen()
    {
        CommAt = Clock.Tick;
        CmScr = 0; CmScrT = 0;
        foreach (var ds in Cm.Values) { ds.D = 0; ds.S = 0; }
        FfmComm.IndexLoad();
        _ = FfmComm.Sync();
        QueueGames();
    }
    /// <summary>FFMCommQueueGames(): every game card's details and pictures.</summary>
    public static void QueueGames()
    {
        foreach (var ent in Comm) if (ent.Place != "") GameInfo.Fetch(ent.Place);
    }
    public static void Tick()
    {
        CmScrT = Clamp(CmScrT, 0.0, ScrMax());
        CmScr += (CmScrT - CmScr) * EK(0.28);
        if (Math.Abs(CmScr - CmScrT) < 0.4) CmScr = CmScrT;
    }
    public static bool Animating => Math.Abs(CmScr - CmScrT) > 0.4 || (ReelSkipAt != 0 && Clock.Tick - ReelSkipAt < 340) || FfmViews.View == "comm";

    // ---- the view ----
    public static void Draw(HubSurface hub, double ff, long now, uint acc, double dx, double dy2)
    {
        var HL = hub.HL;
        double e = Ease3(Clamp((now - FfmViews.ViewAt) / 280.0, 0.0, 1.0));
        double t = FfmViews.View == "comm" ? e : FfmViews.ViewPrev == "comm" ? 1 - e : 0;
        if (t <= 0.01) return;
        double fo = ff * t;
        double x = HL.ctx + dx, y = HL.cty + dy2 + 30, w = HL.ctw;
        double h = ViewH();
        FillRR(x - 10, HL.cty + dy2 - 6, w + 20, h + 54, 14, SBrush(FA(Alpha(0x06070E, R(215 * t)), ff)));
        int st = PushXform(x + w / 2, y + h / 2, 0.96 + 0.04 * t, 0);
        FfmViews.HubViewChrome(x, y, w, h, fo, now, acc);
        FillRR(x, y + 10, 3, 22, 1.5, SBrush(FA(Alpha(acc, 200), fo)));
        Txt("COMMUNITY FLAGS", x + 16, y + 10, 180, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 235), fo), Fmt.L);
        bool syncing = FfmComm.Syncing;
        string hdr = syncing ? "refreshing from the repository..." : FfmComm.Msg != "" ? "offline - " + FfmComm.Msg : "sets shared by other players";
        Txt(hdr, x + 176, y + 12, 260, 16, HL.fS, FA((FfmComm.Msg != "" && !syncing) ? 0x80FBBF24 : 0x66C7CBE0, fo), Fmt.L);
        double hvS = hub.Hv(578);
        double sbx = x + w - 116, sby = y + 6;
        FillRR(sbx, sby, 76, 22, 7, SBrush(FA(Alpha(acc, syncing ? 18 : R(26 + 40 * hvS)), fo)));
        StrokeRR(sbx, sby, 76, 22, 7, Pen(FA(Alpha(acc, syncing ? 60 : R(90 + 90 * hvS)), fo), 1));
        Txt(syncing ? "REFRESHING" : "REFRESH", sbx, sby + 3, 76, 16, HL.fXs, FA(Alpha(syncing ? 0xFFC7CBE0 : AccHi(acc, 0.4), syncing ? 120 : R(200 + 55 * hvS)), fo), Fmt.C);
        double hvX = hub.Hv(446);
        var pnX = Pen(FA(Alpha(hvX > 0.2 ? acc : 0xFFC7CBE0, 130 + 120 * hvX), fo), 1.6);
        double cxx = x + w - 18, cyy = y + 14;
        Line(cxx - 5, cyy - 5, cxx + 5, cyy + 5, pnX); Line(cxx + 5, cyy - 5, cxx - 5, cyy + 5, pnX);
        CmScrT = Clamp(CmScrT, 0.0, ScrMax());
        double bodyY = y + BodyY(), bodyH = BodyH();
        int stBody = PushG();
        ClipRR(x + 8, bodyY, w - 16, bodyH, 10);
        double scr = CmScr;
        double bt = Ease3(Clamp((t - 0.25) / 0.75, 0.0, 1.0));
        double by = bodyY - scr + (1 - bt) * 10;
        Banner(hub, x + 14, by, w - 28, BANNERH, fo * bt, now, acc);
        StrokeRR(x + 14, by, w - 28, BANNERH, 10, Pen(FA(Alpha(GOLD, R(90 * bt)), fo), 1));
        Txt("COMMUNITY", x + 32, by + BANNERH - 46, 300, 20, Fonts.fBrand, FA(Alpha(GOLD_HI, R(240 * bt)), fo), Fmt.L);
        Txt("flag sets shared by other players", x + 32, by + BANNERH - 26, 320, 15, Fonts.fHint, FA(Alpha(GOLD, R(170 * bt)), fo), Fmt.L);
        double mt2 = Ease3(Clamp((t - 0.45) / 0.55, 0.0, 1.0));
        double mx = x + 14, mw = w - 28;
        if (Comm.Count == 0)
        {
            double ey = bodyY - scr + BANNERH + GAP;
            FillRR(mx, ey, mw, 120, 10, VBrush(mx, ey, mw, 120, FA(0xFF12152A, fo * mt2), FA(0xFF0C0E17, fo * mt2)));
            StrokeRR(mx, ey, mw, 120, 10, Pen(FA(Alpha(0xFFFFFF, 20), fo * mt2), 1));
            Txt(syncing ? "fetching the community sets..." : FfmComm.Msg != "" ? FfmComm.Msg : "no sets cached yet", mx, ey + 44, mw, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 210), fo * mt2), Fmt.C);
            Txt(syncing ? "it ran at launch; this is a refresh" : "they are downloaded once and kept for offline use", mx, ey + 68, mw, 16, Fonts.fHint, FA(0x66C7CBE0, fo * mt2), Fmt.C);
        }
        for (int gi = 1; gi <= Math.Min(Comm.Count, MAXC); gi++)
        {
            double sy = bodyY - scr + CardTop(gi) + (1 - mt2) * 8;
            if (sy > bodyY + bodyH || sy + CardH(Comm[gi - 1]) < bodyY) continue;
            GameCard(hub, gi, Comm[gi - 1], mx, sy, mw, fo * mt2, now, acc);
        }
        Pop(stBody);
        if (ScrMax() > 0)
        {
            double sbA = Math.Max(hub.Hv(577), HL.drag == 23 ? 1.0 : 0.0);
            double sw = 3 + 4 * sbA;
            double sxB = x + w - 7 - 3 * sbA;
            double trH = BodyTrackH(), thH = BodyThumb();
            double tyB = bodyY + 4 + (trH - thH) * (scr / ScrMax());
            FillRR(sxB, bodyY + 4, sw, trH, sw / 2, SBrush(FA(Alpha(0xFFFFFF, 14 + 28 * sbA), fo)));
            FillRR(sxB, tyB, sw, thH, sw / 2, VBrush(sxB, tyB, sw, thH, FA(Alpha(AccHi(acc, 0.35), 200 + 55 * sbA), fo), FA(Alpha(acc, 150 + 60 * sbA), fo)));
        }
        Pop(st);
    }

    /// <summary>FFMCommBanner: the eight pieces of art, each held 5.2 s then cross-faded, with the gold sheen and the dots.</summary>
    static void Banner(HubSurface hub, double x, double y, double w, double h, double fo, long now, uint acc)
    {
        if (HubState.LowPerf) { FillRR(x, y, w, h, 10, SBrush(FA(0xFF0C0E17, fo))); return; }
        const int n = 8;
        int per = HOLD + FADE;
        long tot = (long)(DecT(now) + ReelOff) % (per * n);
        int idx = (int)(tot / per) + 1;
        long into = tot % per;
        double ft = into > HOLD ? Ease3(Clamp((into - HOLD) / (double)FADE, 0.0, 1.0)) : 0.0;
        int nxt = idx % n + 1;
        int sv = PushG();
        ClipRR(x, y, w, h, 10);
        FillRR(x, y, w, h, 10, SBrushP(FA(0xFF0B0A06, fo)));
        double dr = (Math.Sin(DecT(now) * 0.00023) + 1) / 2;
        double py = 0.02 + 0.40 * dr;
        Img.FitRR(Img.Asset("comm_art_" + idx + ".jpg"), x, y, w, h, 10, fo, 1.06, 0.5, py);
        if (ft > 0.001) Img.FitRR(Img.Asset("comm_art_" + nxt + ".jpg"), x, y, w, h, 10, ft * fo, 1.06, 0.5, py);
        FillRect(x, y + h * 0.42, w, h * 0.58, VBrushP(x, y + h * 0.42, w, h * 0.58, FA(Alpha(0x000000, 0), fo), FA(Alpha(0x000000, 215), fo)));
        FillRect(x, y, w * 0.55, h, HBrushP(x, y, w * 0.55, h, FA(Alpha(0x000000, 170), fo), FA(Alpha(0x000000, 0), fo)));
        double sx = x - w * 0.3 + (DecT(now) * 0.03) % (w * 1.6);
        FillRect(sx, y, w * 0.22, h, HBrushP(sx, y, w * 0.22, h, FA(Alpha(GOLD, 0), fo), FA(Alpha(GOLD, 26), fo)));
        double dr2 = 2.6, gp2 = 8;
        double bx2 = x + w - 12 - ((n - 1) * gp2 + dr2 * 2);
        for (int k = 1; k <= n; k++)
        {
            double on = k == idx ? 1 - ft : k == nxt ? ft : 0;
            double rr = dr2 + 0.7 * on;
            FillEll(bx2 + (k - 1) * gp2 - rr + dr2, y + h - 14 - rr, rr * 2, rr * 2, SBrushP(FA(Alpha(k == idx || k == nxt ? GOLD_HI : 0xFFFFFFFF, R(60 + 175 * on)), fo)));
        }
        BannerSkipHover(hub, 2010, ReelSkipAt, x, y, w, h, n, fo, now, acc, false);
        Pop(sv);
    }

    /// <summary>BannerSkipHover: the dimmed veil and the SKIP pill a hovered reel shows.</summary>
    public static void BannerSkipHover(HubSurface hub, int z, long pressAt, double dx, double ly, double dw, double ih, int n, double fo, long now, uint acc, bool compact)
    {
        if (n < 2) return;
        double hv = Ease3(hub.Hv(z));
        double pr = pressAt != 0 ? Clamp(1 - (now - pressAt) / 320.0, 0.0, 1.0) : 0.0;
        if (hv < 0.004 && pr < 0.004) return;
        double e = Math.Max(hv, pr);
        double rad = compact ? 8 : 10;
        int stC = PushG();
        ClipRR(dx, ly, dw, ih, rad);
        FillRR(dx, ly, dw, ih, rad, SBrushP(ElA(FA(Alpha(0x05060C, R(120 * hv + 60 * pr)), fo))));
        double cx = dx + dw / 2, cy = ly + ih / 2 - (compact ? 0 : 6);
        double sc = 0.88 + 0.12 * Ease3(hv) + 0.06 * pr;
        int st = PushXform(cx, cy + (compact ? 0 : 6), sc, 0);
        double pw2 = compact ? 46 : 96, ph2 = compact ? 24 : 30;
        FillRR(cx - pw2 / 2, cy - ph2 / 2, pw2, ph2, ph2 / 2, SBrush(FA(Alpha(0x10131F, R(150 * e)), fo)));
        StrokeRR(cx - pw2 / 2, cy - ph2 / 2, pw2, ph2, ph2 / 2, Pen(FA(Alpha(acc, R(150 * e)), fo), 1.2));
        uint ink = HubState.ThT > 0.5 ? Mix(acc, 0xFF23242B, 0.55) : AccHi(acc, 0.45);
        double gx = compact ? cx - 6 : cx - 30, gy = cy;
        var pn = Pen(FA(Alpha(ink, R(235 * e)), fo), compact ? 1.6 : 1.8);
        for (int i = 1; i <= 2; i++)
        {
            double ph = Clamp(Math.Sin(DecT(now) * 0.005 - (i - 1) * 0.7), 0.0, 1.0);
            double ox = gx + (i - 1) * 6 + ph * 2.2;
            Line(ox - 3, gy - 5, ox + 2, gy, pn); Line(ox + 2, gy, ox - 3, gy + 5, pn);
        }
        Line(gx + 11, gy - 5.5, gx + 11, gy + 5.5, pn);
        if (!compact) Txt("SKIP", cx - 8, cy - 8, 44, 16, Fonts.fBadge, FA(Alpha(ink, R(235 * e)), fo), Fmt.L);
        Pop(st);
        Pop(stC);
    }

    /// <summary>FFMCommReelSkip / FFMCommThumbSkip: jump the reel to its next picture.</summary>
    public static void ReelSkip()
    {
        if (HubState.LowPerf) return;
        const int n = 8; int per = HOLD + FADE;
        long now = Clock.Tick;
        long into = (long)(DecT(now) + ReelOff) % (per * n) % per;
        if (into >= HOLD) return;
        ReelOff = (ReelOff + (HOLD - into)) % (per * n);
        ReelSkipAt = now;
    }
    public static void ThumbSkip(int gi)
    {
        if (gi < 1 || gi > Comm.Count) return;
        var ent = Comm[gi - 1];
        if (ent.Kind == "general" || ent.Place == "") return;
        int nB = GameInfo.ThumbN(ent.Place);
        if (nB < 2) return;
        var ds = State(ent.Folder);
        int per = HOLD + FADE;
        long now = Clock.Tick;
        long into = ((now - CommAt + ds.Off) % (per * nB)) % per;
        if (into >= HOLD) return;
        ds.Off = (ds.Off + (HOLD - into)) % (per * nB);
        ds.SkipAt = now;
    }

    // ---- the cards ----
    static void GameCard(HubSurface hub, int gi, CommEntry ent, double mx, double sy, double mw, double fo, long now, uint acc)
    {
        var HL = hub.HL;
        var ds = State(ent.Folder);
        if (ent.Kind == "general") { GeneralCard(hub, gi, ent, ds, mx, sy, mw, fo, now, acc); return; }
        string pl = ent.Place;
        GameInfo.Fetch(pl);
        var r = GameInfo.Get(pl);
        double mh = CardH(ent);
        FillRR(mx, sy, mw, mh, 10, VBrush(mx, sy, mw, mh, FA(0xFF12152A, fo), FA(0xFF0C0E17, fo)));
        MiniBackdrop(mx, sy, mw, mh, 10, acc, fo, now, 0.8);
        StrokeRR(mx, sy, mw, mh, 10, Pen(FA(Alpha(0xFFFFFF, 20), fo), 1));
        double tw2 = 148, th2 = 84, tx2 = mx + 12, ty2 = sy + 12;
        FillRR(tx2 - 4, ty2 - 3, tw2 + 8, th2 + 8, 11, SBrush(FA(Alpha(acc, 20), fo)));
        FillRR(tx2, ty2, tw2, th2, 8, SBrush(FA(0xFF080A12, fo)));
        int nB = GameInfo.ThumbN(pl);
        Avalonia.Media.Imaging.Bitmap? bmT = null, bmN = null; double ft = 0.0; int idx = 1, nxt = 1;
        if (nB > 0)
        {
            int per = HOLD + FADE;
            long tot = (now - CommAt + ds.Off) % (per * nB);
            idx = (int)(tot / per) + 1;
            long into = tot % per;
            ft = into > HOLD && !HubState.LowPerf ? Ease3(Clamp((into - HOLD) / (double)FADE, 0.0, 1.0)) : 0.0;
            nxt = idx % nB + 1;
            bmT = GameInfo.ThumbAt(pl, idx);
            if (bmT is null && idx != 1) bmT = GameInfo.ThumbAt(pl, 1);
            if (ft > 0.001 && nxt != idx) bmN = GameInfo.ThumbAt(pl, nxt);
        }
        if (bmT is not null)
        {
            int stB = PushG();
            ClipRR(tx2, ty2, tw2, th2, 8);
            Img.DrawRR(bmT, tx2, ty2, tw2, th2, 8, fo);
            if (bmN is not null)
            {
                Img.DrawRR(bmN, tx2, ty2, tw2, th2, 8, fo * ft);
                double wsh = Math.Sin(3.14159 * ft);
                FillRect(tx2, ty2, tw2, th2, VBrush(tx2, ty2, tw2, th2, FA(Alpha(acc, R(26 * wsh)), fo), Alpha(acc, 0)));
            }
            Pop(stB);
        }
        else Txt(nB > 0 ? "loading image..." : "loading...", tx2, ty2 + th2 / 2 - 8, tw2, 16, HL.fS, FA(0x6EC7CBE0, fo), Fmt.C);
        StrokeRR(tx2, ty2, tw2, th2, 8, Pen(FA(Alpha(0xFFFFFF, 26), fo), 1));
        if (nB > 1)
        {
            int npk = Math.Min(nB, 8);
            FillRR(tx2 + 6, ty2 + th2 - 15, npk * 8 + 6, 11, 5.5, SBrush(FA(Alpha(0x05060C, 120), fo)));
            for (int pk = 1; pk <= npk; pk++)
            {
                double on = pk == idx ? 1 - ft : pk == nxt ? ft : 0.0;
                double rp = 1.8 + 0.5 * on;
                FillEll(tx2 + 5 + pk * 8 - rp, ty2 + th2 - 9.5 - rp, rp * 2, rp * 2, SBrush(FA(Alpha(on > 0.01 ? AccHi(acc, 0.3) : 0xFFFFFFFF, R(90 + 150 * on)), fo)));
            }
            BannerSkipHover(hub, 2020 + gi, ds.SkipAt, tx2, ty2, tw2, th2, nB, fo, now, acc, true);
        }
        double cx2 = tx2 + tw2 + 14;
        double ntx = cx2;
        var ico = GameInfo.Ico(pl);
        if (ico is not null)
        {
            double isz = 34, ibx = cx2, iby = sy + 12;
            Img.DrawRR(ico, ibx, iby, isz, isz, 8, fo);
            StrokeRR(ibx - 1.5, iby - 1.5, isz + 3, isz + 3, 9, Pen(FA(Alpha(acc, 150), fo), 1.2));
            ntx = cx2 + isz + 12;
        }
        string gname = StripEmoji(r is not null && r.Nm != "" ? r.Nm : ent.Folder);
        double ntw = mx + mw - 14 - ntx;
        Txt(FFMElide(gname, Fonts.fStatus, ntw), ntx, sy + 9, ntw, 20, Fonts.fStatus, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.15), 240), fo), Fmt.L);
        string plS = "PLACE " + pl;
        double plw = Fonts.MeasureW(plS, HL.fXs) + 18;
        FillRR(ntx, sy + 31, plw, 17, 6, SBrush(FA(Alpha(0xFFFFFF, 12), fo)));
        StrokeRR(ntx, sy + 31, plw, 17, 6, Pen(FA(Alpha(acc, 55), fo), 1));
        Txt(plS, ntx, sy + 33, plw, 14, HL.fXs, FA(Alpha(0xFFC7CBE0, 200), fo), Fmt.C);
        if (r is not null)
        {
            long up = r.Up, dn = r.Down, tot = up + dn;
            double cwc = (mw - (cx2 - mx) - 24) / 4;
            Chip(hub, cx2, sy + 56, cwc - 6, Num(r.Playing), "playing", C_ON, fo, 24);
            Chip(hub, cx2 + cwc, sy + 56, cwc - 6, Num(r.Visits), "visits", 0xFFE8EAF6, fo, 24);
            Chip(hub, cx2 + cwc * 2, sy + 56, cwc - 6, Num(up), "likes", C_ON, fo, 24);
            Chip(hub, cx2 + cwc * 3, sy + 56, cwc - 6, Num(dn), "dislikes", 0xFFF87171, fo, 24);
            if (tot != 0)
            {
                double rw = mw - (cx2 - mx) - 24;
                double ry = sy + 86;
                int pct = (int)Math.Round(100.0 * up / tot);
                double pw = Fonts.MeasureW(pct + "%", HL.fXs) + 14;
                FillRR(cx2, ry - 2, pw, 16, 5, SBrush(FA(Alpha(C_ON, 40), fo)));
                StrokeRR(cx2, ry - 2, pw, 16, 5, Pen(FA(Alpha(C_ON, 120), fo), 1));
                Txt(pct + "%", cx2, ry - 1, pw, 14, HL.fXs, FA(Alpha(AccHi(C_ON, 0.3), 240), fo), Fmt.C);
                string favS = Num(r.Favs);
                double fw = Fonts.MeasureW(favS, HL.fXs) + 20;
                MiniHeart(cx2 + rw - fw + 7, ry + 6, 7, FA(Alpha(0xFFF472B6, 220), fo));
                Txt(favS, cx2 + rw - fw + 16, ry - 1, fw - 16, 14, HL.fXs, FA(0x9AC7CBE0, fo), Fmt.R);
                double bx0 = cx2 + pw + 10, bw0 = rw - pw - 10 - fw - 10;
                if (bw0 > 30)
                {
                    FillRR(bx0, ry + 4, bw0, 4, 2, SBrush(FA(Alpha(0xFFFFFF, 16), fo)));
                    double lw = bw0 * up / tot;
                    FillRR(bx0, ry + 4, Math.Max(lw, 4), 4, 2, HBrush(bx0, ry + 4, Math.Max(lw, 4), 4, FA(Alpha(AccHi(C_ON, 0.3), 230), fo), FA(Alpha(C_ON, 200), fo)));
                    if (bw0 - lw > 6) FillRR(bx0 + lw + 2, ry + 4, bw0 - lw - 2, 4, 2, SBrush(FA(Alpha(0xFFF87171, 170), fo)));
                }
            }
        }
        else Txt("loading game details...", cx2, sy + 58, 240, 16, Fonts.fHint, FA(0x7EC7CBE0, fo), Fmt.L);
        // ---- the description ----
        double dscX = mx + 12, dscW = mw - 24;
        double dscY = sy + DscY(), dscH = DscH();
        FillRR(dscX - 6, dscY - 4, dscW + 10, dscH + 8, 8, SBrush(FA(0xFF090B14, fo)));
        MicroBackdrop(dscX - 6, dscY - 4, dscW + 10, dscH + 8, 8, acc, fo, now, 0.6);
        StrokeRR(dscX - 6, dscY - 4, dscW + 10, dscH + 8, 8, Pen(FA(Alpha(0xFFFFFF, 14), fo), 1));
        FillRR(dscX - 5, dscY, 2, dscH, 1, VBrush(dscX - 5, dscY, 2, dscH, FA(Alpha(AccHi(acc, 0.4), 220), fo), FA(Alpha(acc, 60), fo)));
        string desc = r is not null && r.Desc != "" ? r.Desc : "";
        if (desc == "") desc = r is not null ? "no description" : "fetching details...";
        string wk = dscW + "|" + desc;
        if (ds.WrapKey != wk)
        {
            ds.WrapKey = wk;
            ds.Wrap = new List<string>();
            foreach (var para in desc.Replace("\r", "").Split('\n'))
            {
                if (para.Trim() == "") { ds.Wrap.Add(""); continue; }
                ds.Wrap.AddRange(Detail.WrapText(para, Fonts.fHint, dscW - 14, 400));
            }
        }
        var dLines = ds.Wrap;
        ds.DMax = Math.Max(0, dLines.Count - DSCVIS);
        ds.D = Math.Clamp(ds.D, 0, ds.DMax);
        int stD = PushG();
        ClipRR(dscX, dscY - 2, dscW, dscH + 4, 6);
        for (int k = 1; k <= DSCVIS; k++)
        {
            int i3 = ds.D + k;
            if (i3 > dLines.Count) break;
            Txt(dLines[i3 - 1], dscX, dscY + (k - 1) * DSCLH, dscW - 12, DSCLH, Fonts.fHint, FA(0x7AC7CBE0, fo), Fmt.L);
        }
        Pop(stD);
        if (ds.DMax > 0)
        {
            double sbD = Math.Max(hub.Hv(580 + gi), (HL.drag == 24 && HL.dragZone == 580 + gi) ? 1.0 : 0.0);
            double swD = 3 + 3 * sbD;
            double sxD = dscX + dscW - 3 - 2 * sbD;
            double thh = DscThumb(dLines.Count);
            double tyy = dscY + (dscH - thh) * (ds.D / (double)ds.DMax);
            FillRR(sxD, dscY, swD, dscH, swD / 2, SBrush(FA(Alpha(0xFFFFFF, 16 + 26 * sbD), fo)));
            FillRR(sxD, tyy, swD, thh, swD / 2, VBrush(sxD, tyy, swD, thh, FA(Alpha(AccHi(acc, 0.35), 200 + 55 * sbD), fo), FA(Alpha(acc, 150 + 60 * sbD), fo)));
        }
        SetList(hub, gi, ent, ds, mx + 12, sy + SetY(ent), mw - 24, fo, now, acc);
    }

    static void GeneralCard(HubSurface hub, int gi, CommEntry ent, DS ds, double mx, double sy, double mw, double fo, long now, uint acc)
    {
        var HL = hub.HL;
        double mh = CardH(ent);
        FillRR(mx, sy, mw, mh, 10, VBrush(mx, sy, mw, mh, FA(0xFF141026, fo), FA(0xFF0C0E17, fo)));
        MiniBackdrop(mx, sy, mw, mh, 10, acc, fo, now, 0.8);
        StrokeRR(mx, sy, mw, mh, 10, Pen(FA(Alpha(0xFFFFFF, 20), fo), 1));
        FillRR(mx + 14, sy + 12, 3, 20, 1.5, SBrush(FA(Alpha(GOLD, 200), fo)));
        Txt("GENERAL", mx + 26, sy + 11, 200, 20, Fonts.fBadge, FA(Alpha(GOLD_HI, 240), fo), Fmt.L);
        Txt($"{ent.Sets.Count} set{(ent.Sets.Count == 1 ? "" : "s")}  -  not tied to any game", mx + 108, sy + 13, mw - 130, 16, HL.fS, FA(0x72C7CBE0, fo), Fmt.L);
        SetList(hub, gi, ent, ds, mx + 12, sy + SetY(ent), mw - 24, fo, now, acc);
    }

    static void SetList(HubSurface hub, int gi, CommEntry ent, DS ds, double lx, double ly, double lw, double fo, long now, uint acc)
    {
        var HL = hub.HL;
        int vis = SetVis(ent);
        double lh = SetListH(ent);
        int n = ent.Sets.Count;
        ds.S = Math.Clamp(ds.S, 0, SetMax(ent));
        if (n == 0) { Txt("no sets in this folder", lx, ly + lh / 2 - 8, lw, 16, Fonts.fHint, FA(0x66C7CBE0, fo), Fmt.C); return; }
        for (int slot = 1; slot <= vis; slot++)
        {
            int si = ds.S + slot;
            if (si > n) break;
            SetRow(hub, CMZ_INS + (gi - 1) * 8 + slot, ent.Sets[si - 1], lx, ly + (slot - 1) * SETRH, SetRowW(ent, lw), fo, now, acc);
        }
        if (SetMax(ent) > 0)
        {
            int z = CMZ_SBAR + gi;
            double sbS = Math.Max(hub.Hv(z), (HL.drag == 25 && HL.dragZone == z) ? 1.0 : 0.0);
            double swS = 3 + 3 * sbS;
            double sxS = lx + lw - 3 - 2 * sbS;
            double thS = SetThumb(ent, n);
            double tyS = ly + (lh - thS) * (ds.S / (double)SetMax(ent));
            FillRR(sxS, ly, swS, lh, swS / 2, SBrush(FA(Alpha(0xFFFFFF, 16 + 26 * sbS), fo)));
            FillRR(sxS, tyS, swS, thS, swS / 2, VBrush(sxS, tyS, swS, thS, FA(Alpha(AccHi(acc, 0.35), 200 + 55 * sbS), fo), FA(Alpha(acc, 150 + 60 * sbS), fo)));
        }
    }

    static void SetRow(HubSurface hub, int z, CommSet st, double bx, double by, double bw, double fo, long now, uint acc)
    {
        var HL = hub.HL;
        double bh2 = SETRH - 6;
        double hv = hub.Hv(z);
        FillRR(bx, by, bw, bh2, 9, VBrush(bx, by, bw, bh2, FA(Mix(0xFF171A34, 0xFF1E2346, hv), fo), FA(0xFF0C0E17, fo)));
        MiniBackdrop(bx, by, bw, bh2, 9, acc, fo, now, 0.9);
        StrokeRR(bx, by, bw, bh2, 9, Pen(FA(Alpha(acc, 50 + 70 * hv), fo), 1.1));
        FillRR(bx + 9, by + 8, 3, bh2 - 16, 1.5, VBrush(bx + 9, by + 8, 3, bh2 - 16, FA(Alpha(AccHi(acc, 0.4), 240), fo), FA(Alpha(acc, 150), fo)));
        string ini = st.Author.Trim().Length > 0 ? st.Author.Trim()[..1].ToUpperInvariant() : "?";
        double ccx = bx + 30, ccy = by + bh2 / 2;
        FillEll(ccx - 11, ccy - 11, 22, 22, VBrush(ccx - 11, ccy - 11, 22, 22, FA(Alpha(AccHi(acc, 0.35), 230), fo), FA(Alpha(acc, 190), fo)));
        Ell(ccx - 11, ccy - 11, 22, 22, Pen(FA(Alpha(AccHi(acc, 0.6), 200), fo), 1));
        TxtP(ini, ccx - 11, ccy - 8, 22, 16, HL.fS, FA(0xFFFFFFFF, fo), Fmt.C);
        double tx0 = bx + 48;
        double aw = Fonts.MeasureW(st.Author, Fonts.fBadge);
        Txt(FFMElide(st.Author, Fonts.fBadge, bw - 156), tx0, by + 5, bw - 156, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.22), 240), fo), Fmt.L);
        string cntS = st.N + " flags";
        double cbw = 14 + Fonts.MeasureW(cntS, HL.fXs);
        if (tx0 + aw + 8 + cbw < bx + bw - 108)
        {
            FillRR(tx0 + aw + 8, by + 5, cbw, 15, 5, SBrush(FA(Alpha(acc, 34), fo)));
            StrokeRR(tx0 + aw + 8, by + 5, cbw, 15, 5, Pen(FA(Alpha(acc, 110), fo), 1));
            Txt(cntS, tx0 + aw + 8, by + 4, cbw, 15, HL.fXs, FA(Alpha(AccHi(acc, 0.4), 235), fo), Fmt.C);
        }
        Txt(FFMElide(st.File, Fonts.fHint, bw - 156), tx0, by + 21, bw - 156, 14, Fonts.fHint, FA(st.Have ? 0x72C7CBE0u : 0x50C7CBE0u, fo), Fmt.L);
        if (st.Have) FFMBtn(z, bx + bw - 96, by + bh2 / 2 - 12, 88, 24, "INSERT", acc, fo, 1);
        else Txt("not cached", bx + bw - 96, by + bh2 / 2 - 8, 88, 16, HL.fXs, FA(Alpha(AMBER, 190), fo), Fmt.C);
    }

    static void Chip(HubSurface hub, double cx, double cy, double cw, string val, string label, uint col, double fo, double ch = 28)
    {
        FillRR(cx, cy, cw, ch, 6, VBrush(cx, cy, cw, ch, FA(0x1CFFFFFF, fo), FA(0x08FFFFFF, fo)));
        StrokeRR(cx, cy, cw, ch, 6, Pen(FA(Alpha(0xFFFFFF, 18), fo), 1));
        FillRR(cx + 8, cy + ch - 3, cw - 16, 1.5, 0.75, SBrush(FA(Alpha(col, 120), fo)));
        Txt(val, cx, cy + 1, cw, 13, hub.HL.fS, FA(Alpha(col, 240), fo), Fmt.C);
        Txt(label, cx, cy + ch - 15, cw, 12, hub.HL.fXs, FA(0x6EC7CBE0, fo), Fmt.C);
    }

    static readonly Regex EmojiRx = new("[\\p{So}\\p{Cs}\\u2600-\\u27BF\\u2B00-\\u2BFF\\u2300-\\u23FF\\u2190-\\u21FF\\uFE00-\\uFE0F\\u200D\\u20E3\\uE000-\\uF8FF]", RegexOptions.Compiled);
    /// <summary>StripEmoji: game names without their decorations, brackets tightened, spaces collapsed.</summary>
    public static string StripEmoji(string s)
    {
        s = EmojiRx.Replace(s, "");
        s = Regex.Replace(s, "\\[\\s+", "["); s = Regex.Replace(s, "\\s+\\]", "]");
        s = Regex.Replace(s, "\\(\\s+", "("); s = Regex.Replace(s, "\\s+\\)", ")");
        return Regex.Replace(s, "\\s{2,}", " ").Trim();
    }
    /// <summary>FFMNum: thousands separators; "-" for nothing.</summary>
    public static string Num(long v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

    // ---- zones, clicks, wheel, drags ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x = HL.ctx, y = HL.cty + 30, w = HL.ctw;
        double h = ViewH();
        if (ux >= x + w - 34 && ux <= x + w && uy >= y && uy <= y + 30) return 446;
        if (ux >= x + w - 116 && ux <= x + w - 40 && uy >= y + 6 && uy <= y + 28) return 578;
        double bodyY = y + BodyY(), bodyH = BodyH();
        double mx = x + 14, mw = w - 28;
        double bx = mx + 12, bw = mw - 24;
        if (!HubState.LowPerf && uy >= bodyY && uy <= bodyY + bodyH)
        {
            double hby = bodyY - CmScr;
            if (ux >= mx && ux <= mx + mw && uy >= hby && uy <= hby + BANNERH) return 2010;
        }
        if (uy >= bodyY && uy <= bodyY + bodyH)
        {
            for (int gi = 1; gi <= Math.Min(Comm.Count, MAXC); gi++)
            {
                var ent = Comm[gi - 1];
                var ds = State(ent.Folder);
                if (ent.Kind != "general" && ent.Place != "" && GameInfo.ThumbN(ent.Place) > 1)
                {
                    double tty = bodyY - CmScr + CardTop(gi) + 12;
                    if (ux >= mx + 12 && ux <= mx + 160 && uy >= Math.Max(tty, bodyY) && uy <= Math.Min(tty + 84, bodyY + bodyH)) return 2020 + gi;
                }
                double ly = bodyY - CmScr + CardTop(gi) + SetY(ent);
                double lw = SetRowW(ent, mw - 24);
                if (ux >= bx + lw - 96 && ux <= bx + lw - 8)
                    for (int slot = 1; slot <= SetVis(ent); slot++)
                    {
                        if (ds.S + slot > ent.Sets.Count) break;
                        if (!ent.Sets[ds.S + slot - 1].Have) continue;
                        double ry = ly + (slot - 1) * SETRH;
                        double ty = ry + SETRH / 2.0 - 18;
                        if (uy >= Math.Max(ty, bodyY) && uy <= Math.Min(ty + 24, bodyY + bodyH)) return CMZ_INS + (gi - 1) * 8 + slot;
                    }
                if (SetMax(ent) > 0 && ux >= bx + lw - 4 && ux <= bx + lw + 16 && uy >= Math.Max(ly, bodyY) && uy <= Math.Min(ly + SetListH(ent), bodyY + bodyH)) return CMZ_SBAR + gi;
            }
        }
        if (uy >= bodyY && uy <= bodyY + bodyH)
        {
            if (ScrMax() > 0 && ux >= x + w - 18 && ux <= x + w - 2) return 577;
            if (ux >= mx + mw - 26 && ux <= mx + mw - 6)
                for (int gi = 1; gi <= Math.Min(Comm.Count, MAXC); gi++)
                {
                    if (Comm[gi - 1].Kind == "general") continue;
                    var ds = State(Comm[gi - 1].Folder);
                    if (ds.DMax <= 0) continue;
                    double dy4 = bodyY - CmScr + CardTop(gi) + DscY();
                    if (uy >= Math.Max(dy4, bodyY) && uy <= Math.Min(dy4 + DscH(), bodyY + bodyH)) return 580 + gi;
                }
        }
        if (ux >= x - 10 && ux <= x + w + 10 && uy >= HL.cty - 6 && uy <= HL.cty + h + 48) return 447;
        return 440;
    }

    static int DscAt(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x = HL.ctx, y = HL.cty + 30, w = HL.ctw;
        double bodyY = y + BodyY(), bodyH = BodyH();
        if (uy < bodyY || uy > bodyY + bodyH) return 0;
        double dscX = x + 14 + 12, dscW = w - 28 - 24;
        if (ux < dscX || ux > dscX + dscW) return 0;
        for (int gi = 1; gi <= Math.Min(Comm.Count, MAXC); gi++)
        {
            if (Comm[gi - 1].Kind == "general") continue;
            double dy3 = bodyY - CmScr + CardTop(gi) + DscY();
            if (uy >= dy3 - 2 && uy <= dy3 + DscH() + 2) return gi;
        }
        return 0;
    }
    static int SetAt(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x = HL.ctx, y = HL.cty + 30, w = HL.ctw;
        double bodyY = y + BodyY(), bodyH = BodyH();
        if (uy < bodyY || uy > bodyY + bodyH) return 0;
        double lx = x + 14 + 12, lw = w - 28 - 24;
        if (ux < lx || ux > lx + lw) return 0;
        for (int gi = 1; gi <= Math.Min(Comm.Count, MAXC); gi++)
        {
            var ent = Comm[gi - 1];
            double ly = bodyY - CmScr + CardTop(gi) + SetY(ent);
            if (uy >= ly && uy <= ly + SetListH(ent)) return gi;
        }
        return 0;
    }

    public static bool Click(HubSurface hub, int z)
    {
        if (z == 446 || z == 440) { FfmViews.CloseView(); return true; }
        if (z == 578)
        {
            if (!FfmComm.Syncing) _ = FfmComm.Sync(true);
            else Ffm.Say("COMMUNITY SYNC ALREADY RUNNING - ONE MOMENT", AMBER);
            return true;
        }
        if (z == 2010) { ReelSkip(); return true; }
        if (z >= 2021 && z <= 2020 + MAXC) { ThumbSkip(z - 2020); return true; }
        if (z > CMZ_INS && z <= CMZ_INS + MAXC * 8)
        {
            int gi = (z - CMZ_INS - 1) / 8, slot = (z - CMZ_INS - 1) % 8 + 1;
            if (gi < Comm.Count) FfmComm.Insert(gi, State(Comm[gi].Folder).S + slot - 1);
            return true;
        }
        if (z == 447) return true;
        return false;
    }
    /// <summary>The scrollbar presses: 577 the body, 580+card a description, 1900+card a set list.</summary>
    public static bool Press(HubSurface hub, int z, double uy)
    {
        if (z == 577) { hub.HL.drag = 23; hub.HL.dragZone = z; DragBody(hub, uy); return true; }
        if (z >= 581 && z <= 580 + MAXC) { hub.HL.drag = 24; hub.HL.dragZone = z; DragDsc(hub, z - 580, uy); return true; }
        if (z > CMZ_SBAR && z <= CMZ_SBAR + MAXC) { hub.HL.drag = 25; hub.HL.dragZone = z; DragSet(hub, z - CMZ_SBAR, uy); return true; }
        return false;
    }
    public static void Drag(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        if (HL.drag == 23) DragBody(hub, uy);
        else if (HL.drag == 24) DragDsc(hub, HL.dragZone - 580, uy);
        else if (HL.drag == 25) DragSet(hub, HL.dragZone - CMZ_SBAR, uy);
    }
    // ---- the thumb keeps the point it was taken by ----
    // This had its own copy of ScrollFromY that always put the thumb's MIDDLE
    // under the cursor, so grabbing a thumb anywhere but dead centre jumped it
    // before the drag had moved at all. HubUI's version remembers where inside
    // the thumb the press landed and only centres when the press missed it -
    // which is what every other scrollbar in the hub does.
    static void DragBody(HubSurface hub, double uy)
    {
        double bodyY = hub.HL.cty + 30 + BodyY();
        CmScrT = HubUI.ScrollFromY(hub.HL, uy, bodyY + 4, BodyTrackH(), ContentH(), BodyH(), BodyThumb(), CmScr);
        CmScr = CmScrT;
    }
    static void DragSet(HubSurface hub, int gi, double uy)
    {
        if (gi < 1 || gi > Comm.Count) return;
        var ent = Comm[gi - 1];
        if (SetMax(ent) <= 0) return;
        var ds = State(ent.Folder);
        double ly = hub.HL.cty + 30 + BodyY() - CmScr + CardTop(gi) + SetY(ent);
        double v = HubUI.ScrollFromY(hub.HL, uy, ly, SetListH(ent), ent.Sets.Count, SetVis(ent), SetThumb(ent, ent.Sets.Count), ds.S);
        ds.S = (int)Math.Round(Clamp(v, 0, SetMax(ent)));
    }
    static void DragDsc(HubSurface hub, int gi, double uy)
    {
        if (gi < 1 || gi > Comm.Count) return;
        var ds = State(Comm[gi - 1].Folder);
        if (ds.DMax <= 0) return;
        int nl = ds.Wrap.Count;
        double dscY = hub.HL.cty + 30 + BodyY() - CmScr + CardTop(gi) + DscY();
        double v = HubUI.ScrollFromY(hub.HL, uy, dscY, DscH(), nl, DSCVIS, DscThumb(nl), ds.D);
        ds.D = (int)Math.Round(Clamp(v, 0, ds.DMax));
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        int kD = DscAt(hub, ux, uy);
        var ds = kD != 0 ? State(Comm[kD - 1].Folder) : null;
        int kS = SetAt(hub, ux, uy);
        var dsS = kS != 0 ? State(Comm[kS - 1].Folder) : null;
        int notch = (int)Math.Round(delta);
        if (ds is not null && ds.DMax > 0) ds.D = Math.Clamp(ds.D - notch, 0, ds.DMax);
        else if (dsS is not null && SetMax(Comm[kS - 1]) > 0) dsS.S = Math.Clamp(dsS.S - notch, 0, SetMax(Comm[kS - 1]));
        else if (ScrMax() > 0) CmScrT = Clamp(CmScrT - delta * 54, 0.0, ScrMax());
        return true;
    }
}
