using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Shell.Hub;

/// <summary>
/// The tour (Tut*): sixty steps that walk the hub control by control. Each
/// step opens the tab, module, panel, view or sheet it talks about, dims
/// everything but the control it lights (the hole), and places its card
/// beside it: kicker, title, the wrapped body, BACK / NEXT (FINISH), SKIP,
/// the progress dots, and the small light that flies from NEXT to the
/// lit control on the steps that ask for a click. A click on the lit
/// control counts as NEXT. Nothing is applied, injected or launched.
/// </summary>
public static partial class Tut
{
    const double IN_MS = 420, OUT_MS = 360, STEP_MS = 260, CUR_MS = 1100, CW = 360;
    static List<TutStep>? _steps;
    public static List<TutStep> Steps => _steps ??= Build();
    public static bool On; public static int Step, Prev, Dir = 1; public static long At, OutAt, StepAt, CurAt, ClickAt; public static bool Auto;
    static bool _hInit, _cInit, _hasHole; static double _hx, _hy, _hw, _hh, _cx, _cy, _ch, _cwCur = CW;
    public static bool Animating => On || OutAt != 0;
    public static bool Seen => Ini.ReadInt(Paths.IniFile, "hub", "tourseen", 0) != 0;
    static HubSurface? Hub => HubSurface.Live;

    // ---- the anchors' helpers (TutFFRows / CurRows / RSRows / SPFRows / DOPRows / SheetHead / DetHead) ----
    static double PanelH(HubLayout HL) => Math.Max(160, HubLayout.pd + HubLayout.ch - 34 - HL.aby);
    static double[] Band(double[] r, double top, double bot) { double y0 = Math.Max(r[1], top), y1 = Math.Min(r[1] + r[3], bot); if (y1 - y0 < 8) return new[] { r[0], y0 < bot ? y0 : bot - 8, r[2], 8 }; return new[] { r[0], y0, r[2], y1 - y0 }; }
    static double[] FFRows(HubLayout HL, int a, int b) => Band(new[] { HL.abx + 6, HL.aby + Modules.FastFlags.FfmPanel.SysRowY(HL, a) - Modules.FastFlags.FfmPanel.SysScrT - 2, HL.abw - 12, Modules.FastFlags.FfmPanel.SysRowY(HL, b) - Modules.FastFlags.FfmPanel.SysRowY(HL, a) + 40 }, HL.aby + 4, HL.aby + HL.ffh - 4);
    static double[] CurRows(HubLayout HL, int a, int b) => Band(new[] { HL.abx + 6, HL.aby + 12 + (a - 1) * HL.ffrg - Modules.Cursor.Cur.ScrT - 2, HL.abw - 12, (b - a) * HL.ffrg + 40 }, HL.aby + 4, HL.aby + HL.ffh - 4);
    static double RSetY(HubLayout HL, int i) => Modules.ClientSettings.RSet.RowY(i, HL.rsrg);
    static int RSIdx(string k) { for (int i = 0; i < Modules.ClientSettings.RSet.Opts.Count; i++) if (Modules.ClientSettings.RSet.Opts[i].K == k) return i + 1; return 1; }
    static double[] RSRows(HubLayout HL, int a, int b) => Band(new[] { HL.abx + 6, HL.aby + RSetY(HL, a) - Modules.ClientSettings.RSet.ScrT - 2, HL.abw - 12, RSetY(HL, b) - RSetY(HL, a) + HL.rsrh + 4 }, HL.aby + 4, HL.aby + HL.ffh - 4);
    static double[] SPFRows(HubLayout HL, int a, int b) => Band(new[] { HL.abx + 6, HL.aby + 12 + (a - 1) * Modules.Special.SpfPanel.spfrg - Modules.Special.Spf.SysScrT - 2, HL.abw - 12, (b - a) * Modules.Special.SpfPanel.spfrg + Modules.Special.SpfPanel.spfrh + 4 }, HL.aby + 8, HL.aby + Modules.Special.SpfPanel.spfrg * 3 + 10);
    static double[] DOPRows(HubLayout HL) => new[] { HL.abx + 6, HL.aby + 10, HL.abw - 12, Modules.DeviceOpt.DopPanel.Doph - 20 };
    static double[] SheetHead(HubLayout HL) => new[] { HL.ctx, HL.cty + 34, HL.ctw, 30 };
    static double[] DetHead(HubLayout HL) { Modules.FastFlags.Detail.Geom(out double dx, out double dy, out double dw, out _); return new[] { dx, dy, dw, 96 }; }

    static List<string> Lines(TutStep st, double w)
    {
        if (st.Wrap is not null && st.WrapW == w) return st.Wrap;
        var res = new List<string>(); string para = "";
        foreach (var ln in st.Body)
        {
            if (ln == "") { if (para != "") { res.AddRange(Modules.FastFlags.Detail.WrapText(para, Fonts.fHint, w, 40)); para = ""; } res.Add(""); continue; }
            para += (para != "" ? " " : "") + ln.Trim();
        }
        if (para != "") res.AddRange(Modules.FastFlags.Detail.WrapText(para, Fonts.fHint, w, 40));
        st.Wrap = res; st.WrapW = w;
        return res;
    }
    static Font TitleFont(HubLayout HL, string ttl, double w) => Fonts.MeasureW(ttl, HL.fT) <= w ? HL.fT : Fonts.MeasureW(ttl, Fonts.fStatus) <= w ? Fonts.fStatus : Fonts.fBadge;
    static string Kicker(TutStep st)
    {
        if (st.Kick != "") return st.Kick;
        if (st.Tab == 2) return st.Mod switch { 1 => "INTEGRATIONS  \u00B7  FAST FLAG MANAGER", 2 => "INTEGRATIONS  \u00B7  FORSAKEN", 3 => "INTEGRATIONS  \u00B7  CURSOR", 4 => "INTEGRATIONS  \u00B7  CLIENT SETTINGS", 5 => "INTEGRATIONS  \u00B7  SPECIAL FEATURES", 6 => "INTEGRATIONS  \u00B7  DEVICE OPTIMIZATIONS", 7 => "INTEGRATIONS  \u00B7  LOGIN ITEMS", _ => "INTEGRATIONS" };
        return st.Tab == 3 ? "SCRIPT HUB" : st.Tab == 4 ? "SETTINGS" : st.Tab == 5 ? "CREDITS" : st.Tab == 6 ? "UPDATE LOGS" : st.Tab == 7 ? "TOWN" : st.Tab == 1 ? (st.Anchor is not null ? "DASHBOARD" : "YURI") : "HUB";
    }

    // ---- the run ----
    public static void Start(bool auto = false)
    {
        if (On || Hub is null) return;
        On = true; Auto = auto; At = Clock.Tick; OutAt = 0; _hInit = false; _cInit = false; Step = 0; Prev = 0;
        Go(1);
    }
    public static void End()
    {
        if (!On || OutAt != 0) return;
        OutAt = Clock.Tick;
        Ini.Write(Paths.IniFile, "hub", "tourseen", 1L);
        if (EditProfile.On) EditProfile.Close();
        PutAway();
        if (Hub is { } h) { if (h.Tab != 1) h.TabSet(1); if (h.HubMod != 0) h.HubModSel(h.HubMod); h.Tim(Pace.TICK_A); }
    }
    static void PutAway()
    {
        try { Modules.FastFlags.FfmViews.CloseView(); } catch { }
        try { if (Modules.Special.Spf.View != "") Modules.Special.Spf.ViewClose(); } catch { }
        try { if (Modules.FastFlags.Detail.On) Modules.FastFlags.Detail.Close(); } catch { }
        try { Modules.ClientSettings.RSetFx.Close(); Modules.ClientSettings.RSet.DdClose(); } catch { }
        Modules.ScriptHub.Scr.LibOpen = false;
        Modules.FastFlags.FfmPanel.SysScrT = 0; Modules.Cursor.Cur.ScrT = 0; Modules.ClientSettings.RSet.ScrT = 0; Modules.Special.Spf.SysScrT = 0;
        if (Hub is { } h) h.HL.mdscrT = 0;
    }
    static void Finish() { On = false; OutAt = 0; Step = 0; }
    public static void Go(int i)
    {
        if (i < 1 || i > Steps.Count || Hub is not { } h) return;
        var st = Steps[i - 1];
        Prev = Step; Dir = i >= Step ? 1 : -1;
        Step = i; StepAt = Clock.Tick; CurAt = 0; ClickAt = 0;
        PutAway();
        if (st.Tab != h.Tab) h.TabSet(st.Tab);
        if (st.Mod >= 0 && st.Mod != h.HubMod) { if (h.HubMod != 0) h.HubModSel(h.HubMod); if (st.Mod != 0) h.HubModSel(st.Mod); }
        if (st.Prof && !EditProfile.On) { EditProfile.Open(); ProfilePlate.BioOpen = false; }
        else if (!st.Prof && EditProfile.On) EditProfile.Close();
        var HL = h.HL;
        if (st.Ffs is not null) Modules.FastFlags.FfmPanel.SysScrT = Clamp(st.Ffs(HL), 0.0, Math.Max(0.0, Modules.FastFlags.FfmPanel.SysTotal(HL) - (HL.ffh - 20)));
        if (st.Crs is not null) Modules.Cursor.Cur.ScrT = Clamp(st.Crs(HL), 0.0, Math.Max(0.0, Modules.Cursor.CurPanel.SysTotal(HL) - (HL.ffh - 20)));
        if (st.Rss is not null) Modules.ClientSettings.RSet.ScrT = Clamp(st.Rss(HL), 0.0, Math.Max(0.0, Modules.ClientSettings.RSet.Total(HL.rsrg) - (HL.ffh - 20)));
        if (st.Sps is not null) Modules.Special.Spf.SysScrT = Clamp(st.Sps(HL), 0.0, Math.Max(0.0, Modules.Special.Spf.SYSN * Modules.Special.SpfPanel.spfrg - (Modules.Special.SpfPanel.spfrg * 3 + 10)));
        if (st.Rail) HL.mdscrT = Tabs.Integrations.MdRailMax(HL);
        if (st.View != "")
        {
            if (st.Mod == 5) Modules.Special.Spf.ViewOpen(st.View);
            else { Modules.FastFlags.FfmViews.OpenView(st.View); if (st.View == "db") Modules.FastFlags.FfmField.End(false); }
        }
        st.Det?.Invoke();
        if (st.Cur) CurAt = Clock.Tick + 520;
        h.Tim(Pace.TICK_A);
    }
    public static void Next() { if (Step >= Steps.Count) End(); else Go(Step + 1); }
    public static void Back() { if (Step > 1) Go(Step - 1); }
    public static int Zone(double ux, double uy)
    {
        if (OutAt != 0 || !_cInit) return 2206;
        double cx = _cx, cy = _cy, cw = _cwCur, ch = _ch, by = cy + ch - 56;
        if (uy >= by && uy <= by + 26) { if (ux >= cx + cw - 96 && ux <= cx + cw - 14) return 2200; if (Step > 1 && ux >= cx + cw - 172 && ux <= cx + cw - 104) return 2201; }
        if (ux >= cx + cw - 82 && ux <= cx + cw - 12 && uy >= cy + 11 && uy <= cy + 33) return 2202;
        if (ux >= cx && ux <= cx + cw && uy >= cy && uy <= cy + ch) return 2203;
        if (_hasHole && ux >= _hx && ux <= _hx + _hw && uy >= _hy && uy <= _hy + _hh) return 2204;
        return 2206;
    }
    public static bool Click(HubSurface hub, int z)
    {
        if (z == 2207) { Start(false); return true; }
        if (!On) return false;
        if (z == 2200 || z == 2204) Next(); else if (z == 2201) Back(); else if (z == 2202) End();
        if (z >= 2200 && z <= 2206) { hub.ClickAt[z] = Clock.Tick; hub.Tim(Pace.TICK_A); return true; }
        return false;
    }

    // ---- the flourishes ----
    // The tour is the one part of the hub that has to hold attention while it
    // talks, and a static cutout with a breathing border does not. These are the
    // hub's own vocabulary rather than anything new: the sparks are the pill's
    // collapse sparks, the trail is the FFM glint's, and the burst is the
    // overlay card's scatter. Nothing here draws under LOW PERFORMANCE MODE,
    // which is a stop, not a slower version of itself.

    /// <summary>A light running the cutout's perimeter with a comet trail behind it, and four corner motes that breathe.</summary>
    static void Spark(double hx, double hy, double hw, double hh, long now, uint acc, double f)
    {
        if (HubState.LowPerf) return;
        uint lit = AccHi(acc, 0.5);
        double per = (hw + hh) * 2, t = DecT(now) / 2600.0 % 1.0;
        for (int i = 0; i < 16; i++)
        {
            double d = (t * per) - i * 6; if (d < 0) d += per;
            Perim(hx - 9, hy - 9, hw + 18, hh + 18, d / per, out double lx, out double ly);
            double a = Math.Pow(1 - i / 16.0, 2), rr = 2.4 * a + 0.4;
            FillEll(lx - rr, ly - rr, rr * 2, rr * 2, SBrush(FA(Alpha(i == 0 ? 0xFFFFFF : lit, R(235 * a)), f)));
        }
        for (int k = 0; k < 4; k++)                                           // corner motes, one breath each, out of phase
        {
            double br = (Math.Sin(DecT(now) * 0.0026 + k * 1.5708) + 1) / 2;
            double mx = k == 1 || k == 2 ? hx + hw + 9 : hx - 9, my = k >= 2 ? hy + hh + 9 : hy - 9;
            double rr = 1.6 + 1.4 * br;
            FillEll(mx - rr, my - rr, rr * 2, rr * 2, SBrush(FA(Alpha(lit, R(70 + 130 * br)), f)));
        }
    }
    /// <summary>A point at t along a rounded rect's perimeter, clockwise from the top-left.</summary>
    static void Perim(double x, double y, double w, double h, double t, out double px, out double py)
    {
        double per = (w + h) * 2, d = (t % 1.0 + 1.0) % 1.0 * per;
        if (d < w) { px = x + d; py = y; }
        else if (d < w + h) { px = x + w; py = y + (d - w); }
        else if (d < w * 2 + h) { px = x + w - (d - w - h); py = y + h; }
        else { px = x; py = y + h - (d - w * 2 - h); }
    }
    /// <summary>
    /// The step change, thrown from the card that is leaving toward the one
    /// arriving: a short arc of motes with a burst where they land. It reads as
    /// the tour MOVING rather than as two cards crossfading in place.
    /// </summary>
    static void Fly(double cx, double cy, double sf, long now, uint acc, double f, int dir)
    {
        if (HubState.LowPerf || sf >= 1) return;
        uint lit = AccHi(acc, 0.5);
        double e = Ease3(sf);
        for (int i = 0; i < 9; i++)
        {
            double lag = Clamp((sf - i * 0.035) / 0.5, 0.0, 1.0);
            if (lag <= 0) continue;
            double le = 1 - Math.Pow(1 - lag, 3);
            double px = cx + dir * (-52 + 104 * le);
            double py = cy - Math.Sin(3.14159 * le) * (16 + i % 3 * 7) + (i % 2 == 0 ? -3 : 3);
            double a = Math.Pow(1 - lag, 1.4), rr = (2.6 - i * 0.14) * a + 0.4;
            if (rr <= 0.4) continue;
            FillEll(px - rr, py - rr, rr * 2, rr * 2, SBrush(FA(Alpha(i < 2 ? 0xFFFFFF : lit, R(230 * a)), f)));
        }
        if (sf > 0.55)                                                        // the burst where they land
        {
            double b = (sf - 0.55) / 0.45, be = 1 - Math.Pow(1 - b, 3), rr = 6 + be * 26;
            Ell(cx + dir * 52 - rr, cy - rr, rr * 2, rr * 2, Pen(FA(Alpha(lit, R(190 * (1 - be))), f), 2.4 * (1 - be) + 0.3));
            for (int i = 0; i < 6; i++)
            {
                double ang = (i * 60 + 20 * be) * 0.0174533, dd = 4 + be * 24, sr = 2.2 * (1 - be) + 0.3;
                FillEll(cx + dir * 52 + dd * Math.Cos(ang) - sr, cy + dd * Math.Sin(ang) - sr, sr * 2, sr * 2, SBrush(FA(Alpha(acc, R(210 * (1 - be))), f)));
            }
        }
        _ = e;
    }
    static void Glyph(string kind, double cx, double cy, uint acc, double f)
    {
        uint hi = AccHi(acc, 0.45);
        var pn = Pen(FA(Alpha(hi, 230), f), 1.5); var b = SBrush(FA(Alpha(hi, 230), f));
        switch (kind)
        {
            case "nav": for (int i = 0; i < 3; i++) Line(cx - 7, cy - 6 + i * 6, cx + 7, cy - 6 + i * 6, pn); break;
            case "grip": for (int i = 0; i < 6; i++) FillEll(cx - 7 + (i % 3) * 6, cy - 4 + (i / 3) * 6, 2.6, 2.6, b); break;
            case "people": Ell(cx - 4, cy - 8, 8, 8, pn); Arc(cx - 8, cy + 1, 16, 14, 180, 180, pn); break;
            case "brand": Ell(cx - 7, cy - 7, 14, 14, pn); FillEll(cx - 2.5, cy - 2.5, 5, 5, b); break;
            case "flag": Line(cx - 6, cy - 8, cx - 6, cy + 8, pn); Line(cx - 6, cy - 8, cx + 7, cy - 4, pn); Line(cx + 7, cy - 4, cx - 6, cy + 1, pn); break;
            case "cursor": Line(cx - 5, cy - 7, cx - 5, cy + 6, pn); Line(cx - 5, cy - 7, cx + 5, cy + 2, pn); Line(cx + 5, cy + 2, cx - 1, cy + 2, pn); Line(cx - 1, cy + 2, cx - 5, cy + 6, pn); break;
            case "gear": Ell(cx - 6, cy - 6, 12, 12, pn); for (int i = 0; i < 6; i++) { double a = i * Math.PI / 3; Line(cx + Math.Cos(a) * 6, cy + Math.Sin(a) * 6, cx + Math.Cos(a) * 9, cy + Math.Sin(a) * 9, pn); } break;
            case "star": for (int i = 0; i < 5; i++) { double a = -Math.PI / 2 + i * 2 * Math.PI / 5, a2 = a + Math.PI / 5; Line(cx + Math.Cos(a) * 8, cy + Math.Sin(a) * 8, cx + Math.Cos(a2) * 3.5, cy + Math.Sin(a2) * 3.5, pn); } break;
            case "script": StrokeRR(cx - 6, cy - 8, 12, 16, 2, pn); Line(cx - 3, cy - 3, cx + 3, cy - 3, pn); Line(cx - 3, cy + 1, cx + 3, cy + 1, pn); break;
            case "chip": StrokeRR(cx - 7, cy - 7, 14, 14, 2, pn); StrokeRR(cx - 3, cy - 3, 6, 6, 1, pn); break;
            case "power": Arc(cx - 7, cy - 7, 14, 14, -60, 300, pn); Line(cx, cy - 9, cx, cy - 1, pn); break;
            case "login": StrokeRR(cx - 8, cy - 6, 16, 12, 2, pn); Line(cx - 4, cy, cx + 1, cy, pn); Line(cx + 1, cy, cx - 1, cy - 2, pn); Line(cx + 1, cy, cx - 1, cy + 2, pn); break;
            case "sliders": for (int i = 0; i < 3; i++) { Line(cx - 7, cy - 5 + i * 5, cx + 7, cy - 5 + i * 5, pn); FillEll(cx - 5 + i * 4 - 1.5, cy - 5 + i * 5 - 1.5, 3, 3, b); } break;
            case "heart": Arc(cx - 7, cy - 6, 7, 7, 180, 180, pn); Arc(cx, cy - 6, 7, 7, 180, 180, pn); Line(cx - 7, cy - 2.5, cx, cy + 6, pn); Line(cx, cy + 6, cx + 7, cy - 2.5, pn); break;
            case "book": StrokeRR(cx - 7, cy - 6, 14, 12, 1.5, pn); Line(cx, cy - 6, cx, cy + 6, pn); break;
            case "picture": StrokeRR(cx - 8, cy - 6, 16, 12, 2, pn); Line(cx - 6, cy + 4, cx - 2, cy - 1, pn); Line(cx - 2, cy - 1, cx + 1, cy + 2, pn); Line(cx + 1, cy + 2, cx + 3, cy, pn); Line(cx + 3, cy, cx + 6, cy + 4, pn); break;
            default: Ell(cx - 6, cy - 6, 12, 12, pn); break;
        }
    }
    public static void Draw(HubSurface hub, long now, uint acc)
    {
        if (!On) return;
        var HL = hub.HL;
        var st = Steps[Step - 1];
        double f;
        if (OutAt != 0) { double e = Clamp((now - OutAt) / OUT_MS, 0.0, 1.0); f = 1 - Ease3(e); if (e >= 1) { Finish(); return; } }
        else f = Ease3(Clamp((now - At) / IN_MS, 0.0, 1.0));
        double[]? a = null; try { a = st.Anchor?.Invoke(HL); } catch { a = null; }
        bool hasHole = a is not null;
        double tx = 0, ty = 0, tw = 0, th = 0;
        if (hasHole)
        {
            tx = a![0] - 8; ty = a[1] - 8; tw = a[2] + 16; th = a[3] + 16;
            if (!_hInit) { _hx = tx; _hy = ty; _hw = tw; _hh = th; _hInit = true; }
            else { double k = EK(0.2); _hx += (tx - _hx) * k; _hy += (ty - _hy) * k; _hw += (tw - _hw) * k; _hh += (th - _hh) * k; }
        }
        _hasHole = hasHole;
        double hx = _hx, hy = _hy, hw = _hw, hh = _hh;
        bool settled = !hasHole || (Math.Abs(hx - tx) < 3 && Math.Abs(hy - ty) < 3 && Math.Abs(hw - tw) < 4 && Math.Abs(hh - th) < 4);
        double px = HubLayout.pd, py = HubLayout.pd, pw = HubLayout.cw, ph = HubLayout.ch;
        var veil = new Avalonia.Media.GeometryGroup { FillRule = Avalonia.Media.FillRule.EvenOdd };
        veil.Children.Add(new Avalonia.Media.RectangleGeometry(new Avalonia.Rect(px, py, pw, ph), 20, 20));
        if (hasHole) veil.Children.Add(new Avalonia.Media.RectangleGeometry(new Avalonia.Rect(hx, hy, hw, hh), 12, 12));
        FillPath(veil, SBrushP(FA(Alpha(0x05060C, 168), f)));
        if (hasHole)
        {
            double brth = (Math.Sin(DecT(now) * 0.0035) + 1) / 2;
            StrokeRR(hx, hy, hw, hh, 12, Pen(FA(Alpha(acc, R(150 + 70 * brth)), f), 1.6));
            StrokeRR(hx - 3, hy - 3, hw + 6, hh + 6, 15, Pen(FA(Alpha(AccHi(acc, 0.4), R(60 + 40 * brth)), f), 4));
            if (!HubState.LowPerf) { var pnD = Pen(FA(Alpha(acc, 110), f), 1); PenDash(pnD, 1); StrokeRR(hx - 9, hy - 9, hw + 18, hh + 18, 18, pnD); }
            if (ClickAt != 0 && now - ClickAt < 320) { double fl = 1 - (now - ClickAt) / 320.0; StrokeRR(hx - 1, hy - 1, hw + 2, hh + 2, 13, Pen(FA(Alpha(AccHi(acc, 0.7), R(230 * fl)), f), 2.5)); }
            Spark(hx, hy, hw, hh, now, acc, f);
        }
        double cw = CW;
        string pin = st.Card;
        if (hasHole && pin == "")
        {
            bool below = ty + th + 16 + (96 + 6 * 17 + 76) <= py + ph - 12, above = ty - 16 - (96 + 6 * 17 + 76) >= py + 12, rightR = tx + tw + 16 + cw <= px + pw - 12, leftR = tx - 16 - cw >= px + 12;
            if (!below && !above && !rightR && !leftR && tx - 16 - 300 >= px + 12) cw = tx - 16 - (px + 12);
        }
        _cwCur = cw;
        var lines = Lines(st, cw - 40);
        double ch = 96 + lines.Count * 17 + 76, cxT, cyT;
        if (hasHole)
        {
            cxT = Clamp(tx + tw / 2 - cw / 2, px + 12, px + pw - cw - 12);
            if (pin == "bot") cyT = py + ph - ch - 12;
            else if (pin == "top") cyT = py + 12;
            else if (ty + th + 16 + ch <= py + ph - 12) cyT = ty + th + 16;
            else if (ty - 16 - ch >= py + 12) cyT = ty - 16 - ch;
            else if (tx + tw + 16 + cw <= px + pw - 12) { cyT = Clamp(ty + th / 2 - ch / 2, py + 12, py + ph - ch - 12); cxT = tx + tw + 16; }
            else if (tx - 16 - cw >= px + 12) { cyT = Clamp(ty + th / 2 - ch / 2, py + 12, py + ph - ch - 12); cxT = tx - 16 - cw; }
            else cyT = ty + th / 2 < py + ph / 2 ? py + ph - ch - 12 : py + 12;
        }
        else { cxT = px + pw / 2 - cw / 2; cyT = py + ph / 2 - ch / 2; }
        if (!_cInit) { _cx = cxT; _cy = cyT; _ch = ch; _cInit = true; }
        else { double k = EK(0.22); _cx += (cxT - _cx) * k; _cy += (cyT - _cy) * k; _ch += (ch - _ch) * k; }
        double cx = _cx, cy = _cy; ch = _ch;
        double sf = Ease3(Clamp((now - StepAt) / STEP_MS, 0.0, 1.0)), cf = f;
        int stC = PushXform(cx + cw / 2, cy + ch / 2, 0.97 + 0.03 * sf, 0);
        ShadowDraw(cx, cy + 3, cw, ch, 14, 6, 34, 18, cf);
        FillRR(cx, cy, cw, ch, 14, VBrush(cx, cy, cw, ch, FA(Mix(0xFF1B1F38, 0xFF232948, 0.4), cf), FA(0xFF10121E, cf)));
        MiniBackdrop(cx, cy, cw, ch, 14, acc, cf, now, 0.85);
        StrokeRR(cx, cy, cw, ch, 14, Pen(FA(Alpha(acc, 130), cf), 1.2));
        const double hbh = 44;
        int stH = PushG(); ClipRR(cx, cy, cw, ch, 14);
        FillRect(cx, cy, cw, hbh, HBrush(cx, cy, cw, hbh, FA(Alpha(acc, 46), cf), FA(Alpha(acc, 8), cf)));
        FillRect(cx, cy, cw, hbh, VBrush(cx, cy, cw, hbh, FA(Alpha(0xFFFFFF, 14), cf), FA(Alpha(0xFFFFFF, 0), cf)));
        FillRect(cx, cy + hbh - 1, cw, 1, SBrush(FA(Alpha(acc, 60), cf)));
        Pop(stH);
        double rlh = (ch - 28) * sf;
        FillRR(cx, cy + 14, 3, Math.Max(rlh, 1), 1.5, VBrush(cx, cy + 14, 3, Math.Max(rlh, 1), FA(Alpha(AccHi(acc, 0.4), 230), cf), FA(Alpha(acc, 90), cf)));
        string cnt = Step + " / " + Steps.Count; double cnw = Fonts.MeasureW(cnt, HL.fXs) + 14;
        FillRR(cx + 16, cy + 13, cnw, 18, 9, SBrush(FA(Alpha(0x000000, 70), cf)));
        StrokeRR(cx + 16, cy + 13, cnw, 18, 9, Pen(FA(Alpha(acc, 110), cf), 1));
        Txt(cnt, cx + 16, cy + 15, cnw, 14, HL.fXs, FA(Alpha(AccHi(acc, 0.4), 235), cf), Fmt.C);
        double gcx = cx + cw - 118, gcy = cy + hbh / 2;
        FillEll(gcx - 15, gcy - 15, 30, 30, SBrush(FA(Alpha(0x000000, 60), cf)));
        Ell(gcx - 15, gcy - 15, 30, 30, Pen(FA(Alpha(acc, 140), cf), 1.2));
        if (st.Glyph != "") Glyph(st.Glyph, gcx, gcy, acc, cf * sf);
        double hvS = hub.Hv(2202), skx = cx + cw - 82, sky = cy + 11, skw = 70;
        FillRR(skx, sky, skw, 22, 11, VBrush(skx, sky, skw, 22, FA(Alpha(0xFFFFFF, R(10 + 16 * hvS)), cf), FA(Alpha(0xFFFFFF, R(4 + 6 * hvS)), cf)));
        StrokeRR(skx, sky, skw, 22, 11, Pen(FA(Alpha(acc, R(70 + 120 * hvS)), cf), 1));
        TxtP("SKIP", skx + 10, sky + 4, 34, 14, HL.fXs, FA(Alpha(THMix(0xFFC7CBE0, 0xFFFFFFFF, hvS), R(180 + 75 * hvS)), cf), Fmt.L);
        var pnCh = Pen(FA(Alpha(AccHi(acc, 0.4), R(180 + 60 * hvS)), cf), 1.4);
        double chx = skx + skw - 20 + 2 * hvS, chy = sky + 11;
        for (int q = 0; q < 2; q++) { double ox = q * 5; Line(chx + ox, chy - 4, chx + ox + 4, chy, pnCh); Line(chx + ox + 4, chy, chx + ox, chy + 4, pnCh); }
        // the motes that carry the tour from one step to the next
        Fly(cx + cw / 2, cy + 62, sf, now, acc, cf, Dir);
        if (Prev >= 1 && Prev <= Steps.Count && sf < 1)
        {
            var po = Steps[Prev - 1]; double of = (1 - sf) * cf, ox = -14 * sf * Dir;
            var pl = Lines(po, cw - 40);
            Txt(Kicker(po), cx + 18 + ox, cy + hbh + 8, cw - 60, 12, HL.fXs, FA(Alpha(acc, R(150 * (1 - sf))), of), Fmt.L);
            Txt(po.Ttl, cx + 18 + ox, cy + hbh + 20, cw - 36, 24, TitleFont(HL, po.Ttl, cw - 36), FA(Alpha(Mix(0xFFE8EAF6, acc, 0.15), 245), of), Fmt.L);
            for (int i = 0; i < pl.Count; i++) if (pl[i] != "") Txt(pl[i], cx + 18 + ox, cy + 96 + i * 17, cw - 36, 17, Fonts.fHint, FA(0xD2C7CBE0, of), Fmt.L);
        }
        double ix = 14 * (1 - sf) * Dir;
        Txt(Kicker(st), cx + 18 + ix, cy + hbh + 8, cw - 60, 12, HL.fXs, FA(Alpha(acc, R(150 * sf)), cf), Fmt.L);
        Txt(st.Ttl, cx + 18 + ix, cy + hbh + 20, cw - 36, 24, TitleFont(HL, st.Ttl, cw - 36), FA(Alpha(Mix(0xFFE8EAF6, acc, 0.15), 245), cf * sf), Fmt.L);
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] == "") continue;
            double lf = Ease3(Clamp(((now - StepAt) - 60 - i * 40) / 220.0, 0.0, 1.0));
            Txt(lines[i], cx + 18 + 6 * (1 - lf), cy + 96 + i * 17, cw - 36, 17, Fonts.fHint, FA(Alpha(0xFFC7CBE0, R(215 * lf)), cf), Fmt.L);
        }
        double fty = cy + ch - 66;
        FillRect(cx + 16, fty, cw - 32, 1, HBrush(cx + 16, fty, cw - 32, 1, FA(Alpha(acc, 90), cf), FA(Alpha(acc, 0), cf)));
        double by = cy + ch - 56;
        if (hasHole) Txt(FFMElide("click the lit control, or NEXT", HL.fXs, cw - 196), cx + 18, by + 6, cw - 196, 14, HL.fXs, FA(0x6EC7CBE0, cf), Fmt.L);
        int n = Steps.Count; double dp = n * 6 + 12 <= cw - 36 ? 3 : 2, dx = cx + 18, dy = cy + ch - 18;
        for (int i = 1; i <= n; i++)
        {
            bool on = i == Step;
            double w2 = on ? dp + 9 * sf : (i == Prev && Prev != Step ? dp + 9 * (1 - sf) : dp);
            FillRR(dx, dy, w2, 3, 1.5, SBrush(FA(Alpha(on ? AccHi(acc, 0.4) : i < Step ? acc : 0xFFC7CBE0, on ? 235 : i < Step ? 150 : 60), cf)));
            dx += w2 + dp;
        }
        if (Step > 1) FFMBtn(2201, cx + cw - 172, by, 68, 26, "BACK", acc, cf, 0, HL.fS);
        FFMBtn(2200, cx + cw - 96, by, 82, 26, Step == n ? "FINISH" : "NEXT", acc, cf, 1, HL.fS);
        if (Step == 1) { double bw2 = Fonts.MeasureW(AppInfo.AppName, HL.fG); Txt(AppInfo.AppName, cx + cw - 18 - bw2, cy + ch - 104, bw2 + 4, 50, HL.fG, FA(Alpha(acc, R(22 * sf)), cf), Fmt.L); }
        Pop(stC);
        if (st.Cur && hasHole && settled && CurAt != 0 && now >= CurAt && OutAt == 0)
        {
            double tt = Clamp((now - CurAt) / CUR_MS, 0.0, 1.0), et = Ease3(tt);
            double x0 = cx + cw - 55, y0 = by + 13, x1 = tx + tw / 2, y1 = ty + th / 2;
            double mx = (x0 + x1) / 2 + (y0 - y1) * 0.25, my = (y0 + y1) / 2 + (x1 - x0) * 0.25;
            double gx = (1 - et) * (1 - et) * x0 + 2 * (1 - et) * et * mx + et * et * x1, gy = (1 - et) * (1 - et) * y0 + 2 * (1 - et) * et * my + et * et * y1;
            long arr = now - (CurAt + (long)CUR_MS);
            if (arr >= 0 && arr < 40 && ClickAt == 0) ClickAt = now;
            if (tt < 1 && !HubState.LowPerf)
                for (int q = 1; q <= 5; q++) { double be = Ease3(Clamp(tt - q * 0.06, 0.0, 1.0)); double bx2 = (1 - be) * (1 - be) * x0 + 2 * (1 - be) * be * mx + be * be * x1, by2 = (1 - be) * (1 - be) * y0 + 2 * (1 - be) * be * my + be * be * y1; FillEll(bx2 - 0.5, by2 - 0.5, 5, 5, SBrush(FA(Alpha(acc, R(150.0 / q)), cf))); }
            if (arr >= 0 && arr < 700) { double rp = arr / 700.0; Ell(gx - 6 - 26 * rp, gy - 6 - 26 * rp, 12 + 52 * rp, 12 + 52 * rp, Pen(FA(Alpha(acc, R(200 * (1 - rp))), cf), 2.4 - 1.8 * rp)); }
            if (arr >= 0) { double pls = (Math.Sin(DecT(now) * 0.006) + 1) / 2; FillEll(gx - 12 - 4 * pls, gy - 12 - 4 * pls, 24 + 8 * pls, 24 + 8 * pls, SBrush(FA(Alpha(acc, R(40 + 30 * pls)), cf))); }
            var ap = new Avalonia.Media.StreamGeometry();
            using (var c = ap.Open())
            {
                c.BeginFigure(new Avalonia.Point(gx, gy), true);
                foreach (var (px2, py2) in new[] { (gx, gy + 15), (gx + 4, gy + 11.5), (gx + 6.5, gy + 17), (gx + 9.5, gy + 15.6), (gx + 7, gy + 10.4), (gx + 11.5, gy + 10.4) }) c.LineTo(new Avalonia.Point(px2, py2));
                c.EndFigure(true);
            }
            FillPath(ap, SBrush(FA(0xFFFFFFFF, cf)));
            StrokePath(ap, Pen(FA(Alpha(0x000000, 200), cf), 1.2));
        }
    }
}
