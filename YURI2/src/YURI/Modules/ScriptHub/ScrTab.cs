using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.ScriptHub;

public static class ScrTab
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399;
    const double TBW = 54, TBH = 26, FDH = 26, EDH = 122, LSH = 64, LINEH = 15;
    static double Tby(HubLayout HL) => HL.cty + 38;
    static double TabY(HubLayout HL) => HL.cty + 72;
    static double Fdy(HubLayout HL) => HL.cty + 100;
    static double Edy(HubLayout HL) => HL.cty + 134;
    static double Lsy(HubLayout HL) => Edy(HL) + EDH + 52;
    static double CeY(HubLayout HL) => HL.cty + 66 + FDH + 12;
    static double CeBtnY() => HubLayout.pd + HubLayout.ch - 64;
    static double CeH(HubLayout HL) => CeBtnY() - 12 - CeY(HL);
    static int CeVis(HubLayout HL) => Math.Max(3, (int)Math.Floor((CeH(HL) - 16) / LINEH));
    static double ChW(HubLayout HL) => Fonts.Adv("0000000000", HL.fM) / 10;
    static double LibT, ExpT, TabU = 1;
    static bool _picking;
    public static bool Animating => Scr.Focus == 1 || Math.Abs(Scr.LsSc - Scr.LsScT) > 0.3 || Math.Abs(Scr.Hsc - Scr.HscT) > 0.3 || Math.Abs(TabU - Scr.Cur) > 0.01 || Scr.WarnAt != 0 && Clock.Tick - Scr.WarnAt < 2700
        || Scr.CardAt.Count > 0 || Scr.TogAt.Count > 0 || (Scr.PlaceAt != 0 && Clock.Tick - Scr.PlaceAt < 720) || (Scr.OutAt != 0 && Clock.Tick - Scr.OutAt < 720) || Scr.DelAt != 0 || Scr.LibDelAt != 0
        || Math.Abs(LibT - (Scr.LibOpen ? 1 : 0)) > 0.01 || Math.Abs(ExpT - (Scr.ExpOpen ? 1 : 0)) > 0.01 || Scr.List.Any(it => it.Pid != 0) || (Scr.EdAt != 0 && Clock.Tick - Scr.EdAt < 540) || (Scr.CeAt != 0 && Clock.Tick - Scr.CeAt < 320);
    static double ShiftAll() => Scr.Shift() + 26 * ExpT;
    static int CardsVis(HubLayout HL) { double avail = HubLayout.pd + HubLayout.ch - 22 - Lsy(HL); return Math.Max(1, (int)Math.Floor((avail + 6) / (LSH + 6))); }
    static double ListW(HubLayout HL) => HL.ctw - (Scr.LsMax > 0 ? 14 : 0);
    static double EdY(HubLayout HL) => Scr.CeIdx != 0 ? CeY(HL) : Edy(HL) + ShiftAll();
    static double EdH(HubLayout HL) => Scr.CeIdx != 0 ? CeH(HL) : EDH;
    static int EdVis(HubLayout HL) => Math.Max(1, (int)Math.Floor((EdH(HL) - 16) / LINEH));
    static double EdTxtW(HubLayout HL) => HL.ctw - 52;
    static double RowsH() => (Scr.List.Count > 0 ? Scr.List.Count : 1) * (LSH + 6);
    static double CardMax(HubLayout HL) => Math.Max(0.0, RowsH() + 46 - CardsVis(HL) * (LSH + 6));

    public static void Register(HubSurface hub)
    {
        Scr.Register();
        hub.TabBodies[3] = (f, dx, dy2, now) => Draw(hub, f, dx, dy2, now);
        FfmField.TextX = Wrap(FfmField.TextX, hub);
    }
    static Func<string, double> Wrap(Func<string, double> inner, HubSurface hub) => mode => mode == "sn" ? hub.HL.ctx + 10 : mode == "sd" ? hub.HL.ctx + 200 + 10 : inner(mode);

    // ---- drawing ----
    public static void Draw(HubSurface hub, double f, double dx, double dy2, long now)
    {
        var HL = hub.HL;
        uint hubCur = HubState.Cur;
        double x0 = HL.ctx + dx, y0 = HL.cty + dy2;
        LibT += ((Scr.LibOpen ? 1.0 : 0.0) - LibT) * EK(0.2);
        ExpT += ((Scr.ExpOpen ? 1.0 : 0.0) - ExpT) * EK(0.22);
        TabU += (Scr.Cur - TabU) * EK(0.25);
        Scr.LsSc += (Scr.LsScT - Scr.LsSc) * EK(0.3); if (Math.Abs(Scr.LsSc - Scr.LsScT) < 0.3) Scr.LsSc = Scr.LsScT;
        Scr.Hsc += (Scr.HscT - Scr.Hsc) * EK(0.35); if (Math.Abs(Scr.Hsc - Scr.HscT) < 0.3) Scr.Hsc = Scr.HscT;
        if (Scr.TokStale) Scr.Retok();          // count too, not just the dirty flag: a cut removes lines
        double emb2G = 0.5 + 0.5 * Math.Sin(DecT(now) * 0.0026);
        if (Scr.CeIdx != 0) { DrawInline(hub, x0, y0, dx, dy2, f, now, hubCur, emb2G); return; }
        double tby = Tby(HL) + dy2;
        string[] btns = { "NEW", "PASTE", "FILE", "CHECK", "SAVE", "EXPORT", "SAVED", "CLEAR" };
        for (int j = 1; j <= 8; j++)
        {
            double bx2 = x0 + (j - 1) * (TBW + 5);
            int zid = j == 4 ? 68 : j == 5 ? 55 : j == 6 ? 56 : j == 7 ? 59 : j == 8 ? 65 : 60 + j;
            uint ccol = j == 4 ? Mix(hubCur, 0xFF34D399, 0.35) : j == 7 && Scr.LibOpen ? AccHi(hubCur, 0.3) : hubCur;
            double hvB = hub.Hv(zid);
            ChipDraw(bx2, tby, TBW, TBH, btns[j - 1], hvB, ccol, f);
            if (hvB > 0.01) FillRR(bx2 + TBW / 2 - (TBW / 2 - 8) * hvB, tby + TBH - 2.4, (TBW - 16) * hvB, 2, 1, SBrush(FA(Alpha(ccol, 200 * hvB), f)));
        }
        for (int q = 1; q <= 2; q++)
        {
            double sxj = x0 + (q == 1 ? 3 : 4) * (TBW + 5) - 4;
            Line(sxj, tby + 5, sxj, tby + TBH - 5, Pen(FA(0x26FFFFFF, f), 1));
            FillEll(sxj - 1.2, tby + TBH / 2 - 1.2, 2.4, 2.4, SBrush(FA(Alpha(hubCur, 90), f)));
        }
        double wsa = ShiftAll(), tabY2 = TabY(HL) + dy2 + wsa;
        int tn = Scr.Tabs.Count;
        double tw3 = Math.Min(118, (HL.ctw - 130) / tn);
        FillRR(x0 - 4, tabY2 - 3, HL.ctw + 8, 28, 9, SBrush(FA(0x08FFFFFF, f)));
        FadeLine(x0 - 4, x0 + HL.ctw + 4, tabY2 + 25, 0x16FFFFFF, f);
        double agx = x0 + (TabU - 1) * (tw3 + 4);
        FillRR(agx - 3, tabY2 + 16, tw3 + 6, 10, 5, SBrush(FA(Alpha(hubCur, 26), f)));
        FillRR(agx + 8, tabY2 + 23.4, tw3 - 16, 2.2, 1.1, SBrush(FA(Alpha(hubCur, 190), f)));
        for (int i = 1; i <= tn; i++)
        {
            double tx3 = x0 + (i - 1) * (tw3 + 4);
            bool act = i == Scr.Cur;
            long brn = Scr.Tabs[i - 1].Born;
            double bage = brn != 0 ? Clamp((now - brn) / 260.0, 0.0, 1.0) : 1.0, bea = 1 - Math.Pow(1 - bage, 3);
            double hvT = hub.Hv(200 + i);
            string tnm = act ? (Scr.Name != "" ? Scr.Name : "TAB " + i) : (Scr.Tabs[i - 1].Name != "" ? Scr.Tabs[i - 1].Name : "TAB " + i);
            double f2 = f * bea, ty4 = tabY2 + (1 - bea) * 7;
            FillRR(tx3, ty4, tw3, 22, 7, VBrush(tx3, ty4, tw3, 22, FA(Mix(0xFF141728, 0xFF1E2340, Math.Max(hvT, act ? 0.9 : 0)), f2), FA(0xFF10121C, f2)));
            MicroBackdrop(tx3, ty4, tw3, 22, 7, hubCur, f2, now, 0.85);
            StrokeRR(tx3, ty4, tw3, 22, 7, Pen(FA(Alpha(act ? hubCur : 0xFFFFFF, act ? 160 : 24 + 50 * hvT), f2), 1));
            if (brn != 0 && bage < 1) { double ex4 = bage * 8; StrokeRR(tx3 - ex4, ty4 - ex4 * 0.6, tw3 + ex4 * 2, 22 + ex4 * 1.2, 7 + ex4 * 0.3, Pen(FA(Alpha(hubCur, 150 * (1 - bage)), f), 1.6 * (1 - bage) + 0.3)); }
            int ei2 = act ? Scr.EditIdx : Scr.Tabs[i - 1].EditIdx;
            if (ei2 != 0) FillEll(tx3 + 6, ty4 + 8.5, 5, 5, SBrush(FA(Alpha(AMBER, 210), f2)));
            Txt(tnm, tx3 + (ei2 != 0 ? 15 : 9), ty4 + 3, tw3 - (tn > 1 ? 30 : 16), 16, HL.fXs, FA(Alpha(act ? 0xFFFFFFFF : 0xFFC7CBE0, act ? 235 : 130 + 80 * hvT), f2), Fmt.L);
            if (tn > 1) { double hvX = hub.Hv(210 + i); var pnX = Pen(FA(Alpha(hubCur, 100 + 130 * hvX), f2), 1.3); Line(tx3 + tw3 - 12, ty4 + 8, tx3 + tw3 - 6, ty4 + 14, pnX); Line(tx3 + tw3 - 12, ty4 + 14, tx3 + tw3 - 6, ty4 + 8, pnX); }
        }
        double px3 = x0 + tn * (tw3 + 4), hvP2 = hub.Hv(199);
        FillRR(px3, tabY2, 22, 22, 7, SBrush(FA(Alpha(hubCur, 16 + 34 * hvP2), f)));
        MicroBackdrop(px3, tabY2, 22, 22, 7, hubCur, f, now, 0.85);
        StrokeRR(px3, tabY2, 22, 22, 7, Pen(FA(Alpha(hubCur, 70 + 110 * hvP2), f), 1));
        var pnP = Pen(FA(Alpha(AccHi(hubCur, 0.4), 190 + 60 * hvP2), f), 1.5);
        Line(px3 + 6.5, tabY2 + 11, px3 + 15.5, tabY2 + 11, pnP); Line(px3 + 11, tabY2 + 6.5, px3 + 11, tabY2 + 15.5, pnP);
        if (Scr.Cur > 1) ChipDraw(x0 + HL.ctw - 74, tabY2, 74, 22, "IN MAIN", hub.Hv(57), AMBER, f);
        if (ExpT > 0.02)
        {
            double exf = ExpT, ex0 = x0 + 5 * (TBW + 5), eyy = tby + TBH + 6 - (1 - exf) * 10;
            FillPolyPts(new[] { (ex0 + 21, eyy + 1), (ex0 + 33, eyy + 1), (ex0 + 27, eyy - 6) }, SBrush(FA(Alpha(hubCur, 60 * exf), f)));
            FadeLine(ex0 - 6, ex0 + 104, eyy - 1, Alpha(hubCur, R(50 * exf)), f);
            ChipDraw(ex0, eyy, 44, 20, ".AHK", hub.Hv(51) * exf, hubCur, f * exf);
            ChipDraw(ex0 + 50, eyy, 44, 20, ".TXT", hub.Hv(52) * exf, hubCur, f * exf);
        }
        string plc = Scr.EditIdx >= 1 && Scr.EditIdx <= Scr.List.Count ? "UPDATE" : "PLACE";
        double plX = x0 + HL.ctw - 84, hvPl = hub.Hv(64);
        bool hasTx = !(Scr.Lines.Count == 1 && Scr.Lines[0] == "");
        if (hasTx) StrokeRR(plX - 3, tby - 3, 90, TBH + 6, 10, Pen(FA(Alpha(hubCur, 34 + 30 * emb2G), f), 1.4));
        int stPl = PushXform(plX + 42, tby + TBH / 2, 1 + 0.05 * hvPl, 0);
        FillRR(plX, tby, 84, TBH, 7, VBrush(plX, tby, 84, TBH, FA(Alpha(hubCur, 64 + 60 * hvPl + 12 * emb2G), f), FA(Alpha(hubCur, 26 + 30 * hvPl), f)));
        MicroBackdrop(plX, tby, 84, TBH, 7, hubCur, f, now, 0.9);
        FillRR(plX, tby, 84, 10, 7, VBrush(plX, tby, 84, 10, FA(Alpha(0xFFFFFF, 26), f), Alpha(0xFFFFFF, 0)));
        StrokeRR(plX, tby, 84, TBH, 7, Pen(FA(Alpha(AccHi(hubCur, 0.25), 150 + 90 * hvPl), f), 1.1));
        var pnA = Pen(FA(Alpha(0xFFFFFF, 200 + 55 * hvPl), f), 1.6);
        Line(plX + 14, tby + TBH / 2, plX + 21, tby + TBH / 2, pnA); Line(plX + 21, tby + TBH / 2, plX + 17.5, tby + TBH / 2 - 3.5, pnA); Line(plX + 21, tby + TBH / 2, plX + 17.5, tby + TBH / 2 + 3.5, pnA);
        Txt(plc, plX + 22, tby - 1, 60, TBH, Fonts.fBadge, FA(Alpha(0xFFFFFF, 225 + 30 * hvPl), f), Fmt.C);
        Pop(stPl);
        if (Scr.PlaceAt != 0 && now - Scr.PlaceAt < 700) { double e = Ease3((now - Scr.PlaceAt) / 700.0), ex = e * 12; StrokeRR(x0 + HL.ctw - 84 - ex, tby - ex * 0.6, 84 + ex * 2, TBH + ex * 1.2, 7 + ex * 0.3, Pen(FA(Alpha(C_ON, 190 * (1 - e)), f), 2.2 * (1 - e) + 0.4)); }
        double wsh = Scr.Shift();
        if (wsh > 0.4)
        {
            double wo = wsh / 34.0, wby = tby + TBH + 8 - (1 - wo) * 24;
            FillRR(x0 + 2, wby + 2, HL.ctw - 4, 26, 8, SBrush(FA(Alpha(0x000000, 50 * wo), f)));
            FillRR(x0, wby, HL.ctw, 26, 8, VBrush(x0, wby, HL.ctw, 26, FA(Alpha(hubCur, 46 * wo), f), FA(Alpha(hubCur, 22 * wo), f)));
            MicroBackdrop(x0, wby, HL.ctw, 26, 8, hubCur, f, now, 0.8);
            StrokeRR(x0, wby, HL.ctw, 26, 8, Pen(FA(Alpha(hubCur, 150 * wo), f), 1.2));
            FillRR(x0, wby + 6, 3, 14, 1.5, SBrush(FA(Alpha(hubCur, 225 * wo), f)));
            double wcx = x0 + 22, wcy = wby + 13;
            var pnW = Pen(FA(Alpha(hubCur, 235 * wo), f), 1.4);
            Ell(wcx - 7, wcy - 7, 14, 14, pnW); Line(wcx, wcy - 4, wcx, wcy + 1, pnW);
            FillEll(wcx - 1, wcy + 2.6, 2, 2, SBrush(FA(Alpha(hubCur, 245 * wo), f)));
            Txt(Scr.WarnMsg, x0 + 38, wby - 1, HL.ctw - 60, 26, Fonts.fHint, FA(Alpha(AccHi(hubCur, 0.45), 245 * wo), f), Fmt.L);
            double wpr = Clamp(1 - ((now - Scr.WarnAt) - 220) / 2120.0, 0.0, 1.0);
            FillRR(x0 + 4, wby + 24, (HL.ctw - 8) * wpr, 2, 1, SBrush(FA(Alpha(hubCur, 120 * wo), f)));
        }
        double fdy = Fdy(HL) + dy2 + wsa;
        FldBox(hub, x0, fdy, 190, "sn", Scr.Name, "script name", f, 66, now, hubCur);
        FldBox(hub, x0 + 200, fdy, HL.ctw - 200, "sd", Scr.Desc, "short description", f, 67, now, hubCur);
        double edy = Edy(HL) + dy2 + wsa, edh2 = EDH - wsa;
        Editor(hub, x0, edy, edh2, f, now, hubCur, emb2G, false);
        double oy = edy + edh2 + 8;
        uint ocol = Scr.OutOk == 1 ? C_ON : Scr.OutOk == 0 ? hubCur : 0xFFC7CBE0;
        FillRR(x0 - 4, oy - 2, HL.ctw + 8, 20, 7, SBrush(FA(Alpha(ocol, Scr.OutOk == -1 ? 4 : 10), f)));
        MicroBackdrop(x0 - 4, oy - 2, HL.ctw + 8, 20, 7, ocol, f, now, 0.7);
        StrokeRR(x0 - 4, oy - 2, HL.ctw + 8, 20, 7, Pen(FA(Alpha(ocol, Scr.OutOk == -1 ? 14 : 40), f), 1));
        FillRR(x0, oy + 3, 3, 12, 1.5, SBrush(FA(Alpha(ocol, Scr.OutOk == -1 ? 40 : 170), f)));
        Txt("OUTPUT", x0 + 10, oy, 56, 16, HL.fXs, FA(0x7CC7CBE0, f), Fmt.L);
        double oGlow = Scr.OutAt != 0 && now - Scr.OutAt < 700 ? Math.Sin(Math.PI * (now - Scr.OutAt) / 700.0) : 0, omx = x0 + 62;
        if (Scr.OutVer != "" && Scr.OutOk != -1)
        {
            uint vbc = Scr.OutOk == 1 ? C_ON : hubCur;
            FillRR(omx, oy + 1.5, 26, 13, 4, SBrush(FA(Alpha(vbc, 34), f)));
            StrokeRR(omx, oy + 1.5, 26, 13, 4, Pen(FA(Alpha(vbc, 140), f), 1));
            Txt("V" + Scr.OutVer, omx, oy + 1, 26, 13, HL.fS, FA(Alpha(AccHi(vbc, 0.45), 235), f), Fmt.C);
            omx += 34;
        }
        Txt(Scr.OutOk == -1 ? "press CHECK to validate the script" : Scr.OutMsg, omx, oy, x0 + HL.ctw - 8 - omx, 16, Fonts.fHint, FA(Alpha(Scr.OutOk == 1 ? AccHi(C_ON, 0.25) : Scr.OutOk == 0 ? AccHi(AccHi(hubCur, 0.45), 0.25) : 0xFF9AA8C0, (Scr.OutOk == -1 ? 120 : 240) + 15 * oGlow), f), Fmt.L);
        double lsy = Lsy(HL) + dy2;
        if (LibT > 0.02) Library(hub, x0, lsy, f, now, hubCur);
        if (LibT > 0.6) return;
        f *= 1 - LibT;
        Cards(hub, x0, lsy, f, now, hubCur);
    }
    static void FillPolyPts((double x, double y)[] pts, Avalonia.Media.IBrush? b)
    {
        var g = new Avalonia.Media.StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Avalonia.Point(pts[0].x, pts[0].y), true); for (int i = 1; i < pts.Length; i++) c.LineTo(new Avalonia.Point(pts[i].x, pts[i].y)); c.EndFigure(true); }
        FillPath(g, b);
    }
    static void ClipEll(double x, double y, double w, double h) => ClipPath(new Avalonia.Media.EllipseGeometry(new Avalonia.Rect(x, y, w, h)));
    static void FldBox(HubSurface hub, double fx, double fdy, double fw, string mode, string val, string ph, double f, int zid, long now, uint hubCur)
    {
        var HL = hub.HL;
        bool foc = FfmField.Edit == mode;
        double hv = hub.Hv(zid);
        FillRR(fx, fdy, fw, FDH, 7, SBrush(FA(Mix(0xFF141728, 0xFF1B2038, Math.Max(hv, foc ? 1.0 : 0)), f)));
        MicroBackdrop(fx, fdy, fw, FDH, 7, hubCur, f, now, 0.75);
        StrokeRR(fx, fdy, fw, FDH, 7, Pen(FA(Alpha(foc ? hubCur : 0xFFFFFF, foc ? 190 : 26 + 40 * hv), f), 1));
        string shown = foc ? FfmField.Buf : val;
        double fwv = fw - 20;
        string vis = shown; int off = 0;
        while (Fonts.MeasureW(vis, Fonts.fHint) > fwv && vis.Length > 1) { vis = vis[1..]; off++; }
        if (foc) FfmField.Paint(fx + 10, fdy, FDH, vis, off, hubCur, f, now, Fonts.fHint);
        Txt(shown == "" ? ph : vis, fx + 10, fdy, fwv, FDH, Fonts.fHint, FA(Alpha(shown == "" ? 0xFF9AA8C0 : 0xFFE8EAF6, shown == "" ? 110 : 225), f), Fmt.L);
    }
    static void Editor(HubSurface hub, double x0, double edy, double edh2, double f, long now, uint hubCur, double emb2G, bool inline)
    {
        var HL = hub.HL;
        bool focE = Scr.Focus == 1;
        FillRR(x0, edy, HL.ctw, edh2, 10, VBrush(x0, edy, HL.ctw, edh2, FA(0xFF10131F, f), FA(0xFF0C0E17, f)));
        MiniBackdrop(x0, edy, HL.ctw, edh2, 10, hubCur, f, now, 0.6);
        FillRR(x0 + 1, edy + 1, 34, edh2 - 2, 9, SBrush(FA(0x10FFFFFF, f)));
        StrokeRR(x0, edy, HL.ctw, edh2, 10, Pen(FA(Alpha(focE ? hubCur : 0xFFFFFF, focE ? 150 : 26 + 40 * hub.Hv(inline ? 242 : 60)), f), 1.2));
        int vis = Math.Max(1, (int)Math.Floor((edh2 - 16) / LINEH));
        if (Scr.Cl != Scr.LastCl || Scr.Cc != Scr.LastCc) { Scr.Follow = true; Scr.LastCl = Scr.Cl; Scr.LastCc = Scr.Cc; }
        if (Scr.Follow) { if (Scr.Cl < Scr.Top) Scr.Top = Scr.Cl; else if (Scr.Cl > Scr.Top + vis - 1) Scr.Top = Scr.Cl - vis + 1; }
        Scr.Top = Math.Clamp(Scr.Top, 1, Math.Max(1, Scr.Lines.Count - vis + 1));
        double chW = ChW(HL), edTxtW = EdTxtW(HL);
        int maxCols = 0; foreach (var l in Scr.Lines) maxCols = Math.Max(maxCols, l.Length);
        Scr.HscMax = Math.Max(0.0, maxCols * chW + 12 - edTxtW);
        if (Scr.Follow) { double cxPix = Scr.Cc * chW; if (cxPix - Scr.HscT < 0) Scr.HscT = Math.Max(0.0, cxPix - 8); else if (cxPix - Scr.HscT > edTxtW - 16) Scr.HscT = cxPix - edTxtW + 16; }
        Scr.HscT = Clamp(Scr.HscT, 0.0, Scr.HscMax);
        Scr.Follow = false;
        int stEd = PushG(); ClipRect(x0 + 36, edy + 6, HL.ctw - 42, edh2 - 12);
        double txx = x0 + 42 - Scr.Hsc;
        bool hasSel = Scr.SelGet(out int sl1, out int sc1, out int sl2, out int sc2);
        for (int k = 1; k <= vis; k++)
        {
            int li = Scr.Top + k - 1;
            if (li > Scr.Lines.Count) break;
            double ty2 = edy + 8 + (k - 1) * LINEH;
            if (li == Scr.Cl && focE) FillRR(x0 + 36, ty2 - 1, HL.ctw - 42, LINEH, 3, SBrush(FA(Alpha(hubCur, 22), f)));
            if (hasSel && li >= sl1 && li <= sl2)
            {
                int caS = li == sl1 ? sc1 : 0, caE = li == sl2 ? sc2 : Scr.Lines[li - 1].Length;
                FillRR(txx + caS * chW, ty2 - 1, Math.Max((caE - caS) * chW, 4), LINEH, 2, SBrush(FA(Alpha(hubCur, 64), f)));
            }
            int stGN = PushG(); ResetClip();
            Txt(li.ToString(), x0 + 2, ty2 - 1, 28, LINEH, HL.fMs, FA(Alpha(li == Scr.Cl ? AccHi(hubCur, 0.4) : 0xFFC7CBE0, li == Scr.Cl ? 200 : 80), f), Fmt.R);
            Pop(stGN);
            var toks = li <= Scr.Toks.Count ? Scr.Toks[li - 1] : new List<ScrTok>();
            foreach (var tk in toks)
            {
                uint tc2 = tk.T == 4 ? 0xFF6B7392 : tk.T == 3 ? C_ON : tk.T == 2 ? AMBER : tk.T == 1 ? AccHi(hubCur, 0.3) : tk.T == 5 ? 0xFF8AB4F8 : 0xFFD8DEE9;
                Txt(tk.S, txx + tk.C * chW, ty2 - 1, 460, LINEH, HL.fM, FA(Alpha(tc2, tk.T == 4 ? 190 : 235), f), Fmt.L);
            }
        }
        if (focE && (now - Scr.CaretAt) % 1000 < 550) { double crx = txx + Scr.Cc * chW, cry = edy + 8 + (Scr.Cl - Scr.Top) * LINEH; Line(crx, cry, crx, cry + LINEH - 2, Pen(FA(Alpha(AccHi(hubCur, 0.4), 245), f), 1.6)); }
        Pop(stEd);
        if (Scr.Lines.Count > vis)
        {
            double trk = edh2 - 16, sh_ = Math.Max(20, trk * vis / Scr.Lines.Count), sy3 = edy + 8 + (trk - sh_) * ((Scr.Top - 1) / (double)Math.Max(1, Scr.Lines.Count - vis));
            double hvV8 = Math.Max(hub.Hv(248), HL.drag == 10 ? 1.0 : 0.0);
            FillRR(x0 + HL.ctw - 6 - 2 * hvV8, edy + 8, 3 + 2 * hvV8, trk, 2, SBrush(FA(Alpha(0xFFFFFF, R(14 + 20 * hvV8)), f)));
            FillRR(x0 + HL.ctw - 6 - 2 * hvV8, sy3, 3 + 2 * hvV8, sh_, 2, VBrush(x0 + HL.ctw - 6 - 2 * hvV8, sy3, 3 + 2 * hvV8, sh_, FA(Alpha(AccHi(hubCur, 0.35), R(190 + 60 * hvV8)), f), FA(Alpha(hubCur, R(150 + 60 * hvV8)), f)));
        }
        double hsA2 = Clamp(Scr.HscMax / 60, 0.0, 1.0);
        if (hsA2 > 0.01)
        {
            double hvH2 = Math.Max(hub.Hv(249), HL.drag == 9 ? 1.0 : 0.0), htk2 = HL.ctw - 56;
            double hbw2 = Math.Max(28, htk2 * (edTxtW / (edTxtW + Scr.HscMax))), hbx2 = x0 + 42 + (htk2 - hbw2) * (Scr.HscMax > 0 ? Scr.Hsc / Scr.HscMax : 0), hby2 = edy + edh2 - 7;
            FillRR(x0 + 42, hby2, htk2, 3 + 2 * hvH2, 2, SBrush(FA(Alpha(0xFFFFFF, R((16 + 20 * hvH2) * hsA2)), f)));
            FillRR(hbx2, hby2, hbw2, 3 + 2 * hvH2, 2, VBrush(hbx2, hby2, hbw2, 3 + 2 * hvH2, FA(Alpha(AccHi(hubCur, 0.35), R(220 * hsA2)), f), FA(Alpha(hubCur, R(170 * hsA2)), f)));
        }
        if (focE)
        {
            FillRR(x0 + HL.ctw - 92, edy + edh2 - 20, 84, 15, 5, SBrush(FA(Alpha(hubCur, 30 + 20 * emb2G), f)));
            Txt("TYPING - ESC", x0 + HL.ctw - 92, edy + edh2 - 21, 84, 15, HL.fS, FA(Alpha(AccHi(hubCur, 0.4), 235), f), Fmt.C);
        }
        else if (!inline && Scr.Lines.Count == 1 && Scr.Lines[0] == "") Txt("click here and type - ctrl+V pastes - FILE loads an .ahk", x0 + 46, edy + 8, HL.ctw - 60, 16, Fonts.fHint, FA(0x5AC7CBE0, f), Fmt.L);
    }
    static void DrawInline(HubSurface hub, double x0, double y0, double dx, double dy2, double f, long now, uint hubCur, double emb2G)
    {
        var HL = hub.HL;
        double cet = Ease3(Clamp((now - Scr.CeAt) / 300.0, 0.0, 1.0));
        if (Scr.EdAt != 0 && now - Scr.EdAt < 520) { double ee = Ease3((now - Scr.EdAt) / 520.0), exE = 26 * (1 - ee); StrokeRR(x0 - exE, y0 + 30 - exE * 0.5, HL.ctw + exE * 2, HubLayout.ch - 120 + exE, 14 + exE * 0.3, Pen(FA(Alpha(hubCur, R(160 * (1 - ee))), f), 2.4 * (1 - ee) + 0.4)); }
        var it7 = Scr.CeIdx <= Scr.List.Count ? Scr.List[Scr.CeIdx - 1] : null;
        double hdy2 = y0 + 34 + (1 - cet) * 12;
        FillRR(x0, hdy2 + 4, 3, 18, 1.5, SBrush(FA(Alpha(hubCur, 190), f * cet)));
        Txt("INLINE EDIT", x0 + 12, hdy2 + 4, 110, 14, Fonts.fBadge, FA(Alpha(0xFFC7CBE0, 150 * cet), f), Fmt.L);
        FillRR(x0 + 104, hdy2 + 3, 66, 18, 5, SBrush(FA(Alpha(AMBER, 30), f * cet)));
        Txt("SLOT " + Scr.CeIdx, x0 + 104, hdy2 + 4, 66, 15, HL.fS, FA(Alpha(AccHi(AMBER, 0.35), 220 * cet), f), Fmt.C);
        Txt(it7?.Name ?? "", x0 + 182, hdy2 + 3, 220, 16, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 215 * cet), f), Fmt.L);
        if (it7 is not null && it7.Pid != 0) { FillEll(x0 + HL.ctw - 58, hdy2 + 9, 5, 5, SBrush(FA(Alpha(C_ON, 160 + 70 * emb2G), f * cet))); Txt("live", x0 + HL.ctw - 48, hdy2 + 4, 48, 14, HL.fXs, FA(Alpha(C_ON, 170 * cet), f), Fmt.L); }
        double fdyC = y0 + 66 + (1 - cet) * 8;
        FldBox(hub, x0, fdyC, 190, "sn", Scr.Name, "script name", f * cet, 240, now, hubCur);
        FldBox(hub, x0 + 200, fdyC, HL.ctw - 200, "sd", Scr.Desc, "short description", f * cet, 241, now, hubCur);
        Editor(hub, x0, CeY(HL) + dy2, CeH(HL), f, now, hubCur, emb2G, true);
        double btnY2 = CeBtnY() + dy2, hvS = hub.Hv(243);
        double fl243 = Scr.CeBtnAt != 0 && Scr.CeBtnZ == 243 && now - Scr.CeBtnAt < 520 ? 1 - Ease3((now - Scr.CeBtnAt) / 520.0) : 0;
        int stSv = PushXform(x0 + 48, btnY2 + 14, (1 + 0.05 * hvS) * (1 + 0.06 * fl243), 0);
        FillRR(x0, btnY2, 96, 28, 8, VBrush(x0, btnY2, 96, 28, FA(Alpha(hubCur, 70 + 60 * hvS), f), FA(Alpha(hubCur, 28 + 30 * hvS), f)));
        MicroBackdrop(x0, btnY2, 96, 28, 8, hubCur, f, now, 0.9);
        StrokeRR(x0, btnY2, 96, 28, 8, Pen(FA(Alpha(AccHi(hubCur, 0.3), 150 + 90 * hvS), f), 1.1));
        var pnK = Pen(FA(Alpha(0xFFFFFF, 210 + 45 * hvS), f), 1.7);
        Line(x0 + 16, btnY2 + 14, x0 + 20, btnY2 + 18.5, pnK); Line(x0 + 20, btnY2 + 18.5, x0 + 27, btnY2 + 9.5, pnK);
        Txt("SAVE", x0 + 30, btnY2, 58, 28, Fonts.fBadge, FA(Alpha(0xFFFFFF, 230 + 25 * hvS), f), Fmt.C);
        if (fl243 > 0.01) { double ex243 = (1 - fl243) * 14; StrokeRR(x0 - ex243, btnY2 - ex243 * 0.6, 96 + ex243 * 2, 28 + ex243 * 1.2, 8 + ex243 * 0.3, Pen(FA(Alpha(C_ON, R(200 * fl243)), f), 2.2 * fl243 + 0.4)); }
        Pop(stSv);
        CeBtn(hub, 244, x0 + 104, btnY2, 96, 28, "CANCEL", 0xFFC7CBE0, f, now);
        CeBtn(hub, 245, x0 + 208, btnY2, 114, 28, "OPEN IN MAIN", AMBER, f, now);
        double wsh = Scr.Shift();
        if (wsh > 0.4) Txt(Scr.WarnMsg, x0 + 334, btnY2 + 6, HL.ctw - 334, 16, Fonts.fHint, FA(Alpha(AccHi(hubCur, 0.45), 235 * (wsh / 34.0)), f), Fmt.R);
        Txt("SAVE overwrites the placed script - CANCEL discards changes - OPEN IN MAIN loads it into the editor", x0, btnY2 + 36, HL.ctw, 14, HL.fXs, FA(0x46C7CBE0, f), Fmt.L);
    }
    static void CeBtn(HubSurface hub, int zid, double x, double y, double w, double h, string label, uint bas, double f, long now)
    {
        double hv = hub.Hv(zid);
        double fl = Scr.CeBtnAt != 0 && Scr.CeBtnZ == zid && now - Scr.CeBtnAt < 520 ? 1 - Ease3((now - Scr.CeBtnAt) / 520.0) : 0;
        int st = PushXform(x + w / 2, y + h / 2, (1 + 0.05 * hv) * (1 + 0.06 * fl), 0);
        ChipDraw(x, y, w, h, label, hv, bas, f);
        Pop(st);
        if (fl > 0.01) { double ex = (1 - fl) * 14; StrokeRR(x - ex, y - ex * 0.6, w + ex * 2, h + ex * 1.2, 7 + ex * 0.3, Pen(FA(Alpha(bas, R(200 * fl)), f), 2.2 * fl + 0.4)); }
    }
    static void Library(HubSurface hub, double x0, double lsy, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        double lf = LibT;
        Txt("SAVED SCRIPTS", x0, lsy - 22, 130, 14, Fonts.fBadge, FA(Alpha(0xFFC7CBE0, 98 * lf), f), Fmt.L);
        Txt(Scr.LibList.Count + " on disk", x0 + 122, lsy - 22, 100, 14, Fonts.fHint, FA(Alpha(0xFFC7CBE0, 68 * lf), f), Fmt.L);
        ChipDraw(x0 + HL.ctw - 66, lsy - 24, 66, 18, "CLOSE", hub.Hv(219) * lf, hubCur, f * lf);
        FillRR(x0, lsy, HL.ctw, 2 * LSH + 6, 12, VBrush(x0, lsy, HL.ctw, 2 * LSH + 6, FA(0xFF10131F, f * lf), FA(0xFF0C0E17, f * lf)));
        MiniBackdrop(x0, lsy, HL.ctw, 2 * LSH + 6, 12, hubCur, f * lf, now, 0.75);
        StrokeRR(x0, lsy, HL.ctw, 2 * LSH + 6, 12, Pen(FA(Alpha(hubCur, 90 * lf), f), 1));
        if (Scr.LibList.Count == 0) Txt("nothing saved yet - SAVE writes into YURI\\scripts", x0, lsy + LSH - 8, HL.ctw, 16, Fonts.fHint, FA(Alpha(0xFFC7CBE0, 80 * lf), f), Fmt.C);
        for (int i = 1; i <= Math.Min(Scr.LibList.Count, 4); i++)
        {
            var itL = Scr.LibList[i - 1];
            double ry5 = lsy + 4 + (i - 1) * 32, sld2 = (1 - Clamp((lf - i * 0.08) * 2.2, 0.0, 1.0)) * 10, hvL2 = hub.Hv(220 + i);
            FillRR(x0 + 8 + sld2, ry5, HL.ctw - 16, 28, 8, SBrush(FA(Alpha(0xFFFFFF, 5 + 14 * hvL2), f * lf)));
            if (hvL2 > 0.01) FillRR(x0 + 8 + sld2, ry5 + 5, 3, 18, 1.5, SBrush(FA(Alpha(hubCur, 190 * hvL2), f * lf)));
            if (i > 1) FadeLine(x0 + 16, x0 + HL.ctw - 16, ry5 - 2, Alpha(0xFFFFFF, R(16 * lf)), f);
            bool isTxt = itL.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
            uint bcol = isTxt ? 0xFF9AA8C0 : hubCur;
            FillRR(x0 + 17 + sld2, ry5 + 7, 30, 14, 4, SBrush(FA(Alpha(bcol, 34), f * lf)));
            StrokeRR(x0 + 17 + sld2, ry5 + 7, 30, 14, 4, Pen(FA(Alpha(bcol, 120), f * lf), 1));
            Txt(isTxt ? "TXT" : "AHK", x0 + 17 + sld2, ry5 + 8, 30, 12, HL.fS, FA(Alpha(AccHi(bcol, 0.4), 220 * lf), f), Fmt.C);
            Txt(itL.Disp, x0 + 54 + sld2, ry5 + 2, HL.ctw - 262, 15, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 220 * lf), f), Fmt.L);
            Txt(itL.Tms, x0 + 54 + sld2, ry5 + 16, 160, 12, HL.fXs, FA(Alpha(0xFFC7CBE0, 90 * lf), f), Fmt.L);
            ChipDraw(x0 + HL.ctw - 96, ry5 + 3, 60, 22, "OPEN", hvL2 * lf, hubCur, f * lf);
            double hvD2 = hub.Hv(230 + i);
            var pnD = Pen(FA(Alpha(hubCur, (100 + 130 * hvD2) * lf), f), 1.4);
            Line(x0 + HL.ctw - 22, ry5 + 10, x0 + HL.ctw - 14, ry5 + 18, pnD); Line(x0 + HL.ctw - 22, ry5 + 18, x0 + HL.ctw - 14, ry5 + 10, pnD);
        }
        if (Scr.LibDelAt != 0 && now - Scr.LibDelAt < 420)
        {
            double le = Ease3((now - Scr.LibDelAt) / 420.0), lh5 = 28 * (1 - le);
            FillRR(x0 + 6, Scr.LibDelY, HL.ctw - 12, Math.Max(lh5, 1), 7 * (1 - le) + 1, SBrush(FA(Alpha(hubCur, R(46 * (1 - le))), f * lf)));
            StrokeRR(x0 + 6 - le * 8, Scr.LibDelY, HL.ctw - 12 + le * 16, Math.Max(lh5, 1), 7 * (1 - le) + 1, Pen(FA(Alpha(hubCur, R(180 * (1 - le))), f * lf), 1.4 * (1 - le) + 0.3));
            if (lh5 > 10) Txt(Scr.LibDelName, x0 + 22, Scr.LibDelY + lh5 / 2 - 8, HL.ctw - 140, 16, Fonts.fHint, FA(Alpha(hubCur, R(200 * (1 - le))), f * lf), Fmt.L);
        }
        else if (Scr.LibDelAt != 0) Scr.LibDelAt = 0;
        if (Scr.LibList.Count > 4) Txt("+" + (Scr.LibList.Count - 4) + " more in the folder", x0 + 8, lsy + 2 * LSH - 14, 200, 14, HL.fXs, FA(Alpha(0xFFC7CBE0, 80 * lf), f), Fmt.L);
    }
    static void Cards(HubSurface hub, double x0, double lsy, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        int visCards = CardsVis(HL);
        if (Scr.BackAt != 0 && now - Scr.BackAt < 520) { double be = Ease3((now - Scr.BackAt) / 520.0), exB = be * 22; StrokeRR(x0 - exB, lsy - exB * 0.5, HL.ctw + exB * 2, Math.Min(visCards * (LSH + 6) - 6, HubLayout.pd + HubLayout.ch - 26 - lsy) + exB, 14 + exB * 0.3, Pen(FA(Alpha(hubCur, R(150 * (1 - be))), f), 2.2 * (1 - be) + 0.4)); }
        Txt("PLACED", x0, lsy - 22, 90, 14, Fonts.fBadge, FA(0x62C7CBE0, f), Fmt.L);
        Txt(Scr.List.Count + " placed" + (Scr.List.Count > visCards ? "  \u00B7  scroll for more" : "") + " - click a card or EDIT to load it back", x0 + 62, lsy - 22, 320, 14, Fonts.fHint, FA(0x44C7CBE0, f), Fmt.L);
        bool edting = Scr.EditIdx >= 1 && Scr.EditIdx <= Scr.List.Count;
        int selN2 = Scr.SelLen();
        Txt(edting ? "editing " + Scr.List[Scr.EditIdx - 1].Name + " - PLACE saves over it" : "Ln " + Scr.Cl + ", Col " + (Scr.Cc + 1) + "  -  " + Scr.Lines.Count + " lines" + (selN2 > 0 ? "  -  " + selN2 + " selected" : ""), x0 + HL.ctw - 330, lsy - 22, 328, 14, HL.fXs, FA(edting ? Alpha(hubCur, 205) : 0x58C7CBE0, f), Fmt.R);
        if (Scr.List.Count == 0)
        {
            FillRR(x0, lsy, HL.ctw, LSH, 8, SBrush(FA(0x0CFFFFFF, f)));
            MicroBackdrop(x0, lsy, HL.ctw, LSH, 8, hubCur, f, now, 0.8);
            { var pnDs = Pen(FA(0x16FFFFFF, f), 1); PenDash(pnDs, 1); StrokeRR(x0, lsy, HL.ctw, LSH, 8, pnDs); }
            Txt("nothing placed yet - write something and hit PLACE", x0, lsy, HL.ctw, LSH, Fonts.fHint, FA(0x4EC7CBE0, f), Fmt.C);
        }
        double lsVisH = visCards * (LSH + 6) - 6;
        Scr.LsMax = CardMax(HL);
        Scr.LsScT = Clamp(Scr.LsScT, 0.0, Scr.LsMax);
        double lsTop = lsy - 3, lsBot = HubLayout.pd + HubLayout.ch - 22, lsClipH = Math.Min(lsVisH + 6, lsBot - lsTop);
        if (lsClipH > 8)
        {
            int stLS = PushG(); ClipRR(x0 - 4, lsTop, HL.ctw + 8, lsClipH, 14);
            double cwL = ListW(HL);
            for (int i = 1; i <= Scr.List.Count; i++)
            {
                var itm = Scr.List[i - 1];
                double ry = lsy + (i - 1) * (LSH + 6) - Scr.LsSc;
                if (ry + LSH <= lsTop || ry >= lsTop + lsClipH) continue;
                long cAt = Scr.CardAt.TryGetValue(i, out var ca) ? ca : 0;
                double cP = cAt != 0 ? Clamp((now - cAt) / 420.0, 0.0, 1.0) : 1.0;
                int stCd = -1;
                if (cP < 1) { double ce2 = 1 + 2.2 * Math.Pow(cP - 1, 3) + 1.2 * Math.Pow(cP - 1, 2); stCd = PushXform(x0 + cwL / 2, ry + LSH / 2, 0.86 + 0.14 * ce2, 0); }
                else Scr.CardAt.Remove(i);
                bool on_ = itm.Pid != 0 && Scr.Running(i), edg = Scr.EditIdx == i;
                double hvR = hub.Hv(80 + i);
                FillRR(x0, ry, cwL, LSH, 12, VBrush(x0, ry, cwL, LSH, FA(Mix(0xFF171A30, 0xFF1E2340, Math.Max(hvR, edg ? 0.55 : 0)), f), FA(0xFF12141F, f)));
                MiniBackdrop(x0, ry, cwL, LSH, 12, hubCur, f, now, 0.8);
                int stClA = PushG(); ClipRR(x0, ry, cwL, LSH, 12);
                FillEll(x0 - 30, ry - 46, 150, 150, SBrush(FA(Alpha(on_ ? C_ON : hubCur, on_ ? 30 : 14), f)));
                var pnH = Pen(FA(Alpha(0xFFFFFF, 10), f), 1);
                for (int k3 = 1; k3 <= 6; k3++) Line(x0 + cwL - 250 + k3 * 14, ry + LSH, x0 + cwL - 218 + k3 * 14, ry, pnH);
                Pop(stClA);
                StrokeRR(x0, ry, cwL, LSH, 12, Pen(FA(Alpha(on_ ? C_ON : 0xFFFFFF, on_ ? 140 : 22 + 40 * hvR), f), 1));
                if (edg) FillRR(x0, ry + 12, 3, LSH - 24, 1.5, SBrush(FA(Alpha(hubCur, 215), f)));
                double hvP = hub.Hv(100 + i), pcx = x0 + 34, pcy = ry + 33;
                if (itm.Av >= 2 && Pool.SelAt(itm.Av) is { } bmA) ProfilePlate.Circle(bmA, pcx, pcy, 22, f);
                else Sigil(itm.Name, pcx, pcy, 22, hubCur, f, now, on_);
                if (hvP > 0.01)
                {
                    int stClB = PushG(); ClipEll(pcx - 22, pcy - 22, 44, 44);
                    FillEll(pcx - 22, pcy - 22, 44, 44, SBrush(FA(Alpha(0xFF05060C, R(168 * hvP)), f)));
                    double gyC = pcy - 15 + (1 - hvP) * 5;
                    var pnCm = Pen(FA(Alpha(0xFFFFFFFF, R(215 * hvP)), f), 1.1);
                    StrokeRR(pcx - 6, gyC, 12, 9, 1.8, pnCm); Line(pcx - 3.6, gyC + 6.6, pcx - 1, gyC + 3.4, pnCm); Line(pcx - 1, gyC + 3.4, pcx + 1.8, gyC + 6.6, pnCm); Line(pcx + 1.8, gyC + 6.6, pcx + 4, gyC + 4.6, pnCm);
                    FillEll(pcx + 2, gyC + 1.4, 2, 2, SBrush(FA(Alpha(0xFFFFFFFF, R(220 * hvP)), f)));
                    Txt("CHANGE", pcx - 24, pcy + 2 + (1 - hvP) * 5, 48, 10, HL.fXs, FA(Alpha(0xFFFFFFFF, R(245 * hvP)), f), Fmt.C);
                    Txt("IMAGE", pcx - 24, pcy + 12 + (1 - hvP) * 5, 48, 10, HL.fXs, FA(Alpha(0xFFFFFFFF, R(245 * hvP)), f), Fmt.C);
                    Pop(stClB);
                }
                Ell(pcx - 21, pcy - 21, 42, 42, Pen(FA(0x50000000, f), 1));
                Ell(pcx - 24, pcy - 24, 48, 48, Pen(FA(Alpha(on_ ? C_ON : hubCur, 150 + 90 * hvP), f), 1.8));
                double da4 = DecT(now) * (on_ ? 0.09 : 0.05) % 360;
                Arc(pcx - 24, pcy - 24, 48, 48, da4, on_ ? 90 : 54, Pen(FA(Alpha(AccHi(on_ ? C_ON : hubCur, 0.45), 130 + 90 * hvP), f), 1.8));
                double nmW = Math.Min(Fonts.MeasureW(itm.Name, Fonts.fStatus), cwL - 300);
                Txt(itm.Name, x0 + 68, ry + 6, cwL - 300, 20, Fonts.fStatus, FA(Alpha(Mix(0xFFE8EAF6, hubCur, 0.15), 238), f), Fmt.L);
                FillEll(x0 + 74 + nmW, ry + 13, 5, 5, SBrush(FA(Alpha(on_ ? C_ON : 0xFFC7CBE0, on_ ? 200 + 55 * Math.Abs(Math.Sin(DecT(now) * 0.005)) : 60), f)));
                if (edg) { FillRR(x0 + 86 + nmW, ry + 9, 52, 15, 4, SBrush(FA(Alpha(hubCur, 34), f))); Txt("EDITING", x0 + 86 + nmW, ry + 8, 52, 15, HL.fXs, FA(Alpha(hubCur, 225), f), Fmt.C); }
                ChipDraw(x0 + cwL - 206, ry + 8, 60, 24, "EDIT", hub.Hv(110 + i), hubCur, f);
                ChipDraw(x0 + cwL - 138, ry + 8, 88, 24, on_ ? "STOP" : "ENABLE", hub.Hv(70 + i), on_ ? C_ON : hubCur, f);
                double hvD = hub.Hv(90 + i), dcx = x0 + cwL - 18, dcy = ry + 16;
                if (hvD > 0.01) FillEll(dcx - 10, dcy - 10, 20, 20, SBrush(FA(Alpha(hubCur, 40 * hvD), f)));
                var pnX = Pen(FA(Alpha(hubCur, 110 + 130 * hvD), f), 1.5);
                Line(dcx - 4, dcy - 4, dcx + 4, dcy + 4, pnX); Line(dcx - 4, dcy + 4, dcx + 4, dcy - 4, pnX);
                FadeLine(x0 + 68, x0 + cwL - 16, ry + 36, 0x26FFFFFF, f);
                int stD3 = PushXform(x0 + 72, ry + 48, 1 + 0.18 * Math.Sin(DecT(now) * 0.004 + i), 45);
                FillRR(x0 + 69.2, ry + 45.2, 5.6, 5.6, 1, SBrush(FA(Alpha(hubCur, 210), f)));
                Pop(stD3);
                FillEll(x0 + 79, ry + 46, 3.4, 3.4, SBrush(FA(Alpha(hubCur, 55), f)));
                Txt(itm.Desc == "" ? "no description" : itm.Desc, x0 + 88, ry + 40, cwL - 268, 16, Fonts.fHint, FA(0x78C7CBE0, f), Fmt.L);
                for (int j2 = 1; j2 <= 5; j2++) { double bh4 = 3 + 8 * Math.Abs(Math.Sin(DecT(now) * 0.0042 + j2 * 0.9 + i * 1.7)) * (on_ ? 1 : 0.4); FillRR(x0 + cwL - 76 + (j2 - 1) * 7, ry + 56 - bh4, 3, bh4, 1.2, SBrush(FA(Alpha(on_ ? C_ON : hubCur, 45 + 55 * Math.Abs(Math.Sin(DecT(now) * 0.003 + j2 + i))), f))); }
                for (int k2 = 0; k2 <= 1; k2++) { double ca2 = 45 + 90 * Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.0028 - k2 * 0.8)), 3); var pnC = Pen(FA(Alpha(AccHi(hubCur, 0.35), ca2), f), 1.5); Line(x0 + cwL - 120 + k2 * 7, ry + 44, x0 + cwL - 116 + k2 * 7, ry + 48, pnC); Line(x0 + cwL - 116 + k2 * 7, ry + 48, x0 + cwL - 120 + k2 * 7, ry + 52, pnC); }
                Txt(on_ ? "running" : "idle", x0 + cwL - 176, ry + 40, 50, 16, HL.fXs, FA(Alpha(on_ ? C_ON : 0xFFC7CBE0, on_ ? 200 : 90), f), Fmt.R);
                long tAt = Scr.TogAt.TryGetValue(i, out var ta) ? ta : 0;
                if (tAt != 0 && now - tAt < 620) { double te = Ease3((now - tAt) / 620.0), ex7 = te * 16; StrokeRR(x0 - ex7, ry - ex7 * 0.55, cwL + ex7 * 2, LSH + ex7 * 1.1, 12 + ex7 * 0.35, Pen(FA(Alpha(on_ ? C_ON : hubCur, R(190 * (1 - te))), f), 2.4 * (1 - te) + 0.4)); }
                else if (tAt != 0) Scr.TogAt.Remove(i);
                if (stCd >= 0) Pop(stCd);
            }
            if (Scr.DelAt != 0 && now - Scr.DelAt < 460 && Scr.DelY + LSH > lsTop && Scr.DelY < lsTop + lsClipH)
            {
                double de = Ease3((now - Scr.DelAt) / 460.0), dy5 = Scr.DelY, dh5 = LSH * (1 - de);
                FillRR(x0, dy5, cwL, Math.Max(dh5, 1), 12 * (1 - de) + 1, VBrush(x0, dy5, cwL, Math.Max(dh5, 1), FA(Alpha(hubCur, R(60 * (1 - de))), f), FA(0xFF12141F, f * (1 - de))));
                StrokeRR(x0 - de * 10, dy5, cwL + de * 20, Math.Max(dh5, 1), 12 * (1 - de) + 1, Pen(FA(Alpha(hubCur, R(190 * (1 - de))), f), 1.6 * (1 - de) + 0.3));
                if (dh5 > 12) Txt(Scr.DelName, x0 + 68, dy5 + dh5 / 2 - 10, cwL - 300, 20, Fonts.fStatus, FA(Alpha(hubCur, R(200 * (1 - de))), f), Fmt.L);
                for (int pk5 = 1; pk5 <= 5; pk5++) { double px5 = x0 + 60 + pk5 * (cwL - 140) / 6; FillEll(px5 - 2, dy5 + LSH / 2 - 2 - de * 26 * (1 + pk5 % 2), 4, 4, SBrush(FA(Alpha(hubCur, R(160 * (1 - de))), f))); }
            }
            else if (Scr.DelAt != 0) Scr.DelAt = 0;
            double spY = lsy + RowsH() + 8 - Scr.LsSc;
            if (spY + 34 > lsTop && spY < lsTop + lsClipH)
            {
                FadeLine(x0, x0 + cwL, spY, 0x1CFFFFFF, f);
                string[] sbadg = { "AHK V1 + V2", "LIVE VALIDATE", Scr.Tabs.Count + " TAB" + (Scr.Tabs.Count == 1 ? "" : "S") };
                double sbx3 = x0;
                for (int k = 1; k <= 3; k++)
                {
                    double bwj = 18 + Fonts.MeasureW(sbadg[k - 1], HL.fS);
                    FillRR(sbx3, spY + 12, bwj, 18, 6, SBrush(FA(0x0EFFFFFF, f)));
                    StrokeRR(sbx3, spY + 12, bwj, 18, 6, Pen(FA(Alpha(hubCur, 60 + 30 * Math.Abs(Math.Sin(DecT(now) * 0.002 + k))), f), 1));
                    FillEll(sbx3 + 7, spY + 18.5, 4, 4, SBrush(FA(Alpha(hubCur, 150 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.0026 + k * 1.4))), f)));
                    Txt(sbadg[k - 1], sbx3 + 14, spY + 13, bwj - 14, 15, HL.fS, FA(0x9AC7CBE0, f), Fmt.L);
                    sbx3 += bwj + 10;
                }
                double tpH = DecT(now) * 0.06 % (cwL + 60) - 30;
                FillEll(x0 + tpH - 3.5, spY - 3.5, 7, 7, SBrush(FA(Alpha(hubCur, 60), f)));
                FillEll(x0 + tpH - 1.5, spY - 1.5, 3, 3, SBrush(FA(Alpha(AccHi(hubCur, 0.45), 210), f)));
                Txt("engine auto-detected on CHECK", x0 + cwL - 210, spY + 13, 210, 16, HL.fXs, FA(0x4AC7CBE0, f), Fmt.R);
            }
            Pop(stLS);
        }
        if (Scr.LsMax > 0)
        {
            double sbx9 = x0 + HL.ctw - 3, hv9 = Math.Max(hub.Hv(69), HL.drag == 7 ? 1.0 : 0.0), sw9 = 3 + 3 * hv9;
            FillRR(sbx9 - 3 * hv9, lsy, sw9, lsVisH, sw9 / 2, SBrush(FA(Alpha(0xFFFFFF, R(20 + 26 * hv9)), f)));
            double th9 = Math.Max(26, lsVisH * (lsVisH / (lsVisH + Scr.LsMax))), ty9 = lsy + (lsVisH - th9) * (Scr.LsSc / Scr.LsMax);
            FillRR(sbx9 - 3 * hv9, ty9, sw9, th9, sw9 / 2, VBrush(sbx9 - 3 * hv9, ty9, sw9, th9, FA(Alpha(AccHi(hubCur, 0.35), R(195 + 60 * hv9)), f), FA(Alpha(hubCur, R(170 + 60 * hv9)), f)));
        }
    }
    /// <summary>ScrSigil: a card with no picture wears a moon phased from its name's hash.</summary>
    static void Sigil(string nm, double cx, double cy, double r, uint acc, double f, long now, bool live)
    {
        ulong h = FFlags.Hash(nm.ToLowerInvariant());
        double pulse = live ? 0.74 + 0.26 * Math.Sin(DecT(now) * 0.0042) : 1.0;
        FillEll(cx - r, cy - r, r * 2, r * 2, SBrush(FA(Mix(0xFF12141F, acc, 0.16), f)));
        double mr = r * 0.58;
        FillEll(cx - mr, cy - mr, mr * 2, mr * 2, SBrush(FA(Alpha(0xFFF2EFE6, R(232 * pulse)), f)));
        double an = (h % 8) * 0.7854 - 1.2;
        int sv = PushG(); ClipEll(cx - mr, cy - mr, mr * 2, mr * 2);
        double sx = cx + mr * 0.62 * Math.Cos(an), sy = cy + mr * 0.62 * Math.Sin(an);
        FillEll(sx - mr * 0.94, sy - mr * 0.94, mr * 1.88, mr * 1.88, SBrush(FA(Mix(0xFF12141F, acc, 0.22), f)));
        Pop(sv);
        double dx0 = cx - mr * 1.28 * Math.Cos(an), dy0 = cy - mr * 1.28 * Math.Sin(an);
        FillEll(dx0 - 1.4, dy0 - 1.4, 2.8, 2.8, SBrush(FA(Alpha(AccHi(acc, 0.5), R(230 * pulse)), f)));
        Ell(cx - r + 0.5, cy - r + 0.5, r * 2 - 1, r * 2 - 1, Pen(FA(Alpha(acc, R(95 + 60 * (pulse - 0.74))), f), 1));
    }

    // ---- zones ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x0 = HL.ctx;
        if (Scr.CeIdx != 0)
        {
            double fdyZ = HL.cty + 66;
            if (uy >= fdyZ && uy <= fdyZ + FDH) { if (ux >= x0 && ux <= x0 + 190) return 240; if (ux >= x0 + 200 && ux <= x0 + HL.ctw) return 241; }
            double ceY = CeY(HL), ceH = CeH(HL);
            if (Scr.Lines.Count > EdVis(HL) && ux >= x0 + HL.ctw - 14 && ux <= x0 + HL.ctw && uy >= ceY + 6 && uy <= ceY + ceH - 6) return 248;
            if (Scr.HscMax > 0.6 && uy >= ceY + ceH - 14 && uy <= ceY + ceH && ux >= x0 + 42 && ux <= x0 + HL.ctw - 14) return 249;
            if (ux >= x0 && ux <= x0 + HL.ctw && uy >= ceY && uy <= ceY + ceH) return 242;
            double by = CeBtnY();
            if (uy >= by && uy <= by + 28) { if (ux >= x0 && ux <= x0 + 96) return 243; if (ux >= x0 + 104 && ux <= x0 + 200) return 244; if (ux >= x0 + 208 && ux <= x0 + 322) return 245; }
            return 0;
        }
        double tby = Tby(HL);
        for (int j = 1; j <= 8; j++) { double bx2 = x0 + (j - 1) * (TBW + 5); if (ux >= bx2 && ux <= bx2 + TBW && uy >= tby && uy <= tby + TBH) return j == 4 ? 68 : j == 5 ? 55 : j == 6 ? 56 : j == 7 ? 59 : j == 8 ? 65 : 60 + j; }
        if (Scr.ExpOpen && uy >= tby + TBH + 6 && uy <= tby + TBH + 26) { double ex0 = x0 + 5 * (TBW + 5); if (ux >= ex0 && ux <= ex0 + 44) return 51; if (ux >= ex0 + 50 && ux <= ex0 + 94) return 52; }
        double wshT = ShiftAll(), tabY = TabY(HL);
        if (uy >= tabY + wshT && uy <= tabY + 22 + wshT)
        {
            int tn = Scr.Tabs.Count; double tw3 = Math.Min(118, (HL.ctw - 130) / tn);
            for (int i = 1; i <= tn; i++) { double tx3 = x0 + (i - 1) * (tw3 + 4); if (ux >= tx3 && ux <= tx3 + tw3) return tn > 1 && ux >= tx3 + tw3 - 16 ? 210 + i : 200 + i; }
            double px3 = x0 + tn * (tw3 + 4);
            if (ux >= px3 && ux <= px3 + 22) return 199;
            if (Scr.Cur > 1 && ux >= x0 + HL.ctw - 74 && ux <= x0 + HL.ctw) return 57;
        }
        double lsy = Lsy(HL);
        if (Scr.LibOpen && LibT > 0.5)
        {
            double lly = lsy - 24;
            if (ux >= x0 + HL.ctw - 66 && ux <= x0 + HL.ctw && uy >= lly && uy <= lly + 18) return 219;
            for (int i = 1; i <= Math.Min(Scr.LibList.Count, 4); i++) { double ry5 = lsy + 4 + (i - 1) * 32; if (uy >= ry5 && uy <= ry5 + 28) { if (ux >= x0 + HL.ctw - 30) return 230 + i; if (ux >= x0 + HL.ctw - 96 && ux <= x0 + HL.ctw - 36) return 220 + i; } }
            return 0;
        }
        if (ux >= x0 + HL.ctw - 84 && ux <= x0 + HL.ctw && uy >= tby && uy <= tby + TBH) return 64;
        double fdy = Fdy(HL);
        if (uy >= fdy + wshT && uy <= fdy + FDH + wshT) { if (ux >= x0 && ux <= x0 + 190) return 66; if (ux >= x0 + 200 && ux <= x0 + HL.ctw) return 67; }
        double edY8 = EdY(HL), edH8 = EdH(HL);
        if (Scr.Lines.Count > EdVis(HL) && ux >= x0 + HL.ctw - 14 && ux <= x0 + HL.ctw && uy >= edY8 + 6 && uy <= edY8 + edH8 - 6) return 248;
        if (Scr.HscMax > 0.6 && uy >= edY8 + edH8 - 14 && uy <= edY8 + edH8 && ux >= x0 + 42 && ux <= x0 + HL.ctw - 14) return 249;
        if (ux >= x0 && ux <= x0 + HL.ctw && uy >= Edy(HL) + wshT && uy <= Edy(HL) + EDH) return 60;
        int visCards = CardsVis(HL);
        if (Scr.List.Count > visCards && ux >= x0 + HL.ctw - 6 && ux <= x0 + HL.ctw + 8 && uy >= lsy && uy <= lsy + visCards * (LSH + 6) - 6) return 69;
        double cwZ = ListW(HL), hlsTop = lsy - 3, hlsH = Math.Min(visCards * (LSH + 6), HubLayout.pd + HubLayout.ch - 22 - hlsTop);
        for (int i = 1; i <= Scr.List.Count; i++)
        {
            double ry = lsy + (i - 1) * (LSH + 6) - Scr.LsSc;
            if (ry + LSH <= hlsTop || ry >= hlsTop + hlsH || uy < hlsTop || uy > hlsTop + hlsH) continue;
            if (uy >= ry && uy <= ry + LSH)
            {
                if (ux > x0 + cwZ) return 0;
                if (ux <= x0 + 62) return 100 + i;
                if ((ux - (x0 + cwZ - 18)) * (ux - (x0 + cwZ - 18)) + (uy - (ry + 16)) * (uy - (ry + 16)) <= 100) return 90 + i;
                if (uy >= ry + 8 && uy <= ry + 32) { if (ux >= x0 + cwZ - 138 && ux <= x0 + cwZ - 50) return 70 + i; if (ux >= x0 + cwZ - 206 && ux <= x0 + cwZ - 146) return 110 + i; }
                if (ux >= x0 + 62) return 80 + i;
            }
        }
        return 0;
    }
    /// <summary>The press half: the editor takes the caret and starts a selection, the fields take the caret, the scrollbars start their drags.</summary>
    public static bool Press(HubSurface hub, int z)
    {
        var HL = hub.HL;
        switch (z)
        {
            case 60: case 242:
                if (Scr.Focus != 1) Scr.FocusSet(1);
                CaretAtPointer(hub);
                if (Scr.DblAt != 0 && Clock.Tick - Scr.DblAt < 420) { Scr.WordSelect(); Scr.MSel = false; Scr.DblAt = 0; }
                else { Scr.Sl = Scr.Cl; Scr.Sc = Scr.Cc; Scr.SelOn = false; Scr.MSel = true; Scr.DblAt = Clock.Tick; hub.BeginPtrDrag(); }
                return true;
            case 66: case 240: Scr.Blur(); if (FfmField.Edit != "sn") FfmField.Begin("sn", 0); FfmField.Mouse(hub.PtrX); return true;
            case 67: case 241: Scr.Blur(); if (FfmField.Edit != "sd") FfmField.Begin("sd", 0); FfmField.Mouse(hub.PtrX); return true;
            case 248: HL.drag = 10; HL.dragZone = 248; EdVDrag(hub, hub.PtrY); return true;
            case 249: HL.drag = 9; HL.dragZone = 249; EdHDrag(hub, hub.PtrX); return true;
            case 69: HL.drag = 7; HL.dragZone = 69; ListDrag(hub, hub.PtrY); return true;
        }
        return false;
    }
    static void CaretAtPointer(HubSurface hub)
    {
        var HL = hub.HL;
        double edB = EdY(HL);
        int li = Scr.Top + (int)Math.Floor((hub.PtrY - (edB + 8)) / LINEH);
        int co = (int)Math.Round((hub.PtrX - (HL.ctx + 42 - Scr.Hsc)) / ChW(HL));
        Scr.CaretFromMouse(li, co);
    }
    /// <summary>The pointer moving with the editor's mouse selection held.</summary>
    public static void Drag(HubSurface hub)
    {
        if (!Scr.MSel) return;
        CaretAtPointer(hub);
        Scr.SelOn = Scr.Sl != Scr.Cl || Scr.Sc != Scr.Cc;
        hub.Tim(Pace.TICK_A);
    }
    public static void MouseUp() => Scr.MSel = false;
    public static void EdVDrag(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        int vs = EdVis(HL);
        if (Scr.Lines.Count <= vs) return;
        double edY = EdY(HL), edH = EdH(HL), trk = edH - 16, th = Math.Max(20, trk * vs / Scr.Lines.Count);
        double fr = ScrollFromY(HL, uy, edY + 8, trk, Scr.Lines.Count, vs, th, Scr.Top - 1) / Math.Max(1, Scr.Lines.Count - vs);
        Scr.Top = Math.Clamp(1 + (int)Math.Round(fr * (Scr.Lines.Count - vs)), 1, Math.Max(1, Scr.Lines.Count - vs + 1));
        Scr.LastCl = Scr.Cl; Scr.LastCc = Scr.Cc; Scr.Follow = false;
        hub.Tim(Pace.TICK_A);
    }
    public static void EdHDrag(HubSurface hub, double ux)
    {
        var HL = hub.HL;
        if (Scr.HscMax <= 0) return;
        double htk = HL.ctw - 56, hbw = Math.Max(28, htk * (EdTxtW(HL) / (EdTxtW(HL) + Scr.HscMax)));
        Scr.HscT = ScrollFromY(HL, ux, HL.ctx + 42, htk, EdTxtW(HL) + Scr.HscMax, EdTxtW(HL), hbw, Scr.Hsc); Scr.Hsc = Scr.HscT;
        hub.Tim(Pace.TICK_A);
    }
    public static void ListDrag(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        if (Scr.LsMax <= 0) return;
        double vis = CardsVis(HL) * (LSH + 6) - 6, th = Math.Max(26, vis * (vis / (vis + Scr.LsMax)));
        Scr.LsScT = ScrollFromY(HL, uy, Lsy(HL), vis, vis + Scr.LsMax, vis, th, Scr.LsSc); Scr.LsSc = Scr.LsScT;
        hub.Tim(Pace.TICK_A);
    }
    public static bool Click(HubSurface hub, int z)
    {
        var HL = hub.HL;
        switch (z)
        {
            case 61: Scr.New(); break;
            case 62: _ = PasteFromClipAsync(hub); break;
            case 63: _ = PickFileAsync(hub); break;
            case 64: Scr.Place(); break;
            case 65: Scr.Clear(); break;
            case 68: Scr.Check(); break;
            case 55: Scr.Save(); break;
            case 56: Scr.ExpOpen = !Scr.ExpOpen; break;
            case 51: case 52: Scr.ExpOpen = false; _ = ExportAsync(hub, z == 51 ? "ahk" : "txt"); break;
            case 59: Scr.LibOpen = !Scr.LibOpen; if (Scr.LibOpen) Scr.LibRefresh(); break;
            case 199: Scr.TabAdd(); break;
            case >= 201 and <= 204: Scr.TabSwitch(z - 200); break;
            case >= 211 and <= 214: Scr.TabClose(z - 210); break;
            case 57: Scr.InMain(); break;
            case 219: Scr.LibOpen = false; break;
            case >= 221 and <= 224: Scr.LibOpenAt(z - 220); break;
            case >= 231 and <= 234: { int li = z - 230; if (li >= 1 && li <= Scr.LibList.Count) { Scr.LibDelY = Lsy(HL) + 4 + (li - 1) * 32; Scr.LibDelName = Scr.LibList[li - 1].Name; Scr.LibDelAt = Clock.Tick; } Scr.LibDel(li); break; }
            case 243: Scr.CeBtnAt = Clock.Tick; Scr.CeBtnZ = 243; Scr.CardSave(); break;
            case 244: Scr.BackAt = Clock.Tick; Scr.CeBtnAt = Clock.Tick; Scr.CeBtnZ = 244; Scr.CardCancel(); break;
            case 245: Scr.CeBtnAt = Clock.Tick; Scr.CeBtnZ = 245; Scr.CardMain(); break;
            case >= 71 and <= 76: Scr.TogAt[z - 70] = Clock.Tick; Scr.Toggle(z - 70); break;
            case >= 81 and <= 86: Scr.EdAt = Clock.Tick; Scr.CardEdit(z - 80); break;
            case >= 91 and <= 96: Scr.Delete(z - 90, Lsy(HL), LSH + 6, CardsVis(HL)); break;
            case >= 101 and <= 106: Scr.MSel = false; Gallery.CardPicked = (t, idx) => { if (t >= 1 && t <= Scr.List.Count) { Scr.List[t - 1].Av = idx; Scr.SaveList(); } }; Gallery.Show(z - 100, false); break;
            case >= 111 and <= 116: Scr.EdAt = Clock.Tick; Scr.CardEdit(z - 110); break;
            default: return false;
        }
        hub.Tim(Pace.TICK_A);
        return true;
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        double x0 = HL.ctx, edY = EdY(HL), edH = EdH(HL);
        if (ux >= x0 && ux <= x0 + HL.ctw && uy >= edY && uy <= edY + edH)
        {
            int vs = EdVis(HL);
            Scr.Top = Math.Clamp(Scr.Top - (int)Math.Sign(delta) * 3, 1, Math.Max(1, Scr.Lines.Count - vs + 1));
            Scr.LastCl = Scr.Cl; Scr.LastCc = Scr.Cc; Scr.Follow = false;
            return true;
        }
        if (Scr.CeIdx == 0 && uy >= Lsy(HL) - 3 && Scr.LsMax > 0) { Scr.LsScT = Clamp(Scr.LsScT - delta * (LSH + 6), 0.0, Scr.LsMax); return true; }
        return false;
    }
    static async Task PasteFromClipAsync(HubSurface hub)
    {
        try { var cb = TopLevel.GetTopLevel(hub)?.Clipboard; string? t = cb is null ? null : await cb.GetTextAsync(); if (Scr.Focus != 1) Scr.FocusSet(1); Scr.Paste(t ?? ""); } catch { }
    }
    static async Task PickFileAsync(HubSurface hub)
    {
        if (_picking) return;
        _picking = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            if (top?.StorageProvider is not { CanOpen: true } sp) return;
            var files = await sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Load a script", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("Scripts") { Patterns = new[] { "*.ahk", "*.txt" } }, FilePickerFileTypes.All } });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } p) Scr.LoadFile(p);
        }
        catch { }
        finally { _picking = false; hub.Tim(Pace.TICK_A); }
    }
    static async Task ExportAsync(HubSurface hub, string ext)
    {
        if (Scr.Text().Trim() == "") { Scr.Warn("nothing to export - write a script first"); return; }
        if (_picking) return;
        _picking = true;
        try
        {
            var top = TopLevel.GetTopLevel(hub);
            if (top?.StorageProvider is not { CanSave: true } sp) return;
            string nm = Scr.Name.Trim() == "" ? "script" : System.Text.RegularExpressions.Regex.Replace(Scr.Name.Trim(), "[\\\\/:*?\"<>|]", "_");
            var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export script", SuggestedFileName = nm + "." + ext, DefaultExtension = ext, FileTypeChoices = new[] { new FilePickerFileType(ext == "ahk" ? "AutoHotkey script" : "Text file") { Patterns = new[] { "*." + ext } } } });
            if (file?.TryGetLocalPath() is { } p) Scr.ExportTo(p, ext);
        }
        catch { }
        finally { _picking = false; hub.Tim(Pace.TICK_A); }
    }
}
