using Avalonia.Media;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Modules.Town;

/// <summary>The three pixel towns (TownNight / TownStreet / TownVillage), transliterated from the .ahk's P()-calls onto the 142 x 101 grid at cs = 4.</summary>
public sealed partial class TownScene
{
    public double ox, oy, cs, f; public uint acc; public long now; public HubSurface hub = null!;
    public TownScene(HubSurface h, double ox_, double oy_, double cs_, uint acc_, double f_, long now_) { hub = h; ox = ox_; oy = oy_; cs = cs_; acc = acc_; f = f_; now = now_; }
    // ---- the primitives the .ahk drew with ----
    public void P(double x, double y, double w, double h, double c) { var b = SBrushP(FA((uint)(long)c, f)); if (b is not null) FillRR(ox + x * cs, oy + y * cs, w * cs, h * cs, 0, b); }
    public IBrush? TB(double c, double f2) => SBrushP(FA((uint)(long)c, f2));
    public static double Mix(double a, double b, double t) => Col.Mix((uint)(long)a, (uint)(long)b, Clamp(t, 0.0, 1.0));
    public static double Alpha(double c, double a) => Col.Alpha((uint)(long)c, (int)Math.Round(Clamp(a, 0, 255)));
    public static double AccHi(double c, double t) => Col.AccHi((uint)(long)c, t);
    public static uint FA(double c, double f2) => Col.FA((uint)(long)c, f2);
    public static double Md(double a, double b) => b == 0 ? 0 : a - b * Math.Floor(a / b);
    public static double Fd(double a, double b) => Math.Floor(a / b);
    public static double Rn(double a) => Math.Round(a);
    static readonly Random _rnd = new();
    public static double Rnd(double a, double b) => _rnd.Next((int)a, (int)b + 1);
    public static bool B(bool b) => b;
    public static bool B(double d) => d != 0;
    public static IEnumerable<(int, double)> En(double[] a) { for (int i = 0; i < a.Length; i++) yield return (i + 1, a[i]); }
    public static IEnumerable<(int, double[])> En(double[][] a) { for (int i = 0; i < a.Length; i++) yield return (i + 1, a[i]); }
    public IPen? PenP(uint c, double w) => G.PenP(c, w);
    public void SkyBands(double ox_, double oy_, double cs_, double rows, double cTop, double cBot, double f2, double n = 8)
    {
        for (int k = 1; k <= (int)n; k++)
        {
            double c = Mix(cTop, cBot, (k - 1) / (n - 1.0));
            var b = TB(c, f2);
            FillRR(ox_, oy_ + Math.Round((k - 1) * rows / n) * cs_, 142 * cs_, (Math.Round(k * rows / n) - Math.Round((k - 1) * rows / n)) * cs_, 0, b);
        }
    }
    public void TownWalker(object _, double x, double y, double shirt, double pants, double skin, double hair, double stp, double dir, double hat = 0)
    {
        P(x, y - 6, 2, 2, skin); P(x, y - 7, 2, 1, hair);
        if (hat != 0) { P(x - 1, y - 8, 4, 1, hat); P(x, y - 9, 2, 1, hat); }
        P(x, y - 4, 2, 2, shirt); P(x + (dir > 0 ? 2 : -1), y - 4, 1, 1, skin);
        P(x - stp, y - 2, 1, 2, pants); P(x + 1 + stp, y - 2, 1, 2, pants);
    }
    public void TownTree(object _, double x, double bas, double sw, double trunk, double dark, double light, double big = 0)
    {
        if (big != 0)
        {
            P(x + 4, bas - 7, 3, 7, trunk);
            P(x + 1 + sw, bas - 18, 9, 3, dark); P(x + sw, bas - 15, 11, 5, dark); P(x + 1 + sw, bas - 10, 9, 3, dark);
            P(x + 2 + sw, bas - 17, 4, 1, light); P(x + 1 + sw, bas - 14, 3, 2, light); P(x + 7 + sw, bas - 13, 2, 1, light);
        }
        else
        {
            P(x + 3, bas - 5, 2, 5, trunk);
            P(x + 1 + sw, bas - 11, 6, 2, dark); P(x + sw, bas - 9, 8, 3, dark); P(x + 2 + sw, bas - 6, 4, 1, dark);
            P(x + 2 + sw, bas - 10, 2, 1, light); P(x + 1 + sw, bas - 8, 2, 1, light);
        }
    }
    public void TownCloud(object _, double x, double y, double w, double top, double under)
    {
        P(x, y + 1, w, 3, top); P(x + 2, y, w - 5, 1, top); P(x + Fd(w, 3), y - 1, Fd(w, 3), 1, top);
        P(x + 1, y + 4, w - 2, 1, under);
    }
    public void TownBird(object _, double x, double y, double up, double c) { P(x - 1, y - up, 1, 1, c); P(x, y, 1, 1, c); P(x + 1, y - up, 1, 1, c); }
    static object P_ = new();
    public void Night()
    {
    double t = 0, ph = 0, sunAlt = 0, dayc = 0, night = 0, dusk = 0, rain = 0, wh = 0, warm = 0, cT = 0, cB = 0, k = 0, sx = 0, sy = 0, a = 0, sph = 0, arcP = 0, ax_ = 0, ay_ = 0, an = 0, cTop = 0, cUnd = 0, bx_ = 0, by_ = 0, fl = 0, cFar = 0, sk = 0, sh = 0, cH1 = 0, cH2 = 0, hh = 0, cPine = 0, px = 0, wa = 0, cW = 0, cW2 = 0, bxb = 0, tph = 0, tx_ = 0, sp = 0, cG = 0, cPv = 0, cRd = 0, lk = 0, lx0 = 0, wcx = 0, wcy = 0, wr = 0, gx = 0, gy = 0, bi = 0, bx0 = 0, bw0 = 0, bh0 = 0, bk = 0, top = 0, cWl = 0, r = 0, wy = 0, wx = 0, hsh = 0, lit = 0, gl = 0, cGl = 0, cxk = 0, cSt = 0, ccx = 0, ccy = 0, hrA = 0, mnA = 0, scA = 0, cTw = 0, fp = 0, fcx = 0, fcy = 0, fcl = 0, rr = 0, cLd = 0, cLl = 0, tk = 0, tx0 = 0, dir = 0, px0 = 0, stp = 0, cx1 = 0, cx3 = 0, cx2 = 0, bph = 0, bxs = 0, ck = 0, cxx = 0, lv = 0, q = 0, fx = 0, fy = 0, ba = 0, bx2 = 0, by2 = 0, zx = 0, zy = 0, fph = 0, rp = 0;
    bool on1 = false, onk = false, bl = false, tail = false;
    double[] pal = null!, bd = null!, bx1 = null!;
    double[][] bld = null!, cols = null!;
    IBrush? b = null;
    IPen? pn = null;
    t = DecT(now);
    ph = Md(t, 120000)/120000.0;
    sunAlt = Math.Sin(ph*6.2832);
    dayc = Clamp(0.5 + 0.9*sunAlt, 0.0, 1.0);
    night = 1 - dayc;
    dusk = Clamp(1 - Math.Abs(sunAlt)*2.6, 0.0, 1.0);
    rain = B((Md(t, 100000) < 12000)) ? Clamp(Math.Min(Md(t, 100000), 12000 - Md(t, 100000))/1500.0, 0.0, 1.0) : 0.0;
    wh = 0xFFF4F1E8;
    warm = 0xFFFFD98A;
    cT = Mix(Mix(0xFF0B1230, 0xFF74BDEA, dayc), 0xFF5A3F7A, dusk*0.6);
    cB = Mix(Mix(0xFF2A3466, 0xFFCFE8F7, dayc), 0xFFF2A468, dusk*0.9);
    cT = Mix(cT, 0xFF4A5470, rain*0.5);
    cB = Mix(cB, 0xFF7A8496, rain*0.5);
    SkyBands(ox, oy, cs, 58, cT, cB, f, 14);
    if (B(night > 0.02 && rain < 0.5)) {
        for (int A1 = 1; A1 <= (int)(26); A1++) {
        k = A1;
        sx = Md(k*37 + 11, 142);
        sy = Md(k*23 + 5, 44) + 1;
        a = Rn(night*(1 - rain)*(90 + 130*Math.Abs(Math.Sin(t*0.0029 + k*1.3))));
        P(sx, sy, 1, 1, Alpha(wh, a));
        if (B(Md(k, 5) == 0)) {
        P(sx - 1, sy, 1, 1, Alpha(wh, Fd(a, 2)));
        P(sx + 1, sy, 1, 1, Alpha(wh, Fd(a, 2)));
        P(sx, sy - 1, 1, 1, Alpha(wh, Fd(a, 2)));
        P(sx, sy + 1, 1, 1, Alpha(wh, Fd(a, 2)));
        }
        }
    sph = Md(t, 11000);
    if (B(sph < 640)) {
        for (int A1 = 1; A1 <= (int)(5); A1++) {
        k = A1;
        P(112 - Rn(sph*0.062) + (k - 1), 6 + Rn(sph*0.028) - (k - 1), 1, 1, Alpha(wh, Rn(night*220*(1 - sph/640.0)*(1 - (k - 1)*0.22))));
        }
    }
    }
    arcP = B((ph < 0.5)) ? ph*2 : (ph - 0.5)*2;
    ax_ = 6 + Rn(128*arcP);
    ay_ = 56 - Rn(48*Math.Sin(arcP*3.14159));
    if (B(ph < 0.5)) {
    b = TB(Alpha(0xFFFFE9A8, Rn(40*(1 - rain))), f);
    FillEll(ox + (ax_ - 8)*cs, oy + (ay_ - 8)*cs, 17*cs, 17*cs, b);
    P(ax_ - 3, ay_ - 2, 7, 5, 0xFFFFD86B);
    P(ax_ - 2, ay_ - 3, 5, 7, 0xFFFFD86B);
    P(ax_ - 1, ay_ - 2, 3, 2, 0xFFFFF0B8);
        for (int A1 = 1; A1 <= (int)(8); A1++) {
        an = (A1 - 1)*0.785 + t*0.0003;
        P(ax_ + Rn(6*Math.Cos(an)), ay_ + Rn(6*Math.Sin(an)), 1, 1, Alpha(0xFFFFE9A8, 150));
        }
    } else {
    b = TB(Alpha(wh, Rn(26*night)), f);
    FillEll(ox + (ax_ - 7)*cs, oy + (ay_ - 7)*cs, 15*cs, 15*cs, b);
    P(ax_ - 2, ay_ - 1, 5, 3, wh);
    P(ax_ - 1, ay_ - 2, 3, 5, wh);
    P(ax_ - 1, ay_ - 1, 3, 3, Mix(cT, cB, 0.45));
    P(ax_, ay_ - 2, 2, 5, Mix(cT, cB, 0.45));
    P(ax_ - 2, ay_, 1, 1, 0xFFD8D4C4);
    P(ax_ - 1, ay_ + 1, 1, 1, 0xFFD8D4C4);
    }
    cTop = Mix(Mix(0xFF3A4270, wh, 0.35 + 0.65*dayc), 0xFF8A93A8, rain*0.6);
    cUnd = Mix(Mix(0xFF2A3058, 0xFFC4D6E6, 0.35 + 0.65*dayc), 0xFF6A7488, rain*0.6);
        for (int A1 = 1; A1 <= (int)(5); A1++) {
        k = A1;
        TownCloud(P_, Md(t*(0.0014 + 0.0006*k) + k*37, 178) - 22, 3 + Md(k*7, 22), 11 + Md(k, 3)*4, cTop, cUnd);
        }
    bx_ = Md(t*0.0022, 200) - 24;
    by_ = 12 + Rn(4*Math.Sin(t*0.0009));
    P(bx_ - 2, by_ - 1, 5, 3, 0xFFE8574A);
    P(bx_ - 1, by_ - 2, 3, 5, 0xFFE8574A);
    P(bx_ - 1, by_ - 1, 1, 3, 0xFFFFE08A);
    P(bx_ + 1, by_ - 1, 1, 3, 0xFFB83A2E);
    P(bx_ - 1, by_ + 3, 1, 1, 0xFF8A5A3A);
    P(bx_ + 1, by_ + 3, 1, 1, 0xFF8A5A3A);
    P(bx_ - 1, by_ + 4, 3, 1, 0xFF8A5A3A);
    fl = Md(t*0.019, 190) - 20;
        for (int A1 = 1; A1 <= (int)(5); A1++) {
        k = A1;
        TownBird(P_, fl - Math.Abs(k - 3)*4, 14 + Math.Abs(k - 3)*2 + Rn(1.5*Math.Sin(t*0.003 + k)), Md(Math.Floor(t/210) + k, 2), Mix(0xFF1B2140, 0xFF2B2F45, dayc));
        }
    cFar = Mix(Mix(0xFF1C2650, 0xFF9FC2DC, dayc), 0xFF6A7A90, rain*0.5);
        foreach (var (sk_, sh_) in En(new double[] { 9, 14, 11, 18, 8, 13, 16, 10, 12, 19, 9, 15, 12, 17, 8, 14, 11, 16, 9, 13, 15, 10, 18, 12, 9, 16, 11, 14 })) {
        sk = sk_; sh = sh_;
        P((sk - 1)*5, 58 - sh - 4, 5, sh + 4, cFar);
        }
    if (B(night > 0.1)) {
        for (int A1 = 1; A1 <= (int)(40); A1++) {
        k = A1;
        if (B(Md(k*7, 5) < 3)) {
        P(Md(k*31 + 3, 140), 44 + Md(k*11, 12), 1, 1, Alpha(warm, Rn(150*night)));
        }
        }
    }
    cH1 = Mix(Mix(0xFF2A3466, 0xFF7EA6C8, dayc), 0xFF5A6A80, rain*0.4);
    cH2 = Mix(Mix(0xFF1A2448, 0xFF4E7EA6, dayc), 0xFF445466, rain*0.4);
        for (int A1 = 1; A1 <= (int)(36); A1++) {
        k = A1;
        hh = 9 + Rn(6*Math.Sin(k*0.41 + 2) + 3*Math.Sin(k*1.1));
        P((k - 1)*4, 58 - hh, 4, hh, cH1);
        }
        for (int A1 = 1; A1 <= (int)(36); A1++) {
        k = A1;
        hh = 5 + Rn(4*Math.Sin(k*0.55) + 3*Math.Sin(k*1.3 + 1));
        P((k - 1)*4, 58 - hh, 4, hh, cH2);
        }
    cPine = Mix(0xFF0F1A38, 0xFF2E5A4A, dayc);
        for (int A1 = 1; A1 <= (int)(14); A1++) {
        k = A1;
        px = 3 + (k - 1)*10 + Md(k*3, 4);
        hh = 5 + Rn(4*Math.Sin(k*0.55) + 3*Math.Sin(k*1.3 + 1));
        P(px, 58 - hh - 4, 1, 4, cPine);
        P(px - 1, 58 - hh - 3, 3, 2, cPine);
        P(px - 2, 58 - hh - 1, 5, 1, cPine);
        }
    P(119, 44, 3, 14, cH2);
    P(118, 43, 5, 1, cH2);
    wa = Math.Floor(Md(t*0.03, 360)/15)*15;
    pn = PenP(FA(Mix(0xFF3B4A78, 0xFF8AA6C4, dayc), f), 2);
        for (int A1 = 1; A1 <= (int)(4); A1++) {
        an = (wa + (A1 - 1)*90)*0.0174533;
        Line(ox + 120.5*cs, oy + 44.5*cs, ox + 120.5*cs + 9*cs*Math.Cos(an), oy + 44.5*cs + 9*cs*Math.Sin(an), pn);
        }
    cW = Mix(Mix(0xFF16204A, 0xFF64A8D6, dayc), 0xFF3F5468, rain*0.4);
    cW2 = Mix(Mix(0xFF25326A, 0xFF9ACBEA, dayc), 0xFF5A7088, rain*0.4);
    P(0, 58, 142, 8, cW);
        for (int A1 = 1; A1 <= (int)(12); A1++) {
        k = A1;
        P(Md(k*17 + Rn(t*0.004) + k*3, 150) - 8, 59 + Md(k, 6), 5 - Md(k, 3), 1, cW2);
        }
        for (int A1 = 1; A1 <= (int)(4); A1++) {
        k = A1;
        P(ax_ - 1 + Rn(Math.Sin(t*0.005 + k*1.7)*1.5), 59 + (k - 1)*2, 3 - Md(k, 2), 1, Alpha(B((ph < 0.5)) ? 0xFFFFD86B : wh, Rn(70 + 40*Math.Sin(t*0.004 + k))));
        }
    if (B(night > 0.1)) {
        for (int A1 = 1; A1 <= (int)(20); A1++) {
        k = A1;
        P(Md(k*31 + 3, 140), 60 + Md(k*7, 5), 1, 1, Alpha(warm, Rn(70*night*(0.5 + 0.5*Math.Sin(t*0.006 + k)))));
        }
    }
    bxb = 150 - Md(t*0.0028, 190);
    P(bxb, 62, 8, 2, 0xFF5A3E2E);
    P(bxb + 2, 61, 4, 1, 0xFFE9D5A8);
    P(bxb + 4, 59, 1, 2, 0xFF3A2E24);
    P(bxb + 4, 58, 1, 1, night > 0.2 ? warm : 0xFFE0574A);
    P(bxb - 2 - Rn(Md(t*0.01, 3)), 63, 2, 1, cW2);
    P(0, 55, 142, 2, 0xFF4A4F6E);
    P(0, 55, 142, 1, 0xFF6A6F8E);
        for (int A1 = 1; A1 <= (int)(12); A1++) {
        P((A1 - 1)*12 + 4, 57, 2, 9, 0xFF3A3F5C);
        P((A1 - 1)*12 + 3, 57, 4, 1, 0xFF4A4F6E);
        }
    tph = Md(t, 24000);
    if (B(tph < 8000)) {
    tx_ = -32 + Rn(tph*0.0245);
    P(tx_, 51, 9, 4, 0xFF9A3B3B);
    P(tx_ + 1, 50, 4, 1, 0xFF7A2B2B);
    P(tx_ + 11, 52, 9, 3, 0xFF6E7391);
    P(tx_ + 21, 52, 9, 3, 0xFF6E7391);
    P(tx_ + 7, 50, 2, 1, warm);
    P(tx_ + 8, 52, 1, 1, warm);
    P(tx_ + 13, 53, 1, 1, warm);
    P(tx_ + 16, 53, 1, 1, warm);
    P(tx_ + 23, 53, 1, 1, warm);
    P(tx_ + 26, 53, 1, 1, warm);
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        P(tx_ + (A1 - 1)*5 + 1, 55, 2, 1, 0xFF2A2E44);
        }
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        sp = Md(t*0.0013 + k*0.33, 1.0);
        P(tx_ + 1 - Rn(sp*7), 49 - Rn(sp*4), 1, 1, Alpha(wh, Rn(140*(1 - sp))));
        }
    }
    cG = Mix(Mix(0xFF243A2C, 0xFF5B9A5C, dayc), 0xFF3A5A44, rain*0.3);
    P(0, 66, 142, 16, cG);
        for (int A1 = 1; A1 <= (int)(40); A1++) {
        k = A1;
        P(Md(k*29, 142), 67 + Md(k*17, 14), 2, 1, Mix(cG, 0xFF000000, 0.15));
        }
    if (B(night > 0.3 && rain < 0.3)) {
        for (int A1 = 1; A1 <= (int)(8); A1++) {
        k = A1;
        P(10 + Md(k*31, 122) + Rn(2*Math.Sin(t*0.0009 + k)), 68 + Md(k*7, 12) + Rn(Math.Sin(t*0.0013 + k*2)), 1, 1, Alpha(0xFFD6FF7A, Rn(200*night*Math.Max(0.0, Math.Sin(t*0.004 + k*1.7)))));
        }
    }
    cPv = Mix(Mix(0xFF4A4D66, 0xFF9C9DB0, dayc), 0xFF5A6070, rain*0.4);
    cRd = Mix(Mix(0xFF23263A, 0xFF474B5E, dayc), 0xFF2A2E3E, rain*0.5);
    P(0, 82, 142, 4, cPv);
    P(0, 85, 142, 1, Mix(cPv, 0xFF000000, 0.25));
    P(0, 86, 142, 9, cRd);
        for (int A1 = 1; A1 <= (int)(30); A1++) {
        k = A1;
        P(Md(k*23, 142), 87 + Md(k*5, 7), 1, 1, Mix(cRd, wh, 0.06));
        }
        for (int A1 = 1; A1 <= (int)(18); A1++) {
        P((A1 - 1)*8 + 2, 90, 4, 1, 0xFFB8B08A);
        }
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        P(60 + (A1 - 1)*4, 86, 2, 9, Mix(0xFFD8D4C4, cRd, 0.3));
        }
    if (B(rain > 0.05 || night > 0.4)) {
        foreach (var (lk_, lx0_) in En(new double[] { 20, 56, 90, 122 })) {
        lk = lk_; lx0 = lx0_;
        P(lx0 - 1, 87, 3, 8, Alpha(warm, Rn(26*Math.Max(rain, night*0.7))));
        }
    }
    P(0, 95, 142, 6, Mix(Mix(0xFF1F3325, 0xFF4F8A4F, dayc), 0xFF2E4A38, rain*0.3));
    if (B(Tw.Level(1) >= 2)) {
    wcx = 124;
    wcy = 60;
    wr = 9;
    P(wcx - 4, wcy + 6, 1, 11, 0xFF2A2E44);
    P(wcx + 4, wcy + 6, 1, 11, 0xFF2A2E44);
    P(wcx - 2, wcy + 4, 5, 1, 0xFF2A2E44);
    P(wcx - 5, wcy + 16, 11, 1, 0xFF3A3F5C);
        for (int A1 = 1; A1 <= (int)(24); A1++) {
        an = (A1 - 1)*0.2618;
        P(wcx + Rn(wr*Math.Cos(an)), wcy + Rn(wr*Math.Sin(an)), 1, 1, 0xFF8A8FA8);
        }
    wa = Math.Floor(Md(DecT(now)*0.012, 360)/15)*15*0.0174533;
        for (int A1 = 1; A1 <= (int)(8); A1++) {
        an = wa + (A1 - 1)*0.7854;
            for (int A2 = 1; A2 <= (int)(wr - 1); A2++) {
            P(wcx + Rn(A2*Math.Cos(an)), wcy + Rn(A2*Math.Sin(an)), 1, 1, 0xFF5A5E78);
            }
        gx = wcx + Rn(wr*Math.Cos(an));
        gy = wcy + Rn(wr*Math.Sin(an));
        P(gx - 1, gy, 3, 2, B(Md(A1, 2)) ? 0xFFE8574A : 0xFF3F7FC9);
        P(gx - 1, gy, 3, 1, B(Md(A1, 2)) ? 0xFFFF8A7A : 0xFF7FB7E0);
        if (B(night > 0.2)) {
        P(gx, gy + 1, 1, 1, Alpha(warm, Rn(230*night)));
        }
        }
    P(wcx - 1, wcy - 1, 3, 3, 0xFF9A9EB8);
    P(wcx, wcy, 1, 1, 0xFF3A3F5C);
    }
    bld = new[] { new double[] { 2, 14, 16, 0 }, new double[] { 17, 11, 22, 4 }, new double[] { 29, 13, 14, 1 }, new double[] { 43, 12, 26, 3 }, new double[] { 56, 7, 12, 0 } , new double[] { 78, 13, 20, 2 }, new double[] { 92, 9, 15, 0 }, new double[] { 102, 15, 24, 3 }, new double[] { 118, 10, 18, 0 }, new double[] { 129, 12, 13, 0 } };
    pal = new double[] { 0xFF3E4463, 0xFF4A3F5C, 0xFF5A4A3E, 0xFF3A4C66, 0xFF4E4658, 0xFF44405E, 0xFF3F5158, 0xFF4B4466, 0xFF3D4560, 0xFF524A52 };
        foreach (var (bi_, bd_) in En(bld)) {
        bi = bi_; bd = bd_;
        bx0 = bd[(int)(1) - 1];
        bw0 = bd[(int)(2) - 1];
        bh0 = bd[(int)(3) - 1];
        bk = bd[(int)(4) - 1];
        top = 82 - bh0;
        cWl = Mix(pal[(int)(bi) - 1], 0xFF0E1428, night*0.55);
        P(bx0, top, bw0, bh0, cWl);
            for (int A2 = 1; A2 <= (int)(Fd(bh0, 2)); A2++) {
            r = A2;
            P(bx0 + Md(r, 2), top + 1 + (r - 1)*2, bw0 - 2, 1, Mix(cWl, 0xFF000000, 0.08));
            }
        P(bx0 + bw0 - 2, top, 2, bh0, Mix(cWl, 0xFF000000, 0.35));
        P(bx0, top, bw0, 1, Mix(cWl, wh, 0.35));
        P(bx0, top + 1, bw0, 1, Mix(cWl, wh, 0.12));
        P(bx0 + Fd(bw0, 2) - 1, 79, 2, 3, 0xFF2A2236);
        P(bx0 + Fd(bw0, 2), 80, 1, 1, warm);
        P(bx0 + Fd(bw0, 2) - 2, 78, 4, 1, Mix(cWl, wh, 0.25));
        wy = top + 3;
        while (B(wy <= 76)) {
        wx = bx0 + 2;
        while (B(wx <= bx0 + bw0 - 5)) {
        hsh = Md(bi*7 + wx*13 + wy*5, 9);
        lit = B((hsh < 6)) ? 1.0 : (hsh < 8) ? (0.5 + 0.5*Math.Sin(t*0.0007 + hsh + wx)) : 0.0;
        gl = night*lit;
        cGl = B((Md(hsh, 4) == 1 && gl > 0.5 && Md(Math.Floor(t/90) + wx, 7) < 2)) ? 0xFF8AB8FF : warm;
        P(wx, wy, 2, 2, Mix(Mix(0xFF9FB6D6, 0xFF1C2238, night*0.8), cGl, gl));
        if (B(Md(hsh, 3) == 0)) {
        P(wx, wy, 1, 2, Mix(cWl, 0xFFE9D5A8, 0.5));
        }
        P(wx - 1, wy - 1, 4, 1, Mix(cWl, wh, 0.2));
        P(wx - 1, wy + 2, 4, 1, Mix(cWl, wh, 0.25));
        if (B(gl > 0.3)) {
        P(wx, wy + 3, 2, 1, Alpha(warm, Rn(40*gl)));
        }
        wx += 4;
        }
        wy += 4;
        }
        if (B(bk == 1)) {
            for (int A2 = 1; A2 <= (int)(bw0 - 2); A2++) {
            k = A2;
            P(bx0 + k, top + 6, 1, 2, B(Md(k, 2)) ? 0xFFE0D8C8 : 0xFFC44A4A);
            if (B(Md(k, 2))) {
            P(bx0 + k, top + 8, 1, 1, 0xFFE0D8C8);
            }
            }
        P(bx0 + 2, top + 2, bw0 - 4, 3, 0xFF2A2236);
        P(bx0 + 5, top + 3, 3, 1, warm);
        P(bx0 + 9, top + 3, 1, 1, warm);
        on1 = (Md(t, 1000) < 500);
        P(bx0 + 3, top + 3, 1, 1, B(on1) ? warm : 0xFF5A4A50);
        P(bx0 + bw0 - 4, top + 3, 1, 1, B(on1) ? 0xFF5A4A50 : warm);
        P(bx0 + 1, 81, 3, 1, 0xFF6E4A34);
        P(bx0 + 2, 79, 1, 2, 0xFF6E4A34);
        P(bx0 + bw0 - 4, 81, 3, 1, 0xFF6E4A34);
        P(bx0 + bw0 - 3, 79, 1, 2, 0xFF6E4A34);
        P(bx0, 78, 5, 1, 0xFFC44A4A);
        P(bx0 + bw0 - 5, 78, 5, 1, 0xFFC44A4A);
        } else if (B(bk == 2)) {
        P(bx0 + 1, top + 2, bw0 - 2, 4, 0xFF1C1E2A);
        P(bx0 + 3, top + 3, 2, 2, 0xFFFF6A6A);
        P(bx0 + 6, top + 3, 2, 2, warm);
        P(bx0 + 9, top + 3, 2, 2, 0xFF8AB8FF);
            for (int A2 = 1; A2 <= (int)((bw0 - 2)*2 + 8); A2++) {
            k = A2;
            onk = (Md(Math.Floor(t/120) + k, 5) == 0);
            if (B(k <= bw0 - 2)) {
            P(bx0 + k, top + 1, 1, 1, B(onk) ? 0xFFFFE9A8 : 0xFF6A5A4A);
            }
            else if (B(k <= (bw0 - 2)*2)) {
            P(bx0 + k - (bw0 - 2), top + 6, 1, 1, B(onk) ? 0xFFFFE9A8 : 0xFF6A5A4A);
            }
            else if (B(k <= (bw0 - 2)*2 + 4)) {
            P(bx0, top + 1 + (k - (bw0 - 2)*2), 1, 1, B(onk) ? 0xFFFFE9A8 : 0xFF6A5A4A);
            }
            else {
            P(bx0 + bw0 - 1, top + 1 + (k - (bw0 - 2)*2 - 4), 1, 1, B(onk) ? 0xFFFFE9A8 : 0xFF6A5A4A);
            }
            }
        P(bx0 + 2, 75, bw0 - 4, 4, Mix(0xFFFFE9C0, 0xFF3A2E24, dayc*0.7));
        } else if (B(bk == 3)) {
        P(bx0 + Fd(bw0, 2), top - 3, 1, 3, 0xFF8A8FA8);
        P(bx0 + Fd(bw0, 2), top - 4, 1, 1, B((Md(t, 1600) < 200)) ? 0xFFFF6A6A : 0xFF6A3A3A);
        P(bx0 + 2, top - 2, 3, 2, 0xFF6A6F8E);
        P(bx0 + 3, top - 1, 1, 1, 0xFF3A3F5C);
        } else if (B(bk == 4)) {
        P(bx0 + 1, top - 7, bw0 - 2, 6, 0xFF1C1E2A);
        P(bx0 + 3, top - 1, 1, 1, 0xFF6A6F8E);
        P(bx0 + bw0 - 4, top - 1, 1, 1, 0xFF6A6F8E);
        bl = (Md(t, 2200) < 1600);
        P(bx0 + 2, top - 6, bw0 - 4, 4, B(bl) ? 0xFFE8574A : 0xFF6A2A2A);
        P(bx0 + 3, top - 5, 2, 2, 0xFFFFE9A8);
        P(bx0 + 6, top - 5, 3, 2, 0xFFFFE9A8);
        }
        if (B(bi == 4 || bi == 8)) {
            for (int A2 = 1; A2 <= (int)(Fd((bh0 - 6), 4)); A2++) {
            r = A2;
            P(bx0 - 1, top + 4 + (r - 1)*4, 3, 1, 0xFF2A2E44);
            P(bx0 - 1, top + 5 + (r - 1)*4, 1, 3, 0xFF2A2E44);
            }
        }
        if (B(bi == 2 || bi == 4 || bi == 8)) {
        cxk = bx0 + bw0 - 4;
        P(cxk, top - 2, 2, 2, 0xFF6A5A5A);
        P(cxk, top - 2, 2, 1, 0xFF8A7A7A);
            for (int A2 = 1; A2 <= (int)(3); A2++) {
            k = A2;
            sp = Md(t*0.0009 + k*0.33 + bi*0.1, 1.0);
            P(cxk + Rn(Math.Sin(sp*6.28 + k)*0.8) + 1 - Rn(sp*3), top - 3 - Rn(sp*6), 1, 1, Alpha(wh, Rn(120*(1 - sp))));
            }
        }
        }
    cSt = Mix(0xFF6B6F8A, 0xFF1E2440, night*0.5);
    P(64, 26, 12, 56, cSt);
    P(74, 26, 2, 56, Mix(cSt, 0xFF000000, 0.3));
        for (int A1 = 1; A1 <= (int)(28); A1++) {
        P(64 + Md(A1, 2), 27 + (A1 - 1)*2, 10, 1, Mix(cSt, 0xFF000000, 0.08));
        }
    P(64, 26, 12, 1, 0xFF9A9EB8);
    P(67, 19, 6, 7, Mix(0xFF9A9EB8, 0xFF2A3050, night*0.5));
    P(69, 15, 2, 4, 0xFFB9BDD4);
    P(68, 21, 1, 3, 0xFF2A2E44);
    P(71, 21, 1, 3, 0xFF2A2E44);
    P(69, 22, 2, 1, B((Md(DateTime.Now.Second, 15) == 0)) ? warm : 0xFFB89A4A);
    P(65, 30, 10, 10, 0xFF3A3F5C);
    P(66, 31, 8, 8, 0xFFF4EFDC);
    if (B(night > 0.3)) {
    b = TB(Alpha(warm, Rn(30*night)), f);
    FillEll(ox + 63*cs, oy + 28*cs, 14*cs, 14*cs, b);
    }
    P(69, 31, 2, 1, 0xFF5A5470);
    P(69, 38, 2, 1, 0xFF5A5470);
    P(66, 34, 1, 2, 0xFF5A5470);
    P(73, 34, 1, 2, 0xFF5A5470);
    ccx = ox + 70*cs;
    ccy = oy + 35*cs;
    hrA = (Md(DateTime.Now.Hour, 12) + DateTime.Now.Minute/60.0)*30*0.0174533 - 1.5708;
    mnA = DateTime.Now.Minute*6*0.0174533 - 1.5708;
    scA = DateTime.Now.Second*6*0.0174533 - 1.5708;
    pn = PenP(FA(0xFF2A2E44, f), 2.2);
    Line(ccx, ccy, ccx + 2.6*cs*Math.Cos(hrA), ccy + 2.6*cs*Math.Sin(hrA), pn);
    Line(ccx, ccy, ccx + 3.6*cs*Math.Cos(mnA), ccy + 3.6*cs*Math.Sin(mnA), pn);
    pn = PenP(FA(0xFFD9463E, f), 1);
    Line(ccx, ccy, ccx + 3.8*cs*Math.Cos(scA), ccy + 3.8*cs*Math.Sin(scA), pn);
    b = TB(0xFF2A2E44, f);
    FillEll(ccx - 2, ccy - 2, 4, 4, b);
    P(64, 42, 12, 1, 0xFF9A9EB8);
    P(64, 62, 12, 1, 0xFF9A9EB8);
    cTw = Mix(0xFF9FB6D6, warm, night);
    P(68, 46, 1, 3, cTw);
    P(71, 46, 1, 3, cTw);
    P(68, 54, 1, 3, cTw);
    P(71, 54, 1, 3, cTw);
    P(68, 66, 1, 3, cTw);
    P(71, 66, 1, 3, cTw);
    P(66, 74, 8, 8, Mix(cSt, 0xFF000000, 0.3));
    P(68, 76, 4, 6, 0xFF2A2236);
    P(69, 79, 1, 1, warm);
    if (B(Md(t, 700) < 350)) {
    P(71, 15, 4, 2, 0xFFE0574A);
    }
    else {
    P(71, 16, 4, 2, 0xFFE0574A);
    P(74, 15, 1, 1, 0xFFE0574A);
    }
    if (B(DateTime.Now.Minute == 0 && DateTime.Now.Second < 14)) {
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        fp = Md(DateTime.Now.Second*1000 + DateTime.Now.Millisecond - k*1800, 4200)/4200.0;
        if (B(fp < 0)) {
        continue;
        }
        fcx = 40 + k*28;
        fcy = 18 + Md(k, 2)*8;
        fcl = B((k == 1)) ? 0xFFFF6A6A : (k == 2) ? 0xFFFFE08A : 0xFF8AB8FF;
            for (int A2 = 1; A2 <= (int)(12); A2++) {
            an = (A2 - 1)*0.5236;
            rr = Rn(fp*16);
            P(fcx + Rn(rr*Math.Cos(an)), fcy + Rn(rr*Math.Sin(an)) + Rn(fp*fp*6), 1, 1, Alpha(fcl, Rn(240*(1 - fp))));
            }
        }
    }
    cLd = Mix(0xFF3D7A4A, 0xFF1E3A2A, night*0.5);
    cLl = Mix(0xFF6FBF63, 0xFF2E5A3A, night*0.5);
        foreach (var (tk_, tx0_) in En(new double[] { 1, 60, 100, 134 })) {
        tk = tk_; tx0 = tx0_;
        TownTree(P_, tx0, 82, Rn(Math.Sin(t*0.0011 + tk*1.9)), 0xFF5A3E2E, cLd, cLl);
        }
    TownTree(P_, 8, 78, Rn(Math.Sin(t*0.0009 + 3)), 0xFF5A3E2E, cLd, cLl, 1);
    P(63, 83, 14, 3, 0xFF8A8DA5);
    P(64, 84, 12, 1, 0xFF4F8FD0);
    P(63, 83, 14, 1, 0xFFA5A8BE);
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        fp = Math.Abs(Math.Sin(t*0.0021 + k*1.05));
        P(66 + (k - 1)*3, 82 - Rn(fp*4), 1, 1 + Rn(fp*2), 0xFFB8E0F8);
        }
        foreach (var (lk_, lx0_) in En(new double[] { 20, 56, 90, 122 })) {
        lk = lk_; lx0 = lx0_;
        P(lx0, 76, 1, 6, 0xFF2A2E44);
        P(lx0 - 1, 75, 3, 1, 0xFF2A2E44);
        P(lx0 - 1, 76, 1, 1, 0xFF2A2E44);
        P(lx0 + 1, 76, 1, 1, 0xFF2A2E44);
        if (B(night > 0.05)) {
        P(lx0, 76, 1, 1, Alpha(warm, Rn(235*night)));
        b = TB(Alpha(warm, Rn(36*night)), f);
        FillEll(ox + (lx0 - 5)*cs, oy + 74*cs, 11*cs, 12*cs, b);
        b = TB(Alpha(warm, Rn(22*night)), f);
        FillEll(ox + (lx0 - 7)*cs, oy + 80*cs, 15*cs, 6*cs, b);
        }
        }
    P(44, 76, 9, 1, 0xFF3A3F5C);
    P(44, 77, 1, 5, 0xFF3A3F5C);
    P(52, 77, 1, 5, 0xFF3A3F5C);
    P(45, 77, 7, 4, Mix(0xFF9FB6D6, warm, night*0.6));
    P(46, 78, 5, 2, Mix(0xFF2A2E44, warm, night*0.5));
    P(45, 81, 7, 1, 0xFF6E4A34);
    P(108, 76, 3, 6, 0xFFC44A4A);
    P(109, 77, 1, 3, Mix(0xFF9FB6D6, warm, night*0.5));
    P(108, 75, 3, 1, 0xFF8A3A3A);
    P(84, 80, 2, 2, 0xFFE8574A);
    P(83, 80, 4, 1, 0xFFE8574A);
    P(84, 79, 2, 1, 0xFFB83A2E);
    P(114, 78, 6, 3, 0xFFE9D5A8);
    P(115, 77, 4, 1, 0xFFC44A4A);
    P(114, 81, 1, 1, 0xFF2A2E44);
    P(119, 81, 1, 1, 0xFF2A2E44);
    P(120, 76, 1, 5, 0xFF6E4A34);
    P(116, 76, 2, 1, Alpha(wh, Rn(120 + 60*Math.Sin(t*0.005))));
    P(126, 77, 6, 2, 0xFFE0D8C8);
    P(128, 79, 1, 3, 0xFF5A5470);
    P(127, 77, 1, 2, 0xFFE0574A);
    P(106, 83, 8, 1, 0xFF6E4A34);
    P(106, 84, 1, 2, 0xFF6E4A34);
    P(113, 84, 1, 2, 0xFF6E4A34);
    P(96, 83, 2, 3, 0xFF3A3F5C);
    P(96, 82, 2, 1, 0xFF6A6F8E);
    cols = new[] { new double[] { 0xFF3A4C8A, 0xFF23263A }, new double[] { 0xFF8A3A4C, 0xFF3A4C8A }, new double[] { 0xFF4FA85C, 0xFF3A2E24 }, new double[] { 0xFFE0872E, 0xFF23263A }, new double[] { 0xFF6A4A8A, 0xFF2A2E44 } };
        for (int A1 = 1; A1 <= (int)(5); A1++) {
        k = A1;
        dir = B((k == 4 || k == 5)) ? -1 : 1;
        px0 = B((dir > 0)) ? Md(t*(0.0032 + 0.0008*k) + k*41, 158) - 8 : 150 - Md(t*(0.003 + 0.0006*k) + k*53, 158);
        stp = Md(Math.Floor(t/170) + k, 2);
        TownWalker(P_, px0, 86, cols[(int)(k) - 1][(int)(1) - 1], cols[(int)(k) - 1][(int)(2) - 1], 0xFFF1C9A2, B((k == 3)) ? 0xFFE9D5A8 : 0xFF3A2E24, stp, dir, B((k == 5)) ? 0xFF6A4A8A : 0);
        if (B(rain > 0.3)) {
        P(px0 - 2, 76, 6, 1, cols[(int)(k) - 1][(int)(1) - 1]);
        P(px0 - 1, 75, 4, 1, cols[(int)(k) - 1][(int)(1) - 1]);
        P(px0 + 1, 77, 1, 3, 0xFF2A2E44);
        }
        if (B(k == 2)) {
        P(px0 - 4, 84, 3, 1, 0xFF8A6A4A);
        P(px0 - 5, 83, 1, 1, 0xFF8A6A4A);
        P(px0 - 4 + stp, 85, 1, 1, 0xFF8A6A4A);
        P(px0 - 2 - stp, 85, 1, 1, 0xFF8A6A4A);
        P(px0 - 2, 83 - stp, 1, 1, 0xFF8A6A4A);
        }
        }
    cx1 = Md(t*0.014, 174) - 14;
    P(cx1, 88, 8, 2, 0xFFE8C94A);
    P(cx1 + 2, 87, 4, 1, 0xFF9FD3F0);
    P(cx1 + 3, 86, 2, 1, 0xFF2A2E44);
    P(cx1 + 1, 90, 1, 1, 0xFF1C1E2A);
    P(cx1 + 6, 90, 1, 1, 0xFF1C1E2A);
    cx3 = Md(t*0.011 + 90, 174) - 14;
    P(cx3, 88, 8, 2, 0xFFC94A3E);
    P(cx3 + 2, 87, 4, 1, 0xFF9FD3F0);
    P(cx3 + 1, 90, 1, 1, 0xFF1C1E2A);
    P(cx3 + 6, 90, 1, 1, 0xFF1C1E2A);
    cx2 = 156 - Md(t*0.0105 + 60, 174);
    P(cx2, 92, 8, 2, 0xFF3F7FC9);
    P(cx2 + 2, 91, 4, 1, 0xFF9FD3F0);
    P(cx2 + 1, 94, 1, 1, 0xFF1C1E2A);
    P(cx2 + 6, 94, 1, 1, 0xFF1C1E2A);
    bph = Md(t, 30000);
    if (B(bph < 9000)) {
    bxs = 160 - Rn(bph*0.021);
    P(bxs, 91, 18, 3, 0xFF4FA85C);
    P(bxs + 1, 90, 16, 1, 0xFF9FD3F0);
    P(bxs + 2, 94, 1, 1, 0xFF1C1E2A);
    P(bxs + 15, 94, 1, 1, 0xFF1C1E2A);
    P(bxs + 8, 94, 1, 1, 0xFF1C1E2A);
        for (int A1 = 1; A1 <= (int)(7); A1++) {
        P(bxs + 2 + (A1 - 1)*2, 92, 1, 1, Mix(0xFF9FD3F0, warm, night));
        }
    }
    if (B(night > 0.05)) {
        foreach (var (ck_, cxx_) in En(new double[] { cx1, cx3 })) {
        ck = ck_; cxx = cxx_;
        P(cxx + 8, 89, 1, 1, Alpha(0xFFFFF3C4, Rn(235*night)));
        P(cxx - 1, 89, 1, 1, Alpha(0xFFFF5A5A, Rn(200*night)));
        P(cxx + 9, 88, 5, 3, Alpha(0xFFFFF3C4, Rn(34*night)));
        P(cxx + 14, 87, 3, 5, Alpha(0xFFFFF3C4, Rn(16*night)));
        }
    P(cx2 - 1, 93, 1, 1, Alpha(0xFFFFF3C4, Rn(235*night)));
    P(cx2 + 8, 93, 1, 1, Alpha(0xFFFF5A5A, Rn(200*night)));
    P(cx2 - 6, 92, 5, 3, Alpha(0xFFFFF3C4, Rn(34*night)));
    }
    lv = Tw.Level(1);
    if (B(lv >= 1)) {
        foreach (var (bk_, bx1_) in En(new[] { new double[] { 20, 56 }, new double[] { 56, 90 }, new double[] { 90, 122 } })) {
        bk = bk_; bx1 = bx1_;
            for (int A2 = 1; A2 <= (int)(8); A2++) {
            q = A2;
            fx = bx1[(int)(1) - 1] + Rn((bx1[(int)(2) - 1] - bx1[(int)(1) - 1])*q/9.0);
            fy = 76 + Rn(2*Math.Sin(q/9.0*3.14159)) + Md(Math.Floor(t/300) + q, 2);
            P(fx, fy, 1, 2, Md(q, 3) == 0 ? 0xFFE8574A : Md(q, 3) == 1 ? 0xFFFFE08A : 0xFF9FD3F0);
            }
        P(bx1[(int)(1) - 1], 76, bx1[(int)(2) - 1] - bx1[(int)(1) - 1], 1, Alpha(0xFF2A2E44, 120));
        }
    }
    if (B(lv >= 3)) {
    P(8, 40, 3, 12, 0xFFE0D8C8);
    P(8, 44, 3, 2, 0xFFC44A4A);
    P(8, 48, 3, 2, 0xFFC44A4A);
    P(7, 38, 5, 2, 0xFF3A3F5C);
    P(8, 36, 3, 2, warm);
    if (B(night > 0.2)) {
    ba = Math.Floor(Md(DecT(now)*0.035, 360)/10)*10*0.0174533;
        for (int A1 = 1; A1 <= (int)(9); A1++) {
        q = A1;
        bx2 = 9 + Rn(q*4*Math.Cos(ba));
        by2 = 37 + Rn(q*1.4*Math.Sin(ba));
        P(bx2, by2 - (q > 4 ? 1 : 0), 4, 1 + (q > 4 ? 2 : 1), Alpha(warm, Rn((120 - q*11)*night)));
        }
    }
    }
    if (B(lv >= 4)) {
    zx = Md(t*0.0018 + 60, 220) - 40;
    zy = 22 + Rn(2*Math.Sin(t*0.0007));
    P(zx, zy, 24, 6, 0xFF8A8FA8);
    P(zx + 2, zy - 1, 20, 1, 0xFF9A9EB8);
    P(zx + 2, zy + 6, 20, 1, 0xFF6E7391);
    P(zx - 3, zy + 1, 4, 4, 0xFF6E7391);
    P(zx + 9, zy + 6, 6, 2, 0xFF3A3F5C);
        for (int A1 = 1; A1 <= (int)(18); A1++) {
        P(zx + 3 + (A1 - 1), zy + 2, 1, 2, B((Md(Math.Floor(t/110) + A1, 6) < 2)) ? warm : 0xFF3A3F5C);
        }
    }
    if (B(lv >= 5)) {
    fph = Md(t, 30000);
    if (B(fph < 4200 && night > 0.2)) {
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        q = A1;
        fp = (fph - q*900)/2800.0;
        if (B(fp < 0 || fp > 1)) {
        continue;
        }
        fcx = 34 + q*30;
        fcy = 16 + Md(q, 2)*8;
        fcl = B((q == 1)) ? 0xFFFF6A6A : (q == 2) ? 0xFFFFE08A : 0xFF8AB8FF;
            for (int A2 = 1; A2 <= (int)(12); A2++) {
            an = (A2 - 1)*0.5236;
            rr = Rn(fp*15);
            P(fcx + Rn(rr*Math.Cos(an)), fcy + Rn(rr*Math.Sin(an)) + Rn(fp*fp*6), 1, 1, Alpha(fcl, Rn(240*(1 - fp))));
            }
        }
    }
    }
    P(0, 97, 142, 1, 0xFF6E4A34);
    P(0, 98, 142, 1, 0xFF5A3E2E);
        for (int A1 = 1; A1 <= (int)(24); A1++) {
        P((A1 - 1)*6, 96, 1, 3, 0xFF6E4A34);
        P((A1 - 1)*6, 96, 1, 1, 0xFF8A6A4A);
        }
    tail = (Md(t, 800) < 400);
    P(30, 94, 5, 2, 0xFF2A2436);
    P(34, 93, 2, 2, 0xFF2A2436);
    P(34, 92, 1, 1, 0xFF2A2436);
    P(36, 92, 1, 1, 0xFF2A2436);
    if (B(tail)) {
    P(29, 93, 1, 1, 0xFF2A2436);
    P(28, 92, 1, 1, 0xFF2A2436);
    }
    else {
    P(29, 94, 1, 1, 0xFF2A2436);
    P(28, 94, 1, 1, 0xFF2A2436);
    }
    P(35, 93, 1, 1, 0xFFFFE08A);
    if (B(rain > 0.02)) {
        for (int A1 = 1; A1 <= (int)(40); A1++) {
        k = A1;
        rp = Md(t*0.0012 + k*0.137, 1.0);
        P(Md(k*37 + Rn(rp*8), 142), Rn(rp*100), 1, 2, Alpha(0xFFB9DCF2, Rn(150*rain)));
        }
    }

    }
    public void Street()
    {
    double t = 0, wh = 0, k = 0, pxl = 0, pyl = 0, sk = 0, sh = 0, ri = 0, rx = 0, rw = 0, rtop = 0, cw = 0, ct = 0, rk = 0, q = 0, fy = 0, fi = 0, wx = 0, wi = 0, v = 0, wv = 0, tk = 0, tx0 = 0, cL = 0, cL2 = 0, lp = 0, np = 0, lk = 0, lx0 = 0, px0 = 0, fph = 0, fx0 = 0, fy0 = 0, dir = 0, stp = 0, lv = 0, kx = 0, ky = 0, sp = 0, trx = 0, cp = 0, pph = 0, pbx = 0, cyx = 0, vph = 0, vx0 = 0, cph = 0, cx1 = 0, bph = 0, bxs = 0;
    bool pk = false;
    double[] r = null!;
    double[][] row = null!, cols = null!;
    IBrush? b = null;
    t = DecT(now);
    SkyBands(ox, oy, cs, 84, 0xFF5EAFE8, 0xFFD8EEF9, f, 12);
    wh = 0xFFFFFFFF;
    b = TB(Alpha(0xFFFFF0B8, 46), f);
    FillEll(ox + 4*cs, oy - 6*cs, 26*cs, 26*cs, b);
    b = TB(Alpha(0xFFFFF0B8, 60), f);
    FillEll(ox + 9*cs, oy - 1*cs, 16*cs, 16*cs, b);
    P(14, 3, 6, 4, 0xFFFFE9A8);
    P(15, 2, 4, 6, 0xFFFFE9A8);
    P(15, 3, 3, 2, wh);
        for (int A1 = 1; A1 <= (int)(7); A1++) {
        k = A1;
        TownCloud(P_, Md(t*(0.0011 + 0.0005*k) + k*29, 178) - 22, 3 + Md(k*7, 24), 8 + Md(k, 4)*4, wh, 0xFFCFE2F0);
        }
    pxl = Md(t*0.011, 210) - 40;
    pyl = 9 + Rn(1.5*Math.Sin(t*0.0014));
    P(pxl, pyl, 7, 2, 0xFFE8574A);
    P(pxl + 6, pyl - 1, 1, 1, 0xFFE8574A);
    P(pxl + 2, pyl - 1, 3, 1, 0xFFF4F1E8);
    P(pxl - 1, pyl + 1, 2, 1, 0xFFF4F1E8);
    P(pxl - 1, pyl - 1, 1, 1, 0xFF3A3F5C);
    P(pxl + 3, pyl, 2, 1, 0xFF9FD3F0);
        for (int A1 = 1; A1 <= (int)(14); A1++) {
        k = A1;
        P(pxl - 3 - k, pyl + Rn(Math.Sin(t*0.006 - k*0.8)*1.2), 1, 2, B(Md(k, 2)) ? 0xFFFFE9A8 : 0xFFD9463E);
        }
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        TownBird(P_, 180 - Md(t*0.016 + k*30, 200), 22 + k*5 + Rn(2*Math.Sin(t*0.003 + k)), Md(Math.Floor(t/230) + k, 2), 0xFF3A3F5C);
        }
        foreach (var (sk_, sh_) in En(new double[] { 14, 22, 17, 26, 12, 20, 24, 15, 19, 27, 13, 21, 18, 23 })) {
        sk = sk_; sh = sh_;
        P((sk - 1)*10, 58 - sh, 9, sh + 26, 0xFFC6DCEA);
        }
        foreach (var (sk_, sh_) in En(new double[] { 10, 16, 12, 20, 9, 15, 18, 11, 14, 21, 10, 17, 13, 19 })) {
        sk = sk_; sh = sh_;
        P((sk - 1)*10 + 3, 66 - sh, 8, sh + 18, 0xFFB0CBDF);
            for (int A2 = 1; A2 <= (int)(3); A2++) {
            P((sk - 1)*10 + 4 + (A2 - 1)*2, 68 - sh + (Md(sk*3 + A2, 4)*4), 1, 2, 0xFF9CB8CE);
            }
        }
    row = new[] { new double[] { 0, 27, 30, 0xFF4A5470, 0xFF7A88AA, 0 }, new double[] { 27, 30, 34, 0xFFB4703A, 0xFFE0A66C, 1 }, new double[] { 57, 36, 24, 0xFFB84A3A, 0xFFDB7D68, 2 } , new double[] { 93, 29, 28, 0xFFD9A15A, 0xFFF2CC8E, 3 }, new double[] { 122, 20, 22, 0xFF3F4A63, 0xFF6A7CA8, 4 } };
        foreach (var (ri_, r_) in En(row)) {
        ri = ri_; r = r_;
        rx = r[(int)(1) - 1];
        rw = r[(int)(2) - 1];
        rtop = r[(int)(3) - 1];
        cw = r[(int)(4) - 1];
        ct = r[(int)(5) - 1];
        rk = r[(int)(6) - 1];
        P(rx, rtop, rw, 84 - rtop, cw);
            for (int A2 = 1; A2 <= (int)(Fd((84 - rtop), 2)); A2++) {
            q = A2;
            P(rx + 1 + Md(q, 2), rtop + 3 + (q - 1)*2, rw - 3, 1, Mix(cw, 0xFF000000, 0.10));
            }
            for (int A2 = 1; A2 <= (int)(60); A2++) {
            k = A2;
            P(rx + 1 + Md(k*7, rw - 2), rtop + 4 + Md(k*11, 84 - rtop - 6), 2, 1, Mix(cw, 0xFF000000, 0.16));
            }
        P(rx + rw - 1, rtop, 1, 84 - rtop, Mix(cw, 0xFF000000, 0.3));
            for (int A2 = 1; A2 <= (int)(Fd((84 - rtop), 4)); A2++) {
            P(rx + (B(Md(A2, 2)) ? 0 : 1), rtop + 2 + (A2 - 1)*4, 2, 2, Mix(cw, wh, 0.25));
            }
        P(rx, rtop, rw, 2, ct);
        P(rx, rtop + 2, rw, 1, Mix(cw, 0xFF000000, 0.2));
        P(rx + 1, rtop - 1, rw - 2, 1, Mix(ct, wh, 0.3));
            for (int A2 = 1; A2 <= (int)(Fd(rw, 3)); A2++) {
            P(rx + (A2 - 1)*3 + 1, rtop + 1, 1, 1, Mix(ct, 0xFF000000, 0.15));
            }
        fy = rtop + 5;
        fi = 0;
        while (B(fy <= 66)) {
        fi++;
        wx = rx + 3;
        wi = 0;
        while (B(wx <= rx + rw - 7)) {
        wi++;
        P(wx - 1, fy + 6, 6, 1, Mix(cw, wh, 0.42));
        P(wx, fy + 1, 4, 5, wh);
        P(wx + 1, fy, 2, 1, wh);
        P(wx + 1, fy + 2, 2, 3, 0xFFA9D3EE);
        P(wx + 1, fy + 1, 2, 1, 0xFF7FB7E0);
        P(wx + 2, fy + 2, 1, 1, 0xFFD6ECF8);
        v = Md(ri*5 + fi*3 + wi*7, 6);
        if (B(v == 0)) {
        P(wx + 1, fy + 2, 1, 3, 0xFFF4E4C8);
        P(wx + 2, fy + 2, 1, 1, 0xFFF4E4C8);
        }
        else if (B(v == 1)) {
        P(wx + 1, fy + 2, 2, 1, 0xFFE8E2D4);
        P(wx + 1, fy + 3, 2, 1, 0xFFD8D2C4);
        }
        else if (B(v == 2)) {
        P(wx + 1, fy + 4, 1, 1, 0xFF4FA85C);
        P(wx + 2, fy + 3, 1, 1, 0xFFE8574A);
        }
        if (B(v == 3 && fi == 2)) {
        P(wx + 1, fy + 3, 2, 2, 0xFF2A2436);
        P(wx + 1, fy + 2, 1, 1, 0xFF2A2436);
        P(wx + 2, fy + 2, 1, 1, 0xFF2A2436);
        }
        if (B(rk == 0 || rk == 4)) {
        P(wx - 1, fy + 6, 6, 1, Mix(cw, wh, 0.35));
            for (int A2 = 1; A2 <= (int)(4); A2++) {
            P(wx - 1 + (A2 - 1)*2, fy + 4, 1, 2, 0xFF23263A);
            }
        P(wx - 1, fy + 4, 6, 1, 0xFF23263A);
        P(wx + 4, fy + 5, 1, 1, 0xFFE8574A);
        P(wx + 4, fy + 4, 1, 1, 0xFF4FA85C);
        } else if (B(rk == 3 && Md(wi, 2) == 1)) {
        P(wx, fy + 6, 4, 1, 0xFF7A4A2A);
        P(wx, fy + 5, 1, 1, 0xFFE8574A);
        P(wx + 1, fy + 5, 1, 1, 0xFF4FA85C);
        P(wx + 2, fy + 5, 1, 1, 0xFFFFE08A);
        P(wx + 3, fy + 5, 1, 1, 0xFF4FA85C);
        } else if (B(rk == 2 && fi == 2 && wi == 2)) {
        P(wx + 1, fy + 1, 2, 4, 0xFF5A2A22);
        P(wx + 3, fy + 1 + Rn(Math.Abs(Math.Sin(t*0.003))*1), 1, 3, 0xFFF4E4C8);
        }
        wx += 8;
        }
        fy += 10;
        }
        if (B(rk == 1)) {
        P(rx + 3, 72, 22, 12, 0xFF3A2A22);
        P(rx + 5, 74, 8, 6, 0xFFFFE9C0);
        P(rx + 6, 75, 2, 2, 0xFFE8574A);
        P(rx + 9, 76, 2, 2, 0xFF4FA85C);
        P(rx + 6, 78, 5, 1, 0xFFD9B23E);
        P(rx + 17, 74, 5, 10, 0xFF7A4A2A);
        P(rx + 21, 79, 1, 1, 0xFFFFD98A);
        P(rx + 18, 75, 3, 3, 0xFF9FD3F0);
            for (int A2 = 1; A2 <= (int)(22); A2++) {
            k = A2;
            P(rx + 2 + k, 70, 1, 2, B(Md(k, 2)) ? 0xFFF4F1E8 : 0xFFD9463E);
            P(rx + 2 + k, 72, 1, 1 + Md(k + Math.Floor(t/400), 2), B(Md(k, 2)) ? 0xFFF4F1E8 : 0xFFD9463E);
            }
        P(rx + 3, 69, 22, 1, 0xFFB84A3A);
        P(rx + 8, 66, 12, 3, 0xFF23263A);
        P(rx + 10, 67, 2, 1, 0xFFFFE08A);
        P(rx + 13, 67, 4, 1, 0xFFFFE08A);
        P(rx + 6, 75, 1, 1, B((Md(t, 1400) < 900)) ? 0xFFFF6A6A : 0xFF6A3A3A);
        } else if (B(rk == 2)) {
        P(rx + 2, 72, 32, 12, 0xFF2A2E44);
        P(rx + 2, 72, 32, 1, 0xFF5A5E78);
        P(rx + 3, 73, 13, 9, 0xFFB9DCF2);
        P(rx + 20, 73, 13, 9, 0xFFB9DCF2);
        P(rx + 4, 74, 4, 7, 0xFF8FC3E6);
        P(rx + 21, 74, 4, 7, 0xFF8FC3E6);
        P(rx + 10, 76, 4, 4, 0xFFD9463E);
        P(rx + 27, 76, 3, 4, 0xFF4FA85C);
        P(rx + 24, 77, 2, 3, 0xFFFFE08A);
        P(rx + 16, 74, 4, 10, 0xFF4A3A2A);
        P(rx + 19, 79, 1, 1, 0xFFFFD98A);
            for (int A2 = 1; A2 <= (int)(4); A2++) {
            q = A2;
            P(rx + 30, rtop + 8 + (q - 1)*10, 5, 1, 0xFF23263A);
            P(rx + 34, rtop + 9 + (q - 1)*10, 1, 9, 0xFF23263A);
                for (int A3 = 1; A3 <= (int)(3); A3++) {
                P(rx + 30 + (A3 - 1)*2, rtop + 6 + (q - 1)*10, 1, 2, 0xFF23263A);
                }
            }
        } else if (B(rk == 3)) {
        P(rx + 12, 74, 5, 10, 0xFF9A2A2A);
        P(rx + 13, 75, 3, 3, 0xFF9FD3F0);
        P(rx + 16, 79, 1, 1, 0xFFFFD98A);
        P(rx + 11, 73, 7, 1, 0xFFF2CC8E);
        P(rx + 10, 83, 9, 1, 0xFFC9A87A);
        P(rx + 11, 82, 7, 1, 0xFFD9B98A);
        P(rx + 3, 76, 5, 1, 0xFF2A2E44);
        P(rx + 21, 76, 5, 1, 0xFF2A2E44);
        P(rx + 3, 73, 5, 3, 0xFF3E8A48);
        P(rx + 21, 73, 5, 3, 0xFF3E8A48);
        P(rx + 4, 74, 1, 1, 0xFFE8574A);
        P(rx + 23, 74, 1, 1, 0xFFE8574A);
        P(rx + 6, 73, 1, 1, 0xFF6FBF63);
        } else {
        P(rx + Fd(rw, 2) - 2, 76, 4, 8, 0xFF2A2236);
        P(rx + Fd(rw, 2) + 1, 80, 1, 1, 0xFFFFD98A);
        P(rx + Fd(rw, 2) - 1, 77, 2, 2, 0xFF9FD3F0);
        P(rx + Fd(rw, 2) + 3, 77, 1, 1, 0xFFFFE9A8);
        P(rx + Fd(rw, 2) + 3, 78, 1, 1, 0xFF23263A);
        }
        if (B(ri == 2)) {
        P(rx + 20, rtop - 7, 5, 5, 0xFF6A5A4A);
        P(rx + 19, rtop - 8, 7, 1, 0xFF4A3A2A);
        P(rx + 21, rtop - 9, 3, 1, 0xFF4A3A2A);
        P(rx + 21, rtop - 2, 1, 2, 0xFF3A2E24);
        P(rx + 23, rtop - 2, 1, 2, 0xFF3A2E24);
        P(rx + 20, rtop - 6, 5, 1, 0xFF7A6A5A);
        } else if (B(ri == 4)) {
        P(rx + 6, rtop - 8, 1, 8, 0xFF2A2E44);
        P(rx + 3, rtop - 6, 7, 1, 0xFF2A2E44);
        P(rx + 4, rtop - 4, 5, 1, 0xFF2A2E44);
        P(rx + 5, rtop - 2, 3, 1, 0xFF2A2E44);
        } else if (B(ri == 3)) {
        TownTree(P_, rx + 10, rtop, Rn(Math.Sin(t*0.0012)), 0xFF5A3E2E, 0xFF3E8A48, 0xFF6FBF63);
        P(rx + 8, rtop - 2, 8, 2, 0xFF7A4A2A);
        P(rx + 8, rtop - 3, 8, 1, 0xFF4FA85C);
        P(rx + 10, rtop - 4, 1, 1, 0xFFE8574A);
        P(rx + 13, rtop - 4, 1, 1, 0xFFFFE08A);
        }
        }
        for (int A1 = 1; A1 <= (int)(12); A1++) {
        k = A1;
        P(30 + k*2, 46 + Rn(Math.Sin((k - 1)/11.0*3.14159)*3), 1, 1, B((Md(Math.Floor(t/300) + k, 3) == 0)) ? 0xFFFFE9A8 : 0xFFE8C05A);
        }
        for (int A1 = 1; A1 <= (int)(4); A1++) {
        k = A1;
        wv = Rn(Math.Sin(t*0.004 + k)*0.6 + 0.5);
        P(5 + k*2 + (k > 2 ? 118 : 0), 41 + wv, 1, 2 - wv, B(Md(k, 2)) ? 0xFFFFE9A8 : 0xFFB9DCF2);
        }
    P(4, 41, 10, 1, 0xFF23263A);
    P(122, 41, 8, 1, 0xFF23263A);
    P(0, 84, 142, 6, 0xFFCFCABC);
        for (int A1 = 1; A1 <= (int)(70); A1++) {
        k = A1;
        P(Md(k*2 + (B(Md(k, 2)) ? 1 : 0), 142), 84 + Md(k*3, 5), 1, 1, 0xFFC0BAAC);
        }
    P(0, 89, 142, 1, 0xFFA9A396);
    P(0, 90, 142, 11, 0xFF585D6B);
        for (int A1 = 1; A1 <= (int)(40); A1++) {
        k = A1;
        P(Md(k*23, 142), 91 + Md(k*5, 9), 1, 1, 0xFF60656F);
        }
        for (int A1 = 1; A1 <= (int)(18); A1++) {
        P((A1 - 1)*8 + 2, 95, 4, 1, 0xFFE8C95A);
        }
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        P(70 + (A1 - 1)*4, 90, 2, 11, 0xFF9A9EA8);
        }
    P(0, 90, 142, 1, 0xFF6C7080);
    P(96, 88, 3, 2, 0xFF4A4E5C);
    P(97, 89, 1, 1, 0xFF3A3E4C);
        foreach (var (tk_, tx0_) in En(new double[] { 10, 96, 126 })) {
        tk = tk_; tx0 = tx0_;
        cL = B((tk == 2)) ? 0xFFE0872E : 0xFFD9B23E;
        cL2 = B((tk == 2)) ? 0xFFF2A24A : 0xFFF0CF62;
        TownTree(P_, tx0, 84, Rn(Math.Sin(t*0.0012 + tk*1.7)), 0xFF5A3E2E, cL, cL2, 1);
        P(tx0 + 2, 84, 7, 1, 0xFF8A6A4A);
        }
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        k = A1;
        lp = Md(t*0.00035 + k*0.17, 1.0);
        P(8 + Md(k*23, 130) + Rn(Math.Sin(lp*12 + k)*2), 60 + Rn(lp*24), 1, 1, B(Md(k, 2)) ? 0xFFE0872E : 0xFFD9B23E);
        }
        foreach (var (tk_, tx0_) in En(new double[] { 30, 42 })) {
        tk = tk_; tx0 = tx0_;
        P(tx0, 74, 1, 10, 0xFF23263A);
        P(tx0 - 4, 73, 9, 1, 0xFFD9463E);
        P(tx0 - 3, 72, 7, 1, 0xFFD9463E);
        P(tx0 - 2, 71, 5, 1, 0xFFE8574A);
            for (int A2 = 1; A2 <= (int)(4); A2++) {
            P(tx0 - 4 + (A2 - 1)*2, 74, 1, 1, 0xFFF4F1E8);
            }
        P(tx0 - 3, 80, 7, 1, 0xFF6E4A34);
        P(tx0 - 2, 81, 1, 3, 0xFF6E4A34);
        P(tx0 + 2, 81, 1, 3, 0xFF6E4A34);
        P(tx0 - 2, 79, 1, 1, 0xFFF4F1E8);
        P(tx0 + 1, 79, 1, 1, 0xFF8A3A4C);
        }
    TownWalker(P_, 51, 84, 0xFF6A4A8A, 0xFF23263A, 0xFFF1C9A2, 0xFF3A2E24, 0, 1, 0xFF23263A);
    P(53, 80, 3, 1, 0xFF8A5A3A);
    P(55, 79, 1, 1, 0xFF8A5A3A);
    P(56, 80, 1, 2, 0xFF5A3E2E);
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        np = Md(t*0.0007 + k*0.33, 1.0);
        P(56 + Rn(Math.Sin(np*8 + k)*2), 76 - Rn(np*10), 1, 1, Alpha(0xFF23263A, Rn(200*(1 - np))));
        P(57 + Rn(Math.Sin(np*8 + k)*2), 75 - Rn(np*10), 1, 1, Alpha(0xFF23263A, Rn(160*(1 - np))));
        }
    P(48, 83, 3, 1, 0xFF8A6A4A);
    P(49, 82, 1, 1, 0xFFE8C95A);
        foreach (var (lk_, lx0_) in En(new double[] { 64, 118 })) {
        lk = lk_; lx0 = lx0_;
        P(lx0, 76, 1, 8, 0xFF23263A);
        P(lx0 - 2, 75, 5, 1, 0xFF23263A);
        P(lx0 - 2, 76, 1, 1, 0xFF23263A);
        P(lx0 + 2, 76, 1, 1, 0xFF23263A);
        P(lx0 - 1, 74, 3, 1, 0xFF23263A);
        P(lx0 - 3, 77, 2, 2, 0xFF4FA85C);
        P(lx0 + 2, 77, 2, 2, 0xFF4FA85C);
        P(lx0 - 3, 77, 1, 1, 0xFFE8574A);
        P(lx0 + 3, 78, 1, 1, 0xFFFFE08A);
        }
    P(60, 80, 3, 4, 0xFF3F7FC9);
    P(60, 79, 3, 1, 0xFF2F5F99);
    P(61, 81, 1, 1, 0xFFF4F1E8);
    P(86, 81, 2, 3, 0xFFE8574A);
    P(85, 81, 4, 1, 0xFFE8574A);
    P(86, 80, 2, 1, 0xFFB83A2E);
    P(115, 80, 3, 4, 0xFF5A5E78);
    P(115, 79, 3, 1, 0xFF3A3E4C);
    P(119, 80, 3, 4, 0xFF4A6E4A);
    P(119, 79, 3, 1, 0xFF2E4A2E);
    P(102, 80, 8, 1, 0xFF23263A);
    P(103, 81, 1, 3, 0xFF23263A);
    P(108, 81, 1, 3, 0xFF23263A);
    P(104, 81, 4, 1, 0xFFB8322A);
    P(105, 82, 1, 1, 0xFF23263A);
    P(107, 82, 1, 1, 0xFF23263A);
    P(134, 76, 1, 8, 0xFF23263A);
    P(132, 76, 5, 3, 0xFFE8C95A);
    P(133, 77, 3, 1, 0xFF23263A);
    P(36, 82, 6, 2, 0xFF6E4A34);
        for (int A1 = 1; A1 <= (int)(5); A1++) {
        k = A1;
        pk = (Md(t + k*300, 1100) < 550);
        px0 = 74 + k*5 + Rn(Math.Sin(t*0.0004 + k)*2);
        P(px0, 83, 2, 1, 0xFF8A8DA5);
        P(px0 + 2, 82 + (B(pk) ? 1 : 0), 1, 1, 0xFF8A8DA5);
        P(px0 + 1, 82, 1, 1, 0xFFA5A8BE);
        }
    fph = Md(t, 8000);
    if (B(fph < 2600)) {
    fx0 = 80 + Rn(fph*0.02);
    fy0 = 82 - Rn(fph*0.012);
    TownBird(P_, fx0, fy0, Md(Math.Floor(t/120), 2), 0xFF8A8DA5);
    }
    cols = new[] { new double[] { 0xFF3A4C8A, 0xFF23263A }, new double[] { 0xFF4FA85C, 0xFF5A3E2E }, new double[] { 0xFFE8574A, 0xFF3A4C8A }, new double[] { 0xFF6A4A8A, 0xFF23263A }, new double[] { 0xFFD9B23E, 0xFF4A3A2A } };
        for (int A1 = 1; A1 <= (int)(5); A1++) {
        k = A1;
        dir = B((k >= 4)) ? -1 : 1;
        px0 = B((dir > 0)) ? Md(t*(0.0032 + 0.0007*k) + k*45, 158) - 8 : 150 - Md(t*(0.0034 + 0.0005*k) + k*70, 158);
        stp = Md(Math.Floor(t/170) + k, 2);
        if (B(k == 3)) {
        P(px0, 80, 2, 2, 0xFFF1C9A2);
        P(px0, 79, 2, 1, 0xFFE9D5A8);
        P(px0, 82, 2, 1, cols[(int)(k) - 1][(int)(1) - 1]);
        P(px0 - stp, 83, 1, 1, cols[(int)(k) - 1][(int)(2) - 1]);
        P(px0 + 1 + stp, 83, 1, 1, cols[(int)(k) - 1][(int)(2) - 1]);
        P(px0 + 2, 78, 1, 4, 0xFF9A9EA8);
        P(px0 + 2, 75 + Rn(Math.Sin(t*0.003)*1), 2, 3, 0xFFE8574A);
        P(px0 + 2, 74 + Rn(Math.Sin(t*0.003)*1), 1, 1, 0xFFFF8A7A);
        } else {
        TownWalker(P_, px0, 84, cols[(int)(k) - 1][(int)(1) - 1], cols[(int)(k) - 1][(int)(2) - 1], 0xFFF1C9A2, B((k == 2)) ? 0xFFE9D5A8 : (k == 5) ? 0xFFB83A2E : 0xFF3A2E24, stp, dir, B((k == 4)) ? 0xFF23263A : 0);
        }
        if (B(k == 1)) {
        P(px0 - 4, 83, 3, 1, 0xFF8A6A4A);
        P(px0 - 5, 82, 1, 1, 0xFF8A6A4A);
        P(px0 - 4 + stp, 84, 1, 1, 0xFF8A6A4A);
        P(px0 - 2 - stp, 84, 1, 1, 0xFF8A6A4A);
        P(px0 - 2, 82 - stp, 1, 1, 0xFF8A6A4A);
        }
        if (B(k == 5)) {
        P(px0 - 2, 81, 2, 3, 0xFFE0872E);
        }
        }
    P(37, 78, 2, 2, 0xFFF1C9A2);
    P(37, 77, 2, 1, 0xFF3A2E24);
    P(37, 80, 2, 2, 0xFF8A3A4C);
    P(39, 78, 2, 2, 0xFFF1C9A2);
    P(39, 77, 2, 1, 0xFFE9D5A8);
    P(39, 80, 2, 2, 0xFF3A4C8A);
    lv = Tw.Level(2);
    if (B(lv >= 1)) {
        for (int A1 = 1; A1 <= (int)(2); A1++) {
        q = A1;
        kx = 30 + q*50 + Rn(6*Math.Sin(t*0.0009 + q));
        ky = 10 + q*4 + Rn(4*Math.Sin(t*0.0013 + q*2));
        P(kx, ky - 3, 1, 1, q == 1 ? 0xFFE8574A : 0xFF3F7FC9);
        P(kx - 1, ky - 2, 3, 1, q == 1 ? 0xFFE8574A : 0xFF3F7FC9);
        P(kx - 2, ky - 1, 5, 1, q == 1 ? 0xFFE8574A : 0xFF3F7FC9);
        P(kx - 1, ky, 3, 1, q == 1 ? 0xFFE8574A : 0xFF3F7FC9);
        P(kx, ky + 1, 1, 1, q == 1 ? 0xFFE8574A : 0xFF3F7FC9);
            for (int A2 = 1; A2 <= (int)(5); A2++) {
            P(kx + Rn(Math.Sin(t*0.004 + A2)*1.2), ky + 1 + A2, 1, 1, B(Md(A2, 2)) ? 0xFFFFE08A : 0xFFF4F1E8);
            }
            for (int A2 = 1; A2 <= (int)(12); A2++) {
            P(kx - Rn((10 + q*6)*A2/12.0), ky + 6 + Rn((84 - ky - 6)*A2/12.0), 1, 1, Alpha(0xFF3A3F5C, 110));
            }
        }
    }
    if (B(lv >= 2)) {
    P(116, 76, 16, 8, 0xFFE8C95A);
    P(116, 75, 12, 1, 0xFFD9B23E);
    P(128, 77, 4, 3, 0xFF9FD3F0);
    P(118, 78, 8, 3, 0xFF3A2E24);
    P(119, 79, 6, 1, 0xFFFFE9C0);
    P(117, 84, 2, 1, 0xFF1C1E2A);
    P(128, 84, 2, 1, 0xFF1C1E2A);
    P(117, 73, 14, 2, 0xFFE8574A);
    P(120, 74, 2, 1, 0xFFF4F1E8);
    P(124, 74, 2, 1, 0xFFF4F1E8);
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        q = A1;
        sp = Md(t*0.0011 + q*0.33, 1.0);
        P(121 + Rn(Math.Sin(sp*6 + q)*1.2), 77 - Rn(sp*6), 1, 1, Alpha(0xFFF4F1E8, Rn(150*(1 - sp))));
        }
    TownWalker(P_, 111, 84, 0xFF8A3A4C, 0xFF23263A, 0xFFF1C9A2, 0xFF3A2E24, 0, 1);
    TownWalker(P_, 106, 84, 0xFF3A4C8A, 0xFF5A3E2E, 0xFFF1C9A2, 0xFFE9D5A8, Md(Math.Floor(t/500), 2), 1);
    }
    if (B(lv >= 3)) {
    P(0, 96, 142, 1, 0xFF8A8DA5);
    P(0, 98, 142, 1, 0xFF8A8DA5);
    trx = Md(t*0.009, 240) - 60;
    P(trx, 92, 34, 5, 0xFF4FA85C);
    P(trx + 1, 91, 32, 1, 0xFF3E8A48);
    P(trx + 15, 92, 4, 5, 0xFFF4F1E8);
        for (int A1 = 1; A1 <= (int)(7); A1++) {
        P(trx + 3 + (A1 - 1)*4, 93, 3, 2, 0xFF9FD3F0);
        }
    P(trx + 2, 97, 2, 1, 0xFF1C1E2A);
    P(trx + 30, 97, 2, 1, 0xFF1C1E2A);
    P(trx + 12, 97, 2, 1, 0xFF1C1E2A);
    P(trx + 20, 97, 2, 1, 0xFF1C1E2A);
    P(trx + 8, 89, 1, 2, 0xFF23263A);
    P(trx + 4, 88, 9, 1, 0xFF23263A);
    P(trx + 33, 94, 1, 1, 0xFFFFE9A8);
    }
    if (B(lv >= 4)) {
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        q = A1;
        P(58 + q*5 + Rn(Math.Sin(t*0.002 + q)*1), 12 + Rn(Math.Sin(t*0.0015 + q*1.3)*1.5), 2, 2, B((Md(q, 3) == 0)) ? 0xFFE8574A : (Md(q, 3) == 1) ? 0xFFFFE08A : 0xFF3F7FC9);
        P(59 + q*5 + Rn(Math.Sin(t*0.002 + q)*1), 14 + Rn(Math.Sin(t*0.0015 + q*1.3)*1.5), 1, 3, Alpha(0xFF3A3F5C, 120));
        }
        for (int A1 = 1; A1 <= (int)(14); A1++) {
        P(60 + (A1 - 1)*2, 21 + Rn(Math.Sin((A1 - 1)/13.0*3.14159)*2), 1, 1, B((Md(Math.Floor(t/250) + A1, 3) == 0)) ? 0xFFFFE9A8 : 0xFFE8C05A);
        }
        for (int A1 = 1; A1 <= (int)(10); A1++) {
        q = A1;
        cp = Md(t*0.0004 + q*0.1, 1.0);
        P(58 + Md(q*7, 34) + Rn(Math.Sin(cp*10 + q)*2), 10 + Rn(cp*14), 1, 1, B((Md(q, 3) == 0)) ? 0xFFE8574A : (Md(q, 3) == 1) ? 0xFFFFE08A : 0xFF9FD3F0);
        }
    TownWalker(P_, 66, 23, 0xFF6A4A8A, 0xFF23263A, 0xFFF1C9A2, 0xFF3A2E24, Md(Math.Floor(t/300), 2), 1);
    TownWalker(P_, 80, 23, 0xFFE8574A, 0xFF3A4C8A, 0xFFF1C9A2, 0xFFE9D5A8, Md(Math.Floor(t/300) + 1, 2), -1);
    }
    if (B(lv >= 5)) {
    pph = Md(t, 40000);
    if (B(pph < 14000)) {
    pbx = -40 + Rn(pph*0.012);
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        q = A1;
        TownWalker(P_, pbx + q*6, 84, (B(Md(q, 2)) ? 0xFFE8574A : 0xFFFFE08A), 0xFF23263A, 0xFFF1C9A2, 0xFF3A2E24, Md(Math.Floor(t/170) + q, 2), 1, 0xFFE8574A);
        P(pbx + q*6 - 1, 79, 1, 2, 0xFFE8C95A);
        }
    P(pbx - 4, 74, 8, 5, 0xFFE8574A);
    P(pbx - 3, 75, 6, 3, 0xFFFFE08A);
    P(pbx, 79, 1, 5, 0xFF5A3E2E);
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        q = A1;
        np = Md(t*0.0009 + q*0.33, 1.0);
        P(pbx + 30 + Rn(Math.Sin(np*8 + q)*3), 76 - Rn(np*12), 1, 1, Alpha(0xFF23263A, Rn(200*(1 - np))));
        }
    }
    }
    cyx = Md(t*0.0075, 170) - 12;
    P(cyx, 90, 2, 1, 0xFF23263A);
    P(cyx + 4, 90, 2, 1, 0xFF23263A);
    P(cyx + 1, 89, 4, 1, 0xFFB8322A);
    P(cyx + 2, 86, 2, 3, 0xFF4FA85C);
    P(cyx + 2, 84, 2, 2, 0xFFF1C9A2);
    P(cyx + 2, 83, 2, 1, 0xFFD9463E);
    P(cyx + 1 + Md(Math.Floor(t/110), 2), 91, 1, 1, 0xFF3A3E4C);
    P(cyx + 5 - Md(Math.Floor(t/110), 2), 91, 1, 1, 0xFF3A3E4C);
    vph = Md(t, 14000);
    if (B(vph < 6000)) {
    vx0 = 150 - Rn(vph*0.028);
    P(vx0, 91, 12, 4, 0xFFF4F1E8);
    P(vx0, 90, 9, 1, 0xFFF4F1E8);
    P(vx0 + 9, 91, 3, 2, 0xFF9FD3F0);
    P(vx0 + 1, 91, 5, 2, 0xFFE8574A);
    P(vx0 + 2, 95, 1, 1, 0xFF1C1E2A);
    P(vx0 + 10, 95, 1, 1, 0xFF1C1E2A);
    }
    cph = Md(t, 9000);
    if (B(cph < 5000)) {
    cx1 = -12 + Rn(cph*0.031);
    P(cx1, 92, 9, 2, 0xFF3F7FC9);
    P(cx1 + 2, 91, 5, 1, 0xFF9FD3F0);
    P(cx1 + 1, 94, 1, 1, 0xFF1C1E2A);
    P(cx1 + 7, 94, 1, 1, 0xFF1C1E2A);
    }
    bph = Md(t, 40000);
    if (B(bph < 9000)) {
    bxs = -20 + Rn(bph*0.02);
    P(bxs, 91, 18, 4, 0xFFE8574A);
    P(bxs + 1, 90, 16, 1, 0xFFC94A3E);
    P(bxs + 1, 91, 16, 2, 0xFF9FD3F0);
    P(bxs + 2, 95, 1, 1, 0xFF1C1E2A);
    P(bxs + 15, 95, 1, 1, 0xFF1C1E2A);
    P(bxs + 9, 95, 1, 1, 0xFF1C1E2A);
    }

    }
    public void Village()
    {
    double t = 0, k = 0, pk = 0, py_ = 0, an = 0, fp = 0, sk = 0, sp = 0, cxk = 0, cyk = 0, rp = 0, rr = 0, rcx = 0, rcy = 0, cR = 0, dk = 0, dph = 0, da = 0, dx0 = 0, dy0 = 0, fph = 0, fa = 0, hi = 0, hx = 0, hy = 0, hw = 0, hd = 0, cWl = 0, dgx = 0, dgy = 0, lk = 0, r = 0, c = 0, gw = 0, cxp = 0, cyp = 0, cph = 0, fk = 0, fc = 0, fx = 0, fy = 0, bk = 0, bx0 = 0, tx0 = 0, ty0 = 0, dp = 0, sw = 0, cD = 0, cL = 0, rk = 0, px0 = 0, stp = 0, rnd = 0, wy = 0, vy0 = 0, fx0 = 0, cx0 = 0, cy0 = 0, dst = 0, hp = 0, hx0 = 0, hy0 = 0, bfx = 0, bfy = 0, lv = 0, q = 0, shx = 0, shy = 0, wa = 0, ca = 0, by0 = 0, lx = 0, ly = 0, fcx = 0, fcy = 0, fcl = 0;
    bool nib = false;
    double[] pr = null!, sg = null!, h = null!, lp = null!, bs = null!, rs = null!;
    double[][] hs = null!;
    t = DecT(now);
    P(0, 0, 142, 101, 0xFF7CBF5A);
        for (int A1 = 1; A1 <= (int)(110); A1++) {
        k = A1;
        P(Md(k*29, 142), Md(k*17 + 3, 101), 2, 1, 0xFF6BB04E);
        }
        for (int A1 = 1; A1 <= (int)(24); A1++) {
        k = A1;
        P(Md(k*43 + 5, 138), Md(k*31 + 7, 97), 4 + Md(k, 3), 2, 0xFF86C866);
        }
        foreach (var (pk_, pr_) in En(new[] { new double[] { 0, 44, 142, 10 }, new double[] { 64, 20, 10, 20 }, new double[] { 64, 58, 10, 26 }, new double[] { 14, 54, 10, 30 }, new double[] { 14, 80, 72, 8 }, new double[] { 98, 80, 14, 8 }, new double[] { 112, 54, 6, 6 }, new double[] { 90, 18, 8, 28 } })) {
        pk = pk_; pr = pr_;
        P(pr[(int)(1) - 1], pr[(int)(2) - 1], pr[(int)(3) - 1], pr[(int)(4) - 1], 0xFFE3CC90);
        }
        for (int A1 = 1; A1 <= (int)(60); A1++) {
        k = A1;
        P(Md(k*23 + 5, 142), 45 + Md(k*7, 8), 1, 1, 0xFFD2B77A);
        }
        for (int A1 = 1; A1 <= (int)(40); A1++) {
        k = A1;
        py_ = 20 + Md(k*11, 64);
        if (B(py_ < 38 || py_ > 60)) {
        P(65 + Md(k*3, 8), py_, 1, 1, 0xFFD2B77A);
        }
        }
    P(0, 44, 142, 1, 0xFF9DBE6A);
    P(0, 53, 142, 1, 0xFF9DBE6A);
    P(58, 38, 22, 22, 0xFFC9B792);
        for (int A1 = 1; A1 <= (int)(60); A1++) {
        k = A1;
        P(58 + Md(k*7, 22), 38 + Md(k*11, 22), 1, 1, 0xFFB7A47E);
        }
    P(64, 44, 10, 10, 0xFF8A8DA5);
    P(65, 45, 8, 8, 0xFF4C8FD8);
    P(68, 48, 2, 2, 0xFF9A9EB8);
        for (int A1 = 1; A1 <= (int)(4); A1++) {
        an = (A1 - 1)*1.5708 + t*0.001;
        fp = Math.Abs(Math.Sin(t*0.0025 + A1));
        P(69 + Rn((2 + 2*fp)*Math.Cos(an)), 49 + Rn((2 + 2*fp)*Math.Sin(an)), 1, 1, 0xFFDDF0FA);
        }
    P(66, 46, 1, 1, 0xFFA6D4F0);
    P(71, 51, 1, 1, 0xFFA6D4F0);
        foreach (var (sk_, sg_) in En(new[] { new double[] { 92, 76, 5, 6 }, new double[] { 90, 82, 5, 6 }, new double[] { 88, 88, 5, 7 }, new double[] { 86, 95, 5, 6 } })) {
        sk = sk_; sg = sg_;
        P(sg[(int)(1) - 1], sg[(int)(2) - 1], sg[(int)(3) - 1], sg[(int)(4) - 1], 0xFF4C8FD8);
        P(sg[(int)(1) - 1] - 1, sg[(int)(2) - 1], 1, sg[(int)(4) - 1], 0xFF3B6E9E);
        P(sg[(int)(1) - 1] + sg[(int)(3) - 1], sg[(int)(2) - 1], 1, sg[(int)(4) - 1], 0xFF3B6E9E);
        }
        for (int A1 = 1; A1 <= (int)(10); A1++) {
        k = A1;
        sp = Md(t*0.0009 + k*0.1, 1.0);
        P(92 - Rn(sp*6) + Md(k, 3), 77 + Rn(sp*22), 1, 1, 0xFFA6D4F0);
        }
    P(86, 81, 12, 3, 0xFF8A6A4A);
    P(86, 80, 12, 1, 0xFF6E4A34);
    P(86, 84, 12, 1, 0xFF6E4A34);
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        P(86 + (A1 - 1)*2, 82, 1, 1, 0xFF7A5A3A);
        }
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        cxk = Md(t*(0.0009 + 0.0004*k) + k*50, 200) - 40;
        cyk = 10 + k*28;
        P(cxk, cyk + 2, 26, 8, Alpha(0xFF1E3A2A, 30));
        P(cxk + 4, cyk, 16, 2, Alpha(0xFF1E3A2A, 30));
        P(cxk + 6, cyk + 10, 14, 2, Alpha(0xFF1E3A2A, 30));
        }
    P(83, 57, 26, 20, 0xFF8FA96A);
    P(81, 60, 30, 14, 0xFF8FA96A);
    P(84, 58, 24, 18, 0xFF3B6E9E);
    P(82, 61, 28, 12, 0xFF3B6E9E);
    P(85, 59, 22, 16, 0xFF4C8FD8);
    P(83, 62, 26, 10, 0xFF4C8FD8);
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        rp = Md(t*0.0009 + k*0.33, 1.0);
        rr = 1 + Rn(rp*5);
        rcx = 88 + k*6;
        rcy = 63 + Md(k, 2)*5;
        cR = Alpha(0xFFA6D4F0, Rn(200*(1 - rp)));
        P(rcx - rr, rcy - rr, rr*2 + 1, 1, cR);
        P(rcx - rr, rcy + rr, rr*2 + 1, 1, cR);
        P(rcx - rr, rcy - rr, 1, rr*2 + 1, cR);
        P(rcx + rr, rcy - rr, 1, rr*2 + 1, cR);
        }
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        k = A1;
        P(86 + Md(k*7, 20), 60 + Md(k*5, 13), 2 + Md(k, 2), 1, Alpha(0xFFDDF0FA, Rn(60 + 60*Math.Sin(t*0.004 + k))));
        }
    P(86, 70, 3, 2, 0xFF4FA85C);
    P(87, 70, 1, 1, 0xFFF2A2C8);
    P(102, 61, 3, 2, 0xFF4FA85C);
    P(103, 61, 1, 1, 0xFFF2A2C8);
    P(95, 73, 2, 1, 0xFF4FA85C);
    P(84, 74, 3, 3, 0xFF4FA85C);
    P(105, 57, 3, 3, 0xFF4FA85C);
    P(85, 73, 1, 1, 0xFF6FBF63);
    P(106, 56, 1, 1, 0xFF6FBF63);
        foreach (var (dk_, dph_) in En(new double[] { 0.0, 0.5 })) {
        dk = dk_; dph = dph_;
        da = t*0.0006 + dph*6.28;
        dx0 = 95 + Rn(7*Math.Cos(da));
        dy0 = 66 + Rn(4*Math.Sin(da));
        P(dx0, dy0, 2, 1, 0xFFF4F1E8);
        P(dx0 + (Math.Cos(da) < 0 ? -1 : 2), dy0 - 1, 1, 1, 0xFFF4F1E8);
        P(dx0 + (Math.Cos(da) < 0 ? -2 : 3), dy0 - 1, 1, 1, 0xFFE8A64A);
        }
    fph = Md(t, 6000);
    if (B(fph < 900)) {
    fa = fph/900.0;
    P(97 + Rn(fa*4), 66 - Rn(Math.Sin(fa*3.14159)*4), 2, 1, 0xFFE8A64A);
    P(96 + Rn(fa*4), 66 - Rn(Math.Sin(fa*3.14159)*4), 1, 1, 0xFFFFD98A);
    }
    P(78, 62, 2, 2, 0xFFF1C9A2);
    P(78, 61, 2, 1, 0xFFD9B23E);
    P(78, 64, 2, 2, 0xFF3A4C8A);
    P(80, 62, 4, 1, 0xFF5A3E2E);
    P(84, 62, 1, 2, 0xFF23263A);
    P(85, 64 + (Md(t, 1600) < 800 ? 0 : 1), 1, 1, 0xFFE8574A);
    hs = new[] { new double[] { 30, 12, 22, 18, 0xFFD9463E, 0xFFE9D5A8 }, new double[] { 104, 10, 20, 16, 0xFF8AA0B8, 0xFFE0D8C8 } , new double[] { 32, 58, 18, 16, 0xFF9A6238, 0xFFE9D5A8 }, new double[] { 117, 62, 14, 14, 0xFF6FB3B6, 0xFFEFE6D0 } };
        foreach (var (hi_, h_) in En(hs)) {
        hi = hi_; h = h_;
        hx = h[(int)(1) - 1];
        hy = h[(int)(2) - 1];
        hw = h[(int)(3) - 1];
        hd = h[(int)(4) - 1];
        cR = h[(int)(5) - 1];
        cWl = h[(int)(6) - 1];
        P(hx - 3, hy - 3, hw + 6, hd + 8, 0xFF88C862);
            for (int A2 = 1; A2 <= (int)(hw + 6); A2++) {
            P(hx - 3 + (A2 - 1), hy - 3, 1, 1, B(Md(A2, 2)) ? 0xFF8A6A4A : 0xFF7A5A3A);
            }
            for (int A2 = 1; A2 <= (int)(hd + 8); A2++) {
            P(hx - 3, hy - 3 + (A2 - 1), 1, 1, B(Md(A2, 2)) ? 0xFF8A6A4A : 0xFF7A5A3A);
            P(hx + hw + 2, hy - 3 + (A2 - 1), 1, 1, B(Md(A2, 2)) ? 0xFF8A6A4A : 0xFF7A5A3A);
            }
        P(hx + 1, hy + hd, hw, 2, Alpha(0xFF1E3A2A, 60));
        P(hx + hw, hy + 1, 1, hd, Alpha(0xFF1E3A2A, 60));
        P(hx, hy + hd - 4, hw, 4, cWl);
        P(hx, hy + hd - 1, hw, 1, Mix(cWl, 0xFF000000, 0.2));
        P(hx, hy, hw, hd - 4, cR);
            for (int A2 = 1; A2 <= (int)(Fd((hd - 4), 2)); A2++) {
            P(hx, hy + (A2 - 1)*2 + 1, hw, 1, Mix(cR, 0xFF000000, 0.14));
            }
        P(hx, hy + Fd((hd - 4), 2), hw, 1, Mix(cR, 0xFF000000, 0.3));
        P(hx, hy, hw, 1, Mix(cR, 0xFFFFFFFF, 0.22));
        P(hx + Fd(hw, 2) - 2, hy + 2, 4, 3, Mix(cR, 0xFF000000, 0.35));
        P(hx + Fd(hw, 2) - 1, hy + 3, 2, 1, 0xFF9FD3F0);
        P(hx + Fd(hw, 2) - 1, hy + hd - 3, 3, 3, 0xFF6A3A22);
        P(hx + Fd(hw, 2), hy + hd - 2, 1, 1, 0xFFFFD98A);
        P(hx + Fd(hw, 2) - 2, hy + hd, 5, 1, 0xFFC9A87A);
        P(hx + 2, hy + hd - 3, 3, 2, 0xFF9FD3F0);
        P(hx + hw - 5, hy + hd - 3, 3, 2, 0xFF9FD3F0);
        P(hx + 2, hy + hd - 3, 1, 2, 0xFFF4E4C8);
        P(hx + 2, hy + hd - 1, 3, 1, 0xFF7A4A2A);
        P(hx + hw - 5, hy + hd - 1, 3, 1, 0xFF7A4A2A);
        P(hx + 3, hy + hd - 2, 1, 1, 0xFFE8574A);
        P(hx + hw - 4, hy + hd - 2, 1, 1, 0xFFFFE08A);
        P(hx + hw - 4, hy + 1, 2, 2, 0xFF5A4A4A);
        P(hx + hw - 4, hy + 1, 2, 1, 0xFF7A6A6A);
            for (int A2 = 1; A2 <= (int)(3); A2++) {
            k = A2;
            sp = Md(t*0.0008 + k*0.33 + hi*0.2, 1.0);
            P(hx + hw - 3 + Rn(Math.Sin(sp*6 + k)*1.5), hy - Rn(sp*6), 1, 1, Alpha(0xFFF4F1E8, Rn(150*(1 - sp))));
            }
        if (B(hi == 1)) {
        P(hx + 2, hy - 2, 1, 5, 0xFF5A3E2E);
        P(hx + 3, hy - 2 + (Md(t, 700) < 350 ? 0 : 1), 3, 2, 0xFFE8574A);
        }
        if (B(hi == 3)) {
        P(hx + hw + 4, hy + 16, 5, 4, 0xFF7A4A2A);
        P(hx + hw + 4, hy + 15, 5, 1, 0xFF9A2A2A);
        P(hx + hw + 6, hy + 18, 1, 2, 0xFF3A2E24);
        dgx = hx + hw + 4 + Rn(3*Math.Sin(t*0.0007));
        dgy = hy + 20;
        P(dgx, dgy, 3, 1, 0xFFC9A87A);
        P(dgx + 3, dgy - 1, 1, 1, 0xFFC9A87A);
        P(dgx - 1, dgy - (Md(t, 600) < 300 ? 1 : 0), 1, 1, 0xFFC9A87A);
        }
        if (B(hi == 2)) {
        P(hx - 2, hy + hd + 2, 12, 1, 0xFF23263A);
        P(hx, hy + hd + 3, 2, 2 - Md(Math.Floor(t/350), 2), 0xFFFFE9A8);
        P(hx + 4, hy + hd + 3, 2, 2 - Md(Math.Floor(t/350) + 1, 2), 0xFFB9DCF2);
        P(hx + 8, hy + hd + 3, 2, 2 - Md(Math.Floor(t/350), 2), 0xFFF2A2C8);
        }
        }
    P(56, 62, 6, 5, 0xFF7A7A8A);
    P(57, 63, 4, 3, 0xFF5A5E78);
    P(58, 64, 2, 2, 0xFF3B6E9E);
    P(56, 60, 1, 2, 0xFF6A3A22);
    P(61, 60, 1, 2, 0xFF6A3A22);
    P(55, 59, 8, 1, 0xFF9A2A2A);
    P(56, 58, 6, 1, 0xFFB83A2E);
    P(58, 61 + Md(Math.Floor(t/900), 2), 1, 1, 0xFF8A6A4A);
    P(108, 32, 10, 6, 0xFF8A6A4A);
    P(107, 30, 12, 2, B(Md(Math.Floor(t/500), 2)) ? 0xFFE8574A : 0xFFD9463E);
        for (int A1 = 1; A1 <= (int)(6); A1++) {
        P(107 + (A1 - 1)*2, 30, 1, 2, 0xFFF4F1E8);
        }
    P(109, 34, 2, 2, 0xFFE8A64A);
    P(112, 34, 2, 2, 0xFFE8574A);
    P(115, 34, 2, 2, 0xFF4FA85C);
    P(109, 36, 8, 1, 0xFFD9B23E);
    P(48, 38, 6, 2, 0xFF6E4A34);
    P(48, 40, 1, 1, 0xFF6E4A34);
    P(53, 40, 1, 1, 0xFF6E4A34);
    P(82, 38, 6, 2, 0xFF6E4A34);
    P(82, 40, 1, 1, 0xFF6E4A34);
    P(87, 40, 1, 1, 0xFF6E4A34);
    P(49, 37, 2, 1, 0xFF3A4C8A);
    P(52, 37, 2, 1, 0xFF8A3A4C);
    P(49, 36, 2, 1, 0xFFF1C9A2);
    P(52, 36, 2, 1, 0xFFF1C9A2);
        foreach (var (lk_, lp_) in En(new[] { new double[] { 57, 34 }, new double[] { 83, 34 }, new double[] { 108, 41 }, new double[] { 12, 76 }, new double[] { 55, 70 }, new double[] { 112, 76 } })) {
        lk = lk_; lp = lp_;
        P(lp[(int)(1) - 1], lp[(int)(2) - 1], 1, 3, 0xFF2A2E44);
        P(lp[(int)(1) - 1] - 1, lp[(int)(2) - 1] - 1, 3, 1, 0xFF2A2E44);
        P(lp[(int)(1) - 1], lp[(int)(2) - 1] - 1, 1, 1, 0xFFFFE9A8);
        }
    P(128, 38, 1, 6, 0xFF5A3E2E);
    P(126, 38, 6, 2, 0xFFE0D8C8);
    P(126, 41, 5, 2, 0xFFE0D8C8);
    P(98, 88, 34, 12, 0xFFA8895A);
    P(98, 88, 34, 1, 0xFF8A6A4A);
    P(98, 99, 34, 1, 0xFF8A6A4A);
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        r = A1;
            for (int A2 = 1; A2 <= (int)(8); A2++) {
            c = A2;
            gw = Rn(Math.Sin(t*0.0015 + r + c)*0.5 + 0.5);
            cxp = 100 + (c - 1)*4;
            cyp = 91 + (r - 1)*3;
            if (B(r == 1)) {
            P(cxp, cyp - gw, 2, 1 + gw, 0xFF4FA85C);
            P(cxp, cyp + 1, 2, 1, 0xFFE8A64A);
            }
            else if (B(r == 2)) {
            P(cxp, cyp - 1, 1, 3, 0xFF3E8A48);
            P(cxp - 1, cyp - 2 - gw, 3, 2, 0xFFFFD98A);
            P(cxp, cyp - 2 + (1 - gw), 1, 1, 0xFF7A4A2A);
            }
            else {
            P(cxp, cyp, 3, 2, 0xFFE0872E);
            P(cxp + 1, cyp - 1, 1, 1, 0xFF3E8A48);
            P(cxp, cyp, 1, 1, 0xFFF2A24A);
            }
            }
        }
    P(126, 90, 1, 8, 0xFF5A3E2E);
    P(123, 92, 7, 1, 0xFF5A3E2E);
    P(125, 88, 3, 2, 0xFFE9D5A8);
    P(124, 87, 5, 1, 0xFFD9B23E);
    P(125, 86, 3, 1, 0xFFD9B23E);
    P(124, 93, 5, 2, 0xFF8A3A4C);
    cph = Md(t, 9000);
    if (B(cph > 5000 && cph < 8000)) {
    P(122, 91 - Md(Math.Floor(t/400), 2), 2, 1, 0xFF23263A);
    P(124, 90, 1, 1, 0xFF23263A);
    }
    else if (B(cph <= 5000)) {
    TownBird(P_, 60 + Rn(cph*0.013), 30 - Rn(Math.Sin(cph/5000.0*3.14159)*8), Md(Math.Floor(t/160), 2), 0xFF23263A);
    }
        foreach (var (fk_, fc_) in En(new double[] { 0xFFE8574A, 0xFFFFE08A, 0xFFF4F1E8, 0xFFF2A2C8 })) {
        fk = fk_; fc = fc_;
            for (int A2 = 1; A2 <= (int)(8); A2++) {
            k = A2 + fk*8;
            fx = Md(k*31 + 7, 138) + 1;
            fy = Md(k*13 + 2, 40) + (B(Md(k, 2)) ? 2 : 58);
            if (B((fx >= 58 && fx <= 80 && fy >= 38 && fy <= 60) || (fx >= 81 && fx <= 111 && fy >= 57 && fy <= 77))) {
            continue;
            }
            P(fx, fy, 1, 1, fc);
            }
        }
        foreach (var (bk_, bx0_) in En(new double[] { 12, 84, 130 })) {
        bk = bk_; bx0 = bx0_;
        P(bx0 + Rn(4*Math.Sin(t*0.0021 + bk)), 34 + Rn(3*Math.Sin(t*0.0033 + bk*2)), 1, 1, 0xFFFFD98A);
        P(bx0 + Rn(4*Math.Sin(t*0.0021 + bk)), 33 + Rn(3*Math.Sin(t*0.0033 + bk*2)), 1, 1, B(Md(Math.Floor(t/90), 2)) ? 0xFFF4F1E8 : 0xFF23263A);
        }
        for (int A1 = 1; A1 <= (int)(52); A1++) {
        k = A1;
        if (B(k <= 18)) {
        tx0 = (k - 1)*8;
        ty0 = -3 + Md(k, 3)*2;
        dp = Md(k, 3);
        }
        else if (B(k <= 36)) {
        tx0 = (k - 19)*8 + 4;
        ty0 = 90 + Md(k, 3)*2;
        dp = Md(k + 1, 3);
        }
        else if (B(k <= 44)) {
        tx0 = -3 + Md(k, 2)*3;
        ty0 = 4 + (k - 37)*11;
        dp = Md(k, 3);
        }
        else {
        tx0 = 135 + Md(k, 2)*3;
        ty0 = 2 + (k - 45)*11;
        dp = Md(k + 2, 3);
        }
        sw = Rn(Math.Sin(t*0.0011 + k*1.3));
        cD = B((dp == 0)) ? 0xFF2E7A45 : (dp == 1) ? 0xFF347F4A : 0xFF3A8A50;
        cL = B((dp == 0)) ? 0xFF4FA85C : (dp == 1) ? 0xFF5AB366 : 0xFF6FBF63;
        P(tx0 + 2, ty0 + 8, 6, 2, Alpha(0xFF1E3A2A, 70));
        P(tx0 + 1 + sw, ty0 + 1, 7, 6, cD);
        P(tx0 + 2 + sw, ty0, 5, 8, cD);
        P(tx0 + 2 + sw, ty0 + 1, 3, 2, cL);
        P(tx0 + 1 + sw, ty0 + 3, 2, 1, cL);
        P(tx0 + 5 + sw, ty0 + 4, 2, 1, Mix(cD, 0xFF000000, 0.2));
        }
        foreach (var (bk_, bs_) in En(new[] { new double[] { 14, 30 }, new double[] { 122, 26 }, new double[] { 6, 84 }, new double[] { 122, 80 }, new double[] { 76, 24 }, new double[] { 84, 8 } })) {
        bk = bk_; bs = bs_;
        P(bs[(int)(1) - 1], bs[(int)(2) - 1] + 1, 5, 2, 0xFF3E8A48);
        P(bs[(int)(1) - 1] + 1, bs[(int)(2) - 1], 3, 1, 0xFF4FA85C);
        P(bs[(int)(1) - 1] + 1, bs[(int)(2) - 1] + 3, 3, 1, 0xFF2E7A45);
        }
        foreach (var (rk_, rs_) in En(new[] { new double[] { 58, 30 }, new double[] { 110, 40 }, new double[] { 26, 70 } })) {
        rk = rk_; rs = rs_;
        P(rs[(int)(1) - 1], rs[(int)(2) - 1], 3, 2, 0xFF8A8DA5);
        P(rs[(int)(1) - 1] + 1, rs[(int)(2) - 1], 1, 1, 0xFFB0B4C4);
        P(rs[(int)(1) - 1], rs[(int)(2) - 1] + 2, 3, 1, 0xFF5A5E78);
        }
    P(20, 24, 1, 1, 0xFFE8574A);
    P(19, 25, 3, 1, 0xFFE8574A);
    P(20, 26, 1, 1, 0xFFF4F1E8);
    P(134, 36, 1, 1, 0xFFE8574A);
    P(133, 37, 3, 1, 0xFFE8574A);
    P(134, 38, 1, 1, 0xFFF4F1E8);
    P(6, 62, 8, 2, 0xFF6E4A34);
    P(6, 61, 8, 1, 0xFF8A6A4A);
    P(13, 61, 1, 3, 0xFF5A3E2E);
        for (int A1 = 1; A1 <= (int)(2); A1++) {
        k = A1;
        px0 = Md(t*(0.0028 + 0.0006*k) + k*60, 160) - 10;
        stp = Md(Math.Floor(t/190) + k, 2);
        rnd = B((px0 > 56 && px0 < 82)) ? Clamp(Math.Min(px0 - 56, 82 - px0)/5.0, 0.0, 1.0) : 0.0;
        wy = 44 - Rn(6*rnd);
        P(px0, wy, 2, 2, B((k == 1)) ? 0xFF3A2E24 : 0xFFE9D5A8);
        P(px0, wy + 2, 2, 2, B((k == 1)) ? 0xFF3A4C8A : 0xFFB84A3A);
        P(px0 - stp, wy + 4, 1, 1, 0xFF23263A);
        P(px0 + 1 + stp, wy + 4, 1, 1, 0xFF23263A);
        }
    vy0 = Md(t*0.0022, 34);
    vy0 = B((vy0 > 17)) ? 34 - vy0 : vy0;
    stp = Md(Math.Floor(t/190), 2);
    P(68, 20 + Rn(vy0), 2, 2, 0xFFD9B23E);
    P(68, 22 + Rn(vy0), 2, 2, 0xFF6A4A8A);
    P(68 - stp, 24 + Rn(vy0), 1, 1, 0xFF23263A);
    P(69 + stp, 24 + Rn(vy0), 1, 1, 0xFF23263A);
    fx0 = 112 + Rn(12*Math.Sin(t*0.0005));
    P(fx0, 80, 2, 1, 0xFFD9B23E);
    P(fx0, 81, 2, 1, 0xFFF1C9A2);
    P(fx0, 82, 2, 2, 0xFF4FA85C);
    P(fx0 - 1, 83, 1, 2, 0xFF5A3E2E);
    P(fx0 - Md(Math.Floor(t/200), 2), 84, 1, 1, 0xFF23263A);
    P(fx0 + 1 + Md(Math.Floor(t/200), 2), 84, 1, 1, 0xFF23263A);
    cx0 = 40 + Rn(10*Math.Sin(t*0.0012));
    cy0 = 46 + Md(Math.Floor(t/900), 2);
    P(cx0, cy0, 1, 1, 0xFFF1C9A2);
    P(cx0, cy0 + 1, 1, 1, 0xFFE8574A);
    P(cx0, cy0 + 2, 1, 1, 0xFF23263A);
    dx0 = 4 + Rn(Md(t*0.0012, 76));
    dst = Md(Math.Floor(t/260), 2);
    P(dx0, 90, 5, 2, 0xFFB8865A);
    P(dx0 + 5, 89, 2, 2, 0xFFB8865A);
    P(dx0 + 6, 87, 1, 2, 0xFF8A5A3A);
    P(dx0 + 5, 87, 1, 1, 0xFF8A5A3A);
    P(dx0 + dst, 92, 1, 1, 0xFF8A5A3A);
    P(dx0 + 4 - dst, 92, 1, 1, 0xFF8A5A3A);
    P(dx0 - 1, 90, 1, 1, 0xFFF4F1E8);
        for (int A1 = 1; A1 <= (int)(2); A1++) {
        k = A1;
        hp = Md(t*0.0011 + k*0.5, 1.0);
        hx0 = (B((k == 1)) ? 8 : 100) + Rn(hp*8);
        hy0 = (B((k == 1)) ? 66 : 40) - Rn(Math.Sin(hp*3.14159)*2);
        P(hx0, hy0, 2, 1, 0xFFE9D5A8);
        P(hx0 + 1, hy0 - 1, 1, 1, 0xFFE9D5A8);
        P(hx0 - 1, hy0, 1, 1, 0xFFF4F1E8);
        }
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        pk = ((Md(t + k*300, 1100) < 550)) ? 1.0 : 0.0;
        cx0 = 8 + k*4 + Rn(Math.Sin(t*0.0005 + k)*2);
        cy0 = 36 + Md(k, 2);
        P(cx0, cy0, 2, 1, 0xFFF4F1E8);
        P(cx0 + 2, cy0 - (B(pk) ? 0 : 1), 1, 1, 0xFFF4F1E8);
        P(cx0 + 1, cy0 - 1, 1, 1, 0xFFE8574A);
        P(cx0 + 3, cy0 - (B(pk) ? 0 : 1), 1, 1, 0xFFE8A64A);
        }
        for (int A1 = 1; A1 <= (int)(2); A1++) {
        k = A1;
        bfx = 20 + k*50 + Rn(20*Math.Sin(t*0.0006 + k));
        bfy = 66 + Rn(6*Math.Sin(t*0.0017 + k*2));
        c = B(Md(Math.Floor(t/160) + k, 2)) ? (B((k == 1)) ? 0xFFFFE08A : 0xFFB9DCF2) : (B((k == 1)) ? 0xFFF2A24A : 0xFF7FB7E0);
        P(bfx - 1, bfy, 1, 1, c);
        P(bfx + 1, bfy, 1, 1, c);
        }
    lv = Tw.Level(3);
    if (B(lv >= 1)) {
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        q = A1;
        shx = 6 + q*8 + Rn(3*Math.Sin(t*0.0003 + q*2));
        shy = 12 + (q - 1)*4;
        nib = (Md(t + q*400, 1600) < 300);
        P(shx, shy, 4, 2, 0xFFF4F1E8);
        P(shx + 4, shy + (B(nib) ? 1 : 0), 1, 1, 0xFF2A2436);
        P(shx, shy + 2, 1, 1, 0xFF2A2436);
        P(shx + 3, shy + 2, 1, 1, 0xFF2A2436);
        }
    }
    if (B(lv >= 2)) {
    P(134, 82, 4, 6, 0xFFE0D8C8);
    P(133, 81, 6, 1, 0xFF9A2A2A);
    P(135, 85, 2, 2, 0xFF6A3A22);
    P(135, 83, 1, 1, 0xFF9FD3F0);
    wa = Math.Floor(Md(DecT(now)*0.03, 360)/15)*15*0.0174533;
        for (int A1 = 1; A1 <= (int)(4); A1++) {
        an = wa + (A1 - 1)*1.5708;
            for (int A2 = 1; A2 <= (int)(6); A2++) {
            P(136 + Rn(A2*Math.Cos(an)), 80 + Rn(A2*Math.Sin(an)), 1, 1, 0xFF5A3E2E);
            }
        P(136 + Rn(6*Math.Cos(an)) - (Math.Cos(an) < 0 ? 1 : 0), 80 + Rn(6*Math.Sin(an)), 2, 1, 0xFFE9D5A8);
        }
    P(135, 79, 2, 2, 0xFFE0D8C8);
    }
    if (B(lv >= 3)) {
    P(61, 41, 6, 6, 0xFFE8574A);
    P(60, 42, 8, 4, 0xFFE8574A);
    P(62, 40, 4, 8, 0xFFE8574A);
    P(62, 42, 4, 4, 0xFFFFE08A);
    P(61, 43, 6, 2, 0xFFFFE08A);
    P(63, 41, 2, 6, 0xFFFFE08A);
    ca = Math.Floor(Md(DecT(now)*0.05, 360)/22.5)*22.5*0.0174533;
        for (int A1 = 1; A1 <= (int)(4); A1++) {
        an = ca + (A1 - 1)*1.5708;
        hx0 = 64 + Rn(2.6*Math.Cos(an));
        hy0 = 44 + Rn(2.6*Math.Sin(an));
        P(hx0 - 1, hy0, 2, 1, B(Md(A1, 2)) ? 0xFFF4F1E8 : 0xFF8A6A4A);
        P(hx0, hy0 - 1, 1, 1, 0xFF3A2E24);
        }
    P(64, 44, 1, 1, 0xFFC44A4A);
    }
    if (B(lv >= 4)) {
    bx0 = Md(t*0.0016, 200) - 30;
    by0 = 30 + Rn(6*Math.Sin(t*0.0006));
    P(bx0 + 4, by0 + 30, 7, 3, Alpha(0xFF1E3A2A, 50));
    P(bx0 - 3, by0 - 2, 11, 7, 0xFFE8574A);
    P(bx0 - 2, by0 - 4, 9, 2, 0xFFE8574A);
    P(bx0 - 2, by0 + 5, 9, 2, 0xFFB83A2E);
    P(bx0, by0 - 3, 2, 9, 0xFFFFE08A);
    P(bx0 + 4, by0 - 3, 2, 9, 0xFFFFE08A);
    P(bx0 + 1, by0 + 8, 4, 2, 0xFF8A5A3A);
    P(bx0 + 1, by0 + 7, 1, 1, 0xFF5A3E2E);
    P(bx0 + 4, by0 + 7, 1, 1, 0xFF5A3E2E);
    }
    if (B(lv >= 5)) {
        for (int A1 = 1; A1 <= (int)(14); A1++) {
        q = A1;
        lx = 4 + (q - 1)*10;
        ly = 41 + Md(q, 2);
        P(lx, ly, 1, 1, 0xFF5A3E2E);
        P(lx - 1, ly + 1, 3, 2, B((Md(Math.Floor(t/300) + q, 4) == 0)) ? 0xFFFFE9A8 : (B(Md(q, 2)) ? 0xFFE8574A : 0xFFFFE08A));
        }
    fph = Md(t, 12000);
    if (B(fph < 3600)) {
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        q = A1;
        fp = (fph - q*700)/2400.0;
        if (B(fp < 0 || fp > 1)) {
        continue;
        }
        fcx = 60 + q*7;
        fcy = 44 + Md(q, 2)*6;
        fcl = B((q == 1)) ? 0xFFFF6A6A : (q == 2) ? 0xFFFFE08A : 0xFF8AB8FF;
            for (int A2 = 1; A2 <= (int)(10); A2++) {
            an = (A2 - 1)*0.6283;
            rr = Rn(fp*10);
            P(fcx + Rn(rr*Math.Cos(an)), fcy + Rn(rr*Math.Sin(an)*0.7), 1, 1, Alpha(fcl, Rn(230*(1 - fp))));
            }
        }
    }
    }
        for (int A1 = 1; A1 <= (int)(3); A1++) {
        k = A1;
        P(Md(t*0.03 + k*40, 170) - 15, 30 + k*14, 2, 1, Alpha(0xFF1E3A2A, 60));
        P(Md(t*0.03 + k*40, 170) - 14, 29 + k*14, 1, 1, Alpha(0xFF1E3A2A, 60));
        }

    }
}
