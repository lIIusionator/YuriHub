using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Hub;

/// <summary>
/// The sidebar's profile plate: the portrait (DrawAvatar - the profile picture
/// from the pool with its fade, or the plain disc), the ring (RingDraw style
/// 1, the two breathing halos), the name, OPERATOR, the pen for EDIT PROFILE,
/// and CHANGE on hovering the portrait. Zones 7 (portrait), 12 (pen), 13
/// (name). The EDIT PROFILE modal and the bio pop-up follow.
/// </summary>
public static class ProfilePlate
{
    public static void Draw(HubSurface hub, double sdx, double s2, long now, uint hubCur, double emb2G)
    {
        var HL = hub.HL;
        Pool.Load(); Pool.SelTick(now);
        double hPl = Math.Max(Math.Max(hub.Hv(12), hub.Hv(13)), hub.Hv(7));
        double plx = HL.sbx + 5 + sdx, ply = HL.avcy - 28 - 1.5 * hPl, plw = HL.sbw, plh = 56;
        if (hPl > 0.01) FillRR(plx + 1, ply + 4, plw, plh, 13, SBrush(FA(Alpha(0x000000, R(46 * hPl)), s2)));
        FillRR(plx, ply, plw, plh, 13, VBrush(plx, ply, plw, plh, FA(Mix(0xFF14172A, 0xFF1E2340, hPl), s2), FA(0xFF0F1120, s2)));
        MiniBackdrop(plx, ply, plw, plh, 13, hubCur, s2, now, 0.8);
        int stC = PushG(); ClipRR(plx, ply, plw, plh, 13);
        FillRect(plx, ply, plw, 18, VBrush(plx, ply, plw, 18, FA(0x10FFFFFF, s2), Alpha(0xFFFFFF, 0)));
        FillEll(plx - 26, ply - 34, 96, 96, SBrush(FA(Alpha(hubCur, R(18 + 26 * hPl)), s2)));
        if (hPl > 0.01) { double swp2 = DecT(now) * 0.16 % (plw + 90) - 45; FillRect(plx + swp2 - 26, ply, 52, plh, VBrush(plx + swp2 - 26, ply, 52, plh, Alpha(0xFFFFFF, 0), FA(Alpha(0xFFFFFF, R(26 * hPl)), s2))); }
        Pop(stC);
        StrokeRR(plx, ply, plw, plh, 13, Pen(FA(Alpha(AccHi(hubCur, 1 - 0.5 * hPl), R(28 + 90 * hPl)), s2), 1));
        FillRR(plx, ply + 16, 2.6, plh - 32, 1.3, SBrush(FA(Alpha(hubCur, R(120 + 110 * hPl)), s2)));
        FadeLine(HL.sbx + 5 + sdx, HL.sbx + 5 + HL.sbw + sdx, HL.avcy - 38, 0x1EFFFFFF, s2);
        double hAv = hub.Hv(7), avx2 = HL.avcx + sdx, avy2 = HL.avcy - 1.5 * hPl;
        Avatar(avx2, avy2, 20, s2, now);
        if (hAv > 0.01)
        {
            int stA = PushG(); ClipEll(avx2 - 20, avy2 - 20, 40, 40);
            FillEll(avx2 - 20, avy2 - 20, 40, 40, SBrush(FA(Alpha(0x05060C, R(158 * hAv)), s2)));
            double gyA = avy2 - 11 + (1 - hAv) * 5;
            var pnC = Pen(FA(Alpha(0xFFFFFF, R(210 * hAv)), s2), 1.1);
            StrokeRR(avx2 - 5.5, gyA, 11, 8, 1.6, pnC);
            Line(avx2 - 3.5, gyA + 6, avx2 - 1, gyA + 3, pnC); Line(avx2 - 1, gyA + 3, avx2 + 1.6, gyA + 6, pnC); Line(avx2 + 1.6, gyA + 6, avx2 + 3.6, gyA + 4, pnC);
            FillEll(avx2 + 1.8, gyA + 1.2, 1.8, 1.8, SBrush(FA(Alpha(0xFFFFFF, R(220 * hAv)), s2)));
            Txt("CHANGE", avx2 - 20, avy2 + 3 + (1 - hAv) * 5, 40, 11, HL.fS, FA(Alpha(0xFFFFFF, R(245 * hAv)), s2), Fmt.C);
            Pop(stA);
        }
        Ell(avx2 - 19, avy2 - 19, 38, 38, Pen(FA(0x50000000, s2), 1));
        Ring(avx2, avy2, 22, hubCur, s2, hAv, now);
        double h13 = hub.Hv(13), ntx = avx2 + 27 + 1.5 * h13;
        string nm = HubState.ProfName;
        var nf3 = Fonts.fP; double nw3 = Fonts.MeasureW(nm, nf3);
        if (nw3 > HL.opw) { nf3 = Fonts.fPs; nw3 = Fonts.MeasureW(nm, nf3); }
        Txt(nm, ntx, HL.avcy - 16 - 1.5 * hPl, HL.opw + 6, 16, nf3, FA(Alpha(AccHi(hubCur, 1 - (0.25 + 0.3 * h13)), 235), s2), Fmt.L);
        if (h13 > 0.01) Line(ntx, HL.avcy + 1 - 1.5 * hPl, ntx + Math.Min(nw3, HL.opw) * h13, HL.avcy + 1 - 1.5 * hPl, Pen(FA(Alpha(hubCur, R(150 * h13)), s2), 1));
        double ory = HL.avcy + 3 - 1.5 * hPl, odx = avx2 + 27 + 2 + 1.5 * h13;
        FillEll(odx - 3.5, ory + 2.0, 7, 7, SBrush(FA(Alpha(hubCur, R(36 + 26 * emb2G + 40 * h13)), s2)));
        FillEll(odx - 1.5, ory + 4.0, 3, 3, SBrush(FA(Alpha(AccHi(hubCur, 0.35), R(200 + 45 * emb2G)), s2)));
        Txt("OPERATOR", avx2 + 27 + 7 + 1.5 * h13, ory, HL.opw - 6, 11, HL.opF, FA(Alpha(Mix(0xFF9AA8C0, AccHi(hubCur, 0.45), 0.30 + 0.45 * h13), R(170 + 65 * h13)), s2), Fmt.L);
        double h12 = hub.Hv(12), pcx2 = HL.sbx + HL.sbw - 11 + sdx, pcy2 = HL.avcy - 1.5 * hPl;
        if (h12 > 0.01) { double rp2 = DecT(now) % 1500 / 1500.0; Ell(pcx2 - 10 - rp2 * 7, pcy2 - 10 - rp2 * 7, 20 + rp2 * 14, 20 + rp2 * 14, Pen(FA(Alpha(hubCur, R(110 * (1 - rp2) * h12)), s2), 1.2)); }
        FillEll(pcx2 - 10.5, pcy2 - 10.5, 21, 21, SBrush(FA(Alpha(hubCur, R(22 + 52 * h12)), s2)));
        Ell(pcx2 - 10.5, pcy2 - 10.5, 21, 21, Pen(FA(Alpha(hubCur, R(80 + 130 * h12)), s2), 1));
        int stPn = PushXform(pcx2, pcy2, 1 + 0.12 * h12, 42 + 8 * Math.Sin(DecT(now) * 0.005) * h12);
        uint pcl = FA(Alpha(AccHi(hubCur, 0.45), R(200 + 55 * h12)), s2);
        FillRR(pcx2 - 2.6, pcy2 - 7.6, 5.2, 2.6, 1.1, SBrush(FA(Alpha(AccHi(hubCur, 0.75), R(190 + 65 * h12)), s2)));
        FillRR(pcx2 - 2.6, pcy2 - 4.6, 5.2, 1.5, 0.5, SBrush(FA(Alpha(0xFFFFFF, R(120 + 60 * h12)), s2)));
        FillRR(pcx2 - 2.6, pcy2 - 2.8, 5.2, 6.2, 0.6, SBrush(pcl));
        var tip = new Avalonia.Media.StreamGeometry();
        using (var c = tip.Open()) { c.BeginFigure(new Avalonia.Point(pcx2 - 2.6, pcy2 + 3.4), true); c.LineTo(new Avalonia.Point(pcx2 + 2.6, pcy2 + 3.4)); c.LineTo(new Avalonia.Point(pcx2, pcy2 + 7.8)); c.EndFigure(true); }
        FillPath(tip, SBrush(pcl));
        FillEll(pcx2 - 0.7, pcy2 + 5.9, 1.4, 1.4, SBrush(FA(Alpha(0x0B0C14, 220), s2)));
        Pop(stPn);
    }
    /// <summary>DrawAvatar: the profile picture with its fade, or the plain disc; the highlight arc over both.</summary>
    public static void Avatar(double ax, double ay, double r, double f, long now)
    {
        bool have = Pool.ProfPicSet && Pool.N >= 1;
        if (have)
        {
            double ft = Pool.SelFadeAt != 0 ? Ease3(Clamp((now - Pool.SelFadeAt) / Pool.SEL_FADE, 0.0, 1.0)) : 1.0;
            if (Pool.SelNext != Pool.SelCur && ft < 1) { Circle(Pool.SelAt(Pool.SelCur), ax, ay, r, f); Circle(Pool.SelAt(Pool.SelNext), ax, ay, r, f * ft); }
            else Circle(Pool.SelAt(Pool.SelNext != Pool.SelCur ? Pool.SelNext : Pool.SelCur), ax, ay, r, f);
        }
        else FillEll(ax - r, ay - r, r * 2, r * 2, SBrushP(FA(Alpha(Mix(0xFF000000, 0xFFE6EAF2, HubState.ThT), 255), f)));
        Arc(ax - (r - 4), ay - (r - 4), (r - 4) * 2, (r - 4) * 2, 195, 55, Pen(FA(Alpha(0xFFFFFF, 58), f), 2));
    }
    /// <summary>BlitCircle: a picture cover-fitted into a circle.</summary>
    public static void Circle(Avalonia.Media.Imaging.Bitmap? bmp, double ax, double ay, double r, double f)
    {
        if (bmp is null) return;
        int st = PushG(); ClipEll(ax - r, ay - r, r * 2, r * 2);
        Img.FitRR(bmp, ax - r, ay - r, r * 2, r * 2, 0, f, 1.0, 0.5, 0.35);
        Pop(st);
    }
    static void ClipEll(double x, double y, double w, double h) => ClipPath(new Avalonia.Media.EllipseGeometry(new Avalonia.Rect(x, y, w, h)));
    /// <summary>RingDraw: the two breathing halos, plus the chosen style's decoration (1 plain, 2 dashed orbit, 3 comet arcs, 4 the moons).</summary>
    public static void Ring(double cxr, double cyr, double r, uint col, double f, double hv, long now)
    {
        double k = r / 22.0, ph = (Math.Sin(now * 0.0022) + 1) / 2;
        uint lit = AccHi(col, 0.5);
        for (int hk = 1; hk <= 2; hk++) { double rh = r + hk * 2.4 * k; Ell(cxr - rh, cyr - rh, rh * 2, rh * 2, Pen(FA(Alpha(col, R((9 - hk * 3) * (1 + 2.6 * hv) + 3 * ph * hv)), f), 2.6 * k)); }
        int style = HubState.ProfRing;
        if (style == 2)
        {
            var pn = Pen(FA(Alpha(col, R(170 + 60 * hv)), f), 1.8); PenDash(pn, 1); Ell(cxr - r, cyr - r, r * 2, r * 2, pn);
            double ro = r + 3.6 * k; var pn2 = Pen(FA(Alpha(AccHi(col, 0.4), R(60 + 80 * hv)), f), 1); PenDash(pn2, 2); Ell(cxr - ro, cyr - ro, ro * 2, ro * 2, pn2);
        }
        else if (style == 3)
        {
            Ell(cxr - r, cyr - r, r * 2, r * 2, Pen(FA(Alpha(col, R(80 + 50 * hv)), f), 1.4));
            double ro = r + 3 * k, a0 = DecT(now) * 0.04 % 360;
            for (int q = 0; q < 2; q++) { double ab = a0 + q * 180; for (int sg = 1; sg <= 3; sg++) Arc(cxr - ro, cyr - ro, ro * 2, ro * 2, ab, 130 - sg * 38, Pen(FA(Alpha(lit, R(Math.Min(235, (48 + 46 * sg) * (1 + 0.4 * hv)))), f), 0.8 + 0.4 * sg)); }
            double rq = r + 7.5 * k, a1 = 360 - DecT(now) * 0.022 % 360;
            for (int q = 0; q < 2; q++) Arc(cxr - rq, cyr - rq, rq * 2, rq * 2, a1 + q * 180, 54, Pen(FA(Alpha(col, R((38 + 42 * hv) * (0.6 + 0.4 * ph))), f), 1));
        }
        else if (style == 4)
        {
            Ell(cxr - r, cyr - r, r * 2, r * 2, Pen(FA(Alpha(col, R(160 + 60 * hv)), f), 1.8));
            for (int ok = 1; ok <= 3; ok++)
            {
                double rp = r + 3.5 * ok * k;
                var pnO = Pen(FA(Alpha(col, R((30 - ok * 6) + (30 - ok * 7) * hv)), f), 1); PenDash(pnO, 2); Ell(cxr - rp, cyr - rp, rp * 2, rp * 2, pnO);
                double spd = ok == 1 ? 0.0024 : ok == 2 ? -0.0016 : 0.0011, oa3 = now * spd + ok * 2.1;
                double mx = cxr + rp * Math.Cos(oa3), my = cyr + rp * Math.Sin(oa3), mr = (ok == 1 ? 2.2 : 1.4) * k;
                FillEll(mx - mr, my - mr, mr * 2, mr * 2, SBrush(FA(Alpha(lit, R(ok == 1 ? 230 : 170)), f)));
            }
        }
        else if (style == 5)
        {
            // PULSE: rings shed outward on a 1.6 s loop, the way the armed states
            // elsewhere in the hub announce themselves.
            Ell(cxr - r, cyr - r, r * 2, r * 2, Pen(FA(Alpha(col, R(150 + 70 * hv)), f), 1.7));
            for (int pk = 0; pk < 2; pk++)
            {
                double pt = (DecT(now) + pk * 800) % 1600 / 1600.0, rp = r + pt * 11 * k;
                Ell(cxr - rp, cyr - rp, rp * 2, rp * 2, Pen(FA(Alpha(lit, R((140 + 70 * hv) * Math.Pow(1 - pt, 1.6))), f), (1.8 * (1 - pt) + 0.3) * k));
            }
        }
        else if (style == 6)
        {
            // TICKS: a bezel of marks with every fourth one long, and a lit one
            // walking the dial.
            Ell(cxr - r, cyr - r, r * 2, r * 2, Pen(FA(Alpha(col, R(120 + 60 * hv)), f), 1.4));
            double lead = DecT(now) * 0.02 % 24;
            for (int tk = 0; tk < 24; tk++)
            {
                double a = tk * 15 * 0.0174533, len = (tk % 4 == 0 ? 6.5 : 3.4) * k;
                double d = Math.Abs(((tk - lead + 36) % 24) - 0);
                double lit2 = d < 3 ? Math.Pow(1 - d / 3.0, 2) : 0;
                double r0 = r + 2 * k, r1 = r0 + len;
                Line(cxr + r0 * Math.Cos(a), cyr + r0 * Math.Sin(a), cxr + r1 * Math.Cos(a), cyr + r1 * Math.Sin(a),
                     Pen(FA(Alpha(lit2 > 0 ? lit : col, R((tk % 4 == 0 ? 120 : 70) + 135 * lit2 + 40 * hv)), f), (1 + 0.6 * lit2) * k));
            }
        }
        else if (style == 7)
        {
            // SEGMENT: twelve arcs with a bright head running them, the pill's
            // ring at portrait scale.
            double head = DecT(now) * 0.14 % 360;
            for (int sg = 0; sg < 12; sg++)
            {
                double segA = sg * 30, dd = (segA - head + 540) % 360 - 180;
                double boost = Math.Pow(Math.Max(0.0, Math.Cos(dd * 0.0174533)), 6);
                double rr = r + (sg % 2 == 0 ? 0 : 2.2 * k), sw = 20 + 8 * boost;
                Arc(cxr - rr, cyr - rr, rr * 2, rr * 2, segA + (30 - sw) / 2, sw,
                    Pen(FA(Alpha(boost > 0.15 ? lit : col, R(60 + 175 * boost + 40 * hv)), f), (1.6 + 0.8 * boost) * k));
            }
        }
        else if (style == 8)
        {
            // HALO: no hard edge at all - a soft stack that breathes, for a
            // portrait that should not look framed.
            for (int hk = 0; hk < 5; hk++)
            {
                double rr = r + (1 + hk * 2.1) * k * (1 + 0.05 * ph);
                Ell(cxr - rr, cyr - rr, rr * 2, rr * 2, Pen(FA(Alpha(hk == 0 ? lit : col, R((hk == 0 ? 150 : 46 - hk * 9) * (1 + 0.5 * hv))), f), (hk == 0 ? 1.5 : 2.4) * k));
            }
        }
        else Ell(cxr - r, cyr - r, r * 2, r * 2, Pen(FA(Alpha(col, R(150 + 80 * hv)), f), 1.6));
    }
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        if ((ux - (HL.sbx + HL.sbw - 11)) * (ux - (HL.sbx + HL.sbw - 11)) + (uy - HL.avcy) * (uy - HL.avcy) <= 110) return 12;
        if (ux >= HL.avcx + 26 && ux <= HL.avcx + 82 && uy >= HL.avcy - 18 && uy <= HL.avcy + 2) return 13;
        if ((ux - HL.avcx) * (ux - HL.avcx) + (uy - HL.avcy) * (uy - HL.avcy) <= 484) return 7;
        return 0;
    }
    public static bool BioOpen; public static double BioT;
    public static bool Animating => Math.Abs(BioT - (BioOpen ? 1 : 0)) > 0.01 || BioOpen;
    public static bool Click(HubSurface hub, int z)
    {
        switch (z)
        {
            case 7: Gallery.Show(0, false); return true;
            case 12: BioOpen = false; EditProfile.Open(); return true;
            case 13: BioOpen = !BioOpen; hub.Tim(Pace.TICK_A); return true;
            case 14: BioOpen = false; EditProfile.Open(); return true;
        }
        if (BioOpen && z != 13) { BioOpen = false; hub.Tim(Pace.TICK_A); }
        return false;
    }
    public static int BioZone(HubSurface hub, double ux, double uy)
    {
        if (!BioOpen || BioT < 0.4) return 0;
        var HL = hub.HL;
        double bx4 = HL.sbx + 5, by4 = HL.avcy - 42 - 118;
        return ux >= bx4 && ux <= bx4 + 214 && uy >= by4 && uy <= by4 + 118 ? 14 : 0;
    }
    /// <summary>The bio pop-up above the plate: the portrait, the name, OPERATOR - EST 2026, the bio on two lines, the counts.</summary>
    public static void DrawBio(HubSurface hub, double cf, long now, uint hubCur)
    {
        BioT += ((BioOpen ? 1.0 : 0.0) - BioT) * EK(0.2);
        if (BioT <= 0.02) { if (!BioOpen) BioT = 0; return; }
        var HL = hub.HL;
        double bf2 = BioT, bpx = HL.sbx + 5, bph = 118, bpw = 214, bpy = HL.avcy - 42 - bph + (1 - bf2) * 10;
        FillRR(bpx + 3, bpy + 5, bpw, bph, 14, SBrush(FA(Alpha(0x000000, R(70 * bf2)), cf)));
        FillRR(bpx, bpy, bpw, bph, 14, VBrush(bpx, bpy, bpw, bph, FA(0xFF1B1F38, cf * bf2), FA(0xFF12141F, cf * bf2)));
        MiniBackdrop(bpx, bpy, bpw, bph, 14, hubCur, cf * bf2, now, 0.85);
        StrokeRR(bpx, bpy, bpw, bph, 14, Pen(FA(Alpha(hubCur, R(130 * bf2)), cf), 1.2));
        var tr = new Avalonia.Media.StreamGeometry();
        using (var c = tr.Open()) { c.BeginFigure(new Avalonia.Point(bpx + 24, bpy + bph), true); c.LineTo(new Avalonia.Point(bpx + 36, bpy + bph)); c.LineTo(new Avalonia.Point(bpx + 30, bpy + bph + 8)); c.EndFigure(true); }
        FillPath(tr, SBrush(FA(0xFF12141F, cf * bf2)));
        Avatar(bpx + 30, bpy + 30, 17, cf * bf2, now);
        Ring(bpx + 30, bpy + 30, 19, hubCur, cf * bf2, 0, now);
        double n1 = Clamp((bf2 - 0.25) / 0.45, 0.0, 1.0), n2 = Clamp((bf2 - 0.40) / 0.45, 0.0, 1.0), n3 = Clamp((bf2 - 0.55) / 0.40, 0.0, 1.0);
        var nfB = Fonts.fP; if (Fonts.MeasureW(HubState.ProfName, nfB) > 118) nfB = Fonts.fPs;
        Txt(HubState.ProfName, bpx + 56 + (1 - n1) * 6, bpy + 14, 120, 17, nfB, FA(Alpha(0xFFFFFF, R(240 * bf2 * n1)), cf), Fmt.L);
        Txt("OPERATOR  -  EST 2026", bpx + 56 + (1 - n1) * 6, bpy + 32, 132, 12, HL.fXs, FA(Alpha(hubCur, R(190 * bf2 * n1)), cf), Fmt.L);
        if (!HubState.LowPerf) for (int k5 = 1; k5 <= 3; k5++) { double tw5 = Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.0026 + k5 * 1.9)), 5); var pnS = Pen(FA(Alpha(AccHi(hubCur, 0.5), R((24 + 130 * tw5) * bf2)), cf), 1.1); double sx = bpx + bpw - 40 + (k5 - 1) * 12; Line(sx - 3, bpy + 16, sx + 3, bpy + 16, pnS); Line(sx, bpy + 13, sx, bpy + 19, pnS); }
        FadeLine(bpx + 14, bpx + bpw - 14, bpy + 52, Alpha(0xFFFFFF, R(40 * bf2)), cf);
        string bl1 = "", bl2 = ""; double bmx2 = bpw - 40; var fPs = Fonts.fPs;
        foreach (var wd in HubState.ProfBio.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (bl2 == "" && Fonts.MeasureW(bl1 + (bl1 == "" ? "" : " ") + wd, fPs) <= bmx2) bl1 += (bl1 == "" ? "" : " ") + wd;
            else if (Fonts.MeasureW(bl2 + (bl2 == "" ? "" : " ") + wd, fPs) <= bmx2 - 12) bl2 += (bl2 == "" ? "" : " ") + wd;
            else { bl2 += "..."; break; }
        }
        Txt(bl1, bpx + 16, bpy + 60 + (1 - n2) * 4, bpw - 32, 14, Fonts.fPs, FA(Alpha(0xFFC7CBE0, R(210 * bf2 * n2)), cf), Fmt.L);
        Txt(bl2, bpx + 16, bpy + 75 + (1 - n2) * 4, bpw - 32, 14, Fonts.fPs, FA(Alpha(0xFFC7CBE0, R(210 * bf2 * n2)), cf), Fmt.L);
        FillRR(bpx + 16, bpy + 96, 26 * n3 + 0.01, 3, 1.5, SBrush(FA(Alpha(hubCur, R(190 * bf2 * n3)), cf)));
        Txt(Modules.FastFlags.Ffm.Flags.Count + " blocks  -  " + Modules.ScriptHub.Scr.List.Count + " scripts", bpx + 50, bpy + 92, 148, 13, HL.fXs, FA(Alpha(0xFFC7CBE0, R(150 * bf2 * n3)), cf), Fmt.L);
        if (!HubState.LowPerf) for (int k5 = 1; k5 <= 4; k5++) FillEll(bpx + bpw - 20, bpy + 60 + (k5 - 1) * 11, 2.6, 2.6, SBrush(FA(Alpha(hubCur, R((30 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.003 + k5))) * bf2)), cf)));
    }
}
