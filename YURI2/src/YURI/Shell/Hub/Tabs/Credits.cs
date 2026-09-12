using Avalonia.Media;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Platform;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Shell.Hub.Tabs;

/// <summary>
/// Tab 5. The two developers' plate (ZEAL, LUNARIS), the note, the pixel
/// cafe, the doodle strip, then FIND ME: a ZEAL / LUNARIS switch over three
/// link cards (ROBLOX opens the profile, YOUTUBE the channel, DISCORD copies
/// the tag) and the badge row. The avatars come with the gallery (phase 5);
/// until then both use the .ahk's own fallback, the initial in a disc.
/// </summary>
public static class Credits
{
    /// <summary>DrawAvatarBase: the pool's first picture in a circle, the plain disc with a P when there is none, the highlight arc over both.</summary>
    public static void DrawAvatarBase(double ax, double ay, double r, double f, uint acc)
    {
        Gfx.Pool.Load();
        var bm = Gfx.Pool.SelAt(1);
        if (bm is not null) ProfilePlate.Circle(bm, ax, ay, r, f);
        else { FillEll(ax - r, ay - r, r * 2, r * 2, SBrush(FA(Alpha(acc, 50), f))); Txt("P", ax - r, ay - r, r * 2, r * 2, Fonts.fStatus, FA(Alpha(acc, 235), f), Fmt.C); }
        Arc(ax - (r - 4), ay - (r - 4), (r - 4) * 2, (r - 4) * 2, 195, 55, Pen(FA(Alpha(0xFFFFFF, 58), f), 2));
    }
    const double FFM_TABMS = 260.0;
    public static int Who = 1, WhoPrev = 1; public static long WhoAt;      // credWho / credWhoPrev / credWhoAt
    public sealed record Link(string N, string H, string A, string U, string C = "");
    public static Link[] Links(int who) => who == 2
        ? new[] { new Link("ROBLOX", "lIIusionator", "open profile", "https://www.roblox.com/users/10679736474/profile"),
                  new Link("YOUTUBE", "@Lunaris", "open channel", "https://www.youtube.com/@Luna_.ris182"),
                  new Link("DISCORD", "@liiusionator", "copy tag", "", "liiusionator") }
        : new[] { new Link("ROBLOX", "lmZeaI", "open profile", "https://www.roblox.com/users/417371862/profile?friendshipSourceType=PlayerSearch"),
                  new Link("YOUTUBE", "@lmZeaI", "open channel", "https://www.youtube.com/@lmZeaI"),
                  new Link("DISCORD", "@zeaiish", "copy tag", "", "zeaiish") };

    public static void Draw(HubSurface hub, double x0, double y0, double dx, double dy2, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        double emb2G = (Math.Sin(DecT(now) * 0.004) + 1) / 2;
        double py0 = y0 + 34, ph_ = 128;
        FillRR(x0, py0, HL.ctw, ph_, 12, VBrush(x0, py0, HL.ctw, ph_, FA(0xFF171A30, f), FA(0xFF12141F, f)));
        MiniBackdrop(x0, py0, HL.ctw, ph_, 12, hubCur, f, now, 1.0);
        StrokeRR(x0, py0, HL.ctw, ph_, 12, Pen(FA(Alpha(hubCur, 80 + 30 * emb2G), f), 1.2));
        var pnE = Pen(FA(Alpha(hubCur, 130), f), 1.6);
        Line(x0 + 12, py0 + 1.5, x0 + 30, py0 + 1.5, pnE);
        Line(x0 + HL.ctw - 30, py0 + ph_ - 1.5, x0 + HL.ctw - 12, py0 + ph_ - 1.5, pnE);
        double colW = HL.ctw / 2;
        var pnD = Pen(FA(Alpha(hubCur, 40 + 30 * emb2G), f), 1);
        PenDash(pnD, 2);
        Line(x0 + colW, py0 + 22, x0 + colW, py0 + ph_ - 22, pnD);
        double acy = py0 + ph_ / 2;
        for (int pk = 1; pk <= 2; pk++)
        {
            double pcx = x0 + (pk - 1) * colW;
            double acx = pcx + 46;
            string nm = pk == 1 ? "ZEAL" : "LUNARIS";
            string[] chips = pk == 1 ? new[] { "DESIGN", "CODE", "MOTION" } : new[] { "FUNCTIONS", "FFLAGS" };
            if (pk == 1) DrawAvatarBase(acx, acy, 26, f, hubCur);                                   // DrawAvatarBase: ZEAL wears the pool's first picture (avatar.*)
            else if (!Gfx.Img.CircleFit(Gfx.Img.Asset("luna.jpg"), acx, acy, 26, f))
            {
                FillEll(acx - 26, acy - 26, 52, 52, SBrush(FA(Alpha(hubCur, 50), f)));
                Txt(nm[..1], acx - 26, acy - 26, 52, 52, Fonts.fStatus, FA(Alpha(hubCur, 235), f), Fmt.C);
            }
            Ell(acx - 29, acy - 29, 58, 58, Pen(FA(Alpha(hubCur, 200 + 40 * emb2G), f), 2));
            double oa = now * 0.0021 + (pk - 1) * 3.14159;
            FillEll(acx + 34 * Math.Cos(oa) - 2, acy + 34 * Math.Sin(oa) - 2, 4, 4, SBrush(FA(Alpha(AccHi(hubCur, 0.5), 225), f)));
            var pnO = Pen(FA(Alpha(hubCur, 55), f), 1);
            PenDash(pnO, 2);
            Ell(acx - 34, acy - 34, 68, 68, pnO);
            double tx = acx + 42;
            Txt(nm, tx, py0 + 26, 160, 22, HL.fT, FA(Alpha(AccHi(hubCur, 0.8), 245), f), Fmt.L);
            Txt("DEVELOPER", tx, py0 + 50, 120, 14, Fonts.fBadge, FA(Alpha(hubCur, 225), f), Fmt.L);
            Line(tx, py0 + 68, tx + 52, py0 + 68, Pen(FA(Alpha(hubCur, 90 + 70 * (Math.Sin(DecT(now) * 0.0035 + pk) + 1) / 2), f), 1.4));
            double cw3 = pk == 1 ? 52 : 66;
            for (int ck = 0; ck < chips.Length; ck++) ChipDraw(tx + ck * (cw3 + 4), py0 + 80, cw3, 20, chips[ck], 0.0, hubCur, f);
        }
        double ly2 = py0 + ph_ + 20;
        FadeLine(x0, x0 + HL.ctw, ly2 - 8, 0x20FFFFFF, f);
        FillRR(x0, ly2 + 2, 3, 60, 1.5, SBrush(FA(Alpha(hubCur, 210), f)));
        Txt(AppInfo.AppName + " is designed, built and animated by ZEAL - every window,", x0 + 16, ly2, 344, 16, Fonts.fHint, FA(0x9AC7CBE0, f), Fmt.L);
        Txt("easing curve and pixel of it. Functions and fflags by LUNARIS.", x0 + 16, ly2 + 20, 344, 16, Fonts.fHint, FA(0x6EC7CBE0, f), Fmt.L);
        FillEll(x0 + 16, ly2 + 47, 4, 4, SBrush(FA(Alpha(hubCur, 140 + 55 * emb2G), f)));
        Txt("no frameworks - raw GDI+ and a lot of easing math", x0 + 26, ly2 + 42, 320, 14, HL.fXs, FA(Alpha(hubCur, 150), f), Fmt.L);
        double spx4 = x0 + HL.ctw - 196, spy4 = ly2 + 6;
        FillRR(spx4 + 2, spy4 + 4, 196, 72, 12, SBrush(FA(Alpha(0x000000, 44), f)));
        FillRR(spx4, spy4, 196, 72, 12, VBrush(spx4, spy4, 196, 72, FA(0xFF171A30, f), FA(0xFF12141F, f)));
        MiniBackdrop(spx4, spy4, 196, 72, 12, hubCur, f, now, 0.8);
        StrokeRR(spx4, spy4, 196, 72, 12, Pen(FA(Alpha(hubCur, 70 + 40 * emb2G), f), 1.1));
        var pnS = Pen(FA(Alpha(hubCur, 150), f), 1.5);
        Line(spx4 + 10, spy4 + 1.5, spx4 + 26, spy4 + 1.5, pnS);
        Line(spx4 + 170, spy4 + 70.5, spx4 + 186, spy4 + 70.5, pnS);
        CafeDraw(spx4 + 12, spy4 + 6, hubCur, f, now);
        Txt("two contributors - est 2026", spx4 + 14, spy4 + 52, 170, 12, HL.fXs, FA(0x52C7CBE0, f), Fmt.L);
        double lky = HL.lky + dy2;
        double dby = Math.Min(ly2 + 76, lky - 30);
        double dLim = spx4 - 18;
        Doodle(1, x0 + 30, dby, 8, hubCur, f, now, 0.0);
        Doodle(3, x0 + 82, dby + 3, 13, hubCur, f, now, 1.1);
        Doodle(2, x0 + 146, dby - 2, 9, hubCur, f, now, 2.0);
        Doodle(4, x0 + 200, dby + 2, 8, hubCur, f, now, 2.8);
        Doodle(1, x0 + 248, dby - 3, 6, hubCur, f, now, 3.5);
        Doodle(5, x0 + 296, dby + 1, 11, hubCur, f, now, 4.2);
        if (x0 + 348 + 10 < dLim) Doodle(3, x0 + 342, dby + 3, 10, hubCur, f, now, 5.0);
        for (int dk = 1; dk <= 5; dk++)
        {
            double dpx = x0 + 14 + (dk * 79) % 320;
            double dpy = dby - 12 + 20 * ((Math.Sin(DecT(now) * 0.0009 + dk * 1.7) + 1) / 2);
            FillEll(dpx - 1.4, dpy - 1.4, 2.8, 2.8, SBrush(FA(Alpha(hubCur, R(50 + 70 * Math.Abs(Math.Sin(DecT(now) * 0.0018 + dk)))), f)));
        }
        // ---- FIND ME ----
        Txt("FIND ME", x0, lky - 20, 100, 14, Fonts.fBadge, FA(0x62C7CBE0, f), Fmt.L);
        FadeLine(x0 + 62, x0 + HL.ctw - 162, lky - 13, 0x20FFFFFF, f);
        double cwT = (WhoAt != 0 && now - WhoAt < FFM_TABMS) ? Ease3((now - WhoAt) / FFM_TABMS) : 1.0;
        bool cwS = cwT < 1 && WhoPrev != Who;
        double cwU = cwS ? Lerp(WhoPrev == 2 ? 1.0 : 0.0, Who == 2 ? 1.0 : 0.0, cwT) : (Who == 2 ? 1.0 : 0.0);
        for (int wk = 1; wk <= 2; wk++)
        {
            double wx = x0 + HL.ctw - 152 + (wk - 1) * 78;
            double wsl = wk == 2 ? cwU : 1 - cwU;
            double whv = hub.Hv(1219 + wk);
            FillRR(wx, lky - 28, 74, 18, 5, SBrush(FA(Alpha(Mix(0xFF8A90A6, hubCur, wsl), R(Lerp(14 + 14 * whv, 34, wsl))), f)));
            Txt(wk == 1 ? "ZEAL" : "LUNARIS", wx, lky - 29, 74, 18, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, 0xFFE8EAF6, wsl), R(255 * Lerp(0.4 + 0.3 * whv, 1.0, wsl))), f), Fmt.C);
        }
        double ubx2 = Lerp(x0 + HL.ctw - 152, x0 + HL.ctw - 74, cwU);
        FillRR(ubx2 + 8, lky - 12, 58, 2, 1, SBrush(FA(Alpha(hubCur, 210), f)));
        var lnkNow = Links(Who);
        var lnkOld = cwS ? Links(WhoPrev) : lnkNow;
        string[] names = { "ROBLOX", "YOUTUBE", "DISCORD" };
        string[] acts = { "open profile", "open channel", "copy tag" };
        for (int k = 1; k <= 3; k++)
        {
            double lx = x0 + (k - 1) * 174;
            double hvL = hub.Hv(42 + k);
            double lift = 3 * hvL;
            FillRR(lx + 2, lky + 4, 158, HL.lkh - 4, 12, SBrush(FA(Alpha(0x000000, 40 + 30 * hvL), f)));
            FillRR(lx, lky - lift, 162, HL.lkh, 12, VBrush(lx, lky - lift, 162, HL.lkh, FA(Mix(0xFF171A30, 0xFF1E2340, hvL), f), FA(0xFF12141F, f)));
            MiniBackdrop(lx, lky - lift, 162, HL.lkh, 12, hubCur, f, now, 0.8);
            int cl = PushG();
            ClipRR(lx, lky - lift, 162, HL.lkh, 12);
            FillEll(lx - 42, lky - lift - 54, 120, 120, SBrush(FA(Alpha(hubCur, 16 + 26 * hvL), f)));
            double swL = (DecT(now) * 0.05 + k * 80) % 320;
            FillRect(lx + swL - 30, lky - lift, 30, HL.lkh, VBrush(lx + swL - 30, lky - lift, 30, HL.lkh, Alpha(0xFFFFFF, 0), FA(Alpha(0xFFFFFF, 12 + 14 * hvL), f)));
            Pop(cl);
            StrokeRR(lx, lky - lift, 162, HL.lkh, 12, Pen(FA(Alpha(hubCur, 55 + 130 * hvL), f), 1.2));
            double icx2 = lx + 30, icy3 = lky - lift + 28;
            FillEll(icx2 - 17, icy3 - 17, 34, 34, SBrush(FA(Alpha(hubCur, 34 + 30 * hvL), f)));
            Ell(icx2 - 17, icy3 - 17, 34, 34, Pen(FA(Alpha(hubCur, 130 + 90 * hvL), f), 1.2));
            uint icL = FA(Alpha(AccHi(hubCur, 0.45), 235), f);
            if (k == 1)
            {
                int stR = PushXform(icx2, icy3, 1, 12 * Math.Sin(DecT(now) * 0.0018 + 1));
                StrokeRR(icx2 - 8, icy3 - 8, 16, 16, 3, Pen(icL, 1.6));
                FillRR(icx2 - 3, icy3 - 3, 6, 6, 1.4, SBrush(icL));
                Pop(stR);
            }
            else if (k == 2)
            {
                FillRR(icx2 - 11, icy3 - 8, 22, 16, 5, SBrush(icL));
                var pY = new StreamGeometry();
                using (var g = pY.Open())
                {
                    g.BeginFigure(new Avalonia.Point(icx2 - 3, icy3 - 4.5), true);
                    g.LineTo(new Avalonia.Point(icx2 + 4.5, icy3));
                    g.LineTo(new Avalonia.Point(icx2 - 3, icy3 + 4.5));
                    g.EndFigure(true);
                }
                FillPath(pY, SBrush(FA(0xFF12141F, f)));
            }
            else
            {
                var bD = SBrush(icL);
                FillRR(icx2 - 11, icy3 - 7, 22, 14, 7, bD);
                FillEll(icx2 - 11, icy3 + 1, 7, 8, bD);
                FillEll(icx2 + 4, icy3 + 1, 7, 8, bD);
                var bE = SBrush(FA(0xFF12141F, f));
                FillEll(icx2 - 6, icy3 - 4, 4, 5, bE);
                FillEll(icx2 + 2, icy3 - 4, 4, 5, bE);
            }
            Txt(names[k - 1], lx + 56, lky - lift + 14, 96, 16, Fonts.fBadge, FA(Alpha(AccHi(hubCur, 0.8), 235), f), Fmt.L);
            if (cwS)
            {
                Txt(lnkOld[k - 1].H, lx + 56, lky - lift + 31 - 9 * cwT, 96, 14, Fonts.fHint, FA(Alpha(0xFFC7CBE0, R(138 * (1 - cwT))), f), Fmt.L);
                Txt(lnkNow[k - 1].H, lx + 56, lky - lift + 31 + 9 * (1 - cwT), 96, 14, Fonts.fHint, FA(Alpha(0xFFC7CBE0, R(138 * cwT)), f), Fmt.L);
            }
            else Txt(lnkNow[k - 1].H, lx + 56, lky - lift + 31, 96, 14, Fonts.fHint, FA(0x8AC7CBE0, f), Fmt.L);
            bool cpd = k == 3 && HL.cpAt != 0 && now - HL.cpAt < 1600;
            Txt(cpd ? "copied" : acts[k - 1], lx + 56, lky - lift + 50, 96, 13, HL.fXs, FA(Alpha(cpd ? HubState.C_ON : hubCur, cpd ? 235 : 120 + 120 * hvL), f), Fmt.L);
            double arw = lx + 140 + 3 * hvL;
            var pnA = Pen(FA(Alpha(AccHi(hubCur, 0.4), 90 + 150 * hvL), f), 1.5);
            Line(arw - 4, lky - lift + 50, arw + 1, lky - lift + 55, pnA);
            Line(arw + 1, lky - lift + 55, arw - 4, lky - lift + 60, pnA);
            for (int j3 = 1; j3 <= 3; j3++)
                FillEll(lx + 128 + (j3 - 1) * 7, lky - lift + 18, 3.4, 3.4, SBrush(FA(Alpha(hubCur, 30 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.0026 + j3 + k))), f)));
            if (cpd)
            {
                double e = Ease3((now - HL.cpAt) / 900.0);
                if (e < 1)
                {
                    double ex2 = e * 12;
                    StrokeRR(lx - ex2, lky - lift - ex2 * 0.6, 162 + ex2 * 2, HL.lkh + ex2 * 1.2, 12 + ex2 * 0.3, Pen(FA(Alpha(HubState.C_ON, 170 * (1 - e)), f), 2 * (1 - e) + 0.4));
                }
            }
        }
        double cbY = lky + HL.lkh + 18;
        FadeLine(x0, x0 + HL.ctw, cbY, 0x1AFFFFFF, f);
        string[] cbdg = { "GDI+ RENDER", "TEAM BUILD", "EST 2026" };
        double cbx = x0;
        for (int k = 1; k <= 3; k++)
        {
            double bwj = 18 + Fonts.MeasureW(cbdg[k - 1], HL.fS);
            FillRR(cbx, cbY + 12, bwj, 18, 6, SBrush(FA(0x0EFFFFFF, f)));
            StrokeRR(cbx, cbY + 12, bwj, 18, 6, Pen(FA(Alpha(hubCur, 55 + 30 * Math.Abs(Math.Sin(DecT(now) * 0.0021 + k))), f), 1));
            FillEll(cbx + 7, cbY + 18.5, 4, 4, SBrush(FA(Alpha(hubCur, 150 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.0027 + k * 1.3))), f)));
            Txt(cbdg[k - 1], cbx + 14, cbY + 13, bwj - 14, 15, HL.fS, FA(0x92C7CBE0, f), Fmt.L);
            cbx += bwj + 10;
        }
        double occ = x0 + HL.ctw - 24, ocy2 = cbY + 21;
        var pnC = Pen(FA(Alpha(hubCur, 45), f), 1);
        PenDash(pnC, 2);
        Ell(occ - 11, ocy2 - 11, 22, 22, pnC);
        double oaC = now * 0.0024;
        FillEll(occ + 11 * Math.Cos(oaC) - 1.8, ocy2 + 11 * Math.Sin(oaC) - 1.8, 3.6, 3.6, SBrush(FA(Alpha(AccHi(hubCur, 0.5), 210), f)));
        FillEll(occ - 2.5, ocy2 - 2.5, 5, 5, SBrush(FA(Alpha(hubCur, 140 + 60 * emb2G), f)));
        Txt("crafted frame by frame - thank you for running " + AppInfo.AppName, x0, cbY + 36, HL.ctw - 60, 14, HL.fXs, FA(0x48C7CBE0, f), Fmt.L);
    }

    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        if (uy >= HL.lky - 28 && uy <= HL.lky - 10)
        {
            if (ux >= HL.ctx + HL.ctw - 152 && ux <= HL.ctx + HL.ctw - 78) return 1220;
            if (ux >= HL.ctx + HL.ctw - 74 && ux <= HL.ctx + HL.ctw) return 1221;
        }
        if (uy >= HL.lky && uy <= HL.lky + HL.lkh)
            for (int j = 1; j <= 3; j++)
            {
                double lx = HL.ctx + (j - 1) * 174;
                if (ux >= lx && ux <= lx + 162) return 42 + j;
            }
        return 0;
    }

    public static bool Click(HubSurface hub, int z)
    {
        if (z == 1220 || z == 1221) { CredWho(hub, z == 1220 ? 1 : 2); return true; }
        if (z >= 43 && z <= 45) { HubLink(hub, z - 42); return true; }
        return false;
    }

    /// <summary>HubCredWho(w): switch the FIND ME cards between the two developers.</summary>
    static void CredWho(HubSurface hub, int w)
    {
        if (w == Who) return;
        long now = Clock.Tick;
        if (!(WhoAt != 0 && now - WhoAt < FFM_TABMS && Ease3((now - WhoAt) / FFM_TABMS) < 0.5)) WhoPrev = Who;
        Who = w; WhoAt = now;
        hub.HL.cpAt = 0;                                  // the copied flash belongs to the old set
    }

    /// <summary>HubLink(i): open the link, or copy the tag to the clipboard.</summary>
    static async void HubLink(HubSurface hub, int i)
    {
        var all = Links(Who);
        if (i < 1 || i > all.Length) return;                   // async void: an index fault here would take the process
        var L = all[i - 1];
        if (L.U == "")
        {
            try
            {
                var cb = Avalonia.Controls.TopLevel.GetTopLevel(hub)?.Clipboard;
                if (cb is not null) await cb.SetTextAsync(L.C != "" ? L.C : L.H);
            }
            catch { }
            hub.HL.cpAt = Clock.Tick;
            return;
        }
        Roblox.OpenUrl(L.U);
    }
}
