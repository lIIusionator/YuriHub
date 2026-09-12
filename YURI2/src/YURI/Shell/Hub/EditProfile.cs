using Yuri.Core;
using Yuri.Gfx;
using Yuri.Modules.FastFlags;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Hub;

/// <summary>
/// EDIT PROFILE: the 380x354 sheet over the hub - the portrait with CHANGE,
/// the four RING styles, NAME (16) and BIO (64) on the shared field, the
/// eight HUB FONT chips, DONE. Zones 130-148 as the .ahk's. Name and bio
/// restyle at once; the hub font applies next launch.
/// </summary>
public static class EditProfile
{
    const uint AMBER = 0xFFFBBF24;
    public static bool On; public static double T;
    static readonly string[] Fonts8 = { "Segoe UI", "Bahnschrift", "Consolas", "Georgia", "Trebuchet MS", "Candara", "Cambria", "Impact" };
    public static bool Animating => On || T > 0.01;
    static void Geom(out double ccx, out double ccy) { ccx = HubLayout.pd + HubLayout.cw / 2 - 190; ccy = HubLayout.pd + HubLayout.ch / 2 - 177; }
    public static void Open() { On = true; HubSurface.Live?.Tim(Pace.TICK_A); }
    public static void Close() { FfmField.End(true); On = false; Save(); HubSurface.Live?.Tim(Pace.TICK_A); }
    public static void Save()
    {
        Ini.Write(Paths.IniFile, "profile", "name", HubState.ProfName); Ini.Write(Paths.IniFile, "profile", "bio", HubState.ProfBio);
        Ini.Write(Paths.IniFile, "profile", "ring", (long)HubState.ProfRing); Ini.Write(Paths.IniFile, "profile", "font", (long)HubState.ProfileFont);
        Pool.ProfPicSave();
    }
    public static double TextX(HubLayout HL, string mode) { Geom(out double ccx, out _); return ccx + 36; }
    public static string BufFor(string mode) => mode == "pn" ? (HubState.ProfName == "USERNAME" ? "" : HubState.ProfName) : HubState.ProfBio == "EMPTY BIO" ? "" : HubState.ProfBio;
    public static void Commit(string mode, string buf)
    {
        string v = buf.Trim();
        if (mode == "pn") HubState.ProfName = v == "" ? "USERNAME" : v.Length > 16 ? v[..16] : v;
        else HubState.ProfBio = v == "" ? "EMPTY BIO" : v.Length > 64 ? v[..64] : v;
        Save();
    }
    public static int Zone(double ux, double uy)
    {
        if (!On || T < 0.4) return 0;
        Geom(out double ccx, out double ccy);
        if (ux < ccx || ux > ccx + 380 || uy < ccy || uy > ccy + 354) return 138;                    // the veil around the sheet: closes it
        if ((ux - (ccx + 354)) * (ux - (ccx + 354)) + (uy - (ccy + 24)) * (uy - (ccy + 24)) <= 169) return 130;
        if (ux >= ccx + 288 && ux <= ccx + 356 && uy >= ccy + 318 && uy <= ccy + 342) return 144;
        if ((ux - (ccx + 64)) * (ux - (ccx + 64)) + (uy - (ccy + 94)) * (uy - (ccy + 94)) <= 1296) return 131;
        // two rows of four, 131+1 .. 131+8
        for (int row8 = 0; row8 < 2; row8++)
            if (uy >= ccy + 66 + row8 * 30 && uy <= ccy + 92 + row8 * 30)
                for (int j = 1; j <= 4; j++)
                    if (ux >= ccx + 124 + (j - 1) * 60 && ux <= ccx + 176 + (j - 1) * 60) return row8 == 0 ? 131 + j : 149 + j;
        if (uy >= ccy + 152 && uy <= ccy + 178 && ux >= ccx + 24 && ux <= ccx + 200) return 136;
        if (uy >= ccy + 200 && uy <= ccy + 226 && ux >= ccx + 24 && ux <= ccx + 356) return 137;
        if (uy >= ccy + 248 && uy <= ccy + 274) for (int j = 1; j <= 4; j++) if (ux >= ccx + 24 + (j - 1) * 86 && ux <= ccx + 102 + (j - 1) * 86) return 139 + j;
        if (uy >= ccy + 282 && uy <= ccy + 308) for (int j = 1; j <= 4; j++) if (ux >= ccx + 24 + (j - 1) * 86 && ux <= ccx + 102 + (j - 1) * 86) return 144 + j;
        return 139;
    }
    public static bool Press(HubSurface hub, int z)
    {
        if (!On) return false;
        if (z == 136) { if (FfmField.Edit != "pn") FfmField.Begin("pn", 0); FfmField.Mouse(hub.PtrX); return true; }
        if (z == 137) { if (FfmField.Edit != "pb") FfmField.Begin("pb", 0); FfmField.Mouse(hub.PtrX); return true; }
        return false;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (!On) return false;
        switch (z)
        {
            case 130: case 144: case 138: Close(); break;
            case 131: FfmField.End(true); Gallery.Show(0, false); break;
            case >= 132 and <= 135: HubState.ProfRing = z - 131; Save(); break;
            case >= 150 and <= 153: HubState.ProfRing = z - 145; Save(); break;      // the second row: 150..153 -> 5..8
            case >= 140 and <= 143: HubState.ProfileFont = z - 139; Save(); break;
            case >= 145 and <= 148: HubState.ProfileFont = z - 140; Save(); break;
            case 139: break;
            default: return false;
        }
        hub.Tim(Pace.TICK_A);
        return true;
    }
    static string Fit(string s, double wmax, Font f, out int off) { off = 0; while (Fonts.MeasureW(s, f) > wmax && s.Length > 1) { s = s[1..]; off++; } return s; }
    public static void Draw(HubSurface hub, double cf, long now, uint hubCur)
    {
        T += ((On ? 1.0 : 0.0) - T) * EK(0.22); if (!On && T < 0.01) { T = 0; return; }
        var HL = hub.HL;
        double pf = T, e2 = 1 - Math.Pow(1 - pf, 3), cx = HubLayout.pd, cy = HubLayout.pd, cw = HubLayout.cw, ch = HubLayout.ch;
        FillRR(cx, cy, cw, ch, 20, SBrush(FA(Alpha(0x05060C, R(130 * pf)), cf)));
        Geom(out double ccx, out double ccy);
        int stPf = PushXform(cx + cw / 2, cy + ch / 2, 0.93 + 0.07 * e2, 0);
        ShadowDraw(ccx, ccy + 3, 380, 354, 16, 5, 16, 16, pf);
        FillRR(ccx, ccy, 380, 354, 16, VBrush(ccx, ccy, 380, 354, FA(0xFF1C2040, cf * pf), FA(0xFF12141F, cf * pf)));
        MiniBackdrop(ccx, ccy, 380, 354, 16, hubCur, cf * pf, now, 0.9);
        StrokeRR(ccx, ccy, 380, 354, 16, Pen(FA(Alpha(hubCur, R(150 * pf)), cf), 1.3));
        int stM = PushG(); ClipRR(ccx, ccy, 380, 354, 16);
        if (!HubState.LowPerf) { var bD = SBrush(FA(Alpha(0xFFFFFF, R(9 * pf)), cf)); for (int gr3 = 0; gr3 < 7; gr3++) for (int gc3 = 0; gc3 < 10; gc3++) FillEll(ccx + 18 + gc3 * 38, ccy + 56 + gr3 * 36, 1.8, 1.8, bD); }
        FillEll(ccx - 40, ccy + 26, 176, 176, SBrush(FA(Alpha(hubCur, R(26 * pf)), cf)));
        FillRect(ccx, ccy, 380, 46, VBrush(ccx, ccy, 380, 46, FA(0x12FFFFFF, cf * pf), Alpha(0xFFFFFF, 0)));
        double swm = DecT(now) * 0.06 % 760;
        if (swm < 380) FillRect(ccx + swm - 60, ccy + 45, 60, 1.6, VBrush(ccx + swm - 60, ccy + 45, 60, 1.6, Alpha(hubCur, 0), FA(Alpha(hubCur, R(130 * pf)), cf)));
        Pop(stM);
        FadeLine(ccx + 16, ccx + 364, ccy + 45, Alpha(0xFFFFFF, R(34 * pf)), cf);
        double m1 = Clamp((pf - 0.30) / 0.36, 0.0, 1.0), m2 = Clamp((pf - 0.42) / 0.36, 0.0, 1.0), m3 = Clamp((pf - 0.54) / 0.34, 0.0, 1.0), m4 = Clamp((pf - 0.66) / 0.30, 0.0, 1.0), pfb = pf;
        pf = pfb * m1;
        FillRR(ccx, ccy + 14, 3, 24, 1.5, SBrush(FA(Alpha(hubCur, R(205 * pf)), cf)));
        Txt("EDIT PROFILE", ccx + 16 - (1 - m1) * 8, ccy + 12, 200, 20, Fonts.fStatus, FA(Alpha(Mix(0xFFE8EAF6, hubCur, 0.2), R(240 * pf)), cf), Fmt.L);
        double h130 = hub.Hv(130), ec3 = 1 + 0.26 * h130, xcb = ccx + 354 + 2 * h130, ycb3 = ccy + 24 - 1 * h130;
        FillEll(xcb - 9 * ec3, ycb3 - 9 * ec3, 18 * ec3, 18 * ec3, SBrush(FA(Alpha(hubCur, R((34 + 66 * h130) * pf)), cf)));
        Ell(xcb - 9 * ec3, ycb3 - 9 * ec3, 18 * ec3, 18 * ec3, Pen(FA(Alpha(hubCur, R((95 + 90 * h130) * pf)), cf), 1));
        if (h130 > 0.01) { double rp3 = DecT(now) % 1300 / 1300.0; Ell(xcb - 9 - rp3 * 8, ycb3 - 9 - rp3 * 8, 18 + rp3 * 16, 18 + rp3 * 16, Pen(FA(Alpha(hubCur, R(110 * (1 - rp3) * h130 * pf)), cf), 1.2)); }
        int stXc = PushXform(xcb, ycb3, 1, 90 * h130);
        var pnX = Pen(FA(Alpha(AccHi(hubCur, 0.45), R((215 + 40 * h130) * pf)), cf), 1.7);
        Line(xcb - 4 * ec3, ycb3 - 4 * ec3, xcb + 4 * ec3, ycb3 + 4 * ec3, pnX); Line(xcb - 4 * ec3, ycb3 + 4 * ec3, xcb + 4 * ec3, ycb3 - 4 * ec3, pnX);
        Pop(stXc);
        pf = pfb * m2;
        double h131 = hub.Hv(131), acy3 = ccy + 94 + (1 - m2) * 8;
        FillEll(ccx + 64 - 44, acy3 - 44, 88, 88, SBrush(FA(Alpha(hubCur, R((26 + 30 * h131) * pf)), cf)));
        ProfilePlate.Avatar(ccx + 64, acy3, 32, cf * pf, now);
        ProfilePlate.Ring(ccx + 64, acy3, 36, hubCur, cf * pf, h131, now);
        for (int k6 = 1; k6 <= 3; k6++) { double oa4 = now * 0.0013 + k6 * 2.094; FillEll(ccx + 64 + 41 * Math.Cos(oa4) - 1.8, acy3 + 41 * Math.Sin(oa4) - 1.8, 3.6, 3.6, SBrush(FA(Alpha(AccHi(hubCur, 0.5), R((70 + 90 * h131) * pf)), cf))); }
        if (h131 > 0.01) { FillEll(ccx + 32, acy3 - 32, 64, 64, SBrush(FA(Alpha(0x05060C, R(155 * h131 * pf)), cf))); int stCg = PushXform(ccx + 64, acy3 + 2, 0.9 + 0.1 * h131, 0); Txt("CHANGE", ccx + 32, acy3 - 8, 64, 12, HL.fS, FA(Alpha(0xFFFFFF, R(245 * h131 * pf)), cf), Fmt.C); Pop(stCg); }
        Txt("RING", ccx + 124, ccy + 50, 80, 13, HL.fS, FA(Alpha(0xFFC7CBE0, R(120 * pf)), cf), Fmt.L);
        string[] rlabs = { "SOLID", "DASH", "DUAL", "ORBIT", "PULSE", "TICKS", "SEGMENT", "HALO" };
        for (int j = 1; j <= 8; j++)
        {
            int col8 = (j - 1) % 4, row8 = (j - 1) / 4;
            double hvR2 = hub.Hv(row8 == 0 ? 131 + j : 145 + j), rxb = ccx + 124 + col8 * 60, ryb = ccy + 66 + row8 * 30 - 1.5 * hvR2;
            bool selR = HubState.ProfRing == j;
            FillRR(rxb, ryb, 52, 26, 7, SBrush(FA(Alpha(selR ? hubCur : 0xFFFFFF, R(selR ? 36 : 8 + 18 * hvR2)), cf * pf)));
            MicroBackdrop(rxb, ryb, 52, 26, 7, hubCur, cf * pf, now, 0.8);
            StrokeRR(rxb, ryb, 52, 26, 7, Pen(FA(Alpha(selR ? hubCur : 0xFFFFFF, R(selR ? 200 : 30 + 70 * hvR2)), cf * pf), 1));
            Txt(rlabs[j - 1], rxb, ryb + 6, 52, 14, HL.fXs, FA(Alpha(selR ? 0xFFFFFF : 0xFFC7CBE0, R((selR ? 240 : 150 + 60 * hvR2) * pf)), cf), Fmt.C);
            if (selR) { FillRR(rxb + 18, ryb + 23, 16, 2.2, 1.1, SBrush(FA(Alpha(hubCur, R(220 * pf)), cf))); double pp2 = DecT(now) % 1400 / 1400.0; StrokeRR(rxb - pp2 * 5, ryb - pp2 * 3, 52 + pp2 * 10, 26 + pp2 * 6, 7 + pp2 * 2, Pen(FA(Alpha(hubCur, R(110 * (1 - pp2) * pf)), cf), 1.2)); }
        }
        Txt("applies to your portrait everywhere", ccx + 124, ccy + 128, 240, 13, HL.fXs, FA(Alpha(0xFFC7CBE0, R(90 * pf)), cf), Fmt.L);
        pf = pfb * m3;
        Field(hub, "pn", ccx, ccy + 136, ccy + 152, 176, 152, "NAME", HubState.ProfName == "USERNAME" ? "" : HubState.ProfName, "your name", 16, 136, cf, pf, now, hubCur, 140);
        Field(hub, "pb", ccx, ccy + 184, ccy + 200, 332, 306, "BIO", HubState.ProfBio == "EMPTY BIO" ? "" : HubState.ProfBio, "write a short bio", 64, 137, cf, pf, now, hubCur, 296);
        pf = pfb * m4;
        Txt("HUB FONT", ccx + 24, ccy + 232, 90, 13, HL.fS, FA(Alpha(0xFFC7CBE0, R(120 * pf)), cf), Fmt.L);
        Txt(Fonts8[Math.Clamp(HubState.ProfileFont, 1, 8) - 1], ccx + 116, ccy + 232, 240, 13, HL.fXs, FA(Alpha(hubCur, R(140 * pf)), cf), Fmt.R);
        string[] flabs = { "SEGOE", "BAHN", "MONO", "SERIF", "TREB", "CAND", "CAMBR", "IMPACT" };
        for (int j = 1; j <= 8; j++)
        {
            int zidF = j <= 4 ? 139 + j : 140 + j;
            double hvF = hub.Hv(zidF), fxb = ccx + 24 + ((j - 1) % 4) * 86, fyb = ccy + 248 + (j > 4 ? 34 : 0) - 1.5 * hvF;
            bool selF = HubState.ProfileFont == j;
            FillRR(fxb, fyb, 78, 26, 7, SBrush(FA(Alpha(selF ? hubCur : 0xFFFFFF, R(selF ? 36 : 8 + 18 * hvF)), cf * pf)));
            MicroBackdrop(fxb, fyb, 78, 26, 7, hubCur, cf * pf, now, 0.8);
            StrokeRR(fxb, fyb, 78, 26, 7, Pen(FA(Alpha(selF ? hubCur : 0xFFFFFF, R(selF ? 200 : 30 + 70 * hvF)), cf * pf), 1));
            Txt(flabs[j - 1], fxb, fyb + 6, 78, 14, HL.fXs, FA(Alpha(selF ? 0xFFFFFF : 0xFFC7CBE0, R((selF ? 240 : 150 + 60 * hvF) * pf)), cf), Fmt.C);
            if (selF) FillRR(fxb + 31, fyb + 23, 16, 2.2, 1.1, SBrush(FA(Alpha(hubCur, R(220 * pf)), cf)));
        }
        FadeLine(ccx + 24, ccx + 274, ccy + 312, Alpha(0xFFFFFF, R(30 * pf)), cf);
        Txt("name + bio restyle now - hub font applies next launch", ccx + 24, ccy + 322, 244, 14, HL.fXs, FA(Alpha(0xFFC7CBE0, R(115 * pf)), cf), Fmt.L);
        double hDn = hub.Hv(144), dny = ccy + 318 - 1.5 * hDn;
        FillRR(ccx + 288, dny, 68, 24, 8, VBrush(ccx + 288, dny, 68, 24, FA(Alpha(hubCur, R((34 + 54 * hDn) * pf)), cf), FA(Alpha(hubCur, R((14 + 28 * hDn) * pf)), cf)));
        MicroBackdrop(ccx + 288, dny, 68, 24, 8, hubCur, cf, now, 0.9);
        StrokeRR(ccx + 288, dny, 68, 24, 8, Pen(FA(Alpha(hubCur, R((120 + 110 * hDn) * pf)), cf), 1.1));
        var pnK = Pen(FA(Alpha(AccHi(hubCur, 0.5), R((170 + 85 * hDn) * pf)), cf), 1.6);
        Line(ccx + 300, dny + 12, ccx + 304, dny + 16, pnK); Line(ccx + 304, dny + 16, ccx + 311, dny + 8, pnK);
        Txt("DONE", ccx + 312, dny + 5, 40, 14, HL.fXs, FA(Alpha(AccHi(hubCur, 0.4), R(240 * pf)), cf), Fmt.C);
        Pop(stPf);
    }
    static void Field(HubSurface hub, string mode, double ccx, double ly, double fy, double fw, double tw, string label, string val, string ph, int max, int zid, double cf, double pf, long now, uint hubCur, double cntX)
    {
        var HL = hub.HL;
        bool foc = FfmField.Edit == mode;
        string shown = foc ? FfmField.Buf : val;
        Txt(label, ccx + 24, ly, 60, 13, HL.fS, FA(Alpha(foc ? hubCur : 0xFFC7CBE0, R((foc ? 190 : 120) * pf)), cf), Fmt.L);
        Txt(shown.Length + "/" + max, ccx + cntX, ly, 60, 13, HL.fS, FA(Alpha(shown.Length > max - 3 ? AMBER : 0xFFC7CBE0, R(110 * pf)), cf), Fmt.R);
        double hv = hub.Hv(zid);
        FillRR(ccx + 24, fy, fw, 26, 7, SBrush(FA(Mix(0xFF141728, 0xFF1B2038, Math.Max(hv, foc ? 1.0 : 0)), cf * pf)));
        MicroBackdrop(ccx + 24, fy, fw, 26, 7, hubCur, cf * pf, now, 0.75);
        StrokeRR(ccx + 24, fy, fw, 26, 7, Pen(FA(Alpha(foc ? hubCur : 0xFFFFFF, R(foc ? 190 : 30 + 50 * hv)), cf * pf), 1));
        if (foc) FillRR(ccx + 24, fy + 6, 2.6, 14, 1.3, SBrush(FA(Alpha(hubCur, R(210 * pf)), cf)));
        string vis = Fit(shown, tw, Fonts.fHint, out int off);
        if (foc) FfmField.Paint(ccx + 36, fy, 26, vis, off, hubCur, cf * pf, now, Fonts.fHint);
        Txt(shown == "" ? ph : vis, ccx + 36, fy, tw, 26, Fonts.fHint, FA(Alpha(shown == "" ? 0xFF9AA8C0 : 0xFFE8EAF6, R((shown == "" ? 110 : 225) * pf)), cf), Fmt.L);
    }
}
