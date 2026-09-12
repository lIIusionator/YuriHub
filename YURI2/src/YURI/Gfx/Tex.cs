using Avalonia.Media;
using Yuri.Core;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;

namespace Yuri.Gfx;

/// <summary>
/// BACKDROPS, the shadow, and the shared UI primitives (chips, grips, rings)
/// that every panel reuses. Two designs, one per theme: the dark surfaces are
/// lit FROM WITHIN (a dot lattice, a travelling diagonal hatch, the accent's
/// glow in the corners); the light ones are printed (a ruled grid, register
/// crosses, a margin rule). The .ahk cached these as bitmaps (the BACKDROP
/// CACHE, HATCH LAYER, PLATE CACHE) because GDI+ rasterised them in software
/// every frame; Skia draws them live within the frame budget, so they are
/// drawn live. LEAN and LOW PERFORMANCE MODE still skip them as before.
/// </summary>
public static class Tex
{
    public const int GRIP_PX = 11, GRIP_PY = 8, GRIP_H = 22;

    // ---- ShadowDraw(x, y, w, h, r0, rings, baseA, dynA, f): stacked rounded rects, each at alpha baseA ----
    public static void ShadowDraw(double x, double y, double w, double h, double r0, int rings, double baseA, double dynA, double f)
    {
        if (HubState.LowPerf) return;
        double a = f * HubState.Opacity * (dynA / baseA);
        if (a <= 0.004) return;
        long t0 = Perf.On ? Perf.Now() : 0;
        int op = PushOpacity(a);
        var sb = Solid(((uint)((int)baseA & 0xFF)) << 24);
        for (int i = 1; i <= rings; i++)
            Ctx.DrawRectangle(sb, null, RRect(x - i, y - i, w + i * 2, h + i * 2, r0 + i));
        Pop(op);
        if (Perf.On) Perf.Add("shadow", t0);
    }

    // ---- PanelBackdrop: the card's ambient texture, per theme ----
    public static void PanelBackdrop(double x, double y, double w, double h, uint acc, double f, long now, double k = 1.0)
    {
        if (HubState.LowPerf) return;                        // LOW PERFORMANCE MODE: flat surfaces
        if (f <= 0.004) return;
        if (HubState.Lean) return;                           // LEAN: no live texture
        long t0 = Perf.On ? Perf.Now() : 0;
        double dk = k * (1 - HubState.ThT), lt = k * HubState.ThT;
        if (dk > 0.004) PanelBackdropDark(x, y, w, h, acc, f, now, dk);
        if (lt > 0.004) PanelBackdropLight(x, y, w, h, acc, f, now, lt);
        if (Perf.On) Perf.Add("tex", t0);
    }

    static void PanelBackdropDark(double x, double y, double w, double h, uint acc, double f, long now, double k)
    {
        double gp = 44, wv = now * 0.0016;
        var b = SBrush(FA(Alpha(0xFFFFFF, R(22 * k)), f));
        for (double yy = y + gp / 2; yy < y + h; yy += gp)
            for (double xx = x + gp / 2; xx < x + w; xx += gp)
            {
                double dr = 0.85 + 0.75 * (Math.Sin(wv + (xx - x) * 0.012 + (yy - y) * 0.017) + 1) / 2;
                FillEll(xx - dr, yy - dr, dr * 2, dr * 2, b);
            }
        double off = (DecT(now) * 0.007) % 38;
        var pn = Pen(FA(Alpha(0xFFFFFF, R(11 * k)), f), 1);
        for (int kk = -(int)Math.Ceiling(h / 38); kk < Math.Ceiling(w / 38) + 1; kk++)
        {
            double gx = x + kk * 38 + off;
            Line(gx, y, gx + h, y + h, pn);
        }
        pn = Pen(FA(Alpha(acc, R(9 * k)), f), 1);
        for (int kk = -(int)Math.Ceiling(h / 76); kk < Math.Ceiling(w / 76) + 1; kk++)
        {
            double gx = x + kk * 76 - off * 0.6;
            Line(gx + h, y, gx, y + h, pn);                  // opposite diagonal
        }
        for (int i = 1; i <= 4; i++)
        {
            double rr = 150 + i * 130;
            double ph = (Math.Sin(DecT(now) * 0.00055 + i * 0.7) + 1) / 2;
            pn = Pen(FA(Alpha(acc, R((16 - i * 2) * k * (0.5 + 0.5 * ph))), f), 1.3);
            Arc(x - rr * 0.45, y - rr * 0.5, rr * 2, rr * 2, 8, 74, pn);
        }
        for (int i = 1; i <= 3; i++)
        {
            double rB = 230 + i * 100;
            double aB = (Math.Sin(DecT(now) * 0.00072 + i * 0.9) + 1) / 2;
            b = SBrush(FA(Alpha(acc, R((20 - i * 4) * k * (0.55 + 0.45 * aB))), f));
            FillEll(x - rB * 0.26, y - rB * 0.40, rB, rB, b);
        }
        for (int i = 1; i <= 3; i++)
        {
            double rB = 210 + i * 90;
            double aB = (Math.Sin(DecT(now) * 0.00061 + 2.1 + i * 0.9) + 1) / 2;
            b = SBrush(FA(Alpha(acc, R((17 - i * 4) * k * (0.55 + 0.45 * aB))), f));
            FillEll(x + w - rB * 0.70, y + h - rB * 0.58, rB, rB, b);
        }
        double dw2 = Math.Sin(DecT(now) * 0.00043);
        FillRect(x, y, w, h, LineBrush(x - 120 + dw2 * 90, y - 40, w + 240, h + 80, FA(Alpha(acc, 0), f), FA(Alpha(acc, R(26 * k)), f), 2));
        double vw = Math.Min(150, w * 0.22);
        FillRect(x, y, vw, h, HBrush(x, y, vw, h, FA(Alpha(0x000000, R(46 * k)), f), Alpha(0x000000, 0)));
        FillRect(x + w - vw, y, vw, h, HBrush(x + w - vw, y, vw, h, Alpha(0x000000, 0), FA(Alpha(0x000000, R(52 * k)), f)));
        double vh = Math.Min(90, h * 0.22);
        FillRect(x, y + h - vh, w, vh, VBrush(x, y + h - vh, w, vh, Alpha(0x000000, 0), FA(Alpha(0x000000, R(40 * k)), f)));
    }

    static void PanelBackdropLight(double x, double y, double w, double h, uint acc, double f, long now, double k)
    {
        double gp = 44;
        var pnMin = Pen(FA(Alpha(0x000000, R(13 * k)), f), 1);
        var pnMaj = Pen(FA(Alpha(0x000000, R(24 * k)), f), 1);
        int i = 0;
        for (double xx = x + gp / 2; xx < x + w; xx += gp, i++) Line(xx, y, xx, y + h, i % 4 != 0 ? pnMin : pnMaj);
        i = 0;
        for (double yy = y + gp / 2; yy < y + h; yy += gp, i++) Line(x, yy, x + w, yy, i % 4 != 0 ? pnMin : pnMaj);
        var b = SBrush(FA(Alpha(0x000000, R(30 * k)), f));
        for (double yy = y + gp / 2; yy < y + h; yy += gp * 4)
            for (double xx = x + gp / 2; xx < x + w; xx += gp * 4)
            {
                FillRR(xx - 2.6, yy - 0.5, 5.2, 1, 0.5, b);
                FillRR(xx - 0.5, yy - 2.6, 1, 5.2, 0.5, b);
            }
        double mgx = x + gp * 1.5;
        var pn = Pen(FA(Alpha(acc, R(70 * k)), f), 1);
        Line(mgx, y, mgx, y + h, pn);
        Line(mgx + 3, y, mgx + 3, y + h, pn);
        double lp = 6;
        pn = Pen(FA(Alpha(0x000000, R(5 * k)), f), 1);
        for (double yy = y + lp / 2; yy < y + h; yy += lp) Line(x, yy, x + w, yy, pn);
        FillRect(x, y, 26, h, HBrush(x, y, 26, h, FA(Alpha(0x000000, R(26 * k)), f), Alpha(0x000000, 0)));
        FillRect(x, y, w, 18, VBrush(x, y, w, 18, FA(Alpha(0xFFFFFF, R(70 * k)), f), Alpha(0xFFFFFF, 0)));
        double cm = 14, ins = 22;
        pn = Pen(FA(Alpha(0x000000, R(46 * k)), f), 1);
        for (int j = 1; j <= 4; j++)
        {
            double cx = (j == 1 || j == 3) ? x + ins : x + w - ins;
            double cy = (j <= 2) ? y + ins : y + h - ins;
            Line(cx - cm / 2, cy, cx + cm / 2, cy, pn);
            Line(cx, cy - cm / 2, cx, cy + cm / 2, pn);
        }
        for (int j = 1; j <= 3; j++)
        {
            double rB = 260 + j * 110;
            double aB = (Math.Sin(DecT(now) * 0.00072 + j * 0.9) + 1) / 2;
            b = SBrush(FA(Alpha(acc, R((15 - j * 3) * k * (0.55 + 0.45 * aB))), f));
            FillEll(x - rB * 0.30, y - rB * 0.42, rB, rB, b);
        }
        for (int j = 1; j <= 2; j++)
        {
            double rB = 230 + j * 100;
            double aB = (Math.Sin(DecT(now) * 0.00061 + 2.1 + j * 0.9) + 1) / 2;
            b = SBrush(FA(Alpha(acc, R((13 - j * 3) * k * (0.55 + 0.45 * aB))), f));
            FillEll(x + w - rB * 0.68, y + h - rB * 0.56, rB, rB, b);
        }
        for (int j = 1; j <= 3; j++)
        {
            double rr = 190 + j * 150;
            double ph = (Math.Sin(DecT(now) * 0.00055 + j * 0.7) + 1) / 2;
            pn = Pen(FA(Alpha(acc, R((26 - j * 5) * k * (0.5 + 0.5 * ph))), f), 1.2);
            Arc(x - rr * 0.45, y - rr * 0.5, rr * 2, rr * 2, 8, 74, pn);
        }
        double dw2 = Math.Sin(DecT(now) * 0.00043);
        FillRect(x, y, w, h, LineBrush(x - 120 + dw2 * 90, y - 40, w + 240, h + 80, FA(Alpha(acc, 0), f), FA(Alpha(acc, R(20 * k)), f), 2));
        double vw = Math.Min(150, w * 0.22);
        FillRect(x, y, vw, h, HBrush(x, y, vw, h, FA(Alpha(0x000000, R(15 * k)), f), Alpha(0x000000, 0)));
        FillRect(x + w - vw, y, vw, h, HBrush(x + w - vw, y, vw, h, Alpha(0x000000, 0), FA(Alpha(0x000000, R(18 * k)), f)));
        double vh = Math.Min(90, h * 0.22);
        FillRect(x, y + h - vh, w, vh, VBrush(x, y + h - vh, w, vh, Alpha(0x000000, 0), FA(Alpha(0x000000, R(16 * k)), f)));
    }

    // ---- MicroBackdrop: the same idea at chip / field scale ----
    public static void MicroBackdrop(double x, double y, double w, double h, double r, uint acc, double f, long now, double k = 1.0)
    {
        if (HubState.LowPerf) return;
        if (w < 20 || h < 8 || f <= 0.02) return;
        if (HubState.Lean) return;
        int sv = PushG();
        ClipRR(x, y, w, h, r);
        double dk = k * (1 - HubState.ThT), lt = k * HubState.ThT;
        if (R(8 * dk) >= 1)
        {
            double gp = Math.Max(12, Math.Min(18, h * 0.8));
            double off = (DecT(now) * 0.004) % gp;
            var pn = Pen(FA(Alpha(0xFFFFFF, R(8 * dk)), f), 1);
            if (pn is not null)
                for (int kk = -(int)Math.Ceiling(h / gp); kk < Math.Ceiling(w / gp) + 1; kk++)
                {
                    double gx = x + kk * gp + off;
                    Line(gx, y, gx + h, y + h, pn);
                }
        }
        if (R(7 * lt) >= 1)
        {
            double gp2 = Math.Max(14, Math.Min(22, w / 5));
            var pn = Pen(FA(Alpha(0x000000, R(7 * lt)), f), 1);
            if (pn is not null)
                for (double xx = x + gp2 / 2; xx < x + w; xx += gp2) Line(xx, y, xx, y + h, pn);
        }
        double rB = Math.Max(w, h) * 0.8;
        double aB = (Math.Sin(DecT(now) * 0.0009) + 1) / 2;
        FillEll(x - rB * 0.20, y - rB * 0.35, rB, rB, SBrush(FA(Alpha(acc, R(14 * k * (0.5 + 0.5 * aB))), f)));
        double sh = Math.Min(h * 0.5, 12);
        if (R(14 * dk) >= 1)
            FillRect(x, y, w, sh, VBrush(x, y, w, sh, FA(Alpha(0xFFFFFF, R(14 * dk)), f), Alpha(0xFFFFFF, 0)));
        if (R(20 * lt) >= 1)
            Line(x + r, y + 0.5, x + w - r, y + 0.5, Pen(FA(Alpha(0x000000, R(20 * lt)), f), 1));
        double vg = Math.Min(h * 0.45, 12);
        FillRect(x, y + h - vg, w, vg, VBrush(x, y + h - vg, w, vg, Alpha(0x000000, 0), FA(Alpha(0x000000, R(34 * dk + 18 * lt)), f)));
        Pop(sv);
    }


    // ---- MiniBackdrop: the texture at card scale (tiles, rows, panels) ----
    public static void MiniBackdrop(double x, double y, double w, double h, double r, uint acc, double f, long now, double k = 1.0)
    {
        if (HubState.LowPerf) return;                        // LOW PERFORMANCE MODE: flat surfaces
        if (w < 24 || h < 16) return;
        if (f <= 0.004 || HubState.Lean) return;             // LEAN: no live texture
        double dk = k * (1 - HubState.ThT), lt = k * HubState.ThT;
        if (dk > 0.004) MiniBackdropDark(x, y, w, h, r, acc, f, now, dk);
        if (lt > 0.004) MiniBackdropLight(x, y, w, h, r, acc, f, now, lt);
    }
    static void MiniBackdropDark(double x, double y, double w, double h, double r, uint acc, double f, long now, double k)
    {
        int svM = PushG();
        ClipRR(x, y, w, h, r);
        double off = (DecT(now) * 0.005) % 28;
        var pn = Pen(FA(Alpha(0xFFFFFF, R(9 * k)), f), 1);
        for (int kk = -(int)Math.Ceiling(h / 28); kk < Math.Ceiling(w / 28) + 1; kk++)
        {
            double gx = x + kk * 28 + off;
            Line(gx, y, gx + h, y + h, pn);
        }
        for (int i = 1; i <= 2; i++)
        {
            double rB = Math.Max(w, h) * (0.55 + i * 0.28);
            double aB = (Math.Sin(DecT(now) * 0.00078 + i * 1.1) + 1) / 2;
            FillEll(x - rB * 0.22, y - rB * 0.34, rB, rB, SBrush(FA(Alpha(acc, R((17 - i * 6) * k * (0.5 + 0.5 * aB))), f)));
        }
        double rC = Math.Max(w, h) * 0.7;
        FillEll(x + w - rC * 0.62, y + h - rC * 0.55, rC, rC, SBrush(FA(Alpha(acc, R(7 * k)), f)));
        double sh = Math.Min(h * 0.45, 26);
        FillRect(x, y, w, sh, VBrush(x, y, w, sh, FA(Alpha(0xFFFFFF, R(16 * k)), f), Alpha(0xFFFFFF, 0)));
        double vg = Math.Min(h * 0.42, 30);
        FillRect(x, y + h - vg, w, vg, VBrush(x, y + h - vg, w, vg, Alpha(0x000000, 0), FA(Alpha(0x000000, R(44 * k)), f)));
        double rA = Math.Max(w, h) * 0.9;
        double ph = (Math.Sin(DecT(now) * 0.0006) + 1) / 2;
        Arc(x - rA * 0.35, y - rA * 0.42, rA * 2, rA * 2, 10, 66, Pen(FA(Alpha(acc, R(13 * k * (0.5 + 0.5 * ph))), f), 1.2));
        Pop(svM);
    }
    static void MiniBackdropLight(double x, double y, double w, double h, double r, uint acc, double f, long now, double k)
    {
        int svM = PushG();
        ClipRR(x, y, w, h, r);
        double gp = 22;
        var pn = Pen(FA(Alpha(0x000000, R(9 * k)), f), 1);
        for (double xx = x + gp / 2; xx < x + w; xx += gp) Line(xx, y, xx, y + h, pn);
        for (double yy = y + gp / 2; yy < y + h; yy += gp) Line(x, yy, x + w, yy, pn);
        for (int i = 1; i <= 2; i++)
        {
            double rB = Math.Max(w, h) * (0.55 + i * 0.28);
            double aB = (Math.Sin(DecT(now) * 0.00078 + i * 1.1) + 1) / 2;
            FillEll(x - rB * 0.22, y - rB * 0.34, rB, rB, SBrush(FA(Alpha(acc, R((14 - i * 5) * k * (0.5 + 0.5 * aB))), f)));
        }
        Line(x + r, y + 0.5, x + w - r, y + 0.5, Pen(FA(Alpha(0x000000, R(22 * k)), f), 1));
        double vg = Math.Min(h * 0.42, 30);
        FillRect(x, y + h - vg, w, vg, VBrush(x, y + h - vg, w, vg, Alpha(0x000000, 0), FA(Alpha(0x000000, R(20 * k)), f)));
        double rA = Math.Max(w, h) * 0.9;
        double ph = (Math.Sin(DecT(now) * 0.0006) + 1) / 2;
        Arc(x - rA * 0.35, y - rA * 0.42, rA * 2, rA * 2, 10, 66, Pen(FA(Alpha(acc, R(22 * k * (0.5 + 0.5 * ph))), f), 1.2));
        Pop(svM);
    }

    // ---- FadeLine(x1, x2, y, argb, f): a hairline that fades out toward both ends ----
    public static void FadeLine(double x1, double x2, double y, uint argb, double f)
    {
        argb = ElA(TH(argb));
        if (HubState.LowPerf) { FillRect(x1, y, x2 - x1, 1, SBrushP(FA(argb, f * 0.7))); return; }
        uint c1 = FA(argb, f), c0 = argb & 0xFFFFFF;
        double mid = (x1 + x2) / 2;
        FillRect(x1, y, mid - x1, 1, LineBrush(x1 - 1, y - 2, mid - x1 + 2, 4, c0, c1, 0));
        FillRect(mid, y, x2 - mid, 1, LineBrush(mid - 1, y - 2, x2 - mid + 2, 4, c1, c0, 0));
    }

    // ---- CornerFlourish(x, y, w, h, col, f2): the four animated corner arcs ----
    public static void CornerFlourish(double x, double y, double w, double h, uint col, double f2 = 1.0)
    {
        long now2 = Clock.Tick;
        for (int k = 1; k <= 4; k++)
        {
            double cxr = (k == 1 || k == 3) ? x + 4 : x + w - 4;
            double cyr = (k <= 2) ? y + 4 : y + h - 4;
            double qa = k == 1 ? 0 : k == 2 ? 90 : k == 4 ? 180 : 270;
            double a0 = qa + 12 + 8 * Math.Sin(now2 * 0.0021 + k * 1.6);
            double swp = 58 + 12 * Math.Sin(now2 * 0.0033 + k);
            Arc(cxr - 15, cyr - 15, 30, 30, a0, swp, Pen(Alpha(col, R((110 + 60 * Math.Sin(now2 * 0.003 + k)) * f2)), 1.5));
            Arc(cxr - 10, cyr - 10, 20, 20, a0 + 12, swp * 0.7, Pen(Alpha(col, R(66 * f2)), 1));
            double da = (a0 + swp * (0.5 + 0.5 * Math.Sin(now2 * 0.0026 + k * 1.3))) * 0.0174533;
            FillEll(cxr + 15 * Math.Cos(da) - 1.6, cyr + 15 * Math.Sin(da) - 1.6, 3.2, 3.2, SBrush(Alpha(AccHi(col, 0.5), R(200 * f2))));
        }
    }

    // ---- MiniHeart(x, y, ms, argb): two circles and a wedge ----
    public static void MiniHeart(double x, double y, double ms, uint argb)
    {
        if (HubState.LowPerf) return;
        var b = Solid(argb);
        if (b is null) return;
        FillEll(x - ms * 0.95, y - ms * 0.72, ms, ms, b);
        FillEll(x - ms * 0.05, y - ms * 0.72, ms, ms, b);
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Avalonia.Point(x - ms * 0.92, y - ms * 0.10), true);
            g.LineTo(new Avalonia.Point(x + ms * 0.92, y - ms * 0.10));
            g.LineTo(new Avalonia.Point(x, y + ms * 0.95));
            g.EndFigure(true);
        }
        FillPath(geo, b);
    }

    // ---- GripDots(cx, cy, a, acc, now, f, pw): the window grip, six dots rippling left to right ----
    public static void GripDots(double cx, double cy, double a, uint acc, long now, double f = 1.0, double pw = 48)
    {
        int st = PushXform(cx, cy, 1 + 0.10 * a, 0);
        if (a > 0.01)
        {
            FillRR(cx - pw / 2, cy - GRIP_H / 2.0, pw, GRIP_H, 8, SBrush(FA(Alpha(acc, R(26 * a)), f)));
            StrokeRR(cx - pw / 2, cy - GRIP_H / 2.0, pw, GRIP_H, 8, Pen(FA(Alpha(acc, R(70 * a)), f), 1));
        }
        for (int k = 1; k <= 6; k++)
        {
            int gc = (k - 1) % 3, gr = (k - 1) / 3;
            double gx = cx - GRIP_PX + gc * GRIP_PX;
            double gy = cy - GRIP_PY / 2.0 + gr * GRIP_PY;
            double wv = 0.5 + 0.5 * Math.Sin(DecT(now) * 0.005 - gc * 0.9);
            double r6 = 1.6 + 0.5 * a;
            FillEll(gx - r6, gy - r6, r6 * 2, r6 * 2, SBrush(FA(Alpha(Mix(0xFFC7CBE0, acc, a), R(90 + 60 * a + 70 * a * wv)), f)));
        }
        Pop(st);
    }

    // ---- GroupLine(x1, x2, y, acc, f, now): a section rule with a travelling spark ----
    public static void GroupLine(double x1, double x2, double y, uint acc, double f, long now)
    {
        if (x2 - x1 <= 24) return;
        FadeLine(x1, x2, y, Alpha(0xFFFFFF, 52), f);
        double phz = (DecT(now) % 3400) / 3400.0;
        if (phz < 0.42)
        {
            double t2 = phz / 0.42;
            double hx = x1 + (x2 - x1) * Ease3(t2);
            double ha = Math.Sin(3.14159 * t2);
            FillRR(hx - 22, y, 22, 1, 0.5, HBrush(hx - 22, y - 1, 22, 2, FA(Alpha(acc, 0), f), FA(Alpha(acc, R(165 * ha)), f)));
            FillRR(hx, y, 22, 1, 0.5, HBrush(hx, y - 1, 22, 2, FA(Alpha(acc, R(165 * ha)), f), FA(Alpha(acc, 0), f)));
            FillEll(hx - 1.6, y - 1.6, 3.2, 3.2, SBrush(FA(Alpha(AccHi(acc, 0.4), R(210 * ha)), f)));
        }
        FillEll(x2 - 2, y - 2, 4, 4, SBrush(FA(Alpha(acc, 90 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.0022))), f)));
    }

    // ---- ChipDraw(x, y, w, h, label, hv, base, f): a chip, scaled up a touch on hover ----
    public static void ChipDraw(double x, double y, double w, double h, string label, double hv, uint bas, double f)
    {
        int st = PushXform(x + w / 2, y + h / 2, 1 + 0.05 * hv, 0);
        FillRR(x, y, w, h, 7, VBrush(x, y, w, h, FA(Mix(0xFF31344C, 0xFF41466A, hv), f), FA(Mix(0xFF20223A, 0xFF262A45, hv), f)));
        MicroBackdrop(x, y, w, h, 7, bas, f, Clock.Tick, 0.9);
        StrokeRR(x, y, w, h, 7, Pen(FA(Alpha(bas, 110 + 90 * hv), f), 1));
        Txt(label, x, y - 1, w, h, Fonts.fBadge, FA(Alpha(AccHi(bas, 0.35), 215 + 40 * hv), f), Fmt.C);
        Pop(st);
    }

    // ---- ModRing(x, y, w, h, r, acc, f, at, now, dur, amt, grow): the ring a press or a switch leaves behind ----
    public static void ModRing(double x, double y, double w, double h, double r, uint acc, double f, long at, long now, double dur, double amt, bool grow)
    {
        if (HubState.LowPerf) return;
        if (at == 0 || now - at >= dur) return;
        double e = Ease3((now - at) / dur);
        double ex = (grow ? e : 1 - e) * amt;
        StrokeRR(x - ex, y - ex * 0.6, w + ex * 2, h + ex * 1.2, r + ex * 0.35, Pen(FA(Alpha(acc, R(165 * (1 - e))), f), 2.2 * (1 - e) + 0.4));
    }
}
