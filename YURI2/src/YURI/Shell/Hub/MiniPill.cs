using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Hub;

/// <summary>
/// The minimised hub: MBW x MBH of gallery reel in a lit frame, sitting where
/// the card's bottom-right corner was. The .ahk's `bt_` block in HubRender,
/// verbatim - the collapse cues (a ring closing onto the orb plus inbound
/// sparks), the reel with its scrim and travelling band, the page dots, the
/// grid button, the border stack, the four EDIT PROFILE ring styles, the
/// hover and armed states, the burst and scatter rings, and its own drag veil.
///
/// It is drawn only when TRUE MINIMISE is off; with that on the window is
/// hidden outright and there is no pill at all (see HubMinPress).
/// </summary>
public static class MiniPill
{
    public const double MBW = 74, MBH = 96;
    const double GR = 16;                                          // gr3: the reel's corner radius

    /// <summary>The collapse cues, drawn under the orb while the fold is running.</summary>
    public static void Cues(HubLayout HL, long now, uint bcol)
    {
        if (HL.minStart == 0) return;
        bool mDir = HL.minTo == 1.0;
        double mp = Clamp(HL.minV, 0.0, 1.0);
        double rp = mDir ? mp : 1 - mp;
        if (rp > 0.02 && rp < 0.995)
        {
            double rr = Lerp(330, 34, Ease3(rp));
            int ra = R(165 * Math.Pow(1 - rp, 1.6) * (mDir ? 1 : 0.8));
            if (ra > 3) Ell(HL.mbx - rr, HL.mby - rr, rr * 2, rr * 2, Pen(Alpha(AccHi(bcol, 0.25), ra), 2.6 * (1 - rp) + 0.5));
        }
        double sp = Math.Sin(3.14159 * mp);
        if (sp > 0.04)
            for (int sk = 1; sk <= 8; sk++)
            {
                double sAng = sk * 0.7854 + (mDir ? 0.35 : 1.95);
                double d0 = 215 + sk * 67 % 110;
                double dd = mDir ? Lerp(d0, 30, Ease3(mp)) : Lerp(30, d0, Ease3(1 - mp));
                FillEll(HL.mbx + dd * Math.Cos(sAng) - 1.7, HL.mby + dd * Math.Sin(sAng) - 1.7, 3.4, 3.4, SBrush(Alpha(AccHi(bcol, 0.5), R(205 * sp))));
                FillEll(HL.mbx + (dd + 9) * Math.Cos(sAng) - 1.1, HL.mby + (dd + 9) * Math.Sin(sAng) - 1.1, 2.2, 2.2, SBrush(Alpha(AccHi(bcol, 0.3), R(70 * sp))));
            }
    }

    /// <summary>The orb itself. h9 is the hover ease on zone 9, mbA the armed strength.</summary>
    public static void Draw(HubSurface hub, HubLayout HL, long now, uint bcol, double mbA, double embG, double h9)
    {
        double bt = Math.Max(0.0, (Clamp(HL.minV, 0.0, 1.0) - 0.34) / 0.66);
        if (bt <= 0.004) return;
        double eb = 1 + 2.3 * Math.Pow(bt - 1, 3) + 1.3 * Math.Pow(bt - 1, 2);
        double prsB = HL.scatAt != 0 && now - HL.scatAt < 120 ? 1 - 0.06 * (1 - (now - HL.scatAt) / 120.0) : 1;
        double bsc = (0.55 + 0.45 * eb) * prsB * (1 + 0.05 * h9);
        double rotB = -35 * Math.Pow(1 - Clamp(bt, 0.0, 1.0), 2);
        double fB = Math.Min(bt * 1.6, 1.0);
        double mbPul = (Math.Sin(DecT(now) * 0.0021) + 1) / 2;
        int stB = PushXform(HL.mbx, HL.mby, bsc, rotB);

        if (h9 > 0.01) FillRR(HL.mbx - MBW / 2 - 7, HL.mby - MBH / 2 - 7, MBW + 14, MBH + 14, 22, SBrush(FA(Alpha(bcol, 26 * h9), fB)));

        // ---- the reel: tall gallery pictures, the avatar when there is no rotation ----
        double gw = MBW, gh = MBH, gx = HL.mbx - gw / 2, gy = HL.mby - gh / 2;
        if (Pool.HasRot)
        {
            if (HL.mbIdx < Pool.GalLo || HL.mbIdx > Pool.GalHi) { HL.mbIdx = Pool.GalLo; HL.mbNext = Pool.GalLo; }
            // ambient motion: a picture every 4.2 s. LOW PERFORMANCE MODE draws none of it,
            // so the pill keeps the picture it has and the renderer can stop on it.
            if (!HubState.LowPerf && HL.mbFade == 0 && now - HL.mbAt >= 4200)
            {
                HL.mbNext = HL.mbIdx >= Pool.GalHi ? Pool.GalLo : HL.mbIdx + 1;
                HL.mbFade = now; HL.mbAt = now;
            }
        }
        double gfd = HL.mbFade != 0 ? Ease3((now - HL.mbFade) / 460.0) : 1.0;

        for (int i = 1; i <= 5; i++)
            FillRR(gx - i, gy - i + 2, gw + i * 2, gh + i * 2, GR + i, SBrush(FA(Alpha(0x000000, 10 + 5 * HL.dragT), fB)));
        FillRR(gx, gy, gw, gh, GR, SBrush(FA(Alpha(0xFF0B0C14, 235), fB)));
        if (Pool.HasRot)
        {
            Img.FitRR(Pool.SelAt(HL.mbIdx), gx, gy, gw, gh, GR, fB);
            if (HL.mbFade != 0) Img.FitRR(Pool.SelAt(HL.mbNext), gx, gy, gw, gh, GR, fB * gfd);
            if (HL.mbFade != 0 && gfd >= 1) { HL.mbIdx = HL.mbNext; HL.mbFade = 0; }
        }
        else ProfilePlate.Avatar(HL.mbx, HL.mby, gw / 2 - 4, fB, now);

        // bottom scrim so the glyphs stay legible over any photo, and the drifting band
        int clip = PushG();
        ClipRR(gx, gy, gw, gh, GR);
        FillRect(gx, gy + gh - 30, gw, 30, VBrush(gx, gy + gh - 30, gw, 30, Alpha(0x000000, 0), FA(0x9E000000, fB)));
        double bandY = gy + DecT(now) * 0.045 % (gh + 50) - 25;
        FillRect(gx, bandY, gw, 18, VBrush(gx, bandY, gw, 18, Alpha(0xFFFFFF, 0), FA(Alpha(0xFFFFFF, 22), fB)));
        Pop(clip);

        // one dot per ROTATION entry - not per pool entry, which also holds the
        // pictures taken out of the rotation and the overlay portrait
        if (Pool.HasRot)
            for (int dk = 1; dk <= Pool.RotN; dk++)
            {
                bool onD = dk == HL.mbIdx - Pool.Base;
                FillEll(HL.mbx - (Pool.RotN - 1) * 3 + (dk - 1) * 6 - 1.6, gy + gh - 11, 3.2, 3.2,
                        SBrush(FA(Alpha(onD ? AccHi(bcol, 0.4) : 0xFFFFFF, onD ? 235 : 70), fB)));
            }

        if (h9 > 0.01)
        {
            int st = PushG();
            ClipRR(HL.mbx - MBW / 2, HL.mby - MBH / 2, MBW, MBH, GR);
            FillRR(HL.mbx - MBW / 2, HL.mby - MBH / 2, MBW, MBH, GR, SBrush(FA(Alpha(0x05060C, 168 * h9), fB)));
            double gy2 = HL.mby - 14 + (1 - h9) * 7;
            var pn = Pen(FA(Alpha(0xFFFFFF, 215 * h9), fB), 1.6);
            Line(HL.mbx - 5, gy2 + 4, HL.mbx, gy2 - 1, pn);
            Line(HL.mbx, gy2 - 1, HL.mbx + 5, gy2 + 4, pn);
            Line(HL.mbx - 5, gy2 + 10, HL.mbx, gy2 + 5, pn);
            Line(HL.mbx, gy2 + 5, HL.mbx + 5, gy2 + 10, pn);
            Txt("OPEN", HL.mbx - 26, HL.mby + 3 + (1 - h9) * 7, 52, 14, Fonts.fBadge, FA(Alpha(0xFFFFFF, 245 * h9), fB), Fmt.C);
            Pop(st);
        }

        // ---- border stack: soft halo, gradient ring, inner hairline, brackets ----
        double bxL = HL.mbx - MBW / 2, bxR = HL.mbx + MBW / 2, byT = HL.mby - MBH / 2, byB = HL.mby + MBH / 2;
        for (int hk = 1; hk <= 4; hk++)
        {
            double ha = (26 - hk * 5) * (0.55 + 0.45 * embG) * (1 + 0.55 * mbA * mbPul);
            FillRR(bxL - hk * 2.2, byT - hk * 2.2, MBW + hk * 4.4, MBH + hk * 4.4, GR + hk * 2, SBrush(FA(Alpha(bcol, R(Math.Min(255, ha))), fB)));
        }
        // the bright end of the ring drifts, so the rim light reads as light
        // moving over the frame rather than a decal
        double rimT = (Math.Sin(DecT(now) * 0.0013) + 1) / 2;
        var rimBr = LineBrush(bxL - 1, byT - 1, MBW + 2, MBH + 2,
                        FA(Alpha(AccHi(bcol, 0.45 + 0.30 * rimT), 240), fB),
                        FA(Alpha(Mix(bcol, 0xFF000000, 0.24 - 0.14 * rimT), R(150 + 30 * rimT)), fB), 2);
        if (rimBr is not null) StrokeRR(bxL - 1, byT - 1, MBW + 2, MBH + 2, GR + 1, new Avalonia.Media.Pen(rimBr, 1.8));

        double stbW = 14 + 16 * mbA;                                             // status bar set into the top edge
        FillRR(HL.mbx - stbW / 2, byT - 1.4, stbW, 2.8, 1.4,
               VBrush(HL.mbx - stbW / 2, byT - 1.4, stbW, 2.8, FA(Alpha(AccHi(bcol, 0.7), R(120 + 120 * mbA)), fB), FA(Alpha(bcol, R(60 + 90 * mbA)), fB)));
        StrokeRR(bxL + 1.5, byT + 1.5, MBW - 3, MBH - 3, GR - 1.5, Pen(FA(Alpha(AccHi(bcol, 1 - 0.55 * mbA), R(38 + 26 * mbA)), fB), 1));
        for (int ck = 0; ck <= 3; ck++)
        {
            double csh = (Math.Sin(DecT(now) * 0.0032 + ck * 1.5708) + 1) / 2;
            double cxk = ck == 1 || ck == 2 ? bxR - GR * 2 : bxL;
            double cyk = ck >= 2 ? byB - GR * 2 : byT;
            double cang = ck == 0 ? 195 : ck == 1 ? 285 : ck == 2 ? 15 : 105;
            Arc(cxk, cyk, GR * 2, GR * 2, cang, 60, Pen(FA(Alpha(bcol, R(Math.Min(255, 40 + 38 * csh + 74 * mbA))), fB), 1.5 + 0.4 * mbA));
        }

        Style(HL, now, bcol, mbA, fB, bxL, byT);

        if (mbA > 0.02)                                                          // armed: a slow ping shedding off the frame
        {
            double pt5 = DecT(now) % 1700 / 1700.0;
            double ex7 = 3 + pt5 * 22;
            StrokeRR(bxL - ex7, byT - ex7, MBW + ex7 * 2, MBH + ex7 * 2, GR + ex7 * 0.5, Pen(FA(Alpha(bcol, R(115 * mbA * Math.Pow(1 - pt5, 1.5))), fB), 2.0 * (1 - pt5) + 0.3));
        }
        if (h9 > 0.01)                                                           // hover: two crossing dashed orbits, then a ping
        {
            var pnO = Pen(FA(Alpha(bcol, R((60 + 25 * embG) * h9)), fB), 1.2);
            PenDash(pnO, 2); PenDashOff(pnO, 1000 - DecT(now) * 0.02 % 1000);
            StrokeRR(bxL - 8, byT - 8, MBW + 16, MBH + 16, GR + 8, pnO);
            var pnI = Pen(FA(Alpha(AccHi(bcol, 0.35), R(42 * h9)), fB), 1);
            PenDash(pnI, 1); PenDashOff(pnI, DecT(now) * 0.034 % 1000);
            StrokeRR(bxL - 4, byT - 4, MBW + 8, MBH + 8, GR + 4, pnI);
            double prt = DecT(now) % 950 / 950.0, ex4 = 6 + prt * 13;
            StrokeRR(HL.mbx - MBW / 2 - ex4, HL.mby - MBH / 2 - ex4, MBW + ex4 * 2, MBH + ex4 * 2, 20 + ex4 * 0.4, Pen(FA(Alpha(bcol, 130 * h9 * (1 - prt)), fB), 2.2 * (1 - prt) + 0.4));
        }
        if (HL.scatAt != 0 && now - HL.scatAt < 380)                             // expand: the scatter ring
        {
            double e = Ease3((now - HL.scatAt) / 380.0), ex5 = 2 + e * 14;
            StrokeRR(HL.mbx - MBW / 2 - ex5, HL.mby - MBH / 2 - ex5, MBW + ex5 * 2, MBH + ex5 * 2, 18 + ex5 * 0.4, Pen(FA(Alpha(0xFFFFFF, 190 * (1 - e)), fB), 2.6 * (1 - e) + 0.4));
        }
        if (HL.burstAt != 0 && now >= HL.burstAt)                                // collapse: the burst as the orb lands
        {
            double btm = (now - HL.burstAt) / 450.0;
            if (btm >= 1) HL.burstAt = 0;
            else
            {
                double e = 1 - Math.Pow(1 - btm, 3), ex6 = 2 + e * 20;
                StrokeRR(HL.mbx - MBW / 2 - ex6, HL.mby - MBH / 2 - ex6, MBW + ex6 * 2, MBH + ex6 * 2, 18 + ex6 * 0.5, Pen(FA(Alpha(bcol, 170 * (1 - e)), fB), 2.6 * (1 - e) + 0.4));
            }
        }

        // the 2x2 grid button in the bottom-right corner
        double gqx = HL.mbx + 12, gqy = HL.mby + 12;
        FillEll(gqx + 1, gqy + 2.5, 20, 19, SBrush(FA(0x59000000, fB)));
        FillRR(gqx, gqy, 20, 20, 6, VBrush(gqx, gqy, 20, 20, FA(0xFF3A3D58, fB), FA(0xFF20223A, fB)));
        MicroBackdrop(gqx, gqy, 20, 20, 6, bcol, fB, now, 0.8);
        StrokeRR(gqx, gqy, 20, 20, 6, Pen(FA(Alpha(bcol, 150 + 70 * embG), fB), 1));
        var qb = SBrush(FA(Alpha(bcol, 240), fB));
        for (int q = 1; q <= 4; q++)
            FillRR(gqx + 5.4 + (q - 1) % 2 * 5.4, gqy + 5.4 + (q - 1) / 2 * 5.4, 3.8, 3.8, 1.1, qb);

        if (HL.dragT > 0.01 && HL.minT >= 0.5)                                   // the pill's own drag veil
        {
            double d = HL.dragT;
            FillRR(HL.mbx - MBW / 2, HL.mby - MBH / 2, MBW, MBH, GR, SBrush(FA(Alpha(0x0B0C14, 118 * d), fB)));
            var pnD = Pen(FA(Alpha(bcol, 200 * d), fB), 1.6);
            PenDash(pnD, 1); PenDashOff(pnD, DecT(now) * 0.03 % 1000);
            StrokeRR(HL.mbx - MBW / 2 - 7, HL.mby - MBH / 2 - 7, MBW + 14, MBH + 14, 22, pnD);
        }
        Pop(stB);
    }

    /// <summary>
    /// The style layer follows the portrait ring chosen in EDIT PROFILE, so the
    /// collapsed orb is recognisably the same identity as the avatar.
    /// </summary>
    static void Style(HubLayout HL, long now, uint bcol, double mbA, double fB, double bxL, double byT)
    {
        if (HubState.ProfRing == 2)                                              // DASH: two dashed frames turning against each other
        {
            var p1 = Pen(FA(Alpha(bcol, R(150 + 60 * mbA)), fB), 1.6);
            PenDash(p1, 1); PenDashOff(p1, DecT(now) * 0.02 % 1000);
            StrokeRR(bxL - 3, byT - 3, MBW + 6, MBH + 6, GR + 3, p1);
            var p2 = Pen(FA(Alpha(AccHi(bcol, 0.4), R(70 + 60 * mbA)), fB), 1);
            PenDash(p2, 2); PenDashOff(p2, 1000 - DecT(now) * 0.031 % 1000);
            StrokeRR(bxL - 7, byT - 7, MBW + 14, MBH + 14, GR + 7, p2);
        }
        else if (HubState.ProfRing == 3)                                         // DUAL: two comet heads on opposite sides
        {
            double d0 = DecT(now) * 0.00042 % 1.0;
            for (int hd2 = 0; hd2 < 2; hd2++)
            {
                double hd = hd2 * 0.5;
                for (int tk = 0; tk < 5; tk++)
                {
                    Modules.FastFlags.FfmViews.RectPerim(d0 + hd - tk * 0.014, bxL - 3, byT - 3, MBW + 6, MBH + 6, out double gxg, out double gyg);
                    double ta = Math.Pow(1 - tk / 5.0, 2), r6 = 2.2 * ta + 0.4;
                    FillEll(gxg - r6, gyg - r6, r6 * 2, r6 * 2, SBrush(FA(Alpha(AccHi(bcol, 0.5), R(215 * ta)), fB)));
                }
            }
        }
        else if (HubState.ProfRing == 4)                                         // ORBIT: three satellites on three offset tracks
        {
            for (int ok = 1; ok <= 3; ok++)
            {
                double ins = -1 - ok * 4;
                double spd = ok == 1 ? 0.00042 : ok == 2 ? -0.00028 : 0.00019;
                Modules.FastFlags.FfmViews.RectPerim(now * spd + ok * 0.31, bxL + ins, byT + ins, MBW - ins * 2, MBH - ins * 2, out double gxg, out double gyg);
                if (ok == 1)
                {
                    FillEll(gxg - 6, gyg - 6, 12, 12, SBrush(FA(Alpha(bcol, 55), fB)));
                    FillEll(gxg - 2, gyg - 2, 4, 4, SBrush(FA(Alpha(AccHi(bcol, 0.6), 210), fB)));
                }
                else
                {
                    double sr = ok == 2 ? 2.4 : 1.8;
                    FillEll(gxg - sr, gyg - sr, sr * 2, sr * 2, SBrush(FA(Alpha(AccHi(bcol, ok == 2 ? 0.3 : 0.12), R((ok == 2 ? 150 : 105) + 60 * mbA)), fB)));
                }
            }
        }
        else if (HubState.ProfRing == 5)                                         // PULSE: frames shedding outward
        {
            for (int pk = 0; pk < 2; pk++)
            {
                double pt = (DecT(now) + pk * 800) % 1600 / 1600.0, ex = pt * 12;
                StrokeRR(bxL - ex, byT - ex, MBW + ex * 2, MBH + ex * 2, GR + ex * 0.5,
                         Pen(FA(Alpha(AccHi(bcol, 0.4), R((150 + 70 * mbA) * Math.Pow(1 - pt, 1.6))), fB), 1.8 * (1 - pt) + 0.3));
            }
        }
        else if (HubState.ProfRing == 6)                                         // TICKS: a bezel of marks, one lit and walking
        {
            double lead = DecT(now) * 0.018 % 28;
            for (int tk = 0; tk < 28; tk++)
            {
                Modules.FastFlags.FfmViews.RectPerim(tk / 28.0, bxL - 2, byT - 2, MBW + 4, MBH + 4, out double tx0, out double ty0);
                Modules.FastFlags.FfmViews.RectPerim(tk / 28.0, bxL - (tk % 4 == 0 ? 8.0 : 5.0), byT - (tk % 4 == 0 ? 8.0 : 5.0),
                                                     MBW + (tk % 4 == 0 ? 16.0 : 10.0), MBH + (tk % 4 == 0 ? 16.0 : 10.0), out double tx1, out double ty1);
                double d = Math.Abs(((tk - lead + 42) % 28) - 0), li = d < 3 ? Math.Pow(1 - d / 3.0, 2) : 0;
                Line(tx0, ty0, tx1, ty1, Pen(FA(Alpha(li > 0 ? AccHi(bcol, 0.5) : bcol, R((tk % 4 == 0 ? 120 : 70) + 135 * li + 40 * mbA)), fB), 1 + 0.6 * li));
            }
        }
        else if (HubState.ProfRing == 7)                                         // SEGMENT: arcs of the perimeter with a head running them
        {
            double head = DecT(now) * 0.00040 % 1.0;
            for (int sg = 0; sg < 20; sg++)
            {
                double t0 = sg / 20.0, dd = Math.Abs(((t0 - head + 1.5) % 1.0) - 0.5);
                double boost = Math.Pow(Math.Max(0.0, 1 - dd * 4), 3);
                for (int q = 0; q < 3; q++)
                {
                    Modules.FastFlags.FfmViews.RectPerim(t0 + q * 0.012, bxL - 1, byT - 1, MBW + 2, MBH + 2, out double sx0, out double sy0);
                    double rr = (0.9 + 1.3 * boost);
                    FillEll(sx0 - rr, sy0 - rr, rr * 2, rr * 2, SBrush(FA(Alpha(boost > 0.15 ? AccHi(bcol, 0.5) : bcol, R(55 + 180 * boost + 40 * mbA)), fB)));
                }
            }
        }
        else if (HubState.ProfRing == 8)                                         // HALO: no hard edge, a soft stack that breathes
        {
            for (int hk = 0; hk < 4; hk++)
            {
                double ex = 1 + hk * 2.4 * (1 + 0.05 * ((Math.Sin(DecT(now) * 0.0021) + 1) / 2));
                StrokeRR(bxL - ex, byT - ex, MBW + ex * 2, MBH + ex * 2, GR + ex * 0.5,
                         Pen(FA(Alpha(hk == 0 ? AccHi(bcol, 0.45) : bcol, R((hk == 0 ? 150 : 46 - hk * 9) * (1 + 0.4 * mbA))), fB), hk == 0 ? 1.5 : 2.4));
            }
        }
        else                                                                     // SOLID: the glint head with its comet trail, plus a counter spark
        {
            double gl4 = DecT(now) * 0.00042 % 1.0;
            for (int tk = 0; tk < 6; tk++)
            {
                Modules.FastFlags.FfmViews.RectPerim(gl4 - tk * 0.013, bxL, byT, MBW, MBH, out double gxg, out double gyg);
                if (tk == 0)
                {
                    FillEll(gxg - 6, gyg - 6, 12, 12, SBrush(FA(Alpha(bcol, 55), fB)));
                    FillEll(gxg - 2, gyg - 2, 4, 4, SBrush(FA(Alpha(AccHi(bcol, 0.6), 210), fB)));
                }
                else
                {
                    double ta = Math.Pow(1 - tk / 6.0, 2), r5 = 1.5 * ta + 0.35;
                    FillEll(gxg - r5, gyg - r5, r5 * 2, r5 * 2, SBrush(FA(Alpha(AccHi(bcol, 0.45), R(150 * ta)), fB)));
                }
            }
            Modules.FastFlags.FfmViews.RectPerim(0.5 - gl4, bxL, byT, MBW, MBH, out double g2x, out double g2y);
            FillEll(g2x - 3.4, g2y - 3.4, 6.8, 6.8, SBrush(FA(Alpha(bcol, R(64 + 66 * mbA)), fB)));
        }
    }
}
