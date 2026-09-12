using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Shell.Hub;
using Yuri.Shell.Hub.Tabs;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.Cursor;

/// <summary>CurSystems / CurPreview / CurTile / CurZone / CurClick / CurDetail: module 3's panel. Zone ids are the .ahk's (1110-1152).</summary>
public static class CurPanel
{
    const uint AMBER = Cur.AMBER, C_ON = Cur.C_ON, C_ACC = Cur.C_ACC;
    const int PV = Cur.PV, CH = Cur.CH;
    static bool _picking;
    static readonly string[] Names = { "ARROW IMAGE", "SHIFT LOCK IMAGE", "CAMERA TOGGLE IMAGE", "TEXT CURSOR IMAGE", "APPLY TO ROBLOX", "RESTORE DEFAULT", "FAR CURSOR", "AUTO RE-APPLY" };
    static readonly string[] Hints = { "the pointer you see almost all the time", "the crosshair while shift lock is held", "the crosshair while camera toggle is on", "the caret over chat and text boxes",
                                       "write every staged slot over the roblox files", "put the original roblox cursors back", "also replace the distant-object pointer", "rewrite after roblox updates its client" };
    static readonly string[] Btns = { "BROWSE", "BROWSE", "BROWSE", "BROWSE", "APPLY", "RESTORE" };

    public static void Register()
    {
        Cur.Boot();
        Integrations.Panels[3] = Draw;
        Integrations.PanelZones[3] = Zone;
        Integrations.PanelClicks[3] = Click;
        Integrations.PanelWheels[3] = Wheel;
        Integrations.CursorState = () => (Cur.Slot.Count(s => s.Has), Cur.Slots.Length, Cur.Applied > 0);
    }
    public static bool Animating => Math.Abs(Cur.Scr - Cur.ScrT) > 0.3 || (Cur.MsgAt != 0 && Clock.Tick - Cur.MsgAt < 5300) || (Cur.ApplyAt != 0 && Clock.Tick - Cur.ApplyAt < 700)
        || Cur.Slot.Any(s => s.PickAt != 0 && Clock.Tick - s.PickAt < 700) || Math.Abs(Cur.FarT - (Cur.Far ? 1 : 0)) > 0.004 || Math.Abs(Cur.AutoT - (Cur.Auto ? 1 : 0)) > 0.004 || Cur.Auto || Cur.Applied > 0;
    public static double SysTotal(HubLayout HL) => Cur.SYSN * HL.ffrg;
    static int TileZone(int si) => 1140 + si;

    static void CurArrow(double x, double y, double s, Avalonia.Media.IPen? pn)
    {
        Line(x, y, x, y + 13 * s, pn); Line(x, y + 13 * s, x + 3.4 * s, y + 9.6 * s, pn); Line(x + 3.4 * s, y + 9.6 * s, x + 5.8 * s, y + 15 * s, pn);
        Line(x + 5.8 * s, y + 15 * s, x + 8 * s, y + 14 * s, pn); Line(x + 8 * s, y + 14 * s, x + 5.6 * s, y + 8.8 * s, pn); Line(x + 5.6 * s, y + 8.8 * s, x + 9.6 * s, y + 8.2 * s, pn); Line(x + 9.6 * s, y + 8.2 * s, x, y, pn);
    }
    static void SysIcon(int i, double cx, double cy, uint col, bool live, long now)
    {
        if (HubState.LowPerf) return;
        var pn = Pen(col, 1.5);
        if (i == 1)
        {
            StrokeRR(cx - 8, cy - 7, 16, 14, 2.5, pn);
            Line(cx - 6, cy + 5, cx - 1.5, cy - 1, pn); Line(cx - 1.5, cy - 1, cx + 1.5, cy + 2.5, pn); Line(cx + 1.5, cy + 2.5, cx + 3.5, cy + 0.5, pn); Line(cx + 3.5, cy + 0.5, cx + 6, cy + 5, pn);
            FillEll(cx + 1.5, cy - 5, 3, 3, SBrush(col));
        }
        else if (i == 2)
        {
            Ell(cx - 6, cy - 6, 12, 12, pn); Line(cx - 10, cy, cx - 7, cy, pn); Line(cx + 7, cy, cx + 10, cy, pn); Line(cx, cy - 10, cx, cy - 7, pn); Line(cx, cy + 7, cx, cy + 10, pn);
            FillEll(cx - 1.6, cy - 1.6, 3.2, 3.2, SBrush(col));
        }
        else if (i == 3)
        {
            Line(cx - 8, cy - 2, cx - 8, cy + 7, pn); Line(cx + 8, cy - 2, cx + 8, cy + 7, pn); Line(cx - 8, cy + 7, cx + 8, cy + 7, pn); Line(cx - 8, cy - 2, cx - 5, cy - 2, pn); Line(cx + 5, cy - 2, cx + 8, cy - 2, pn);
            Line(cx - 5, cy - 2, cx - 3, cy - 6, pn); Line(cx - 3, cy - 6, cx + 3, cy - 6, pn); Line(cx + 3, cy - 6, cx + 5, cy - 2, pn); Ell(cx - 4, cy, 8, 8, pn);
            FillEll(cx - 1.5, cy + 2.5, 3, 3, SBrush(col));
        }
        else if (i == 4)
        {
            StrokeRR(cx - 9, cy - 7, 18, 14, 2.5, Pen(FA(Alpha(col, 90), 1), 1.1));
            Line(cx - 3.5, cy - 4.5, cx + 3.5, cy - 4.5, pn); Line(cx - 3.5, cy + 4.5, cx + 3.5, cy + 4.5, pn); Line(cx, cy - 4.5, cx, cy + 4.5, pn);
            if (live) { double bl = (DecT(now) % 1060) < 530 ? 1.0 : 0.25; FillRR(cx - 0.9, cy - 4.5, 1.8, 9, 0.9, SBrush(FA(Alpha(col, R(220 * bl)), 1))); }
        }
        else if (i == 5)
        {
            CurArrow(cx - 4, cy - 8, 0.85, pn);
            if (live) { double a_ = (DecT(now) * 0.2 % 360) * 0.0174533; FillEll(cx + 4 + 2 * Math.Cos(a_), cy - 5 + 2 * Math.Sin(a_), 2.6, 2.6, SBrush(col)); }
        }
        else if (i == 6) { Arc(cx - 7, cy - 7, 14, 14, 110, 250, pn); Line(cx - 6.6, cy - 1.5, cx - 6.6, cy - 6.5, pn); Line(cx - 6.6, cy - 1.5, cx - 2, cy - 3.5, pn); }
        else if (i == 7) { CurArrow(cx - 9, cy - 7, 0.62, pn); CurArrow(cx + 1, cy - 2, 0.95, pn); }
        else
        {
            double sp = live ? DecT(now) * 0.12 % 360 : 30;
            Arc(cx - 7, cy - 7, 14, 14, sp, 265, pn);
            double a_ = (sp + 265) * 0.0174533;
            FillEll(cx + 7 * Math.Cos(a_) - 2, cy + 7 * Math.Sin(a_) - 2, 4, 4, SBrush(col));
        }
    }

    public static void Draw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        double mt = HL.modT;
        Cur.FarT += ((Cur.Far ? 1.0 : 0.0) - Cur.FarT) * EK(0.22); Cur.AutoT += ((Cur.Auto ? 1.0 : 0.0) - Cur.AutoT) * EK(0.22);
        int found = Cur.FoundN();
        Txt("SYSTEMS", ax, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, ff), Fmt.L);
        Txt(mt > 0.5 ? Cur.SYSN + " listed" : "0 listed", ax + 66, y0 + 36, 80, 14, HL.fS, FA(Alpha(acc, 60 + 120 * mt), ff), Fmt.L);
        if (mt > 0.4)
        {
            double fh = ff * Clamp((mt - 0.4) / 0.4, 0.0, 1.0);
            Txt(found > 0 ? found + " cursor file(s) detected" : "no roblox cursor files found", ax + HL.abw - 240, y0 + 30, 240, 22, Fonts.fHint, FA(Alpha(found > 0 ? 0xFFC7CBE0 : acc, 145), fh), Fmt.R);
        }
        double pah = 26 + (HL.ffh - 26) * mt;
        FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(Mix(0xFF141728, 0xFF181C34, mt), ff), FA(0xFF12141F, ff)));
        MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, ff, now, 0.9);
        Integrations.HubEmptyGhost(hub, ax, ay, pah, ff);
        StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, 70 + 90 * mt), ff), 1.2));
        double gl = 40 * mt;
        var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 200 * mt), ff), 1.6);
        Line(ax + 14, ay + 0.6, ax + 14 + gl, ay + 0.6, pnG); Line(ax + HL.abw - 14 - gl, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        ModRing(ax, ay, HL.abw, pah, 12, acc, ff, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (mt < 0.02) return;
        int st = PushG(); ClipRR(ax, ay, HL.abw, pah, 12);
        double[] ons = { 0, 0, 0, 0, 0, 0, Cur.FarT, Cur.AutoT };
        string[] subs = { "", "", "", "", "", "", Cur.Far ? "ON" : "OFF", Cur.Auto ? "ON" : "OFF" };
        int writeN = Cur.ActiveN();
        int nS = Cur.Slots.Length;
        for (int i = 1; i <= Cur.SYSN; i++)
        {
            double cg = Clamp((mt - 0.30 - Math.Min(i, 6) * 0.055) / 0.38, 0.0, 1.0);
            if (cg <= 0.01) continue;
            double fb = ff * cg;
            double ry = ay + 12 + (i - 1) * HL.ffrg - Cur.Scr;
            if (ry + 36 < ay + 4 || ry > ay + pah - 4) continue;
            double hv = Math.Max(hub.Hv(1110 + i), hub.Hv(1130 + i));
            double sld = (1 - cg) * 10, axs = ax + sld;
            bool isBtn = i <= 6;
            bool live = i <= nS ? Cur.Slot[i - 1].Has : i == nS + 1 ? writeN > 0 : i == nS + 2 ? Cur.Applied > 0 : ons[i - 1] > 0.5;
            if (hv > 0.01) FillRR(ax + 6, ry, HL.abw - 12, 36, 8, HBrush(ax + 6, ry, HL.abw - 12, 36, FA(Alpha(0xFFFFFF, 15 * hv), fb), FA(Alpha(0xFFFFFF, 3 * hv), fb)));
            FillRR(axs + 14, ry + 10, 3, 16, 1.5, VBrush(axs + 14, ry + 10, 3, 16, FA(Alpha(live ? AccHi(acc, 0.35) : 0xFFC7CBE0, live ? 235 : 60), fb), FA(Alpha(live ? acc : 0xFFC7CBE0, live ? 150 : 40), fb)));
            SysIcon(i, axs + 36, ry + 18, FA(Alpha(live ? acc : 0xFF9AA8C0, live ? 220 : 110), fb), live, now);
            double tw = (isBtn ? HL.abw - 118 : HL.abw - 158) - 54;
            Txt(Names[i - 1], axs + 54, ry + 4, tw, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.18), 235), fb), Fmt.L);
            Txt(FFMElide(Hints[i - 1], Fonts.fHint, tw), axs + 54, ry + 20, tw, 13, Fonts.fHint, FA(0x7EC7CBE0, fb), Fmt.L);
            if (isBtn)
            {
                if (i <= nS && Cur.Slot[i - 1].Has) Txt("set", axs + HL.abw - 176, ry + 11, 62, 14, HL.fXs, FA(Alpha(C_ON, 190), fb), Fmt.R);
                else if (i == nS + 1 && Cur.Applied > 0) Txt(Cur.Applied + " live", axs + HL.abw - 176, ry + 11, 62, 14, HL.fXs, FA(Alpha(C_ON, 200), fb), Fmt.R);
                else if (i == nS + 2) { int nb2 = Cur.BakCount(); if (nb2 > 0) Txt(nb2 + " saved", axs + HL.abw - 176, ry + 11, 62, 14, HL.fXs, FA(Alpha(acc, 185), fb), Fmt.R); }
                int kind = i <= nS ? (Cur.Slot[i - 1].Has ? 0 : 1) : i == nS + 1 ? (writeN > 0 ? 1 : 0) : 2;
                FFMBtn(1130 + i, axs + HL.abw - 108, ry + 5, 92, 26, Btns[i - 1], acc, fb, kind);
            }
            else
            {
                Txt(subs[i - 1], axs + HL.abw - 148, ry + 11, 70, 14, HL.fXs, FA(Alpha(ons[i - 1] > 0.5 ? C_ON : 0xFF9AA8C0, ons[i - 1] > 0.5 ? 200 : 95), fb), Fmt.R);
                FFMTogDraw(axs + HL.abw - 62, ry + 7, 44, 22, ons[i - 1], acc, hv, fb);
            }
        }
        double sysTot = SysTotal(HL), sysVis = HL.ffh - 20;
        if (sysTot > sysVis)
        {
            double sbS = Math.Max(hub.Hv(1152), HL.drag == 13 ? 1.0 : 0.0);
            double sw2 = 3 + 3 * sbS, sxs = ax + HL.abw - 6 - 3 * sbS;
            FillRR(sxs, ay + 10, sw2, sysVis, sw2 / 2, SBrush(FA(Alpha(0xFFFFFF, 18 + 26 * sbS), ff * mt)));
            double th2 = Math.Max(22, sysVis * (sysVis / sysTot));
            double ty2 = ay + 10 + (sysVis - th2) * (Cur.Scr / Math.Max(1, sysTot - sysVis));
            FillRR(sxs, ty2, sw2, th2, sw2 / 2, VBrush(sxs, ty2, sw2, th2, FA(Alpha(AccHi(acc, 0.35), 190 + 65 * sbS), ff * mt), FA(Alpha(acc, 170 + 60 * sbS), ff * mt)));
        }
        Pop(st);
        Cur.ScrT = Clamp(Cur.ScrT, 0.0, Math.Max(0.0, sysTot - sysVis));
        Cur.Scr += (Cur.ScrT - Cur.Scr) * EK(0.3); if (Math.Abs(Cur.Scr - Cur.ScrT) < 0.3) Cur.Scr = Cur.ScrT;
        Preview(hub, ax, ff, now, acc, mt, writeN);
    }

    static void Preview(HubSurface hub, double ax, double ff, long now, uint acc, double mt, int writeN)
    {
        var HL = hub.HL;
        double cp = Clamp((mt - 0.60) / 0.34, 0.0, 1.0);
        if (cp <= 0.01) return;
        Cur.SyncDefaults();
        double fp = ff * cp;
        double px = ax, pw = HL.abw, py = HL.ffby + (1 - cp) * 10;
        FillRR(px, py, pw, CH, 12, VBrush(px, py, pw, CH, FA(Mix(0xFF141728, 0xFF181C34, cp), fp), FA(0xFF11131F, fp)));
        MiniBackdrop(px, py, pw, CH, 12, acc, fp, now, 0.9);
        StrokeRR(px, py, pw, CH, 12, Pen(FA(Alpha(acc, 60 + 70 * cp), fp), 1.2));
        FillRR(px, py + 9, 3, 20, 1.5, SBrush(FA(Alpha(acc, 200 * cp), fp)));
        Txt("PREVIEW", px + 16, py + 8, 120, 16, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 225), fp), Fmt.L);
        bool on = Cur.Applied > 0;
        uint pc = on ? C_ON : 0xFF9AA8C0;
        double plx = px + pw - 218;
        FillRR(plx, py + 8, 100, 18, 5, SBrush(FA(Alpha(pc, on ? 40 : 22), fp)));
        FillEll(plx + 8, py + 14.5, 5, 5, SBrush(FA(Alpha(pc, on ? 180 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.0026)) : 90), fp)));
        Txt(on ? "APPLIED" : "NOT APPLIED", plx + 18, py + 8, 76, 18, HL.fXs, FA(Alpha(AccHi(pc, 0.25), on ? 235 : 150), fp), Fmt.L);
        FFMBtn(1150, px + pw - 108, py + 6, 92, 22, "OPEN FOLDER", acc, fp, 0);
        for (int si = 1; si <= Cur.Slots.Length; si++) Tile(hub, si, px + 14 + (si - 1) * (PV + 10), py + 32, TileZone(si), fp, now, acc, cp);
        double cGap = 10, cHalf = Math.Floor((pw - 28 - cGap) / 2), lx = px + 14, rx = px + 14 + cHalf + cGap, rowY = py + 130;
        for (int si = 1; si <= Cur.Slots.Length; si++) Row(hub, lx, rowY + (si - 1) * 14, cHalf, Cur.Slots[si - 1].Label, Cur.DimText(si), Cur.Slot[si - 1].Has ? 0xFFC7CBE0 : 0xFF9AA8C0, fp);
        string which = Cur.Far ? "arrow + far" : "arrow only";
        Row(hub, rx, rowY, cHalf, "TARGETS", writeN > 0 ? writeN + " file(s)  \u00B7  " + which : "nothing to write", writeN > 0 ? 0xFFC7CBE0 : acc, fp);
        int nb3 = Cur.BakCount();
        Row(hub, rx, rowY + 14, cHalf, "BACKUP", nb3 > 0 ? nb3 + " original(s) held" : "none held yet", nb3 > 0 ? 0xFFC7CBE0 : 0xFF9AA8C0, fp);
        double mf = Cur.MsgAt != 0 ? Clamp(1 - (now - Cur.MsgAt - 4200) / 900.0, 0.0, 1.0) : 1.0;
        if (mf > 0.01)
        {
            uint mc = Cur.MsgCol == C_ACC ? acc : Cur.MsgCol != 0 ? Cur.MsgCol : 0xFFC7CBE0;
            FillRR(rx, rowY + 30, 3, 12, 1.5, SBrush(FA(Alpha(mc, R(150 * mf)), fp)));
            Txt(FFMElide(Cur.Msg, HL.fXs, cHalf - 12), rx + 9, rowY + 28, cHalf - 12, 15, HL.fXs, FA(Alpha(AccHi(mc, 0.2), R(225 * mf)), fp), Fmt.L);
        }
    }
    static void Row(HubSurface hub, double x, double y, double w, string label, string value, uint col, double ff)
    {
        Txt(label, x, y, 58, 16, hub.HL.fXs, FA(0x72C7CBE0, ff), Fmt.L);
        Txt(FFMElide(value, Fonts.fHint, w - 62), x + 62, y - 1, w - 62, 18, Fonts.fHint, FA(Alpha(col, 200), ff), Fmt.L);
    }
    static void Tile(HubSurface hub, int si, double tx, double ty, int zone, double fp, long now, uint acc, double cp)
    {
        var HL = hub.HL;
        var s = Cur.Slot[si - 1];
        bool own = s.Bmp is not null && s.Bw != 0 && s.Bh != 0;
        var bmp = own ? s.Bmp : s.Dbmp;
        int bw = own ? s.Bw : s.Dbw, bh = own ? s.Bh : s.Dbh;
        double pscl = 1.0;
        if (s.PickAt != 0 && now - s.PickAt < 520) pscl = 0.86 + 0.14 * EBackOut(Clamp((now - s.PickAt) / 520.0, 0.0, 1.0), 2.0);
        double hvP = hub.Hv(zone);
        int stT = PushXform(tx + PV / 2.0, ty + PV / 2.0, pscl * (1 + 0.025 * Ease3(hvP)), 0);
        FillRR(tx, ty, PV, PV, 10, SBrush(FA(0xFF0B0D16, fp)));
        int stTC = PushG(); ClipRR(tx, ty, PV, PV, 10);
        var pnH = Pen(FA(Alpha(0xFFFFFF, 9), fp), 1);
        for (double k = -PV; k < PV; k += 11) Line(tx + k, ty + PV, tx + k + PV, ty, pnH);
        if (bmp is not null && bw > 0 && bh > 0)
        {
            double fit = Math.Min((PV - 20) / (double)bw, (PV - 20) / (double)bh);
            double iw = bw * fit, ih = bh * fit;
            Img.DrawRR(bmp, tx + (PV - iw) / 2, ty + (PV - ih) / 2, iw, ih, 0, fp * (own ? 1.0 : 0.62));
        }
        else
        {
            int stR = PushXform(tx + PV / 2.0, ty + PV / 2.0, 1, DecT(now) * 0.03 % 360);
            var pnD = Pen(FA(Alpha(0xFFC7CBE0, 55), fp), 1.2); PenDash(pnD, 1);
            Ell(tx + PV / 2.0 - 25, ty + PV / 2.0 - 25, 50, 50, pnD);
            Pop(stR);
            CurArrow(tx + PV / 2.0 - 6, ty + PV / 2.0 - 11, 1.4, Pen(FA(Alpha(0xFFC7CBE0, 70), fp), 1.4));
        }
        if (Cur.ApplyAt != 0 && now - Cur.ApplyAt < 620 && own)
        {
            double sw = Clamp((now - Cur.ApplyAt) / 620.0, 0.0, 1.0);
            double sxp = tx - 26 + (PV + 52) * Ease3(sw), sa = Math.Sin(Math.PI * sw);
            FillRect(sxp - 14, ty, 14, PV, HBrush(sxp - 14, ty, 14, PV, FA(Alpha(acc, 0), fp), FA(Alpha(AccHi(acc, 0.5), R(120 * sa)), fp)));
            FillRect(sxp, ty, 14, PV, HBrush(sxp, ty, 14, PV, FA(Alpha(AccHi(acc, 0.5), R(120 * sa)), fp), FA(Alpha(acc, 0), fp)));
        }
        Pop(stTC);
        uint fcol = own ? acc : 0xFF6E7590;
        StrokeRR(tx, ty, PV, PV, 10, Pen(FA(Alpha(fcol, 85 + 110 * Ease3(hvP) + (own ? 30 * Math.Abs(Math.Sin(DecT(now) * 0.0022)) : 0)), fp), 1.3));
        var pnC = Pen(FA(Alpha(acc, 120 + 90 * Ease3(hvP)), fp), 1.4);
        for (int q = 1; q <= 4; q++)
        {
            double cxq = (q == 1 || q == 3) ? tx + 4 : tx + PV - 4, cyq = q <= 2 ? ty + 4 : ty + PV - 4;
            int sxq = (q == 1 || q == 3) ? 1 : -1, syq = q <= 2 ? 1 : -1;
            Line(cxq, cyq, cxq + 8 * sxq, cyq, pnC); Line(cxq, cyq, cxq, cyq + 8 * syq, pnC);
        }
        if (!own && bmp is not null)
        {
            bool isMod = s.Dsrc == 1;
            string bg = isMod ? "MOD" : "DEFAULT"; double bgw = isMod ? 40 : 50;
            FillRR(tx + 6, ty + PV - 21, bgw, 15, 4, SBrush(FA(Alpha(0xFF0B0D16, 210), fp)));
            Txt(bg, tx + 6, ty + PV - 21, bgw, 15, HL.fXs, FA(isMod ? Alpha(AMBER, 190) : 0x8AC7CBE0u, fp), Fmt.C);
        }
        if (s.PickAt != 0 && now - s.PickAt < 620)
        {
            double e = Ease3((now - s.PickAt) / 620.0), ex = e * 16;
            StrokeRR(tx - ex, ty - ex, PV + ex * 2, PV + ex * 2, 10 + ex * 0.4, Pen(FA(Alpha(acc, 180 * (1 - e)), fp), 2.2 * (1 - e) + 0.3));
        }
        Pop(stT);
        Txt(Cur.Slots[si - 1].Label, tx, ty + PV + 2, PV, 13, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, acc, own ? 0.45 : 0.0), own ? 210 : 130), fp), Fmt.C);
        string nm = own ? Path.GetFileName(s.Src != "" ? s.Src : Cur.SlotSrc(si)) : bmp is not null ? Cur.SrcName(si) : "not set";
        Txt(FFMElide(nm, HL.fXs, PV), tx, ty + PV + 15, PV, 13, HL.fXs, FA(Alpha(0xFFE8EAF6, own ? 190 : 110), fp), Fmt.C);
    }

    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double ax = HL.abx, ay = HL.aby;
        if (SysTotal(HL) > HL.ffh - 20 && ux >= ax + HL.abw - 12 && ux <= ax + HL.abw && uy >= ay + 10 && uy <= ay + HL.ffh - 10) return 1152;
        if (uy >= ay + 6 && uy <= ay + HL.ffh - 6 && ux >= ax && ux <= ax + HL.abw)
            for (int i = 1; i <= Cur.SYSN; i++)
            {
                double ry = ay + 12 + (i - 1) * HL.ffrg - Cur.Scr;
                if (uy >= ry && uy <= ry + 36)
                {
                    if (i <= Cur.SYSN - 2 && ux >= ax + HL.abw - 108 && ux <= ax + HL.abw - 16) return 1130 + i;
                    if (i > Cur.SYSN - 2 && ux >= ax + HL.abw - 64 && ux <= ax + HL.abw - 16) return 1130 + i;
                    return 1110 + i;
                }
            }
        double px = ax, py = HL.ffby, pw = HL.abw;
        if (ux >= px && ux <= px + pw && uy >= py && uy <= py + CH)
        {
            if (ux >= px + pw - 108 && ux <= px + pw - 16 && uy >= py + 6 && uy <= py + 28) return 1150;
            if (uy >= py + 32 && uy <= py + 32 + PV)
                for (int si = 1; si <= Cur.Slots.Length; si++) { double tx = px + 14 + (si - 1) * (PV + 10); if (ux >= tx && ux <= tx + PV) return TileZone(si); }
            return 1151;
        }
        return 0;
    }
    public static bool Click(HubSurface hub, int z)
    {
        int nS = Cur.Slots.Length;
        if (z > 1140 && z <= 1140 + nS) _ = PickAsync(hub, z - 1140);
        else if (z > 1130 && z <= 1130 + nS) _ = PickAsync(hub, z - 1130);
        else if (z == 1130 + nS + 1) Cur.Apply();
        else if (z == 1130 + nS + 2) Cur.Restore();
        else if (z >= 1111 && z <= 1110 + Cur.SYSN) OpenDetail(z - 1110);
        else if (z == 1130 + Cur.SYSN - 1) { Cur.Far = !Cur.Far; Ini.Write(Paths.IniFile, "cursor", "far", Cur.Far ? 1 : 0); Cur.Say(Cur.Far ? "FAR CURSOR ON" : "FAR CURSOR OFF", 0); }
        else if (z == 1130 + Cur.SYSN) { Cur.Auto = !Cur.Auto; Ini.Write(Paths.IniFile, "cursor", "auto", Cur.Auto ? 1 : 0); Cur.ManageAuto(); Cur.Say(Cur.Auto ? "AUTO RE-APPLY ON" : "AUTO RE-APPLY OFF", 0); }
        else if (z == 1150) Cur.OpenFolder();
        else if (z == 1151 || z == 1152) { }
        else return false;
        if (z > 0) hub.ClickAt[z] = Clock.Tick;
        hub.Tim(Pace.TICK_A);
        return true;
    }
    static async Task PickAsync(HubSurface hub, int si)
    {
        if (_picking) return;
        _picking = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            IReadOnlyList<IStorageFile> files = Array.Empty<IStorageFile>();
            if (top?.StorageProvider is { CanOpen: true } sp)
                files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose the " + Cur.Slots[si - 1].Label + " image", AllowMultiple = false,
                    FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" } }, FilePickerFileTypes.All } });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not { } sel) { Cur.Say("NOTHING PICKED", 0xFFC7CBE0); return; }
            Cur.SetImage(si, sel);
        }
        finally { _picking = false; hub.Tim(Pace.TICK_A); }
    }
    /// <summary>CurDragScroll(sy): the scrollbar thumb follows the pointer.</summary>
    public static void DragScroll(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        double vis = HL.ffh - 20, tot = SysTotal(HL);
        double thmb = Math.Max(22, vis * (vis / Math.Max(1, tot)));
        Cur.ScrT = HubUI.ScrollFromY(HL, uy, HL.aby + 10, vis, tot, vis, thmb, Cur.Scr); Cur.Scr = Cur.ScrT;
        hub.Tim(Pace.TICK_A);
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (ux < HL.abx - 8 || ux > HL.abx + HL.abw + 8 || uy < HL.aby || uy > HL.aby + HL.ffh) return false;
        Cur.ScrT = Clamp(Cur.ScrT - delta * HL.ffrg, 0.0, Math.Max(0.0, SysTotal(HL) - (HL.ffh - 20)));
        return true;
    }
    /// <summary>CurDetail(i): the eight explainers.</summary>
    public static void OpenDetail(int i)
    {
        string[] ttl = Names, hnt = Hints;
        string[] bdy =
        {
            "This is ArrowCursor and ArrowFarCursor - the pointer Roblox draws in almost every menu and over most of the world. Picking an image stages it; nothing touches the client until you press APPLY. Square images around 64x64 hold up best, since Roblox scales whatever you give it.",
            "The reticle Roblox swaps in while shift lock is held. It is a separate file from the arrow, so a set can replace one and leave the other alone. Staged the same way - APPLY is still what writes it.",
            "CrossMouseIcon.png - the crosshair for camera toggle, which is a different Roblox feature from shift lock and a different file. Tap right mouse button to enter toggle mode: the cursor parks above your head and locks there, and tapping again leaves. It works in both first and third person. Shift lock has its own crosshair in the slot above, so replacing one leaves the other alone. Note this file lives in textures\\Cursors rather than textures\\Cursors\\KeyboardMouse where the arrow pair sits - the panel handles that, but it matters if you ever place one by hand. Staged like the other slots and written by the same APPLY step.",
            "IBeamCursor.png - the caret Roblox shows whenever the pointer is over something you can type into: the chat entry box, a text field in a game's interface, a search box in the menus. It is the one cursor here you see outside of gameplay as much as inside it. Unlike the other three this one is not a crosshair or a pointer, so an image that reads as a vertical bar keeps it recognisable - a replacement shaped like an arrow will work, but you lose the only signal that tells you a field will accept typing. Staged like the rest and written by the same APPLY step.",
            "Copies every staged slot over the files in the Roblox install, backing up whatever was there first. The backup is keyed to the install path, so two installs never overwrite each other's originals. A Roblox update replaces these files, which is what AUTO RE-APPLY watches for.",
            "Puts the backed-up originals back and clears the staged set. Safe to press at any time - if no backup exists for a slot it is left alone rather than guessed at.  It can only undo what THIS hub did. A backup is taken the first time a file is overwritten, so a cursor put there by something else - a Bloxstrap mod, a texture pack, a manual copy - has no original stored here and RESTORE will say so rather than invent one. The tile captions tell you which is which: 'roblox default' is the client's own file, while 'bloxstrap mod' means Modifications is copying an image over the client on every launch and RESTORE has no original of its own to put back.  To get Roblox's own cursors back in that case, take the file out of Bloxstrap's Modifications folder if that is where it came from - OPEN FOLDER goes straight there - and relaunch. Roblox restores its own content on its next update either way.",
            "Roblox draws a second, smaller pointer for things far away. With this on, that slot is replaced from the same image as the arrow, so the pointer does not change shape at distance. Off, Roblox keeps its own.",
            "A Roblox update replaces the cursor files, which quietly puts the default pointers back. This watches for that and rewrites your set afterwards, so a set applied once stays applied across updates.",
        };
        if (i < 1 || i > bdy.Length) return;
        int nS = Cur.Slots.Length;
        Detail.OpenSheet(ttl[i - 1], hnt[i - 1], bdy[i - 1], i <= nS ? "CURSOR  -  IMAGE SLOT" : i <= nS + 2 ? "CURSOR  -  ACTION" : "CURSOR");
    }
}
