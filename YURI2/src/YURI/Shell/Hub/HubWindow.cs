using Avalonia.Input;
using Avalonia.Media;
using Yuri.Core;
using Yuri.Gfx;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;

namespace Yuri.Shell.Hub;

/// <summary>
/// HL: the hub's layout, exactly as HubOpen() lays it out — a 780x520 card in
/// a 26 pad, the rail at sbx/sby, the content column at ctx/cty, the module
/// panel at abx/aby — and the eased UI state the frame carries between frames.
/// Every tab and module panel positions against these.
/// </summary>
public sealed class HubLayout
{
    public const double cw = 780, ch = 520, pd = 26;
    public const double bw = cw + pd * 2, bh = ch + pd * 2;
    public double k = 1.0;                                // pw/bw
    public readonly double bcx = pd + cw - 24, bmx = pd + cw - 48, bty = pd + 22;
    public readonly double brx = pd + 30;
    public readonly double sbx = pd + 18, sby = pd + 46, sbw = 140;
    public readonly double nvh = 38, nvg = 44;
    public readonly double avcx, avcy;
    public readonly double ctx, cty, ctw;
    public readonly double abx, aby, abw, abh = 82;
    public readonly double kby, aby2, abh2 = 178, kby2;
    public readonly double tgw = 46, tgh = 24, tgx, tgy, tgx2, tgy2;
    public readonly double mdx, mdy, mdw = 152, mdh = 54, m1h = 64, mdgap = 10, m2y, mdsep = 22, mdsep2 = 20;
    public readonly double m3y, m4y, m5y, m6h = 64, m6y, m7h, m7y, ffrh = 26, ffrg = 40, rsrh = 42, rsrg = 52;
    public readonly double fdy, fdh = 26, edy, edh = 122, lsy, lsh = 64;
    public readonly double mbx = pd + cw - 44, mby = pd + ch - 44;
    // the dashboard
    public readonly double stw, sty, sth = 70, gy, gh = 104, ly;
    // the settings and credits pages
    public readonly double rys, rh = 38, rgap = 6, setr, setb, sett, seth = 86, setw, setp, lky, lkh = 76, sldx, sldw = 260;
    public readonly double tbw = 54, tbh = 26, tby, tabY;
    // the fast flag panel
    public readonly double ffh, ffby, ffqy, ffly, fflh; public readonly int ffrows = 4;
    public const double HUB_FTRH = 32;
    /// <summary>HubFtrY(): the footer's top edge.</summary>
    public static double FtrY() => pd + ch - HUB_FTRH;
    public readonly Font fT, fV, fS, fG, fM, fMs, fXs;
    public readonly double chW, optx, opw, vbw, zvw, ubw;
    public readonly Font opF;
    public readonly Fonts.Scr[] scr;

    // eased UI state
    public long intro, closeAt, minStart, scrAt, accFlashAt, heartAt, cogAt;
    public double minT, minV, minFrom, minTo, minDur = 430;
    public long burstAt, scatAt;
    public double dragT, tilt, modT, profT, bioT, armT, abT, actT; public long dragAt;
    public double navY, navA = 1.0, townT, cogA = 22.5, cogV;
    // the minimised pill's gallery reel: which picture, the one crossfading in,
    // when the fade started and when the last swap was - see MiniPill
    public int mbIdx = 1, mbNext = 1; public long mbFade, mbAt;
    public int navLast = 1;
    public bool profOpen, bioOpen;
    public int drag, dragZone;                            // 0 none, 2 the grip, 3 / 4 the opacity sliders, 18 the sheet, 23-25 the community lists
    public double dragOff = -1;                           // HL.dragOff: where inside the thumb the drag took hold (ScrGrip), -1 until the first move
    // the settings tiles' eases and flashes
    public double tintT, lpT, topT, tmT, auT;
    public long tintAt, lpAt, topAt, tmAt, auAt, thFlashAt, rstAt, cpAt;
    // the update logs' scroll
    public double lgSc, lgScT, lgMax;
    // the module rail: its scroll, the selection's clock, the card being left
    public double mdscr, mdscrT;
    public long modAt; public int modOut, lastMod;

    public HubLayout()
    {
        avcx = sbx + 35; avcy = pd + ch - 44;
        ctx = sbx + sbw + 30; cty = pd + 46;
        ctw = cw - (ctx - pd) - 22;
        abx = ctx + 168; aby = cty + 56; abw = ctw - 168;
        kby = aby + abh + 14;                            // AUTOBLOCK's key
        aby2 = kby + 22 + 16;                            // PUZZLE AI panel (speed + size + grid + status + note)
        kby2 = aby2 + abh2 + 14;                         // PUZZLE AI's two keys
        tgx = abx + abw - tgw - 18; tgy = aby + 30;
        tgx2 = tgx; tgy2 = aby2 + 28;
        mdx = ctx; mdy = cty + 56;
        m2y = mdy + m1h + mdgap;
        m3y = m2y + mdh + mdgap;
        m4y = m3y + mdh + mdgap;
        m5y = m4y + mdh + mdgap + mdsep;
        m6y = m5y + mdh + mdgap + mdsep2;
        m7h = mdh; m7y = m6y + m6h + mdgap;
        fdy = cty + 100; edy = cty + 134; lsy = edy + edh + 52;
        stw = (ctw - 24) / 3; sty = cty + 40;
        gy = sty + sth + 16;
        ly = gy + gh + 20;
        rys = cty + 36;
        setr = rys + 18;
        setb = setr + 4 * (rh + rgap) + 8;
        sett = setb + 20;
        setw = (ctw - 30) / 4;
        setp = sett + seth + 6;
        lky = cty + 300;
        sldx = ctx + 118;
        tby = cty + 38; tabY = cty + 72;
        ffh = 12 + 4 * ffrg + 8;
        ffby = aby + ffh + 12;
        ffqy = ffby + 34;
        ffly = ffqy + 30;
        fflh = ffrows * ffrh + 12;
        tintT = HubState.ArmTint != 0 ? 1.0 : 0.0; lpT = HubState.LowPerf ? 1.0 : 0.0; topT = HubState.OnTop ? 1.0 : 0.0;
        tmT = HubState.TrueMin ? 1.0 : 0.0; auT = HubState.AutoUpdate ? 1.0 : 0.0;
        fT = Fonts.New(17, true); fV = Fonts.New(15, true); fS = Fonts.New(7.5, true);
        fG = Fonts.New(46, true); fM = Fonts.NewM(9.5, false); fMs = Fonts.NewM(8, false); fXs = Fonts.New(6.5, true);
        chW = Fonts.MeasureW("0000000000", fM) / 10;
        optx = avcx + 27;
        opw = (sbx + sbw - 11) - 14 - optx;
        opF = (Fonts.MeasureW("OPERATOR", fS) + 9 <= opw) ? fS : fXs;
        vbw = Math.Max(74, Math.Round(Fonts.MeasureW(AppInfo.ZVer, Fonts.fBadge)) + 18);   // fBadge badges (header, credits)
        zvw = Math.Max(52, Math.Round(Fonts.MeasureW(AppInfo.ZVer, fS)) + 14);             // the dashboard's small chip
        ubw = Math.Round(Fonts.MeasureW("CHECK FOR UPDATES", fS)) + 24;
        scr = new[] { "DASHBOARD", "INTEGRATIONS", "SCRIPT HUB", "SETTINGS", "CREDITS", "UPDATE LOGS", "TOWN" }
            .Select(s => Fonts.BuildScr(s, fT)).ToArray();
        intro = Clock.Tick; scrAt = intro;
        navY = NavY(1);
    }
    /// <summary>HubNavY(i): the rail's i-th row.</summary>
    public double NavY(int i) => sby + 6 + (i - 1) * nvg;
}

/// <summary>
/// The main hub window: HubRender() — the intro, the close fade, the
/// minimise, the drag veil, the hover ease, the timer tiers, the card's
/// plate and its chrome (header, rail, heart, footer). The seven tab bodies
/// dispatch through DrawTab in the .ahk's order — dashboard, integrations,
/// script hub, settings, credits, update logs, town — and port on top of this
/// frame with their modules; an unported tab draws nothing yet.
/// </summary>
public sealed class HubSurface : Surface
{
    const uint AMBER = 0xFFFBBF24;
    public static HubSurface? Live;                      // hubLive
    public readonly HubLayout HL = new();
    public int Tab = 1, TabPrev = 1; public long TabAt;  // hubTab / hubTabPrev / hubTabAt
    public uint AccPrev;                                 // hubAccPrev
    public int HubMod;                                   // hubMod: the open module, 0 for none
    public readonly long StatStart = Clock.Tick;         // statStart: the SESSION tile's zero
    /// <summary>FFM.clickAt: zone -> press time, for the buttons' press and ripple.</summary>
    public readonly Dictionary<int, long> ClickAt = new();
    const string SCRSET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789#$%&";
    /// <summary>The tab bodies: DrawTab(tab, f, dx, dy, now). Modules register theirs.</summary>
    public readonly Dictionary<int, Action<double, double, double, long>> TabBodies = new();
    /// <summary>The tabs' own zone tables, chained after the chrome's.</summary>
    public Func<double, double, int>? TabZone;

    public HubSurface() : base(HubLayout.bw, HubLayout.bh, 1.0)
    {
        Live = this;
        Modules.ScriptHub.ScrTab.Register(this);                 // tab 3: SCRIPT HUB
        Modules.Town.TownTab.Register(this);                     // tab 7: TOWN, behind the heart
        Tim(Pace.TICK_A);
    }

    // ---- HubZone: the chrome's zones ----
    public int ZoneAtPublic(double ux, double uy) => ZoneAt(ux, uy);
    protected override int ZoneAt(double ux, double uy)
    {
        if (HL.closeAt != 0) return 0;
        if (HL.minTo >= 1.0 || HL.minT >= 0.5)
        {
            const double MBW = 74, MBH = 96;
            if (ux >= HL.mbx - MBW / 2 - 3 && ux <= HL.mbx + MBW / 2 + 3 && uy >= HL.mby - MBH / 2 - 3 && uy <= HL.mby + MBH / 2 + 3) return 9;
            return 0;
        }
        if (Modules.FastFlags.FfmCtx.On) return Modules.FastFlags.FfmCtx.Zone(ux, uy);
        if (Modules.FastFlags.Detail.Live) return Modules.FastFlags.Detail.Zone(ux, uy);
        if (Upd.GateUp) return Upd.Zone(ux, uy);
        // The tour is MODAL. It returned 2206 for anything that was not its own
        // and let the hit test fall through, so every button, tab and row under
        // the dim stayed live - a click meant to advance the tour could open a
        // module or start a download behind it. 2206 is the scrim now: a real
        // zone that swallows the click and does nothing.
        if (Tut.On) return Tut.Zone(ux, uy);
        if (Gallery.Crop && Gallery.CropClosing) return 1223;      // the exit is playing: inert, but still the crop's own scrim
        if (Gallery.Open || Gallery.Crop) return Gallery.Zone(ux, uy);
        if (EditProfile.On) { int ze = EditProfile.Zone(ux, uy); if (ze != 0) return ze; }
        if ((ux - HL.bcx) * (ux - HL.bcx) + (uy - HL.bty) * (uy - HL.bty) <= 81) return 1;
        if ((ux - HL.bmx) * (ux - HL.bmx) + (uy - HL.bty) * (uy - HL.bty) <= 81) return 6;
        if (ux >= HL.brx + 68 && ux <= HL.brx + 104 && uy >= HubLayout.pd + 10 && uy <= HubLayout.pd + 34) return 2;
        if (ux >= HL.sbx && ux <= HL.sbx + HL.sbw)
            for (int i = 1; i <= 6; i++)
            {
                double ny = HL.NavY(i);
                if (uy >= ny && uy <= ny + HL.nvh) return 30 + i;
            }
        { int zb = ProfilePlate.BioZone(this, ux, uy); if (zb != 0) return zb; int zp = ProfilePlate.Zone(this, ux, uy); if (zp != 0) return zp; }
        double hcx0 = HL.sbx + HL.sbw / 2 + 5, hcy0 = HL.NavY(6) + HL.nvh + 53;
        if ((ux - hcx0) * (ux - hcx0) + ((uy - hcy0) * 1.1) * ((uy - hcy0) * 1.1) <= 1296) return 37;
        int z = Tab switch
        {
            1 => Tabs.Dashboard.Zone(this, ux, uy),
            2 => Tabs.Integrations.Zone(this, ux, uy),
            4 => Tabs.SettingsTab.Zone(this, ux, uy),
            5 => Tabs.Credits.Zone(this, ux, uy),
            3 => Modules.ScriptHub.ScrTab.Zone(this, ux, uy),
            7 => Modules.Town.TownTab.Zone(this, ux, uy),
            _ => TabZone?.Invoke(ux, uy) ?? 0,
        };
        if (z != 0) return z;
        if (ux >= HubLayout.pd && ux <= HubLayout.pd + HubLayout.cw && uy >= HubLayout.pd && uy <= HubLayout.pd + HubLayout.ch) return 8;
        return 0;
    }

    /// <summary>HubTim: LOW PERFORMANCE MODE's three rates and a stop - fast 40, idle 250, deep idle stops outright.</summary>
    protected override int LowPerfMap(int p) => p <= Pace.TICK_A || p == 40 ? (Tab == 7 && Modules.Town.Tw.Town == 4 && Modules.Town.Fl.on && !Modules.Town.Fl.dead ? Pace.TICK_A : 40) : p == Pace.TICK_LP ? 250 : 0;   // a game gets the real rate
    static int RSet_Sld() => Modules.ClientSettings.RSet.Sld;
    bool PressConsumed;                                               // the press did the zone's work (a field, a drag): the release is not a click
    /// <summary>A panel starting its own captured drag from a click (the .ahk's SetCapture on the hub): the surface keeps sending moves until the button lifts.</summary>
    public void BeginPtrDrag() { Dragging = true; DragMoves = false; }
    protected override void OnZoneDown(int z, PointerPressedEventArgs e)
    {
        if (z == 2) { HL.drag = 2; HL.dragAt = Clock.Tick; BeginDrag(e); return; }
        if (z == 9) { HL.drag = 1; HL.dragAt = Clock.Tick; BeginDrag(e); DragMoves = false; return; }      // the folded badge: a click expands, a move drags
        HL.dragOff = -1;
        if (z == 399) { HL.drag = 18; Modules.FastFlags.Detail.DragScroll(PtrY); return; }
        if (z == 435 || z == 436 || z == 441 || z == 442) { HL.drag = z == 435 ? 5 : z == 436 ? 6 : z == 441 ? 11 : 12; HL.dragZone = z; Modules.FastFlags.FfmPanel.DragScroll(this, PtrY); return; }
        if (z == 971 && Tab == 2 && HubMod == 5) { HL.drag = 16; Modules.Special.SpfPanel.DragScroll(this, PtrY); return; }
        if (z == 1152) { HL.drag = 13; Modules.Cursor.CurPanel.DragScroll(this, PtrY); return; }
        DragSnapDrop();                                   // a new press: whatever was cached is a frame old
        if (z == 1150) { HL.drag = 26; Tabs.Integrations.DragScroll(this, PtrY); return; }
        if (z == 920 && Tab == 2 && HubMod == 4) { HL.drag = 14; Modules.ClientSettings.RSetPanel.DragScroll(this, PtrY); return; }
        if (Tab == 2 && HubMod == 1 && Modules.FastFlags.FfmPanel.Press(this, z, PtrY)) { PressConsumed = true; return; }
        if (Tab == 2 && HubMod == 4 && Modules.ClientSettings.RSetPanel.Press(this, z)) { PressConsumed = true; return; }
        if (Tab == 2 && HubMod == 5 && Modules.Special.SpfPanel.Press(this, z)) { PressConsumed = true; return; }
        if (Tab == 2 && HubMod == 2 && Modules.Forsaken.FskPanel.Press(this, z)) { PressConsumed = true; return; }
        if (Tab == 3 && Modules.ScriptHub.ScrTab.Press(this, z)) { PressConsumed = true; return; }
        if (Gallery.Press(this, z)) { PressConsumed = true; return; }
        if (EditProfile.Press(this, z)) { PressConsumed = true; return; }
        if (Tab == 4 && Tabs.SettingsTab.Press(this, z, PtrX)) return;
    }
    protected override void OnPointerMove(double ux, double uy)
    {
        if (HL.drag == 1 && Dragging && (ux - DragGripX) * (ux - DragGripX) + (uy - DragGripY) * (uy - DragGripY) > 16) { HL.drag = 2; DragMoves = true; }
        if (HL.drag == 3 || HL.drag == 4) Tabs.SettingsTab.SlideSet(this, ux, HL.drag == 3 ? 1 : 2);
        else if (HL.drag == 18) Modules.FastFlags.Detail.DragScroll(uy);
        else if (HL.drag == 5 || HL.drag == 6 || HL.drag == 11 || HL.drag == 12) Modules.FastFlags.FfmPanel.DragScroll(this, uy);
        else if (HL.drag == 16) Modules.Special.SpfPanel.DragScroll(this, uy);
        else if (HL.drag == 13) Modules.Cursor.CurPanel.DragScroll(this, uy);
        else if (HL.drag == 26) Tabs.Integrations.DragScroll(this, uy);
        else if (HL.drag == 14) Modules.ClientSettings.RSetPanel.DragScroll(this, uy);
        else if (HL.drag == 15) Modules.ClientSettings.RSetPanel.SlideSet(this, RSet_Sld(), ux);
        else if (HL.drag == 21 || HL.drag == 22) Modules.ClientSettings.RSetFx.Drag(this, uy);
        else if (HL.drag == 19) Modules.Forsaken.FskPanel.SlideSet(this, ux);
        else if (HL.drag == 7) Modules.ScriptHub.ScrTab.ListDrag(this, uy);
        else if (HL.drag == 10) Modules.ScriptHub.ScrTab.EdVDrag(this, uy);
        else if (HL.drag == 9) Modules.ScriptHub.ScrTab.EdHDrag(this, ux);
        else if (Pressed && Modules.ScriptHub.Scr.MSel) Modules.ScriptHub.ScrTab.Drag(this);
        else if (Pressed && Gallery.Crop) Gallery.Drag(this);
        else if (HL.drag >= 23 && HL.drag <= 25) Modules.FastFlags.FfmPanel.Drag(this, uy);
        else if (Pressed && Modules.FastFlags.FfmField.MSel) Modules.FastFlags.FfmField.Drag(ux);
    }
    /// <summary>HubRClick: the right button over a text field (or one already live) opens the Cut / Copy / Paste menu.</summary>
    protected override void OnZoneRightDown(int z, PointerPressedEventArgs e)
    {
        bool isFld = z is 422 or 437 or 66 or 67 or 240 or 241 or 136 or 137 or 242 || (z >= 541 && z <= 545) || Modules.ClientSettings.RSetFx.IsFldZone(z);
        bool live = Modules.FastFlags.FfmField.Edit != "" || Modules.ScriptHub.Scr.Focus == 1;
        if (!isFld && !live) { if (Modules.FastFlags.FfmCtx.On) Modules.FastFlags.FfmCtx.Close(this); return; }
        if (isFld && !live) { OnZoneDown(z, e); HL.drag = 0; Pressed = false; PressConsumed = false; Modules.FastFlags.FfmField.MouseUp(); Modules.ScriptHub.ScrTab.MouseUp(); }
        Modules.FastFlags.FfmCtx.Open(this, PtrX, PtrY);
        e.Handled = true;
    }
    protected override void OnZoneUp(int z, PointerReleasedEventArgs e)
    {
        bool wasSlide = HL.drag == 3 || HL.drag == 4;
        bool wasClick = HL.drag == 1;
        bool wasScroll = HL.drag is 5 or 6 or 7 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 18 or 19 or 21 or 22 or 23 or 24 or 25 or 26;
        HL.drag = 0; HL.dragOff = -1;
        bool consumed = PressConsumed; PressConsumed = false;
        if (wasClick && z == 9) { HubMin(0); return; }
        Modules.FastFlags.FfmField.MouseUp(); Modules.ScriptHub.ScrTab.MouseUp(); Gallery.MouseUp();
        if (wasSlide) { Tabs.SettingsTab.SlideDone(); return; }
        if (wasScroll || consumed) return;                                 // a scrollbar or slider drag, or a field taking the caret, is not a click on what is under the pointer
        if (z != PressZone) return;
        if (z == 396) { Modules.FastFlags.Detail.Close(); return; }
        if (z == 397 || z == 398 || z == 399) return;                    // the sheet and what is under it: inert
        // a click off the module's panel commits or cancels a live field (HubClick's FFMBlur)
        if (Modules.FastFlags.FfmField.Edit != "" && !Modules.FastFlags.FfmField.KeepEdit(z, Tab, HubMod)) Modules.FastFlags.FfmField.Blur();
        if (Modules.ScriptHub.Scr.Focus == 1 && z is not (60 or 242)) Modules.ScriptHub.Scr.Blur();
        if (Modules.FastFlags.FfmCtx.Live && Modules.FastFlags.FfmCtx.Click(this, z)) return;
        if (Tut.On && Tut.Click(this, z)) return;
        if (Gallery.Click(this, z)) return;
        if (EditProfile.Click(this, z)) return;
        if (ProfilePlate.Click(this, z)) return;
        if (Upd.Click(this, z)) return;
        if (z == 1) HubClose();
        else if (z == 6) HubMinPress();
        else if (z >= 31 && z <= 36) TabSet(z - 30);
        else if (z == 37) { HL.heartAt = Clock.Tick; TabSet(Tab == 7 ? 1 : 7); }
        else if (Tab == 1 && Tabs.Dashboard.Click(this, z)) { }
        else if (Tab == 2 && Tabs.Integrations.Click(this, z)) { }
        else if (Tab == 4 && Tabs.SettingsTab.Click(this, z)) { }
        else if (Tab == 5 && Tabs.Credits.Click(this, z)) { }
        else if (Tab == 3 && Modules.ScriptHub.ScrTab.Click(this, z)) { }
        else if (Tab == 7 && Modules.Town.TownTab.Click(this, z)) { }
    }
    protected override void OnZoneWheel(int z, double delta, PointerWheelEventArgs e)
    {
        if (Tut.On) return;                                                // modal: nothing behind the tour scrolls either
        if (Gallery.Crop) { if (Gallery.Wheel(delta)) Tim(Pace.TICK_A); return; }
        if (Modules.FastFlags.Detail.On) { if (Modules.FastFlags.Detail.WheelNotches(delta)) Tim(Pace.TICK_A); return; }
        if (Tab == 6 && Tabs.UpdateLogs.Wheel(this, PtrX, PtrY, delta)) Tim(Pace.TICK_A);
        else if (Tab == 2 && Tabs.Integrations.Wheel(this, PtrX, PtrY, delta)) Tim(Pace.TICK_A);
        else if (Tab == 3 && Modules.ScriptHub.ScrTab.Wheel(this, PtrX, PtrY, delta)) Tim(Pace.TICK_A);
    }
    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (Modules.ScriptHub.Scr.Focus == 1) { Modules.ScriptHub.Scr.Char(e.Text ?? ""); e.Handled = true; return; }
        if (Modules.FastFlags.FfmField.Edit == "") return;
        Modules.FastFlags.FfmField.Char(e.Text ?? "");
        e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Tut.On)                                                        // the tour owns the keyboard: ESC leaves it, the arrows walk it
        {
            if (e.Key == Key.Escape) Tut.End();
            else if (e.Key is Key.Right or Key.Down or Key.Space or Key.Enter) Tut.Next();
            else if (e.Key is Key.Left or Key.Up) Tut.Back();
            e.Handled = true; Tim(Pace.TICK_A); return;
        }
        if (Modules.ScriptHub.Scr.Focus == 1)
        {
            var top = Avalonia.Controls.TopLevel.GetTopLevel(this);
            if (Modules.ScriptHub.Scr.HandleKey(e, async () => top?.Clipboard is { } cb ? await cb.GetTextAsync() : null, t => { try { top?.Clipboard?.SetTextAsync(t); } catch { } })) { e.Handled = true; return; }
        }
        if (Modules.FastFlags.FfmField.HandleKey(e, Avalonia.Controls.TopLevel.GetTopLevel(this))) e.Handled = true;
    }
    protected override void OnDragCancel()
    {
        bool wasSlide = HL.drag == 3 || HL.drag == 4;
        HL.drag = 0;
        if (wasSlide) Tabs.SettingsTab.SlideDone();
    }
    protected override void Poke() { if (Period != Pace.TICK_A && !Hidden) Tim(Pace.TICK_A); }
    /// <summary>
    /// The foreground went to another application. A caret still blinking in a
    /// field there is a lie - the keys are going somewhere else - so the edit is
    /// committed and the field and the editor let go of the keyboard.
    /// </summary>
    protected override void OnDeactivated()
    {
        Modules.FastFlags.FfmField.Blur();
        Modules.ScriptHub.Scr.Blur();
        Tim(Pace.TICK_S);
    }

    /// <summary>HubTabSet(t).</summary>
    public void TabSet(int t)
    {
        if (t == Tab) return;
        long now = Clock.Tick;
        if (!(TabAt != 0 && now - TabAt < 280 && Ease3((now - TabAt) / 280.0) < 0.5)) TabPrev = Tab;
        Tab = t; TabAt = now;
        HL.scrAt = now;
        Poke();
    }
    /// <summary>HubModSel(i): open a module's panel, or close it when it is the one open.</summary>
    public void HubModSel(int i)
    {
        int prev = HubMod;
        HubMod = HubMod == i ? 0 : i;
        if (HubMod != 0) HL.lastMod = HubMod;
        HL.modAt = Clock.Tick;
        HL.modOut = (prev != 0 && prev != HubMod) ? prev : 0;
        Poke();
    }

    /// <summary>HubClose(): start the fade; HubFinish ends the process.</summary>
    public void HubClose()
    {
        if (HL.closeAt != 0) return;
        HL.closeAt = Clock.Tick;
        Poke();
    }
    /// <summary>HubMin(target): fold the card into the corner badge (1) or back out (0).</summary>
    public void HubMin(int target)
    {
        HL.minFrom = HL.minT; HL.minTo = target; HL.minStart = Clock.Tick;
        HL.minDur = target != 0 ? 430 : 480;
        HL.burstAt = target != 0 ? HL.minStart + (long)Math.Round(HL.minDur * 0.72) : 0;
        HL.scatAt = target != 0 ? 0 : HL.minStart;
        Poke();
    }
    /// <summary>
    /// HubMinPress: one control with two meanings, picked by the setting. Off,
    /// the card folds into the pill; on, TRUE MINIMISE hides the window outright
    /// and the tray icon is the only way back.
    /// </summary>
    public void HubMinPress()
    {
        if (HubState.TrueMin && Tray.Available && HL.minT < 0.5) { HubHide(); return; }
        HubMin(HL.minT < 0.5 ? 1 : 0);
    }
    /// <summary>
    /// HubHide: the pill is a state the hub is IN; this is the hub not being
    /// there. Any pill state is dropped rather than preserved - coming back from
    /// the tray should give you the hub, not the hub caught mid-collapse.
    /// </summary>
    public void HubHide()
    {
        if (Hidden) return;
        HL.minT = 0; HL.minV = 0; HL.minStart = 0; HL.minFrom = 0; HL.minTo = 0;
        HL.burstAt = 0; HL.scatAt = 0;
        Hidden = true; Tim(0);
        if (Win is { } w) w.Hide();
        Tray.Tip(AppInfo.AppName + " - hidden (click to open)");
    }
    /// <summary>HubShow: the intro is replayed deliberately - a window simply reappearing at full opacity reads as a glitch next to every other transition.</summary>
    public void HubShow()
    {
        if (!Hidden) return;
        Hidden = false;
        HL.intro = Clock.Tick; HL.scrAt = HL.intro;
        if (Win is { } w) { w.Show(); w.Activate(); }
        Tray.Tip(AppInfo.AppName);
        Tim(Pace.TICK_A);
    }
    /// <summary>HubAccentSet(i): one of the six ACCENTS, with the flash the header shows.</summary>
    public void AccentSet(int i)
    {
        uint c = HubState.Accents[i - 1];
        if (HubState.Accent == c) return;
        AccPrev = HubState.Accent; HubState.Accent = c;
        Ini.WriteArgb(Paths.IniFile, "hub", "accent", c);
        Tray.Apply();                                     // TrayIcoApply(true): the mark IS the accent
        LayeredWindow.IconRefresh();                      // ... and so is the taskbar's
        HL.accFlashAt = Clock.Tick;
        Poke();
    }

    protected override void Frame(long now)
    {
        var HL = this.HL;
        HubState.ThemeTick();
        double it = HubState.LowPerf ? 1.0 : Math.Min((now - HL.intro) / (double)Pace.INTRO_MS, 1.0);
        double ei = 1 - Math.Pow(1 - it, 3);
        int winA = R(255 * Math.Min(it * 1.7, 1.0));
        double yO = R((1 - ei) * 30);
        double scl = 1.0;
        bool fin = false;
        if (HL.closeAt != 0)
        {
            double ft = Math.Min((now - HL.closeAt) / 380.0, 1.0);
            winA = R(winA * (1 - ft)); scl = 1 - 0.06 * ft;
            yO += R(10 * ft);
            if (ft >= 1) fin = true;
        }
        scl *= 1 + 0.02 * HL.dragT;
        if (HL.minStart != 0)
        {
            double t = Math.Min((now - HL.minStart) / HL.minDur, 1.0);
            HL.minT = Lerp(HL.minFrom, HL.minTo, t * t * (3 - 2 * t));
            double ev = HL.minTo == 1.0 ? MinIn(t) : EBackOut(t, 1.15);
            HL.minV = Lerp(HL.minFrom, HL.minTo, ev);
            if (t >= 1) { HL.minStart = 0; HL.minT = HL.minTo; HL.minV = HL.minTo; }
        }
        else HL.minV = HL.minT;
        double cf = Clamp(1 - Math.Max(0.0, HL.minV - 0.40) / 0.50, 0.0, 1.0);
        double dtgt = (!HubState.LowPerf && (HL.drag == 2 || (HL.drag == 1 && now - HL.dragAt > 130))) ? 1.0 : 0.0;
        HL.dragT += (dtgt - HL.dragT) * EK(0.2);
        if (HL.dragT < 0.004 && HL.drag != 2) HL.dragT = 0.0;
        double velX = DragVelX();
        HL.tilt += (((HL.drag == 2 && !HubState.LowPerf) ? Clamp(velX * 0.22, -3.0, 3.0) : 0.0) - HL.tilt) * EK(0.18);
        if (HL.drag == 0 && Math.Abs(HL.tilt) < 0.03) HL.tilt = 0.0;
        int hz = (HL.drag != 0 || HL.closeAt != 0) ? 0 : ZoneCursor();
        double hmax = HoverTick(hz);
        HL.profT += ((HL.profOpen ? 1.0 : 0.0) - HL.profT) * EK(0.18);
        if (!HL.profOpen && HL.profT < 0.004) HL.profT = 0.0;
        HL.bioT += ((HL.bioOpen ? 1.0 : 0.0) - HL.bioT) * EK(0.2);
        if (!HL.bioOpen && HL.bioT < 0.004) HL.bioT = 0.0;
        if (Tab <= 6) HL.navLast = Tab;
        double nvTgt = HL.NavY(HL.navLast);
        HL.navY += (nvTgt - HL.navY) * EK(0.22);
        HL.navA += ((Tab == 7 ? 0.0 : 1.0) - HL.navA) * EK(0.2);
        HL.townT += ((Tab == 7 ? 1.0 : 0.0) - HL.townT) * EK(0.16);
        HL.tintT += ((HubState.ArmTint != 0 ? 1.0 : 0.0) - HL.tintT) * EK(0.2);
        HL.lpT += ((HubState.LowPerf ? 1.0 : 0.0) - HL.lpT) * EK(0.2);
        HL.topT += ((HubState.OnTop ? 1.0 : 0.0) - HL.topT) * EK(0.2);
        HL.tmT += ((HubState.TrueMin ? 1.0 : 0.0) - HL.tmT) * EK(0.2);
        HL.auT += ((HubState.AutoUpdate ? 1.0 : 0.0) - HL.auT) * EK(0.2);
        // hub-wide status tint. AMBER means "something is armed", so it follows
        // every module, not just autoblock; C_ON means autoblock is firing.
        HL.actT += (((Tabs.Dashboard.BlockActive() ? 1.0 : 0.0) - HL.actT)) * EK(0.18);
        bool tinted = Tabs.Dashboard.SysArmedN() > 0 && HubState.ArmTint != 0;
        HL.armT += ((tinted ? 1.0 : 0.0) - HL.armT) * EK(0.16);
        if (!tinted && HL.armT < 0.004) HL.armT = 0.0;
        else if (tinted && HL.armT > 0.996) HL.armT = 1.0;
        HL.abT += (((Tabs.Dashboard.BlockArmed() ? 1.0 : 0.0) - HL.abT)) * EK(0.18);
        HL.lgSc += (HL.lgScT - HL.lgSc) * EK(0.3);
        if (Math.Abs(HL.lgSc - HL.lgScT) < 0.3) HL.lgSc = HL.lgScT;
        Modules.FastFlags.Detail.Tick();
        HL.modT += ((HubMod != 0 ? 1.0 : 0.0) - HL.modT) * EK(0.17);
        if (HubMod == 0 && HL.modT < 0.003) HL.modT = 0.0;
        else if (HubMod != 0 && HL.modT > 0.997) HL.modT = 1.0;
        uint hubCur = HubState.Accent;
        if (HL.accFlashAt != 0 && now - HL.accFlashAt < 420)
            hubCur = AccSat(Mix(AccPrev, HubState.Accent, Ease3((now - HL.accFlashAt) / 420.0)));
        HubState.Cur = hubCur;
        double embG = (Math.Sin(DecT(now) * 0.0016) + 1) / 2;
        double emb2G = (Math.Sin(DecT(now) * 0.004) + 1) / 2;
        double hb = DecT(now) % 2200;
        int blink = (hb < 140 || (hb > 300 && hb < 440)) ? 1 : 0;
        int fTab = Tab, fPrev = TabPrev; long fAt = TabAt;
        double tt = fAt != 0 ? Ease3((now - fAt) / 280.0) : 1.0;
        long ims = now - HL.intro;
        bool stg = ims < 700 && !HubState.LowPerf;
        double s1 = (stg ? Ease3((ims - 60) / 340.0) : 1.0) * cf;
        double s2 = (stg ? Ease3((ims - 140) / 340.0) : 1.0) * cf;
        double s3 = (stg ? Ease3((ims - 220) / 340.0) : 1.0) * cf;
        double hdy = -(1 - s1) * 6;
        double sdx = -(1 - s2) * 14;
        double cdy = (1 - s3) * 10;
        double cx = HubLayout.pd, cy = HubLayout.pd, cw = HubLayout.cw, ch = HubLayout.ch;

        int win = PushOpacity(winA / 255.0);
        PushShift(0, yO);
        double oAX = HL.minT < 0.5 ? cx + cw / 2 : HL.mbx;
        double oAY = HL.minT < 0.5 ? cy + ch / 2 : HL.mby;
        // Capturing: the card sitting still, which is what the drag then moves.
        if (_snapping) { scl = 1.0; HL.tilt = 0; }
        int st0 = PushXform(oAX, oAY, scl, HL.tilt);
        double mvV = HL.minV;
        double mvC = Clamp(mvV, 0.0, 1.0);
        if (HL.minStart != 0 && mvC > 0.02 && mvC < 0.99)
        {
            for (int gk = 1; gk <= 3; gk++)                                       // the fold's ghosts
            {
                double lag = Clamp(mvV + (HL.minTo == 1 ? -0.085 : 0.085) * gk, 0.0, 1.0);
                double gA = (0.42 / gk) * Math.Sin(3.14159 * mvC);
                if (gA < 0.01) continue;
                int stG = PushXform(HL.mbx, HL.mby, 1 - 0.76 * lag, 15 * lag);
                FillRR(cx, cy, cw, ch, 20, SBrush(Alpha(hubCur, R(30 * gA))));
                StrokeRR(cx, cy, cw, ch, 20, Pen(Alpha(hubCur, R(120 * gA)), 1.3));
                Pop(stG);
            }
        }
        if (cf > 0.004)
        {
            int stM = PushXform(HL.mbx, HL.mby, 1 - 0.76 * mvV, 15 * mvV);
            // ---- the plate ----
            ShadowDraw(cx, cy + 3 + 2.5 * HL.dragT, cw, ch, 20, 10, 8, 8, cf);
            FillRR(cx, cy, cw, ch, 20, BgV(cx, cy, cw, ch, FA(0xF2222438, cf), FA(0xF8121423, cf)));
            int clipP = PushG();
            ClipRR(cx, cy, cw, ch, 20);
            FillRect(cx, cy, cw, ch, LineBrush(cx - 1, cy - 1, cw + 2, ch + 2, FA(TH(0x0EFFFFFF), cf), TH(0x0EFFFFFF) & 0xFFFFFF, 2));
            FillRect(cx, cy, cw, 64, VBrush(cx, cy, cw, 64, FA(0x14FFFFFF, cf), Alpha(0xFFFFFF, 0)));
            FillRect(cx, cy + ch - 46, cw, 46, VBrush(cx, cy + ch - 46, cw, 46, Alpha(0x000000, 0), FA(0x2E000000, cf)));
            PanelBackdrop(cx, cy, cw, ch, hubCur, cf, now, 1.0);
            if (!HubState.LowPerf)
            {
                for (int k = 1; k <= 12; k++)                                     // the motes
                {
                    int cyc = 5200 + k * 337;
                    double prt = ((now + k * 997) % cyc) / (double)cyc;
                    double mx_ = cx + 34 + (k * 167) % (cw - 68) + 9 * Math.Sin(DecT(now) * 0.0009 + k * 1.7);
                    double my_ = cy + ch - 10 - prt * (ch - 20);
                    double mr_ = 0.9 + (k % 3) * 0.5;
                    FillEll(mx_ - mr_, my_ - mr_, mr_ * 2, mr_ * 2, SBrush(FA(Alpha(AccHi(hubCur, 0.35), 44 * Math.Sin(3.14159 * prt)), s1)));
                }
                double swp = (DecT(now) % 4600) / 4600.0;                         // the travelling sheen
                double swx = cx - 26 + (cw + 52) * swp;
                uint c0s = hubCur & 0xFFFFFF;
                uint c1s = FA(Alpha(c0s, 16 + 20 * HL.armT), cf);
                FillRect(swx - 24, cy, 24, ch, LineBrush(swx - 25, cy - 2, 26, ch + 4, c0s, c1s, 0));
                FillRect(swx, cy, 24, ch, LineBrush(swx - 1, cy - 2, 26, ch + 4, c1s, c0s, 0));
            }
            ThemeSweep(cx, cy, cw, ch, hubCur, cf, now);
            FillRect(cx, cy, 14, ch, SBrush(FA(Alpha(hubCur, Math.Min(255, 46 + 16 * embG + 50 * HL.armT)), cf)));   // the accent edge
            FillRect(cx, cy, 4.5, ch, VBrush(cx, cy, 4.5, ch, FA(Alpha(hubCur, 245), cf), FA(Alpha(hubCur, 110 + 40 * embG), cf)));
            for (int k = 1; k <= (HubState.LowPerf ? 0 : 26); k++)              // the edge's ticks
            {
                double ty_ = cy + 14 + (k - 1) * ((ch - 28) / 25);
                double tw_ = k % 5 == 1 ? 9 : 5;
                FillRect(cx + 14, ty_, tw_, 1, SBrush(FA(Alpha(hubCur, k % 5 == 1 ? 60 : 30), cf)));
            }
            Pop(clipP);
            StrokeRR(cx, cy, cw, ch, 20, Pen(FA(0x28FFFFFF, cf), 1));
            StrokeRR(cx + 1.5, cy + 1.5, cw - 3, ch - 3, 18.5, Pen(FA(0x0DFFFFFF, cf), 1));
            Line(cx + 20, cy + 1.5, cx + cw - 20, cy + 1.5, Pen(FA(0x16FFFFFF, cf), 1));
            Line(cx + 18, cy + ch - 1.5, cx + cw - 18, cy + ch - 1.5, Pen(FA(0x30000000, cf), 1));
            var pnD = Pen(FA(Alpha(hubCur, 20 + 10 * emb2G + 46 * HL.dragT), cf), 1);
            PenDash(pnD, 1);
            PenDashOff(pnD, (DecT(now) * 0.02) % 1000);
            StrokeRR(cx + 6, cy + 6, cw - 12, ch - 12, 15, pnD);
            for (int k = 0; k < (HubState.LowPerf ? 0 : 4); k++)                 // the corner arcs
            {
                double shim = (Math.Sin(DecT(now) * 0.0032 + k * 1.5708) + 1) / 2;
                double ga = Math.Min(255, 70 + 60 * shim + 50 * HL.armT);
                double xk = (k == 1 || k == 2) ? cx + cw - 31.5 : cx + 0.5;
                double yk = (k >= 2) ? cy + ch - 31.5 : cy + 0.5;
                double ang = k == 0 ? 195 : k == 1 ? 285 : k == 2 ? 15 : 105;
                Arc(xk, yk, 31, 31, ang, 60, Pen(FA(Alpha(hubCur, ga), s1), 2));
            }
            for (int k = 0; k < 2; k++)                                          // the top and bottom markers
            {
                double dy3 = k != 0 ? cy + ch - 8 : cy + 8;
                double tw3 = (Math.Sin(DecT(now) * 0.0024 + k * 2.2) + 1) / 2;
                int stD = PushXform(cx + cw / 2, dy3, 1, 45);
                FillRR(cx + cw / 2 - 2.6, dy3 - 2.6, 5.2, 5.2, 1, SBrush(FA(Alpha(hubCur, 40 + 90 * tw3), s1)));
                Pop(stD);
                var pnM = Pen(FA(Alpha(hubCur, 26), s1), 1);
                Line(cx + cw / 2 - 64, dy3, cx + cw / 2 - 12, dy3, pnM);
                Line(cx + cw / 2 + 12, dy3, cx + cw / 2 + 64, dy3, pnM);
            }
            // ---- the header ----
            double h6 = Hv(6), em = 1 + 0.28 * h6, ymb = HL.bty + hdy - 1.2 * h6, bmx_ = HL.bmx - 3 * h6;
            FillEll(bmx_ - 7 * em, ymb - 7 * em, 14 * em, 14 * em, SBrush(FA(Alpha(0xFFFFFF, 22 + 34 * h6), s1)));
            Ell(bmx_ - 7 * em, ymb - 7 * em, 14 * em, 14 * em, Pen(FA(Alpha(0xFFFFFF, 42 + 60 * h6), s1), 1));
            Line(bmx_ - 3.5 * em, ymb, bmx_ + 3.5 * em, ymb, Pen(FA(Alpha(0xE8EAF6, 168 + 87 * h6), s1), 1.5));
            double h1 = Hv(1), ec = 1 + 0.28 * h1, ycb = HL.bty + hdy - 1.2 * h1, bcx_ = HL.bcx + 3 * h1;
            FillEll(bcx_ - 7 * ec, ycb - 7 * ec, 14 * ec, 14 * ec, SBrush(FA(Alpha(HubState.Accent, 36 + 60 * h1), s1)));
            Ell(bcx_ - 7 * ec, ycb - 7 * ec, 14 * ec, 14 * ec, Pen(FA(Alpha(HubState.Accent, 100 + 80 * h1), s1), 1));
            var pnX = Pen(FA(Alpha(AccHi(HubState.Accent, 0.4), 210 + 45 * h1), s1), 1.5);
            Line(bcx_ - 3.2 * ec, ycb - 3.2 * ec, bcx_ + 3.2 * ec, ycb + 3.2 * ec, pnX);
            Line(bcx_ - 3.2 * ec, ycb + 3.2 * ec, bcx_ + 3.2 * ec, ycb - 3.2 * ec, pnX);
            Txt(AppInfo.AppName, HL.brx, cy + 8 + hdy, 90, 20, Fonts.fBrand, FA(0xCFE8EAF6, s1), Fmt.L);
            Txt("CONTROL SUITE", HL.brx + 1, cy + 25 + hdy, 110, 10, HL.fS, FA(Alpha(hubCur, 160 + 40 * embG), s1), Fmt.L);
            FillEll(HL.brx + Fonts.wZeal + 6, cy + 15.5 + hdy, 5, 5, SBrush(FA(Alpha(hubCur, Math.Min(255, 150 + 35 * emb2G + 48 * blink)), s1)));
            double hnA = Math.Max(Hv(2), HL.dragT);
            GripDots(HL.brx + 86, cy + 22 + hdy, hnA, hubCur, now, s1, 36);
            int sbn = 1 + (int)((now / 520) % 4);
            for (int k = 1; k <= 4; k++)                                          // the signal bars
            {
                double bh_ = 3 + k * 2.4;
                bool on_ = k <= sbn;
                FillRR(HL.brx + 112 + (k - 1) * 6, cy + 26 + hdy - bh_, 3, bh_, 1.2, SBrush(FA(Alpha(on_ ? hubCur : 0xFFFFFF, on_ ? 200 : 42), s1)));
            }
            double rlx = HL.brx + 146, rlw = 176;                                  // the ruler
            Line(rlx, cy + 22 + hdy, rlx + rlw, cy + 22 + hdy, Pen(FA(0x1AFFFFFF, s1), 1));
            for (int k = 1; k <= (HubState.LowPerf ? 0 : 12); k++)
            {
                double tkx = rlx + (k - 1) * (rlw / 11);
                double th_ = k % 4 == 1 ? 6 : 3;
                Line(tkx, cy + 22 + hdy - th_ / 2, tkx, cy + 22 + hdy + th_ / 2, Pen(FA(Alpha(0xFFFFFF, k % 4 == 1 ? 60 : 30), s1), 1));
            }
            double rpp = (DecT(now) % 3400) / 3400.0;
            FillEll(rlx + rpp * rlw - 4, cy + 18 + hdy, 8, 8, SBrush(FA(Alpha(hubCur, 60), s1)));
            FillEll(rlx + rpp * rlw - 1.8, cy + 20.2 + hdy, 3.6, 3.6, SBrush(FA(Alpha(AccHi(hubCur, 0.45), 225), s1)));
            int stH = PushXform(rlx + rlw + 18, cy + 22 + hdy, 1, (DecT(now) * 0.03) % 360);   // the spinner
            var pnSp = Pen(FA(Alpha(hubCur, 120 + 60 * embG), s1), 1.3);
            for (int k = 0; k < (HubState.LowPerf ? 0 : 3); k++) Arc(rlx + rlw + 10, cy + 14 + hdy, 16, 16, k * 120, 76, pnSp);
            Pop(stH);
            FillEll(rlx + rlw + 15, cy + 19 + hdy, 6, 6, SBrush(FA(Alpha(hubCur, 150 + 60 * emb2G), s1)));
            string tbn = fTab == 1 ? "DASHBOARD" : fTab == 2 ? "INTEGRATIONS" : fTab == 3 ? "SCRIPT HUB" : fTab == 4 ? "SETTINGS" : fTab == 5 ? "CREDITS" : "UPDATE LOGS";
            double vbw = HL.vbw;
            double vbx = cx + cw - 78 - vbw;
            double bcx2 = vbx - 100;
            FillRR(bcx2, cy + 12 + hdy, 92, 18, 5, SBrush(FA(Alpha(hubCur, 14 + 8 * tt), s1)));     // the tab chip
            MicroBackdrop(bcx2, cy + 12 + hdy, 92, 18, 5, hubCur, s1, now, 0.8);
            StrokeRR(bcx2, cy + 12 + hdy, 92, 18, 5, Pen(FA(Alpha(hubCur, 60 + 40 * tt), s1), 1));
            FillEll(bcx2 + 8, cy + 18.5 + hdy, 5, 5, SBrush(FA(Alpha(hubCur, 200), s1)));
            var tbf = Fonts.MeasureW(tbn, HL.fS) <= 75 ? HL.fS : HL.fXs;
            Txt(tbn, bcx2 + 17, cy + 11 + hdy + (1 - tt) * 3, 75, 18, tbf, FA(Alpha(AccHi(hubCur, 0.45), 120 + 115 * tt), s1), Fmt.L);
            FillRR(vbx, cy + 12 + hdy, vbw, 18, 5, SBrush(FA(Alpha(hubCur, 26 + 10 * emb2G), s1)));  // the version badge
            MicroBackdrop(vbx, cy + 12 + hdy, vbw, 18, 5, hubCur, s1, now, 0.8);
            StrokeRR(vbx, cy + 12 + hdy, vbw, 18, 5, Pen(FA(Alpha(hubCur, 120), s1), 1));
            Txt(AppInfo.ZVer, vbx, cy + 11 + hdy, vbw, 18, Fonts.fBadge, FA(Alpha(AccHi(hubCur, 0.35), 235), s1), Fmt.C);
            FadeLine(HL.brx - 12, cx + cw - 20, cy + 38, 0x1EFFFFFF, s1);
            // ---- the rail ----
            double dvx = HL.sbx + HL.sbw + 15;
            FillRect(dvx, HL.cty - 6, 1, ch - 66, VBrush(dvx, HL.cty - 6, 1, ch - 66, FA(Alpha(0xFFFFFF, 40), cf), Alpha(0xFFFFFF, 0)));
            double sN = s2 * HL.navA;                                            // the pill's own alpha - gone on the seventh tab
            FillRR(HL.sbx + sdx, HL.navY, HL.sbw, HL.nvh, 9, VBrush(HL.sbx + sdx, HL.navY, HL.sbw, HL.nvh, FA(Alpha(hubCur, 38), sN), FA(Alpha(hubCur, 12), sN)));
            MicroBackdrop(HL.sbx + sdx, HL.navY, HL.sbw, HL.nvh, 9, hubCur, sN, now, 0.8);
            StrokeRR(HL.sbx + sdx, HL.navY, HL.sbw, HL.nvh, 9, Pen(FA(Alpha(hubCur, 90 + 30 * emb2G), sN), 1));
            FillRR(HL.sbx + sdx, HL.navY + 8, 3, HL.nvh - 16, 1.5, SBrush(FA(Alpha(hubCur, 220), sN)));
            FillEll(HL.sbx + sdx + 12, HL.navY + HL.nvh / 2 - 11, 22, 22, SBrush(FA(Alpha(hubCur, 26 + 14 * emb2G), sN)));
            for (int tk2 = 1; tk2 <= 2; tk2++)
                Line(HL.sbx + sdx + HL.sbw - 10, HL.navY + 12 + (tk2 - 1) * 10, HL.sbx + sdx + HL.sbw - 5, HL.navY + 12 + (tk2 - 1) * 10,
                    Pen(FA(Alpha(hubCur, 90 + 90 * Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.0035 - tk2 * 0.9)), 2)), sN), 1.3));
            for (int i = 1; i <= 6; i++)
            {
                double ny = HL.NavY(i);
                bool actv = fTab == i;
                double hv = Hv(30 + i);
                double nx = HL.sbx + sdx + 2 * hv;
                double icx = nx + 22, icy = ny + HL.nvh / 2;
                uint icc = FA(Alpha(actv ? AccHi(hubCur, 0.6) : 0xFFC7CBE0, actv ? 235 : 120 + 90 * hv), s2);
                NavIcon(i, icx, icy, icc, actv, hv, now);
                string lab = i == 1 ? "DASHBOARD" : i == 2 ? "INTEGRATIONS" : i == 3 ? "SCRIPT HUB" : i == 4 ? "SETTINGS" : i == 5 ? "CREDITS" : "UPDATE LOGS";
                Txt(lab, nx + 38, ny, HL.sbw - 42, HL.nvh, Fonts.fBadge, FA(Alpha(actv ? 0xFFFFFFu : 0xC7CBE0u, actv ? 235 : 120 + 95 * hv), s2), Fmt.L);
            }
            Heart(HL.sbx + sdx + HL.sbw / 2 + 5, HL.NavY(6) + HL.nvh + 53, hubCur, s2, now);
            ProfilePlate.Draw(this, sdx, s2, now, hubCur, 0.5 + 0.5 * Math.Sin(DecT(now) * 0.0026));
            // ---- the tabs ----
            int stTabs = PushG();
            ClipRR(HubLayout.pd, HubLayout.pd, HubLayout.cw, HubLayout.ch, 20);
            if (fAt != 0 && tt < 1)
            {
                DrawTab(fPrev, s3 * (1 - tt), -16 * tt, cdy, now);
                DrawTab(fTab, s3 * tt, 16 * (1 - tt), cdy, now);
            }
            else DrawTab(fTab, s3, 0, cdy, now);
            Pop(stTabs);
            Txt(AppInfo.AppName + "  -  " + AppInfo.ZVer, cx + cw - 212, cy + ch - 22, 194, 14, Fonts.fHint, FA(0x36C7CBE0, cf), Fmt.R);
            Modules.FastFlags.Detail.Draw(this, cf, now, hubCur);
            ProfilePlate.DrawBio(this, cf, now, hubCur);
            EditProfile.Draw(this, cf, now, hubCur);
            Gallery.Draw(this, now);
            Tut.Draw(this, now, hubCur);
            Upd.Draw(this, now);
            // LAST. ZoneAt gives the context menu the first look at a click, so
            // it has to be the last thing painted - drawn before EDIT PROFILE and
            // the gallery it was hidden underneath them, and a right-click on
            // those fields opened a menu nobody could see.
            Modules.FastFlags.FfmCtx.Draw(this, now, hubCur);
            if (HL.dragT > 0.01 && HL.minT < 0.5 && !_snapping) DragVeil(now, hubCur, cf);   // the drag veil
            Pop(stM);
        }
        // ---- the minimised pill ----
        // Armed-aware base for every orb border layer: stCol tracks the armed
        // state, so the whole frame agrees rather than half of it.
        uint armHue = Mix(AMBER, HubState.C_ON, Clamp(HL.actT, 0.0, 1.0));
        uint stCol = Mix(hubCur, armHue, Clamp(HL.armT, 0.0, 1.0));
        uint bcol = AccHi(stCol, 0.14);
        MiniPill.Cues(HL, now, bcol);
        MiniPill.Draw(this, HL, now, bcol, HL.armT, embG, Hv(9));
        // Fully folded and settled, the window is confined to the pill. Anything
        // mid-fold keeps the whole rect, because the card is still on screen.
        // Fully folded and settled, the window IS the pill - so the space the
        // card used to fill stops taking clicks. 37 x 48 of orb plus the armed
        // ping at +25 and the hover orbits at +8, so the box has to clear the
        // widest ring the pill ever throws.
        if (HL.minT >= 0.999 && HL.minStart == 0) SetCrop(HL.mbx - 82, HL.mby - 92, 164, 184);
        else SetCrop(0, 0, 0, 0);
        Pop(st0);
        Pop(win);

        if (fin) { HubFinish(); return; }
        // ---- the tiers ----
        bool anim = it < 1 || HL.closeAt != 0 || HL.minStart != 0 || (fAt != 0 && tt < 1) || HL.dragT > 0 || Math.Abs(HL.navY - nvTgt) > 0.3
                 || (HL.accFlashAt != 0 && now - HL.accFlashAt < 420) || HL.heartAt != 0 || HubState.ThemeMoving is not null
                 || HL.burstAt != 0 || (HL.scatAt != 0 && now - HL.scatAt < 400) || (HL.minT > 0.004 && HL.minT < 0.996)
                 || (HL.minV > 0.004 && HL.minV < 0.996) || HL.minV < -0.001
                 // Collapsed: a fade that is RUNNING, not a rotation that exists.
                 // The same mistake UPDATE LOGS had - it asked whether there were
                 // pictures rather than whether one was crossfading, and held the
                 // active tier for as long as the pill was on screen.
                 || (HL.minT >= 0.5 && HL.mbFade != 0)
                 || HL.drag != 0 || ClickRecent(now) || Math.Abs(HL.lgSc - HL.lgScT) > 0.3 || (HL.cpAt != 0 && now - HL.cpAt < 1600)
                 || Math.Abs(HL.mdscr - HL.mdscrT) > 0.4 || (HL.modT > 0 && HL.modT < 1) || (HL.modAt != 0 && now - HL.modAt < 700)
                 || (Tab == 2 && HubMod == 1 && Modules.FastFlags.FfmPanel.Animating) || (Tab == 2 && HubMod == 5 && (Modules.Special.Spf.Animating || Modules.Special.SpfSaved.Animating || Modules.Special.SpfAcctView.Animating)) || (Tab == 2 && HubMod == 7 && Modules.LoginItems.LgiPanel.Animating) || (Tab == 2 && HubMod == 6 && Modules.DeviceOpt.DopPanel.Animating) || (Tab == 2 && HubMod == 3 && Modules.Cursor.CurPanel.Animating) || (Tab == 2 && HubMod == 4 && Modules.ClientSettings.RSetPanel.Animating) || (Tab == 2 && HubMod == 2 && Modules.Forsaken.FskPanel.Animating) || (Tab == 3 && Modules.ScriptHub.ScrTab.Animating) || Upd.Animating || Gallery.Animating || EditProfile.Animating || ProfilePlate.Animating || Tut.Animating || Tab == 7 || Modules.FastFlags.FfmCtx.Live || (Tab == 6 && Tabs.UpdateLogs.ReelAnimating) || Gfx.Pool.SelFadeAt != 0 || Modules.FastFlags.Detail.Live;
        // ---- while the window is being dragged ----
        // The card's POSITION is the compositor's job: moving the window does not
        // need a repaint, and every pointer move already moves it. What still
        // needs frames is the tilt easing and the marching ants on the veil, and
        // neither reads any better at 60 than at 30 - while a full hub redraw at
        // 60 is the most expensive thing this renderer ever does, over a card the
        // veil is dimming to a third anyway. Halving the rate for the duration of
        // a drag halves its cost and cannot be seen.
        // ---- nobody is looking at it ----
        // Another application has the foreground: no pointer is going to arrive
        // on this window, no hover is going to change, and everything left is
        // ambient. The transitions that must still finish - a fold, a close, a
        // tab change - keep the settled tier so they land rather than freeze;
        // everything else stands down to the in-game rate. This is the single
        // biggest saving available, because the hub spends most of its life
        // behind whatever the person is actually doing.
        _animWhy = anim ? Why(now, nvTgt, it, fAt, tt) : hmax > 0 ? "hover" : PtrIn ? "pointer" : "";
        if (!Active) Tim(anim || HL.drag != 0 ? Pace.TICK_S : Pace.TICK_G);
        else if (HL.drag == 2) Tim(Pace.TICK_S);
        // ---- folded and settled ----
        // Everything still moving on the pill - the drifting rim, the ring, the
        // armed ping, the reel between fades - is slow, and this is the state
        // the hub sits in for as long as it is out of the way. Thirty reads the
        // same as sixty here and costs half. A hover or a transition still gets
        // the fast tier from the line below.
        else if (HL.minT >= 0.999 && HL.minStart == 0 && HL.burstAt == 0 && HL.scatAt == 0 && HL.mbFade == 0 && hmax <= 0 && !PtrIn)
            Tim(Pace.TICK_S);
        else if (anim || hmax > 0 || PtrIn) Tim(Pace.TICK_A);
        // UPDATE LOGS' picture reel used to hold the ACTIVE tier for as long as
        // the tab was open - sixty full redraws a second for a crossfade that
        // runs 420 ms once every three seconds. It asks for the fast tier while
        // a fade is actually running and sits on the settled one between them.
        // The pill's own motion - the drifting rim, the ring, the armed ping - is
        // slow enough that thirty reads the same as sixty, and it is on screen
        // for as long as the hub is folded.
        else if (HL.minT >= 0.5) Tim(Pace.TICK_S);
        else if (Tab == 6 && Gfx.Pool.HasRot) Tim(Pace.TICK_S);
        else if (now - PtrAt < Pace.HUB_DEEP_MS) Tim(Pace.TICK_S);
        else Tim(Pace.TICK_Z);
    }


    // ---- the drag ----
    // The veil, drawn by the full frame and by the snapshot path alike.
    void DragVeil(long now, uint acc, double cf)
    {
        double cx = HubLayout.pd, cy = HubLayout.pd, cw = HubLayout.cw, ch = HubLayout.ch, d = HL.dragT;
        FillRR(cx, cy, cw, ch, 20, SBrush(FA(Alpha(0x0B0C14, 122 * d), cf)));
        var pnV = Pen(FA(Alpha(acc, 205 * d), cf), 1.6);
        PenDash(pnV, 1);
        PenDashOff(pnV, DecT(now) * 0.03 % 1000);
        StrokeRR(cx + 3, cy + 3, cw - 6, ch - 6, 17, pnV);
        double mcx = cx + cw / 2, mcy = cy + ch / 2 - 7;
        var pnA = Pen(FA(Alpha(0xFFFFFF, 225 * d), cf), 1.8);
        for (int k = 0; k < 4; k++)
        {
            double a_ = k * 1.5708;
            double x2 = mcx + 13 * Math.Cos(a_), y2 = mcy + 13 * Math.Sin(a_);
            Line(mcx + 4 * Math.Cos(a_), mcy + 4 * Math.Sin(a_), x2, y2, pnA);
            Line(x2, y2, x2 - 4.5 * Math.Cos(a_ - 0.5), y2 - 4.5 * Math.Sin(a_ - 0.5), pnA);
            Line(x2, y2, x2 - 4.5 * Math.Cos(a_ + 0.5), y2 - 4.5 * Math.Sin(a_ + 0.5), pnA);
        }
        Txt("MOVING", mcx - 40, mcy + 16, 80, 14, Fonts.fBadge, FA(Alpha(0xFFFFFF, 235 * d), cf), Fmt.C);
    }

    // The card as it looked when the drag began. Rendered by re-entering this
    // surface once with _capturing set, which takes the ordinary path with the
    // drag transform and the veil suppressed - so what lands in the bitmap is
    // the card sitting still, which is exactly what the drag needs to move.
    Avalonia.Media.Imaging.RenderTargetBitmap? _snap;
    bool _capturing, _snapWant, _snapping;
    public void DragSnapInvalidate() { _snapWant = true; }
    /// <summary>
    /// The cached card, or null until it exists. NEVER built here: this runs
    /// inside the render pass and a bitmap cannot be rendered from within one -
    /// the capture is posted, and the frames before it lands take the ordinary
    /// path, which is correct and merely not yet cheap.
    /// </summary>
    Avalonia.Media.Imaging.Bitmap? DragSnap()
    {
        if (_snap is null && !_capturing) { _capturing = true; Avalonia.Threading.Dispatcher.UIThread.Post(CaptureSnap, Avalonia.Threading.DispatcherPriority.Render); }
        return _snapWant ? null : _snap;
    }
    void CaptureSnap()
    {
        try
        {
            if (Win is null || HL.drag != 2) return;
            _snapWant = false;
            var b = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new Avalonia.PixelSize(Math.Max(1, (int)(HubLayout.bw * K)), Math.Max(1, (int)(HubLayout.bh * K))), new Avalonia.Vector(96, 96));
            _snapping = true;
            try { b.Render(this); } finally { _snapping = false; }
            _snap?.Dispose(); _snap = b;
        }
        catch { }
        finally { _capturing = false; Tim(Pace.TICK_S); }
    }
    void DragSnapDrop() { if (_snapping) return; _snap?.Dispose(); _snap = null; _snapWant = false; }
    // ---- why is this frame not the last one? ----
    // The active tier costs sixty full redraws a second, so anything that holds
    // it holds the whole hub's CPU. Naming the term that is true turns "the hub
    // is busy" into one line of evidence.
    string _animWhy = "";
    public string AnimWhy => _animWhy;
    string Why(long now, double nvTgt, double it, long fAt, double tt)
    {
        var w = new List<string>();
        if (it < 1) w.Add("intro");
        if (HL.closeAt != 0) w.Add("closing");
        if (HL.minStart != 0 || (HL.minT > 0.004 && HL.minT < 0.996)) w.Add("fold");
        if (fAt != 0 && tt < 1) w.Add("tabfade");
        if (HL.dragT > 0) w.Add("drag");
        if (Math.Abs(HL.navY - nvTgt) > 0.3) w.Add("nav");
        if (HL.accFlashAt != 0 && now - HL.accFlashAt < 420) w.Add("accflash");
        if (HL.heartAt != 0) w.Add("heart");
        if (HubState.ThemeMoving is not null) w.Add("theme");
        if (HL.burstAt != 0 || (HL.scatAt != 0 && now - HL.scatAt < 400)) w.Add("pillfx");
        if (HL.drag != 0) w.Add("dragzone");
        if (ClickRecent(now)) w.Add("click");
        if (Math.Abs(HL.lgSc - HL.lgScT) > 0.3) w.Add("logscroll");
        if (HL.cpAt != 0 && now - HL.cpAt < 1600) w.Add("copied");
        if (Math.Abs(HL.mdscr - HL.mdscrT) > 0.4) w.Add("railscroll");
        if ((HL.modT > 0 && HL.modT < 1) || (HL.modAt != 0 && now - HL.modAt < 700)) w.Add("module");
        if (Upd.Animating) w.Add("upd");
        if (Gallery.Animating) w.Add("gallery");
        if (EditProfile.Animating) w.Add("editprofile");
        if (ProfilePlate.Animating) w.Add("profileplate");
        if (Tut.Animating) w.Add("tut");
        if (Tab == 7) w.Add("town");
        if (Modules.FastFlags.FfmCtx.Live) w.Add("ctxmenu");
        if (Tab == 6 && Tabs.UpdateLogs.ReelAnimating) w.Add("logsreel");
        if (Gfx.Pool.SelFadeAt != 0) w.Add("selfade");
        if (Modules.FastFlags.Detail.Live) w.Add("detail");
        if (Tab == 3 && Modules.ScriptHub.ScrTab.Animating) w.Add("scrtab");
        return w.Count == 0 ? "?" : string.Join(",", w);
    }
    bool ClickRecent(long now)
    {
        foreach (var t in ClickAt.Values) if (now - t < 600) return true;
        return false;
    }

    /// <summary>DrawTab(tab, f, dx, dy2): the title scramble, the chevrons, the tab number, the rule — then the body.</summary>
    void DrawTab(int tab, double f, double dx, double dy2, long now)
    {
        if (f <= 0.01) return;
        var HL = this.HL;
        uint hubCur = HubState.Cur;
        double x0 = HL.ctx + dx, y0 = HL.cty + dy2;
        string ttl = tab == 1 ? "DASHBOARD" : tab == 2 ? "INTEGRATIONS" : tab == 3 ? "SCRIPT HUB" : tab == 4 ? "SETTINGS" : tab == 5 ? "CREDITS" : tab == 7 ? "TOWN" : "UPDATE LOGS";
        var scr = HL.scr[tab - 1];
        long scrA = tab == Tab ? HL.scrAt : now - 99999;
        uint tcol = Mix(0xFFE8EAF6, hubCur, 0.2);
        if (now < scrA + 120 + scr.Chs.Count * 38)
        {
            for (int i2 = 1; i2 <= scr.Chs.Count; i2++)
            {
                string ch2; uint cc;
                if (now < scrA + 80 + i2 * 38) { ch2 = SCRSET[Random.Shared.Next(SCRSET.Length)].ToString(); cc = Alpha(Mix(hubCur, 0xFFE8EAF6, 0.55), 205); }
                else { ch2 = scr.Chs[i2 - 1]; cc = Alpha(tcol, 245); }
                Txt(ch2, x0 + scr.Xs[i2 - 1], y0 - 2, 40, 22, HL.fT, FA(cc, f), Fmt.L);
            }
        }
        else Txt(ttl, x0, y0 - 2, 240, 22, HL.fT, FA(Alpha(tcol, 245), f), Fmt.L);
        for (int k = 0; k <= 2; k++)
        {
            double xc = x0 + scr.W + 12 + k * 7;
            double ca = 50 + 85 * Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.0025 - k * 0.9)), 3);
            var pn = Pen(FA(Alpha(Mix(0xFFE8EAF6, hubCur, 0.35), Math.Min(255, ca)), f), 1.8);
            Line(xc, y0 + 8 - 3.8, xc + 3.6, y0 + 8, pn);
            Line(xc + 3.6, y0 + 8, xc, y0 + 8 + 3.8, pn);
        }
        FillRR(x0, y0 + 23, 40, 3, 1.5, SBrush(FA(Alpha(hubCur, 205), f)));
        FillRR(x0 + 46, y0 + 23, 12, 3, 1.5, SBrush(FA(Alpha(hubCur, 70), f)));
        if (tab == 1) Tabs.Dashboard.Head(this, x0, y0, f, now, hubCur);
        Txt("0" + tab, x0 + HL.ctw - 30, y0 - 2, 30, 18, Fonts.fBadge, FA(Alpha(hubCur, 80), f), Fmt.R);
        FadeLine(x0, x0 + HL.ctw, y0 + 26, 0x24FFFFFF, f);
        switch (tab)
        {
            case 1: Tabs.Dashboard.Draw(this, x0, y0, dx, dy2, f, now, hubCur); break;
            case 2: Tabs.Integrations.Draw(this, x0, y0, dx, dy2, f, now, hubCur); break;
            case 4: Tabs.SettingsTab.Draw(this, x0, y0, dx, dy2, f, now, hubCur); break;
            case 5: Tabs.Credits.Draw(this, x0, y0, dx, dy2, f, now, hubCur); break;
            case 6: Tabs.UpdateLogs.Draw(this, x0, y0, dx, dy2, f, now, hubCur); break;
            default: if (TabBodies.TryGetValue(tab, out var draw)) draw(f, dx, dy2, now); break;
        }
    }

    /// <summary>The theme switch's sweep across the card.</summary>
    void ThemeSweep(double cx, double cy, double cw, double ch, uint hubCur, double cf, long now)
    {
        if (HubState.ThemeMoving is not { } thAt) return;
        double twp = Math.Min((now - thAt) / (double)HubState.THEME_MS, 1.0);
        double te = 1 - Math.Pow(1 - twp, 3);
        double tsx = cx - 80 + (cw + 160) * te;
        double tfa = Math.Sin(3.14159 * twp);
        uint c0t = AccHi(hubCur, 0.55) & 0xFFFFFF;
        uint c1t = FA(Alpha(c0t, 80 * tfa), cf);
        FillRect(tsx - 78, cy, 78, ch, LineBrush(tsx - 79, cy - 2, 80, ch + 4, c0t, c1t, 0));
        FillRect(tsx, cy, 34, ch, LineBrush(tsx - 1, cy - 2, 36, ch + 4, c1t, c0t, 0));
        Line(tsx, cy, tsx, cy + ch, Pen(FA(Alpha(AccHi(hubCur, 0.75), 210 * tfa), cf), 1.6));
        for (int k = 1; k <= (HubState.LowPerf ? 0 : 7); k++)
        {
            double spy = cy + ch * (k / 8.0) + 10 * Math.Sin(DecT(now) * 0.004 + k);
            FillEll(tsx - 2.2 - 14 * Math.Sin(3.14159 * twp), spy - 2.2, 4.4, 4.4,
                SBrush(FA(Alpha(AccHi(hubCur, 0.6), 220 * tfa * Math.Max(0.0, Math.Sin(3.14159 * ((twp * 1.4 + k * 0.07) % 1.0)))), cf)));
        }
    }

    /// <summary>The rail's six icons: grid, plug, terminal, cog, medal, log.</summary>
    void NavIcon(int i, double icx, double icy, uint icc, bool actv, double hv, long now)
    {
        var HL = this.HL;
        if (i == 1)
        {
            var b = SBrush(icc);
            for (int q = 1; q <= 4; q++)
            {
                double qx = icx - 6 + ((q - 1) % 2) * 7, qy = icy - 6 + ((q - 1) / 2) * 7;
                FillRR(qx, qy, 5, 5, 1.4, b);
            }
        }
        else if (i == 2)
        {
            var pn = Pen(icc, 1.5);
            StrokeRR(icx - 6, icy - 6, 12, 12, 2.5, pn);
            Line(icx - 6, icy - 1.5, icx + 6, icy - 1.5, pn);
            Line(icx - 1.5, icy - 1.5, icx - 1.5, icy + 6, pn);
        }
        else if (i == 3)
        {
            var pn = Pen(icc, 1.5);
            Line(icx - 6.5, icy - 2, icx - 2.5, icy + 2, pn);
            Line(icx - 2.5, icy + 2, icx - 6.5, icy + 6, pn);
            Line(icx + 0.5, icy + 6, icx + 6.5, icy + 6, pn);
            Arc(icx - 7, icy - 7.5, 14, 9, 200, 140, pn);
        }
        else if (i == 4)
        {
            var pn = Pen(icc, 1.6);
            Ell(icx - 4.6, icy - 4.6, 9.2, 9.2, pn);
            double dtc = HL.cogAt != 0 ? Math.Min(now - HL.cogAt, 50) : 0;
            HL.cogAt = now;
            double vT = (actv && !HubState.LowPerf) ? 0.05 : 0.0;
            HL.cogV += (vT - HL.cogV) * EK(0.08);
            HL.cogA += HL.cogV * dtc;
            if (!actv)
            {
                double want = hv > 0.5 ? 67.5 : 22.5;
                double dA = (want - HL.cogA) % 90;
                if (dA < 0) dA += 90;
                HL.cogA += dA * EK(0.16);
            }
            if (HL.cogA > 36000) HL.cogA -= 36000;
            for (int q = 0; q < 4; q++)
            {
                double a_ = (HL.cogA + q * 90) * 0.0174533;
                Line(icx + 5.6 * Math.Cos(a_), icy + 5.6 * Math.Sin(a_), icx + 8.4 * Math.Cos(a_), icy + 8.4 * Math.Sin(a_), pn);
            }
            FillEll(icx - 1.5, icy - 1.5, 3, 3, SBrush(icc));
        }
        else if (i == 5)
        {
            var pn = Pen(icc, 1.6);
            Ell(icx - 2.8, icy - 7.4, 5.6, 5.6, pn);
            Arc(icx - 6, icy - 0.6, 12, 12, 180, 180, pn);
        }
        else
        {
            var pn = Pen(icc, 1.5);
            StrokeRR(icx - 5.5, icy - 7, 11, 14, 2.5, pn);
            Line(icx - 2.6, icy - 3.4, icx + 2.6, icy - 3.4, pn);
            Line(icx - 2.6, icy, icx + 2.6, icy, pn);
            Line(icx - 2.6, icy + 3.4, icx + 0.8, icy + 3.4, pn);
        }
    }

    /// <summary>The heart under the rail: the TOWN page's door, beating, with its satellites.</summary>
    void Heart(double hcx, double hcy, uint hubCur, double s2, long now)
    {
        var HL = this.HL;
        double hvH = Hv(37);
        double selH = HL.townT;
        double hph2 = (DecT(now) % 1400) / 1400.0;
        double thmp = Math.Pow(Math.Max(0.0, Math.Sin(hph2 * 6.283)), 8) + 0.55 * Math.Pow(Math.Max(0.0, Math.Sin(hph2 * 6.283 - 1.05)), 8);
        double hs = 27 * (1 + 0.11 * thmp) * (1 + 0.08 * hvH + 0.05 * selH);
        if (!HubState.LowPerf)
        {
            FillEll(hcx - 62, hcy - 62, 124, 124, SBrush(FA(Alpha(hubCur, 8 + 10 * selH), s2)));
            FillEll(hcx - hs * 1.65, hcy - hs * 1.65, hs * 3.3, hs * 3.3, SBrush(FA(Alpha(hubCur, 20 + 52 * thmp + 30 * hvH + 24 * selH), s2)));
            for (int k6 = 1; k6 <= 5; k6++)
            {
                double oang = now * 0.00042 * (k6 % 2 != 0 ? 1 : -1) + k6 * 1.257;
                double orx = hs * 1.62 + 5 * Math.Sin(DecT(now) * 0.0011 + k6 * 1.7);
                double ory2 = orx * 0.78;
                double mhx = hcx + orx * Math.Cos(oang);
                double mhy = hcy - 2 + ory2 * Math.Sin(oang);
                double ma = 0.45 + 0.55 * (0.5 + 0.5 * Math.Sin(DecT(now) * 0.0017 + k6 * 2.1));
                double ms = 3.2 + (k6 % 3) * 1.1 + 1.2 * thmp;
                MiniHeart(mhx, mhy, ms, ElA(TH(FA(Alpha(AccHi(hubCur, 0.30 + 0.1 * (k6 % 2)), 150 * ma), s2))));
            }
            for (int k6 = 1; k6 <= 2; k6++)
            {
                int cyc6 = 3400 + k6 * 1100;
                double ph6 = ((now + k6 * 1483) % cyc6) / (double)cyc6;
                double mhx = hcx + (k6 == 1 ? -1 : 1) * (hs * 1.35) + 6 * Math.Sin(DecT(now) * 0.002 + k6 * 2.2);
                double mhy = hcy + hs * 0.9 - ph6 * (hs * 2.3);
                double ma = Math.Sin(3.14159 * ph6);
                MiniHeart(mhx, mhy, 3.4 + k6, ElA(TH(FA(Alpha(AccHi(hubCur, 0.4), 140 * ma), s2))));
            }
        }
        double hcyH = hcy - hs * 0.11;
        int stH = PushXform(hcx, hcyH, 1.0, 3.2 * Math.Sin(DecT(now) * 0.0016));
        var hp2 = new StreamGeometry();
        using (var g = hp2.Open())
        {
            g.BeginFigure(new Avalonia.Point(hcx, hcyH + hs * 0.85), true);
            g.CubicBezierTo(new Avalonia.Point(hcx - hs * 1.25, hcyH + hs * 0.10), new Avalonia.Point(hcx - hs * 0.80, hcyH - hs * 0.85), new Avalonia.Point(hcx, hcyH - hs * 0.28));
            g.CubicBezierTo(new Avalonia.Point(hcx + hs * 0.80, hcyH - hs * 0.85), new Avalonia.Point(hcx + hs * 1.25, hcyH + hs * 0.10), new Avalonia.Point(hcx, hcyH + hs * 0.85));
            g.EndFigure(true);
        }
        FillPath(hp2, VBrush(hcx - hs * 1.3, hcyH - hs, hs * 2.6, hs * 2, FA(Alpha(AccHi(hubCur, 0.32), 230), s2), FA(Alpha(hubCur, 160 + 40 * thmp), s2)));
        StrokePath(hp2, Pen(FA(Alpha(AccHi(hubCur, 0.5), 170 + 65 * thmp), s2), 1.5));
        FillEll(hcx - hs * 0.52, hcyH - hs * 0.46, hs * 0.36, hs * 0.32, SBrush(FA(Alpha(0xFFFFFF, 65 + 55 * thmp), s2)));
        FillEll(hcx + hs * 0.22, hcyH - hs * 0.52, hs * 0.14, hs * 0.12, SBrush(FA(Alpha(0xFFFFFF, 40), s2)));
        Pop(stH);
        if (HL.heartAt != 0 && now - HL.heartAt < 560 && !HubState.LowPerf)
        {
            double bt = (now - HL.heartAt) / 560.0;
            double be = 1 - (1 - bt) * (1 - bt);
            for (int q = 0; q < 8; q++)
            {
                double ba = q * 0.785 + 0.39;
                double brd = hs * (0.6 + 1.9 * be);
                MiniHeart(hcx + brd * Math.Cos(ba), hcy + brd * Math.Sin(ba) * 0.9 - 6 * be, 3.6 + 1.2 * (1 - bt),
                    ElA(TH(FA(Alpha(AccHi(hubCur, 0.4), 200 * (1 - bt)), s2))));
            }
        }
        else if (HL.heartAt != 0 && now - HL.heartAt >= 560) HL.heartAt = 0;
        if (selH > 0.01)
        {
            Ell(hcx - hs * 1.32, hcy - hs * 1.22, hs * 2.64, hs * 2.44, Pen(FA(Alpha(AccHi(hubCur, 0.45), R(150 * selH)), s2), 1.4));
            var pnT = Pen(FA(Alpha(hubCur, R(60 * selH)), s2), 1);
            PenDash(pnT, 2);
            Ell(hcx - hs * 1.52, hcy - hs * 1.42, hs * 3.04, hs * 2.84, pnT);
            Txt("TOWN", hcx - 30, hcy + hs * 1.5, 60, 12, HL.fXs, FA(Alpha(AccHi(hubCur, 0.4), R(200 * selH)), s2), Fmt.C);
        }
        if (!HubState.LowPerf)
        {
            if (thmp > 0.03)
            {
                double rr2 = hs * (1.28 + 0.85 * (1 - thmp));
                Ell(hcx - rr2, hcy - rr2 * 0.92, rr2 * 2, rr2 * 1.84, Pen(FA(Alpha(hubCur, 125 * thmp), s2), 1.5));
                double rr3 = hs * (1.05 + 0.55 * (1 - thmp));
                Ell(hcx - rr3, hcy - rr3 * 0.92, rr3 * 2, rr3 * 1.84, Pen(FA(Alpha(AccHi(hubCur, 0.4), 70 * thmp), s2), 1));
            }
            double spk = thmp * thmp;
            if (spk > 0.04)
            {
                var pn = Pen(FA(Alpha(AccHi(hubCur, 0.6), 210 * spk), s2), 1.3);
                for (int k7 = 1; k7 <= 3; k7++)
                {
                    double sxk = hcx + (k7 == 1 ? -hs * 1.15 : k7 == 2 ? hs * 1.25 : hs * 0.95);
                    double syk = hcy + (k7 == 1 ? -hs * 0.85 : k7 == 2 ? -hs * 0.45 : hs * 0.95);
                    double sl2 = 3 + 3.5 * spk;
                    Line(sxk - sl2, syk, sxk + sl2, syk, pn);
                    Line(sxk, syk - sl2, sxk, syk + sl2, pn);
                }
            }
        }
    }

    /// <summary>HubFinish(): the hub is the process.</summary>
    void HubFinish()
    {
        Stop();
        Live = null;
        var w = Win;
        // not from inside the render pass
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { w?.Close(); Boot.Exit(); });
    }
}

public static class HubWindow
{
    public static LayeredWindow? Win;
    /// <summary>HubOpen(): the hub, magnified by [hub] scale within the work area.</summary>
    public static void Open()
    {
        if (HubSurface.Live is not null) return;
        var s = new HubSurface();
        Win = new LayeredWindow(s, "YURIHUB", topmost: HubState.OnTop, toolWindow: false,
            scale: ui =>
            {
                double mag = Math.Min(HubState.Mag, Math.Min((UiScale.WorkW - 16) / (HubLayout.bw * ui), (UiScale.WorkH - 16) / (HubLayout.bh * ui)));
                s.HL.k = ui * mag;
                return ui * mag;
            });
        s.TrackActiveOn(Win);                             // stand down while another application has the foreground
        Win.Show();
        s.Focus();
        Tray.Init();                                      // TrayInit(): set up unconditionally, whatever TRUE MINIMISE is set to
    }
}
