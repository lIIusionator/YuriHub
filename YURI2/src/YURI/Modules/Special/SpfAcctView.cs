using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.Special;

/// <summary>SPFAcctView and SPFAcctZone: the accounts manager. Zone ids are the .ahk's (1020-1057).</summary>
public static class SpfAcctView
{
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_ACC = 0xFFFB7185, C_OFF = 0xFFFB7185;
    const int ACCT_MAX = SpfAcct.ACCT_MAX;
    public static Action<string>? Clip;

    static double FldX(HubLayout HL) => HL.ctx + 240;
    static double FldW(HubLayout HL) => HL.ctw - 242 - 24;

    public static void Draw(HubSurface hub, double ff, long now, uint acc, double dx0, double dy2)
    {
        var HL = hub.HL;
        double e = Ease3(Clamp((now - Spf.ViewAt) / 300.0, 0.0, 1.0));
        double t = Spf.View == "acct" ? e : Spf.ViewPrev == "acct" ? 1 - e : 0;
        if (t <= 0.01) return;
        double fo = ff * t;
        double x = HL.ctx + dx0, y = HL.cty + dy2 + 30, w = HL.ctw, h = 414;
        FillRR(x - 10, HL.cty + dy2 - 6, w + 20, h + 54, 14, SBrush(FA(Alpha(0x06070E, R(220 * t)), ff)));
        int st = PushXform(x + w / 2, y + h / 2, 0.96 + 0.04 * t, 0);
        FfmViews.HubViewChrome(x, y, w, h, fo, now, acc);
        FillRR(x, y + 10, 3, 22, 1.5, SBrush(FA(Alpha(acc, 200), fo)));
        Txt("ACCOUNTS", x + 16, y + 11, 200, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 240), fo), Fmt.L);
        int nA = SpfAcct.Acct.Count;
        double cw3 = 42;
        FillRR(x + 108, y + 13, cw3, 16, 8, SBrush(FA(Alpha(nA > 0 ? acc : 0xFFFFFF, nA > 0 ? 56 : 14), fo)));
        Txt(nA + "/" + ACCT_MAX, x + 108, y + 14, cw3, 14, HL.fXs, FA(Alpha(nA > 0 ? AccHi(acc, 0.5) : 0xFFC7CBE0, 225), fo), Fmt.C);
        // w - 300 put the subtitle's right edge 14 px PAST the left edge of ADD
        // FROM CLIPBOARD, so the two ran into each other. Measured against the
        // button instead, and elided, so a longer line cannot reach it either.
        double abx2 = x + w - 150, aby2 = y + 8, subX = x + 108 + cw3 + 14;
        Txt(FFMElide("launch a client already signed into any saved account", HL.fS, abx2 - 12 - subX),
            subX, y + 14, abx2 - 12 - subX, 16, HL.fS, FA(0x66C7CBE0, fo), Fmt.L);
        double hvA = hub.Hv(1030);
        FillRR(abx2, aby2, 116, 22, 7, SBrush(FA(Alpha(acc, R(22 + 34 * hvA)), fo)));
        MicroBackdrop(abx2, aby2, 116, 22, 7, acc, fo, now, 0.9);
        StrokeRR(abx2, aby2, 116, 22, 7, Pen(FA(Alpha(acc, R(90 + 90 * hvA)), fo), 1));
        var bP = SBrush(FA(Alpha(AccHi(acc, 0.4), 210 + 45 * hvA), fo));
        FillRR(abx2 + 11, aby2 + 10, 10, 2, 1, bP); FillRR(abx2 + 15, aby2 + 6, 2, 10, 1, bP);
        Txt("ADD FROM CLIPBOARD", abx2 + 26, aby2 + 4, 86, 14, HL.fXs, FA(Alpha(0xFFE8EAF6, 190 + 60 * hvA), fo), Fmt.L);
        double hvX = hub.Hv(1020);
        if (hvX > 0.01) FillEll(x + w - 29, y + 3, 22, 22, SBrush(FA(Alpha(acc, R(40 * hvX)), fo)));
        var pnX = Pen(FA(Alpha(hvX > 0.2 ? acc : 0xFFC7CBE0, 130 + 120 * hvX), fo), 1.6);
        double cxx = x + w - 18, cyy = y + 14;
        Line(cxx - 5, cyy - 5, cxx + 5, cyy + 5, pnX); Line(cxx + 5, cyy - 5, cxx - 5, cyy + 5, pnX);
        Line(x + 14, y + 38, x + w - 14, y + 38, Pen(FA(Alpha(0xFFFFFF, 20), fo), 1));
        if (t < 0.85) { Pop(st); return; }
        double lx = x + 14, ly = y + 48, lw = 192, rowH = 38;
        if (nA == 0)
        {
            double by0 = ly + ACCT_MAX * rowH + 6;
            Txt("no accounts saved", x + 16, y + h / 2 - 30, w - 32, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 200), fo), Fmt.C);
            Txt("copy your .ROBLOSECURITY cookie and press ADD FROM CLIPBOARD", x + 16, y + h / 2 - 8, w - 32, 18, Fonts.fHint, FA(0x72C7CBE0, fo), Fmt.C);
            Txt("the cookie is sealed with your Windows account - the file is useless to anyone else", x + 16, y + h / 2 + 14, w - 32, 16, HL.fXs, FA(Alpha(AMBER, 140), fo), Fmt.C);
            Pop(st);
            return;
        }
        SpfAcct.Sel = Math.Clamp(SpfAcct.Sel, 1, nA);
        var sel = SpfAcct.Acct[SpfAcct.Sel - 1];
        for (int i = 1; i <= nA; i++)
        {
            var en = SpfAcct.Acct[i - 1];
            double ry = ly + (i - 1) * rowH;
            bool on = i == SpfAcct.Sel;
            double hv = hub.Hv(1020 + i);
            if (on || hv > 0.01) FillRR(lx, ry, lw, rowH - 4, 8, HBrush(lx, ry, lw, rowH - 4, FA(Alpha(on ? acc : 0xFFFFFF, on ? 38 : 14 * hv), fo), FA(Alpha(on ? acc : 0xFFFFFF, on ? 8 : 3 * hv), fo)));
            if (on)
            {
                FillRR(lx, ry + 6, 3, rowH - 16, 1.5, VBrush(lx, ry + 6, 3, rowH - 16, FA(Alpha(AccHi(acc, 0.4), 240), fo), FA(Alpha(acc, 160), fo)));
                StrokeRR(lx, ry, lw, rowH - 4, 8, Pen(FA(Alpha(acc, 70), fo), 1));
            }
            double bxp = lx + 8 + 2 * hv;
            var hd = SpfAcct.Head(en.Id);
            if (hd is not null) Img.FitRR(hd, bxp, ry + 3, 28, 28, 7, fo);
            else
            {
                FillRR(bxp, ry + 3, 28, 28, 7, VBrush(bxp, ry + 3, 28, 28, FA(Alpha(acc, 60), fo), FA(Alpha(acc, 22), fo)));
                MicroBackdrop(bxp, ry + 3, 28, 28, 7, acc, fo, now, 0.7);
                Txt(en.Nm.Length > 0 ? en.Nm[..1].ToUpperInvariant() : "?", bxp, ry + 7, 28, 20, Fonts.fBadge, FA(Alpha(AccHi(acc, 0.5), 215), fo), Fmt.C);
            }
            StrokeRR(bxp, ry + 3, 28, 28, 7, Pen(FA(Alpha(on ? acc : 0xFFFFFF, on ? 150 : 40), fo), 1));
            Txt(FFMElide(en.Nm, Fonts.fHint, lw - 52), bxp + 36, ry + 2, lw - 52, 16, Fonts.fHint, FA(Alpha(on ? 0xFFE8EAF6 : 0xFFC7CBE0, on ? 240 : 165 + 55 * hv), fo), Fmt.L);
            var pr = SpfAcct.GetProf(en.Id);
            string sub = pr is not null ? SpfAcct.PresName(pr.Pres) : "cookie saved";
            uint scol = pr is not null ? SpfAcct.PresCol(pr.Pres) : 0xFFC7CBE0;
            FillEll(bxp + 36, ry + 21, 5, 5, SBrush(FA(Alpha(scol, pr is not null ? 200 : 110), fo)));
            Txt(sub, bxp + 46, ry + 17, lw - 62, 13, HL.fXs, FA(Alpha(pr is not null ? AccHi(scol, 0.2) : 0xFFC7CBE0, pr is not null ? 200 : 140), fo), Fmt.L);
        }
        double by = ly + ACCT_MAX * rowH + 6;
        FFMBtn(1032, lx, by, 92, 26, "REMOVE", acc, fo, 0);
        FFMBtn(1038, lx + 96, by, 96, 26, "PASTE COOKIE", acc, fo, 0);
        var pnD = Pen(FA(Alpha(0xFFFFFF, 16), fo), 1); PenDash(pnD, 1); PenDashOff(pnD, (DecT(now) * 0.012) % 1000);
        Line(x + 214, ly, x + 214, by + 26, pnD);
        // ---- detail ----
        double dx2 = x + 228, dw = w - 242;
        string aid = sel.Id;
        var prf = aid != "" ? SpfAcct.GetProf(aid) : null;
        if (aid != "") SpfAcct.ProfFetch(SpfAcct.Sel);
        double avW = 116, avH = 150;
        Avatar(hub, aid, dx2, ly, avW, avH, fo, now, acc);
        double ix = dx2 + 130, iw = dw - 130;
        string dnm = prf is not null && prf.Disp != "" ? prf.Disp : sel.Nm;
        Txt(FFMElide(dnm, Fonts.fBadge, iw), ix, ly - 2, iw, 20, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 240), fo), Fmt.L);
        if (prf is not null && prf.Nm != "" && prf.Nm != dnm) Txt("@" + prf.Nm, ix, ly + 18, iw, 14, HL.fXs, FA(0x8EC7CBE0, fo), Fmt.L);
        double py2 = ly + 36;
        int pt = prf?.Pres ?? 0;
        uint pc = SpfAcct.PresCol(pt);
        string pnm = SpfAcct.PresName(pt);
        double pw2 = Fonts.MeasureW(pnm.ToUpperInvariant(), HL.fXs) + 24;
        double pu2 = (Math.Sin(DecT(now) * 0.0032) + 1) / 2;
        FillEll(ix + 8, py2 + 6, 5, 5, SBrush(FA(Alpha(pc, R(pt != 0 ? 170 + 70 * pu2 : 110)), fo)));
        Txt(pnm.ToUpperInvariant(), ix + 18, py2 + 1, pw2 - 22, 14, HL.fXs, FA(Alpha(AccHi(pc, 0.3), 230), fo), Fmt.L);
        // tabs
        string[] tabs2 = { "ABOUT", "GROUPS", "CREATED" };
        int[] tcnt = { 0, prf?.Groups.Count ?? 0, prf?.Made.Count ?? 0 };
        double tby = ly + 62, tbh = 18, tbw2 = iw / 3;
        SpfAcct.TabT += (SpfAcct.Tab - SpfAcct.TabT) * EK(0.28);
        double uxa = ix + (SpfAcct.TabT - 1) * tbw2;
        FillRR(uxa, tby, tbw2, tbh, 6, SBrush(FA(Alpha(acc, 30), fo)));
        FillRR(uxa, tby + tbh - 2, tbw2, 2, 1, VBrush(uxa, tby + tbh - 2, tbw2, 2, FA(Alpha(AccHi(acc, 0.5), 235), fo), FA(Alpha(acc, 150), fo)));
        for (int k = 1; k <= 3; k++)
        {
            double tx4 = ix + (k - 1) * tbw2;
            bool selT = SpfAcct.Tab == k;
            double hvT = hub.Hv(1039 + k);
            if (hvT > 0.01 && !selT) FillRR(tx4, tby, tbw2, tbh, 6, SBrush(FA(Alpha(0xFFFFFF, R(14 * hvT)), fo)));
            string lb = tabs2[k - 1] + (tcnt[k - 1] > 0 ? " " + tcnt[k - 1] : "");
            Txt(lb, tx4, tby + 1, tbw2, 15, HL.fXs, FA(Alpha(selT ? AccHi(acc, 0.4) : 0xFFC7CBE0, selT ? 240 : 120 + 70 * hvT), fo), Fmt.C);
        }
        double pnY = ly + 82, pnH = 73;
        int stP = PushG(); ClipRR(ix, pnY - 2, iw, pnH + 4, 6);
        if (SpfAcct.Tab == 1)
        {
            string dsc2 = prf is not null ? prf.Desc.Trim() : "";
            if (dsc2 == "") dsc2 = prf is not null ? "no description" : "loading profile...";
            int n2 = 0;
            foreach (var wl in Detail.WrapText(dsc2, Fonts.fHint, iw, 2)) { if (n2 >= 2) break; Txt(wl, ix, pnY + n2 * 15, iw, 15, Fonts.fHint, FA(0x9EC7CBE0, fo), Fmt.L); n2++; }
            string[] cLab = { "FRIENDS", "FOLLOWERS", "FOLLOWING", "GROUPS" };
            string[] cVal = { prf?.Fr ?? "", prf?.Fol ?? "", prf?.Fwg ?? "", prf is not null ? prf.Groups.Count.ToString() : "" };
            double cw4 = (iw - 9) / 4, cy4 = pnY + 34;
            for (int k = 1; k <= 4; k++)
            {
                double cx4 = ix + (k - 1) * (cw4 + 3);
                FillRR(cx4, cy4, cw4, 32, 7, VBrush(cx4, cy4, cw4, 32, FA(Mix(0xFF1B2038, acc, 0.10), fo), FA(Mix(0xFF12141F, acc, 0.05), fo)));
                MiniBackdrop(cx4, cy4, cw4, 32, 7, acc, fo, now, 0.8);
                StrokeRR(cx4, cy4, cw4, 32, 7, Pen(FA(Alpha(acc, 46), fo), 1));
                Txt(cLab[k - 1], cx4, cy4 + 2, cw4, 12, HL.fXs, FA(Alpha(0xFFC7CBE0, 130), fo), Fmt.C);
                Txt(cVal[k - 1] == "" ? "--" : FfmCommView.Num(long.Parse(cVal[k - 1])), cx4, cy4 + 13, cw4, 16, Fonts.fBadge, FA(Alpha(AccHi(acc, 0.35), 240), fo), Fmt.C);
            }
        }
        else
        {
            var rows2 = SpfAcct.Tab == 2 ? prf?.Groups : prf?.Made;
            if (rows2 is null || rows2.Count == 0)
            {
                string code = SpfAcct.Tab == 2 ? prf?.GroupsCode ?? "" : prf?.MadeCode ?? "";
                string eMsg = prf is null ? "loading..." : code == "200" ? "nothing here" : code is "-1" or "0" ? "no reply from roblox" : "roblox refused this list  \u00B7  http " + code;
                Txt(eMsg, ix, pnY + 22, iw, 16, Fonts.fHint, FA(Alpha(prf is not null && code != "200" ? AMBER : 0xFFC7CBE0, 145), fo), Fmt.C);
            }
            else
            {
                double rh2 = 24;
                string kind2 = SpfAcct.Tab == 2 ? "r" : "g";
                SpfAcct.PaneT += (SpfAcct.Pane - SpfAcct.PaneT) * EK(0.3);
                for (int k = 1; k <= rows2.Count; k++)
                {
                    var e3 = rows2[k - 1];
                    double ry2 = pnY + (k - 1) * rh2 - SpfAcct.PaneT * rh2;
                    if (ry2 < pnY - rh2 || ry2 > pnY + pnH) continue;
                    double en3 = HubState.LowPerf ? 1.0 : Ease3(Clamp((now - SpfAcct.TabAt - (k - 1) * 45) / 260.0, 0.0, 1.0));
                    if (en3 <= 0.004) continue;
                    double fRw = fo * en3, sld3 = (1 - en3) * 14, hv3 = hub.Hv(1044 + k), rx3 = ix + sld3 + 2 * hv3;
                    FillRR(rx3, ry2, iw, rh2 - 3, 5, SBrush(FA(Alpha(0xFFFFFF, R(8 + 14 * hv3)), fRw)));
                    if (hv3 > 0.01) StrokeRR(rx3, ry2, iw, rh2 - 3, 5, Pen(FA(Alpha(acc, R(90 * hv3)), fRw), 1));
                    var ico = SpfAcct.ListIcon(kind2, e3.Ic);
                    double isz = rh2 - 9, ixp = rx3 + 4;
                    if (ico is not null) Img.FitRR(ico, ixp, ry2 + 3, isz, isz, 5, fRw);
                    else
                    {
                        double shp = HubState.LowPerf ? 0.5 : 0.5 + 0.5 * Math.Sin(DecT(now) * 0.004 + k * 0.7);
                        FillRR(ixp, ry2 + 3, isz, isz, 5, VBrush(ixp, ry2 + 3, isz, isz, FA(Alpha(acc, R(38 + 26 * shp)), fRw), FA(Alpha(acc, 18), fRw)));
                        Txt(e3.N.Length > 0 ? e3.N[..1].ToUpperInvariant() : "?", ixp, ry2 + 3, isz, isz - 1, HL.fXs, FA(Alpha(AccHi(acc, 0.4), 205), fRw), Fmt.C);
                    }
                    StrokeRR(ixp, ry2 + 3, isz, isz, 5, Pen(FA(Alpha(acc, R(60 + 90 * hv3)), fRw), 1));
                    double tx5 = ixp + isz + 8, sw4 = e3.S != "" ? 62 : 0;
                    Txt(FFMElide(e3.N, Fonts.fHint, iw - (tx5 - rx3) - sw4 - 8), tx5, ry2 + 2, iw - (tx5 - rx3) - sw4 - 8, 15, Fonts.fHint, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.25 * hv3), 225 + 30 * hv3), fRw), Fmt.L);
                    if (e3.S != "")
                    {
                        string sTxt = SpfAcct.Tab == 2 ? e3.S : long.TryParse(e3.S, out var nv) ? FfmCommView.Num(nv) + " visits" : e3.S;
                        Txt(FFMElide(sTxt, HL.fXs, sw4), rx3 + iw - sw4 - 6, ry2 + 3, sw4, 14, HL.fXs, FA(Alpha(AccHi(acc, 0.2), 190), fRw), Fmt.R);
                    }
                }
                double vis2 = pnH / rh2;
                if (rows2.Count > vis2)
                {
                    double th4 = Math.Max(14, pnH * (vis2 / rows2.Count)), mx4 = Math.Max(0.0, rows2.Count - vis2), ty4 = pnY + (pnH - th4) * (SpfAcct.PaneT / Math.Max(0.001, mx4));
                    FillRR(ix + iw - 3, pnY, 3, pnH, 1.5, SBrush(FA(Alpha(0xFFFFFF, 16), fo)));
                    FillRR(ix + iw - 3, ty4, 3, th4, 1.5, VBrush(ix + iw - 3, ty4, 3, th4, FA(Alpha(AccHi(acc, 0.35), 200), fo), FA(Alpha(acc, 150), fo)));
                }
            }
        }
        Pop(stP);
        // launch box
        double ty2 = ly + 176;
        bool fon = FfmField.Edit == "at";
        double fhv = hub.Hv(1035);
        Txt("LAUNCH TARGET", dx2, ty2 - 17, 200, 14, HL.fXs, FA(Alpha(fon ? acc : 0xFFC7CBE0, fon ? 210 : 130), fo), Fmt.L);
        FillRR(dx2, ty2, dw, 28, 8, SBrush(FA(Mix(0xFF141728, 0xFF1B2038, Math.Max(fhv, fon ? 1.0 : 0.0)), fo)));
        MicroBackdrop(dx2, ty2, dw, 28, 8, acc, fo, now, 0.75);
        StrokeRR(dx2, ty2, dw, 28, 8, Pen(FA(Alpha(fon ? acc : 0xFFFFFF, fon ? 190 : 30 + 50 * fhv), fo), fon ? 1.3 : 1));
        if (fon) FillRR(dx2, ty2 + 7, 2.6, 14, 1.3, SBrush(FA(Alpha(acc, 210), fo)));
        string shown = fon ? FfmField.Buf : SpfAcct.Tgt;
        string vv = shown; int vo = 0;
        while (Fonts.MeasureW(vv, Fonts.fHint) > FldW(HL) - 20 && vv.Length > 1) { vv = vv[1..]; vo++; }
        if (fon) FfmField.Paint(FldX(HL), ty2 + 6, 16, vv, vo, acc, fo, now, Fonts.fHint);
        if (vv == "") Txt("place id, game link, private server link, or place id + job id", FldX(HL), ty2 + 6, FldW(HL), 16, Fonts.fHint, FA(0x58C7CBE0, fo), Fmt.L);
        else Txt(vv, FldX(HL), ty2 + 6, FldW(HL), 16, Fonts.fHint, FA(Alpha(0xFFE8EAF6, 235), fo), Fmt.L);
        string dsc = SpfAcct.TargetDesc(SpfAcct.Tgt);
        string tSrc = SpfAcct.Tgt.Trim();
        bool okT = tSrc == "" || (SpfAcct.ParseTarget(tSrc, out _, out _, out _, out var s0) && s0 == "");
        FillRR(dx2, ty2 + 36, 3, 14, 1.5, SBrush(FA(Alpha(okT ? C_ON : AMBER, 150), fo)));
        Txt(FFMElide(dsc, Fonts.fHint, dw - 14), dx2 + 10, ty2 + 34, dw - 14, 18, Fonts.fHint, FA(Alpha(okT ? 0xFFC7CBE0 : AMBER, 205), fo), Fmt.L);
        bool busy = SpfAcct.At != 0 && now - SpfAcct.At < 20000;
        double lby = ty2 + 62;
        FFMBtn(1031, dx2, lby, dw, 32, busy ? "LAUNCHING..." : "LAUNCH " + FFMElide(sel.Nm, Fonts.fBadge, dw - 120).ToUpperInvariant(), acc, fo, Spf.Multi && okT ? 1 : 0);
        if (busy && !HubState.LowPerf)
        {
            double sw = (now - SpfAcct.At) % 1200 / 1200.0, sxp = dx2 + dw * sw;
            int sv = PushG(); ClipRR(dx2, lby, dw, 32, 6);
            uint shn = HubState.ThT > 0.5 ? Mix(acc, 0xFF23242B, 0.55) : AccHi(acc, 0.5);
            FillRect(sxp - 40, lby, 40, 32, HBrush(sxp - 40, lby, 80, 32, FA(Alpha(shn, 0), fo), FA(Alpha(shn, 90), fo)));
            FillRect(sxp, lby, 40, 32, HBrush(sxp, lby, 80, 32, FA(Alpha(shn, 90), fo), FA(Alpha(shn, 0), fo)));
            Pop(sv);
        }
        bool fresh = Spf.MsgAt != 0 && now - Spf.MsgAt < 9000;
        if (fresh)
        {
            uint mc = Spf.MsgCol == C_ACC ? acc : Spf.MsgCol != 0 ? Spf.MsgCol : 0xFFC7CBE0;
            double mf = Clamp(1 - (now - Spf.MsgAt - 7000) / 2000.0, 0.0, 1.0);
            double pu = Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) % 1700 / 1700.0 * 6.283)), 4);
            FillEll(dx2, lby + 40, 12, 12, SBrush(FA(Alpha(mc, R((30 + 40 * pu) * mf)), fo)));
            FillEll(dx2 + 3, lby + 43, 6, 6, SBrush(FA(Alpha(mc, R(235 * mf)), fo)));
            Txt(FFMElide(Spf.Msg, Fonts.fHint, dw - 24), dx2 + 20, lby + 38, dw - 20, 17, Fonts.fHint, FA(Alpha(AccHi(mc, 0.25), R(235 * mf)), fo), Fmt.L);
        }
        else Txt("opens a client already signed in as this account - one per account, all at once", dx2, lby + 38, dw, 17, Fonts.fHint, FA(0x5EC7CBE0, fo), Fmt.L);
        Pop(st);
    }

    static void Avatar(HubSurface hub, string id, double x, double y, double w, double h, double fo, long now, uint acc)
    {
        var HL = hub.HL;
        uint avSurf = Mix(0xFF0B0D16, 0xFFEEF2F8, HubState.ThT);
        FillRR(x, y, w, h, 10, SBrush(FA(avSurf, fo)));
        int sv = PushG(); ClipRR(x, y, w, h, 10);
        FillRect(x, y + h * 0.55, w, h * 0.45, VBrush(x, y + h * 0.55, w, h * 0.45, FA(Alpha(acc, 0), fo), FA(Alpha(acc, R(40 + 30 * HubState.ThT)), fo)));
        var bm = SpfAcct.Body(id);
        if (bm is not null) Img.FitRR(bm, x, y, w, h, 10, fo, 1.0, 0.5, 0.42);
        else
        {
            double ph = (Math.Sin(DecT(now) * 0.005) + 1) / 2;
            Arc(x + w / 2 - 13, y + h / 2 - 13, 26, 26, DecT(now) * 0.14 % 360, 80, Pen(FA(Alpha(acc, R(40 + 60 * ph)), fo), 1.4));
            Txt(id == "" ? "sign-in pending" : "loading avatar", x, y + h / 2 + 16, w, 16, HL.fXs, FA(Alpha(0xFFC7CBE0, R(70 + 60 * ph)), fo), Fmt.C);
        }
        Pop(sv);
        StrokeRR(x, y, w, h, 10, Pen(FA(Alpha(acc, R(70 + 110 * hub.Hv(1036))), fo), 1.2));
    }

    // ---- SPFAcctZone ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double x = HL.ctx, y = HL.cty + 30, w = HL.ctw, h = 414;
        double lx = x + 14, ly = y + 48, lw = 192, rowH = 38;
        if (ux >= x + w - 32 && ux <= x + w && uy >= y && uy <= y + 28) return 1020;
        if (ux >= x + w - 150 && ux <= x + w - 34 && uy >= y + 8 && uy <= y + 30) return 1030;
        double byBrw = ly + ACCT_MAX * rowH + 6;
        if (SpfAcct.Acct.Count == 0 && uy >= byBrw && uy <= byBrw + 26 && ux >= lx && ux <= lx + 96) return 1038;
        if (SpfAcct.Acct.Count > 0)
        {
            for (int i = 1; i <= SpfAcct.Acct.Count; i++) { double ry = ly + (i - 1) * rowH; if (uy >= ry && uy <= ry + rowH - 4 && ux >= lx && ux <= lx + lw) return 1020 + i; }
            double by = ly + ACCT_MAX * rowH + 6;
            if (uy >= by && uy <= by + 26 && ux >= lx && ux <= lx + 92) return 1032;
            if (uy >= by && uy <= by + 26 && ux >= lx + 96 && ux <= lx + 192) return 1038;
            double dx2 = x + 228, dw = w - 242;
            if (uy >= ly && uy <= ly + 150 && ux >= dx2 && ux <= dx2 + 116) return 1036;
            double ix2 = dx2 + 130, iw2 = dw - 130;
            if (uy >= ly + 62 && uy <= ly + 80 && ux >= ix2 && ux <= ix2 + iw2) { double tw4 = iw2 / 3; for (int k = 1; k <= 3; k++) if (ux >= ix2 + (k - 1) * tw4 && ux <= ix2 + k * tw4) return 1039 + k; }
            if (uy >= ly + 82 && uy <= ly + 155 && ux >= ix2 && ux <= ix2 + iw2)
            {
                if (SpfAcct.Tab > 1) { int rw = (int)Math.Floor((uy - (ly + 82) + SpfAcct.PaneT * 24) / 24) + 1; if (rw >= 1 && rw <= 12) return 1044 + rw; }
                return 1044;
            }
            double ty2 = ly + 176;
            if (uy >= ty2 && uy <= ty2 + 28 && ux >= dx2 && ux <= dx2 + dw) return 1035;
            double lby = ty2 + 62;
            if (uy >= lby && uy <= lby + 32 && ux >= dx2 && ux <= dx2 + dw) return 1031;
        }
        if (ux >= x && ux <= x + w && uy >= y && uy <= y + h) return 1033;
        return 1034;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (FfmField.Edit == "at" && z != 1035) FfmField.End(true);
        switch (z)
        {
            case 1020 or 1034: Spf.ViewClose(); return true;
            case 1033: return true;
            case >= 1021 and <= 1028:
            {
                int i = z - 1020;
                if (i <= SpfAcct.Acct.Count) { SpfAcct.Sel = i; SpfAcct.Tab = 1; SpfAcct.Pane = 0; SpfAcct.PaneT = 0; SpfAcct.TabAt = Clock.Tick; if (SpfAcct.Acct[i - 1].Id != "") SpfAcct.ProfFetch(i); }
                return true;
            }
            case 1030 or 1038:
                Clip?.Invoke("");   // no-op; the paste comes from the clipboard read below
                PasteCookie(hub);
                return true;
            case 1032: SpfAcct.Del(SpfAcct.Sel); return true;
            case >= 1040 and <= 1042: SpfAcct.Tab = z - 1039; SpfAcct.Pane = 0; SpfAcct.PaneT = 0; SpfAcct.TabAt = Clock.Tick; return true;
            case 1036: return true;                                            // the avatar (drag to turn is cosmetic here)
            case 1035: if (FfmField.Edit != "at") FfmField.Begin("at", 0); FfmField.Mouse(hub.PtrX); return true;
            case 1031: SpfAcct.Launch(SpfAcct.Sel); return true;
            case >= 1045 and <= 1056: return true;                             // a list row: opening the page needs a browser
        }
        return false;
    }
    static async void PasteCookie(HubSurface hub)
    {
        string c = "";
        try { var cb = Avalonia.Controls.TopLevel.GetTopLevel(hub)?.Clipboard; if (cb is not null) c = await cb.GetTextAsync() ?? ""; } catch { }
        if (c.Trim() == "") { Spf.Say("CLIPBOARD IS EMPTY - COPY YOUR .ROBLOSECURITY COOKIE FIRST", C_ACC); return; }
        // An exception out of an `async void` has nowhere to go but the process.
        // Only the clipboard read was guarded; the parse that follows it takes
        // whatever the clipboard happened to hold.
        try { SpfAcct.AddFromClip(c); } catch { Spf.Say("THAT DOES NOT LOOK LIKE A COOKIE", C_ACC); }
        hub.Tim(Pace.TICK_A);
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (SpfAcct.Tab > 1)
        {
            var pr = SpfAcct.Acct.Count > 0 ? SpfAcct.GetProf(SpfAcct.Acct[Math.Clamp(SpfAcct.Sel, 1, SpfAcct.Acct.Count) - 1].Id) : null;
            int n = SpfAcct.Tab == 2 ? pr?.Groups.Count ?? 0 : pr?.Made.Count ?? 0;
            int max = Math.Max(0, n - 3);
            SpfAcct.Pane = Math.Clamp(SpfAcct.Pane - (int)Math.Sign(delta), 0, max);
        }
        return true;
    }
    public static bool Animating => (Spf.View == "acct" || Spf.ViewPrev == "acct") && (Clock.Tick - Spf.ViewAt < 400 || Spf.View == "acct");
}
