using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Shell.Hub.Tabs;

/// <summary>
/// Tab 6. The version chip and channel line, the release number as a
/// watermark, the changelog down a timeline that scrolls with the wheel, the
/// circuit trace under it, the tick rule, and the heart in the corner with
/// its rising hearts. The GALLERY panel on the right comes with the gallery
/// (phase 5); it only draws when the rotation has pictures, as in the .ahk.
/// </summary>
public static class UpdateLogs
{
    public static int LgIdx, LgNext; public static long LgAt, LgFadeAt;
    public static bool ReelAnimating => LgFadeAt != 0;
    /// <summary>The GALLERY reel: the gate's rotation, one picture every three seconds with a 420 ms cross-fade, the counter, the sweeping band, the progress line.</summary>
    static void Reel(HubSurface hub, double x0, double y0, double f, long now, uint hubCur, double emb2G)
    {
        var HL = hub.HL;
        Yuri.Gfx.Pool.Load();
        if (!Yuri.Gfx.Pool.HasRot) return;
        int lo = Yuri.Gfx.Pool.GalLo, hi = Yuri.Gfx.Pool.GalHi;
        if (LgIdx < lo || LgIdx > hi) { LgIdx = lo; LgNext = lo; LgAt = now; }
        if (LgFadeAt == 0 && now - LgAt >= 3000 && hi > lo) { LgNext = LgIdx >= hi ? lo : LgIdx + 1; LgFadeAt = now; }
        double lgf = LgFadeAt != 0 ? Ease3(Clamp((now - LgFadeAt) / 420.0, 0.0, 1.0)) : 1.0;
        double gx0 = x0 + HL.ctw - 224, gy0 = y0 + 40, gw2 = 212, gh2 = HubLayout.pd + HubLayout.ch - HL.cty - 96;
        ShadowDraw(gx0, gy0 + 2, gw2, gh2, 12, 4, 14, 14, f);
        Yuri.Gfx.Img.FitRR(Yuri.Gfx.Pool.SelAt(LgIdx), gx0, gy0, gw2, gh2, 12, f, 1.0, 0.5, 0.35);
        if (LgFadeAt != 0) Yuri.Gfx.Img.FitRR(Yuri.Gfx.Pool.SelAt(LgNext), gx0, gy0, gw2, gh2, 12, f * lgf, 1.0, 0.5, 0.35);
        if (LgFadeAt != 0 && lgf >= 1) { LgIdx = LgNext; LgFadeAt = 0; LgAt = now; }
        int st = PushG(); ClipRR(gx0, gy0, gw2, gh2, 12);
        double bandY = gy0 + DecT(now) * 0.05 % (gh2 + 60) - 30;
        FillRect(gx0, bandY, gw2, 20, VBrush(gx0, bandY, gw2, 20, Alpha(0xFFFFFF, 0), FA(Alpha(0xFFFFFF, 26), f)));
        int cur2 = LgFadeAt != 0 && lgf >= 0.5 ? LgNext : LgIdx;
        FillRR(gx0 + gw2 - 42, gy0 + gh2 - 24, 36, 18, 5, SBrush(FA(Alpha(0x0B0C14, 170), f)));
        Txt((cur2 - Yuri.Gfx.Pool.Base) + "/" + Yuri.Gfx.Pool.RotN, gx0 + gw2 - 42, gy0 + gh2 - 25, 36, 18, HL.fS, FA(Alpha(AccHi(hubCur, 0.4), 235), f), Fmt.C);
        Pop(st);
        StrokeRR(gx0, gy0, gw2, gh2, 12, Pen(FA(0x2EFFFFFF, f), 1));
        StrokeRR(gx0 - 3.5, gy0 - 3.5, gw2 + 7, gh2 + 7, 15, Pen(FA(Alpha(hubCur, R(150 + 50 * emb2G)), f), 1.4));
        var pnA = Pen(FA(Alpha(AccHi(hubCur, 0.4), 220), f), 1.8);
        Line(gx0 - 3.5, gy0 + 12, gx0 - 3.5, gy0 + 34, pnA); Line(gx0 + gw2 + 3.5, gy0 + gh2 - 34, gx0 + gw2 + 3.5, gy0 + gh2 - 12, pnA);
        double prg = LgFadeAt != 0 ? 1.0 : Clamp((now - LgAt) / 3000.0, 0.0, 1.0);
        FillRR(gx0, gy0 + gh2 + 10, gw2, 3, 1.5, SBrush(FA(0x1EFFFFFF, f)));
        FillRR(gx0, gy0 + gh2 + 10, Math.Max(gw2 * prg, 3), 3, 1.5, SBrush(FA(Alpha(hubCur, 210), f)));
        Txt("GALLERY", gx0 - 30, gy0 + gh2 + 20, gw2 + 60, 12, HL.fS, FA(Alpha(AccHi(hubCur, 0.3), 170), f), Fmt.C);
    }
    const uint AMBER = 0xFFFBBF24;
    public static Func<bool> GalHasRot = () => false;

    public static void Draw(HubSurface hub, double x0, double y0, double dx, double dy2, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        double emb2G = (Math.Sin(DecT(now) * 0.004) + 1) / 2;
        long scrA = hub.Tab == 6 ? HL.scrAt : now - 99999;
        double vbw2 = HL.vbw;
        FillRR(x0, y0 + 36, vbw2, 20, 6, SBrush(FA(Alpha(hubCur, 26 + 10 * emb2G), f)));
        MicroBackdrop(x0, y0 + 36, vbw2, 20, 6, hubCur, f, now, 0.8);
        StrokeRR(x0, y0 + 36, vbw2, 20, 6, Pen(FA(Alpha(hubCur, 130), f), 1));
        Txt(AppInfo.ZVer, x0, y0 + 35, vbw2, 20, Fonts.fBadge, FA(Alpha(AccHi(hubCur, 0.35), 240), f), Fmt.C);
        Txt("current build - " + (AppInfo.Stage != "" ? "channel: " + AppInfo.Stage.ToLowerInvariant() : "release channel"), x0 + vbw2 + 12, y0 + 38, 186, 15, Fonts.fHint, FA(0x66C7CBE0, f), Fmt.L);
        Txt(AppInfo.Version, x0 + HL.ctw - 352, y0 + 60, 150, 64, HL.fG, FA(Alpha(hubCur, 14), f), Fmt.R);
        Txt((AppInfo.Stage != "" ? AppInfo.Stage : "RELEASE") + " BUILD", x0 + HL.ctw - 352, y0 + 124, 150, 16, Fonts.fBadge, FA(Alpha(hubCur, 32), f), Fmt.R);
        var ents = Changelog.Entries;
        double tlx = x0 + 8;
        double listTop = y0 + 70, listBot = HubLayout.pd + HubLayout.ch - 22;
        double visH = listBot - listTop;
        HL.lgMax = Math.Max(0, ents.Length * 24 + 58 - visH);
        HL.lgScT = Clamp(HL.lgScT, 0.0, HL.lgMax);
        double scp = Clamp(HL.lgSc, 0.0, HL.lgMax);
        int cl = PushG();
        ClipRect(x0 - 8, listTop, HL.ctw - 224, visH);
        FillRect(tlx, listTop - scp, 1, ents.Length * 24 + 6, VBrush(tlx, listTop - scp, 1, ents.Length * 24 + 6, FA(Alpha(0xFFFFFF, 46), f), Alpha(0xFFFFFF, 0)));
        double ppy = listTop - scp + (DecT(now) * 0.045) % (ents.Length * 24 + 6);
        FillEll(tlx - 4, ppy - 4, 8, 8, SBrush(FA(Alpha(hubCur, 60), f)));
        FillEll(tlx - 1.6, ppy - 1.6, 3.2, 3.2, SBrush(FA(Alpha(AccHi(hubCur, 0.4), 220), f)));
        for (int i4 = 1; i4 <= ents.Length; i4++)
        {
            double tdel = Math.Min(Math.Max((now - scrA - 120 - i4 * 46) / 260.0, 0.0), 1.0);
            double ta = 1 - Math.Pow(1 - tdel, 3);
            double ey = y0 + 76 + (i4 - 1) * 24 + (1 - ta) * 6 - scp;
            if (ey > listBot || ey + 20 < listTop) continue;
            bool add = ents[i4 - 1].kind == "+";
            uint dcol = add ? hubCur : AMBER;
            FillEll(tlx - 5.5, ey + 1.5, 11, 11, SBrush(FA(Alpha(dcol, 70 * ta), f)));
            FillEll(tlx - 2.5, ey + 4.5, 5, 5, SBrush(FA(Alpha(dcol, 235 * ta), f)));
            Txt(ents[i4 - 1].kind, tlx + 14, ey - 1, 14, 16, Fonts.fBadge, FA(Alpha(dcol, 225 * ta), f), Fmt.L);
            double ewd = HL.ctw - 262;
            Txt(FFMElide(ents[i4 - 1].text, Fonts.fHint, ewd), tlx + 30, ey - 1, ewd, 16, Fonts.fHint, FA(Alpha(0xC7CBE0, 90 + 120 * ta), f), Fmt.L);
        }
        double dxL = x0 + 6, dyL = y0 + 76 + ents.Length * 24 + 30 - scp;
        if (dyL - 16 <= listBot)                                                   // the circuit trace
        {
            var pn = Pen(FA(Alpha(0xFFFFFF, 26), f), 1);
            Line(dxL, dyL, dxL + 44, dyL, pn);
            Line(dxL + 44, dyL, dxL + 44, dyL - 14, pn);
            Line(dxL + 44, dyL - 14, dxL + 108, dyL - 14, pn);
            Line(dxL + 108, dyL - 14, dxL + 108, dyL + 8, pn);
            Line(dxL + 108, dyL + 8, dxL + 168, dyL + 8, pn);
            var bN = SBrush(FA(Alpha(0xFFFFFF, 58), f));
            FillEll(dxL + 42.6, dyL - 1.4, 2.8, 2.8, bN);
            FillEll(dxL + 106.6, dyL - 15.4, 2.8, 2.8, bN);
            double tpL = (DecT(now) % 3200) / 3200.0 * 216;
            double pxL, pyL;
            if (tpL <= 44) { pxL = dxL + tpL; pyL = dyL; }
            else if (tpL <= 58) { pxL = dxL + 44; pyL = dyL - (tpL - 44); }
            else if (tpL <= 122) { pxL = dxL + 44 + (tpL - 58); pyL = dyL - 14; }
            else if (tpL <= 144) { pxL = dxL + 108; pyL = dyL - 14 + (tpL - 122); }
            else { pxL = dxL + 108 + (tpL - 144); pyL = dyL + 8; }
            FillEll(pxL - 4, pyL - 4, 8, 8, SBrush(FA(Alpha(hubCur, 55), f)));
            FillEll(pxL - 1.8, pyL - 1.8, 3.6, 3.6, SBrush(FA(Alpha(AccHi(hubCur, 0.4), 220), f)));
        }
        Pop(cl);
        if (HL.lgMax > 0)
        {
            double sbx2 = x0 + HL.ctw - 238;
            FillRR(sbx2, listTop, 3, visH, 1.5, SBrush(FA(Alpha(0xFFFFFF, 22), f)));
            double th2 = Math.Max(24, visH * visH / (visH + HL.lgMax));
            double ty2 = listTop + (visH - th2) * (scp / HL.lgMax);
            FillRR(sbx2, ty2, 3, th2, 1.5, SBrush(FA(Alpha(hubCur, 150 + 30 * emb2G), f)));
        }
        Reel(hub, x0, y0, f, now, hubCur, emb2G);
        for (int k = 1; k <= 7; k++)
        {
            double tkx2 = x0 + HL.ctw - 186;
            Line(tkx2, y0 + 74 + (k - 1) * 34, tkx2 + (k % 2 != 0 ? 7 : 4), y0 + 74 + (k - 1) * 34, Pen(FA(Alpha(0xFFFFFF, k % 2 != 0 ? 34 : 18), f), 1));
        }
        double ocx = x0 + 276, ocy = y0 + 46;
        var pnO = Pen(FA(Alpha(hubCur, 40), f), 1);
        PenDash(pnO, 2);
        Ell(ocx - 13, ocy - 13, 26, 26, pnO);
        double oaL = now * 0.0026;
        FillEll(ocx + 13 * Math.Cos(oaL) - 2, ocy + 13 * Math.Sin(oaL) - 2, 4, 4, SBrush(FA(Alpha(AccHi(hubCur, 0.5), 200), f)));
        FillEll(ocx - 3, ocy - 3, 6, 6, SBrush(FA(Alpha(hubCur, 120 + 60 * emb2G), f)));
        for (int k = 0; k < 3; k++)
        {
            double sxL = x0 + 308 + k * 10;
            double twL = Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.0023 + k * 2.0)), 5);
            var pn = Pen(FA(Alpha(AccHi(hubCur, 0.45), 40 + 170 * twL), f), 1.3);
            Line(sxL - 4, ocy, sxL + 4, ocy, pn);
            Line(sxL, ocy - 4, sxL, ocy + 4, pn);
        }
        double fy2 = y0 + 76 + ents.Length * 24 + 46;
        FadeLine(x0, x0 + HL.ctw - 216, fy2 - 4, 0x1CFFFFFF, f);
        double hbx = x0 + HL.ctw - 190, hby = HubLayout.pd + HubLayout.ch - 96;
        if (hby - 40 > fy2)
        {
            int ch = PushG();
            ClipRect(hbx - 70, hby - 54, 168, 104);
            for (int hk = 1; hk <= 7; hk++)
            {
                int cyc7 = 4200 + hk * 730;
                double ph7 = ((now + hk * 941) % cyc7) / (double)cyc7;
                double hx7 = hbx - 46 + (hk * 53) % 132 + 11 * Math.Sin(DecT(now) * 0.0011 + hk * 1.4);
                double hy7 = hby + 40 - ph7 * 112;
                double ha7 = Math.Sin(3.14159 * ph7);
                MiniHeart(hx7, hy7, 3.2 + (hk % 3) * 1.3, FA(Alpha(AccHi(hubCur, 0.32 + 0.12 * (hk % 2)), R(150 * ha7)), f));
            }
            double pu7 = 0.5 + 0.5 * Math.Sin(DecT(now) * 0.0021);
            FillEll(hbx - 34, hby - 34, 68, 68, SBrush(FA(Alpha(hubCur, R(12 + 8 * pu7)), f)));
            MiniHeart(hbx, hby - 2, 11 + 1.4 * pu7, FA(Alpha(AccHi(hubCur, 0.35), 205), f));
            var pnH = Pen(FA(Alpha(hubCur, R(40 + 26 * pu7)), f), 1.1);
            PenDash(pnH, 2);
            PenDashOff(pnH, (DecT(now) * 0.016) % 1000);
            Ell(hbx - 30, hby - 32, 60, 60, pnH);
            Txt("made with love", hbx - 74, hby + 34, 148, 13, HL.fXs, FA(Alpha(hubCur, 105), f), Fmt.C);
            Pop(ch);
        }
        Txt("v0.9 - internal: gate / loading / overlay / hub", x0, fy2 + 6, HL.ctw - 226, 14, Fonts.fHint, FA(0x42C7CBE0, f), Fmt.L);
        Txt("build 3.4.5", x0, fy2 + 24, 120, 14, Fonts.fHint, FA(Alpha(hubCur, 90), f), Fmt.L);
    }

    /// <summary>HubWheel on tab 6: 48 logical units per notch, within the list.</summary>
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (!(ux >= HL.ctx - 8 && ux <= HL.ctx - 8 + HL.ctw - 224 && uy >= HL.cty + 70 && uy <= HubLayout.pd + HubLayout.ch - 22)) return false;
        HL.lgScT = Clamp(HL.lgScT - delta * 48, 0.0, HL.lgMax);
        return true;
    }
}
