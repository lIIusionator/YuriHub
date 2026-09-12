using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Platform;
using Yuri.Shell.Hub;
using Yuri.Shell.Hub.Tabs;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.LoginItems;

/// <summary>LGISystems / LGIFooter / LGIZone / LGIClick: module 7's panel. Zone ids are the .ahk's (2100-2146).</summary>
public static class LgiPanel
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_BAD = 0xFFF04438, C_ACC = 0xFFFB7185;
    const int RG = 56, RH = 46, VISN = 3, TOP = 48;
    const double DOP_FRAME = 0.62, DOP_ROW0 = 0.63, DOP_ROWD = 0.045, DOP_ROWS = 0.13;
    static bool _picking;

    public static double RowY(int i) => TOP + (i - 1) * RG;
    public static double Total() => Math.Max(1, Lgi.Items.Count) * RG;
    public static double Vis() => VISN * RG;
    public static double Lgih => TOP + VISN * RG + 10;
    static double Lgiby(HubLayout HL) => HL.aby + Lgih + 12;
    const double Lgibh = 72;

    public static void Register()
    {
        Lgi.Load();
        Lgi.WatchStart();
        Integrations.Panels[7] = Draw;
        Integrations.PanelZones[7] = Zone;
        Integrations.PanelClicks[7] = Click;
        Integrations.PanelWheels[7] = Wheel;
        Integrations.LoginItems = () => (Lgi.Items.Count, Lgi.On);
    }
    public static bool Animating => Math.Abs(Lgi.Scr - Lgi.ScrT) > 0.4 || (Lgi.MsgAt != 0 && Clock.Tick - Lgi.MsgAt < 6300) || (Lgi.ActAt != 0 && Clock.Tick - Lgi.ActAt < 1300)
        || (Lgi.AnimAt != 0 && Clock.Tick - Lgi.AnimAt < 700) || Lgi.FlashAt.Count > 0 || (Lgi.On && Lgi.Items.Count > 0 && Lgi.Rbx == 1);

    public static void Draw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        double mt = HL.modT;
        int nI = Lgi.Items.Count;
        if (nI > 0 && mt > 0.5) Lgi.Scan();
        Txt("SYSTEMS", ax, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, ff), Fmt.L);
        Txt(mt > 0.5 ? nI + " listed" : "0 listed", ax + 66, y0 + 36, 80, 14, HL.fS, FA(Alpha(acc, 60 + 120 * mt), ff), Fmt.L);
        double fm = Clamp(mt / DOP_FRAME, 0.0, 1.0);
        double pah = 26 + (Lgih - 26) * fm;
        FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(Mix(0xFF141728, 0xFF181C34, fm), ff), FA(0xFF12141F, ff)));
        MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, ff, now, 0.9);
        Integrations.HubEmptyGhost(hub, ax, ay, pah, ff);
        StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, 70 + 90 * fm), ff), 1.2));
        double gl = 40 * fm;
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 200 * fm), ff), 1.6);
        Line(ax + 14, ay + 0.6, ax + 14 + gl, ay + 0.6, pnG); Line(ax + HL.abw - 14 - gl, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        ModRing(ax, ay, HL.abw, pah, 12, acc, ff, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (mt < 0.02) return;
        int stL = PushG();
        ClipRR(ax, ay, HL.abw, pah, 12);
        double vis = Vis();
        Lgi.ScrT = Clamp(Lgi.ScrT, 0.0, Math.Max(0.0, Total() - vis));
        Lgi.Scr += (Lgi.ScrT - Lgi.Scr) * EK(0.26);
        if (Math.Abs(Lgi.ScrT - Lgi.Scr) < 0.4) Lgi.Scr = Lgi.ScrT;
        double scr = Lgi.Scr;
        double cg0 = Clamp((mt - DOP_ROW0) / DOP_ROWS, 0.0, 1.0);
        bool armed = Lgi.On && nI > 0;
        bool rbxUp = Lgi.Rbx == 1;
        // ---- the lifeline strip ----
        double sy = ay + 12;
        FillRR(ax + 10, sy, HL.abw - 20, 30, 8, VBrush(ax + 10, sy, HL.abw - 20, 30, FA(Alpha(0xFFFFFF, R(12 * cg0)), ff), FA(Alpha(0xFFFFFF, R(4 * cg0)), ff)));
        StrokeRR(ax + 10, sy, HL.abw - 20, 30, 8, Pen(FA(Alpha(acc, R(50 * cg0)), ff), 1));
        double rtx = ax + 30, rty = sy + 15;
        int stR = PushXform(rtx, rty, 1.0, 45);
        FillRR(rtx - 6.5, rty - 6.5, 13, 13, 2, SBrush(FA(Alpha(rbxUp ? C_ON : 0xFF9AA8C0, R((rbxUp ? 225 : 120) * cg0)), ff)));
        FillRR(rtx - 2.2, rty - 2.2, 4.4, 4.4, 0.8, SBrush(FA(Alpha(0xFF12141F, R(240 * cg0)), ff)));
        Pop(stR);
        Txt("ROBLOX", ax + 44, sy + 9, 50, 12, HL.fXs, FA(Alpha(rbxUp ? C_ON : 0xFFC7CBE0, R((rbxUp ? 220 : 130) * cg0)), ff), Fmt.L);
        double lx0 = ax + 96, lx1 = ax + HL.abw - 96, ly = sy + 15;
        var pnD = Pen(FA(Alpha(armed ? acc : 0xFFC7CBE0, R((armed ? 110 : 50) * cg0)), ff), 1.2);
        PenDash(pnD, 2);
        Line(lx0, ly, lx1, ly, pnD);
        if (armed && rbxUp && !HubState.LowPerf)
            for (int k = 0; k < 3; k++)
            {
                double pp = (DecT(now) * 0.00035 + k / 3.0) % 1.0;
                double px2 = lx0 + (lx1 - lx0) * pp;
                FillEll(px2 - 5, ly - 5, 10, 10, SBrush(FA(Alpha(C_ON, R(60 * cg0)), ff)));
                FillEll(px2 - 2, ly - 2, 4, 4, SBrush(FA(Alpha(AccHi(C_ON, 0.3), R(230 * cg0)), ff)));
            }
        int nStr = Math.Min(nI, 6);
        if (nStr > 0)
        {
            double gapS = Math.Min(30, (lx1 - lx0 - 20) / Math.Max(1, nStr));
            for (int k = 1; k <= nStr; k++)
            {
                var it = Lgi.Items[k - 1];
                double cx3 = lx0 + 12 + (k - 1) * gapS + 8, cy3 = ly;
                FillRR(cx3 - 9, cy3 - 9, 18, 18, 5, SBrush(FA(Alpha(0xFF12141F, R(235 * cg0)), ff)));
                bool actv = Lgi.On && (it.Open || it.Close);
                StrokeRR(cx3 - 9, cy3 - 9, 18, 18, 5, Pen(FA(Alpha(actv ? acc : 0xFFC7CBE0, R((actv ? 170 : 60) * cg0)), ff), 1));
                var bm = Lgi.IconBmp(it);
                if (bm is not null) Img.DrawRR(bm, cx3 - 7, cy3 - 7, 14, 14, 3, ff * cg0 * (actv ? 1.0 : 0.5));
            }
        }
        string pTxt = !Lgi.On ? "OFF" : nI == 0 ? "EMPTY" : rbxUp ? "LINKED" : "WAITING";
        uint pCol = !Lgi.On ? 0xFF9AA8C0 : nI == 0 ? AMBER : rbxUp ? C_ON : acc;
        double pw2 = Fonts.MeasureW(pTxt, HL.fXs) + 18;
        FillRR(ax + HL.abw - 20 - pw2, sy + 7, pw2, 16, 8, SBrush(FA(Alpha(pCol, R(40 * cg0)), ff)));
        StrokeRR(ax + HL.abw - 20 - pw2, sy + 7, pw2, 16, 8, Pen(FA(Alpha(pCol, R(130 * cg0)), ff), 1));
        Txt(pTxt, ax + HL.abw - 20 - pw2, sy + 9, pw2, 12, HL.fXs, FA(Alpha(AccHi(pCol, 0.3), R(235 * cg0)), ff), Fmt.C);
        if (nI == 0)
        {
            double ey = ay + TOP + 6;
            var pnE = Pen(FA(Alpha(acc, R(90 * cg0)), ff), 1.2);
            PenDash(pnE, 1);
            StrokeRR(ax + 14, ey, HL.abw - 28, 60, 10, pnE);
            FillRR(ax + 14, ey, HL.abw - 28, 60, 10, SBrush(FA(Alpha(acc, R(14 * cg0)), ff)));
            double pcx = ax + HL.abw / 2, pcy = ey + 22;
            FillEll(pcx - 11, pcy - 11, 22, 22, SBrush(FA(Alpha(acc, R(60 * cg0)), ff)));
            var pnP = Pen(FA(Alpha(AccHi(acc, 0.5), R(230 * cg0)), ff), 1.8);
            Line(pcx - 5, pcy, pcx + 5, pcy, pnP); Line(pcx, pcy - 5, pcx, pcy + 5, pnP);
            Txt("ADD APP to string a program onto the line", ax + 24, ey + 38, HL.abw - 48, 14, HL.fS, FA(Alpha(0xFFE8EAF6, R(200 * cg0)), ff), Fmt.C);
            Txt("Anything goes: a program, a shortcut, a script, a document. OPENS starts it when a", ax + 24, ey + 74, HL.abw - 48, 15, HL.fS, FA(Alpha(0xFFC7CBE0, R(140 * cg0)), ff), Fmt.L);
            Txt("Roblox client appears, CLOSES shuts it once the last one is gone. A change has to", ax + 24, ey + 90, HL.abw - 48, 15, HL.fS, FA(Alpha(0xFFC7CBE0, R(140 * cg0)), ff), Fmt.L);
            Txt("hold a few seconds first, so a client restarting mid-update never closes anything.", ax + 24, ey + 106, HL.abw - 48, 15, HL.fS, FA(Alpha(0xFFC7CBE0, R(140 * cg0)), ff), Fmt.L);
        }
        for (int i = 1; i <= Math.Min(nI, Lgi.MAX); i++)
        {
            var it = Lgi.Items[i - 1];
            double ry = ay + RowY(i) - scr;
            double cg = Clamp((mt - DOP_ROW0 - (i - 1) * DOP_ROWD) / DOP_ROWS, 0.0, 1.0);
            if (cg <= 0.01) continue;
            if (ry + RH < ay + TOP - 6 || ry > ay + pah) continue;
            double fb = ff * cg;
            double hv = Math.Max(Math.Max(hub.Hv(2100 + i), hub.Hv(2110 + i)), Math.Max(hub.Hv(2120 + i), hub.Hv(2130 + i)));
            double sld = (1 - cg) * 10;
            double axs = ax + sld;
            double fl = 0.0;
            if (Lgi.FlashAt.TryGetValue(i, out var fat)) { double fe = (now - fat) / 520.0; if (fe >= 1) Lgi.FlashAt.Remove(i); else fl = 1 - fe; }
            bool live = Lgi.IsRunning(it);
            bool on = (it.Open || it.Close) && Lgi.On;
            FillRR(ax + 8, ry, HL.abw - 16, RH, 9, VBrush(ax + 8, ry, HL.abw - 16, RH, FA(Mix(0xFF171A34, 0xFF1E2346, Math.Max(hv, fl)), fb), FA(0xFF0E1019, fb)));
            MiniBackdrop(ax + 8, ry, HL.abw - 16, RH, 9, acc, fb, now, 0.7);
            StrokeRR(ax + 8, ry, HL.abw - 16, RH, 9, Pen(FA(Alpha(on ? acc : 0xFFC7CBE0, on ? 50 + 70 * hv : 24 + 30 * hv), fb), 1));
            FillRR(axs + 16, ry + 10, 3, RH - 20, 1.5, VBrush(axs + 16, ry + 10, 3, RH - 20, FA(Alpha(on ? AccHi(acc, 0.35) : 0xFFC7CBE0, on ? 235 : 60), fb), FA(Alpha(on ? acc : 0xFFC7CBE0, on ? 150 : 40), fb)));
            double ibx = axs + 28, iby = ry + RH / 2.0 - 12;
            FillRR(ibx, iby, 24, 24, 6, SBrush(FA(Alpha(0xFFFFFF, 12), fb)));
            StrokeRR(ibx, iby, 24, 24, 6, Pen(FA(Alpha(on ? acc : 0xFFC7CBE0, on ? 90 : 40), fb), 1));
            var bm = Lgi.IconBmp(it);
            if (bm is not null) Img.DrawRR(bm, ibx + 2, iby + 2, 20, 20, 4, fb * (on ? 1.0 : 0.6));
            else
            {
                var pnI = Pen(FA(Alpha(on ? acc : 0xFF9AA8C0, on ? 200 : 110), fb), 1.4);
                StrokeRR(ibx + 5, iby + 6, 14, 12, 2, pnI); Line(ibx + 5, iby + 9.5, ibx + 19, iby + 9.5, pnI);
            }
            if (live)
            {
                FillEll(ibx + 17, iby - 3, 10, 10, SBrush(FA(Alpha(0xFF12141F, 240), fb)));
                FillEll(ibx + 19, iby - 1, 6, 6, SBrush(FA(Alpha(C_ON, 235), fb)));
            }
            double tw = HL.abw - 246;
            Txt(FFMElide(it.Name, Fonts.fBadge, tw), axs + 62, ry + 7, tw, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.18), 235), fb), Fmt.L);
            string ht = !Lgi.On ? "off - the master switch is down" : it.Open && it.Close ? "opens with roblox, closes with it" : it.Open ? "opens with roblox" : it.Close ? "closes with roblox" : "listed, both switches off";
            Txt(FFMElide(ht + (live ? "  -  running" : ""), HL.fS, tw), axs + 62, ry + 26, tw, 15, HL.fS, FA(on ? Alpha(C_ON, 175) : 0x8AC7CBE0u, fb), Fmt.L);
            double wx = ax + HL.abw - 178, ww = 138;
            FillRR(wx, ry + 4, ww, RH - 8, 8, SBrush(FA(Alpha(0x000000, 60), fb)));
            StrokeRR(wx, ry + 4, ww, RH - 8, 8, Pen(FA(Alpha(0xFFFFFF, 14), fb), 1));
            double sx1 = wx + 6, sx2 = wx + 72;
            Txt("OPENS", sx1, ry + 7, 60, 11, HL.fXs, FA(Alpha(it.Open ? AccHi(acc, 0.3) : 0xFFC7CBE0, it.Open ? 220 : 110), fb), Fmt.C);
            Txt("CLOSES", sx2, ry + 7, 60, 11, HL.fXs, FA(Alpha(it.Close ? AccHi(acc, 0.3) : 0xFFC7CBE0, it.Close ? 220 : 110), fb), Fmt.C);
            string k1 = i + "|open", k2 = i + "|close";
            double c1 = Lgi.T.TryGetValue(k1, out var v1) ? v1 : (it.Open ? 1.0 : 0.0);
            c1 += ((it.Open ? 1.0 : 0.0) - c1) * EK(0.22); Lgi.T[k1] = c1;
            double c2 = Lgi.T.TryGetValue(k2, out var v2) ? v2 : (it.Close ? 1.0 : 0.0);
            c2 += ((it.Close ? 1.0 : 0.0) - c2) * EK(0.22); Lgi.T[k2] = c2;
            FFMTogDraw(sx1 + 6, ry + 21, 48, 18, c1, acc, hub.Hv(2110 + i), fb * (Lgi.On ? 1.0 : 0.6));
            FFMTogDraw(sx2 + 6, ry + 21, 48, 18, c2, acc, hub.Hv(2120 + i), fb * (Lgi.On ? 1.0 : 0.6));
            double hvX = hub.Hv(2130 + i);
            double cxx = ax + HL.abw - 21, cyy = ry + RH / 2.0;
            if (hvX > 0.02) FillEll(cxx - 9, cyy - 9, 18, 18, SBrush(FA(Alpha(C_BAD, R(50 * hvX)), fb)));
            var pnX = Pen(FA(Alpha(hvX > 0.2 ? C_BAD : 0xFFC7CBE0, 70 + 170 * hvX), fb), 1.4);
            Line(cxx - 3.5, cyy - 3.5, cxx + 3.5, cyy + 3.5, pnX); Line(cxx + 3.5, cyy - 3.5, cxx - 3.5, cyy + 3.5, pnX);
        }
        if (Total() > vis)
        {
            double sbA = hub.Hv(2140);
            double sw = 3 + 4 * sbA;
            double sx3 = ax + HL.abw - 12 - 3 * sbA;
            FillRR(sx3, ay + TOP, sw, vis, sw / 2, SBrush(FA(Alpha(0xFFFFFF, 20 + 28 * sbA), ff * cg0)));
            double thmb = Math.Max(22, vis * (vis / Total()));
            double ty = ay + TOP + (vis - thmb) * (scr / Math.Max(1, Total() - vis));
            FillRR(sx3, ty, sw, thmb, sw / 2, VBrush(sx3, ty, sw, thmb, FA(Alpha(AccHi(acc, 0.35), 200), ff * cg0), FA(Alpha(acc, 180), ff * cg0)));
        }
        Pop(stL);
        Footer(hub, ax, Lgiby(HL) + dy2, HL.abw, ff, now, acc, mt);
    }

    static string StatusLine()
    {
        int n = Lgi.Items.Count;
        if (n == 0) return "nothing listed - ADD APP to begin";
        int o = 0, c = 0;
        foreach (var it in Lgi.Items) { if (it.Open) o++; if (it.Close) c++; }
        if (!Lgi.On) return $"off  -  {n} app{(n == 1 ? "" : "s")} listed, none will open or close";
        return $"armed  -  {o} open{(o == 1 ? "s" : "")} with roblox, {c} close{(c == 1 ? "s" : "")}  -  roblox {(Lgi.Rbx == 1 ? "is up" : "is not running")}";
    }

    static void Footer(HubSurface hub, double px, double py, double pw, double ff, long now, uint acc, double mt)
    {
        var HL = hub.HL;
        double cg = Clamp((mt - 0.84) / 0.12, 0.0, 1.0);
        if (cg <= 0.01) return;
        double fb = ff * cg;
        double ph = Lgibh;
        py -= (1 - cg) * 12;
        FillRR(px, py, pw, ph, 12, VBrush(px, py, pw, ph, FA(Mix(0xFF141728, 0xFF1B2038, 0.5), fb), FA(0xFF10121C, fb)));
        MiniBackdrop(px, py, pw, ph, 12, acc, fb, now, 0.85);
        StrokeRR(px, py, pw, ph, 12, Pen(FA(Alpha(acc, 60 + 40 * mt), fb), 1.2));
        double gl = 30 * cg;
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 170 * cg), fb), 1.5);
        Line(px + 12, py + 0.6, px + 12 + gl, py + 0.6, pnG); Line(px + pw - 12 - gl, py + ph - 0.6, px + pw - 12, py + ph - 0.6, pnG);
        int stF = PushG();
        ClipRR(px, py, pw, ph, 12);
        if (Lgi.ActAt != 0 && now - Lgi.ActAt < 1200 && !HubState.LowPerf)
        {
            double ap = (now - Lgi.ActAt) / 1200.0;
            double bx = px - 40 + (pw + 80) * ap;
            FillRect(bx, py, 46, ph, VBrush(bx, py, 46, ph, Alpha(0xFFFFFF, 0), FA(Alpha(C_ON, R(30 * (1 - ap))), fb)));
        }
        bool armed = Lgi.On && Lgi.Items.Count > 0;
        uint dot = armed ? C_ON : 0xFF8A90A6;
        int da = armed ? R(170 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.004))) : 90;
        FillRR(px + 16, py + 13, 3, 14, 1.5, SBrush(FA(Alpha(dot, da), fb)));
        long mAge = Lgi.MsgAt != 0 ? now - Lgi.MsgAt : 999999;
        string shown = mAge < 6000 ? Lgi.Msg : StatusLine();
        uint scol = mAge < 6000 ? (Lgi.MsgCol == C_ACC ? acc : Lgi.MsgCol) : 0x8AC7CBE0;
        Txt(FFMElide(shown, Fonts.fHint, pw - 130), px + 28, py + 11, pw - 130, 18, Fonts.fHint, FA(scol, fb), Fmt.L);
        int nG = Lgi.Items.Count;
        if (nG > 0)
        {
            double segW = 18, segG = 4;
            double tw = nG * segW + (nG - 1) * segG;
            double tx = px + pw - 132 - tw;
            for (int k = 1; k <= nG; k++)
            {
                var it = Lgi.Items[k - 1];
                double sg = Clamp((cg - 0.30 - k * 0.05) / 0.40, 0.0, 1.0);
                if (sg <= 0.01) continue;
                double sx = tx + (k - 1) * (segW + segG);
                uint scl = !Lgi.On ? 0xFF8A90A6 : it.Open ? C_ON : it.Close ? AMBER : 0xFF8A90A6;
                bool lit = Lgi.On && (it.Open || it.Close);
                FillRR(sx, py + 30, segW, 5, 2.5, SBrush(FA(Alpha(0xFFFFFF, R(16 * sg)), fb)));
                if (lit) FillRR(sx, py + 30, segW, 5, 2.5, HBrush(sx, py + 30, segW, 5, FA(Alpha(AccHi(scl, 0.35), R(230 * sg)), fb), FA(Alpha(scl, R(230 * sg)), fb)));
            }
        }
        Txt("ARMED", px + pw - 118, py + 15, 50, 12, HL.fXs, FA(Alpha(Lgi.On ? AccHi(acc, 0.35) : 0xFFC7CBE0, Lgi.On ? 220 : 130), fb), Fmt.R);
        double mT = Lgi.T.TryGetValue("master", out var mv) ? mv : (Lgi.On ? 1.0 : 0.0);
        mT += ((Lgi.On ? 1.0 : 0.0) - mT) * EK(0.22); Lgi.T["master"] = mT;
        FFMTogDraw(px + pw - 64, py + 12, 48, 20, mT, acc, hub.Hv(2144), fb);
        double bg = Clamp((cg - 0.22) / 0.5, 0.0, 1.0);
        if (bg > 0.01)
        {
            double by2 = py + ph - 32 + (1 - bg) * 6;
            FFMBtn(2141, px + 16, by2, 104, 24, "ADD APP", acc, ff * bg, Lgi.Items.Count < Lgi.MAX ? 1 : 4);
            FFMBtn(2142, px + 128, by2, 104, 24, "OPEN ALL", acc, ff * bg, Lgi.Items.Count > 0 ? 0 : 4);
            FFMBtn(2143, px + 240, by2, 104, 24, "CLOSE ALL", acc, ff * bg, Lgi.Items.Count > 0 ? 3 : 4);
        }
        Pop(stF);
    }

    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double ax = HL.abx, ay = HL.aby;
        double vis = Vis();
        if (Total() > vis && ux >= ax + HL.abw - 12 && ux <= ax + HL.abw && uy >= ay + TOP && uy <= ay + TOP + vis) return 2140;
        if (uy >= ay + TOP - 4 && uy <= ay + TOP + vis && ux >= ax && ux <= ax + HL.abw)
            for (int i = 1; i <= Math.Min(Lgi.Items.Count, Lgi.MAX); i++)
            {
                double ry = ay + RowY(i) - Lgi.Scr;
                double top = Math.Max(ry, ay + TOP - 2), bot = Math.Min(ry + RH, ay + TOP + vis);
                if (uy >= top && uy <= bot)
                {
                    if (ux >= ax + HL.abw - 32 && ux <= ax + HL.abw - 8) return 2130 + i;
                    if (ux >= ax + HL.abw - 108 && ux <= ax + HL.abw - 42) return 2120 + i;
                    if (ux >= ax + HL.abw - 174 && ux <= ax + HL.abw - 110) return 2110 + i;
                    return 2100 + i;
                }
            }
        double fby = Lgiby(HL);
        double fy = fby + Lgibh - 32;
        if (uy >= fy && uy <= fy + 24)
        {
            if (ux >= ax + 16 && ux <= ax + 120) return 2141;
            if (ux >= ax + 128 && ux <= ax + 232) return 2142;
            if (ux >= ax + 240 && ux <= ax + 344) return 2143;
        }
        if (ux >= ax + HL.abw - 64 && ux <= ax + HL.abw - 16 && uy >= fby + 12 && uy <= fby + 32) return 2144;
        if (ux >= ax && ux <= ax + HL.abw && uy >= fby && uy <= fby + Lgibh) return 2145;
        if (ux >= ax && ux <= ax + HL.abw && uy >= ay && uy <= ay + Lgih) return 2146;
        return 0;
    }

    public static bool Click(HubSurface hub, int z)
    {
        if (z >= 2111 && z <= 2110 + Lgi.MAX) { Lgi.Toggle(z - 2111, "open"); }
        else if (z >= 2121 && z <= 2120 + Lgi.MAX) { Lgi.Toggle(z - 2121, "close"); }
        else if (z >= 2131 && z <= 2130 + Lgi.MAX) { Lgi.Remove(z - 2131); }
        else if (z >= 2101 && z <= 2100 + Lgi.MAX)
        {
            int i = z - 2101;
            if (i < Lgi.Items.Count) Reveal(Lgi.Items[i].Path);
        }
        else if (z == 2141) _ = PickAsync(hub);
        else if (z == 2142) Lgi.OpenAll();
        else if (z == 2143) Lgi.CloseAll();
        else if (z == 2144) Lgi.Master();
        else if (z == 2145 || z == 2146) { }
        else return false;
        if (z > 0) hub.ClickAt[z] = Clock.Tick;
        hub.Tim(Pace.TICK_A);
        return true;
    }
    /// <summary>A row click shows the file where it lives.</summary>
    static void Reveal(string path)
    {
        try
        {
            if (Os.IsWin) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
            else if (Os.IsMac) System.Diagnostics.Process.Start("open", new[] { "-R", path });
        }
        catch { }
    }
    /// <summary>LGIPickStart / LGIPicked: the platform's file picker.</summary>
    static async Task PickAsync(HubSurface hub)
    {
        if (_picking) return;
        if (Lgi.Items.Count >= Lgi.MAX) { Lgi.Say("LIST IS FULL (" + Lgi.MAX + ")", AMBER); return; }
        _picking = true;
        try
        {
            Lgi.Say("CHOOSE A PROGRAM", AMBER);
            var top = TopLevel.GetTopLevel(hub);
            IReadOnlyList<IStorageFile> files = Array.Empty<IStorageFile>();
            if (top?.StorageProvider is { CanOpen: true } sp)
                files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose a program to open and close with Roblox", AllowMultiple = false });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not { } sel) { Lgi.Say("NOTHING ADDED", 0xFFC7CBE0); return; }
            Lgi.AddItem(sel, true, true, true);
        }
        finally { _picking = false; hub.Tim(Pace.TICK_A); }
    }
    /// <summary>The wheel over THIS panel's list only, and only while it has somewhere left to go - see DopPanel.Wheel.</summary>
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (ux < HL.abx - 8 || ux > HL.abx + HL.abw + 8 || uy < HL.aby || uy > HL.aby + Vis()) return false;
        double max = Math.Max(0.0, Total() - Vis());
        if (max <= 0.5) return false;
        Lgi.ScrT = Clamp(Lgi.ScrT - delta * RG, 0.0, max);
        return true;
    }
}
