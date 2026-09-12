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

namespace Yuri.Modules.ClientSettings;

/// <summary>RSetSystems / RSetCard / RSetDropdown / RSetZone / RSetClick: module 4's panel. Zone ids are the .ahk's.</summary>
public static class RSetPanel
{
    const uint AMBER = RSet.AMBER, C_ON = RSet.C_ON, C_ACC = RSet.C_ACC;
    const int ROWZ = 1300, KNOBZ = 1400, EXPZ = 1500, CH = RSet.CH, SEPH = RSet.SEPH;
    public static Action? FxOpen;                                            // the EDIT FAST FLAGS page
    public static Func<double>? PageT;                                       // RSetFxT: the page's cross-fade
    public static Action<HubSurface, double, double, double, double, long, uint>? PageDraw;
    public static Func<HubSurface, double, double, int>? PageZone;
    public static Func<HubSurface, int, bool>? PageClick, PagePress;
    public static Func<HubSurface, double, double, double, bool>? PageWheel;
    public static Action<HubSurface, double>? PageDrag;
    static double FxT => PageT?.Invoke() ?? 0.0;
    static bool FxUp => PageT is not null && RSetFx.Fx;
    public static Func<HubSurface, Task>? PickFont, PickLogo, PickLogoS;
    public static Action? LogoRestore; public static Action<int>? LogoApplyOne;
    public static Action<double, double, double, double, uint, long, int>? LogoTile;
    public static Func<int, string, string>? LogoHint;

    public static void Register()
    {
        // The MODULES rail's CLIENT card reads its state through this; left
        // unassigned it always drew "nothing chosen, not applied".
        Integrations.ClientState = () => (RSet.Chosen(), RSet.On);
        RSet.Boot();
        RSetFx.Register();
        RSetLogo.Register();
        Integrations.Panels[4] = Draw;
        Integrations.PanelZones[4] = Zone;
        Integrations.PanelClicks[4] = Click;
        Integrations.PanelWheels[4] = Wheel;
        Integrations.RSetState = () => (RSet.Chosen(), RSet.On);
    }
    public static bool Animating => RSetFx.Animating || Math.Abs(RSet.Scr - RSet.ScrT) > 0.3 || RSet.Dd != 0 || RSet.DdT > 0.004 || (RSet.MsgAt != 0 && Clock.Tick - RSet.MsgAt < 5300)
        || (RSet.ApplyAt != 0 && Clock.Tick - RSet.ApplyAt < 700) || Math.Abs(RSet.OnT - (RSet.On ? 1 : 0)) > 0.004 || Math.Abs(RSet.SrcT - (RSet.SrcOn ? 1 : 0)) > 0.004 || Math.Abs(RSet.SrcSelT - RSet.SrcSel) > 0.004
        || RSet.FlashAt.Values.Any(t => Clock.Tick - t < 700) || (RSet.PresetAt != 0 && Clock.Tick - RSet.PresetAt < 700) || RSet.On;
    static double RowY(HubLayout HL, int i) => RSet.RowY(i, HL.rsrg);
    static double Total(HubLayout HL) => RSet.Total(HL.rsrg);
    static double PresetChipX(HubLayout HL) => HL.abw - 16 - 60 * 3 - 6 * 2;
    static double SrcSwX(HubLayout HL) => HL.abw - 202;
    static double SrcSgX(HubLayout HL) => HL.abw - 158;
    const double SrcSwW = 34, SrcSgW = 71, TrackW = 132, ChipW = 102;
    static double TrackX(HubLayout HL) => HL.abx + HL.abw - 200;
    static double ChipX(HubLayout HL) => HL.abx + HL.abw - 118;
    static bool DdWarn(RSetOpt o, int it) => o.K == "fps" && o.X is not null && it >= 1 && it <= o.X.Count && int.TryParse(o.X[it - 1], out var v) && v > 240;
    static string DdNote(RSetOpt o) => o.K == "fps" ? "marked entries only work if set before roblox opens" : "";
    static double DdPad(RSetOpt o) => DdNote(o) != "" ? 116 : 0;
    static double DdH(RSetOpt o) => Math.Min(o.V.Count, 7) * 23 + 8 + (DdNote(o) != "" ? 24 : 0);
    static void DdGeom(HubLayout HL, int i, out double dy, out bool up)
    {
        double ry = HL.aby + RowY(HL, i) - RSet.Scr, h = DdH(RSet.Opts[i - 1]);
        up = false; dy = ry + 30;
        if (dy + h > HubLayout.pd + HubLayout.ch - 14) { dy = ry + 6 - h; up = true; }
        dy = Clamp(dy, HubLayout.pd + 8, HubLayout.pd + HubLayout.ch - h - 8);
    }

    // ---- icons: one glyph per row kind ----
    static void Icon(int i, double cx, double cy, uint col, bool live, long now)
    {
        if (HubState.LowPerf) return;
        var pn = Pen(col, 1.5);
        string k = RSet.Opts[i - 1].K;
        switch (k)
        {
            case "apply":
            {
                double ay_ = live ? cy + 1 + Math.Sin(DecT(now) * 0.005) * 1.4 : cy + 1;
                Line(cx, cy - 8, cx, ay_, pn); Line(cx - 3.5, ay_ - 3.5, cx, ay_, pn); Line(cx + 3.5, ay_ - 3.5, cx, ay_, pn);
                Line(cx - 8, cy + 4, cx - 8, cy + 8, pn); Line(cx - 8, cy + 8, cx + 8, cy + 8, pn); Line(cx + 8, cy + 8, cx + 8, cy + 4, pn);
                break;
            }
            case "revert": Arc(cx - 7, cy - 7, 14, 14, 110, 250, pn); Line(cx - 6.6, cy - 1.5, cx - 6.6, cy - 6.5, pn); Line(cx - 6.6, cy - 1.5, cx - 2, cy - 3.5, pn); break;
            case "preset": for (int q = 0; q < 3; q++) { double bh = 4 + q * 4; FillRR(cx - 7 + q * 5.5, cy + 6 - bh, 4, bh, 1, SBrush(FA(Alpha(col, 120 + 60 * q), 1))); } break;
            case "render": StrokeRR(cx - 8, cy - 6, 16, 12, 2, pn); Line(cx - 5, cy + 3, cx - 1, cy - 2, pn); Line(cx - 1, cy - 2, cx + 2, cy + 1, pn); Line(cx + 2, cy + 1, cx + 5, cy - 3, pn); break;
            case "light": Ell(cx - 4, cy - 4, 8, 8, pn); for (int q = 0; q < 8; q++) { double a = q * Math.PI / 4; Line(cx + Math.Cos(a) * 6, cy + Math.Sin(a) * 6, cx + Math.Cos(a) * 8.5, cy + Math.Sin(a) * 8.5, pn); } break;
            case "frm": Arc(cx - 8, cy - 6, 16, 16, 180, 180, pn); { double a = (live ? 0.25 + 0.5 * (Math.Sin(DecT(now) * 0.003) + 1) / 2 : 0.5) * Math.PI + Math.PI; Line(cx, cy + 2, cx + Math.Cos(a) * 6, cy + 2 + Math.Sin(a) * 6, pn); } break;
            case "tex": StrokeRR(cx - 8, cy - 8, 16, 16, 2, pn); Line(cx - 8, cy, cx + 8, cy, pn); Line(cx, cy - 8, cx, cy + 8, pn); FillRR(cx - 8, cy - 8, 8, 8, 1, SBrush(FA(Alpha(col, 90), 1))); FillRR(cx, cy, 8, 8, 1, SBrush(FA(Alpha(col, 90), 1))); break;
            case "mesh": Line(cx - 8, cy + 6, cx, cy - 7, pn); Line(cx, cy - 7, cx + 8, cy + 6, pn); Line(cx - 8, cy + 6, cx + 8, cy + 6, pn); Line(cx, cy - 7, cx, cy + 6, pn); Line(cx - 4, cy - 0.5, cx + 4, cy - 0.5, pn); break;
            case "msaa": Line(cx - 8, cy + 7, cx + 7, cy - 8, pn); for (int q = 0; q < 4; q++) FillRR(cx - 7 + q * 4, cy + 5 - q * 4, 3, 3, 0.5, SBrush(FA(Alpha(col, 140), 1))); break;
            case "fps": Ell(cx - 8, cy - 8, 16, 16, pn); Line(cx, cy, cx + 5, cy - 4, pn); Line(cx, cy - 8, cx, cy - 6, pn); Line(cx + 8, cy, cx + 6, cy, pn); break;
            case "dpi": StrokeRR(cx - 8, cy - 6, 16, 12, 2, pn); Line(cx - 4, cy + 9, cx + 4, cy + 9, pn); Line(cx, cy + 6, cx, cy + 9, pn); Line(cx - 2, cy - 2, cx + 2, cy - 2, pn); Line(cx, cy - 4, cx, cy, pn); break;
            case "shadow": Ell(cx - 5, cy - 7, 10, 10, pn); FillEll(cx - 6, cy + 3, 12, 4, SBrush(FA(Alpha(col, 110), 1))); break;
            case "shmap": StrokeRR(cx - 8, cy - 8, 16, 16, 2, pn); for (int q = 1; q < 4; q++) { Line(cx - 8 + q * 4, cy - 8, cx - 8 + q * 4, cy + 8, pn); Line(cx - 8, cy - 8 + q * 4, cx + 8, cy - 8 + q * 4, pn); } break;
            case "postfx": Ell(cx - 6, cy - 6, 12, 12, pn); FillEll(cx - 2, cy - 2, 4, 4, SBrush(col)); Line(cx + 6, cy - 6, cx + 9, cy - 9, pn); Line(cx - 6, cy + 6, cx - 9, cy + 9, pn); break;
            case "font": Line(cx - 7, cy - 7, cx + 7, cy - 7, pn); Line(cx, cy - 7, cx, cy + 8, pn); Line(cx - 3, cy + 8, cx + 3, cy + 8, pn); break;
            case "logo": case "logos": StrokeRR(cx - 8, cy - 8, 16, 16, 3, pn); StrokeRR(cx - 3, cy - 3, 6, 6, 1, pn); if (k == "logos") FillEll(cx + 4, cy + 4, 5, 5, SBrush(FA(Alpha(col, 160), 1))); break;
            case "logorst": Arc(cx - 7, cy - 7, 14, 14, 110, 250, pn); StrokeRR(cx - 3, cy - 3, 6, 6, 1, pn); break;
            case "gfx": for (int q = 0; q < 4; q++) FillRR(cx - 8 + q * 4.5, cy + 6 - (q + 1) * 3, 3, (q + 1) * 3, 1, SBrush(FA(Alpha(col, 100 + 40 * q), 1))); break;
            case "maxq": for (int q = 0; q < 4; q++) FillRR(cx - 8 + q * 4.5, cy - 6, 3, 12, 1, SBrush(FA(Alpha(col, 200), 1))); break;
            case "gmode": Ell(cx - 8, cy - 8, 16, 16, pn); Line(cx, cy, cx - 4, cy - 5, pn); FillEll(cx - 1.5, cy - 1.5, 3, 3, SBrush(col)); break;
            case "vol": Line(cx - 7, cy - 3, cx - 7, cy + 3, pn); Line(cx - 7, cy - 3, cx - 3, cy - 3, pn); Line(cx - 3, cy - 3, cx + 2, cy - 7, pn); Line(cx + 2, cy - 7, cx + 2, cy + 7, pn); Line(cx + 2, cy + 7, cx - 3, cy + 3, pn); Line(cx - 3, cy + 3, cx - 7, cy + 3, pn); Arc(cx - 1, cy - 5, 10, 10, -50, 100, pn); break;
            case "sens": StrokeRR(cx - 5, cy - 8, 10, 16, 5, pn); Line(cx, cy - 8, cx, cy - 3, pn); Line(cx + 7, cy - 4, cx + 9, cy - 6, pn); Line(cx + 7, cy + 4, cx + 9, cy + 6, pn); break;
            case "shiftlock": Arc(cx - 4, cy - 8, 8, 8, 180, 180, pn); StrokeRR(cx - 6, cy - 3, 12, 10, 2, pn); FillEll(cx - 1.5, cy + 0.5, 3, 3, SBrush(col)); break;
            case "cammode": StrokeRR(cx - 8, cy - 5, 12, 10, 2, pn); Line(cx + 4, cy - 2, cx + 8, cy - 5, pn); Line(cx + 8, cy - 5, cx + 8, cy + 5, pn); Line(cx + 8, cy + 5, cx + 4, cy + 2, pn); break;
            case "movemode": Line(cx - 8, cy + 6, cx + 6, cy - 6, pn); Line(cx + 6, cy - 6, cx + 1, cy - 6, pn); Line(cx + 6, cy - 6, cx + 6, cy - 1, pn); FillEll(cx - 8, cy + 4, 4, 4, SBrush(col)); break;
            case "fullscr": StrokeRR(cx - 8, cy - 6, 16, 12, 1.5, pn); Line(cx - 5, cy - 3, cx - 2, cy - 3, pn); Line(cx - 5, cy - 3, cx - 5, cy, pn); Line(cx + 5, cy + 3, cx + 2, cy + 3, pn); Line(cx + 5, cy + 3, cx + 5, cy, pn); break;
            case "startmax": StrokeRR(cx - 8, cy - 6, 16, 12, 1.5, pn); StrokeRR(cx - 8, cy - 6, 16, 3, 0, pn); break;
            case "redmot": Line(cx - 8, cy, cx + 8, cy, pn); Line(cx + 8, cy, cx + 4, cy - 4, pn); Line(cx + 8, cy, cx + 4, cy + 4, pn); Line(cx - 8, cy - 5, cx - 2, cy - 5, pn); Line(cx - 8, cy + 5, cx - 2, cy + 5, pn); break;
            case "uitrans": StrokeRR(cx - 8, cy - 8, 12, 12, 2, pn); StrokeRR(cx - 3, cy - 3, 12, 12, 2, Pen(FA(Alpha(col, 120), 1), 1.5)); break;
            case "fxedit": Line(cx - 6, cy - 7, cx - 6, cy + 7, pn); Line(cx - 6, cy - 7, cx + 3, cy - 7, pn); Line(cx - 6, cy, cx + 1, cy, pn); Line(cx + 2, cy + 8, cx + 8, cy + 2, pn); Line(cx + 5, cy + 5, cx + 7, cy + 7, pn); break;
            case "src": Line(cx - 8, cy - 5, cx, cy - 5, pn); Line(cx - 8, cy + 5, cx, cy + 5, pn); Line(cx, cy - 5, cx + 8, cy, pn); Line(cx, cy + 5, cx + 8, cy, pn); FillEll(cx + 6, cy - 2, 4, 4, SBrush(col)); break;
            default: Ell(cx - 6, cy - 6, 12, 12, pn); break;
        }
    }

    public static void Draw(HubSurface hub, double ax, double ay, double x0, double y0, double dx, double dy2, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        double mt = HL.modT;
        int chosen = RSet.Chosen();
        RSet.OnT += ((RSet.On ? 1.0 : 0.0) - RSet.OnT) * EK(0.2);
        RSet.SrcT += ((RSet.SrcOn ? 1.0 : 0.0) - RSet.SrcT) * EK(0.25);
        RSet.SrcSelT += (RSet.SrcSel - RSet.SrcSelT) * EK(0.25);
        RSet.DdT += ((RSet.Dd != 0 ? 1.0 : 0.0) - RSet.DdT) * EK(0.3); if (RSet.Dd == 0 && RSet.DdT < 0.004) RSet.DdT = 0;
        Txt("SYSTEMS", ax, y0 + 36, 120, 14, Fonts.fBadge, FA(0x62C7CBE0, ff), Fmt.L);
        Txt(mt > 0.5 ? RSet.SysN + " listed" : "0 listed", ax + 66, y0 + 36, 80, 14, HL.fS, FA(Alpha(acc, 60 + 120 * mt), ff), Fmt.L);
        if (mt > 0.4)
        {
            double fh = ff * Clamp((mt - 0.4) / 0.4, 0.0, 1.0);
            FFMBtn(921, ax + HL.abw - 96, y0 + 30, 96, 22, RSet.On ? "REVERT" : "APPLY", acc, fh, RSet.On ? 2 : chosen > 0 ? 1 : 0);
        }
        double pah = 26 + (HL.ffh - 26) * mt;
        double fxT = FxT, pageH = pah + (RSetFx.H(HL) - pah) * fxT, fPan = ff * (1 - fxT), fPag = ff * fxT;
        if (fPan > 0.004)
        {
            FillRR(ax, ay, HL.abw, pah, 12, VBrush(ax, ay, HL.abw, pah, FA(Mix(0xFF141728, 0xFF181C34, mt), fPan), FA(0xFF12141F, fPan)));
            MiniBackdrop(ax, ay, HL.abw, pah, 12, acc, fPan, now, 0.9);
            Integrations.HubEmptyGhost(hub, ax, ay, pah, fPan);
            StrokeRR(ax, ay, HL.abw, pah, 12, Pen(FA(Alpha(acc, 70 + 90 * mt), fPan), 1.2));
            double gl = 40 * mt;
            var pnG = Pen(FA(Alpha(AccHi(acc, 0.4), 200 * mt), fPan), 1.6);
            Line(ax + 14, ay + 0.6, ax + 14 + gl, ay + 0.6, pnG); Line(ax + HL.abw - 14 - gl, ay + pah - 0.6, ax + HL.abw - 14, ay + pah - 0.6, pnG);
        }
        ModRing(ax, ay, HL.abw, pah, 12, acc, ff, HL.modAt, now, 700, 18, hub.HubMod != 0);
        if (mt < 0.02) return;
        if (fPan <= 0.004) { if (fPag > 0.004) PageDraw?.Invoke(hub, ax, ay, pageH, fPag, now, acc); return; }
        ff = fPan;
        double rowSl = fxT * 26;
        int st = PushG(); ClipRR(ax, ay, HL.abw, pah, 12);
        Divider(hub, ax, ay, ff, mt, now, acc, rowSl);
        var locked = RSet.LockedRows();
        for (int i = 1; i <= RSet.SysN; i++)
        {
            double cg = Clamp((mt - 0.30 - Math.Min(i, 4) * 0.07) / 0.38, 0.0, 1.0);
            if (cg <= 0.01) continue;
            double fb = ff * cg;
            double ry = ay + RowY(HL, i) - RSet.Scr;
            if (ry + HL.rsrh < ay + 4 || ry > ay + pah - 4) continue;
            var o = RSet.Opts[i - 1];
            int idx = RSet.Idx(o);
            bool set = idx > 1;
            double hv = Math.Max(hub.Hv(ROWZ + i), Math.Max(hub.Hv(EXPZ + i), hub.Hv(KNOBZ + i)));
            double sld = (1 - cg) * 10 - rowSl, axs = ax + sld;
            bool avail = o.Xml is null || o.F is not null || RSet.XmlPath != "";
            bool lck = locked.Contains(o.K);
            bool skip = o.F is not null && !RSet.SrcRows;
            string ovr = RSet.RowOverride(o);
            bool moot = ovr != "";
            avail = avail && !lck && !moot;
            double fbc = lck || moot ? fb * 0.34 : skip ? fb * 0.55 : fb;
            bool live = o.Ui == "action" ? (o.K == "apply" ? chosen > 0 : o.K == "fxedit" ? RSet.Extra.Count > 0 : o.K == "logorst" ? (RSet.IcoOrigCount?.Invoke() ?? 0) > 0 || RSet.Logo != "" || RSet.LogoS != "" : RSet.On)
                : o.Ui == "preset" ? RSet.PresetActive() > 0 : o.Ui == "source" ? RSet.SrcOn : set && avail;
            if (hv > 0.01) FillRR(ax + 6, ry, HL.abw - 12, HL.rsrh, 8, HBrush(ax + 6, ry, HL.abw - 12, HL.rsrh, FA(Alpha(0xFFFFFF, 15 * hv), fb), FA(Alpha(0xFFFFFF, 3 * hv), fb)));
            FillRR(axs + 14, ry + 8, 3, 20, 1.5, VBrush(axs + 14, ry + 8, 3, 20, FA(Alpha(live ? AccHi(acc, 0.35) : 0xFFC7CBE0, live ? 235 : 60), fb), FA(Alpha(live ? acc : 0xFFC7CBE0, live ? 150 : 40), fb)));
            Icon(i, axs + 36, ry + 16, FA(Alpha(live ? acc : 0xFF9AA8C0, live ? 220 : 110), fb), live, now);
            if (lck) { Arc(axs + 32, ry + 4, 9, 9, 180, 180, Pen(FA(Alpha(0xFF9AA8C0, 150), fb), 1.3)); FillRR(axs + 30.5, ry + 8.5, 12, 8, 2, SBrush(FA(Alpha(0xFF9AA8C0, 160), fb))); }
            else if (moot) { var pnM = Pen(FA(Alpha(0xFF9AA8C0, 160), fb), 1.3); Ell(axs + 31.5, ry + 4.5, 9, 9, pnM); Line(axs + 33.5, ry + 11.5, axs + 38.5, ry + 6.5, pnM); }
            double tw = (o.Ui == "slider" ? HL.abw - 208 : o.Ui == "preset" ? HL.abw - 258 : o.Ui == "source" ? SrcSwX(HL) - 10 : HL.abw - 132) - 54;
            Txt(o.N, axs + 54, ry + 4, tw, 16, Fonts.fBadge, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.18), avail ? (skip ? 165 : 235) : 120), fb), Fmt.L);
            string hint;
            if (o.K == "fxedit") { int nx = RSet.Extra.Count; string cf = nx > 0 ? nx + " custom flag" + (nx == 1 ? "" : "s") : "nothing custom"; hint = !RSet.SrcExtra ? cf + "  \u00B7  not written - FLAG SOURCE is CLIENT SYSTEMS" : nx > 0 ? cf + "  \u00B7  " + o.H : o.H; }
            else if (o.Ui == "action") hint = o.H;
            else if (o.Ui == "source") hint = !RSet.SrcOn ? o.H + "  \u00B7  MIXING both, an override wins a shared name" : RSet.SrcSel == 1 ? o.H + "  \u00B7  CLIENT SYSTEMS only, overrides ignored" : o.H + "  \u00B7  EDIT FAST FLAGS only, the rows above ignored";
            else if (o.Ui == "preset") { int ap = RSet.PresetActive(); hint = ap > 0 ? o.H + "  \u00B7  now " + o.V[ap - 1] : o.H + "  \u00B7  quality only, not volume or sensitivity"; }
            else if (lck) hint = "taken over in EDIT FAST FLAGS  \u00B7  remove the override there to get this back";
            else if (moot) hint = "overridden by " + ovr;
            else if (skip) hint = o.H + "  \u00B7  not written - FLAG SOURCE is EDIT FAST FLAGS";
            else if (!avail) hint = "not present in your client settings file";
            else if (o.Xml is not null && RSet.XmlHave.TryGetValue(o.Xml, out var have)) hint = o.H + "  \u00B7  now " + RSet.XmlLabel(o, have);
            else if (o.Ui == "file" && o.K is "logo" or "logos") { string src = o.K == "logo" ? RSet.Logo : RSet.LogoS; hint = LogoHint is not null ? LogoHint(o.K == "logos" ? 2 : 1, o.H) : src == "" ? o.H : Path.GetFileName(src) + "  \u00B7  staged - APPLY writes it"; }
            else if (o.K == "fps" && idx > 1) { bool hiFp = o.X is not null && int.TryParse(o.X[idx - 1], out var tg9) && tg9 > 240; hint = hiFp ? o.H + "  \u00B7  above 240 needs a restart to take" : FfmPanel.Pid != 0 ? o.H + "  \u00B7  applies live to the attached client" : o.H + "  \u00B7  applies next launch"; }
            else if (o.Ui == "file" && o.K == "font" && RSet.Font != "") hint = Path.GetFileName(RSet.Font);
            else if (o.Al == 0) hint = o.Alp != 0 ? o.H + "  \u00B7  partly off the allowlist - the rest needs the flag manager" : o.H + "  \u00B7  off the allowlist, use the flag manager";
            else hint = o.H;
            double hw = o.Ui == "slider" ? HL.abw - 264 : o.Ui == "preset" ? PresetChipX(HL) - 64 : o.Ui == "source" ? SrcSwX(HL) - 64 : o.Ui == "file" && o.K != "font" ? HL.abw - 240 : o.Ui is "action" or "file" ? HL.abw - 172 : HL.abw - 182;
            Txt(FFMElide(hint, Fonts.fHint, hw), axs + 54, ry + 20, hw, 13, Fonts.fHint, FA(avail ? 0x7EC7CBE0 : Alpha(acc, 102), fb), Fmt.L);
            if (o.Ui == "preset")
            {
                int actP = RSet.PresetActive();
                double pw3 = 60, pg3 = 6, px3 = axs + PresetChipX(HL);
                for (int q = 1; q <= 3; q++)
                {
                    double hq = hub.Hv(924 + q);
                    bool onq = actP == q;
                    double fl3 = RSet.PresetAt != 0 && RSet.Preset == q && now - RSet.PresetAt < 620 ? 1 - Clamp((now - RSet.PresetAt) / 620.0, 0.0, 1.0) : 0.0;
                    double bx3 = px3 + (q - 1) * (pw3 + pg3), by3 = ry + 8 - 1.5 * hq;
                    FillRR(bx3, by3, pw3, 26, 8, VBrush(bx3, by3, pw3, 26, FA(Alpha(acc, (onq ? 74 : 26) + 46 * hq + 60 * fl3), fb), FA(Alpha(acc, (onq ? 34 : 10) + 22 * hq + 30 * fl3), fb)));
                    MicroBackdrop(bx3, by3, pw3, 26, 8, acc, fb, now, 0.85);
                    StrokeRR(bx3, by3, pw3, 26, 8, Pen(FA(Alpha(acc, (onq ? 200 : 96) + 100 * hq), fb), 1.1));
                    if (onq) FillRR(bx3 + 12, by3 + 21.5, pw3 - 24, 2.4, 1.2, SBrush(FA(Alpha(AccHi(acc, 0.45), 225), fb)));
                    Txt(o.V[q - 1], bx3, by3 + 5.5, pw3, 15, HL.fXs, FA(Alpha(onq ? AccHi(acc, 0.4) : 0xFFE8EAF6, onq ? 245 : 165 + 60 * hq), fb), Fmt.C);
                    if (fl3 > 0.01) { double e3 = 1 - fl3, ex3 = e3 * 12; StrokeRR(bx3 - ex3, by3 - ex3 * 0.6, pw3 + ex3 * 2, 26 + ex3 * 1.2, 8, Pen(FA(Alpha(acc, R(190 * fl3)), fb), 2 * fl3 + 0.3)); }
                }
            }
            else if (o.Ui == "action")
            {
                if (o.K == "apply" && !RSet.NoProc && Platform.Roblox.IsRunning()) Txt("close rbx", axs + HL.abw - 182, ry + 7, 68, 14, HL.fXs, FA(Alpha(AMBER, 210), fb), Fmt.R);
                else if (o.K == "apply" && RSet.On) Txt("live", axs + HL.abw - 176, ry + 7, 62, 14, HL.fXs, FA(Alpha(C_ON, 200), fb), Fmt.R);
                FFMBtn(ROWZ + i, axs + HL.abw - 108, ry + 1, 92, 24, o.V[0], acc, fbc, lck ? 4 : o.K == "apply" ? (chosen > 0 ? 1 : 0) : o.K == "fxedit" ? (RSet.Extra.Count > 0 ? 1 : 0) : 2);
            }
            else if (o.Ui == "source")
            {
                double swx = axs + SrcSwX(HL), swy = ry + 12, hvs = hub.Hv(933);
                double flS = RSet.FlashAt.TryGetValue("src", out var fsa) && now - fsa < 360 ? 1 - Clamp((now - fsa) / 360.0, 0.0, 1.0) : 0.0;
                if (flS > 0.01) { double rr = 10 + 16 * (1 - flS); Ell(swx + SrcSwW / 2 - rr, swy + 8 - rr, rr * 2, rr * 2, Pen(FA(Alpha(AccHi(acc, 0.4), R(150 * flS)), fb), 1.4 * flS + 0.4)); }
                FFMTogDraw(swx, swy, SrcSwW, 16, RSet.SrcT, acc, hvs, fb);
                double sgx = axs + SrcSgX(HL), sgw = SrcSgW;
                FillRR(sgx, ry + 9, sgw * 2, 22, 7, SBrush(FA(Alpha(0xFFFFFF, 12), fb)));
                double mkx = sgx + (RSet.SrcSelT - 1) * sgw;
                if (RSet.SrcT > 0.01)
                {
                    FillRR(mkx + 2, ry + 11, sgw - 4, 18, 6, VBrush(mkx + 2, ry + 11, sgw - 4, 18, FA(Alpha(AccHi(acc, 0.3), R(78 * RSet.SrcT)), fb), FA(Alpha(acc, R(40 * RSet.SrcT)), fb)));
                    StrokeRR(mkx + 2, ry + 11, sgw - 4, 18, 6, Pen(FA(Alpha(acc, R(180 * RSet.SrcT)), fb), 1.1));
                }
                StrokeRR(sgx, ry + 9, sgw * 2, 22, 7, Pen(FA(Alpha(acc, 60), fb), 1));
                for (int q3 = 1; q3 <= 2; q3++)
                {
                    double hq3 = hub.Hv(933 + q3), onq3 = RSet.SrcT * Clamp(1 - Math.Abs(RSet.SrcSelT - q3), 0.0, 1.0);
                    Txt(q3 == 1 ? "SYSTEMS" : "FLAGS", sgx + (q3 - 1) * sgw, ry + 14, sgw, 14, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, AccHi(acc, 0.4), onq3), R(140 + 60 * hq3 + 55 * onq3)), fb), Fmt.C);
                }
            }
            else if (o.Ui == "slider") Slider(hub, ROWZ + i, i, ry, axs, acc, fbc, now);
            else if (o.Ui == "file")
            {
                if (o.K is "logo" or "logos")
                {
                    int lgK = o.K == "logos" ? 2 : 1;
                    if (!HubState.LowPerf) LogoTile?.Invoke(axs + HL.abw - 176, ry + 5, 26, fb, acc, now, lgK);
                    bool hasI = (lgK == 1 ? RSet.Logo : RSet.LogoS) != "";
                    FFMBtn(927 + lgK, axs + HL.abw - 144, ry + 1, 64, 24, "BROWSE", acc, fbc, hasI ? 0 : 1);
                    FFMBtn(930 + lgK, axs + HL.abw - 76, ry + 1, 60, 24, "APPLY", acc, fb, hasI ? 1 : 4);
                }
                else FFMBtn(924, axs + HL.abw - 108, ry + 1, 92, 24, "BROWSE", acc, fbc, RSet.Font != "" ? 0 : 1);
            }
            else
            {
                double ot = RSet.Dd == i ? RSet.DdT : 0.0;
                Chip(hub, ROWZ + i, axs + HL.abw - 118, ry + 2, ChipW, 22, o.V[idx - 1], set && avail, acc, fbc, RSet.FlashAt.TryGetValue(o.K, out var fa) ? fa : 0, now, ot);
            }
        }
        double sysTot = Total(HL), sysVis = HL.ffh - 20;
        if (sysTot > sysVis)
        {
            double sbS = Math.Max(hub.Hv(920), HL.drag == 14 ? 1.0 : 0.0);
            double sw2 = 3 + 3 * sbS, sxs = ax + HL.abw - 6 - 3 * sbS;
            FillRR(sxs, ay + 10, sw2, sysVis, sw2 / 2, SBrush(FA(Alpha(0xFFFFFF, 18 + 26 * sbS), ff * mt)));
            double th2 = Math.Max(22, sysVis * (sysVis / sysTot)), ty2 = ay + 10 + (sysVis - th2) * (RSet.Scr / Math.Max(1, sysTot - sysVis));
            FillRR(sxs, ty2, sw2, th2, sw2 / 2, VBrush(sxs, ty2, sw2, th2, FA(Alpha(AccHi(acc, 0.35), 190 + 65 * sbS), ff * mt), FA(Alpha(acc, 170 + 60 * sbS), ff * mt)));
        }
        Pop(st);
        RSet.ScrT = Clamp(RSet.ScrT, 0.0, Math.Max(0.0, sysTot - sysVis));
        RSet.Scr += (RSet.ScrT - RSet.Scr) * EK(0.3); if (Math.Abs(RSet.Scr - RSet.ScrT) < 0.3) RSet.Scr = RSet.ScrT;
        Card(hub, ax, ff, now, acc, mt, chosen);
        Dropdown(hub, ff, now, acc);
        if (fPag > 0.004) PageDraw?.Invoke(hub, ax, ay, pageH, fPag, now, acc);
    }
    static void Divider(HubSurface hub, double ax, double ay, double ff, double mt, long now, uint acc, double rowSl = 0)
    {
        var HL = hub.HL;
        double cg = Clamp((mt - 0.58) / 0.34, 0.0, 1.0);
        if (cg <= 0.01) return;
        double fd = ff * cg;
        for (int i = 2; i <= RSet.SysN; i++)
        {
            if (RSet.Opts[i - 1].Grp == RSet.Opts[i - 2].Grp) continue;
            double dy = ay + RowY(HL, i) - SEPH / 2.0 - 3 - RSet.Scr;
            if (dy < ay - 10 || dy > ay + HL.ffh + 10) continue;
            double sld = (1 - cg) * 14 - rowSl, lx = ax + 14 + sld;
            string lbl = RSet.GrpName(RSet.Opts[i - 1].Grp);
            double lw = Fonts.MeasureW(lbl, HL.fXs);
            var pn = Pen(FA(Alpha(acc, 150 + 70 * cg), fd), 1.4);
            Line(lx, dy - 3, lx + 3.5, dy + 0.5, pn); Line(lx + 3.5, dy + 0.5, lx, dy + 4, pn);
            Txt(lbl, lx + 10, dy - 7, lw + 8, 14, HL.fXs, FA(Alpha(Mix(0xFFC7CBE0, acc, 0.42), 150 + 70 * cg), fd), Fmt.L);
            GroupLine(lx + 24 + lw, ax + HL.abw - 16 + sld, dy, acc, fd, now);
        }
    }
    static void Chip(HubSurface hub, int z, double cx2, double cy2, double w, double h, string label, bool chosen, uint acc, double ff, long flashAt, long now, double openT)
    {
        var HL = hub.HL;
        double hvE = Ease3(hub.Hv(z));
        double fl = flashAt != 0 && now - flashAt < 420 ? 1 - Clamp((now - flashAt) / 420.0, 0.0, 1.0) : 0.0;
        int st = PushXform(cx2 + w / 2, cy2 + h / 2, 1 + 0.05 * hvE + 0.07 * fl, 0);
        if (fl > 0.01) { double ex = (1 - fl) * 12; StrokeRR(cx2 - ex, cy2 - ex * 0.6, w + ex * 2, h + ex * 1.2, 6 + ex * 0.3, Pen(FA(Alpha(acc, R(190 * fl)), ff), 1.6 * fl + 0.3)); }
        double lit = Math.Max(hvE, openT);
        uint bc = chosen ? Alpha(acc, R(36 + 34 * lit + 40 * fl)) : Alpha(0xFFFFFF, R(14 + 20 * lit));
        FillRR(cx2, cy2, w, h, 6, SBrush(FA(bc, ff)));
        MicroBackdrop(cx2, cy2, w, h, 6, acc, ff, now, 0.85);
        StrokeRR(cx2, cy2, w, h, 6, Pen(FA(Alpha(chosen ? acc : 0xFF9AA8C0, R(chosen ? 150 + 80 * lit : 55 + 60 * lit)), ff), 1.1));
        Txt(label, cx2, cy2 - 1, w - 10, h, HL.fXs, FA(Alpha(chosen ? AccHi(acc, 0.45) : 0xFFC7CBE0, R(chosen ? 235 : 165 + 50 * lit)), ff), Fmt.C);
        double ccx = cx2 + w - 10, ccy = cy2 + h / 2;
        int stC = PushXform(ccx, ccy, 1, -90 * openT);
        var pn = Pen(FA(Alpha(chosen ? acc : 0xFF9AA8C0, R(90 + 120 * lit)), ff), 1.2);
        Line(ccx - 3, ccy - 1.6, ccx, ccy + 1.6, pn); Line(ccx, ccy + 1.6, ccx + 3, ccy - 1.6, pn);
        Pop(stC); Pop(st);
    }
    static void Slider(HubSurface hub, int z, int i, double ry, double ax, uint acc, double ff, long now)
    {
        var HL = hub.HL;
        var o = RSet.Opts[i - 1];
        int idx = RSet.Idx(o), n = o.V.Count;
        double fr = n > 1 ? (idx - 1) / (double)(n - 1) : 0.0;
        bool set = idx > 1, drag = HL.drag == 15 && RSet.Sld == i;
        double hvT = Math.Max(hub.Hv(z), drag ? 1.0 : 0.0), hvK = Math.Max(hub.Hv(KNOBZ + i), drag ? 1.0 : 0.0);
        double hvE = Ease3(Math.Max(hvT, hvK)), hkE = Ease3(hvK);
        double fl = RSet.FlashAt.TryGetValue(o.K, out var fa) && now - fa < 360 ? 1 - Clamp((now - fa) / 360.0, 0.0, 1.0) : 0.0;
        double tx = ax + HL.abw - 200, tw = TrackW, ty = ry + 13;
        FillRR(tx, ty - 2.5, tw, 5, 2.5, SBrush(FA(0x22FFFFFF, ff)));
        if (fr > 0.001) FillRR(tx, ty - 2.5, tw * fr + 1, 5, 2.5, VBrush(tx, ty - 2.5, tw * fr + 1, 5, FA(Alpha(AccHi(acc, 0.25), 235), ff), FA(Alpha(acc, 150), ff)));
        int nt = n <= 8 ? n : 6;
        for (int k = 0; k < nt; k++) { double tkx = tx + k * (tw / (nt - 1)); Line(tkx, ty + 5, tkx, ty + 8, Pen(FA(Alpha(0xFFFFFF, R(22 + 22 * hvE)), ff), 1)); }
        double knx = tx + tw * fr;
        if (hkE > 0.01 || fl > 0.01) { double r_ = 9 + 3 * hkE + 3 * fl; FillEll(knx - r_, ty - r_, r_ * 2, r_ * 2, SBrush(FA(Alpha(acc, R(40 + 55 * hkE + 60 * fl)), ff))); }
        double kr = 5.5 + 1.1 * hkE + 0.8 * fl, kdp = drag ? 0.6 : 0.0;
        FillEll(knx - kr, ty - kr + 1.5, kr * 2, kr * 2, SBrush(FA(Alpha(0x000000, 90), ff)));
        FillEll(knx - kr + kdp * 0.5, ty - kr + kdp * 0.5, (kr - kdp * 0.5) * 2, (kr - kdp * 0.5) * 2, SBrush(FA(Alpha(0xFEFEFE, 250), ff)));
        if (hkE > 0.01) Ell(knx - kr - 0.6, ty - kr - 0.6, (kr + 0.6) * 2, (kr + 0.6) * 2, Pen(FA(Alpha(AccHi(acc, 0.4), R(140 * hkE)), ff), 1.1));
        double kc = 2.2 + 0.5 * hkE;
        FillEll(knx - kc, ty - kc, kc * 2, kc * 2, SBrush(FA(Alpha(set ? acc : 0xFF9AA8C0, set ? 240 : 150), ff)));
        Txt(o.V[idx - 1], ax + HL.abw - 62, ry + 5 - 2 * fl, 46, 16, Fonts.fBadge, FA(Alpha(set ? AccHi(acc, 0.4) : 0xFF9AA8C0, R(set ? 235 : 160)), ff), Fmt.R);
    }
    static void Dropdown(HubSurface hub, double ff, long now, uint acc)
    {
        var HL = hub.HL;
        double t = RSet.DdT;
        if (t <= 0.004) return;
        int i = RSet.Dd != 0 ? RSet.Dd : RSet.DdPrev;
        if (i < 1 || i > RSet.Opts.Count) return;
        var o = RSet.Opts[i - 1];
        int n = Math.Min(o.V.Count, 7);
        double pad = DdPad(o), dx = ChipX(HL) - 4 - pad, dw = ChipW + 8 + pad;
        DdGeom(HL, i, out var dy, out var up);
        double dh = DdH(o);
        string note = DdNote(o);
        double e = Ease3(t);
        ShadowDraw(dx, dy, dw, dh, 9, 8, 16, 16, ff * e);
        double axD = dx + dw / 2, ayD = dy + (up ? dh : 0);
        int stD = PushXformXY(axD, ayD, 1, 0.55 + 0.45 * e, 0);
        FillRR(dx, dy, dw, dh, 9, VBrush(dx, dy, dw, dh, FA(Alpha(AccHi(0xFF1B2038, 0.03), R(252 * e)), ff), FA(Alpha(0xFF141728, R(252 * e)), ff)));
        MiniBackdrop(dx, dy, dw, dh, 9, acc, ff * e, now, 0.8);
        StrokeRR(dx, dy, dw, dh, 9, Pen(FA(Alpha(acc, R(150 * e)), ff), 1.2));
        int curIdx = RSet.Idx(o);
        for (int j = 1; j <= n; j++)
        {
            int it = RSet.DdTop + j;
            if (it < 1 || it > o.V.Count) continue;
            double rt = Clamp((t - 0.08 * (j - 1)) / 0.55, 0.0, 1.0);
            if (rt <= 0.01) continue;
            double iy = dy + 4 + (j - 1) * 23, hv = hub.Hv(939 + j);
            bool on = it == curIdx;
            if (hv > 0.01) FillRR(dx + 3, iy, dw - 6, 22, 6, HBrush(dx + 3, iy, dw - 6, 22, FA(Alpha(0xFFFFFF, R(20 * hv * rt)), ff), FA(Alpha(0xFFFFFF, R(5 * hv * rt)), ff)));
            if (on) FillRR(dx + 5, iy + 5, 2.5, 12, 1.2, SBrush(FA(Alpha(acc, R(200 * rt)), ff)));
            Txt(o.V[it - 1], dx + 14 + 2 * hv, iy - 1 + (1 - rt) * 4, dw - (note != "" ? 46 : 34), 22, HL.fXs, FA(Alpha(on ? AccHi(acc, 0.45) : 0xFFE8EAF6, R((on ? 240 : 175 + 60 * hv) * rt)), ff), Fmt.L);
            if (DdWarn(o, it)) { double wx = dx + dw - 26; var bW = SBrush(FA(Alpha(AMBER, R(225 * rt)), ff)); FillRR(wx - 1, iy + 5.5, 2, 6.5, 1, bW); FillEll(wx - 1, iy + 14, 2, 2, bW); }
            if (on) { var pnT = Pen(FA(Alpha(AccHi(acc, 0.4), R(235 * rt)), ff), 1.5); double tkx = dx + dw - 15; Line(tkx - 3.5, iy + 11, tkx - 1, iy + 14, pnT); Line(tkx - 1, iy + 14, tkx + 3.5, iy + 7.5, pnT); }
        }
        if (note != "")
        {
            double ny = dy + 4 + n * 23;
            FadeLine(dx + 10, dx + dw - 10, ny + 1, 0x18FFFFFF, ff * e);
            var bN = SBrush(FA(Alpha(AMBER, R(215 * e)), ff)); FillRR(dx + 12, ny + 7, 2, 6.5, 1, bN); FillEll(dx + 12, ny + 15.5, 2, 2, bN);
            Txt(note, dx + 20, ny + 5, dw - 30, 16, HL.fXs, FA(Alpha(AMBER, R(200 * e)), ff), Fmt.L);
        }
        if (o.V.Count > n) FillRR(dx + dw / 2 - 9, dy + dh - 4, 18, 1.6, 0.8, SBrush(FA(Alpha(0xFFFFFF, R(50 * e)), ff)));
        Pop(stD);
    }
    static void Card(HubSurface hub, double ax, double ff, long now, uint acc, double mt, int chosen)
    {
        var HL = hub.HL;
        double cp = Clamp((mt - 0.60) / 0.34, 0.0, 1.0);
        if (cp <= 0.01) return;
        double fp = ff * cp, px = ax, pw = HL.abw, py = HL.ffby + (1 - cp) * 10;
        FillRR(px, py, pw, CH, 12, VBrush(px, py, pw, CH, FA(Mix(0xFF141728, 0xFF181C34, cp), fp), FA(0xFF11131F, fp)));
        MiniBackdrop(px, py, pw, CH, 12, acc, fp, now, 0.9);
        StrokeRR(px, py, pw, CH, 12, Pen(FA(Alpha(acc, 60 + 70 * cp), fp), 1.2));
        FillRR(px, py + 10, 3, 20, 1.5, SBrush(FA(Alpha(acc, 200 * cp), fp)));
        Txt("STATE", px + 16, py + 9, 120, 16, Fonts.fBadge, FA(Alpha(0xFFE8EAF6, 225), fp), Fmt.L);
        bool on = RSet.On;
        uint pc = on ? C_ON : 0xFF9AA8C0;
        FillRR(px + pw - 116, py + 9, 100, 17, 5, SBrush(FA(Alpha(pc, on ? 40 : 22), fp)));
        FillEll(px + pw - 108, py + 15, 5, 5, SBrush(FA(Alpha(pc, on ? 180 + 60 * Math.Abs(Math.Sin(DecT(now) * 0.0026)) : 90), fp)));
        Txt(on ? "APPLIED" : "NOT APPLIED", px + pw - 100, py + 9, 84, 17, HL.fXs, FA(Alpha(AccHi(pc, 0.25), on ? 235 : 150), fp), Fmt.L);
        double dcx = px + 14 + 39, dcy = py + 34 + 39, dscl = 1.0;
        if (RSet.ApplyAt != 0 && now - RSet.ApplyAt < 520) dscl = 0.88 + 0.12 * EBackOut(Clamp((now - RSet.ApplyAt) / 520.0, 0.0, 1.0), 2.0);
        int stD = PushXform(dcx, dcy, dscl, 0);
        FillEll(dcx - 34, dcy - 34, 68, 68, SBrush(FA(0xFF0B0D16, fp)));
        Ell(dcx - 28, dcy - 28, 56, 56, Pen(FA(Alpha(0xFFFFFF, 26), fp), 3));
        if (RSet.OnT > 0.004) Arc(dcx - 28, dcy - 28, 56, 56, -90, 359.9 * RSet.OnT, Pen(FA(Alpha(AccHi(acc, 0.25), 235), fp), 3));
        Txt(chosen.ToString(), dcx - 30, dcy - 16, 60, 26, HL.fV, FA(Alpha(on ? AccHi(acc, 0.3) : 0xFFE8EAF6, on ? 245 : 175), fp), Fmt.C);
        Txt(chosen == 1 ? "row set" : "rows set", dcx - 40, dcy + 10, 80, 14, HL.fXs, FA(0x72C7CBE0, fp), Fmt.C);
        if (RSet.ApplyAt != 0 && now - RSet.ApplyAt < 620) { double e = Ease3((now - RSet.ApplyAt) / 620.0), r_ = 34 + e * 16; Ell(dcx - r_, dcy - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(acc, R(180 * (1 - e))), fp), 2.2 * (1 - e) + 0.3)); }
        Pop(stD);
        double cx2 = px + 14 + 78 + 16, cw2 = px + pw - 16 - cx2;
        int nf = RSet.Flags().Count;
        Row(hub, cx2, py + 36, cw2, "FLAGS", nf > 0 ? nf + " flag(s)  \u00B7  not shared with the manager" : "none - every row on auto", nf > 0 ? 0xFFC7CBE0 : 0xFF9AA8C0, fp);
        string xp = RSet.Xml();
        string lk = ""; try { if (xp != "" && (File.GetAttributes(xp) & FileAttributes.ReadOnly) != 0) lk = "  \u00B7  locked"; } catch { }
        Row(hub, cx2, py + 58, cw2, "FILE", xp != "" ? Path.GetFileName(xp) + lk : "no client settings file found", xp != "" ? 0xFFC7CBE0 : acc, fp);
        bool hasBak = File.Exists(RSet.XmlBak);
        Row(hub, cx2, py + 80, cw2, "BACKUP", hasBak ? "original file held" : "none held yet", hasBak ? 0xFFC7CBE0 : 0xFF9AA8C0, fp);
        int ncs = RSet.AppSettingsPaths().Count;
        Row(hub, cx2, py + 102, cw2, "WRITES", ncs > 0 ? ncs + " ClientAppSettings.json  \u00B7  xml in place" : "no roblox install found", ncs > 0 ? 0xFFC7CBE0 : acc, fp);
        double mf = RSet.MsgAt != 0 ? Clamp(1 - (now - RSet.MsgAt - 4200) / 900.0, 0.0, 1.0) : 1.0;
        if (mf > 0.01)
        {
            uint mc = RSet.MsgCol == C_ACC ? acc : RSet.MsgCol != 0 ? RSet.MsgCol : 0xFFC7CBE0;
            FillRR(cx2, py + 128, 3, 14, 1.5, SBrush(FA(Alpha(mc, R(150 * mf)), fp)));
            Txt(FFMElide(RSet.Msg, Fonts.fHint, cw2 - 12), cx2 + 10, py + 126, cw2 - 12, 18, Fonts.fHint, FA(Alpha(AccHi(mc, 0.2), R(225 * mf)), fp), Fmt.L);
        }
        FadeLine(px + 14, px + pw - 14, py + 152, 0x1CFFFFFF, fp);
        Txt(FFMElide("pick a value, then APPLY", HL.fXs, pw - 252), px + 16, py + 161, pw - 252, 13, HL.fXs, FA(0x8AC7CBE0, fp), Fmt.L);
        Txt(FFMElide("roblox must be closed", HL.fXs, pw - 252), px + 16, py + 174, pw - 252, 13, HL.fXs, FA(0x62C7CBE0, fp), Fmt.L);
        FFMBtn(922, px + pw - 224, py + 160, 100, 26, "ALL AUTO", acc, fp, 3);
        FFMBtn(923, px + pw - 116, py + 160, 100, 26, "OPEN FILE", acc, fp, 0);
    }
    static void Row(HubSurface hub, double x, double y, double w, string label, string value, uint col, double ff)
    {
        Txt(label, x, y, 58, 16, hub.HL.fXs, FA(0x72C7CBE0, ff), Fmt.L);
        Txt(FFMElide(value, Fonts.fHint, w - 62), x + 62, y - 1, w - 62, 18, Fonts.fHint, FA(Alpha(col, 200), ff), Fmt.L);
    }

    // ---- RSetZone ----
    public static int Zone(HubSurface hub, double ux, double uy)
    {
        var HL = hub.HL;
        double ax = HL.abx, ay = HL.aby;
        if (RSet.Dd != 0)
        {
            var o = RSet.Opts[RSet.Dd - 1];
            int n = Math.Min(o.V.Count, 7);
            double pad = DdPad(o), dx = ChipX(HL) - 4 - pad, dw = ChipW + 8 + pad;
            DdGeom(HL, RSet.Dd, out var ddy, out _);
            if (ux >= dx && ux <= dx + dw && uy >= ddy + 4 && uy <= ddy + 4 + n * 23) { int it = (int)Math.Floor((uy - ddy - 4) / 23) + 1; if (it >= 1 && it <= n) return 939 + it; }
            return 939;
        }
        if (FxUp && uy >= ay && uy <= ay + RSetFx.H(HL) && ux >= ax && ux <= ax + HL.abw && PageZone is not null) return PageZone(hub, ux, uy);
        if (ux >= ax + HL.abw - 96 && ux <= ax + HL.abw && uy >= HL.cty + 30 && uy <= HL.cty + 52) return 921;
        if (Total(HL) > HL.ffh - 20 && ux >= ax + HL.abw - 12 && ux <= ax + HL.abw && uy >= ay + 10 && uy <= ay + HL.ffh - 10) return 920;
        if (uy >= ay + 6 && uy <= ay + HL.ffh - 6 && ux >= ax && ux <= ax + HL.abw)
            for (int i = 1; i <= RSet.SysN; i++)
            {
                double ry = ay + RowY(HL, i) - RSet.Scr;
                if (uy < ry || uy > ry + HL.rsrh) continue;
                var o9 = RSet.Opts[i - 1];
                if (o9.Ui == "source")
                {
                    if (ux >= ax + SrcSwX(HL) && ux <= ax + SrcSwX(HL) + SrcSwW && uy >= ry + 8 && uy <= ry + 30) return 933;
                    for (int q2 = 1; q2 <= 2; q2++) { double sx2 = ax + SrcSgX(HL) + (q2 - 1) * SrcSgW; if (ux >= sx2 && ux <= sx2 + SrcSgW && uy >= ry + 8 && uy <= ry + 32) return 933 + q2; }
                    return EXPZ + i;
                }
                if (o9.Ui == "preset")
                {
                    double px3 = ax + PresetChipX(HL);
                    for (int q = 1; q <= 3; q++) { double bx3 = px3 + (q - 1) * 66; if (ux >= bx3 && ux <= bx3 + 60 && uy >= ry + 6 && uy <= ry + 36) return 924 + q; }
                    return EXPZ + i;
                }
                if (o9.Ui == "file")
                {
                    int kZ = o9.K == "logo" ? 1 : o9.K == "logos" ? 2 : 0;
                    if (kZ != 0)
                    {
                        if (ux >= ax + HL.abw - 144 && ux <= ax + HL.abw - 80) return 927 + kZ;
                        if (ux >= ax + HL.abw - 76 && ux <= ax + HL.abw - 16 && (kZ == 1 ? RSet.Logo : RSet.LogoS) != "") return 930 + kZ;
                    }
                    else if (ux >= ax + HL.abw - 108 && ux <= ax + HL.abw - 16) return 924;
                }
                if (RSet.RowLocked(o9) || RSet.RowMoot(o9)) return EXPZ + i;
                if (o9.Ui == "action" || (o9.Ui == "file" && o9.K == "font")) { if (ux >= ax + HL.abw - 108 && ux <= ax + HL.abw - 16 && uy >= ry && uy <= ry + 26) return ROWZ + i; }
                else if (o9.Ui == "drop") { if (ux >= ax + HL.abw - 118 && ux <= ax + HL.abw - 16 && uy >= ry && uy <= ry + 26) return ROWZ + i; }
                else if (o9.Ui == "slider")
                {
                    double tx9 = ax + HL.abw - 200, ty9 = ry + 13, kx9 = tx9 + TrackW * RSet.SlideFrac(i);
                    if ((ux - kx9) * (ux - kx9) + (uy - ty9) * (uy - ty9) <= 121) return KNOBZ + i;
                    if (ux >= tx9 - 6 && ux <= tx9 + TrackW + 6 && uy >= ty9 - 10 && uy <= ty9 + 10) return ROWZ + i;
                }
                return EXPZ + i;
            }
        double px = ax, py = HL.ffby, pw = HL.abw;
        if (ux >= px && ux <= px + pw && uy >= py && uy <= py + CH)
        {
            if (uy >= py + CH - 38 && uy <= py + CH - 12) { if (ux >= px + pw - 116 && ux <= px + pw - 16) return 923; if (ux >= px + pw - 224 && ux <= px + pw - 124) return 922; }
            return 930;
        }
        return 0;
    }
    public static void SlideSet(HubSurface hub, int i, double ux)
    {
        var HL = hub.HL;
        var o = RSet.Opts[i - 1];
        double fr = Clamp((ux - TrackX(HL)) / TrackW, 0.0, 1.0);
        int nv = 1 + (int)Math.Round(fr * (o.V.Count - 1));
        RSet.SetPick(o, nv);
        hub.Tim(Pace.TICK_A);
    }
    public static void DragScroll(HubSurface hub, double uy)
    {
        var HL = hub.HL;
        double vis = HL.ffh - 20, tot = Total(HL), thmb = Math.Max(22, vis * (vis / Math.Max(1, tot)));
        RSet.ScrT = ScrollFromY(HL, uy, HL.aby + 10, vis, tot, vis, thmb, RSet.Scr); RSet.Scr = RSet.ScrT;
        hub.Tim(Pace.TICK_A);
    }
    /// <summary>The press half of a slider click: the .ahk's RSetClick sets the capture on the button going down, so the knob follows the pointer until it lifts.</summary>
    public static bool Press(HubSurface hub, int z)
    {
        if (FxUp && z >= 1600 && z < 2000 && PagePress is not null && PagePress(hub, z)) return true;
        int i = z > KNOBZ && z <= KNOBZ + 40 ? z - KNOBZ : z > ROWZ && z <= ROWZ + 40 && RSet.Opts[z - ROWZ - 1].Ui == "slider" ? z - ROWZ : 0;
        if (i == 0) return false;
        hub.HL.drag = 15; RSet.Sld = i; hub.BeginPtrDrag();
        SlideSet(hub, i, hub.PtrX);
        return true;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (FxUp && z >= 1600 && z < 2000 && PageClick is not null && PageClick(hub, z)) { hub.Tim(Pace.TICK_A); return true; }
        if (z >= 940 && z <= 959) { RSet.DdPick(z - 939); hub.ClickAt[z] = Clock.Tick; return true; }
        if (z == 939) { RSet.DdClose(); return true; }
        switch (z)
        {
            case 921: RSet.Toggle(); break;
            case 922: RSet.Reset(); break;
            case 923: RSet.OpenXml(); break;
            case 924: if (PickFont is not null) _ = PickFont(hub); else RSet.Say("THE FONT PICKER ARRIVES WITH THE NEXT BUILD", AMBER); break;
            case 928: if (PickLogo is not null) _ = PickLogo(hub); else RSet.Say("THE LOGO PICKER ARRIVES WITH THE NEXT BUILD", AMBER); break;
            case 929: if (PickLogoS is not null) _ = PickLogoS(hub); else RSet.Say("THE LOGO PICKER ARRIVES WITH THE NEXT BUILD", AMBER); break;
            case 931 or 932: if (LogoApplyOne is not null) LogoApplyOne(z - 930); else RSet.Say("LOGO WRITING ARRIVES WITH THE NEXT BUILD", AMBER); break;
            case 933: RSet.SrcToggle(); break;
            case 934 or 935: RSet.SrcSelect(z - 933); break;
            case >= 925 and <= 927: RSet.ApplyPreset(z - 924); break;
            case > KNOBZ and <= KNOBZ + 40: break;                             // the drag began on the press - see Press
            case > ROWZ and <= ROWZ + 40:
            {
                int i = z - ROWZ;
                var o = RSet.Opts[i - 1];
                if (o.Ui == "action")
                {
                    if (o.K == "apply") RSet.Apply();
                    else if (o.K == "logorst") { if (LogoRestore is not null) LogoRestore(); else RSet.Say("LOGO RESTORE ARRIVES WITH THE NEXT BUILD", AMBER); }
                    else if (o.K == "fxedit") { if (FxOpen is not null) FxOpen(); else RSet.Say("THE FLAG EDITOR PAGE ARRIVES WITH THE NEXT BUILD", AMBER); }
                    else RSet.Revert();
                }
                else if (o.Ui == "drop") RSet.DdOpen(i);
                else if (o.Ui == "file") { if (o.K == "logo") { if (PickLogo is not null) _ = PickLogo(hub); } else if (o.K == "logos") { if (PickLogoS is not null) _ = PickLogoS(hub); } else if (PickFont is not null) _ = PickFont(hub); }
                break;
            }
            case > EXPZ and <= EXPZ + 40: RSetDetail.Open(z - EXPZ); break;
            case 920 or 930: break;
            default: return false;
        }
        if (z > 0) hub.ClickAt[z] = Clock.Tick;
        hub.Tim(Pace.TICK_A);
        return true;
    }
    public static bool Wheel(HubSurface hub, double ux, double uy, double delta)
    {
        var HL = hub.HL;
        if (FxUp && PageWheel is not null) return PageWheel(hub, ux, uy, delta);
        if (RSet.Dd != 0)
        {
            var o = RSet.Opts[RSet.Dd - 1]; int n = Math.Min(o.V.Count, 7);
            RSet.DdTop = Math.Clamp(RSet.DdTop - (int)Math.Sign(delta), 0, Math.Max(0, o.V.Count - n));
            return true;
        }
        // x as well as y. A guard on height alone takes the wheel at any
        // horizontal position on that band - over the module rail, over the
        // card's margins - and hands it to a list the pointer is nowhere near.
        if (ux < HL.abx - 8 || ux > HL.abx + HL.abw + 8 || uy < HL.aby || uy > HL.aby + HL.ffh) return false;
        RSet.ScrT = Clamp(RSet.ScrT - delta * HL.rsrg, 0.0, Math.Max(0.0, Total(HL) - (HL.ffh - 20)));
        return true;
    }
}
