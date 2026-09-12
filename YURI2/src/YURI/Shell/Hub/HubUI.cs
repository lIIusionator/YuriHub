using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Hub;

/// <summary>
/// The widgets every panel shares: FFMBtn (the button in its five kinds),
/// FFMTogDraw (the switch), FFMElide, SetHead, the dashboard's LAUNCH and
/// TUTORIAL pills, and the credits page's pixel cafe and doodles. They read
/// the hover map and the click map of the live hub, as the .ahk's read
/// `HL.h` and `FFM.clickAt`.
/// </summary>
public static class HubUI
{
    static HubSurface Hub => HubSurface.Live!;
    static double Hv(int z) => HubSurface.Live?.Hv(z) ?? 0.0;

    /// <summary>FFMBtn(z, bx, by, bw, bh, label, acc, ff, kind, fnt): kind 0 plain, 1 primary, 2/3 accent, 4 disabled.</summary>
    public static void FFMBtn(int z, double bx, double by, double bw, double bh, string label, uint acc, double ff, int kind = 0, Font? fnt = null)
    {
        var hub = Hub;
        double hv = hub.Hv(z);
        double hvE = Ease3(hv);
        double press = 0.0, ripple = 0.0;
        double thT = HubState.ThT;
        long now = Clock.Tick;
        if ((kind == 4 || HubState.LowPerf) && hub.ClickAt.ContainsKey(z)) hub.ClickAt.Remove(z);
        else if (hub.ClickAt.TryGetValue(z, out var at))
        {
            long el = now - at;
            if (el < 420)
            {
                press = el < 90 ? el / 90.0 : Math.Max(0.0, 1 - EBackOut(Clamp((el - 90) / 330.0, 0.0, 1.0), 1.7));
                ripple = Clamp(el / 420.0, 0.0, 1.0);
            }
            else hub.ClickAt.Remove(z);
        }
        double sc = HubState.LowPerf ? 1 : 1 + 0.045 * hvE - 0.055 * press;
        double cx = bx + bw / 2, cy = by + bh / 2;
        int st = PushXform(cx, cy, sc, 0);
        if (ripple > 0.01 && ripple < 1.0 && !HubState.LowPerf)
        {
            double rr = Math.Pow(ripple, 0.55), ra = Math.Pow(1 - ripple, 1.6);
            uint rc = kind == 1 ? 0xFFFFFFFF : acc;
            StrokeRR(bx - 14 * rr, by - 9 * rr, bw + 28 * rr, bh + 18 * rr, 6 + 7 * rr, Pen(FA(Alpha(rc, R(190 * ra)), ff), 1.4 * (1 - ripple) + 0.3));
        }
        uint tc;
        if (kind == 4)
        {
            FillRR(bx, by, bw, bh, 6, SBrush(FA(Alpha(0xFFFFFF, 5), ff)));
            var pn = Pen(FA(Alpha(0xFF9AA8C0, 46), ff), 1);
            PenDash(pn, 1);
            StrokeRR(bx, by, bw, bh, 6, pn);
            tc = FA(Alpha(THMix(0xFF9AA8C0, 0xFF99A2B8, thT), 100), ff);
        }
        else if (kind == 1)
        {
            double glow = 0.30 + 0.70 * hvE;
            for (int k = 1; k <= (HubState.LowPerf ? 1 : 3); k++)
                FillRR(bx - k * 1.5, by + k, bw + k * 3, bh + k, 8 + k, SBrush(FA(Alpha(acc, R((20 - k * 5) * glow)), ff)));
            if (hvE > 0.01) FillRR(bx - 3, by - 2, bw + 6, bh + 5, 9, SBrush(FA(Alpha(acc, R(60 * hvE)), ff)));
            double mixT = (0.26 + 0.18 * hvE - 0.10 * press) * (1 - thT) + (0.10 * hvE) * thT;
            FillRR(bx, by, bw, bh, 6, VBrush(bx, by, bw, bh, FA(AccHi(acc, mixT), ff), FA(Mix(acc, 0xFF000000, 0.16 + 0.10 * press), ff)));
            MicroBackdrop(bx, by, bw, bh, 6, 0xFFFFFF, ff, now, 0.55);
            int gl1 = R((40 + 30 * hvE - 18 * press) * (1 - thT));
            if (gl1 > 1) FillRR(bx + 1, by + 1, bw - 2, bh * (0.40 - 0.12 * press), 5, SBrush(FA(Alpha(0xFFFFFF, gl1), ff)));
            StrokeRR(bx, by, bw, bh, 6, Pen(FA(Alpha(AccHi(acc, 0.35), R(130 + 90 * hvE)), ff), 0.8));
            tc = FA(Alpha(THMix(0xFF080C18, 0xFFFDFDFE, thT), 252), ff);
        }
        else if (kind == 2 || kind == 3)
        {
            for (int k = 1; k <= 2; k++)
                FillRR(bx - 1, by + k, bw + 2, bh + 1, 7, SBrush(FA(Alpha(0x000000, R(14 * (1 - k * 0.28) * (0.4 + 0.6 * hvE))), ff)));
            FillRR(bx, by, bw, bh, 6, VBrush(bx, by, bw, bh, FA(Mix(0xFF171A30, 0xFF232744, hvE), ff), FA(0xFF12141F, ff)));
            FillRR(bx, by, bw, bh, 6, SBrush(FA(Alpha(acc, R(44 + 56 * hvE - 14 * press)), ff)));
            MicroBackdrop(bx, by, bw, bh, 6, acc, ff, now, 0.85);
            StrokeRR(bx, by, bw, bh, 6, Pen(FA(Alpha(acc, R(110 + 120 * hvE)), ff), 1));
            if (hvE > 0.02 && !HubState.LowPerf)
            {
                double uw = (bw - 12) * hvE;
                FillRR(cx - uw / 2, by + bh - 3, uw, 2, 1, SBrush(FA(Alpha(acc, R(90 * hvE)), ff)));
            }
            tc = FA(Alpha(THMix(0xFFE8EAF6, 0xFFFDFDFE, thT), R(235 + 20 * hvE)), ff);
        }
        else
        {
            int shN = HubState.LowPerf ? 0 : 1 + R(hvE);
            for (int k = 1; k <= shN; k++)
                FillRR(bx - 1, by + k + hvE, bw + 2, bh + 1, 7, SBrush(FA(Alpha(0x000000, R(16 * (1 - k * 0.28) * (0.5 + 0.5 * hvE))), ff)));
            FillRR(bx, by, bw, bh, 6, VBrush(bx, by, bw, bh, FA(Mix(0xFF171A30, 0xFF232744, hvE), ff), FA(0xFF12141F, ff)));
            MicroBackdrop(bx, by, bw, bh, 6, acc, ff, now, 0.9);
            StrokeRR(bx, by, bw, bh, 6, Pen(FA(Alpha(acc, R(70 + 130 * hvE)), ff), 1));
            int sh0 = R((20 + 24 * hvE) * (1 - thT));
            if (sh0 > 1) FillRR(bx + 3, by + 1, bw - 6, 2, 1, SBrush(FA(Alpha(0xFFFFFF, sh0), ff)));
            if (hvE > 0.02 && !HubState.LowPerf)
            {
                double uw = (bw - 16) * hvE;
                FillRR(cx - uw / 2, by + bh - 3, uw, 2, 1, SBrush(FA(Alpha(acc, R(180 * hvE)), ff)));
            }
            tc = FA(Alpha(THMix(0xFFC0C4DC, 0xFFFFFFFF, hvE), R(190 + 65 * hvE)), ff);
        }
        if (hvE > 0.05 && thT < 0.98 && !HubState.LowPerf)                       // the sheen
        {
            double sp = (now % 2400) / 2400.0;
            double sx = bx - bw * 0.4 + sp * (bw * 1.8);
            int shSt = PushG();
            ClipRR(bx, by, bw, bh, 6);
            FillRR(sx, by - 2, bw * 0.16, bh + 4, 2, SBrush(FA(Alpha(0xFFFFFF, R(26 * hvE * Math.Sin(3.14159 * sp) * (1 - thT))), ff)));
            Pop(shSt);
        }
        TxtP(label, bx, by - 1 - (HubState.LowPerf ? 0 : 0.6 * hvE - 0.8 * press), bw, bh, fnt ?? Fonts.fBadge, tc, Fmt.C);
        Pop(st);
    }

    /// <summary>FFMTogDraw(tx, ty, tw, th, on, acc, hv, ff): the switch; `on` is the eased 0..1.</summary>
    /// <summary>FFMScrollFromY / ScrGrip: a scrollbar drag keeps the point inside the thumb it took hold at, or the thumb's middle when the press missed it.</summary>
    public static double ScrollFromY(HubLayout HL, double uy, double trackTop, double trackLen, double total, double viewLen, double thumbLen, double curScr = -1)
    {
        if (total <= viewLen) return 0.0;
        double span = trackLen - thumbLen;
        if (span <= 0) return 0.0;
        if (HL.dragOff < 0)
        {
            double curFrac = curScr >= 0 && total > viewLen ? curScr / (total - viewLen) : -1;
            if (curFrac < 0) HL.dragOff = thumbLen / 2;
            else { double thTop = trackTop + span * Clamp(curFrac, 0.0, 1.0); HL.dragOff = uy >= thTop && uy <= thTop + thumbLen ? uy - thTop : thumbLen / 2; }
        }
        double f = Clamp((uy - trackTop - HL.dragOff) / span, 0.0, 1.0);
        return f * (total - viewLen);
    }
    public static void FFMTogDraw(double tx, double ty, double tw, double thm, double on, uint acc, double hv, double ff)
    {
        double hvE = Ease3(hv);
        long now = Clock.Tick;
        int st = PushXform(tx + tw / 2, ty + thm / 2, 1 + 0.07 * hvE, 0);
        if (hvE > 0.01)
            FillRR(tx - 4, ty - 4, tw + 8, thm + 8, thm / 2 + 4, SBrushP(FA(Alpha(THMix(acc, HubState.C_ON, on), R(46 * hvE)), ff)));
        uint trk = THMix(0xFF2A2D44, 0xFF34D399, on);
        FillRR(tx, ty, tw, thm, thm / 2, SBrushP(FA(Mix(trk, TH(0xFFFFFFFF), 0.10 * hvE), ff)));
        MicroBackdrop(tx, ty, tw, thm, thm / 2, acc, ff, now, 0.6);
        StrokeRR(tx, ty, tw, thm, thm / 2, Pen(FA(Alpha(acc, R(110 + 90 * hvE)), ff), 1 + 0.3 * hvE));
        if (on > 0.02)
        {
            double pw = hvE > 0.02 ? 1 + 0.5 * Math.Abs(Math.Sin(now * 0.004)) : 0;
            StrokeRR(tx - 2.5 - pw, ty - 2.5 - pw, tw + 5 + pw * 2, thm + 5 + pw * 2, thm / 2 + 2.5 + pw, Pen(FA(Alpha(HubState.C_ON, R((70 + 70 * hvE) * on)), ff), 1.6));
        }
        double kx = tx + thm / 2 + (tw - thm) * on;
        double kr = 7 + 0.9 * hvE;
        FillEll(kx - kr, ty + thm / 2 - kr + 1.5 + 0.5 * hvE, kr * 2, kr * 2, SBrush(FA(Alpha(0x000000, R(90 + 40 * hvE)), ff)));
        FillEll(kx - kr, ty + thm / 2 - kr, kr * 2, kr * 2, SBrush(FA(Alpha(0xFEFEFE, 245), ff)));
        double dr = 2.3 + 0.5 * hvE;
        FillEll(kx - dr, ty + thm / 2 - dr, dr * 2, dr * 2, SBrushP(FA(Alpha(THMix(acc, HubState.C_ON, on), R(210 + 45 * hvE)), ff)));
        Pop(st);
    }

    // ---- FFMElide(s, font, maxw): the string, or as much of it as fits with an ellipsis ----
    static readonly Dictionary<(Font, int, string), string> ElideMemo = new();
    // ---- WINDOWS ONLY ----
    // Some of this suite is a Windows program wearing the hub's clothes: a
    // global keyboard hook, ReadProcessMemory, the registry, bcdedit. Those
    // cannot be ported, and a control that looks live and does nothing is worse
    // than one that says why. This dims the panel that owns them and puts the
    // reason on top - drawn LAST, over whatever the panel drew, and the caller
    // refuses the panel's zones so nothing under the dim can be pressed.
    public static void WinOnlyVeil(double x, double y, double w, double h, uint acc, double f, long now, string what, string why)
    {
        int st = PushG();
        ClipRR(x, y, w, h, 12);
        FillRR(x, y, w, h, 12, SBrushP(FA(Alpha(0x05060C, 205), f)));
        if (!HubState.LowPerf)                                            // the hazard hatch the clash card uses, at a whisper
        {
            var pn = Pen(FA(Alpha(0xFFFFFF, 7), f), 9);
            for (double sx = x - h - 40; sx < x + w + h; sx += 40) Line(sx, y + h, sx + h, y, pn);
        }
        Pop(st);
        double cw = Math.Min(w - 48, 344), ch2 = 108, cx = x + (w - cw) / 2, cy = y + h / 2 - ch2 / 2;
        ShadowDraw(cx, cy + 3, cw, ch2, 12, 8, 10, 10, f);
        FillRR(cx, cy, cw, ch2, 12, VBrushP(cx, cy, cw, ch2, FA(0xF2222438, f), FA(0xF8121423, f)));
        MicroBackdrop(cx, cy, cw, ch2, 12, acc, f, now, 0.85);
        StrokeRR(cx, cy, cw, ch2, 12, PenP(FA(Alpha(acc, 90), f), 1));
        FillRR(cx, cy + 10, 3, ch2 - 20, 1.5, SBrushP(FA(Alpha(acc, 200), f)));
        // the same slashed circle the keybind conflict uses for "this cannot be"
        double gx = cx + 30, gy = cy + 30;
        var pnG = PenP(FA(Alpha(acc, 215), f), 1.6);
        Ell(gx - 9, gy - 9, 18, 18, pnG);
        Line(gx - 6.2, gy + 6.2, gx + 6.2, gy - 6.2, pnG);
        Txt("WINDOWS ONLY", cx + 52, cy + 18, cw - 70, 18, Fonts.fBrand, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.2), 240), f), Fmt.L);
        Txt(FFMElide(what, Fonts.fBadge, cw - 70), cx + 52, cy + 38, cw - 70, 16, Fonts.fBadge, FA(0xC0C7CBE0, f), Fmt.L);
        foreach (var (line, i) in Wrap2(why, cw - 60)) Txt(line, cx + 30, cy + 58 + i * 15, cw - 60, 15, Fonts.fHint, FA(0x92C7CBE0, f), Fmt.L);
    }
    /// <summary>Three lines at most - the reason has to fit the card, and a reason that needs a fourth line is too long to read here anyway.</summary>
    static IEnumerable<(string, int)> Wrap2(string s, double w)
    {
        var words = s.Split(' '); var cur = ""; int i = 0;
        foreach (var wd in words)
        {
            string t = cur == "" ? wd : cur + " " + wd;
            if (Fonts.MeasureW(t, Fonts.fHint) > w && cur != "")
            {
                yield return (cur, i++);
                cur = wd;
                if (i >= 3) yield break;
            }
            else cur = t;
        }
        if (cur != "" && i < 3) yield return (cur, i);
    }

    public static string FFMElide(string s, Font font, double maxw)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var key = (font, (int)Math.Round(maxw), s);
        if (ElideMemo.TryGetValue(key, out var hit)) return hit;
        if (ElideMemo.Count > 4000) ElideMemo.Clear();
        if (Fonts.MeasureW(s, font) <= maxw) return ElideMemo[key] = s;
        int lo = 0, hi = s.Length;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (Fonts.MeasureW(s[..mid] + "\u2026", font) <= maxw) lo = mid; else hi = mid;
        }
        return ElideMemo[key] = lo >= 1 ? s[..lo] + "\u2026" : "\u2026";
    }

    /// <summary>SetHead(x, w, y, label, acc, f, now): a section heading with its rule.</summary>
    public static void SetHead(double x, double w, double y, string label, uint acc, double f, long now)
    {
        var HL = Hub.HL;
        FillRR(x, y + 2, 2.5, 11, 1.2, SBrush(FA(Alpha(acc, 200), f)));
        Txt(label, x + 9, y, 160, 14, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, acc, 0.4), 205), f), Fmt.L);
        GroupLine(x + 9 + Fonts.MeasureW(label, HL.fXs) + 10, x + w, y + 7, acc, f, now);
    }

    /// <summary>SetTileIcon(i, cx, cy, on, acc, f, now): the BEHAVIOUR tiles' icons — bars, window, tray, refresh.</summary>
    public static void SetTileIcon(int i, double cx, double cy, double on, uint acc, double f, long now)
    {
        if (i == 1)
        {
            var b = SBrush(FA(Alpha(on > 0.5 ? HubState.C_ON : acc, R(150 + 80 * on)), f));
            for (int k = 1; k <= 3; k++)
            {
                double bh = (3 + k * 3.4) * (1 - on) + 3;
                FillRR(cx - 8 + (k - 1) * 5.4, cy + 6 - bh, 3.4, bh, 1.6, b);
            }
        }
        else if (i == 2)
        {
            StrokeRR(cx + 1, cy - 2, 13, 11, 2.2, Pen(FA(Alpha(acc, R(80 + 50 * on)), f), 1.3));
            double fy = cy - 8 - 2.5 * on;
            var pn = Pen(FA(Alpha(on > 0.5 ? HubState.C_ON : acc, R(150 + 80 * on)), f), 1.4);
            StrokeRR(cx - 4, fy, 13, 11, 2.2, pn);
            Line(cx - 4, fy + 3.4, cx + 9, fy + 3.4, pn);
        }
        else if (i == 3)
        {
            var pn = Pen(FA(Alpha(on > 0.5 ? HubState.C_ON : acc, R(150 + 80 * on)), f), 1.5);
            double ay = cy - 8 + 4 * on;
            Line(cx, ay, cx, ay + 8, pn);
            Line(cx - 3.5, ay + 4.5, cx, ay + 8, pn);
            Line(cx + 3.5, ay + 4.5, cx, ay + 8, pn);
            Line(cx - 8, cy + 2, cx - 8, cy + 7, pn);
            Line(cx - 8, cy + 7, cx + 8, cy + 7, pn);
            Line(cx + 8, cy + 7, cx + 8, cy + 2, pn);
            if (on > 0.02) FillRR(cx - 7, cy + 7 - 3.4 * on, 14, 3.4 * on, 1.2, SBrush(FA(Alpha(HubState.C_ON, R(150 * on)), f)));
        }
        else
        {
            uint col4 = on > 0.5 ? HubState.C_ON : acc;
            var pn = Pen(FA(Alpha(col4, R(150 + 80 * on)), f), 1.5);
            double sw4 = 215 + 100 * on;
            Arc(cx - 8, cy - 8, 16, 16, -60, sw4, pn);
            double ae = (-60 + sw4) * 0.0174533;
            double ex = cx + 8 * Math.Cos(ae), ey = cy + 8 * Math.Sin(ae);
            double tx4 = -Math.Sin(ae), ty4 = Math.Cos(ae);
            Line(ex, ey, ex - 4.2 * tx4 + 2.6 * Math.Cos(ae), ey - 4.2 * ty4 + 2.6 * Math.Sin(ae), pn);
            Line(ex, ey, ex - 4.2 * tx4 - 2.6 * Math.Cos(ae), ey - 4.2 * ty4 - 2.6 * Math.Sin(ae), pn);
            if (on > 0.02) FillEll(cx - 2.4 * on, cy - 2.4 * on, 4.8 * on, 4.8 * on, SBrush(FA(Alpha(HubState.C_ON, R(170 * on)), f)));
        }
    }

    /// <summary>HubDart(cx, cy, s, argb): the launch pill's arrowhead.</summary>
    public static void HubDart(double cx, double cy, double s, uint argb)
    {
        var geo = new Avalonia.Media.StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Avalonia.Point(cx + s * 1.05, cy), true);
            g.LineTo(new Avalonia.Point(cx - s * 0.75, cy - s * 0.92));
            g.LineTo(new Avalonia.Point(cx - s * 0.30, cy));
            g.LineTo(new Avalonia.Point(cx - s * 0.75, cy + s * 0.92));
            g.EndFigure(true);
        }
        FillPath(geo, Solid(argb));
    }

    /// <summary>HubLaunchBtn: OPEN ROBLOX, zone 205; `live` lights the dot when the client runs.</summary>
    public static void HubLaunchBtn(double x, double y, double w, double h, double f, long now, uint acc, bool live)
    {
        var hub = Hub;
        double hv = hub.Hv(205);
        double prs = hub.ClickAt.TryGetValue(205, out var at) ? Clamp((now - at) / 400.0, 0.0, 1.0) : 1.0;
        bool firing = prs < 1.0 && !HubState.LowPerf;
        FillRR(x, y, w, h, h / 2, VBrush(x, y, w, h, FA(Mix(0xFF0E1120, 0xFF161B34, hv), f), FA(0xFF080A12, f)));
        if (!HubState.LowPerf) FillRR(x + 2, y + 1, w - 4, 5, 2.5, VBrush(x, y, w, 5, FA(Alpha(0x000000, 90), f), Alpha(0x000000, 0)));
        double cy2 = y + h / 2;
        double home = x + 14 + (HubState.LowPerf ? 0 : 2.2 * hv);
        double go = prs * prs * (1.6 - 0.6 * prs);
        double outA = firing ? Clamp((1 - prs) / 0.35, 0.0, 1.0) : 0.0;
        double restA = firing ? Clamp((prs - 0.55) / 0.45, 0.0, 1.0) : 1.0;
        if (firing)
        {
            double tr = home + (w - 26) * go;
            for (int k = 1; k <= 3; k++)
            {
                double tx = tr - k * (7 + 9 * go);
                if (tx < x + 6) continue;
                FillRR(tx - 4, cy2 - 1.4, 8, 2.8, 1.4, SBrush(FA(Alpha(AccHi(acc, 0.45), R(85 * outA / k)), f)));
            }
            HubDart(tr, cy2, 7.4, FA(Alpha(AccHi(acc, 0.55), R(240 * outA)), f));
        }
        if (restA > 0.01) HubDart(home, cy2, 7 + (HubState.LowPerf ? 0 : 1.4 * hv), FA(Alpha(AccHi(acc, 0.5), R(235 * restA)), f));
        Txt("OPEN ROBLOX", x + 32, y + 1, w - 60, h, hub.HL.fXs, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.25), 210 + 45 * hv), f), Fmt.L);
        double lcx = x + w - 13, lcy = y + h / 2;
        if (live && !HubState.LowPerf) FillEll(lcx - 6, lcy - 6, 12, 12, SBrush(FA(Alpha(HubState.C_ON, 40), f)));
        FillEll(lcx - 3, lcy - 3, 6, 6, SBrush(FA(Alpha(live ? HubState.C_ON : 0xFF6B7590, live ? 235 : 120), f)));
        StrokeRR(x, y, w, h, h / 2, Pen(FA(Alpha(acc, 55 + 90 * hv + (firing ? R(80 * (1 - prs)) : 0)), f), 1.2));
    }

    /// <summary>HubTourBtn: TUTORIAL, zone 2207.</summary>
    public static void HubTourBtn(double x, double y, double w, double h, double f, long now, uint acc)
    {
        var hub = Hub;
        double hv = hub.Hv(2207);
        double prs = hub.ClickAt.TryGetValue(2207, out var at) ? Clamp((now - at) / 400.0, 0.0, 1.0) : 1.0;
        FillRR(x, y, w, h, h / 2, VBrush(x, y, w, h, FA(Mix(0xFF0E1120, 0xFF161B34, hv), f), FA(0xFF080A12, f)));
        if (!HubState.LowPerf) FillRR(x + 2, y + 1, w - 4, 5, 2.5, VBrush(x, y, w, 5, FA(Alpha(0x000000, 90), f), Alpha(0x000000, 0)));
        double gx = x + 11, cy2 = y + h / 2;
        double p1x = gx, p1y = cy2 + 4, p2x = gx + 7, p2y = cy2 - 4, p3x = gx + 14, p3y = cy2 + 3;
        var pn = Pen(FA(Alpha(AccHi(acc, 0.35), 120 + 60 * hv), f), 1.3);
        Line(p1x, p1y, p2x, p2y, pn); Line(p2x, p2y, p3x, p3y, pn);
        var bd = SBrush(FA(Alpha(0xFFC7CBE0, 150 + 60 * hv), f));
        FillEll(p1x - 2, p1y - 2, 4, 4, bd); FillEll(p2x - 2, p2y - 2, 4, 4, bd);
        FillEll(p3x - 2.6, p3y - 2.6, 5.2, 5.2, SBrush(FA(Alpha(AccHi(acc, 0.5), 235), f)));
        if (hv > 0.02 && !HubState.LowPerf)
        {
            double pp = (DecT(now) * 0.0016) % 1.0;
            double qx, qy;
            if (pp < 0.5) { double tt = pp / 0.5; qx = p1x + (p2x - p1x) * tt; qy = p1y + (p2y - p1y) * tt; }
            else { double tt = (pp - 0.5) / 0.5; qx = p2x + (p3x - p2x) * tt; qy = p2y + (p3y - p2y) * tt; }
            FillEll(qx - 2, qy - 2, 4, 4, SBrush(FA(Alpha(AccHi(acc, 0.6), R(220 * hv)), f)));
        }
        if (prs < 1 && !HubState.LowPerf)
            Ell(p3x - 4 - 10 * prs, p3y - 4 - 10 * prs, 8 + 20 * prs, 8 + 20 * prs, Pen(FA(Alpha(acc, R(180 * (1 - prs))), f), 1.6));
        Txt("TUTORIAL", x + 31, y + 1, w - 38, h, hub.HL.fXs, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.25), 210 + 45 * hv), f), Fmt.L);
        StrokeRR(x, y, w, h, h / 2, Pen(FA(Alpha(acc, 55 + 90 * hv + (prs < 1 ? R(80 * (1 - prs)) : 0)), f), 1.2));
    }

    /// <summary>CafeDraw(ox, oy, acc, f, now): the credits page's pixel cafe, 3 px per cell.</summary>
    public static void CafeDraw(double ox, double oy, uint acc, double f, long now)
    {
        const double cs = 3.0;
        double t = DecT(now);
        uint hi = AccHi(acc, 0.5);
        void Px(double x, double y, double w, double h, Avalonia.Media.IBrush? b) => FillRR(ox + x * cs, oy + y * cs, w * cs, h * cs, 0, b);
        var bF = SBrush(FA(Alpha(acc, 55), f));            // floor, frames, the dim structure
        var bC = SBrush(FA(Alpha(acc, 95), f));            // the counter, bodies
        var bT = SBrush(FA(Alpha(acc, 170), f));           // edges, the sign, the cup
        var bH = SBrush(FA(Alpha(hi, 235), f));            // highlights
        var bI = SBrush(FA(Alpha(0xFFE8EAF6, 215), f));    // ink: eyes and steam
        for (int i = 0; i < 15; i++) Px(i * 4, 13, 2, 1, bF);                     // a tiled floor
        double gl = 0.55 + 0.45 * Math.Sin(t * 0.0028);                            // the lamp breathes
        Px(8, 0, 1, 2, bT); Px(6, 2, 5, 1, bT); Px(8, 3, 1, 1, bH);
        Px(5, 3, 7, 5, SBrush(FA(Alpha(hi, R(10 + 16 * gl)), f)));
        Px(33, 3, 7, 1, bT); Px(33, 9, 7, 1, bT); Px(33, 3, 1, 7, bT); Px(39, 3, 1, 7, bT);
        Px(34, 4, 5, 5, bF); Px(36, 4, 1, 5, bT); Px(34, 6, 5, 1, bT);
        Px(37, 5, 1, 1, bH);
        double tw = 0.5 + 0.5 * Math.Sin(t * 0.0037);
        Px(35, 7, 1, 1, SBrush(FA(Alpha(hi, R(60 + 170 * tw)), f)));
        Px(38, 8, 1, 1, SBrush(FA(Alpha(hi, R(60 + 170 * (1 - tw))), f)));
        Px(44, 0, 1, 2, bF); Px(48, 0, 1, 2, bF);
        Px(42, 2, 9, 4, bC);
        Px(42, 2, 9, 1, bT); Px(42, 5, 9, 1, bT); Px(42, 2, 1, 4, bT); Px(50, 2, 1, 4, bT);
        Px(45, 3, 3, 2, bH); Px(48, 3, 1, 1, bH);
        bool on1 = t % 1000 < 500;
        Px(43, 3, 1, 2, on1 ? bH : bF); Px(49, 3, 1, 2, on1 ? bF : bH);
        Px(4, 9, 28, 1, bT); Px(4, 10, 28, 3, bC); Px(4, 12, 28, 1, bF);
        Px(22, 7, 3, 2, bT); Px(25, 7, 1, 1, bT); Px(23, 7, 1, 1, bH);              // the cup, its handle, the coffee
        Px(28, 8, 2, 1, bT); Px(28, 6, 1, 1, bH); Px(29, 5, 1, 2, bH); Px(30, 6, 1, 1, bH);   // a plant
        for (int k = 1; k <= 3; k++)                                               // steam: three pixels rising in turn
        {
            double ph = (t * 0.0011 + k * 0.33) % 1.0;
            double sy = 6 - Math.Round(ph * 5);
            double sx = 23 + Math.Round(Math.Sin(ph * 6.28 + k * 2) * 0.7);
            Px(sx, sy, 1, 1, SBrush(FA(Alpha(0xFFE8EAF6, R(200 * (1 - ph))), f)));
        }
        int bob = t % 1400 < 700 ? 0 : 1;                                          // the barista, working
        bool blink = t % 3200 < 140;
        Px(14, 6 + bob, 5, 3, bC); Px(14, 1 + bob, 5, 1, bH); Px(14, 2 + bob, 5, 3, bT);
        if (!blink) { Px(15, 3 + bob, 1, 1, bI); Px(17, 3 + bob, 1, 1, bI); }
        Px(18, 8, 2, 1, bC);                                                       // a hand on the counter
        bool blk2 = (t + 1700) % 3200 < 140;                                       // the customer, on the stool
        Px(1, 11, 3, 1, bT); Px(2, 12, 1, 1, bF);
        Px(0, 6, 4, 3, bC); Px(1, 2, 3, 1, bH); Px(1, 3, 3, 3, bT); Px(3, 8, 1, 1, bC);
        if (!blk2) Px(3, 4, 1, 1, bI);
        bool tail = t % 800 < 400;                                                 // the cat
        Px(49, 11, 5, 2, bT); Px(53, 10, 2, 2, bT); Px(53, 9, 1, 1, bT); Px(55, 9, 1, 1, bT); Px(54, 10, 1, 1, bH);
        if (tail) { Px(48, 10, 1, 1, bT); Px(47, 9, 1, 1, bT); }
        else { Px(48, 11, 1, 1, bT); Px(47, 11, 1, 1, bT); }
    }

    /// <summary>Doodle(kind, dx, dy, sc, col, f, now, ph): the credits page's five sketches.</summary>
    public static void Doodle(int kind, double dx, double dy, double sc, uint col, double f, long now, double ph)
    {
        if (HubState.LowPerf) return;
        double tw = (Math.Sin(DecT(now) * 0.0014 + ph) + 1) / 2;
        int al = R(70 + 90 * tw);
        var pn = Pen(FA(Alpha(col, al), f), 1.3);
        if (kind == 1)
        {
            for (int i = 1; i <= 4; i++)
            {
                double a = (i - 1) * 45 + now * 0.02;
                double r = (i % 2 != 0 ? 1.0 : 0.55) * sc * (0.85 + 0.15 * tw);
                Line(dx - r * Math.Cos(a * 0.017453), dy - r * Math.Sin(a * 0.017453), dx + r * Math.Cos(a * 0.017453), dy + r * Math.Sin(a * 0.017453), pn);
            }
        }
        else if (kind == 2)
        {
            bool pr = false; double px = dx, py = dy;
            for (int i = 1; i <= 22; i++)
            {
                double t = i / 22.0;
                double a = t * 720 + now * 0.03;
                double r = t * sc;
                double nx = dx + r * Math.Cos(a * 0.017453), ny = dy + r * Math.Sin(a * 0.017453);
                if (pr) Line(px, py, nx, ny, pn);
                px = nx; py = ny; pr = true;
            }
        }
        else if (kind == 3)
        {
            double px = dx - sc, py = dy;
            for (int i = 1; i <= 18; i++)
            {
                double t = i / 18.0;
                double nx = dx - sc + t * sc * 2;
                double ny = dy + Math.Sin(t * 9.4 + now * 0.003 + ph) * sc * 0.42;
                Line(px, py, nx, ny, pn);
                px = nx; py = ny;
            }
        }
        else if (kind == 4)
        {
            Line(dx - sc, dy + sc * 0.5, dx + sc * 0.4, dy - sc * 0.5, pn);
            Line(dx + sc * 0.4, dy - sc * 0.5, dx + sc * 0.1, dy - sc * 0.1, pn);
            Line(dx + sc * 0.4, dy - sc * 0.5, dx, dy - sc * 0.62, pn);
        }
        else
        {
            PenDash(pn, 2);
            Ell(dx - sc, dy - sc * 0.62, sc * 2, sc * 1.24, pn);
            double oa = now * 0.0022 + ph;
            FillEll(dx + sc * Math.Cos(oa) - 1.6, dy + sc * 0.62 * Math.Sin(oa) - 1.6, 3.2, 3.2, SBrush(FA(Alpha(AccHi(col, 0.45), 200), f)));
        }
    }
}
