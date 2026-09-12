using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Shell.Hub.Tabs;

/// <summary>
/// Tab 4. APPEARANCE: the six ACCENTS, the BACKDROP and ELEMENTS opacity
/// sliders, DARK / LIGHT with ARMED TINT. BEHAVIOUR: four tiles —
/// PERFORMANCE (LOW PERFORMANCE MODE), ON TOP, MINIMIZE (true minimise),
/// AUTO UPDATE. Then the LIVE PREVIEW, the GALLERY link and RESET APPEARANCE.
/// Every change writes zeal.ini's [hub] the way the .ahk did.
/// </summary>
public static class SettingsTab
{
    // filled by the gallery module
    public static Func<int> GalRotN = () => 0;
    public static Func<int> GalHidN = () => 0;

    public static void Draw(HubSurface hub, double x0, double y0, double dx, double dy2, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        double thT = HubState.ThT;
        double emb2G = (Math.Sin(DecT(now) * 0.004) + 1) / 2;
        SetHead(x0, HL.ctw, HL.rys + dy2, "APPEARANCE", hubCur, f, now);
        string[] labs = { "ACCENT", "BACKDROP", "ELEMENTS", "THEME" };
        for (int i3 = 1; i3 <= 4; i3++)
        {
            double ry = HL.setr + (i3 - 1) * (HL.rh + HL.rgap) + dy2;
            FillRR(x0, ry, HL.ctw, HL.rh, 10, VBrush(x0, ry, HL.ctw, HL.rh, FA(0xFF161930, f), FA(0xFF12141F, f)));
            MiniBackdrop(x0, ry, HL.ctw, HL.rh, 10, hubCur, f, now, 0.8);
            StrokeRR(x0, ry, HL.ctw, HL.rh, 10, Pen(FA(0x1CFFFFFF, f), 1));
            double icy2 = ry + HL.rh / 2;
            if (i3 == 1)
            {
                Ell(x0 + 16, icy2 - 6, 12, 12, Pen(FA(Alpha(hubCur, 200), f), 1.5));
                var b = SBrush(FA(Alpha(hubCur, 220), f));
                FillEll(x0 + 19, icy2 - 3, 2.4, 2.4, b); FillEll(x0 + 23, icy2 - 1, 2.4, 2.4, b); FillEll(x0 + 19.6, icy2 + 2, 2.4, 2.4, b);
            }
            else if (i3 == 3)
            {
                var pn = Pen(FA(Alpha(hubCur, 200), f), 1.4);
                StrokeRR(x0 + 14, icy2 - 7, 15, 15, 3, pn);
                StrokeRR(x0 + 19, icy2 - 2, 15, 15, 3, pn);
            }
            else if (i3 == 2)
            {
                StrokeRR(x0 + 15, icy2 - 6, 13, 13, 3, Pen(FA(Alpha(hubCur, 200), f), 1.5));
                FillRR(x0 + 15, icy2 - 6, 6.5, 13, 3, SBrush(FA(Alpha(hubCur, 150), f)));
            }
            else
            {
                Ell(x0 + 15, icy2 - 6.5, 13, 13, Pen(FA(Alpha(hubCur, 200), f), 1.5));
                FillPie(x0 + 15, icy2 - 6.5, 13, 13, 90, 180, SBrush(FA(Alpha(hubCur, 190), f)));
            }
            Txt(labs[i3 - 1], x0 + 38, ry, 90, HL.rh, Fonts.fBadge, FA(0x76C7CBE0, f), Fmt.L);
            if (i3 == 1)
            {
                for (int j = 1; j <= 6; j++)
                {
                    double sxc = x0 + 118 + (j - 1) * 40, syc = ry + HL.rh / 2;
                    uint accJ = HubState.Accents[j - 1];
                    bool selA = HubState.Accent == accJ;
                    double hv = hub.Hv(19 + j);
                    double scl2 = 1 + 0.15 * hv + (selA ? 0.06 : 0);
                    int stW = PushXform(sxc, syc, scl2, 0);
                    FillEll(sxc - 11, syc - 9.5, 22, 22, SBrush(FA(Alpha(0x000000, 90), f)));
                    FillEll(sxc - 11, syc - 11, 22, 22, SBrush(FA(accJ, f)));
                    FillEll(sxc - 5.5, syc - 7.5, 5, 5, SBrush(FA(Alpha(0xFFFFFF, 70), f)));
                    if (selA)
                    {
                        Ell(sxc - 14.5, syc - 14.5, 29, 29, Pen(FA(Alpha(AccHi(accJ, 0.55), 235), f), 2));
                        double aa = (DecT(now) * 0.12) % 360;
                        Arc(sxc - 14.5, syc - 14.5, 29, 29, aa, 60, Pen(FA(Alpha(0xFFFFFF, 225), f), 2));
                        FillEll(sxc - 2.4, syc - 2.4, 4.8, 4.8, SBrush(FA(Alpha(0xFFFFFF, 240), f)));
                    }
                    else if (hv > 0.01) Ell(sxc - 13.5, syc - 13.5, 27, 27, Pen(FA(Alpha(0xFFFFFF, 60 + 130 * hv), f), 1.4));
                    if (selA && HL.accFlashAt != 0 && now - HL.accFlashAt < 560)
                    {
                        double e = Ease3((now - HL.accFlashAt) / 560.0);
                        double r_ = 13 + e * 13;
                        Ell(sxc - r_, syc - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(accJ, 170 * (1 - e)), f), 2.2 * (1 - e) + 0.4));
                    }
                    Pop(stW);
                }
                Txt("hub color", x0 + HL.ctw - 92, ry + 11, 80, 16, Fonts.fHint, FA(0x66C7CBE0, f), Fmt.R);
            }
            else if (i3 == 2 || i3 == 3)
            {
                double sx2 = HL.sldx + dx, syc = ry + HL.rh / 2;
                int zS = i3 == 2 ? 42 : 46;
                double hvS = Math.Max(hub.Hv(zS), (HL.drag == 3 && i3 == 2) || (HL.drag == 4 && i3 == 3) ? 1.0 : 0.0);
                FillRR(sx2, syc - 2.5, HL.sldw, 5, 2.5, SBrush(FA(0x22FFFFFF, f)));
                double fr = i3 == 2 ? (HubState.BgOpacity - 0.15) / 0.85 : (HubState.Opacity - 0.35) / 0.65;
                FillRR(sx2, syc - 2.5, HL.sldw * fr + 1, 5, 2.5, VBrush(sx2, syc - 2.5, HL.sldw * fr + 1, 5, FA(Alpha(hubCur, 235), f), FA(Alpha(hubCur, 150), f)));
                for (int k = 0; k <= 5; k++)
                {
                    double tkx = sx2 + k * (HL.sldw / 5);
                    Line(tkx, syc + 8, tkx, syc + 12, Pen(FA(0x1EFFFFFF, f), 1));
                }
                double knx = sx2 + HL.sldw * fr;
                FillEll(knx - 13 - 2 * hvS, syc - 13 - 2 * hvS, 26 + 4 * hvS, 26 + 4 * hvS, SBrush(FA(Alpha(hubCur, 50 + 60 * hvS), f)));
                FillEll(knx - 8, syc - 6.5, 16, 16, SBrush(FA(Alpha(0x000000, 90), f)));
                FillEll(knx - 8, syc - 8, 16, 16, SBrush(FA(Alpha(0xFEFEFE, 250), f)));
                FillEll(knx - 3, syc - 3, 6, 6, SBrush(FA(Alpha(hubCur, 235), f)));
                Txt(i3 == 2 ? "backdrop opacity" : "element opacity", x0 + HL.ctw - 178, ry + 11, 118, 16, Fonts.fHint, FA(0x66C7CBE0, f), Fmt.R);
                Txt(R((i3 == 2 ? HubState.BgOpacity : HubState.Opacity) * 100) + "%", x0 + HL.ctw - 54, ry + 11, 42, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, hubCur, 0.3), 230), f), Fmt.R);
            }
            else
            {
                for (int j = 1; j <= 2; j++)
                {
                    double bxT = x0 + (j == 1 ? 118 : 214), byT = ry + 6;
                    bool selT = HubState.Theme == j - 1;
                    double sv = j == 1 ? 1 - thT : thT;
                    double hv = hub.Hv(39 + j);
                    FillRR(bxT, byT, 84, 26, 7, VBrush(bxT, byT, 84, 26, FA(0xFF23263C, f), FA(0xFF191C2E, f)));
                    MicroBackdrop(bxT, byT, 84, 26, 7, hubCur, f, now, 0.9);
                    FillRR(bxT, byT, 84, 26, 7, SBrush(FA(Alpha(hubCur, 40 * sv), f)));
                    MicroBackdrop(bxT, byT, 84, 26, 7, hubCur, f, now, 0.9);
                    FillRR(bxT, byT, 84, 26, 7, SBrush(FA(Alpha(0xFFFFFF, (8 + 16 * hv) * (1 - sv)), f)));
                    MicroBackdrop(bxT, byT, 84, 26, 7, hubCur, f, now, 0.9);
                    StrokeRR(bxT, byT, 84, 26, 7, Pen(FA(Alpha(AccHi(hubCur, 1 - sv), 30 + 60 * hv + (180 - 60 * hv) * sv), f), 1 + 0.4 * sv));
                    uint icc2 = FA(Alpha(Mix(0xFFC7CBE0, AccHi(hubCur, 0.35), sv), 130 + 60 * hv + (110 - 60 * hv) * sv), f);
                    if (j == 1)
                    {
                        FillPie(bxT + 12, byT + 7, 12, 12, 90, 180, SBrush(icc2));
                        Ell(bxT + 12, byT + 7, 12, 12, Pen(icc2, 1.3));
                    }
                    else
                    {
                        FillEll(bxT + 15, byT + 10, 7, 7, SBrush(icc2));
                        var pn = Pen(icc2, 1.2);
                        for (int q = 0; q < 6; q++)
                        {
                            double a_ = q * 60 * 0.0174533;
                            Line(bxT + 18.5 + 6.4 * Math.Cos(a_), byT + 13.5 + 6.4 * Math.Sin(a_), bxT + 18.5 + 9 * Math.Cos(a_), byT + 13.5 + 9 * Math.Sin(a_), pn);
                        }
                    }
                    TxtP(j == 1 ? "DARK" : "LIGHT", bxT + 30, byT - 1, 48, 26, Fonts.fBadge, FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, sv), 140 + 60 * hv + (100 - 60 * hv) * sv), f), Fmt.L);
                    if (selT && HL.thFlashAt != 0 && now - HL.thFlashAt < 560)
                    {
                        double e = Ease3((now - HL.thFlashAt) / 560.0);
                        double ex = e * 12;
                        StrokeRR(bxT - ex, byT - ex * 0.6, 84 + ex * 2, 26 + ex * 1.2, 7 + ex * 0.3, Pen(FA(Alpha(hubCur, 180 * (1 - e)), f), 2.2 * (1 - e) + 0.4));
                    }
                }
                double h48 = hub.Hv(48);
                Txt("ARMED TINT", x0 + 312, ry + 11, 80, 16, HL.fXs, FA(Alpha(HubState.ArmTint != 0 ? Mix(0xFFE8EAF6, hubCur, 0.3) : 0xFFC7CBE0, HubState.ArmTint != 0 ? 210 : 120), f), Fmt.L);
                FFMTogDraw(x0 + 396, ry + 8, 44, 22, HL.tintT, hubCur, h48, f);
                if (HL.tintAt != 0 && now - HL.tintAt < 620)
                {
                    double e6 = Ease3((now - HL.tintAt) / 620.0);
                    double ex6 = e6 * 13;
                    StrokeRR(x0 + 396 - ex6, ry + 8 - ex6 * 0.6, 44 + ex6 * 2, 22 + ex6 * 1.2, 11 + ex6 * 0.3, Pen(FA(Alpha(hubCur, 190 * (1 - e6)), f), 2.2 * (1 - e6) + 0.4));
                }
                Txt("interface base", x0 + HL.ctw - 92, ry + 11, 80, 16, Fonts.fHint, FA(0x66C7CBE0, f), Fmt.R);
            }
        }
        // ---- BEHAVIOUR ----
        SetHead(x0, HL.ctw, HL.setb + dy2, "BEHAVIOUR", hubCur, f, now);
        var bT = new (int z, string t, bool on, double e, long at, string l1, string l2)[]
        {
            (49, "PERFORMANCE", HubState.LowPerf, HL.lpT, HL.lpAt,
                HubState.LowPerf ? "effects off" : "full effects",
                HubState.LowPerf ? "flat - idles at zero" : (Pace.FtEma != 0 ? $"frame {Math.Round(Pace.FtEma)} ms  \u00B7  skia" : "every surface animates")),
            (50, "ON TOP", HubState.OnTop, HL.topT, HL.topAt,
                HubState.OnTop ? "always on top" : "normal window",
                HubState.OnTop ? "stays above windows" : "windows can cover it"),
            (53, "MINIMIZE", HubState.TrueMin, HL.tmT, HL.tmAt,
                HubState.TrueMin ? "true minimize" : "minimize to a pill",
                HubState.TrueMin ? "reopen from the tray" : "a pill stays on screen"),
            (58, "AUTO UPDATE", HubState.AutoUpdate, HL.auT, HL.auAt,
                HubState.AutoUpdate ? "checks at launch" : "manual only",
                HubState.AutoUpdate ? "asks before installing" : "dashboard check only"),
        };
        for (int k7 = 1; k7 <= 4; k7++)
        {
            var it7 = bT[k7 - 1];
            double tx0 = x0 + (k7 - 1) * (HL.setw + 10);
            double ty0 = HL.sett + dy2;
            double hv7 = hub.Hv(it7.z);
            double se7 = Ease3(it7.e);
            FillRR(tx0, ty0, HL.setw, HL.seth, 10, VBrush(tx0, ty0, HL.setw, HL.seth, FA(Mix(0xFF161930, 0xFF1B2038, hv7), f), FA(0xFF12141F, f)));
            MiniBackdrop(tx0, ty0, HL.setw, HL.seth, 10, hubCur, f, now, 0.8);
            if (se7 > 0.02) FillRR(tx0, ty0, HL.setw, HL.seth, 10, SBrush(FA(Alpha(hubCur, R(20 * se7)), f)));
            StrokeRR(tx0, ty0, HL.setw, HL.seth, 10, Pen(FA(Alpha(se7 > 0.5 ? hubCur : 0xFFFFFF, R((se7 > 0.5 ? 70 : 28) + 60 * hv7)), f), 1));
            SetTileIcon(k7, tx0 + 22, ty0 + 21, se7, hubCur, f, now);
            FFMTogDraw(tx0 + HL.setw - 54, ty0 + 10, 44, 22, it7.e, hubCur, hv7, f);
            Txt(it7.t, tx0 + 14, ty0 + 37, HL.setw - 28, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, hubCur, 0.18), 235), f), Fmt.L);
            Txt(FFMElide(it7.l1, HL.fXs, HL.setw - 28), tx0 + 14, ty0 + 55, HL.setw - 28, 13, HL.fXs, FA(Alpha(it7.on ? Mix(0xFFE8EAF6, HubState.C_ON, 0.35) : 0xFFC7CBE0, it7.on ? 225 : 150), f), Fmt.L);
            Txt(FFMElide(it7.l2, HL.fXs, HL.setw - 28), tx0 + 14, ty0 + 67, HL.setw - 28, 12, HL.fXs, FA(0x66C7CBE0, f), Fmt.L);
            if (it7.at != 0 && now - it7.at < 620)
            {
                double e7 = Ease3((now - it7.at) / 620.0);
                double ex7 = e7 * 12;
                StrokeRR(tx0 - ex7, ty0 - ex7 * 0.6, HL.setw + ex7 * 2, HL.seth + ex7 * 1.2, 10 + ex7 * 0.3, Pen(FA(Alpha(hubCur, 190 * (1 - e7)), f), 2.2 * (1 - e7) + 0.4));
            }
        }
        // ---- LIVE PREVIEW, GALLERY, RESET ----
        double pvy = HL.setp + dy2, pvx = x0;
        FillRR(pvx, pvy, 64, 40, 8, VBrush(pvx, pvy, 64, 40, FA(Alpha(0x1B1F38, R(255 * HubState.BgOpacity)), f), FA(Alpha(0x12141F, R(255 * HubState.BgOpacity)), f)));
        MiniBackdrop(pvx, pvy, 64, 40, 8, hubCur, f * HubState.BgOpacity, now, 0.7);
        StrokeRR(pvx, pvy, 64, 40, 8, Pen(FA(Alpha(hubCur, 120), f), 1));
        FillRR(pvx + 7, pvy + 7, 12, 2.2, 1.1, SBrush(FA(Alpha(hubCur, R(210 * HubState.Opacity)), f)));
        var bL = SBrush(FA(Alpha(0xC7CBE0, R(120 * HubState.Opacity)), f));
        FillRR(pvx + 7, pvy + 14, 40, 2, 1, bL); FillRR(pvx + 7, pvy + 20, 30, 2, 1, bL);
        FillRR(pvx + 7, pvy + 28, 18, 6, 3, SBrush(FA(Alpha(hubCur, R(150 * HubState.Opacity)), f)));
        Txt("LIVE PREVIEW", pvx + 78, pvy + 6, 130, 12, HL.fS, FA(0x58C7CBE0, f), Fmt.L);
        Txt($"{R(HubState.BgOpacity * 100)} / {R(HubState.Opacity * 100)}  \u00B7  {(HubState.Theme != 0 ? "LIGHT" : "DARK")}", pvx + 78, pvy + 21, 130, 13, HL.fXs, FA(Alpha(hubCur, 150), f), Fmt.L);
        Line(x0 + 228, pvy + 9, x0 + 228, pvy + 31, Pen(FA(0x1EC7CBE0, f), 1));
        double hvG = hub.Hv(54);
        double gbx = x0 + HL.ctw - 116, gby = pvy + 7 - 1.5 * hvG;
        Txt("GALLERY", gbx - 16 - 190, pvy + 6, 190, 12, HL.fS, FA(0x58C7CBE0, f), Fmt.R);
        int galN = GalRotN(), galH = GalHidN();
        Txt($"{galN} in rotation" + (galH != 0 ? $"  \u00B7  {galH} removed" : ""), gbx - 16 - 190, pvy + 21, 190, 13, HL.fXs, FA(Alpha(hubCur, 150), f), Fmt.R);
        FillRR(gbx, gby, 116, 26, 8, VBrush(gbx, gby, 116, 26, FA(Alpha(hubCur, 40 + 55 * hvG), f), FA(Alpha(hubCur, 16 + 26 * hvG), f)));
        StrokeRR(gbx, gby, 116, 26, 8, Pen(FA(Alpha(hubCur, 120 + 110 * hvG), f), 1.1));
        var pnGl = Pen(FA(Alpha(AccHi(hubCur, 0.5), 190 + 60 * hvG), f), 1.4);
        StrokeRR(gbx + 15, gby + 9, 13, 10, 2, pnGl);
        StrokeRR(gbx + 11, gby + 6, 13, 10, 2, pnGl);
        Txt("MANAGE", gbx + 34, gby + 5.5, 74, 15, HL.fS, FA(Alpha(AccHi(hubCur, 0.4), 235), f), Fmt.L);
        const double RSH = 46;
        double ryR = Math.Min(pvy + 48, HubLayout.FtrY() - RSH);
        FillRR(x0, ryR, HL.ctw, RSH, 12, VBrush(x0, ryR, HL.ctw, RSH, FA(0xFF14172A, f), FA(0xFF0F1120, f)));
        MiniBackdrop(x0, ryR, HL.ctw, RSH, 12, hubCur, f, now, 0.85);
        StrokeRR(x0, ryR, HL.ctw, RSH, 12, Pen(FA(0x1AFFFFFF, f), 1));
        double hvRs = hub.Hv(47);
        double rbx = x0 + 14, rby = ryR + 9 - 1.5 * hvRs;
        FillRR(rbx, rby, 158, 28, 8, VBrush(rbx, rby, 158, 28, FA(Alpha(hubCur, 40 + 55 * hvRs), f), FA(Alpha(hubCur, 16 + 26 * hvRs), f)));
        StrokeRR(rbx, rby, 158, 28, 8, Pen(FA(Alpha(hubCur, 120 + 110 * hvRs), f), 1.1));
        int stRs = PushXform(rbx + 15, rby + 14, 1, -220 * hvRs);
        var pnR = Pen(FA(Alpha(AccHi(hubCur, 0.5), 190 + 60 * hvRs), f), 1.5);
        Arc(rbx + 9, rby + 8, 12, 12, 40, 280, pnR);
        Line(rbx + 19.5, rby + 9.5, rbx + 21.5, rby + 6, pnR);
        Line(rbx + 19.5, rby + 9.5, rbx + 16, rby + 8, pnR);
        Pop(stRs);
        Txt("RESET APPEARANCE", rbx + 28, rby + 6.5, 126, 15, HL.fS, FA(Alpha(AccHi(hubCur, 0.4), 235), f), Fmt.L);
        if (HL.rstAt != 0 && now - HL.rstAt < 620)
        {
            double e = Ease3((now - HL.rstAt) / 620.0);
            double ex = e * 12;
            StrokeRR(rbx - ex, rby - ex * 0.6, 158 + ex * 2, 28 + ex * 1.2, 8 + ex * 0.3, Pen(FA(Alpha(HubState.C_ON, 180 * (1 - e)), f), 2 * (1 - e) + 0.4));
        }
        Txt("accent - opacities - theme back to factory", rbx + 172, ryR + 7, 240, 15, Fonts.fHint, FA(0x5CC7CBE0, f), Fmt.L);
        Txt("defaults: rose accent  \u00B7  100 / 100  \u00B7  dark base", rbx + 172, ryR + 24, 240, 13, HL.fXs, FA(0x40C7CBE0, f), Fmt.L);
        for (int j = 1; j <= 6; j++)
        {
            double gdx = x0 + HL.ctw - 24 - (6 - j) * 20;
            bool cur6 = HubState.Accent == HubState.Accents[j - 1];
            FillEll(gdx - 5, ryR + 18, 10, 10, SBrush(FA(Alpha(HubState.Accents[j - 1], cur6 ? 220 : 70), f)));
            if (cur6) Ell(gdx - 7.5, ryR + 15.5, 15, 15, Pen(FA(Alpha(0xFFFFFF, 170), f), 1.2));
        }
    }

    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        for (int j = 1; j <= 6; j++)
        {
            double cxj = HL.ctx + 118 + (j - 1) * 40, cyj = HL.setr + HL.rh / 2;
            if ((ux - cxj) * (ux - cxj) + (uy - cyj) * (uy - cyj) <= 196) return 19 + j;
        }
        double ry2 = HL.setr + (HL.rh + HL.rgap);
        if (uy >= ry2 + 5 && uy <= ry2 + HL.rh - 5 && ux >= HL.sldx - 8 && ux <= HL.sldx + HL.sldw + 8) return 42;
        double ry3 = HL.setr + 2 * (HL.rh + HL.rgap);
        if (uy >= ry3 + 5 && uy <= ry3 + HL.rh - 5 && ux >= HL.sldx - 8 && ux <= HL.sldx + HL.sldw + 8) return 46;
        double ry4 = HL.setr + 3 * (HL.rh + HL.rgap);
        if (uy >= ry4 + 6 && uy <= ry4 + HL.rh - 6)
        {
            if (ux >= HL.ctx + 118 && ux <= HL.ctx + 118 + 84) return 40;
            if (ux >= HL.ctx + 214 && ux <= HL.ctx + 214 + 84) return 41;
            if (ux >= HL.ctx + 396 && ux <= HL.ctx + 396 + 44) return 48;
        }
        if (uy >= HL.sett && uy <= HL.sett + HL.seth)
            for (int i = 1; i <= 4; i++)
            {
                double tX = HL.ctx + (i - 1) * (HL.setw + 10);
                if (ux >= tX && ux <= tX + HL.setw) return i == 1 ? 49 : i == 2 ? 50 : i == 3 ? 53 : 58;
            }
        if (ux >= HL.ctx + HL.ctw - 116 && ux <= HL.ctx + HL.ctw && uy >= HL.setp + 5 && uy <= HL.setp + 35) return 54;
        double ryR2 = Math.Min(HL.setp + 48, HubLayout.FtrY() - 46);
        if (ux >= HL.ctx + 14 && ux <= HL.ctx + 172 && uy >= ryR2 + 7 && uy <= ryR2 + 39) return 47;
        return 0;
    }

    /// <summary>The slider press: start the drag (3 backdrop, 4 elements) and set from the pointer.</summary>
    public static bool Press(HubSurface hub, int z, double ux)
    {
        if (z != 42 && z != 46) return false;
        hub.HL.drag = z == 42 ? 3 : 4;
        SlideSet(hub, ux, z == 42 ? 1 : 2);
        return true;
    }
    /// <summary>HubSlideSet(ux, which): 1 backdrop 0.15..1, 2 elements 0.35..1.</summary>
    public static void SlideSet(HubSurface hub, double ux, int which)
    {
        double v = Clamp((ux - hub.HL.sldx) / hub.HL.sldw, 0.0, 1.0);
        if (which == 2) HubState.Opacity = 0.35 + 0.65 * v;
        else HubState.BgOpacity = 0.15 + 0.85 * v;
    }
    /// <summary>The release after a slide: persist both.</summary>
    public static void SlideDone()
    {
        Ini.Write(Paths.IniFile, "hub", "opacity", Math.Round(HubState.Opacity, 3));
        Ini.Write(Paths.IniFile, "hub", "bg", Math.Round(HubState.BgOpacity, 3));
    }

    public static bool Click(HubSurface hub, int z)
    {
        if (z >= 20 && z <= 25) { hub.AccentSet(z - 19); return true; }
        if (z == 40) { ThemeSet(hub, 0); return true; }
        if (z == 41) { ThemeSet(hub, 1); return true; }
        if (z == 47) { ResetDesign(hub); return true; }
        if (z == 48) { ArmTintSet(hub, HubState.ArmTint == 0); return true; }
        if (z == 49) { LowPerfSet(hub, !HubState.LowPerf); return true; }
        if (z == 50) { TopSet(hub, !HubState.OnTop); return true; }
        if (z == 53) { TrueMinSet(hub, !HubState.TrueMin); return true; }
        if (z == 58) { AutoUpdSet(hub, !HubState.AutoUpdate); return true; }
        if (z == 54) { Gallery.Show(0, true); return true; }     // MANAGE: the gallery, in its manage mode
        return false;
    }

    // ---- the setters: HubArmTintSet, HubLowPerfSet, HubTopSet, HubTrueMinSet, HubAutoUpdSet, HubThemeSet, HubResetDesign ----
    public static void ArmTintSet(HubSurface hub, bool v)
    {
        HubState.ArmTint = v ? 1 : 0;
        Ini.Write(Paths.IniFile, "hub", "armtint", HubState.ArmTint);
        hub.HL.tintAt = Clock.Tick;
    }
    public static void LowPerfSet(HubSurface hub, bool v)
    {
        HubState.LowPerf = v;
        Ini.Write(Paths.IniFile, "hub", "lowperf", v ? 1 : 0);
        hub.HL.lpAt = Clock.Tick;
        Fonts.MeasureClear();
        hub.Tim(Pace.TICK_A);
    }
    public static void TopSet(HubSurface hub, bool v)
    {
        HubState.OnTop = v;
        Ini.Write(Paths.IniFile, "hub", "ontop", v ? 1 : 0);
        if (HubWindow.Win is { } w) w.Topmost = v;
        hub.HL.topAt = Clock.Tick;
    }
    public static void TrueMinSet(HubSurface hub, bool v)
    {
        HubState.TrueMin = v;
        Ini.Write(Paths.IniFile, "hub", "truemin", v ? 1 : 0);
        hub.HL.tmAt = Clock.Tick;
    }
    public static void AutoUpdSet(HubSurface hub, bool v)
    {
        HubState.AutoUpdate = v;
        Ini.Write(Paths.IniFile, "hub", "autoupdate", v ? 1 : 0);
        hub.HL.auAt = Clock.Tick;
    }
    public static void ThemeSet(HubSurface hub, int v)
    {
        if (HubState.Theme == v) return;
        HubState.ThemeSet(v);
        hub.HL.thFlashAt = Clock.Tick;
    }
    public static void ResetDesign(HubSurface hub)
    {
        ArmTintSet(hub, true);
        hub.AccentSet(1);
        HubState.BgOpacity = 1.0; HubState.Opacity = 1.0;
        Ini.Write(Paths.IniFile, "hub", "opacity", 1.0);
        Ini.Write(Paths.IniFile, "hub", "bg", 1.0);
        if (HubState.Theme != 0) ThemeSet(hub, 0);
        hub.HL.rstAt = Clock.Tick;
    }
}
