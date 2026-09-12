using Avalonia.Input;
using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Loading;

/// <summary>
/// The boot splash. A 440x190 card: the brand and the clock in the header, the
/// INITIALIZING / READY scramble, a six-step progress bar whose ceiling is
/// held back until the boot work is done, a dial, an equaliser, a heartbeat.
/// Progress is a clock (LD_SCRIPT_MS) capped by a ceiling that opens with the
/// work (LD_BODY..LD_CEIL) and by LOAD_MAX_MS regardless, so the bar never
/// waits on the network for more than twelve seconds and never lies about
/// having finished.
/// </summary>
public sealed class LoadingSurface : Surface
{
    const double CWl = 440, CHl = 190, PDl = 26;
    const double BWl = CWl + PDl * 2, BHl = CHl + PDl * 2;
    const int LD_SCRIPT_MS = 2620, LOAD_MAX_MS = 12000;
    const double LD_BODY = 0.72, LD_CEIL = 0.99, LD_RATE = 0.010;
    const string SCRSET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#$%&";
    const string TKB = "7F A2 C4 09 5B E1 3D 88 F0 4A 91 C7 2E 66 0B D4 ";

    readonly double cx = PDl, cy = PDl;
    readonly double pbx = PDl + 26, pby = PDl + 122, pbw = CWl - 52, pbh = 10;
    readonly (int at, string say)[] steps;
    readonly Font fLT;
    readonly Fonts.Scr scrInit, scrReady;
    readonly Action _done;
    readonly long lIntro;
    double prog; long doneAt; int stepIdx = 1; long stepAt;
    int ldPend0; double ldWMax; int ldDoneN;
    bool finished;

    public LoadingSurface(Action done) : base(BWl, BHl, UiScale.Factor)
    {
        _done = done;
        int savN = Boot.SavedPlaces, acctN = Boot.SavedAccounts;
        string ldSay = (savN > 0 && acctN > 0) ? $"resolving {savN} place(s), {acctN} account(s)"
                     : savN > 0 ? $"resolving saved places [{savN}]"
                     : acctN > 0 ? $"signing in saved accounts [{acctN}]"
                     : "arming hotkey matrix";
        steps = new[]
        {
            (0, "verifying credentials"), (360, "decrypting asset pool"),
            (800, $"mounting avatar pool [{Boot.AvatarPoolN}]"),
            (1260, ldSay), (1720, "fetching community flags"), (2180, "syncing interface"),
        };
        ldPend0 = Boot.Left() + 0;
        fLT = Fonts.New(22, true);
        scrInit = Fonts.BuildScr("INITIALIZING", fLT);
        scrReady = Fonts.BuildScr("READY", fLT);
        lIntro = Clock.Tick; stepAt = lIntro;
        Tim(Pace.ModalTick());
    }

    // ---- deliberately not draggable ----
    // LoadingScreen() in the .ahk installs no hit test, no drag zone and no
    // WM_NCHITTEST handler: the transient screens exist for a few seconds and
    // are the only thing to interact with while they are up. This used to
    // override OnZoneDown with BeginDrag, which the surface gives every window
    // for free - the loading card is the one that must not take it.
    protected override void OnZoneDown(int z, PointerPressedEventArgs e) { }

    void LFinish()
    {
        if (finished) return;
        finished = true;
        Stop();
        Avalonia.Threading.Dispatcher.UIThread.Post(_done);        // not from inside the render pass
    }

    protected override void Frame(long now)
    {
        if (finished) return;
        HubState.ThemeTick();
        long el = now - lIntro;
        double it = Math.Min(el / (double)Pace.INTRO_MS, 1.0);
        double ei = 1 - Math.Pow(1 - it, 3);
        int winA = R(255 * Math.Min(it * 1.7, 1.0));
        double yO = R((1 - ei) * 26);
        int ldPend = Boot.Left();
        if (ldPend > ldPend0) ldPend0 = ldPend;
        bool ldOpen = ldPend == 0 || el >= LOAD_MAX_MS;
        double ldT = Clamp(el / (double)LD_SCRIPT_MS, 0.0, 1.0);                 // the clock
        double ldW = ldPend0 > 0 ? Clamp(1.0 - ldPend / (double)ldPend0, 0.0, 1.0) : 1.0;
        ldWMax = Math.Max(ldWMax, ldW);                                          // a floor, never a reading
        ldDoneN = Math.Max(ldDoneN, ldPend0 - ldPend);
        double ldC = Clamp((el - LD_BODY * LD_SCRIPT_MS) / Math.Max(1, LOAD_MAX_MS - LD_BODY * LD_SCRIPT_MS), 0.0, 1.0);
        double ldCeil = ldOpen ? 1.0 : Math.Min(LD_BODY + (LD_CEIL - LD_BODY) * Math.Max(ldWMax, ldC), LD_CEIL);
        double ldWant = Math.Min(ldT, ldCeil);
        double ldD = ldWant - prog;
        if (ldD > 0) prog = Math.Min(prog + Math.Max(ldD * EK(0.085), LD_RATE), ldWant);
        if (prog > 0.999 && ldOpen) prog = 1.0;
        double ldClock = prog * LD_SCRIPT_MS;
        int si = 1;
        for (int i = 0; i < steps.Length; i++) if (ldClock >= steps[i].at) si = i + 1;
        if (si != stepIdx) { stepIdx = si; stepAt = now; }
        if (prog >= 1.0 && doneAt == 0) doneAt = now;
        double scl = 1 + 0.004 * Math.Sin(DecT(now) * 0.0012);
        if (doneAt != 0)
        {
            if (now - doneAt > 480)
            {
                double ft = Math.Min((now - doneAt - 480) / 330.0, 1.0);
                winA = R(winA * (1 - ft)); scl *= 1 - 0.06 * ft;
                yO += R(10 * ft);
                if (ft >= 1) { LFinish(); return; }
            }
            else
                scl *= 1 + 0.02 * Math.Sin(3.14159 * Math.Min((now - doneAt) / 260.0, 1.0));
        }
        uint curL = AccSat(Mix(HubState.Accent, HubState.C_ON, prog));
        double embG = (Math.Sin(DecT(now) * 0.0016) + 1) / 2;
        double emb2G = (Math.Sin(DecT(now) * 0.004) + 1) / 2;

        int win = PushOpacity(winA / 255.0);
        PushShift(0, yO);
        int st0 = PushXform(cx + CWl / 2, cy + CHl / 2, scl, 0);
        ShadowDraw(cx, cy + 3, CWl, CHl, 16, 10, 8, 8, 1.0);
        FillRR(cx, cy, CWl, CHl, 16, BgV(cx, cy, CWl, CHl, 0xF2222438, 0xF8121423));
        int clip = PushG();
        ClipRR(cx, cy, CWl, CHl, 16);
        PanelBackdrop(cx, cy, CWl, CHl, HubState.Accent, 1.0, now, 0.95);
        FillRect(cx, cy, CWl, CHl, LineBrush(cx - 1, cy - 1, CWl + 2, CHl + 2, 0x0EFFFFFF, 0x00FFFFFF, 2));
        for (int k = 1; k <= 10; k++)                                            // the motes
        {
            int cyc = 5200 + k * 337;
            double prt = ((now + k * 997) % cyc) / (double)cyc;
            double mx_ = cx + 34 + (k * 167) % (CWl - 68) + 9 * Math.Sin(DecT(now) * 0.0009 + k * 1.7);
            double my_ = cy + CHl - 10 - prt * (CHl - 20);
            double mr_ = 0.9 + (k % 3) * 0.5;
            FillEll(mx_ - mr_, my_ - mr_, mr_ * 2, mr_ * 2, SBrush(Alpha(AccHi(curL, 0.35), 44 * Math.Sin(3.14159 * prt))));
        }
        double swp = (DecT(now) % 3400) / 3400.0;                                // the travelling sheen
        double swx = cx - 26 + (CWl + 52) * swp;
        uint c0s = curL & 0xFFFFFF;
        uint c1s = Alpha(c0s, 16 + 22 * prog);
        FillRect(swx - 24, cy, 24, CHl, LineBrush(swx - 25, cy - 2, 26, CHl + 4, c0s, c1s, 0));
        FillRect(swx, cy, 24, CHl, LineBrush(swx - 1, cy - 2, 26, CHl + 4, c1s, c0s, 0));
        FillRect(cx, cy, 14, CHl, SBrush(Alpha(curL, Math.Min(255, 46 + 16 * embG + 60 * prog))));   // the accent edge
        FillRect(cx, cy, 4.5, CHl, VBrush(cx, cy, 4.5, CHl, Alpha(curL, 245), Alpha(curL, 110 + 40 * embG)));
        Pop(clip);
        StrokeRR(cx, cy, CWl, CHl, 16, Pen(0x28FFFFFF, 1));
        Line(cx + 18, cy + 1.5, cx + CWl - 18, cy + 1.5, Pen(0x16FFFFFF, 1));
        var pnD = Pen(Alpha(curL, 20 + 10 * emb2G), 1);
        PenDash(pnD, 1);
        PenDashOff(pnD, (DecT(now) * 0.02) % 1000);
        StrokeRR(cx + 6, cy + 6, CWl - 12, CHl - 12, 12, pnD);
        CornerFlourish(cx, cy, CWl, CHl, curL);
        Txt($"{stepIdx:00} / {steps.Length:00}", cx + CWl - 140, cy + 30, 96, 12, Fonts.fBadge, Alpha(0x9AA8C0, 130), Fmt.R);
        Txt(AppInfo.AppName, cx + 26, cy + 12, 90, 20, Fonts.fBrand, 0xCFE8EAF6, Fmt.L);
        double hb = DecT(now) % 2200;
        int blink = (hb < 140 || (hb > 300 && hb < 440)) ? 1 : 0;
        FillEll(cx + 26 + Fonts.wZeal + 6, cy + 19.5, 5, 5, SBrush(Alpha(curL, Math.Min(255, 150 + 35 * emb2G + 48 * blink))));
        double ckx = cx + CWl - 44;                                              // the clock
        var pnC = Pen(Alpha(curL, 170), 1.6);
        Arc(ckx - 86, cy + 15, 10, 10, (DecT(now) * 0.22) % 360, 100, pnC);
        Arc(ckx - 86, cy + 15, 10, 10, (DecT(now) * 0.22) % 360 + 180, 40, pnC);
        Txt(Clock.ClockStr(), ckx - 70, cy + 13, 70, 14, Fonts.fBadge, 0x86C7CBE0, Fmt.R);
        var scr = doneAt != 0 ? scrReady : scrInit;                             // the scramble
        long scrAt = doneAt != 0 ? doneAt : lIntro;
        double ty = cy + 46;
        if (now < scrAt + 130 + scr.Chs.Count * 42)
        {
            for (int i = 1; i <= scr.Chs.Count; i++)
            {
                string ch; uint cc;
                if (now < scrAt + 90 + i * 42) { ch = SCRSET[Random.Shared.Next(SCRSET.Length)].ToString(); cc = Alpha(Mix(curL, 0xFFE8EAF6, 0.55), 205); }
                else { ch = scr.Chs[i - 1]; cc = Alpha(curL, 245); }
                Txt(ch, cx + 26 + scr.Xs[i - 1], ty, 46, 30, fLT, cc, Fmt.L);
            }
        }
        else
            Txt(doneAt != 0 ? "READY" : "INITIALIZING", cx + 26, ty, 290, 30, fLT, Alpha(curL, 245), Fmt.L);
        double ulw = (DecT(now) * 0.09) % 180 - 30;                             // the underline runner
        double sxu = Math.Max(cx + 26, cx + 26 + ulw);
        double sxv = Math.Min(cx + 176, cx + 26 + ulw + 30);
        if (sxv > sxu + 1) FillRect(sxu, ty + 30, sxv - sxu, 2, SBrush(Alpha(AccHi(curL, 0.35), 175)));
        FillRect(cx + 26, ty + 30, 150, 1, SBrush(Alpha(curL, 40)));
        for (int k = 0; k <= 2; k++)                                             // the three chevrons
        {
            double x0 = cx + 26 + scr.W + 12 + k * 7;
            double ca = 50 + 85 * Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.0025 - k * 0.9)), 3);
            var pn = Pen(Alpha(Mix(0xFFE8EAF6, curL, 0.35), Math.Min(255, ca)), 1.8);
            Line(x0, ty + 15 - 3.8, x0 + 3.6, ty + 15, pn);
            Line(x0 + 3.6, ty + 15, x0, ty + 15 + 3.8, pn);
        }
        double stf = Ease3((now - stepAt) / 240.0);                              // the step caption
        double sly = pby - 22 + (1 - stf) * 5;
        var pnS = Pen(Alpha(curL, 190 * stf), 1.6);
        Line(pbx, sly + 6, pbx + 4, sly + 9.5, pnS);
        Line(pbx + 4, sly + 9.5, pbx, sly + 13, pnS);
        string ldLbl = doneAt != 0 ? "handshake complete"
                     : (el >= LD_SCRIPT_MS && ldPend > 0) ? $"resolving  \u00B7  {ldDoneN} / {ldPend0}"
                     : steps[stepIdx - 1].say;
        Txt(ldLbl, pbx + 12, sly, pbw - 120, 18, Fonts.fHint, Alpha(0xC7CBE0, 90 + 130 * stf), Fmt.L);
        Txt(R(prog * 100) + "%", pbx, sly, pbw, 18, Fonts.fBadge, Alpha(curL, 235), Fmt.R);
        FillRR(pbx, pby, pbw, pbh, 5, SBrush(0xFF171A30));                       // the bar
        MicroBackdrop(pbx, pby, pbw, pbh, 5, curL, 1.0, now, 0.9);
        StrokeRR(pbx, pby, pbw, pbh, 5, Pen(0x30FFFFFF, 1));
        double fw_ = pbw * prog;
        if (fw_ > 3)
        {
            int fc = PushG();
            ClipRR(pbx, pby, fw_, pbh, 5);
            FillRect(pbx, pby, fw_, pbh, VBrush(pbx, pby, fw_, pbh, Alpha(AccHi(curL, 0.25), 255), Alpha(curL, 235)));
            double shx = pbx - 40 + (DecT(now) * 0.14) % (pbw + 80);
            FillRect(shx, pby, 18, pbh, LineBrush(shx, pby - 1, 37, pbh + 2, 0x00FFFFFF, 0x8AFFFFFF, 0));
            FillRect(shx + 18, pby, 18, pbh, LineBrush(shx + 17, pby - 1, 37, pbh + 2, 0x8AFFFFFF, 0x00FFFFFF, 0));
            Pop(fc);
            FillEll(pbx + fw_ - 7, pby + pbh / 2 - 7, 14, 14, SBrush(Alpha(curL, 70)));
            FillEll(pbx + fw_ - 2.6, pby + pbh / 2 - 2.6, 5.2, 5.2, SBrush(Alpha(AccHi(curL, 0.5), 235)));
            var pnH = Pen(Alpha(AccHi(curL, 0.4), 170), 1.3);
            Arc(pbx + fw_ - 10, pby + pbh / 2 - 10, 20, 20, (DecT(now) * 0.26) % 360, 90, pnH);
            Arc(pbx + fw_ - 10, pby + pbh / 2 - 10, 20, 20, (DecT(now) * 0.26) % 360 + 180, 40, pnH);
        }
        if (doneAt != 0 && now - doneAt < 560)
        {
            double e = Ease3((now - doneAt) / 560.0);
            double ex = e * 10;
            StrokeRR(pbx - ex, pby - ex * 0.6, pbw + ex * 2, pbh + ex * 1.2, 5 + ex * 0.4, Pen(Alpha(curL, 170 * (1 - e)), 2.2 * (1 - e) + 0.4));
        }
        for (int k = 1; k <= steps.Length; k++)                                  // the step dots
        {
            double px_ = pbx + (k - 0.5) * pbw / steps.Length;
            bool on_ = doneAt != 0 || prog >= (k - 0.5) / steps.Length;
            if (k == stepIdx && doneAt == 0)
            {
                double rpS = (DecT(now) % 1200) / 1200.0;
                Ell(px_ - 3 - rpS * 5, pby + pbh + 7 - rpS * 5, 6 + rpS * 10, 6 + rpS * 10, Pen(Alpha(curL, R(150 * (1 - rpS))), 1.2));
            }
            FillEll(px_ - 2, pby + pbh + 8, 4, 4, SBrush(Alpha(on_ ? curL : 0xFFFFFF, on_ ? 200 : 40)));
        }
        double hbc = (DecT(now) % 2600) / 2600.0;                                 // the heart
        MiniHeart(cx + CWl - 30 - 6 * Math.Sin(DecT(now) * 0.0021), cy + CHl - 26 - hbc * 36, 3.6, Alpha(AccHi(curL, 0.4), R(130 * Math.Sin(3.14159 * hbc))));
        double dsx = cx + 332;                                                    // the divider
        FillRect(dsx, cy + 48, 1, 24, VBrush(dsx, cy + 48, 1, 24, Alpha(0xFFFFFF, 0), Alpha(0xFFFFFF, 34)));
        FillRect(dsx, cy + 72, 1, 24, VBrush(dsx, cy + 72, 1, 24, Alpha(0xFFFFFF, 34), Alpha(0xFFFFFF, 0)));
        double dcx = cx + 368, dcy = cy + 72;                                     // the dial
        double swA = (DecT(now) * 0.14) % 360;
        FillEll(dcx - 24, dcy - 24, 48, 48, SBrush(Alpha(0xFF090A12, 165)));
        FillEll(dcx - 24, dcy - 24, 48, 48, SBrush(Alpha(curL, R(10 + 10 * prog))));
        FillPie(dcx - 23, dcy - 23, 46, 46, swA - 48, 48, SBrush(Alpha(curL, 44)));
        FillPie(dcx - 23, dcy - 23, 46, 46, swA - 16, 16, SBrush(Alpha(AccHi(curL, 0.4), 30)));
        var pnX = Pen(Alpha(curL, 20), 1);
        Line(dcx - 21, dcy, dcx + 21, dcy, pnX);
        Line(dcx, dcy - 21, dcx, dcy + 21, pnX);
        Ell(dcx - 12, dcy - 12, 24, 24, Pen(Alpha(curL, 30), 1));
        Ell(dcx - 24, dcy - 24, 48, 48, Pen(Alpha(AccHi(curL, 0.25), 70 + 30 * embG), 1.2));
        double aR = swA * 0.0174533;
        Line(dcx, dcy, dcx + 23 * Math.Cos(aR), dcy + 23 * Math.Sin(aR), Pen(Alpha(AccHi(curL, 0.55), 215), 1.5));
        FillEll(dcx + 23 * Math.Cos(aR) - 1.8, dcy + 23 * Math.Sin(aR) - 1.8, 3.6, 3.6, SBrush(Alpha(AccHi(curL, 0.5), 200)));
        FillEll(dcx - 5, dcy - 5, 10, 10, SBrush(Alpha(AccHi(curL, 0.4), 60)));
        FillEll(dcx - 2, dcy - 2, 4, 4, SBrush(Alpha(AccHi(curL, 0.45), 230)));
        for (int k = 1; k <= 3; k++)                                              // the three blips
        {
            double bA = k == 1 ? 40 : k == 2 ? 150 : 250;
            double bR = k == 2 ? 17 : 10;
            double dfA = (swA - bA + 360) % 360;
            double bfl = dfA < 44 ? 1 - dfA / 44.0 : 0;
            FillEll(dcx + bR * Math.Cos(bA * 0.0174533) - 1.6, dcy + bR * Math.Sin(bA * 0.0174533) - 1.6, 3.2, 3.2, SBrush(Alpha(AccHi(curL, 0.5), R(40 + 195 * bfl))));
        }
        int tsh = (int)((now / 130) % TKB.Length);                                // the token ticker
        FillEll(cx + 122, cy + CHl - 15, 3.4, 3.4, SBrush(Alpha(curL, 120 + 80 * Math.Abs(Math.Sin(DecT(now) * 0.004)))));
        Txt(TKB[tsh..] + TKB[..tsh], cx + 132, cy + CHl - 19, CWl - 200, 13, Fonts.fBadge, Alpha(0x9AA8C0, 62), Fmt.L);
        double eqx = pbx, eqy = cy + CHl - 14;                                    // the equaliser
        for (int k = 1; k <= 12; k++)
        {
            double bh_ = 3 + 9 * Math.Abs(Math.Sin(DecT(now) * 0.0042 + k * 0.93)) * (0.5 + 0.5 * Math.Pow(Math.Sin(DecT(now) * 0.0016 + k * 0.6), 2));
            FillRR(eqx + (k - 1) * 7, eqy - bh_, 3, bh_, 1.5, SBrush(Alpha(AccHi(curL, 0.25), 40 + 46 * Math.Abs(Math.Sin(DecT(now) * 0.003 + k * 1.3)))));
        }
        Txt(AppInfo.AppName, cx + CWl - 154, cy + CHl - 27, 128, 16, Fonts.fHint, 0x3EC7CBE0, Fmt.R);
        Pop(st0);
        Pop(win);
    }
}

/// <summary>LoadingScreen(): the window around the surface; `done` runs once the card has faded.</summary>
public static class LoadingScreen
{
    public static void Show(Action done)
    {
        LayeredWindow? win = null;
        var surface = new LoadingSurface(() => { win?.Close(); done(); });
        win = new LayeredWindow(surface, "YURILOAD", topmost: true, toolWindow: true);
        win.Show();
    }
}
