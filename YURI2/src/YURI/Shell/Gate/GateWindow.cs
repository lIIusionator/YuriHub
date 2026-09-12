using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Platform;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Shell.Gate;

public static class GateWindow
{
    /// <summary>LaunchGate(): shows the card; done receives the choice — "roblox", "hub", "acct" — or "" when the card was closed (the app exits).</summary>
    public static void Show(Action<string> done)
    {
        Pool.Load();
        LayeredWindow? win = null;
        var surface = new GateSurface(choice => { win?.Close(); done(choice); });
        win = new LayeredWindow(surface, "YURIGATE", topmost: true, toolWindow: true);
        win.Show();
        surface.Focus();
    }
}

public sealed class GateSurface : Surface
{
    const double CW2 = 560, CH2 = 312, PD = 26, BW2 = CW2 + PD * 2, BH2 = CH2 + PD * 2;
    const int INTRO_MS = 900, SEL_FADE = 460, SEL_HOLD = 3200;
    const uint C_ON = 0xFF34D399, C_BAD = 0xFFF04438, AMBER = 0xFFFBBF24;
    const string SCRSET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#$%&";
    readonly double cx = PD, cy = PD, bcxU = PD + CW2 - 24, btyU = PD + 22;
    readonly double hpx, hpy, hpw, hph, rx, fx, fy, fw, fh, fy2, fh2, bw1, thR = 12, thGap = 30, thY, thX0;
    readonly int gBase, gN, thShow, gSets;
    readonly Font fGateT, fGateS, fGateC;
    readonly Fonts.Scr scrReady, scrRbx, scrHub;
    readonly Action<string> _done;
    readonly long gIntro;
    long gOkAt, gCloseAt, gPressAt; int gPressZ; string choice = "";
    bool gDone;
    double gh2, ghE, gbE, gaE, gHovAv, gDragT, gTilt, gHndE;
    int selCur, selNext, selQ; long selFadeAt, selCycleAt;
    readonly Dictionary<int, double> ringW = new();
    int gRbx; long gRbxAt;
    readonly Random rnd = new();
    readonly double wZeal;

    public GateSurface(Action<string> done) : base(BW2, BH2, UiScale.Factor)
    {
        _done = done;
        hpx = cx + 24; hpy = cy + 24; hpw = 170; hph = CH2 - 48;
        rx = cx + 208;
        fx = rx; fy = cy + 144; fw = CW2 - 208 - 26; fh = 40;
        fy2 = fy + fh + 8; fh2 = 34; bw1 = (fw - 10) / 2;
        thY = cy + CH2 - 34; thX0 = rx + thR;
        gBase = Pool.HasRot ? Pool.Base : 0;
        gN = Pool.HasRot ? Pool.RotN : Math.Max(1, Pool.DispN);
        thShow = Math.Min(gN, 8);
        gSets = FfmComm.Entries.Sum(e => e.Sets.Count);
        fGateT = Fonts.New(22, true); fGateS = Fonts.New(9, true); fGateC = Fonts.New(7.5, true);
        scrReady = Fonts.BuildScr("READY", fGateT); scrRbx = Fonts.BuildScr("LAUNCHING", fGateT); scrHub = Fonts.BuildScr("OPENING", fGateT);
        wZeal = Fonts.MeasureW(AppInfo.AppName, Fonts.fBrand);
        gIntro = Clock.Tick;
        selCur = gBase + 1; selNext = selCur; selCycleAt = gIntro;
        Tim(Pace.ModalTick());
    }

    protected override int ZoneAt(double ux, double uy)
    {
        if ((ux - bcxU) * (ux - bcxU) + (uy - btyU) * (uy - btyU) <= 81) return 2;
        if (gOkAt == 0 && gCloseAt == 0)
        {
            if (uy >= fy && uy <= fy + fh && ux >= fx && ux <= fx + fw) return 7;
            if (uy >= fy2 && uy <= fy2 + fh2)
            {
                if (ux >= fx && ux <= fx + bw1) return 3;
                if (ux >= fx + bw1 + 10 && ux <= fx + fw) return 8;
            }
        }
        if (gN > 1)
            for (int i = 1; i <= thShow; i++)
            {
                double txc = thX0 + (i - 1) * thGap;
                if ((ux - txc) * (ux - txc) + (uy - thY) * (uy - thY) <= (thR + 2) * (thR + 2)) return 9 + i;
            }
        if (gN > 1 && ux >= hpx && ux <= hpx + hpw && uy >= hpy && uy <= hpy + hph) return 6;
        if (ux >= rx + 62 && ux <= rx + 98 && uy >= cy + 10 && uy <= cy + 34) return 5;
        if (ux >= cx && ux <= cx + CW2 && uy >= cy && uy <= cy + CH2) return 4;
        return 0;
    }
    protected override void OnZoneDown(int z, PointerPressedEventArgs e)
    {
        if (z == 2) GClose();
        else if (z == 7) GGo("roblox", 7);
        else if (z == 3) GGo("hub", 3);
        else if (z == 8) GGo("acct", 8);
        else if (z == 6) GSelSwitch(gBase + (selCur - gBase) % gN + 1);
        else if (z >= 10) GSelSwitch(gBase + z - 9);
        else if (z == 5) BeginDrag(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Return || e.Key == Key.Enter) { GGo("hub", 3); e.Handled = true; }
        else if (e.Key == Key.Escape) { GClose(); e.Handled = true; }
    }
    void GGo(string which, int z)
    {
        if (gOkAt != 0 || gCloseAt != 0 || gDone) return;
        choice = which; gPressAt = Clock.Tick; gPressZ = z; gOkAt = Clock.Tick;
    }
    void GClose() { if (gCloseAt != 0 || gOkAt != 0) return; gCloseAt = Clock.Tick; }
    void GSelSwitch(int idx)
    {
        selCycleAt = Clock.Tick;
        if (selFadeAt != 0)
        {
            if (idx == selNext) return;
            if (idx == selCur)
            {
                double e0 = Ease3(Clamp((Clock.Tick - selFadeAt) / (double)SEL_FADE, 0.0, 1.0));
                (selCur, selNext) = (selNext, selCur);
                selFadeAt = Clock.Tick - (long)Math.Round(Ease3Inv(1 - e0) * SEL_FADE);
                selQ = 0;
                return;
            }
            selQ = idx;
            return;
        }
        if (idx == selCur) return;
        selQ = 0;
        selNext = idx; selFadeAt = Clock.Tick;
    }
    void GFinish(string result)
    {
        if (gDone) return;
        gDone = true;
        Stop();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _done(result));
    }
    static void SoftGlow(double cx, double cy, double rx, double ry, uint col, double aTotal, double f = 1.0, int n = 6)
    {
        if (HubState.LowPerf) return;
        for (int i = 1; i <= n; i++)
        {
            double sc = 1 - (i - 1) / (double)n;
            FillEll(cx - rx * sc, cy - ry * sc, rx * sc * 2, ry * sc * 2, SBrush(FA(Alpha(col, R(aTotal / n)), f)));
        }
    }

    void GBtn(int z, double bx, double by, double bw, double bh, string label, bool primary, double hv, double pr, double a, long now, uint acc, string glyph = "")
    {
        double lift = -2.0 * hv + 2.4 * pr;
        int st = PushXform(bx + bw / 2, by + bh / 2 + lift, 1 - 0.025 * pr, 0);
        double x0 = bx, y0 = by + lift;
        double brth = (Math.Sin(DecT(now) * 0.0021) + 1) / 2;
        if (pr > 0.01)
        {
            double rp = 1 - pr, gr = 6 + 26 * Ease3(rp);
            StrokeRR(x0 - gr, y0 - gr, bw + gr * 2, bh + gr * 2, 10 + gr, Pen(FA(Alpha(AccHi(acc, 0.45), R(150 * pr)), a), 1.6 * pr + 0.4));
        }
        uint tc, gc;
        if (primary)
        {
            SoftGlow(x0 + bw / 2, y0 + bh / 2, bw * 0.72, bh * 1.25, acc, R((70 + 30 * brth + 60 * hv) * a), 1.0, 6);
            if (hv > 0.01) FillRR(x0 - 6, y0 - 5, bw + 12, bh + 10, 14, SBrush(FA(Alpha(AccHi(acc, 0.4), R(34 * hv)), a)));
            ShadowDraw(x0, y0 + 2, bw, bh, 10, 4, 26, 14, a * (0.55 + 0.45 * hv));
            FillRR(x0, y0, bw, bh, 10, VBrush(x0, y0, bw, bh, FA(Alpha(AccHi(acc, 0.5), R(198 + 40 * hv)), a), FA(Alpha(acc, R(140 + 40 * hv)), a)));
            FillRR(x0 + 6, y0 + 1, bw - 12, bh * 0.42, 8, VBrush(x0 + 6, y0 + 1, bw - 12, bh * 0.42, FA(Alpha(0xFFFFFF, R(52 + 26 * hv)), a), FA(Alpha(0xFFFFFF, 0), a)));
            double shp = (hv > 0.02 && hv < 0.995) ? hv : (DecT(now) % 3400) / 3400.0;
            double shA = (hv > 0.02 && hv < 0.995) ? 46 * Math.Sin(hv * Math.PI) : 26 * Math.Max(0.0, Math.Sin(shp * Math.PI));
            if (shA > 0.5)
            {
                double sx2 = x0 - 30 + (bw + 60) * shp;
                int stS = PushG(); ClipRR(x0, y0, bw, bh, 10);
                FillRect(sx2 - 16, y0, 32, bh, HBrush(sx2 - 16, y0, 32, bh, FA(Alpha(0xFFFFFF, 0), a), FA(Alpha(0xFFFFFF, R(shA)), a)));
                Pop(stS);
            }
            StrokeRR(x0, y0, bw, bh, 10, Pen(FA(Alpha(AccHi(acc, 0.6), R(215 + 40 * hv)), a), 1.3));
            tc = FA(0xFFFFFFFF, a); gc = FA(0xFFFFFFFF, a);
        }
        else
        {
            if (hv > 0.01) FillRR(x0 - 4, y0 - 3, bw + 8, bh + 6, 12, SBrush(FA(Alpha(acc, R(22 * hv)), a)));
            FillRR(x0, y0, bw, bh, 10, VBrush(x0, y0, bw, bh, FA(Alpha(acc, R(40 + 26 * hv)), a), FA(Alpha(acc, R(16 + 14 * hv)), a)));
            FillRR(x0 + 6, y0 + 1, bw - 12, bh * 0.4, 8, VBrush(x0 + 6, y0 + 1, bw - 12, bh * 0.4, FA(Alpha(0xFFFFFF, R(16 + 14 * hv)), a), FA(Alpha(0xFFFFFF, 0), a)));
            StrokeRR(x0, y0, bw, bh, 10, Pen(FA(Alpha(AccHi(acc, 0.3), R(150 + 90 * hv)), a), 1.2));
            if (hv > 0.02) { double uw = (bw - 24) * Ease3(hv); FillRR(x0 + bw / 2 - uw / 2, y0 + bh - 4, uw, 2, 1, SBrush(FA(Alpha(AccHi(acc, 0.5), R(200 * hv)), a))); }
            tc = FA(Alpha(Mix(0xFFE8EAF6, 0xFFFFFFFF, hv), R(225 + 30 * hv)), a);
            gc = FA(Alpha(AccHi(acc, 0.55), R(230 + 25 * hv)), a);
        }
        double lw = Fonts.MeasureW(label, Fonts.fBadge);
        double gw2 = glyph != "" ? 20 : 0;
        double tx0 = x0 + (bw - lw - gw2) / 2;
        double gcx = tx0 + 7, gcy = y0 + bh / 2 - (0.6 * hv - 0.8 * pr);
        if (glyph == "dart") HubDart(gcx + 1.5 * hv, gcy, 6.2, gc);
        else if (glyph == "acct")
        {
            FillEll(gcx - 5.5, gcy - 6.5, 6, 6, SBrush(gc)); FillRR(gcx - 8.5, gcy + 0.5, 12, 6, 3, SBrush(gc));
            var pn = Pen(gc, 1.3); Ell(gcx + 2, gcy - 5.5, 5, 5, pn); Arc(gcx - 1, gcy + 0.5, 11, 9, 200, 140, pn);
        }
        else if (glyph == "hub")
        {
            var pn = Pen(gc, 1.5);
            for (int k = 0; k < 4; k++)
            {
                double qx = gcx - 6 + (k % 2) * 7, qy = gcy - 6 + (k / 2) * 7;
                if (k == 0) FillRR(qx, qy, 5, 5, 1.5, SBrush(gc)); else StrokeRR(qx, qy, 5, 5, 1.5, pn);
            }
        }
        TxtP(label, tx0 + gw2, y0 - 1 - (0.6 * hv - 0.8 * pr), lw + 6, bh, Fonts.fBadge, tc, Fmt.L);
        Pop(st);
    }
    double GChip(double x, double y, string vs, string label, uint col, double a)
    {
        double vw2 = Fonts.MeasureW(vs, Fonts.fBadge), lw2 = Fonts.MeasureW(label, fGateC);
        double cw = 10 + vw2 + 6 + lw2 + 10;
        FillRR(x, y, cw, 17, 8.5, VBrush(x, y, cw, 17, FA(Alpha(col, 46), a), FA(Alpha(col, 22), a)));
        StrokeRR(x, y, cw, 17, 8.5, Pen(FA(Alpha(col, 120), a), 1));
        Txt(vs, x + 10, y, vw2 + 4, 17, Fonts.fBadge, FA(Alpha(AccHi(col, 0.35), 240), a), Fmt.L);
        Txt(label, x + 10 + vw2 + 6, y + 1, lw2 + 4, 16, fGateC, FA(Alpha(0xFFC7CBE0, 170), a), Fmt.L);
        return x + cw;
    }

    protected override void Frame(long now)
    {
        double it = Math.Min((now - gIntro) / (double)INTRO_MS, 1.0);
        double ei = 1 - Math.Pow(1 - it, 3);
        double winA = Math.Min(it * 1.7, 1.0);
        double yO = (1 - ei) * 30;
        double okT = gOkAt != 0 ? Ease3((now - gOkAt) / 380.0) : 0.0;
        uint curG = AccSat(Mix(HubState.Accent, C_ON, okT));
        double embG = (Math.Sin(now * 0.0016) + 1) / 2, emb2G = (Math.Sin(now * 0.004) + 1) / 2;
        double hb = DecT(now) % 2200;
        int blink = (hb < 140 || (hb > 300 && hb < 440)) ? 1 : 0;
        double scl = 1.0;
        if (gOkAt != 0)
        {
            if (now - gOkAt > 520)
            {
                double ft = Math.Min((now - gOkAt - 520) / 330.0, 1.0);
                winA *= 1 - ft; scl = 1 - 0.06 * ft; yO += 10 * ft;
                if (ft >= 1) { GFinish(choice); return; }
            }
            else scl = 1 + 0.02 * Math.Sin(Math.PI * Math.Min((now - gOkAt) / 260.0, 1.0));
        }
        if (gCloseAt != 0)
        {
            double ft = Math.Min((now - gCloseAt) / 300.0, 1.0);
            winA *= 1 - ft; scl = 1 - 0.05 * ft;
            if (ft >= 1) { GFinish(""); return; }
        }
        if (gOkAt == 0 && gCloseAt == 0) scl *= 1 + 0.004 * Math.Sin(DecT(now) * 0.0012);
        scl *= 1 + 0.02 * gDragT;
        gDragT += ((Dragging ? 1.0 : 0.0) - gDragT) * EK(0.2);
        if (gDragT < 0.004 && !Dragging) gDragT = 0.0;
        double velX = DragVelX();
        gTilt += ((Dragging ? Clamp(velX * 0.22, -3.0, 3.0) : 0.0) - gTilt) * EK(0.18);
        int hz = (Dragging || gCloseAt != 0 || gOkAt != 0) ? 0 : ZoneCursor();
        int hzT = hz >= 10 ? hz - 9 : 0;
        gHndE += ((hz == 5 ? 1.0 : 0.0) - gHndE) * EK(0.2); if (gHndE < 0.004 && hz != 5) gHndE = 0.0;
        gh2 += ((hz == 2 ? 1.0 : 0.0) - gh2) * EK(0.2); if (gh2 < 0.004 && hz != 2) gh2 = 0.0;
        ghE += ((hz == 3 ? 1.0 : 0.0) - ghE) * EK(0.2); if (ghE < 0.004 && hz != 3) ghE = 0.0;
        gaE += ((hz == 8 ? 1.0 : 0.0) - gaE) * EK(0.2); if (gaE < 0.004 && hz != 8) gaE = 0.0;
        gbE += ((hz == 7 ? 1.0 : 0.0) - gbE) * EK(0.2); if (gbE < 0.004 && hz != 7) gbE = 0.0;
        if (selFadeAt != 0 && now - selFadeAt >= SEL_FADE) { selCur = selNext; selFadeAt = 0; selCycleAt = now; }
        if (selFadeAt == 0 && selQ != 0) { if (selQ != selCur) { selNext = selQ; selFadeAt = now; selCycleAt = now; } selQ = 0; }
        if (gN > 1 && selFadeAt == 0 && gOkAt == 0 && gCloseAt == 0 && now - selCycleAt >= SEL_HOLD) { selNext = gBase + (selCur - gBase) % gN + 1; selFadeAt = now; }
        double selFT = selFadeAt != 0 ? Clamp(Ease3((now - selFadeAt) / (double)SEL_FADE), 0.0, 1.0) : 1.0;
        gHovAv += ((hz == 6 ? 1.0 : 0.0) - gHovAv) * EK(0.2); if (gHovAv < 0.004 && hz != 6) gHovAv = 0.0;
        long ims = now - gIntro;
        bool stg = ims < 700;
        double s1 = stg ? Ease3((ims - 60) / 340.0) : 1.0, s2 = stg ? Ease3((ims - 140) / 340.0) : 1.0, s3 = stg ? Ease3((ims - 220) / 340.0) : 1.0;
        double hdy = -(1 - s1) * 6, mdy = (1 - s2) * 10, fdy2 = (1 - s3) * 8;

        int win = PushOpacity(winA);
        PushShift(0, yO);
        int st0 = PushXform(cx + CW2 / 2, cy + CH2 / 2, scl, gTilt);
        ShadowDraw(cx, cy + 3 + 2.5 * gDragT, CW2, CH2, 16, 10, 8, 8, 1.0);
        FillRR(cx, cy, CW2, CH2, 16, BgV(cx, cy, CW2, CH2, 0xF2222438, 0xF8121423));
        int stC = PushG(); ClipRR(cx, cy, CW2, CH2, 16);
        PanelBackdrop(cx, cy, CW2, CH2, HubState.Accent, s1, now, 0.95);
        double aur = (Math.Sin(DecT(now) * 0.00052) + 1) / 2;
        SoftGlow(rx + 150 + 24 * Math.Sin(DecT(now) * 0.00041), cy + 52 + 12 * Math.Cos(DecT(now) * 0.00037), 250, 110, curG, R((52 + 22 * aur) * s1), 1.0, 7);
        SoftGlow(rx + 90 + 30 * Math.Cos(DecT(now) * 0.00033), cy + CH2 - 60 + 10 * Math.Sin(DecT(now) * 0.00045), 210, 80, AccHi(curG, 0.4), R((30 + 14 * (1 - aur)) * s1), 1.0, 6);
        SoftGlow(cx + 60, cy + CH2 - 30, 160, 70, curG, R(26 * s1), 1.0, 5);
        FillRect(cx, cy, CW2, CH2, LineBrush(cx - 1, cy - 1, CW2 + 2, CH2 + 2, 0x0EFFFFFF, 0x00FFFFFF, 2));
        for (int k = 1; k <= 12; k++)
        {
            double cyc = 5200 + k * 337, prt = ((now + k * 997) % (long)cyc) / cyc;
            double mx_ = cx + 34 + (k * 167) % (CW2 - 68) + 9 * Math.Sin(DecT(now) * 0.0009 + k * 1.7), my_ = cy + CH2 - 10 - prt * (CH2 - 20);
            double mr_ = 0.9 + (k % 3) * 0.5;
            FillEll(mx_ - mr_, my_ - mr_, mr_ * 2, mr_ * 2, SBrush(FA(Alpha(AccHi(curG, 0.35), 44 * Math.Sin(Math.PI * prt)), s1)));
        }
        double per = gOkAt != 0 ? 3000.0 : 4600.0;
        double swp = (DecT(now) % per) / per, swx = cx - 26 + (CW2 + 52) * swp;
        uint c0s = (gOkAt != 0 ? AccHi(curG, 0.3) : curG) & 0xFFFFFF, c1s = Alpha(c0s, 34 + 20 * okT);
        FillRect(swx - 24, cy, 24, CH2, LineBrush(swx - 25, cy - 2, 26, CH2 + 4, c0s, c1s, 0));
        FillRect(swx, cy, 24, CH2, LineBrush(swx - 1, cy - 2, 26, CH2 + 4, c1s, c0s, 0));
        for (int k = 1; k <= 9; k++)
        {
            double cyc = 5600 + k * 373, prt = ((now + k * 911) % (long)cyc) / cyc;
            double mx_ = cx + 40 + (k * 167) % (CW2 - 80) + 10 * Math.Sin(DecT(now) * 0.0008 + k * 1.7), my_ = cy + CH2 - 12 - prt * (CH2 - 24);
            if (k % 3 == 0) MiniHeart(mx_, my_, 3 + (k % 2) * 1.4, Alpha(AccHi(curG, 0.35), R(120 * Math.Sin(Math.PI * prt) * s1)));
            else { double mr_ = 0.9 + (k % 3) * 0.5; FillEll(mx_ - mr_, my_ - mr_, mr_ * 2, mr_ * 2, SBrush(Alpha(AccHi(curG, 0.35), R(46 * Math.Sin(Math.PI * prt) * s1)))); }
        }
        FillRect(cx, cy, 14, CH2, SBrush(Alpha(curG, Math.Min(255, 46 + 16 * embG + 50 * okT))));
        FillRect(cx, cy, 4.5, CH2, VBrush(cx, cy, 4.5, CH2, Alpha(curG, 245), Alpha(curG, 110 + 40 * embG)));
        Pop(stC);
        StrokeRR(cx, cy, CW2, CH2, 16, Pen(0x28FFFFFF, 1));
        Line(cx + 18, cy + 1.5, cx + CW2 - 18, cy + 1.5, Pen(0x16FFFFFF, 1));
        Line(cx + 18, cy + CH2 - 1.5, cx + CW2 - 18, cy + CH2 - 1.5, Pen(0x30000000, 1));
        var pnD = Pen(Alpha(curG, 20 + 10 * emb2G + 46 * gDragT), 1); PenDash(pnD, 1); PenDashOff(pnD, (DecT(now) * 0.02) % 1000);
        StrokeRR(cx + 6, cy + 6, CW2 - 12, CH2 - 12, 12, pnD);
        CornerFlourish(cx, cy, CW2, CH2, curG, s1);

        // ---- the hero panel ----
        double hx_ = hpx - (1 - s1) * 16;
        FillRR(hx_, hpy, hpw, hph, 14, SBrush(FA(0xFF12141F, s1)));
        if (gN < 1) MiniBackdrop(hx_, hpy, hpw, hph, 14, curG, s1, now, 0.8);
        int stH = PushG(); ClipRR(hx_, hpy, hpw, hph, 14);
        if (gN >= 1)
        {
            Img.FitRR(Pool.SelAt(selCur), hx_, hpy, hpw, hph, 0, s1, 1.0, 0.5, 0.3);
            if (selNext != selCur && selFT < 1) Img.FitRR(Pool.SelAt(selNext), hx_, hpy, hpw, hph, 0, s1 * selFT, 1.0, 0.5, 0.3);
        }
        else Txt("Z", hx_, hpy, hpw, hph, fGateT, FA(Alpha(curG, 200), s1), Fmt.C);
        double sly = hpy - 16 + (DecT(now) % 3600) / 3600.0 * (hph + 32);
        FillRect(hx_, sly - 12, hpw, 12, VBrush(hx_, sly - 12, hpw, 12, Alpha(0xFFFFFF, 0), Alpha(0xFFFFFF, 24)));
        Line(hx_, sly, hx_ + hpw, sly, Pen(FA(Alpha(AccHi(curG, 0.5), 60), s1), 1));
        FillRect(hx_, hpy + hph - 58, hpw, 58, VBrush(hx_, hpy + hph - 58, hpw, 58, 0x00060810, 0xE8060810));
        Txt("GALLERY", hx_ + 10, hpy + hph - 31, 120, 15, fGateS, FA(Alpha(AccHi(curG, 0.75), 240), s1), Fmt.L);
        Line(hx_ + 10, hpy + hph - 8, hx_ + 38, hpy + hph - 8, Pen(FA(Alpha(curG, 90 + 70 * (Math.Sin(DecT(now) * 0.0035) + 1) / 2), s1), 1.4));
        if (gN >= 1) Txt((selCur - gBase) + "/" + gN, hx_, hpy + hph - 31, hpw - 30, 15, fGateS, FA(Alpha(curG, 225), s1), Fmt.R);
        if (gHovAv > 0.01 && gN > 1)
        {
            double he = gHovAv;
            FillRect(hx_, hpy, hpw, hph, SBrush(Alpha(0x05060C, 130 * he)));
            double hyc = hpy + hph / 2 - 10 + (1 - he) * 6;
            var pnN = Pen(Alpha(0xFFFFFF, 225 * he), 2.2);
            Line(hx_ + hpw / 2 - 4, hyc - 9, hx_ + hpw / 2 + 5, hyc, pnN); Line(hx_ + hpw / 2 + 5, hyc, hx_ + hpw / 2 - 4, hyc + 9, pnN);
            Txt("NEXT", hx_, hyc + 15, hpw, 14, Fonts.fBadge, Alpha(0xFFFFFF, 240 * he), Fmt.C);
        }
        if (gOkAt != 0 && okT < 1) FillRect(hx_, hpy, hpw, hph, SBrush(Alpha(0xFFFFFF, 70 * (1 - okT))));
        Pop(stH);
        var pnAr = Pen(FA(Alpha(AccHi(curG, 0.35), 120 + 70 * emb2G), s1), 1.6);
        Arc(hx_ - 8, hpy - 8, 26, 26, 175, 100, pnAr); Arc(hx_ + hpw - 18, hpy + hph - 18, 26, 26, 355, 100, pnAr);
        double daH = (185 + 78 * (0.5 + 0.5 * Math.Sin(DecT(now) * 0.0024))) * 0.0174533;
        FillEll(hx_ + 5 + 13 * Math.Cos(daH) - 1.5, hpy + 5 + 13 * Math.Sin(daH) - 1.5, 3, 3, SBrush(FA(Alpha(AccHi(curG, 0.55), 210), s1)));
        StrokeRR(hx_ + 1, hpy + 1, hpw - 2, hph - 2, 13, Pen(FA(Alpha(0x000000, 110), s1), 1));
        StrokeRR(hx_, hpy, hpw, hph, 14, Pen(FA(Alpha(Mix(0xFF2A2C42, curG, 0.45), 235), s1), 2));
        // the shield badge
        double bdx = hx_ + hpw - 8, bdy = hpy + hph - 8;
        double okScl = 1 + 0.10 * Math.Sin(Math.PI * Math.Min(okT * 1.3, 1.0));
        int stB = PushXform(bdx, bdy, okScl, 0);
        FillEll(bdx - 13, bdy - 13, 26, 26, SBrush(FA(Alpha(0x0B0C14, 235), s1)));
        var sp = ShieldPath(bdx - 8, bdy - 9, 16, 19);
        FillPath(sp, SBrush(FA(Alpha(curG, Math.Min(255, 150 + 40 * emb2G + 90 * okT)), s1)));
        StrokePath(sp, Pen(FA(Alpha(AccHi(curG, 0.35), 235), s1), 1.4));
        if (okT < 0.5)
        {
            FillRR(bdx - 3.4, bdy - 1, 6.8, 6.2, 1.6, SBrush(FA(Alpha(0x05060C, 235), s1)));
            Arc(bdx - 2.6, bdy - 5.4, 5.2, 5.2, 180, 180, Pen(FA(Alpha(0x05060C, 235), s1), 1.4));
        }
        else { var pnT = Pen(FA(Alpha(0x05060C, 245), s1), 1.8); Line(bdx - 3.4, bdy + 0.6, bdx - 1, bdy + 3, pnT); Line(bdx - 1, bdy + 3, bdx + 3.6, bdy - 3, pnT); }
        Pop(stB);
        if (gOkAt != 0)
        {
            double gsp = Math.Min((now - gOkAt) / 560.0, 1.0);
            if (gsp < 1)
            {
                double e = Ease3(gsp);
                for (int k = 1; k <= 8; k++)
                {
                    double a_ = (k * 45 + 22 * e) * 0.0174533, rr_ = 9 + e * 30, sr = 2.2 * (1 - e) + 0.4;
                    FillEll(bdx + rr_ * Math.Cos(a_) - sr, bdy + rr_ * Math.Sin(a_) - sr, sr * 2, sr * 2, SBrush(Alpha(AccHi(curG, 0.4), 220 * (1 - e))));
                }
                double r_ = 13 + e * 26;
                Ell(bdx - r_, bdy - r_, r_ * 2, r_ * 2, Pen(Alpha(curG, 170 * (1 - e)), 2.2 * (1 - e) + 0.4));
            }
        }
        // ---- close, brand, grip, signal, clock ----
        double ec = 1 + 0.28 * gh2, ycb = btyU + hdy - 1.2 * gh2;
        uint gAcc = HubState.Accent;
        FillEll(bcxU - 7 * ec, ycb - 7 * ec, 14 * ec, 14 * ec, SBrush(FA(Alpha(gAcc, 36 + 60 * gh2), s1)));
        Ell(bcxU - 7 * ec, ycb - 7 * ec, 14 * ec, 14 * ec, Pen(FA(Alpha(gAcc, 100 + 80 * gh2), s1), 1));
        var pnX = Pen(FA(Alpha(AccHi(gAcc, 0.45), 210 + 45 * gh2), s1), 1.5);
        Line(bcxU - 3.2 * ec, ycb - 3.2 * ec, bcxU + 3.2 * ec, ycb + 3.2 * ec, pnX); Line(bcxU - 3.2 * ec, ycb + 3.2 * ec, bcxU + 3.2 * ec, ycb - 3.2 * ec, pnX);
        Txt(AppInfo.AppName, rx, cy + 12 + hdy, 90, 20, Fonts.fBrand, FA(0xCFE8EAF6, s1), Fmt.L);
        FillEll(rx + wZeal + 6, cy + 19.5 + hdy, 5, 5, SBrush(FA(Alpha(curG, gOkAt != 0 ? 240 : Math.Min(255, 150 + 35 * emb2G + 48 * blink)), s1)));
        double hnA = Math.Max(gHndE, gDragT);
        if (hnA > 0.01)
        {
            FillRR(rx + 63, cy + 11 + hdy, 34, 22, 6, SBrush(FA(Alpha(0xFFFFFF, 24 * hnA), s1)));
            StrokeRR(rx + 63, cy + 11 + hdy, 34, 22, 6, Pen(FA(Alpha(curG, 90 * hnA), s1), 1));
        }
        var bG = SBrush(FA(Alpha(AccHi(curG, 1 - 0.5 * hnA), 46 + 70 * hnA), s1));
        for (int k = 0; k < 6; k++) FillEll(rx + 71.8 + (k % 3) * 7, cy + 18 + (k / 3) * 5.6 + hdy, 2.4, 2.4, bG);
        int sbn = 1 + (int)((now / 520) % 4);
        for (int k = 1; k <= 4; k++) { double bh_ = 3 + k * 2.4; bool on_ = k <= sbn; FillRR(rx + 126 + (k - 1) * 6, cy + 26 + hdy - bh_, 3, bh_, 1.2, SBrush(FA(Alpha(on_ ? curG : 0xFFFFFF, on_ ? 200 : 42), s1))); }
        double ckx = cx + CW2 - 44;
        var pnCk = Pen(FA(Alpha(curG, 170), s1), 1.6);
        Arc(ckx - 86, cy + 15 + hdy, 10, 10, (DecT(now) * 0.22) % 360, 100, pnCk); Arc(ckx - 86, cy + 15 + hdy, 10, 10, (DecT(now) * 0.22) % 360 + 180, 40, pnCk);
        Txt(Clock.ClockStr(), ckx - 70, cy + 13 + hdy, 70, 14, Fonts.fBadge, FA(0x86C7CBE0, s1), Fmt.R);
        // ---- the title ----
        double sy = cy + 52 + mdy;
        var scr = gOkAt != 0 ? (choice == "roblox" ? scrRbx : scrHub) : scrReady;
        long scrAt = gOkAt != 0 ? gOkAt : gIntro;
        SoftGlow(rx + scr.W / 2, sy + 16, scr.W * 0.85 + 30, 30, curG, R(70 * s2), 1.0, 6);
        double rlw = scr.W + 26;
        FillRR(rx, sy + 34, rlw, 2, 1, HBrush(rx, sy + 34, rlw, 2, FA(Alpha(curG, R(200 + 40 * embG)), s2), FA(Alpha(curG, 0), s2)));
        double rhx = rx + (DecT(now) * 0.09) % (rlw + 24) - 12;
        FillRR(rhx, sy + 33.5, 14, 3, 1.5, SBrush(FA(Alpha(AccHi(curG, 0.6), 220), s2)));
        if (now < scrAt + 130 + scr.Chs.Count * 42)
        {
            for (int i = 1; i <= scr.Chs.Count; i++)
            {
                string ch; uint cc;
                if (now < scrAt + 90 + i * 42) { ch = SCRSET[rnd.Next(SCRSET.Length)].ToString(); cc = Alpha(Mix(curG, 0xFFE8EAF6, 0.55), 205); }
                else { ch = scr.Chs[i - 1]; cc = Alpha(curG, 245); }
                Txt(ch, rx + scr.Xs[i - 1], sy, 46, 30, fGateT, FA(cc, s2), Fmt.L);
            }
        }
        else Txt(gOkAt != 0 ? (choice == "roblox" ? "LAUNCHING" : "OPENING") : "READY", rx, sy, 240, 30, fGateT, FA(Alpha(curG, 245), s2), Fmt.L);
        for (int k = 0; k < 3; k++)
        {
            double x0 = rx + scr.W + 12 + k * 7;
            double ca = 50 + 85 * Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.0025 - k * 0.9)), 3) + 120 * okT * Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.005 - k * 0.9)), 3);
            var pnC = Pen(FA(Alpha(Mix(0xFFE8EAF6, curG, 0.35 + 0.3 * okT), Math.Min(255, ca)), s2), 1.8);
            Line(x0, sy + 15 - 3.8, x0 + 3.6, sy + 15, pnC); Line(x0 + 3.6, sy + 15, x0, sy + 15 + 3.8, pnC);
        }
        // ---- the vitals card ----
        double vx = fx + fw - 128, vy = cy + 46 + mdy, vw = 128, vh = 38;
        FillRR(vx, vy, vw, vh, 10, SBrush(FA(Alpha(0xFFFFFF, 8), s2)));
        MiniBackdrop(vx, vy, vw, vh, 10, curG, s2, now, 0.7);
        StrokeRR(vx, vy, vw, vh, 10, Pen(FA(Alpha(curG, 110 + 40 * emb2G), s2), 1));
        double vph = (DecT(now) % 1400) / 1400.0;
        double vth = Math.Pow(Math.Max(0.0, Math.Sin(vph * 6.283)), 8) + 0.55 * Math.Pow(Math.Max(0.0, Math.Sin(vph * 6.283 - 1.05)), 8);
        MiniHeart(vx + 17, vy + vh / 2 - 1, 5.2 + 1.6 * vth, FA(Alpha(AccHi(curG, 0.3), 225), s2));
        if (vth > 0.05) Ell(vx + 17 - 8 - 5 * (1 - vth), vy + vh / 2 - 9 - 5 * (1 - vth), 16 + 10 * (1 - vth), 16 + 10 * (1 - vth), Pen(FA(Alpha(curG, 110 * vth), s2), 1.1));
        double vex0 = vx + 32, vex1 = vx + vw - 10, vey = vy + vh / 2;
        double vpx = vex0 + 5 + (DecT(now) * 0.05) % (vex1 - vex0 - 10);
        var pnE = Pen(FA(Alpha(AccHi(curG, 0.3), 170), s2), 1.2);
        if (vpx > vex0 + 8 && vpx < vex1 - 8)
        {
            Line(vex0, vey, vpx - 7, vey, pnE); Line(vpx - 7, vey, vpx - 3.5, vey - 6, pnE); Line(vpx - 3.5, vey - 6, vpx, vey + 7, pnE);
            Line(vpx, vey + 7, vpx + 3.5, vey - 3, pnE); Line(vpx + 3.5, vey - 3, vpx + 7, vey, pnE); Line(vpx + 7, vey, vex1, vey, pnE);
        }
        else Line(vex0, vey, vex1, vey, pnE);
        Txt(gOkAt != 0 ? "WELCOME" : "LOADED", vx + 30, vy + 2, vw - 38, 11, fGateC, FA(Alpha(Mix(curG, 0xFFC7CBE0, 0.3), 215), s2), Fmt.L);
        if (now - gRbxAt > 2000 && !Dragging) { gRbxAt = now; gRbx = Roblox.IsRunning() ? 2 : Ffm.AppSettingsPaths().Count > 0 ? 1 : 0; }
        // ---- chips ----
        double chy = cy + 90 + mdy;
        if (gOkAt == 0)
        {
            double chx = rx;
            chx = GChip(chx, chy, FfmCommView.Num(FfmViews.Db.Count), "FLAGS", curG, s2);
            chx = GChip(chx + 8, chy, gSets.ToString(), "SETS", AccHi(curG, 0.3), s2);
            GChip(chx + 8, chy, gRbx == 2 ? "RUNNING" : gRbx == 1 ? "INSTALLED" : "NOT FOUND", "ROBLOX", gRbx != 0 ? C_ON : AMBER, s2);
        }
        else
        {
            string htxt = choice == "roblox" ? "starting roblox, then the hub" : choice == "acct" ? "opening the hub, on your accounts" : "opening the hub";
            Txt(htxt, rx, chy, 320, 16, Fonts.fHint, FA(Alpha(curG, 215), s2), Fmt.L);
        }
        // ---- the pulse strip ----
        double cpy = cy + 119 + mdy, cpw = fw;
        FadeLine(rx, rx + cpw, cpy + 9, FA(Alpha(0xFFFFFF, 22), s2), 1);
        for (int ck = 1; ck <= 26; ck++)
        {
            double cpx = rx + (ck - 1) * (cpw - 2) / 25;
            double ph2 = now * 0.0024 + ck * 0.55;
            double hgt = 3 + 7 * (Math.Sin(ph2) + 1) / 2;
            int al2 = R(56 + 120 * (Math.Sin(ph2 * 0.7 + 1.2) + 1) / 2);
            FillRR(cpx, cpy + 9 - hgt / 2, 2, hgt, 1, SBrush(FA(Alpha(curG, al2), s2)));
        }
        double rh = (DecT(now) * 0.16) % (cpw + 40) - 20;
        if (rh > -12 && rh < cpw + 12)
        {
            FillEll(rx + rh - 9, cpy + 9 - 9, 18, 18, SBrush(FA(Alpha(curG, 40), s2)));
            Line(rx + rh, cpy + 1, rx + rh, cpy + 17, Pen(FA(Alpha(AccHi(curG, 0.5), 190), s2), 1.4));
        }
        var pnR = Pen(FA(Alpha(curG, 90), s2), 1.2);
        Line(rx + 1, cpy + 3, rx + 1, cpy + 15, pnR); Line(rx + cpw - 1, cpy + 3, rx + cpw - 1, cpy + 15, pnR);
        FillEll(rx + cpw - 3.5, cpy + 6.5, 5, 5, SBrush(FA(Alpha(curG, R(120 + 90 * Math.Abs(Math.Sin(DecT(now) * 0.0026)))), s2)));
        FadeLine(rx, rx + fw, cy + 108, 0x24FFFFFF, s3);
        // ---- the buttons ----
        double fyy = fy + fdy2;
        double pr = gPressAt != 0 ? Clamp(1 - (now - gPressAt) / 320.0, 0.0, 1.0) : 0.0;
        double oth = gOkAt != 0 ? Clamp(1 - (now - gOkAt) / 260.0, 0.0, 1.0) : 1.0;
        double dim = 0.35 + 0.65 * oth;
        GBtn(7, fx, fyy, fw, fh, "OPEN ROBLOX", true, gbE, gPressZ == 7 ? pr : 0.0, s3 * ((gPressZ != 0 && gPressZ != 7) ? dim : 1.0), now, curG, "dart");
        double fyy2 = fy2 + fdy2;
        GBtn(3, fx, fyy2, bw1, fh2, "OPEN HUB", false, ghE, gPressZ == 3 ? pr : 0.0, s3 * ((gPressZ != 0 && gPressZ != 3) ? dim : 1.0), now, curG, "hub");
        GBtn(8, fx + bw1 + 10, fyy2, bw1, fh2, "ACCOUNTS", false, gaE, gPressZ == 8 ? pr : 0.0, s3 * ((gPressZ != 0 && gPressZ != 8) ? dim : 1.0), now, curG, "acct");
        // ---- the circuit doodle ----
        double cyD = cy + CH2 - 64;
        var pnCi = Pen(FA(Alpha(0xFFFFFF, 34), s3), 1);
        Line(fx + 2, cyD, fx + 46, cyD, pnCi); Line(fx + 46, cyD, fx + 46, cyD - 10, pnCi); Line(fx + 46, cyD - 10, fx + 92, cyD - 10, pnCi);
        Line(fx + 92, cyD - 10, fx + 92, cyD + 8, pnCi); Line(fx + 92, cyD + 8, fx + 128, cyD + 8, pnCi);
        var bN = SBrush(FA(Alpha(0xFFFFFF, 70), s3));
        FillEll(fx + 44.6, cyD - 1.4, 2.8, 2.8, bN); FillEll(fx + 90.6, cyD - 11.4, 2.8, 2.8, bN); FillEll(fx + 126.6, cyD + 6.6, 2.8, 2.8, bN);
        double tp = (DecT(now) % 2600) / 2600.0 * 154, pxD, pyD;
        if (tp <= 44) { pxD = fx + 2 + tp; pyD = cyD; }
        else if (tp <= 54) { pxD = fx + 46; pyD = cyD - (tp - 44); }
        else if (tp <= 100) { pxD = fx + 46 + (tp - 54); pyD = cyD - 10; }
        else if (tp <= 118) { pxD = fx + 92; pyD = cyD - 10 + (tp - 100); }
        else { pxD = fx + 92 + (tp - 118); pyD = cyD + 8; }
        FillEll(pxD - 4, pyD - 4, 8, 8, SBrush(FA(Alpha(curG, 60), s3)));
        FillEll(pxD - 1.8, pyD - 1.8, 3.6, 3.6, SBrush(FA(Alpha(AccHi(curG, 0.4), 225), s3)));
        // ---- the orbit and the equalizer ----
        double oex = fx + fw - 22, oey = thY;
        var pnO = Pen(FA(Alpha(curG, 55), s3), 1); PenDash(pnO, 2);
        Ell(oex - 13, oey - 13, 26, 26, pnO);
        double oaG = now * 0.0022;
        FillEll(oex + 13 * Math.Cos(oaG) - 1.7, oey + 13 * Math.Sin(oaG) - 1.7, 3.4, 3.4, SBrush(FA(Alpha(AccHi(curG, 0.5), 215), s3)));
        FillEll(oex + 8 * Math.Cos(-oaG * 1.6) - 1.2, oey + 8 * Math.Sin(-oaG * 1.6) - 1.2, 2.4, 2.4, SBrush(FA(Alpha(curG, 130), s3)));
        MiniHeart(oex, oey + 1, 3.4 + 0.8 * emb2G, FA(Alpha(AccHi(curG, 0.35), 200), s3));
        double eqx = fx + fw - 96, eqy = cy + CH2 - 50;
        for (int k = 1; k <= 14; k++)
        {
            double bh = 3.5 + 11 * Math.Abs(Math.Sin(DecT(now) * 0.0042 + k * 0.93)) * (0.5 + 0.5 * Math.Pow(Math.Sin(DecT(now) * 0.0016 + k * 0.6), 2));
            FillRR(eqx + (k - 1) * 7, eqy - bh, 3, bh, 1.5, SBrush(FA(Alpha(AccHi(curG, 0.25), 44 + 52 * Math.Abs(Math.Sin(DecT(now) * 0.003 + k * 1.3))), s3)));
        }
        // ---- the thumbnails ----
        if (gN > 1)
        {
            for (int i = 1; i <= thShow; i++)
            {
                double txc = thX0 + (i - 1) * thGap;
                double tgtW = (gBase + i) == (selFadeAt != 0 ? selNext : selCur) ? 1.0 : 0.0;
                double aw = ringW.TryGetValue(i, out var v) ? v : tgtW;
                aw += (tgtW - aw) * EK(0.16); if (Math.Abs(tgtW - aw) < 0.004) aw = tgtW;
                ringW[i] = aw;
                double tr = (thR - 2 + 2 * aw) + (i == hzT ? 1.5 : 0);
                var bmp = Pool.SelAt(gBase + i);
                if (bmp is not null)
                {
                    Img.CircleFit(bmp, txc, thY, tr, s3 * (0.72 + 0.28 * aw));
                    Ell(txc - tr + 0.5, thY - tr + 0.5, (tr - 0.5) * 2, (tr - 0.5) * 2, Pen(FA(Alpha(0x000000, 70), s3), 1));
                    if (aw < 0.999) Ell(txc - tr, thY - tr, tr * 2, tr * 2, Pen(FA(Alpha(0xFFFFFF, R((i == hzT ? 120 : 55) * (1 - aw))), s3), 1));
                    if (aw > 0.001)
                    {
                        Ell(txc - tr - 1, thY - tr - 1, (tr + 1) * 2, (tr + 1) * 2, Pen(FA(Alpha(curG, R(235 * aw)), s3), 2));
                        double aa = (DecT(now) * 0.12) % 360;
                        Arc(txc - tr - 1, thY - tr - 1, (tr + 1) * 2, (tr + 1) * 2, aa, 70, Pen(FA(Alpha(AccHi(curG, 0.5), R(235 * aw)), s3), 2));
                    }
                }
            }
        }
        else Txt(AppInfo.AppName, cx + CW2 - 152, cy + CH2 - 27, 128, 16, Fonts.fHint, FA(0x3EC7CBE0, s3), Fmt.R);
        // ---- the drag veil ----
        if (gDragT > 0.01)
        {
            double d = gDragT;
            FillRR(cx, cy, CW2, CH2, 16, SBrush(Alpha(0x0B0C14, 122 * d)));
            var pnV = Pen(Alpha(curG, 205 * d), 1.6); PenDash(pnV, 1); PenDashOff(pnV, (DecT(now) * 0.03) % 1000);
            StrokeRR(cx + 3, cy + 3, CW2 - 6, CH2 - 6, 13, pnV);
            double mcx = cx + CW2 / 2, mcy = cy + CH2 / 2 - 7;
            var pnM = Pen(Alpha(0xFFFFFF, 225 * d), 1.8);
            for (int k = 0; k < 4; k++)
            {
                double a_ = k * 1.5708, x2 = mcx + 13 * Math.Cos(a_), y2 = mcy + 13 * Math.Sin(a_);
                Line(mcx + 4 * Math.Cos(a_), mcy + 4 * Math.Sin(a_), x2, y2, pnM);
                Line(x2, y2, x2 - 4.5 * Math.Cos(a_ - 0.5), y2 - 4.5 * Math.Sin(a_ - 0.5), pnM);
                Line(x2, y2, x2 - 4.5 * Math.Cos(a_ + 0.5), y2 - 4.5 * Math.Sin(a_ + 0.5), pnM);
            }
            Txt("MOVING", mcx - 40, mcy + 16, 80, 14, Fonts.fBadge, Alpha(0xFFFFFF, 235 * d), Fmt.C);
        }
        Pop(st0);
        Pop(win);
    }
}
