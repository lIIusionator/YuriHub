using Avalonia.Media;
using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Shell.Hub.Tabs;

/// <summary>
/// Tab 2. The MODULES rail on the left — seven cards in the .ahk's order
/// (FAST FLAG MANAGER, CURSOR, CLIENT, SPECIAL, then FORSAKEN under the
/// BUILT-IN MACRO rule, then DEVICE OPTIMIZATIONS and LOGIN ITEMS) — and the
/// SYSTEMS panel on the right that a selected module fills. Card ids are the
/// .ahk's (400..406 by position; HUBMODS maps a position to a module id,
/// and FORSAKEN keeps id 2). Each module registers its panel, its zone table
/// and the status its card shows; an unregistered module opens to the empty
/// plate with the .ahk's own ghost line.
/// </summary>
public static class Integrations
{
    const uint AMBER = 0xFFFBBF24;
    public const int HUB_MODN = 7;
    /// <summary>HUBMODS: card position -> module id. The ids are NOT positional.</summary>
    public static readonly int[] HUBMODS = { 1, 3, 4, 5, 2, 6, 7 };
    public const int FSK_SYSN = 2, SPF_SYSN = 17, DOP_SYSN = 5;

    // ---- what the cards say: filled by the modules as they land ----
    public static Func<int> FfmStaged = () => 0;                 // FFM.flags.Length
    public static Func<int> FfmPid = () => 0;                    // FFM.pid
    public static Func<int> FfmInjectFail = () => 0;
    public static Func<(int set, int slots, bool applied)> CursorState = () => (0, 8, false);
    public static Func<(int chosen, bool on)> RSetState = () => (0, false);
    public static Func<(int chosen, bool on)> ClientState = () => (0, false);
    public static Func<int> SpecialOn = () => 0;
    public static Func<int> ForsakenOn = () => 0;
    public static Func<int> DevOptOn = () => 0;
    public static Func<(int items, bool on)> LoginItems = () => (0, false);

    /// <summary>A module's SYSTEMS panel: (ax, ay, x0, y0, dx, dy2, f, now, acc).</summary>
    public delegate void PanelDraw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double f, long now, uint acc);
    public static readonly Dictionary<int, PanelDraw> Panels = new();
    public static readonly Dictionary<int, Func<HubSurface, double, double, int>> PanelZones = new();
    public static readonly Dictionary<int, Func<HubSurface, int, bool>> PanelClicks = new();
    public static readonly Dictionary<int, Func<HubSurface, double, double, double, bool>> PanelWheels = new();
    /// <summary>A module's overlays (views drawn over the tab after its panel): (x0, y0, dx, dy2, f, now, acc).</summary>
    public delegate void OverlayDraw(HubSurface hub, double x0, double y0, double dx, double dy2, double f, long now, uint acc);
    public static readonly Dictionary<int, OverlayDraw> PanelOverlays = new();

    // ---- the rail's scroll ----
    public static double MdRailTop(HubLayout HL) => HL.mdy - 8;
    public static double MdRailBot() => HubLayout.pd + HubLayout.ch - 34;          // clear of the "click a module" line
    public static double MdRailVis(HubLayout HL) => MdRailBot() - MdRailTop(HL);
    public static double MdRailTot(HubLayout HL) => (HL.m7y + HL.m7h + 10) - MdRailTop(HL);
    public static double MdRailMax(HubLayout HL) => Math.Max(0.0, MdRailTot(HL) - MdRailVis(HL));

    /// <summary>ModSelT(sel, out, at, now): a card's selection ease — modT while selected, 1 -> 0 while it is the card being left.</summary>
    static double ModSelT(HubSurface hub, bool sel, bool outCard, long at, long now, double dur = 620)
    {
        var HL = hub.HL;
        if (sel) return HL.modT;
        if (!outCard) return 0.0;
        if (hub.HubMod == 0) return HL.modT;                          // closing outright: card and panel retract together
        if (at == 0 || now - at >= dur) return 0.0;                   // switching to another module
        return 1 - Ease3((now - at) / dur);
    }

    public static void Draw(HubSurface hub, double x0, double y0, double dx, double dy2, double f, long now, uint hubCur)
    {
        var HL = hub.HL;
        FFMCards(hub, x0, y0, dx, dy2, f, now, hubCur);
        double ax2 = HL.abx + dx, ay2 = HL.aby + dy2;
        int mod = hub.HubMod;
        int shown = mod != 0 ? mod : (HL.lastMod != 0 && HL.modT > 0.003 ? HL.lastMod : 0);
        if (shown != 0 && Panels.TryGetValue(shown, out var panel))
        {
            panel(hub, ax2, ay2, x0, y0, dx, dy2, f, now, hubCur);
            if (PanelOverlays.TryGetValue(shown, out var ov)) ov(hub, x0, y0, dx, dy2, f, now, hubCur);
            return;
        }
        // ---- the empty plate, with the ghost; a module without a panel yet opens to this ----
        double mt = HL.modT;
        double emb2G = (Math.Sin(DecT(now) * 0.004) + 1) / 2;
        Txt("SYSTEMS", ax2, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, f), Fmt.L);
        Txt("0 listed", ax2 + 66, y0 + 36, 80, 14, HL.fXs, FA(Alpha(hubCur, 60 + 120 * mt), f), Fmt.L);
        double hvA = hub.Hv(5);
        double pah = 26 + (HL.abh - 26) * mt;
        FillRR(ax2, ay2, HL.abw, pah, 12, VBrush(ax2, ay2, HL.abw, pah, FA(Mix(0xFF141728, Mix(0xFF171A30, 0xFF1C2142, hvA * 0.8), mt), f), FA(0xFF12141F, f)));
        MiniBackdrop(ax2, ay2, HL.abw, pah, 12, hubCur, f, now, 0.9);
        HubEmptyGhost(hub, ax2, ay2, pah, f);
        if (mt > 0.01)
        {
            StrokeRR(ax2, ay2, HL.abw, pah, 12, Pen(FA(Alpha(hubCur, (70 + 70 * HL.armT + 70 * hvA) * mt), f), 1.2));
            double gl = 40 * mt;
            var pn = Pen(FA(Alpha(AccHi(hubCur, 0.4), 200 * mt), f), 1.6);
            Line(ax2 + 14, ay2 + 0.6, ax2 + 14 + gl, ay2 + 0.6, pn);
            Line(ax2 + HL.abw - 14 - gl, ay2 + pah - 0.6, ax2 + HL.abw - 14, ay2 + pah - 0.6, pn);
        }
        if (mod != 0 || HL.modOut != 0)
            ModRing(ax2, ay2, HL.abw, pah, 12, hubCur, f, HL.modAt, now, 700, 18, mod != 0);
    }

    /// <summary>HubEmptyGhost(ax, ay, pah, ff): the dashed outline and the line that fade as a module opens.</summary>
    public static void HubEmptyGhost(HubSurface hub, double ax, double ay, double pah, double ff)
    {
        var HL = hub.HL;
        double ef = 1 - Math.Min(HL.modT * 1.6, 1.0);
        if (ef <= 0.01) return;
        var pn = Pen(FA(Alpha(0xFFFFFF, 26 * ef), ff), 1);
        PenDash(pn, 1);
        StrokeRR(ax, ay, HL.abw, pah, 12, pn);
        Txt("select a module to list its systems", ax, ay + pah / 2 - 8, HL.abw, 16, Fonts.fHint, FA(Alpha(0xC7CBE0, 84 * ef), ff), Fmt.C);
    }

    // ---- FFMCards(x0, y0, dx, dy2, ff, now, acc): the rail ----
    static void FFMCards(HubSurface hub, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        int hubMod = hub.HubMod;
        Txt("MODULES", x0, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, ff), Fmt.L);
        Txt(HUB_MODN + " listed", x0 + 66, y0 + 36, 80, 14, HL.fS, FA(Alpha(acc, 120), ff), Fmt.L);
        HL.mdscrT = Clamp(HL.mdscrT, 0.0, MdRailMax(HL));
        HL.mdscr += (HL.mdscrT - HL.mdscr) * EK(0.28);
        if (Math.Abs(HL.mdscrT - HL.mdscr) < 0.4) HL.mdscr = HL.mdscrT;
        dy2 -= HL.mdscr;
        int stRail = PushG();
        ClipRR(HL.mdx + dx - 10, MdRailTop(HL), HL.mdw + 24, MdRailVis(HL), 12);
        double mx = HL.mdx + dx;
        double mpop = (HL.modAt != 0 && now - HL.modAt < 300) ? 1 + 0.035 * Math.Sin(3.14159 * (now - HL.modAt) / 300.0) : 1;

        // ---- card 1: FAST FLAG MANAGER (module 1, zone 400) ----
        double my = HL.mdy + dy2, mh = HL.m1h;
        double hv = hub.Hv(400);
        bool sel = hubMod == 1, out1 = HL.modOut == 1;
        double st = ModSelT(hub, sel, out1, HL.modAt, now);
        double cPop = (sel || out1) ? mpop : 1;
        int stc = PushXform(mx + HL.mdw / 2, my + mh / 2, cPop * (1 + 0.014 * st + 0.022 * hv), 0);
        if (st > 0.01) FillRR(mx - 5 * st, my - 4 * st, HL.mdw + 10 * st, mh + 8 * st, 13, SBrush(FA(Alpha(acc, 30 * st), ff)));
        FillRR(mx, my, HL.mdw, mh, 10, VBrush(mx, my, HL.mdw, mh, FA(Mix(0xFF1A1E38, 0xFF262C58, Math.Max(hv * 0.7, st * 0.9)), ff), FA(0xFF121523, ff)));
        MiniBackdrop(mx, my, HL.mdw, mh, 10, acc, ff, now, 0.8);
        FillRR(mx + 1, my + 1, HL.mdw - 2, mh * 0.42, 9, SBrush(FA(Alpha(acc, 14 + 22 * Math.Max(hv, st)), ff)));
        StrokeRR(mx, my, HL.mdw, mh, 10, Pen(FA(Alpha(acc, 95 + 120 * st + 70 * hv * (1 - st)), ff), 1.3));
        if (st > 0.01) { double bh = (mh - 22) * st; FillRR(mx, my + mh / 2 - bh / 2, 3, bh, 1.5, SBrush(FA(Alpha(acc, 225 * st), ff))); }
        if (sel || out1) ModRing(mx, my, HL.mdw, mh, 10, acc, ff, HL.modAt, now, 620, 17, sel);
        FillPath(FFMFlagPath(mx + 16, my + 16, 17, 26), SBrush(FA(Alpha(acc, 110 + 90 * Math.Max(st, hv)), ff)));
        Line(mx + 16, my + 14, mx + 16, my + 44, Pen(FA(Alpha(AccHi(acc, 0.35), 230), ff), 1.4));
        TxtP("FAST FLAG", mx + 42 + 2 * st, my + 11, 60, 15, Fonts.fBadge, FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, st), 205 + 50 * hv), ff), Fmt.L);
        TxtP("MANAGER", mx + 42 + 2 * st, my + 25, 96, 15, Fonts.fBadge, FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, st), 205 + 50 * hv), ff), Fmt.L);
        int staged = FfmStaged();
        Txt(staged + " staged", mx + 42 + 2 * st, my + 41, 84, 14, Fonts.fHint, FA(0x72C7CBE0, ff), Fmt.L);
        FillRR(mx + HL.mdw - 44, my + 9, 36, 13, 4, SBrush(FA(Alpha(acc, 34), ff)));
        Txt("CORE", mx + HL.mdw - 44, my + 9, 36, 13, HL.fXs, FA(Alpha(AccHi(acc, 0.3), 230), ff), Fmt.C);
        int pid = FfmPid();
        uint dot = pid != 0 ? (FfmInjectFail() > 0 ? AMBER : HubState.C_ON) : (staged != 0 ? AMBER : 0xFFC7CBE0);
        FillEll(mx + HL.mdw - 18, my + mh - 18, 6, 6, SBrush(FA(Alpha(dot, pid != 0 ? 230 : 90), ff)));
        Pop(stc);

        // ---- the small cards share a frame ----
        void Card(double myK, double mhK, int zone, int modId, double kSt, double kHv, Action<double, double, double> icon, string name, string sub, uint dotCol, int dotA)
        {
            double hvK = hub.Hv(zone);
            bool selK = hubMod == modId, outK = HL.modOut == modId;
            double stK = ModSelT(hub, selK, outK, HL.modAt, now);
            int stcK = PushXform(mx + HL.mdw / 2, myK + mhK / 2, ((selK || outK) ? mpop : 1) * (1 + kSt * stK + kHv * hvK), 0);
            if (stK > 0.01) FillRR(mx - 4 * stK, myK - 3 * stK, HL.mdw + 8 * stK, mhK + 6 * stK, 12, SBrush(FA(Alpha(acc, 26 * stK), ff)));
            FillRR(mx, myK, HL.mdw, mhK, 10, VBrush(mx, myK, HL.mdw, mhK, FA(Mix(0xFF171A30, 0xFF20254A, Math.Max(hvK * 0.7, stK * 0.9)), ff), FA(0xFF12141F, ff)));
            MiniBackdrop(mx, myK, HL.mdw, mhK, 10, acc, ff, now, 0.8);
            StrokeRR(mx, myK, HL.mdw, mhK, 10, Pen(FA(Alpha(acc, 60 + 110 * stK + 80 * hvK * (1 - stK)), ff), 1.2));
            if (stK > 0.01) { double bh = (mhK - 20) * stK; FillRR(mx, myK + mhK / 2 - bh / 2, 3, bh, 1.5, SBrush(FA(Alpha(acc, 220 * stK), ff))); }
            if (selK || outK) ModRing(mx, myK, HL.mdw, mhK, 10, acc, ff, HL.modAt, now, 620, 16, selK);
            icon(stK, hvK, myK);
            if (name.Contains('\n'))
            {
                var two = name.Split('\n');
                TxtP(two[0], mx + 44 + 2 * stK, myK + 11, 96, 15, Fonts.fBadge, FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, stK), 200 + 55 * hvK), ff), Fmt.L);
                TxtP(two[1], mx + 44 + 2 * stK, myK + 25, 104, 15, Fonts.fBadge, FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, stK), 200 + 55 * hvK), ff), Fmt.L);
                Txt(sub, mx + 44 + 2 * stK, myK + 41, 96, 14, Fonts.fHint, FA(0x72C7CBE0, ff), Fmt.L);
            }
            else
            {
                TxtP(name, mx + 44 + 2 * stK, myK + 12, 100, 16, Fonts.fBadge, FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, stK), 200 + 55 * hvK), ff), Fmt.L);
                Txt(sub, mx + 44 + 2 * stK, myK + 28, 96, 14, Fonts.fHint, FA(0x72C7CBE0, ff), Fmt.L);
            }
            FillEll(mx + HL.mdw - 18, myK + 14, 6, 6, SBrush(FA(Alpha(dotCol, dotA), ff)));
            Pop(stcK);
        }

        // ---- card 2: CURSOR (module 3, zone 401) ----
        var (cSet, cSlots, cApplied) = CursorState();
        Card(HL.m2y + dy2, HL.mdh, 401, 3, 0.012, 0.02, (stC, hvC, myC) =>
        {
            double pdr = 1.2 * Ease3(hvC);
            CurArrow(mx + 16 + pdr, myC + 14 + pdr, 1.35, Pen(FA(Alpha(AccHi(acc, 0.3), 110 + 120 * Math.Max(stC, hvC)), ff), 2.6));
            CurArrow(mx + 16 + pdr, myC + 14 + pdr, 1.35, Pen(FA(Alpha(AccHi(acc, 0.55), 225), ff), 1.2));
        }, "CURSOR", cApplied ? "applied" : (cSet != 0 ? $"{cSet} of {cSlots} set" : "no image"),
           cApplied ? HubState.C_ON : (cSet != 0 ? AMBER : 0xFFC7CBE0), cApplied ? 230 : (cSet != 0 ? 190 : 70));

        // ---- card 3: CLIENT settings (module 4, zone 402) ----
        var (nSet, rsOn) = ClientState();
        Card(HL.m3y + dy2, HL.mdh, 402, 4, 0.012, 0.02, (stS, hvS, myS) =>
        {
            var pn = Pen(FA(Alpha(AccHi(acc, 0.3), 110 + 120 * Math.Max(stS, hvS)), ff), 1.4);
            for (int j = 1; j <= 3; j++) { double ty3 = myS + 16 + (j - 1) * 8; Line(mx + 14, ty3, mx + 32, ty3, pn); }
            var b = SBrush(FA(Alpha(AccHi(acc, 0.5), 225), ff));
            for (int j = 1; j <= 3; j++)
            {
                double ty3 = myS + 16 + (j - 1) * 8;
                double hx3 = mx + 17 + (j == 2 ? 11 : (j == 1 ? 4 : 8)) + 2 * Ease3(hvS) * (j == 2 ? -1 : 1);
                FillEll(hx3 - 2.6, ty3 - 2.6, 5.2, 5.2, b);
            }
        }, "CLIENT", rsOn ? "applied" : (nSet != 0 ? $"{nSet} set" : "settings"),
           rsOn ? HubState.C_ON : (nSet != 0 ? AMBER : 0xFFC7CBE0), rsOn ? 230 : (nSet != 0 ? 190 : 70));

        // ---- card 4: SPECIAL features (module 5, zone 403) ----
        int spfOn = SpecialOn();
        Card(HL.m4y + dy2, HL.mdh, 403, 5, 0.012, 0.02, (stF, hvF, myF) =>
        {
            double icx = mx + 24, icy = myF + HL.mdh / 2 + 4;
            FillEll(icx - 2.6, icy - 2.6, 5.2, 5.2, SBrush(FA(Alpha(AccHi(acc, 0.3), 200 + 55 * stF), ff)));
            for (int wk = 1; wk <= 3; wk++)
            {
                bool wlive = wk <= spfOn + 1;
                double wsh = (Math.Sin(DecT(now) * 0.004 - wk * 0.9) + 1) / 2;
                double wr = 5 + wk * 4.5;
                Arc(icx - wr, icy - wr, wr * 2, wr * 2, 214, 92, Pen(FA(Alpha(acc, R(wlive ? (70 + 90 * wsh + 60 * stF) : 34)), ff), 1.4));
            }
        }, "SPECIAL", spfOn != 0 ? $"{spfOn} active" : $"{SPF_SYSN} features",
           spfOn == SPF_SYSN ? HubState.C_ON : (spfOn != 0 ? AMBER : 0xFFC7CBE0), spfOn != 0 ? 230 : 70);

        FFMModDivider(hub, mx, HL.m5y + dy2 - HL.mdsep / 2 - 4, ff, now, acc);

        // ---- card 5: FORSAKEN (module 2, zone 404) ----
        int fskOn = ForsakenOn();
        Card(HL.m5y + dy2, HL.mdh, 404, 2, 0.012, 0.02, (st2, hv2, my2) =>
        {
            var sp = ShieldPath(mx + 14, my2 + 15, 20, 24);
            FillPath(sp, SBrush(FA(Alpha(acc, 90 + 110 * st2), ff)));
            StrokePath(sp, Pen(FA(Alpha(AccHi(acc, 0.35), 225), ff), 1.3));
        }, "FORSAKEN", fskOn != 0 ? $"{fskOn} active" : $"{FSK_SYSN} systems",
           fskOn == FSK_SYSN ? HubState.C_ON : (fskOn != 0 ? AMBER : 0xFFC7CBE0), fskOn != 0 ? 230 : 70);

        FFMModDivider2(hub, mx, HL.m6y + dy2 - HL.mdsep2 / 2 - 7, ff, now, acc);

        // ---- card 6: DEVICE OPTIMIZATIONS (module 6, zone 405) ----
        int dopN = DevOptOn();
        Card(HL.m6y + dy2, HL.m6h, 405, 6, 0.014, 0.022, (st6, hv6, my6) =>
        {
            double icx6 = mx + 24, icy6 = my6 + HL.m6h / 2 + 2;
            var pn = Pen(FA(Alpha(AccHi(acc, 0.3), 190 + 55 * st6), ff), 1.5);
            for (int k6 = 1; k6 <= 3; k6++) { double ly6 = icy6 - 6 + (k6 - 1) * 6; Line(icx6 - 9, ly6, icx6 + 9, ly6, pn); }
            var b = SBrush(FA(Alpha(dopN != 0 ? HubState.C_ON : 0xFF9AA8C0, dopN != 0 ? 230 : 130), ff));
            for (int k6 = 1; k6 <= 3; k6++)
            {
                double ly6 = icy6 - 6 + (k6 - 1) * 6;
                double kx6 = icx6 - 9 + (dopN != 0 ? (4 + k6 * 4.4) : (2 + k6 * 2.2));
                FillEll(kx6 - 2.4, ly6 - 2.4, 4.8, 4.8, b);
            }
        }, "DEVICE\nOPTIMIZATIONS", dopN != 0 ? $"{dopN} applied" : $"{DOP_SYSN} groups",
           dopN == DOP_SYSN ? HubState.C_ON : (dopN != 0 ? AMBER : 0xFFC7CBE0), dopN != 0 ? 230 : 70);

        // ---- card 7: LOGIN ITEMS (module 7, zone 406) ----
        var (lgN, lgOn) = LoginItems();
        Card(HL.m7y + dy2, HL.m7h, 406, 7, 0.012, 0.02, (st7, hv7, my7) =>
        {
            double icx7 = mx + 24, icy7 = my7 + HL.m7h / 2;
            uint wc = FA(Alpha(AccHi(acc, 0.3), 160 + 55 * st7 + 40 * hv7), ff);
            var pn = Pen(wc, 1.4);
            StrokeRR(icx7 - 11, icy7 - 9, 18, 15, 3, pn);
            Line(icx7 - 11, icy7 - 5, icx7 + 7, icy7 - 5, pn);
            FillRR(icx7 - 9.5, icy7 - 3.5, 15, 8, 2, SBrush(FA(Alpha(acc, 30 + 30 * Math.Max(st7, hv7)), ff)));
            uint bcol = (lgOn && lgN != 0) ? HubState.C_ON : 0xFF9AA8C0;
            double bx7 = icx7 + 6, by7 = icy7 + 1;
            var bp7 = new StreamGeometry();
            using (var g = bp7.Open())
            {
                g.BeginFigure(new Avalonia.Point(bx7 + 1.5, by7 - 8), true);
                g.LineTo(new Avalonia.Point(bx7 - 4.5, by7 + 1));
                g.LineTo(new Avalonia.Point(bx7 - 0.5, by7 + 1));
                g.LineTo(new Avalonia.Point(bx7 - 2.5, by7 + 8));
                g.LineTo(new Avalonia.Point(bx7 + 4.5, by7 - 1));
                g.LineTo(new Avalonia.Point(bx7 + 0.5, by7 - 1));
                g.EndFigure(true);
            }
            StrokePath(bp7, Pen(FA(Alpha(0xFF0C0E17, 220), ff), 2.6));
            FillPath(bp7, SBrush(FA(Alpha(bcol, (lgOn && lgN != 0) ? 240 : 150), ff)));
        }, "LOGIN ITEMS", lgN != 0 ? $"{lgN} app{(lgN == 1 ? "" : "s")}{(lgOn ? "" : "  -  off")}" : "none yet",
           (lgOn && lgN != 0) ? HubState.C_ON : (lgN != 0 ? AMBER : 0xFFC7CBE0), lgN != 0 ? 230 : 70);

        Pop(stRail);
        if (MdRailMax(HL) > 0.5)
        {
            // It is grabbable, like every other scrollbar in the hub - it was the
            // one that could only be moved with the wheel. Wider and brighter
            // under the pointer so it reads as something you can take hold of.
            double rvis = MdRailVis(HL);
            double rth = Math.Max(24, rvis * (rvis / MdRailTot(HL)));
            double rty = MdRailTop(HL) + (rvis - rth) * (HL.mdscr / MdRailMax(HL));
            double hvB = Math.Max(hub.Hv(1150), HL.drag == 26 ? 1.0 : 0.0);
            double bw = 3 + 2 * hvB, bx = mx + HL.mdw + 6 - hvB;
            FillRR(bx, MdRailTop(HL), bw, rvis, bw / 2, SBrush(FA(Alpha(0xFFFFFF, R(18 + 14 * hvB)), ff)));
            FillRR(bx, rty, bw, rth, bw / 2, VBrush(bx, rty, bw, rth, FA(Alpha(AccHi(acc, 0.35), R(190 + 55 * hvB)), ff), FA(Alpha(acc, R(170 + 60 * hvB)), ff)));
        }
        Txt(hubMod != 0 ? "module selected" : "click a module to open", mx, MdRailBot() + 8, HL.mdw + 10, 14, Fonts.fHint, FA(0x4EC7CBE0, ff), Fmt.L);
    }

    static void FFMModDivider(HubSurface hub, double mx, double my, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        Txt("ROBLOX SETTINGS", mx, my - 15, HL.mdw, 12, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, acc, 0.30), 165), ff), Fmt.R);
        FillRR(mx, my - 3, 2.5, 7, 1.2, SBrush(FA(Alpha(acc, 150), ff)));
        GroupLine(mx + 8, mx + HL.mdw - 4, my, acc, ff, now);
        Txt("BUILT-IN MACRO", mx + 8, my + 3, HL.mdw - 8, 12, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, acc, 0.30), 165), ff), Fmt.L);
    }
    static void FFMModDivider2(HubSurface hub, double mx, double my, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        FillRR(mx, my - 3, 2.5, 7, 1.2, SBrush(FA(Alpha(acc, 150), ff)));
        GroupLine(mx + 8, mx + HL.mdw - 4, my, acc, ff, now);
        Txt("DEVICE OPTIMIZATIONS", mx + 8, my + 3, HL.mdw - 8, 12, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, acc, 0.30), 165), ff), Fmt.L);
    }

    /// <summary>FFMFlagPath(x, y, w, h): the pennant.</summary>
    public static Geometry FFMFlagPath(double x, double y, double w, double h)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Avalonia.Point(x, y), true);
            g.LineTo(new Avalonia.Point(x + w, y + h * 0.16));
            g.LineTo(new Avalonia.Point(x, y + h * 0.34));
            g.EndFigure(true);
        }
        return geo;
    }
    /// <summary>CurArrow(x, y, s, pn): the pointer outline.</summary>
    public static void CurArrow(double x, double y, double s, Pen? pn)
    {
        Line(x, y, x, y + 13 * s, pn);
        Line(x, y + 13 * s, x + 3.4 * s, y + 9.6 * s, pn);
        Line(x + 3.4 * s, y + 9.6 * s, x + 5.8 * s, y + 15 * s, pn);
        Line(x + 5.8 * s, y + 15 * s, x + 8 * s, y + 14 * s, pn);
        Line(x + 8 * s, y + 14 * s, x + 5.6 * s, y + 8.8 * s, pn);
        Line(x + 5.6 * s, y + 8.8 * s, x + 9.6 * s, y + 8.2 * s, pn);
        Line(x + 9.6 * s, y + 8.2 * s, x, y, pn);
    }

    // ---- zones, clicks, wheel ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        // The rail's own bar, ahead of the panel: it sits at the rail's edge and
        // a panel that claims a band of the card would otherwise cover it.
        int zb = BarZone(HL, ux, uy);
        if (zb != 0) return zb;
        if (hub.HubMod != 0 && HL.modT > 0.85 && PanelZones.TryGetValue(hub.HubMod, out var pz))
        {
            int zp = pz(hub, ux, uy);
            if (zp != 0) return zp;
        }
        double msc = HL.mdscr;
        if (uy >= MdRailTop(HL) && uy <= MdRailBot() && ux >= HL.mdx && ux <= HL.mdx + HL.mdw)
        {
            if (uy >= HL.mdy - msc && uy <= HL.mdy - msc + HL.m1h) return 400;
            if (uy >= HL.m2y - msc && uy <= HL.m2y - msc + HL.mdh) return 401;
            if (uy >= HL.m3y - msc && uy <= HL.m3y - msc + HL.mdh) return 402;
            if (uy >= HL.m4y - msc && uy <= HL.m4y - msc + HL.mdh) return 403;
            if (uy >= HL.m5y - msc && uy <= HL.m5y - msc + HL.mdh) return 404;
            if (uy >= HL.m6y - msc && uy <= HL.m6y - msc + HL.m6h) return 405;
            if (uy >= HL.m7y - msc && uy <= HL.m7y - msc + HL.m7h) return 406;
        }
        return 0;
    }

    public static bool Click(HubSurface hub, int z)
    {
        if (z >= 400 && z <= 406) { hub.HubModSel(HUBMODS[z - 400]); return true; }
        if (hub.HubMod != 0 && PanelClicks.TryGetValue(hub.HubMod, out var pc)) return pc(hub, z);
        return false;
    }

    /// <summary>The wheel over the rail scrolls it, 34 units a notch.</summary>
    /// <summary>The rail's scrollbar: 1150, a hand's width around the 3 px bar.</summary>
    public static int BarZone(HubLayout HL, double ux, double uy)
    {
        if (MdRailMax(HL) <= 0.5) return 0;
        if (Modules.FastFlags.FfmViews.Open || Modules.FastFlags.Detail.Live) return 0;
        double bx = HL.mdx + HL.mdw + 6;
        return ux >= bx - 7 && ux <= bx + 11 && uy >= MdRailTop(HL) - 4 && uy <= MdRailBot() + 4 ? 1150 : 0;
    }
    /// <summary>The thumb follows the pointer, keeping the grip it was taken by.</summary>
    public static void DragScroll(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        double vis = MdRailVis(HL), tot = MdRailTot(HL);
        double thmb = Math.Max(24, vis * (vis / Math.Max(1, tot)));
        HL.mdscrT = HubUI.ScrollFromY(HL, uy, MdRailTop(HL), vis, tot, vis, thmb, HL.mdscr);
        HL.mdscr = HL.mdscrT;
        hub.Tim(Pace.TICK_A);
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        // The RAIL first. A panel's own wheel guards on y and mostly not on x, so
        // at any height inside the systems list it took the wheel wherever the
        // pointer was - including over the module cards, which then would not
        // scroll. What is under the pointer decides, and over the rail that is
        // the rail.
        // ... unless a sheet is over the card. A view that covers the whole panel
        // covers the rail with it, and what is drawn on top owns the wheel.
        bool covered = Modules.FastFlags.FfmViews.Open || Modules.FastFlags.Detail.Live;
        bool onRail = !covered && ux >= HL.mdx - 10 && ux <= HL.mdx + HL.mdw + 14 && uy >= MdRailTop(HL) && uy <= MdRailBot();
        if (onRail)
        {
            if (MdRailMax(HL) <= 0.5) return true;                              // nowhere to go, but still the rail's wheel to swallow
            HL.mdscrT = Clamp(HL.mdscrT - delta * 34, 0.0, MdRailMax(HL));
            return true;
        }
        if (hub.HubMod != 0 && HL.modT > 0.85 && PanelWheels.TryGetValue(hub.HubMod, out var pw) && pw(hub, ux, uy, delta)) return true;
        return false;
    }
}
