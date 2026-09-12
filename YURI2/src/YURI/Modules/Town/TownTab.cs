using Avalonia.Media;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.Town;

/// <summary>
/// TOWN (tab 7, behind the heart): NIGHTFALL, MAIN STREET and GREENVALE on
/// the 142 x 101 pixel grid at four screen pixels a cell, the spark that
/// appears now and then (a warm one at a spot, or a cold comet crossing the
/// sky worth three), the burst and the +n when it is taken, the level
/// ribbon, and the progress card with the next reward. Five levels a town,
/// kept in zeal.ini. FLAPPY's chip is here; its game arrives next.
/// </summary>
public static class TownTab
{
    static readonly string[] Names = { "NIGHTFALL", "MAIN STREET", "GREENVALE", "FLAPPY" };
    static readonly Random _rnd = new();
    public static bool Animating => true;                                    // the towns live on their own clocks
    static double Vx(HubLayout HL) => HL.ctx;
    static double Vy(HubLayout HL) => HL.cty + 36;
    const double VW = 142 * 4 + 2, VH = 101 * 4;
    static (double x, double y) SparkXY(HubLayout HL) => (HL.ctx + 1 + Tw.sx * 4 + 2, HL.cty + 36 + Tw.sy * 4 + 2);

    public static void Register(HubSurface hub)
    {
        Tw.Load(); Fl.Load();
        hub.TabBodies[7] = (f, dx, dy2, now) => Draw(hub, f, dx, dy2, now);
    }
    public static void Pick(int k)
    {
        if (k == Tw.Town) return;
        Tw.TownPrev = Tw.Town; Tw.TownAt = Clock.Tick;
        Tw.Town = k;
        Ini.Write(Paths.IniFile, "hub", "town", (long)k);
        Tw.at = 0; Tw.next = Clock.Tick + 1500;
        if (k != 4) Fl.Leave();                                              // leaving FLAPPY ends the game where it stands, and closes the shop
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    static double[][] Spots(int k) => k == 2
        ? new[] { new double[] { 20, 12 }, new double[] { 70, 20 }, new double[] { 120, 14 }, new double[] { 40, 40 }, new double[] { 100, 44 }, new double[] { 10, 60 }, new double[] { 130, 62 }, new double[] { 60, 78 }, new double[] { 90, 78 }, new double[] { 30, 75 }, new double[] { 110, 75 }, new double[] { 80, 8 } }
        : k == 3
        ? new[] { new double[] { 10, 40 }, new double[] { 60, 30 }, new double[] { 120, 44 }, new double[] { 40, 92 }, new double[] { 90, 40 }, new double[] { 130, 20 }, new double[] { 8, 70 }, new double[] { 70, 70 }, new double[] { 100, 10 }, new double[] { 50, 12 }, new double[] { 125, 92 }, new double[] { 80, 92 } }
        : new[] { new double[] { 20, 20 }, new double[] { 50, 12 }, new double[] { 90, 18 }, new double[] { 120, 30 }, new double[] { 30, 70 }, new double[] { 110, 72 }, new double[] { 10, 84 }, new double[] { 130, 84 }, new double[] { 70, 10 }, new double[] { 40, 40 }, new double[] { 100, 50 }, new double[] { 60, 68 } };
    /// <summary>TownCollect(): the spark is taken - one, two while it is fresh, three for the comet.</summary>
    public static void Collect()
    {
        if (Tw.at == 0) return;
        int k = Tw.Town; if (k == 4) return;
        int worth = Tw.kind == 2 ? 3 : Clock.Tick - Tw.at < 8000 ? 2 : 1;
        int was = Tw.N[k - 1];
        Tw.N[k - 1] += worth;
        Ini.Write(Paths.IniFile, "town", "n" + k, (long)Tw.N[k - 1]);
        Tw.burstAt = Clock.Tick; Tw.bx = Tw.sx; Tw.by = Tw.sy; Tw.plusAt = Clock.Tick; Tw.plusN = worth;
        int lvNew = Tw.LevelOf(Tw.N[k - 1]);
        if (lvNew > Tw.LevelOf(was)) { Tw.lvAt = Clock.Tick; Tw.lvNew = lvNew; }
        Tw.at = 0; Tw.next = Clock.Tick + 6000 + _rnd.Next(0, 9001);
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double y0 = HL.cty;
        for (int i = 1; i <= 4; i++) { double cx = HL.ctx + HL.ctw - 40 - (5 - i) * 84; if (ux >= cx && ux <= cx + 78 && uy >= y0 - 3 && uy <= y0 + 21) return 2220 + i; }
        if (Tw.at != 0 && Tw.Town != 4) { var (sx, sy) = SparkXY(HL); if ((ux - sx) * (ux - sx) + (uy - sy) * (uy - sy) <= 18 * 18) return 2225; }
        if (ux >= Vx(HL) && ux <= Vx(HL) + VW && uy >= Vy(HL) && uy <= Vy(HL) + VH) return Tw.Town == 4 ? Fl.Zone(HL, ux, uy) : 2226;
        return 0;
    }
    public static bool Click(HubSurface hub, int z)
    {
        switch (z)
        {
            case >= 2221 and <= 2224: hub.ClickAt[z] = Clock.Tick; Pick(z - 2220); return true;
            case 2225: Collect(); return true;
        }
        if (Tw.Town == 4 && Fl.Click(hub, z)) return true;
        return z == 2226;
    }
    static void Chips(HubSurface hub, double x0, double y0, uint acc, double f)
    {
        var HL = hub.HL;
        for (int i = 1; i <= 4; i++) { double cx = x0 + HL.ctw - 40 - (5 - i) * 84; FFMBtn(2220 + i, cx, y0 - 3, 78, 24, Names[i - 1], acc, f, Tw.Town == i ? 1 : 0, HL.fS); }
    }
    public static void Draw(HubSurface hub, double f, double dx, double dy2, long now)
    {
        var HL = hub.HL;
        uint acc = HubState.Cur;
        double x0 = HL.ctx + dx, y0 = HL.cty + dy2;
        Chips(hub, x0, y0, acc, f);
        double vx = Vx(HL) + dx, vy = Vy(HL) + dy2;
        int st = PushG(); ClipRR(vx, vy, VW, VH, 14);
        const double cs = 4.0;
        double ox = vx + 1, oy = vy;
        double tw = Tw.TownAt != 0 ? Ease3(Clamp((now - Tw.TownAt) / 460.0, 0.0, 1.0)) : 1.0;
        if (tw >= 1 && Tw.TownAt != 0) { Tw.TownAt = 0; Tw.TownPrev = 0; }
        if (tw < 1 && Tw.TownPrev != 0) Scene(hub, Tw.TownPrev, ox - 24 * tw, oy, cs, acc, f * (1 - tw), now);
        Scene(hub, Tw.Town, ox + 24 * (1 - tw), oy, cs, acc, f * tw, now);
        if (Tw.Town != 4) Game(hub, ox + 24 * (1 - tw), oy, cs, acc, f * tw, now);
        Pop(st);
        StrokeRR(vx, vy, VW, VH, 14, Pen(FA(Alpha(acc, 60), f), 1));
    }
    static void Scene(HubSurface hub, int k, double ox, double oy, double cs, uint acc, double f, long now)
    {
        var sc = new TownScene(hub, ox, oy, cs, acc, f, now);
        if (k == 4) Fl.Draw(hub, ox, oy, cs, acc, f, now);
        else if (k == 2) sc.Street();
        else if (k == 3) sc.Village();
        else sc.Night();
    }
    /// <summary>TownGame: the spark, the burst, the +n, the level ribbon, the progress card.</summary>
    static void Game(HubSurface hub, double ox, double oy, double cs, uint acc, double f, long now)
    {
        var HL = hub.HL;
        int k = Tw.Town;
        if (Tw.at == 0 && now >= Tw.next && Tw.TownAt == 0)
        {
            Tw.kind = _rnd.Next(1, 6) == 1 ? 2 : 1;
            if (Tw.kind == 2) { Tw.sx = -12; Tw.sy = 18; Tw.at = now; }
            else
            {
                var spots = Spots(k); int i = _rnd.Next(1, spots.Length + 1);
                if (i == Tw.last) i = i % spots.Length + 1;
                Tw.last = i; Tw.sx = spots[i - 1][0]; Tw.sy = spots[i - 1][1]; Tw.at = now;
            }
        }
        if (Tw.at != 0 && Tw.kind == 2)
        {
            double cp = (now - Tw.at) / 7000.0;
            Tw.sx = -12 + Math.Round(166 * cp); Tw.sy = 22 + Math.Round(10 * Math.Sin(cp * 3.14159 * 1.5)) - Math.Round(6 * cp);
            if (cp > 1.05) { Tw.at = 0; Tw.next = now + 2500; }
        }
        else if (Tw.at != 0 && now - Tw.at > 30000) { Tw.at = 0; Tw.next = now + 3000; }
        if (Tw.at != 0)
        {
            long age = now - Tw.at;
            double ain = Ease3(Math.Min(age / 380.0, 1.0)), aout = age > 28500 ? 1 - (age - 28500) / 1500.0 : 1.0;
            double hv = hub.Hv(2225), sc = ain * (1 + 0.3 * hv) * aout, bob = 2 * Math.Sin(DecT(now) * 0.004);
            double spx = ox + Tw.sx * cs + 2, spy = oy + Tw.sy * cs + 2 + bob, pulse = 0.7 + 0.3 * Math.Sin(DecT(now) * 0.006);
            uint cG = Tw.kind == 2 ? 0xFF9FD3F0 : 0xFFFFE08A, cC = Tw.kind == 2 ? 0xFFEAF6FF : 0xFFFFFBEA;
            if (Tw.kind == 2)
                for (int q = 1; q <= 7; q++)
                {
                    double cq = Math.Max((now - Tw.at) / 7000.0 - q * 0.018, 0.0);
                    double qx = ox + (-12 + 166 * cq) * cs + 2, qy = oy + (22 + 10 * Math.Sin(cq * 3.14159 * 1.5) - 6 * cq) * cs + 2 + bob;
                    FillEll(qx - (5 - q * 0.5), qy - (5 - q * 0.5), 10 - q, 10 - q, SBrushP(FA(Alpha(cG, R((150 - q * 18) * aout)), f)));
                }
            FillEll(spx - 14 * sc, spy - 14 * sc, 28 * sc, 28 * sc, SBrushP(FA(Alpha(cG, R((40 + 30 * pulse + 40 * hv) * aout)), f)));
            FillEll(spx - 7 * sc, spy - 7 * sc, 14 * sc, 14 * sc, SBrushP(FA(Alpha(cG, R((110 + 40 * pulse) * aout)), f)));
            var pn = PenP(FA(Alpha(cC, R(200 * aout)), f), 1.4);
            double ra = DecT(now) * 0.0012;
            for (int q = 0; q < 4; q++) { double an = ra + q * 1.5708; Line(spx + 5 * sc * Math.Cos(an), spy + 5 * sc * Math.Sin(an), spx + (10 + 3 * pulse) * sc * Math.Cos(an), spy + (10 + 3 * pulse) * sc * Math.Sin(an), pn); }
            FillEll(spx - 3.2 * sc, spy - 3.2 * sc, 6.4 * sc, 6.4 * sc, SBrushP(FA(Alpha(cC, R(240 * aout)), f)));
            if (hv > 0.02) { var pnD = PenP(FA(Alpha(cG, R(160 * hv * aout)), f), 1); PenDash(pnD, 2); Ell(spx - 18, spy - 18, 36, 36, pnD); }
            if (Tw.kind == 1 && age < 8000) Arc(spx - 12, spy - 12, 24, 24, -90, 360 * (1 - age / 8000.0), PenP(FA(Alpha(0xFFFFE08A, R(120 * aout)), f), 1.2));
        }
        if (Tw.burstAt != 0 && now - Tw.burstAt < 720)
        {
            double e = (now - Tw.burstAt) / 720.0, ee = 1 - (1 - e) * (1 - e), bx = ox + Tw.bx * cs + 2, by = oy + Tw.by * cs + 2;
            for (int q = 1; q <= 12; q++) { double an = (q - 1) * 0.5236 + 0.26, d = 6 + 30 * ee, r = 3 * (1 - e) + 0.6; FillEll(bx + d * Math.Cos(an) - r, by + d * Math.Sin(an) + 6 * ee * ee - r, r * 2, r * 2, SBrushP(FA(Alpha(q % 3 != 0 ? 0xFFFFE08A : 0xFFFFFBEA, R(230 * (1 - e))), f))); }
            Ell(bx - 4 - 24 * ee, by - 4 - 24 * ee, 8 + 48 * ee, 8 + 48 * ee, PenP(FA(Alpha(0xFFFFF4C8, R(180 * (1 - e))), f), 2 * (1 - e) + 0.5));
        }
        else if (Tw.burstAt != 0) Tw.burstAt = 0;
        if (Tw.plusAt != 0 && now - Tw.plusAt < 1000)
        {
            double e = (now - Tw.plusAt) / 1000.0;
            TxtP("+" + Tw.plusN, ox + Tw.bx * cs - 24, oy + Tw.by * cs - 16 - 22 * Ease3(e), 52, 20, Tw.plusN > 1 ? HL.fV : HL.fS, FA(Alpha(Tw.plusN >= 3 ? 0xFFB8E4FF : 0xFFFFF4C8, R(245 * (1 - e * e))), f), Fmt.C);
        }
        else if (Tw.plusAt != 0) Tw.plusAt = 0;
        if (Tw.lvAt != 0 && now - Tw.lvAt < 3600)
        {
            double e = (now - Tw.lvAt) / 3600.0, inn = Ease3(Math.Min(e * 6, 1.0)), outT = e > 0.85 ? 1 - Ease3((e - 0.85) / 0.15) : 1.0;
            double rw = 300, rx = ox + 142 * cs / 2 - rw / 2, ry = oy + 12 - 30 * (1 - inn);
            FillRR(rx, ry, rw, 30, 8, SBrushP(FA(Alpha(0xFF12141F, R(200 * inn * outT)), f)));
            StrokeRR(rx, ry, rw, 30, 8, PenP(FA(Alpha(0xFFFFE08A, R(170 * inn * outT)), f), 1));
            double shx = rx + (now - Tw.lvAt) % 1400 / 1400.0 * rw;
            FillRR(shx - 14, ry, 28, 30, 0, SBrushP(FA(Alpha(0xFFFFF4C8, R(60 * inn * outT)), f)));
            TxtP("LEVEL " + Tw.lvNew + "  \u00B7  " + Tw.REW[k - 1][Math.Clamp(Tw.lvNew, 1, 5) - 1] + " arrives", rx, ry + 7, rw, 16, HL.fS, FA(Alpha(0xFFFFF4C8, R(245 * inn * outT)), f), Fmt.C);
            for (int q = 1; q <= 24; q++) { double an = (q - 1) * 0.2618, d = 20 + 90 * Ease3(Math.Min(e * 1.6, 1.0)); FillEll(rx + rw / 2 + d * Math.Cos(an) - 1.5, ry + 15 + d * Math.Sin(an) * 0.6 + 30 * e * e - 1.5, 3, 3, SBrushP(FA(Alpha(q % 2 != 0 ? 0xFFFFE08A : 0xFFFFFBEA, R(200 * (1 - Math.Min(e * 1.6, 1.0)))), f))); }
        }
        else if (Tw.lvAt != 0) Tw.lvAt = 0;
        int n = Tw.N[k - 1], lv = Tw.LevelOf(n), nxt = lv < Tw.LV.Length ? Tw.LV[lv] : 0;
        double pw2 = 196, ph2 = 44, hx = ox + 142 * cs - pw2 - 12, hy = oy + 101 * cs - ph2 - 10;
        bool done = nxt == 0;
        FillRR(hx, hy, pw2, ph2, 9, SBrushP(FA(Alpha(0xFF12141F, 175), f)));
        StrokeRR(hx, hy, pw2, ph2, 9, PenP(FA(Alpha(0xFFFFE08A, done ? 150 : 70), f), done ? 1.5 : 1));
        double pulse2 = 0.7 + 0.3 * Math.Sin(DecT(now) * 0.004);
        FillEll(hx + 7, hy + 9, 26, 26, SBrushP(FA(Alpha(0xFFFFE08A, R(60 * pulse2)), f)));
        FillEll(hx + 14, hy + 16, 12, 12, SBrushP(FA(0xFFFFE08A, f)));
        FillEll(hx + 17, hy + 19, 6, 6, SBrushP(FA(0xFFFFFBEA, f)));
        TxtP(n + "  \u00B7  LEVEL " + lv, hx + 38, hy + 6, 150, 13, HL.fS, FA(Alpha(0xFFFFF4C8, 235), f), Fmt.L);
        if (nxt != 0)
        {
            int pv = lv != 0 ? Tw.LV[lv - 1] : 0;
            double fr = Clamp((n - pv) / (nxt - pv + 0.0), 0.0, 1.0);
            if (Tw.barK != k) { Tw.barK = k; Tw.barE = fr; }
            Tw.barE += (fr - Tw.barE) * EK(0.12);
            FillRR(hx + 38, hy + 22, pw2 - 50, 4, 2, SBrushP(FA(Alpha(0xFFFFE08A, 50), f)));
            FillRR(hx + 38, hy + 22, (pw2 - 50) * Tw.barE, 4, 2, SBrushP(FA(0xFFFFE08A, f)));
            if (Tw.barE > 0.02) FillEll(hx + 38 + (pw2 - 50) * Tw.barE - 2, hy + 22, 4, 4, SBrushP(FA(Alpha(0xFFFFFBEA, 200), f)));
            TxtP(FFMElide((nxt - n) + " more: " + Tw.REW[k - 1][lv], HL.fXs, pw2 - 50), hx + 38, hy + 29, pw2 - 50, 12, HL.fXs, FA(Alpha(0xFFFFF4C8, 150), f), Fmt.L);
        }
        else
        {
            Tw.barE = 1.0;
            TxtP("the town is complete", hx + 38, hy + 22, pw2 - 50, 12, HL.fXs, FA(Alpha(0xFFFFE08A, 220), f), Fmt.L);
            for (int q = 1; q <= 3; q++) { double tw2 = 0.5 + 0.5 * Math.Sin(DecT(now) * 0.0035 + q * 2.1); FillEll(hx + pw2 - 40 + q * 9, hy + 8 + q % 2 * 4, 3, 3, SBrushP(FA(Alpha(0xFFFFFBEA, R(80 + 150 * tw2)), f))); }
        }
    }
}
