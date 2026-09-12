using Yuri.Core;
using Yuri.Gfx;
using Yuri.Shell.Hub;
using Yuri.Shell.Hub.Tabs;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.Forsaken;

/// <summary>FORSAKEN (module 2): the EXTERNAL BLOCK REBIND card with its switch and key row, the PUZZLE AI card with its switch, grid chips, SET GRID / RESET, status, session pill, speed slider and two key rows, and the KEYBIND CONFLICT card. Zones 5, 10, 11, 1201-1217 as the .ahk's.</summary>
public static class FskPanel
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_OFF = 0xFFFB7185;
    const double ABH = 82, ABH2 = 178, TGW = 46, TGH = 24, KBRG = 30, KBCW = 24;
    const double SZY = 84, SZX = 100, SZW = 48, SZG = 8, NTY = 158, SLX = 100, SLW = 150, SLY = 54, BTY = 110, BTX = 56, BTW = 84, B2X = 146, B2W = 60, STY = 136;
    static double AbT, PuzT;
    static double Kby(HubLayout HL) => HL.aby + ABH + 14;
    static double Aby2(HubLayout HL) => Kby(HL) + 22 + 16;
    static double Kby2(HubLayout HL) => Aby2(HL) + ABH2 + 14;
    static double Tgx(HubLayout HL) => HL.abx + HL.abw - TGW - 18;
    static double Tgy(HubLayout HL) => HL.aby + 30;
    static double Tgy2(HubLayout HL) => Aby2(HL) + 28;
    static double SzBx(int i) => SZX + (i - 1) * (SZW + SZG);
    static double ClearX(double kbw) => 56 + kbw + 7;
    static double KeyW(int kr) => Keys.RebindOn && Keys.RebindTgt == kr ? Keys.ChipW("???") : Keys.ChipW(Keys.Label(kr == 1 ? Keys.BindKey : kr == 2 ? Keys.PuzKey : Keys.MkKey));
    public static bool Animating => Math.Abs(AbT - (Ab.On ? 1 : 0)) > 0.01 || Math.Abs(PuzT - (Puz.On ? 1 : 0)) > 0.01 || Keys.RebindOn || Puz.ClashUp || (Ab.FlashAt != 0 && Clock.Tick - Ab.FlashAt < 700) || (Puz.FlashAt != 0 && Clock.Tick - Puz.FlashAt < 700) || Puz.On || Ab.On;

    public static void Register()
    {
        Ab.Register(); Puz.Load();
        Integrations.Panels[2] = (hub, ax, ay, x0, y0, dx, dy2, ff, now, acc) =>
        {
            Draw(hub, ax, ay, x0, y0, dx, dy2, ff, now, acc);
            if (!Yuri.Platform.Os.IsWin)
                HubUI.WinOnlyVeil(hub.HL.abx - 10, hub.HL.aby - 10, hub.HL.abw + 20,
                    HubLayout.pd + HubLayout.ch - 34 - (hub.HL.aby - 10), acc, ff, now,
                    "EXTERNAL BLOCK REBIND  \u00B7  PUZZLE AI",
                    "both need a global keyboard hook and a read of the client's own screen, which macOS does not grant an ordinary app");
        };
        Integrations.PanelZones[2] = (hub, ux, uy) => Yuri.Platform.Os.IsWin ? Zone(hub, ux, uy) : 0;
        Integrations.PanelClicks[2] = Click;
        Integrations.ForsakenOn = () => (Ab.On ? 1 : 0) + (Puz.On ? 1 : 0);
        var prevArmed = Yuri.Shell.Hub.Tabs.Dashboard.SysArmedN;
        Yuri.Shell.Hub.Tabs.Dashboard.SysArmedN = () => prevArmed() + (Ab.On ? 1 : 0) + (Puz.On ? 1 : 0);
        Yuri.Shell.Hub.Tabs.Dashboard.BlockActive = () => Ab.Enabled;                 // the SYSTEMS tile's colour
        Yuri.Shell.Hub.Tabs.Dashboard.BlockArmed = () => Ab.On;
        var prevSub = Yuri.Shell.Hub.Tabs.Dashboard.SystemsSub;
        Yuri.Shell.Hub.Tabs.Dashboard.SystemsSub = () => Ab.Enabled ? "external block active" : Ab.On ? "external block armed" : Puz.On ? "puzzle AI armed" : prevSub();
    }
    public static void Draw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        double mt = HL.modT, f = ff;
        AbT += ((Ab.On ? 1.0 : 0.0) - AbT) * EK(0.2); PuzT += ((Puz.On ? 1.0 : 0.0) - PuzT) * EK(0.2);
        Puz.ClashExpire();
        double emb2G = 0.5 + 0.5 * Math.Sin(DecT(now) * 0.0026);
        Txt("SYSTEMS", ax, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, f), Fmt.L);
        Txt(mt > 0.5 ? "2 listed" : "0 listed", ax + 66, y0 + 36, 80, 14, HL.fXs, FA(Alpha(acc, 60 + 120 * mt), f), Fmt.L);
        double hvA = hub.Hv(5), pah = 26 + (ABH - 26) * mt;
        double c1 = Clamp((mt - 0.34) / 0.42, 0.0, 1.0), c2 = Clamp((mt - 0.44) / 0.42, 0.0, 1.0), c3 = Clamp((mt - 0.54) / 0.40, 0.0, 1.0), c4 = Clamp((mt - 0.64) / 0.36, 0.0, 1.0);
        FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(Mix(0xFF141728, Mix(0xFF171A30, 0xFF1C2142, hvA * 0.8), mt), f), FA(0xFF12141F, f)));
        MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, f, now, 0.9);
        Integrations.HubEmptyGhost(hub, ax, ay, pah, f);
        if (mt > 0.01)
        {
            StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, R((70 + 70 * AbT + 70 * hvA) * mt)), f), 1.2));
            double gl = 40 * mt; var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), R(200 * mt)), f), 1.6);
            Line(ax + 14, ay + 0.6, ax + 14 + gl, ay + 0.6, pnG); Line(ax + HL.abw - 14 - gl, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        }
        ModRing(ax, ay, HL.abw, pah, 12, acc, f, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (c1 <= 0.01) return;
        int stAB = PushG(); ClipRR(ax, ay, HL.abw, pah, 12);
        if (Ab.FlashAt != 0 && now - Ab.FlashAt < 620) { double e = Ease3((now - Ab.FlashAt) / 620.0), ex = e * 14; StrokeRR(ax - ex, ay - ex * 0.5, HL.abw + ex * 2, ABH + ex, 12 + ex * 0.4, Pen(FA(Alpha(acc, R(160 * (1 - e))), 1), 2.2 * (1 - e) + 0.4)); }
        double icx = ax + 30, icy = ay + 30 + (1 - c1) * 10;
        double okScl = 1 + 0.10 * Math.Sin(3.14159 * Clamp((now - Ab.FlashAt) / 340.0, 0.0, 1.0));
        int stB2 = PushXform(icx, icy, (Ab.FlashAt != 0 ? okScl : 1) * (0.72 + 0.28 * c1), 0);
        double fb = f; f = fb * c1;
        FillEll(icx - 16, icy - 16, 32, 32, SBrush(FA(Alpha(0x0B0C14, 235), f)));
        var sp2 = Shield(icx - 10, icy - 11.5, 20, 24);
        FillPath(sp2, SBrush(FA(Alpha(acc, R(Math.Min(255, 150 + 40 * emb2G + 70 * AbT))), f)));
        StrokePath(sp2, Pen(FA(Alpha(AccHi(acc, 0.35), 235), f), 1.4));
        if (AbT < 0.5) { FillRR(icx - 4.2, icy - 1.2, 8.4, 7.6, 2, SBrush(FA(Alpha(0x05060C, 235), f))); Arc(icx - 3.2, icy - 6.6, 6.4, 6.4, 180, 180, Pen(FA(Alpha(0x05060C, 235), f), 1.7)); }
        else { var pnT = Pen(FA(Alpha(0x05060C, 245), f), 2.2); Line(icx - 4.2, icy + 0.7, icx - 1.2, icy + 3.7, pnT); Line(icx - 1.2, icy + 3.7, icx + 4.4, icy - 3.7, pnT); }
        Pop(stB2);
        f = fb * c2;
        double sld = (1 - c2) * 9;
        Txt("EXTERNAL BLOCK REBIND", ax + 56 + sld, ay + 12, 220, 20, Fonts.fStatus, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.22), 240), f), Fmt.L);
        double dw1 = HL.abw - 130;
        Txt(FFMElide("Rebind Guest 1337 Block Externally (Versatile)", Fonts.fHint, dw1), ax + 56 + sld, ay + 34, dw1, 14, Fonts.fHint, FA(0x86C7CBE0, f), Fmt.L);
        uint abCol = Mix(HubState.Accent, Mix(AMBER, C_ON, Ab.Enabled ? 1.0 : 0.0), Clamp(AbT, 0.0, 1.0));
        string abSt = Ab.On ? (Ab.Enabled ? "ACTIVE - blocking on LMB" : Keys.BindKey == "" ? "ARMED - no key set, click ??? below" : "ARMED - press " + Keys.Label(Keys.BindKey) + " in game") : "DISABLED - flip the switch to arm";
        Txt(abSt, ax + 56 + sld, ay + 52, HL.abw - 130, 13, Fonts.fBadge, FA(Alpha(Ab.On && Keys.BindKey == "" ? AMBER : abCol, 215), f), Fmt.L);
        f = fb * c3;
        Toggle(hub, Tgx(HL) + (1 - c3) * 12, Tgy(HL), AbT, hvA, Ab.FlashAt, acc, f, now, emb2G);
        Pop(stAB);
        f = fb * c4;
        if (c4 <= 0.01) return;
        double pah2 = 26 + (ABH2 - 26) * mt, by2 = ay + pah + 14 + (1 - c4) * 8, ay2p = by2 + 22 + 16, by3 = ay2p + pah2 + 14;
        double rbp = Keys.RebindOn ? (Math.Sin(DecT(now) * 0.008) + 1) / 2 : 0.0;
        KeyRow(hub, 1, ax, by2, f, now, acc, rbp);
        // ---- PUZZLE AI ----
        double c6 = Clamp((mt - 0.44) / 0.42, 0.0, 1.0), c7 = Clamp((mt - 0.54) / 0.40, 0.0, 1.0), c8 = Clamp((mt - 0.64) / 0.36, 0.0, 1.0);
        double hvP = hub.Hv(11);
        bool puzFlash = Puz.FlashAt != 0 && now - Puz.FlashAt < 340;
        double puzScl = puzFlash ? 1 + 0.10 * Math.Sin(3.14159 * Clamp((now - Puz.FlashAt) / 340.0, 0.0, 1.0)) : 1;
        FillRR(ax, ay2p, HL.abw, pah2, 12, VBrush(ax, ay2p, HL.abw, pah2, FA(Mix(0xFF141728, Mix(0xFF171A30, 0xFF1C2142, hvP * 0.8), mt), f), FA(0xFF12141F, f)));
        MiniBackdrop(ax, ay2p, HL.abw, pah2, 12, acc, f, now, 0.9);
        StrokeRR(ax, ay2p, HL.abw, pah2, 12, Pen(FA(Alpha(acc, R(60 + 70 * hvP + 40 * PuzT)), f), 1));
        ModRing(ax, ay2p, HL.abw, pah2, 12, acc, f, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (Puz.FlashAt != 0 && now - Puz.FlashAt < 520) { double e = Ease3((now - Puz.FlashAt) / 520.0), ex = e * 16; StrokeRR(ax - ex, ay2p - ex * 0.5, HL.abw + ex * 2, pah2 + ex, 12 + ex * 0.4, Pen(FA(Alpha(acc, R(160 * (1 - e))), 1), 2.2 * (1 - e) + 0.4)); }
        int stPC = PushG(); ClipRR(ax, ay2p, HL.abw, pah2, 12);
        double icx2 = ax + 30, icy2 = ay2p + 30 + (1 - c6) * 10;
        int stP = PushXform(icx2, icy2, (puzFlash ? puzScl : 1) * (0.72 + 0.28 * c6), 0);
        double fb2 = f; f = fb2 * c6;
        FillEll(icx2 - 16, icy2 - 16, 32, 32, SBrush(FA(Alpha(0x0B0C14, 235), f)));
        var pnGd = Pen(FA(Alpha(acc, R(60 + 30 * PuzT)), f), 0.9);
        for (int gk = 1; gk <= 2; gk++) { double go2 = (gk - 1.5) * 7.4; Line(icx2 - 11, icy2 + go2, icx2 + 11, icy2 + go2, pnGd); Line(icx2 + go2, icy2 - 11, icx2 + go2, icy2 + 11, pnGd); }
        double rp = Puz.Solving ? DecT(now) % 900 / 900.0 : 1.0, seg1 = Math.Min(rp / 0.5, 1.0), seg2 = Clamp((rp - 0.5) / 0.5, 0.0, 1.0);
        var pnPath = Pen(FA(Alpha(AccHi(acc, 0.35), R(Math.Min(255, 150 + 40 * emb2G + 70 * PuzT))), f), 3.2);
        if (seg1 > 0.01) Line(icx2 - 7.4, icy2 - 7.4, icx2 - 7.4, icy2 - 7.4 + 14.8 * seg1, pnPath);
        if (seg2 > 0.01) Line(icx2 - 7.4, icy2 + 7.4, icx2 - 7.4 + 14.8 * seg2, icy2 + 7.4, pnPath);
        FillEll(icx2 - 10.4, icy2 - 10.4, 6, 6, SBrush(FA(Alpha(AccHi(acc, 0.4), 240), f)));
        FillEll(icx2 + 4.4, icy2 + 4.4, 6, 6, SBrush(FA(Alpha(AccHi(acc, 0.4), R(90 + 150 * seg2)), f)));
        Pop(stP);
        f = fb2 * c7;
        double sld2 = (1 - c7) * 9;
        Txt("PUZZLE AI", ax + 56 + sld2, ay2p + 12, 170, 20, Fonts.fStatus, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.22), 240), f), Fmt.L);
        double dw2 = HL.abw - 130;
        string dsc2 = "auto-solves connect-the-dots boards - hold the key to keep going";
        if (Fonts.MeasureW(dsc2, Fonts.fHint) > dw2) dsc2 = "auto-solves connect-the-dots boards - hold to keep going";
        if (Fonts.MeasureW(dsc2, Fonts.fHint) > dw2) dsc2 = "auto-solves connect-the-dots boards";
        Txt(FFMElide(dsc2, Fonts.fHint, dw2), ax + 56 + sld2, ay2p + 34, dw2, 14, Fonts.fHint, FA(0x86C7CBE0, f), Fmt.L);
        uint armHue = Mix(AMBER, C_ON, Clamp(Ab.Enabled ? 1.0 : 0.0, 0.0, 1.0));
        uint puzCol = Mix(HubState.Accent, armHue, Clamp(PuzT, 0.0, 1.0));
        bool fresh = Puz.MsgAt != 0 && now - Puz.MsgAt < 4000;
        string puzLabel = Keys.Label(Keys.PuzKey);
        string stTxt = Puz.Cal == 1 ? "click the board's TOP-LEFT corner" : Puz.Cal == 2 ? "now its BOTTOM-RIGHT corner  -  ESC cancels"
            : Puz.Solving ? "board " + (Puz.Sess + 1) + " - " + (Puz.Stage == "" ? "starting" : Puz.Stage) + "  \u00B7  press " + puzLabel + " again to stop"
            : fresh ? (Puz.MsgOk ? "DONE - " : "FAILED - ") + Puz.Msg
            : !Puz.On ? "DISABLED - flip the switch to arm"
            : Keys.PuzKey == "" ? "ARMED - no key set, click ??? below"
            : Puz.Sess != 0 ? "ARMED - " + Puz.Sess + " board" + (Puz.Sess == 1 ? "" : "s") + " this session, press " + puzLabel
            : "ARMED - press " + puzLabel + " in game, hold to repeat";
        uint stCl2 = Puz.Cal != 0 ? Alpha(AccHi(acc, 0.35), 255) : fresh && !Puz.MsgOk && !Puz.Solving ? C_OFF : Puz.On && Keys.PuzKey == "" ? AMBER : puzCol;
        double fsz = f * c8;
        Txt("grid", ax + 56, ay2p + SZY + 3, 44, 16, Fonts.fHint, FA(0x62C7CBE0, fsz), Fmt.L);
        for (int szi = 1; szi <= Puz.SIZES.Length; szi++) FFMBtn(1213 + szi, ax + SzBx(szi), ay2p + SZY, SZW, 22, Puz.SIZES[szi - 1] + "x" + Puz.SIZES[szi - 1], acc, fsz, szi == Puz.GridNi ? 3 : 0);
        Txt(Math.Round(Puz.SX) + " px cells" + (Puz.Custom ? "  \u00B7  SET GRID" : ""), ax + SzBx(Puz.SIZES.Length) + SZW + 12, ay2p + SZY + 3, HL.abw - SzBx(Puz.SIZES.Length) - SZW - 28, 16, HL.fXs, FA(Alpha(Puz.SX < 8 ? AMBER : 0xFFC7CBE0, 160), fsz), Fmt.L);
        FFMBtn(1204, ax + BTX, ay2p + BTY, BTW, 22, Puz.Cal != 0 ? "CANCEL" : "SET GRID", acc, fsz, Puz.Cal != 0 ? 2 : 0);
        FFMBtn(1205, ax + B2X, ay2p + BTY, B2W, 22, "RESET", acc, fsz, 0);
        double stW = HL.abw - BTX - 16;
        Txt(FFMElide(stTxt, Fonts.fBadge, stW), ax + BTX + sld2, ay2p + STY, stW, 16, Fonts.fBadge, FA(Alpha(stCl2, 225), f), Fmt.L);
        if (Puz.On)
        {
            string sTx = "SESSION  \u00B7  BOARD " + (Puz.Sess + 1);
            uint sCl = Puz.Sess != 0 ? AccHi(acc, 0.35) : C_ON;
            double sW = Fonts.MeasureW(sTx, HL.fXs) + 26, sX = ax + HL.abw - 16 - sW + sld2, sY = ay2p + BTY + 3;
            FillRR(sX, sY, sW, 16, 8, VBrush(sX, sY, sW, 16, FA(Alpha(sCl, 34), f * c8), FA(Alpha(sCl, 14), f * c8)));
            StrokeRR(sX, sY, sW, 16, 8, Pen(FA(Alpha(sCl, 80), f * c8), 1));
            double pa = Puz.Sess != 0 ? 200 : Math.Round(150 + 90 * (Math.Sin(DecT(now) * 0.005) + 1) / 2);
            FillEll(sX + 9, sY + 6, 4, 4, SBrush(FA(Alpha(sCl, R(pa)), f * c8)));
            Txt(sTx, sX + 18, sY + 1, sW - 22, 14, HL.fXs, FA(Alpha(sCl, 225), f * c8), Fmt.L);
        }
        double nW = HL.abw - BTX - 16;
        if (nW > 40) Txt(FFMElide("(SET IN-GAME, BIT BUGGY)", HL.fXs, nW), ax + BTX + sld2, ay2p + NTY, nW, 13, HL.fXs, FA(Alpha(AMBER, 165), f), Fmt.L);
        double slx = ax + SLX, sly = ay2p + SLY, sfa = f * (Puz.On ? 1 : 0.45), hvS = hub.Hv(1203);
        Txt("speed", ax + 56, sly - 8, 44, 16, Fonts.fHint, FA(0x62C7CBE0, sfa), Fmt.L);
        FillRR(slx, sly - 1.5, SLW, 3, 1.5, SBrush(FA(Alpha(0xFFFFFF, 26), sfa)));
        FillRR(slx, sly - 1.5, SLW * Puz.SpeedT, 3, 1.5, SBrush(FA(Alpha(acc, 190), sfa)));
        for (int q = 0; q < 2; q++) FillRR(slx + q * SLW, sly - 5, 1, 10, 0.5, SBrush(FA(Alpha(0xFFFFFF, 40), sfa)));
        double knx = slx + SLW * Puz.SpeedT, kr2 = 6 + 1.6 * hvS + (HL.drag == 19 ? 1.4 : 0);
        FillEll(knx - kr2, sly - kr2 + 1.5, kr2 * 2, kr2 * 2, SBrush(FA(Alpha(0x000000, 90), sfa)));
        FillEll(knx - kr2, sly - kr2, kr2 * 2, kr2 * 2, SBrush(FA(Alpha(0xFEFEFE, 245), sfa)));
        FillEll(knx - 2.4, sly - 2.4, 4.8, 4.8, SBrush(FA(Alpha(acc, 210), sfa)));
        Txt(Puz.SpeedPct + "%", slx + SLW + 12, sly - 9, 46, 18, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.3), 225), sfa), Fmt.L);
        Txt("assumes " + Math.Round(1000 / Puz.FrameMs) + " fps - keep at or below what your client holds", ax + 56, sly + 12, HL.abw - 130, 13, HL.fXs, FA(Alpha(Puz.SpeedT > 0.72 ? AMBER : 0xFFC7CBE0, 150), sfa), Fmt.L);
        Toggle(hub, Tgx(HL) + (1 - c7) * 12, Tgy2(HL), PuzT, hvP, Puz.FlashAt, acc, f, now, emb2G);
        Pop(stPC);
        KeyRow(hub, 2, ax, by3, f, now, acc, rbp);
        KeyRow(hub, 3, ax, by3 + KBRG, f, now, acc, rbp);
        double c5 = Clamp((mt - 0.74) / 0.26, 0.0, 1.0);
        if (c5 > 0.01)
        {
            double sy2 = by3 + KBRG + 22 + 26;
            FadeLine(ax, ax + HL.abw, sy2 - 12, 0x1CFFFFFF, f * c5);
            Txt("systems ship inside the script and cannot be removed", ax, sy2, HL.abw, 14, Fonts.fHint, FA(0x44C7CBE0, f * c5), Fmt.L);
            for (int k = 1; k <= 20; k++)
            {
                double gr2 = Clamp((c5 - k * 0.02) * 2.4, 0.0, 1.0);
                double hh = (3 + 9 * Math.Abs(Math.Sin(DecT(now) * 0.0026 + k * 0.7))) * gr2;
                FillRR(ax + HL.abw - 20 - (20 - k) * 7, sy2 + 16 - hh, 3, hh, 1.5, SBrush(FA(Alpha(acc, R(22 + 40 * Math.Abs(Math.Sin(DecT(now) * 0.0018 + k * 0.5)))), f * c5)));
            }
        }
        if (Puz.ClashUp && mt > 0.9) Clash(hub, ax, ay, y0, by3, f, now, acc);
    }
    static Avalonia.Media.Geometry Shield(double x, double y, double w, double h)
    {
        var g = new Avalonia.Media.StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Avalonia.Point(x + w / 2, y), true);
            c.LineTo(new Avalonia.Point(x + w, y + h * 0.22)); c.LineTo(new Avalonia.Point(x + w, y + h * 0.55));
            c.QuadraticBezierTo(new Avalonia.Point(x + w * 0.95, y + h * 0.85), new Avalonia.Point(x + w / 2, y + h));
            c.QuadraticBezierTo(new Avalonia.Point(x + w * 0.05, y + h * 0.85), new Avalonia.Point(x, y + h * 0.55));
            c.LineTo(new Avalonia.Point(x, y + h * 0.22)); c.EndFigure(true);
        }
        return g;
    }
    static void Toggle(HubSurface hub, double tx, double ty, double t, double hv, long flashAt, uint acc, double f, long now, double emb2G)
    {
        FillRR(tx, ty, TGW, TGH, TGH / 2, SBrushP(FA(THMix(0xFF33354E, 0xFF34D399, t), f)));
        MicroBackdrop(tx, ty, TGW, TGH, TGH / 2, acc, f, now, 0.6);
        StrokeRR(tx, ty, TGW, TGH, TGH / 2, Pen(FA(Alpha(acc, R(130 + 60 * hv + 40 * emb2G)), f), 1));
        if (t > 0.02) StrokeRR(tx - 2.5, ty - 2.5, TGW + 5, TGH + 5, TGH / 2 + 2.5, Pen(FA(Alpha(C_ON, R((60 + 50 * emb2G) * t)), f), 1.6));
        double kx = tx + TGH / 2 + (TGW - TGH) * t, kpop = flashAt != 0 && now - flashAt < 240 ? 1 + 0.22 * (1 - (now - flashAt) / 240.0) : 1;
        FillEll(kx - 8 * kpop, ty + TGH / 2 - 8 * kpop + 1.5, 16 * kpop, 16 * kpop, SBrush(FA(Alpha(0x000000, 90), f)));
        FillEll(kx - 8 * kpop, ty + TGH / 2 - 8 * kpop, 16 * kpop, 16 * kpop, SBrush(FA(Alpha(0xFEFEFE, 245), f)));
        FillEll(kx - 2.6, ty + TGH / 2 - 2.6, 5.2, 5.2, SBrush(FA(Alpha(acc, 210), f)));
    }
    /// <summary>HubKeyRow(kr): block / solve / markers - the label, the key chip (??? while listening), the clear cross, the hint.</summary>
    static void KeyRow(HubSurface hub, int kr, double ax, double ky, double f, long now, uint acc, double rbp)
    {
        var HL = hub.HL;
        bool mine = Keys.RebindOn && Keys.RebindTgt == kr;
        string klab = Keys.Label(kr == 1 ? Keys.BindKey : kr == 2 ? Keys.PuzKey : Keys.MkKey);
        double kbw = KeyW(kr);
        int kzn = kr == 1 ? 10 : kr == 2 ? 1201 : 1202;
        long kfl = kr == 1 ? Keys.BindFlashAt : kr == 2 ? Keys.PuzFlashAt : Keys.MkFlashAt;
        string knm = kr == 1 ? "block" : kr == 2 ? "solve" : "markers";
        double khv = hub.Hv(kzn), hk_ = Math.Max(khv, mine ? rbp : 0);
        bool live = kr == 1 ? Ab.On : Puz.On;
        double kf = f * (live ? 1 : 0.45);
        Txt(knm, ax, ky, 52, 22, Fonts.fHint, FA(0x62C7CBE0, kf), Fmt.L);
        FillRR(ax + 56, ky, kbw, 22, 6, SBrush(FA(Mix(0xFF171A30, 0xFF232744, hk_), kf)));
        MicroBackdrop(ax + 56, ky, kbw, 22, 6, acc, kf, now, 0.9);
        StrokeRR(ax + 56, ky, kbw, 22, 6, Pen(FA(Alpha(acc, R(120 + 110 * hk_)), kf), 1));
        Txt(mine ? "???" : klab, ax + 56, ky - 1, kbw, 22, Fonts.fBadge, FA(Alpha(acc, R(mine ? 150 + 100 * rbp : 225 + 30 * khv)), kf), Fmt.C);
        if (kfl != 0 && now - kfl < 520) { double e = Ease3((now - kfl) / 520.0), ex = e * 9; StrokeRR(ax + 56 - ex, ky - ex * 0.5, kbw + ex * 2, 22 + ex, 6 + ex * 0.4, Pen(FA(Alpha(acc, R(160 * (1 - e))), 1), 1.8 * (1 - e) + 0.3)); }
        bool cbOn = live && Keys.Bound(kr) && !mine;
        if (cbOn) FFMBtn(1210 + kr, ax + ClearX(kbw), ky, KBCW, 22, "\u00D7", acc, kf, 0);
        string hint = mine ? "press any key - ESC cancels" : kr == 1 ? "arms / disarms the block in game" : kr == 2 ? "solves the board on screen" : "show or hide the grid markers";
        if (!live) hint = "arm the system above to set a key";
        double hx = ax + (cbOn ? ClearX(kbw) + KBCW + 8 : 64 + kbw), hw = Math.Max(60, HL.abw - (hx - ax) - 6);
        Txt(FFMElide(hint, Fonts.fHint, hw), hx, ky, hw, 22, Fonts.fHint, FA(Alpha(mine ? 0xFF9AA8C0 : 0xFF62C7CB, R(mine ? 130 + 80 * rbp : 150)), kf), Fmt.L);
    }
    static void ClashGeom(HubLayout HL, out double wx, out double wy, out double ww, out double wh)
    {
        double dTop = HL.cty + 28 - HL.aby, dBot = Math.Min(Kby2(HL) + 128, HubLayout.pd + HubLayout.ch - 16) - HL.aby;
        wx = 12; ww = HL.abw - 24; wy = dTop + 48; wh = (dBot - 36) - wy;
    }
    static void ClashBtn(double wx, double wy, double ww, double wh, out double bx, out double by, out double bw, out double bh) { bw = 110; bh = 30; bx = wx + ww - 20 - bw; by = wy + wh - 16 - bh; }
    /// <summary>
    /// KEYBIND CONFLICT: one key cannot serve both systems. The .ahk's block in
    /// full - the hazard stripes, the watermark key, the marching chevrons along
    /// the foot, the accent rail, the corner arcs (no reticle brackets - the
    /// straight L outside each arc read as a second, misaligned corner),
    /// the perimeter light, the six-stage entrance, the two stacked rows with
    /// their solid / hollow markers and the barred link between them, and the
    /// exit that collapses the entrance's ring inward and scatters shards.
    /// </summary>
    static void Clash(HubSurface hub, double ax, double ay, double y0, double by3, double f, long now, uint acc)
    {
        var HL = hub.HL;
        ClashGeom(HL, out double wx, out double wy, out double ww, out double wh);
        wx += ax; wy += ay;
        // ---- in and out ----
        // The exit is the entrance run backwards: the dim lifts, the card shrinks
        // and rises, and the ring that punched OUTWARD collapses INWARD.
        double ce = Puz.ClashAt != 0 ? Clamp((now - Puz.ClashAt) / 420.0, 0.0, 1.0) : 1.0;
        double cs = EBackOut(ce, 1.5);
        double co = Puz.ClashOut != 0 ? Clamp((now - Puz.ClashOut) / 300.0, 0.0, 1.0) : 0.0;
        double coE = Ease3(co);
        double wp = (Math.Sin(DecT(now) * 0.003) + 1) / 2;
        double fw = f * Math.Min(ce * 2.2, 1.0) * (1 - coE);
        // contents arrive in sequence, the staging the overlay card and the grid
        // setup card use
        double q1 = Ease3(Clamp((ce - 0.08) / 0.40, 0.0, 1.0)), q2 = Ease3(Clamp((ce - 0.16) / 0.40, 0.0, 1.0));
        double q3 = Ease3(Clamp((ce - 0.26) / 0.40, 0.0, 1.0)), q4 = Ease3(Clamp((ce - 0.38) / 0.40, 0.0, 1.0));
        double q5 = Ease3(Clamp((ce - 0.48) / 0.40, 0.0, 1.0)), q6 = Ease3(Clamp((ce - 0.58) / 0.40, 0.0, 1.0));
        // the dim covers the WHOLE column - header, both cards, every keybind
        // row, the footer strip and the bars
        double dimY = y0 + 28, dimH = (by3 + KBRG + 22 + 26 + 50) - dimY;
        FillRR(ax - 8, dimY, HL.abw + 16, dimH, 12, SBrush(FA(Alpha(0x05060C, R(175 * Math.Min(ce * 2, 1.0) * (1 - coE))), f)));
        // a shade of anticipation on the way out, a damped shudder on the way in:
        // a refusal that arrives perfectly still reads as a panel
        double anti = co > 0 && co < 0.24 ? 1 + 0.018 * Math.Sin(3.14159 * co / 0.24) : 1;
        double shk = !HubState.LowPerf && ce < 1 ? Math.Sin(ce * 34) * Math.Pow(1 - ce, 2) * 6 : 0;
        int stW = PushXform(wx + ww / 2, wy + wh / 2, (0.94 + 0.06 * cs) * anti * (1 - 0.08 * coE), 0);
        wx += shk; wy -= (1 - cs) * 10 + coE * 10;
        double f0 = f; f = fw;
        if (ce < 1 && co == 0)
        {
            double ex = (1 - ce) * 22;
            StrokeRR(wx - ex, wy - ex * 0.6, ww + ex * 2, wh + ex * 1.2, 16 + ex * 0.4, Pen(FA(Alpha(AMBER, R(180 * (1 - ce))), f), 2.4 * (1 - ce) + 0.4));
        }
        ShadowDraw(wx, wy, ww, wh, 16, 8, 26, 26, f);
        FillRR(wx, wy, ww, wh, 16, VBrush(wx, wy, ww, wh, FA(0xFF241C1E, f), FA(0xFF171216, f)));
        // PanelBackdrop, not Mini: Mini is meant for a 26-tall strip and on a card
        // this size leaves a couple of faint dots in a lot of empty plate.
        PanelBackdrop(wx, wy, ww, wh, AMBER, f, now, 0.95);

        // ---- inside the plate ----
        // Clipped and INTERSECTED, then the whole state put back: this card draws
        // inside the systems panel's own clip.
        int svW = PushG();
        ClipRR(wx, wy, ww, wh, 16);
        if (!HubState.LowPerf)                                                   // hazard stripes, drifting
        {
            double dr = DecT(now) % 4200 / 4200.0 * 40;
            var pnH = Pen(FA(Alpha(AMBER, 12), f), 10);
            for (double sxh = wx - wh - 40; sxh < wx + ww + wh; sxh += 40)
                Line(sxh + dr, wy + wh, sxh + dr + wh, wy, pnH);
        }
        // the key again as a watermark: the card is mostly plate and the one
        // thing it is about is a single short string
        if (!HubState.LowPerf)
            TxtP(Puz.ClashKey, wx, wy + wh * 0.30, ww, wh * 0.5, HL.fG, FA(Alpha(AMBER, R(13 + 5 * wp)), f * q3), Fmt.C);
        if (!HubState.LowPerf)                                                   // hazard chevrons along the foot
        {
            double cvy = wy + wh - 13, cvh = 10, cdr = DecT(now) % 2600 / 2600.0 * 30;
            FillRR(wx, cvy - 3, ww, cvh + 6, 0, SBrush(FA(Alpha(0x000000, 40), f * q6)));
            var pnC = Pen(FA(Alpha(AMBER, 58), f * q6), 4);
            for (double cvx = wx - 40; cvx < wx + ww + 40; cvx += 30)
            {
                Line(cvx + cdr, cvy + cvh, cvx + cdr + cvh * 0.8, cvy, pnC);
                Line(cvx + cdr + cvh * 0.8, cvy, cvx + cdr + cvh * 1.6, cvy + cvh, pnC);
            }
        }
        // the accent rail every other card in the suite carries
        FillRect(wx, wy, 12, wh, SBrush(FA(Alpha(AMBER, R(40 + 26 * wp)), f)));
        FillRect(wx, wy, 4, wh, VBrush(wx, wy, 4, wh, FA(Alpha(AMBER, 245), f), FA(Alpha(AMBER, 120), f)));
        Pop(svW);

        StrokeRR(wx, wy, ww, wh, 16, Pen(FA(Alpha(AMBER, R(120 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.003)))), f), 1.4));
        for (int kg = 0; kg <= 3; kg++)
        {
            double shm = (Math.Sin(DecT(now) * 0.005 + kg * 1.5708) + 1) / 2;
            double xk = kg == 1 || kg == 2 ? wx + ww - 31.5 : wx + 0.5;
            double yk = kg >= 2 ? wy + wh - 31.5 : wy + 0.5;
            double ang = kg == 0 ? 195 : kg == 1 ? 285 : kg == 2 ? 15 : 105;
            Arc(xk, yk, 31, 31, ang, 60, Pen(FA(Alpha(AMBER, R(85 + 65 * shm)), f), 1.8));
        }
        if (!HubState.LowPerf)                                                   // a light running the perimeter, one pass every four seconds
        {
            double per2 = (ww + wh) * 2, tp2 = DecT(now) / 4200.0 % 1.0 * per2;
            for (int i = 0; i < 14; i++)
            {
                double d2 = tp2 - i * 7; if (d2 < 0) d2 += per2;
                double lpx, lpy;
                if (d2 < ww) { lpx = wx + d2; lpy = wy; }
                else if (d2 < ww + wh) { lpx = wx + ww; lpy = wy + (d2 - ww); }
                else if (d2 < ww * 2 + wh) { lpx = wx + ww - (d2 - ww - wh); lpy = wy + wh; }
                else { lpx = wx; lpy = wy + wh - (d2 - ww * 2 - wh); }
                double la = R(150 * Math.Pow(1 - i / 14.0, 2)), rl = 2.2 * (1 - i / 16.0);
                FillEll(lpx - rl, lpy - rl, rl * 2, rl * 2, SBrush(FA(Alpha(AccHi(AMBER, 0.5), la), f)));
            }
        }

        // ---- header: the warning TRIANGLE, not a disc ----
        double tcx = wx + 42, tcy = wy + 36 - (1 - q1) * 6;
        for (int i = 1; i <= 2; i++)
        {
            double rh = 13 + i * (3.2 + 2.6 * wp);
            FillEll(tcx - rh, tcy - rh, rh * 2, rh * 2, SBrush(FA(Alpha(AMBER, R(22 - i * 6)), f * q1)));
        }
        var pnT = Pen(FA(Alpha(AMBER, 235), f * q1), 2.3);
        Line(tcx, tcy - 12, tcx + 12, tcy + 9, pnT);
        Line(tcx + 12, tcy + 9, tcx - 12, tcy + 9, pnT);
        Line(tcx - 12, tcy + 9, tcx, tcy - 12, pnT);
        var bTri = SBrush(FA(Alpha(AMBER, 235), f * q1));
        FillRR(tcx - 1.1, tcy - 5, 2.2, 7.5, 1.1, bTri);
        FillRR(tcx - 1.1, tcy + 4.5, 2.2, 2.2, 1.1, bTri);
        Txt("KEYBIND CONFLICT", wx + 68, wy + 20 - (1 - q1) * 6, ww - 88, 24, HL.fV, FA(Alpha(AccHi(AMBER, 0.35), 245), f * q1), Fmt.L);
        Txt("one key cannot serve both systems", wx + 68, wy + 44 - (1 - q1) * 4, ww - 88, 16, Fonts.fHint, FA(0x8AC7CBE0, f * q2), Fmt.L);
        FadeLine(wx + 24, wx + ww - 24, wy + 74, 0x30FFFFFF, f * q2);
        if (q2 > 0.01 && q2 < 1 && !HubState.LowPerf)
            FillRR(wx + 24, wy + 73.2, (ww - 48) * q2, 1.6, 0.8, SBrush(FA(Alpha(AccHi(AMBER, 0.4), R(200 * (1 - q2))), f)));

        // ---- the contested key, as a key ----
        double kcw = Math.Max(84, Fonts.MeasureW(Puz.ClashKey, Fonts.fKey) + 44);
        double kcx = wx + (ww - kcw) / 2, kcy = wy + 94 + (1 - q3) * 10;
        for (int rg = 1; rg <= 2; rg++)
            FillRR(kcx - rg * 5, kcy - rg * 4, kcw + rg * 10, 44 + rg * 8, 12 + rg * 2, SBrush(FA(Alpha(AMBER, R((26 - rg * 8) * (0.5 + 0.5 * wp))), f * q3)));
        FillRR(kcx, kcy, kcw, 44, 10, VBrush(kcx, kcy, kcw, 44, FA(0xFF3E302A, f * q3), FA(0xFF251D1F, f * q3)));
        MicroBackdrop(kcx, kcy, kcw, 44, 10, AMBER, f * q3, now, 0.85);
        StrokeRR(kcx, kcy, kcw, 44, 10, Pen(FA(Alpha(AMBER, R(150 + 80 * wp)), f * q3), 1.4));
        TxtP(Puz.ClashKey, kcx, kcy - 1, kcw, 44, Fonts.fKey, FA(Alpha(AccHi(AMBER, 0.45), 252), f * q3), Fmt.C);
        Txt("is already in use", wx, kcy + 52, ww, 18, Fonts.fHint, FA(Alpha(0xFFC7CBE0, 215), f * q3), Fmt.C);

        // ---- the two systems, one row each, STACKED ----
        // Side by side, each row was half a card wide and both subtitles elided;
        // a row apiece means neither can be truncated by the other.
        double rw = ww - 48, rx = wx + 24;
        for (int kk = 1; kk <= 2; kk++)
        {
            double ry = wy + 176 + (kk - 1) * 54;
            double qr = kk == 1 ? q4 : q5;
            ry += (1 - qr) * 8;
            string nm = kk == 1 ? Puz.ClashOwn : Puz.ClashWho;
            string sb = kk == 1 ? "is listening on it" : "needs its own key, or disarm the other";
            FillRR(rx, ry, rw, 46, 10, SBrush(FA(Alpha(0xFFFFFF, R(10 + 6 * (kk == 1 ? 1 : 0))), f * qr)));
            MicroBackdrop(rx, ry, rw, 46, 10, AMBER, f * qr, now, 0.85);
            // a left bar apiece: solid on the system that holds the key, hollow
            // on the one that was turned away, so the pair reads without labels
            if (kk == 1) FillRR(rx, ry + 8, 3, 30, 1.5, VBrush(rx, ry + 8, 3, 30, FA(Alpha(AMBER, 235), f * qr), FA(Alpha(AMBER, 90), f * qr)));
            else StrokeRR(rx + 0.5, ry + 8, 2.5, 30, 1.2, Pen(FA(Alpha(AMBER, 110), f * qr), 1));
            var pnR = Pen(FA(Alpha(AMBER, R(kk == 1 ? 90 : 52)), f * qr), 1);
            if (kk == 2) PenDash(pnR, 1);
            StrokeRR(rx, ry, rw, 46, 10, pnR);
            // the holder gets a live dot, the refused one a hollow ring
            if (kk == 1) FillEll(rx + 14, ry + 20, 7, 7, SBrush(FA(Alpha(AMBER, R(160 + 90 * wp)), f * qr)));
            else Ell(rx + 14, ry + 20, 7, 7, Pen(FA(Alpha(AMBER, 150), f * qr), 1.4));
            Txt(FFMElide(nm, Fonts.fBadge, rw - 44), rx + 32, ry + 8, rw - 44, 16, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 240), f * qr), Fmt.L);
            Txt(FFMElide(sb, Fonts.fHint, rw - 44), rx + 32, ry + 24, rw - 44, 15, Fonts.fHint, FA(0xA0C7CBE0, f * qr), Fmt.L);
        }
        // the collision itself: a dashed link between the rows with a barred circle on it
        if (q5 > 0.02)
        {
            double lx2 = rx + 17.5, ly1 = wy + 176 + 46, ly2 = wy + 176 + 54;
            var pnL = Pen(FA(Alpha(AMBER, R(110 * q5)), f), 1.2);
            PenDash(pnL, 2);
            Line(lx2, ly1, lx2, ly2, pnL);
            double mcy2 = (ly1 + ly2) / 2;
            var pnO = Pen(FA(Alpha(AMBER, R(200 * q5)), f), 1.4);
            Ell(lx2 - 5, mcy2 - 5, 10, 10, pnO);
            Line(lx2 - 3.4, mcy2 + 3.4, lx2 + 3.4, mcy2 - 3.4, pnO);
        }

        // inert once the dismissal is running - the zone is refused by then, so
        // drawing it live would promise a click that is gone
        ClashBtn(wx, wy, ww, wh, out double bxq, out double byq, out double bwq, out double bhq);
        if (co == 0) FFMBtn(1206, bxq, byq, bwq, bhq, "GOT IT", AMBER, f * q6, 1);
        Txt("or press ESC", wx + 24, byq + bhq / 2 - 8, ww - 48 - bwq - 16, 16, HL.fXs, FA(0x66C7CBE0, f * q6), Fmt.L);
        // the exit's ring: the entrance punched outward, this collapses in
        if (co > 0 && co < 1)
        {
            double ix = coE * 30;
            StrokeRR(wx + ix, wy + ix * 0.6, Math.Max(6, ww - ix * 2), Math.Max(6, wh - ix * 1.2), Math.Max(2, 16 - ix * 0.35),
                     Pen(FA(Alpha(AMBER, R(210 * (1 - coE))), f0), 2.4 * (1 - coE) + 0.4));
            if (!HubState.LowPerf)                                               // and shards, the scatter the overlay card closes with
            {
                double ocx = wx + ww / 2, ocy = wy + wh / 2;
                for (int i = 1; i <= 8; i++)
                {
                    double aa = (i * 45 + 22 * coE) * 0.0174533, rr2 = 24 + coE * 70, sr = 3.2 * (1 - coE) + 0.5;
                    FillEll(ocx + rr2 * Math.Cos(aa) - sr, ocy + rr2 * Math.Sin(aa) - sr, sr * 2, sr * 2, SBrush(FA(Alpha(AMBER, R(210 * (1 - coE))), f0)));
                }
            }
        }
        Pop(stW);
        f = f0;
    }
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        if (HL.modT <= 0.85) return 0;
        if (Puz.ClashUp)
        {
            ClashGeom(HL, out double wxz, out double wyz, out double wwz, out double whz); wxz += HL.abx; wyz += HL.aby;
            ClashBtn(wxz, wyz, wwz, whz, out double bxz, out double byz, out double bwz, out double bhz);
            if (Puz.ClashOut == 0 && ux >= bxz && ux <= bxz + bwz && uy >= byz && uy <= byz + bhz) return 1206;
            return 1207;                                                          // the dim swallows the rest
        }
        double aby2 = Aby2(HL);
        if (Puz.On && ux >= HL.abx + SLX - 8 && ux <= HL.abx + SLX + SLW + 8 && uy >= aby2 + SLY - 11 && uy <= aby2 + SLY + 11) return 1203;
        if (ux >= Tgx(HL) - 10 && ux <= Tgx(HL) + TGW + 10 && uy >= Tgy(HL) - 8 && uy <= Tgy(HL) + TGH + 8) return 5;
        if (ux >= Tgx(HL) - 10 && ux <= Tgx(HL) + TGW + 10 && uy >= Tgy2(HL) - 8 && uy <= Tgy2(HL) + TGH + 8) return 11;
        if (uy >= aby2 + SZY && uy <= aby2 + SZY + 22) for (int i = 1; i <= Puz.SIZES.Length; i++) { double zbx = HL.abx + SzBx(i); if (ux >= zbx && ux <= zbx + SZW) return 1213 + i; }
        if (uy >= aby2 + BTY && uy <= aby2 + BTY + 22) { if (ux >= HL.abx + BTX && ux <= HL.abx + BTX + BTW) return 1204; if (ux >= HL.abx + B2X && ux <= HL.abx + B2X + B2W) return 1205; }
        for (int kr = 1; kr <= 3; kr++)
        {
            if (!(kr == 1 ? Ab.On : Puz.On)) continue;
            double kbw = KeyW(kr), kty = kr == 1 ? Kby(HL) : Kby2(HL) + (kr - 2) * KBRG;
            if (uy < kty || uy > kty + 22) continue;
            if (ux >= HL.abx + 56 && ux <= HL.abx + 56 + kbw) return kr == 1 ? 10 : kr == 2 ? 1201 : 1202;
            double cbx = HL.abx + ClearX(kbw);
            if (Keys.Bound(kr) && !(Keys.RebindOn && Keys.RebindTgt == kr) && ux >= cbx && ux <= cbx + KBCW) return 1210 + kr;
        }
        return 0;
    }
    public static bool Press(HubSurface hub, int z)
    {
        if (z != 1203) return false;
        hub.HL.drag = 19; hub.BeginPtrDrag();
        SlideSet(hub, hub.PtrX);
        return true;
    }
    public static void SlideSet(HubSurface hub, double ux) { Puz.SlideSet((ux - (hub.HL.abx + SLX)) / SLW); hub.Tim(Pace.TICK_A); }
    public static bool Click(HubSurface hub, int z)
    {
        switch (z)
        {
            case 5: Ab.Toggle(); break;
            case 11: Puz.Toggle(); break;
            case 10: Keys.RebindClick(1); break;
            case 1201: Keys.RebindClick(2); break;
            case 1202: Keys.RebindClick(3); break;
            case 1211: case 1212: case 1213: Keys.Clear(z - 1210); break;
            case 1204: Puz.SetGrid(); break;
            case 1205: Puz.GridReset(); break;
            case >= 1214 and <= 1217: Puz.GridPick(z - 1213); break;
            case 1206: Puz.ClashDismiss(); break;
            case 1207: break;
            default: return false;
        }
        hub.ClickAt[z] = Clock.Tick;
        hub.Tim(Pace.TICK_A);
        return true;
    }
}
