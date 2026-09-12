using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Yuri.Core;
using Yuri.Gfx;
using Yuri.Platform;
using Yuri.Shell;
using Yuri.Shell.Hub;
using static Yuri.Gfx.Col;
using static Yuri.Gfx.Ease;
using static Yuri.Gfx.G;
using static Yuri.Gfx.Tex;
using static Yuri.Shell.Hub.HubUI;

namespace Yuri.Modules.Forsaken;

/// <summary>
/// The floating cards the .ahk kept over the game (the block's shield card and
/// PUZZLE AI's status card), the grid markers, and the SET GRID reticle -
/// each a layered, topmost tool window drawn by its own surface. The two
/// cards share the .ahk's 312 x 132 face with its 26 px halo, drag from the
/// grip, remember where they were put, and close with their system.
/// </summary>
public abstract class FskCard : Surface
{
    // the .ahk's overlay geometry, verbatim
    protected const double CW = 312, CH = 132, PAD = 26;
    protected const double BMX = PAD + CW - 44, BCX = PAD + CW - 22, BTY = PAD + 16;      // minimise, close
    protected const double BBX = PAD + CW - 36, BBY = PAD + CH - 36;                      // the bubble's centre
    protected const double AVX = PAD + 46, AVY = PAD + 65;                                // the portrait
    const double ANIM_MS = 240, INTRO_MS = 520, RIPPLE_MS = 480, MIN_MS = 340, CLOSE_MS = 420, TOG_MS = 650;
    protected readonly string IniKey;
    protected long IntroAt, CloseAt, TogAt; protected int TogDir = 1;
    protected double DragT, TiltS, PrevBX, MinT, MinFrom, MinTo; protected long MinStart, BurstAt, ScatAt;
    // dragOn: 0 none, 1 pressed and it may still turn out to be a click, 2 moving.
    // The .ahk promotes at 4 px (16 px squared) and the window does not move
    // until it does - so a click on the bubble restores instead of nudging it.
    int _dragOn; long _dragAt;
    /// <summary>The drag state, for the harness: 0 none, 1 pressed, 2 moving.</summary>
    public int DragOnPublic => _dragOn;
    /// <summary>The bubble's centre, for the harness.</summary>
    public static double BBX_Public => BBX;
    public static double BBY_Public => BBY;
    protected double h1T, h2T, h3T, h4T, hgT, hovT;
    // stateT is TIMED, not exponentially eased: the .ahk runs it over ANIM_MS on
    // a cubic and `animStart` is what the ENABLED / DISABLED crossfade reads to
    // know a change is in flight. An EK ease has no such edge, so the label
    // switched instantly under a pill that was still moving.
    protected double AnimT, AnimFrom, AnimTo; protected long AnimStart, RippleStart;
    /// <summary>ToggleBind: start the state ease, the ripple off the status dot and the sweep across the card.</summary>
    public void ToggleAnim(int dir, double to)
    {
        AnimFrom = AnimT; AnimTo = to; AnimStart = Clock.Tick; RippleStart = AnimStart;
        TogAt = AnimStart; TogDir = dir;
        Tim(Pace.TICK_A);
    }
    protected bool Closed; public Action? OnClosed;
    /// <summary>Is this card's own system firing? The block card says `enabled`, the puzzle card says solving - it drives the pulse, the scan band's rate and the glints.</summary>
    protected virtual bool Armed => false;
    /// <summary>The eased version of it (stateT), for the parts that crossfade rather than switch.</summary>
    protected virtual double StateT => Armed ? 1.0 : 0.0;
    /// <summary>The dormant blink the brand dot carries - 2200 ms, two beats.</summary>
    protected static int Blink(long now) { double hb = DecT(now) % 2200; return hb < 140 || (hb > 300 && hb < 440) ? 1 : 0; }
    protected FskCard(string key) : base(CW + PAD * 2, CH + PAD * 2, UiScale.Factor) { IniKey = key; IntroAt = Clock.Tick; }

    /// <summary>HitZone: 1 minimise, 2 close, 3 the bubble, 4 the grip, 5 the portrait, 6 the key row.</summary>
    protected override int ZoneAt(double ux, double uy)
    {
        if (MinT < 0.5)
        {
            if (Sq(ux - BMX) + Sq(uy - BTY) <= 81) return 1;
            if (Sq(ux - BCX) + Sq(uy - BTY) <= 81) return 2;
            if (Sq(ux - AVX) + Sq(uy - AVY) <= 676) return 5;
            if (ux >= PAD + CW / 2 - 17 && ux <= PAD + CW / 2 + 17 && uy >= PAD + 4 && uy <= PAD + 28) return 4;
            int k = KeyZone(ux, uy); if (k != 0) return k;
        }
        else if (Sq(ux - BBX) + Sq(uy - BBY) <= 1024) return 3;
        return 0;
    }
    static double Sq(double v) => v * v;
    public int ZoneAtPublic(double ux, double uy) => ZoneAt(ux, uy);
    public double MinT_Public { get => MinT; set { MinT = value; MinTo = value; MinStart = 0; } }
    /// <summary>The fold's INTENT, which settles the moment the button is pressed - for the harness.</summary>
    public double MinTo_Public => MinTo;
    /// <summary>The animation state, for the harness: the eased ENABLED weight, whether a change is in flight, and the toggle's clock.</summary>
    public double AnimT_Public => AnimT;
    public bool AnimLive => AnimStart != 0;
    public bool RippleLive => RippleStart != 0;
    public long TogAt_Public => TogAt;
    public int TogDir_Public => TogDir;
    public double H4_Public => h4T;
    /// <summary>Drive the drag state machine without a platform pointer, for the harness.</summary>
    public void PressAt(double ux, double uy)
    {
        TakeFocusIfSafe();
        int z = ZoneAt(ux, uy);
        if (z == 3 || z == 4) { _dragOn = 1; _dragAt = Clock.Tick; Dragging = true; DragMoves = false; DragGripX = ux; DragGripY = uy; return; }
        if (z == 1) MinSet(1); else if (z == 2) Dismiss(); else CardPress(z);
    }
    public void MoveAt(double ux, double uy) => OnPointerMove(ux, uy);
    public void ReleaseAt(double ux, double uy)
    {
        bool wasClick = _dragOn == 1;
        _dragOn = 0; Dragging = false;
        if (wasClick && ZoneAt(ux, uy) == 3) MinSet(0);
        CardRelease(ZoneAt(ux, uy));
    }
    public bool ClosedPublic => Closed;
    protected virtual int KeyZone(double ux, double uy) => 0;
    /// <summary>
    /// The .ahk's Click(): minimise, close, the portrait and the key rows all act
    /// on the PRESS, and only the six-dot grip (4) and the collapsed bubble (3)
    /// begin a drag. The whole card body was draggable here, which it is not in
    /// the .ahk ("only the six-dot grip is draggable, not the title bar"), and the
    /// bubble was not draggable at all.
    /// </summary>
    // ---- a deliberate click is not the same as appearing ----
    // The cards are WS_EX_NOACTIVATE so that showing up over a game, and being
    // clicked over one, never takes the game's input. The cost, which the .ahk
    // never paid because it draws with GDI on a timer, is that this process can
    // then never become the foreground one at all - and a driver or an OS set to
    // cap background applications holds the card at that cap. The thing the
    // person is actually clicking on ends up being the thing running at fifteen
    // frames a second, and no amount of clicking gets it back.
    //
    // So: if the client is NOT in front there is nothing to take, and the window
    // is activated explicitly. The style stops a CLICK activating it; it does
    // not stop us. With a client in front nothing changes - that is the case the
    // style exists for.
    // With the style gone the click activates the window by itself; this is the
    // belt for a platform that refuses it anyway. No client check any more - the
    // press has already reached us, so the person meant this window.
    public bool FocusTried;
    void TakeFocusIfSafe()
    {
        if (Win is not { } w) return;
        FocusTried = true;
        try { if (!w.IsActive) w.Activate(); } catch { }
    }
    protected override void OnZoneDown(int z, PointerPressedEventArgs e)
    {
        TakeFocusIfSafe();
        if (z == 3 || z == 4) { _dragOn = 1; _dragAt = Clock.Tick; BeginDrag(e); DragMoves = false; Tim(Pace.TICK_A); return; }
        if (z == 1) { MinSet(1); Tim(Pace.TICK_A); return; }
        if (z == 2) { Dismiss(); Tim(Pace.TICK_A); return; }
        if (z == 5) { Gallery.Show(0, false); HubSurface.Live?.Tim(Pace.TICK_A); Tim(Pace.TICK_A); return; }
        CardPress(z);
        Tim(Pace.TICK_A);
    }
    protected override void OnPointerMove(double ux, double uy)
    {
        if (_dragOn == 1 && Dragging && (ux - DragGripX) * (ux - DragGripX) + (uy - DragGripY) * (uy - DragGripY) > 16) { _dragOn = 2; DragMoves = true; }
        CardMove(ux, uy);
    }
    /// <summary>LUp: a press on the bubble that never became a drag is the click that restores the card.</summary>
    protected override void OnZoneUp(int z, PointerReleasedEventArgs e)
    {
        bool wasClick = _dragOn == 1;
        _dragOn = 0;
        if (wasClick && z == 3) MinSet(0);
        CardRelease(z);
        Tim(Pace.TICK_A);
    }
    protected override void OnDragCancel() { _dragOn = 0; CardRelease(0); }
    protected virtual void CardPress(int z) { }
    protected virtual void CardMove(double ux, double uy) { }
    protected virtual void CardRelease(int z) { }
    /// <summary>StartMin / the bubble: the card folds toward the corner and a round bubble takes its place.</summary>
    public void MinSet(double to)
    {
        if (MinTo == to && MinStart != 0) return;
        MinFrom = MinT; MinTo = to; MinStart = Clock.Tick;
        BurstAt = to == 1.0 ? MinStart + (long)Math.Round(MIN_MS * 0.45) : 0;    // the ring that sheds as the bubble lands
        ScatAt = to == 1.0 ? 0 : MinStart;                                       // ... and the one that scatters as it opens
        Tim(Pace.TICK_A);
    }
    public void Dismiss() { if (CloseAt == 0) CloseAt = Clock.Tick; Tim(Pace.TICK_A); }
    public bool Closing => CloseAt != 0 && !Closed;
    /// <summary>
    /// Re-armed while the close was still playing. The card is torn down at the
    /// end of that animation and its OnClosed disarms the module - so turning a
    /// macro off and straight back on came up for a moment and then went out
    /// again as the old close landed. Catching it here means the card never dies
    /// and the module is never disarmed behind the person's back.
    /// </summary>
    public void Revive()
    {
        if (!Closing) return;
        CloseAt = 0; IntroAt = Clock.Tick;
        Tim(Pace.TICK_A);
    }
    public void Toggled(int dir) { TogAt = Clock.Tick; TogDir = dir; Tim(Pace.TICK_A); }
    public void SavePos()
    {
        if (Win is null) return;
        // The card's origin, not the bubble's: a card closed while collapsed
        // saved the cropped window's corner and came back a crop-width away
        // from where it was left.
        var p = UncroppedPos();
        Ini.Write(Paths.IniFile, "overlay", IniKey + "x", (long)p.X);
        Ini.Write(Paths.IniFile, "overlay", IniKey + "y", (long)p.Y);
    }
    public void Place()
    {
        if (Win is not { } w) return;
        long x = Ini.ReadInt(Paths.IniFile, "overlay", IniKey + "x", -99999), y = Ini.ReadInt(Paths.IniFile, "overlay", IniKey + "y", -99999);
        var scr = w.Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        if (x != -99999 && y != -99999) { w.Position = new PixelPoint((int)Math.Clamp(x, scr.X, scr.Right - 40), (int)Math.Clamp(y, scr.Y, scr.Bottom - 40)); return; }
        w.Position = new PixelPoint(scr.Right - (int)(Bw * K) - 24, scr.Y + 24 + (IniKey == "puz" ? (int)(Bh * K) + 12 : 0));
    }
    /// <summary>The frame every card shares: the eases, the fold, the card, the bubble.</summary>
    protected void Body(long now, uint acc, Action<double, long, double, double, double, double> card)
    {
        double it = Math.Min((now - IntroAt) / INTRO_MS, 1.0), ei = 1 - Math.Pow(1 - it, 3);
        double winA = Math.Min(it * 1.7, 1.0), yOff = (1 - ei) * 30;
        // ---- the close ----
        // It ANTICIPATES before it goes: a 5% swell over the first 16%, then an
        // implosion to 3% with a -14 degree twist, the alpha held for the first
        // third of that and the whole thing sinking 12 px. A linear fade with a
        // 12% shrink - which is what this was - is a window closing, not a card
        // being put away.
        double closeS = 1.0, closeRot = 0.0, ct = 0.0;
        if (CloseAt != 0)
        {
            ct = Math.Min((now - CloseAt) / CLOSE_MS, 1.0);
            if (ct >= 1)
            {
                if (!Closed) { Closed = true; SavePos(); Stop(); Avalonia.Threading.Dispatcher.UIThread.Post(() => { OnClosed?.Invoke(); Win?.Close(); }); }
                return;
            }
            if (ct < 0.16) closeS = 1 + 0.05 * Math.Sin(3.14159 * ct / 0.16);
            else
            {
                double cb = (ct - 0.16) / 0.84, e = cb * cb;
                closeS = 1 - 0.97 * e;
                closeRot = -14 * e;
                winA *= 1 - Math.Max(0.0, cb - 0.35) / 0.65;
                yOff += 12 * e;
            }
        }
        if (AnimStart != 0)                                                      // stateT, over ANIM_MS on a cubic
        {
            double t = Math.Min((now - AnimStart) / ANIM_MS, 1.0);
            AnimT = Lerp(AnimFrom, AnimTo, 1 - Math.Pow(1 - t, 3));
            if (t >= 1) { AnimStart = 0; AnimT = AnimTo; }
        }
        // Expired HERE, where it is always reached. The draw path that cleared it
        // only runs while the card is open, so a toggle on the minimised bubble
        // left it set for good.
        if (RippleStart != 0 && now - RippleStart >= RIPPLE_MS) RippleStart = 0;
        if (MinStart != 0) { double t = Math.Min((now - MinStart) / MIN_MS, 1.0); MinT = MinFrom + (MinTo - MinFrom) * (1 - Math.Pow(1 - t, 3)); if (t >= 1) { MinStart = 0; MinT = MinTo; } }
        // The .ahk's gate is the PROMOTED state or a press held past 130 ms, not
        // the press itself - the lift and the tilt belong to a move, not a click.
        double dtgt = _dragOn == 2 || (_dragOn == 1 && now - _dragAt > 130) ? 1.0 : 0.0;
        DragT += (dtgt - DragT) * EK(0.16); if (_dragOn != 2 && DragT < 0.004) DragT = 0;
        // baseX in LOGICAL units: Position is device pixels, so at any scale but
        // 1.0 the velocity - and therefore the tilt - was off by K.
        double baseX = (Win?.Position.X ?? 0) / Math.Max(0.001, K), vxd = baseX - PrevBX; PrevBX = baseX;
        TiltS += ((_dragOn == 2 ? Clamp(vxd * 0.35, -6.0, 6.0) : 0.0) - TiltS) * EK(0.18);
        if (_dragOn == 0 && Math.Abs(TiltS) < 0.03) TiltS = 0;
        int hz = CloseAt != 0 || _dragOn != 0 ? 0 : ZoneAt(PtrX, PtrY);
        h1T += ((hz == 1 ? 1.0 : 0.0) - h1T) * EK(0.2); h2T += ((hz == 2 ? 1.0 : 0.0) - h2T) * EK(0.2);
        h3T += ((hz == 3 ? 1.0 : 0.0) - h3T) * EK(0.2); hgT += ((hz == 4 ? 1.0 : 0.0) - hgT) * EK(0.2);
        hovT += ((hz == 5 ? 1.0 : 0.0) - hovT) * EK(0.2);
        h4T += ((hz == 6 ? 1.0 : 0.0) - h4T) * EK(0.2);                          // the keybind row, which had no ease at all
        if (h4T < 0.004 && hz != 6) h4T = 0;
        double f = winA;
        double oAX = MinT < 0.5 ? PAD + CW / 2 : BBX, oAY = MinT < 0.5 ? PAD + CH / 2 : BBY;
        // The .ahk moves the WINDOW by yOff (winPY := baseY + yOff). Adding it to
        // the transform's PIVOT, which is what this did, does not translate
        // anything - it only moves the point the scale and tilt turn about, so
        // the card never dropped in at all.
        int stY = PushShift(0, yOff);
        int stO = PushXform(oAX, oAY, (1 + 0.035 * DragT) * closeS, TiltS + closeRot);
        double cf = 1 - Math.Min(MinT * 1.7, 1.0);
        double tg = TogAt != 0 ? Math.Min((now - TogAt) / TOG_MS, 1.0) : 1.0; bool togActive = TogAt != 0 && tg < 1;
        if (MinStart != 0 && cf > 0.004)
            for (int i = 1; i <= 2; i++)
            {
                double lag = Clamp(MinT + (MinTo == 1 ? -0.09 : 0.09) * i, 0.0, 1.0);
                int stG = PushXform(BBX, BBY, 1 - 0.16 * lag, 8 * lag);
                StrokeRR(PAD, PAD, CW, CH, 16, Pen(Alpha(acc, R(80 - i * 32)), 1.4));
                Pop(stG);
            }
        if (cf > 0.004)
        {
            double antic = MinStart != 0 && MinTo == 1.0 && MinT < 0.18 ? 1 + 0.05 * Math.Sin(MinT / 0.18 * 3.14159) : 1;
            double cardPop = togActive && TogDir == 1 ? 1 + 0.018 * Math.Sin(3.14159 * Math.Min(tg * 1.5, 1.0)) : 1;
            int st = PushXform(BBX, BBY, (1 - 0.16 * MinT) * antic * cardPop, 8 * MinT);
            Card(now, acc, cf, tg, togActive, card);
            Pop(st);
        }
        double bt = Math.Max(0.0, (MinT - 0.45) / 0.55);
        if (bt > 0.004)
        {
            double eb = 1 + 2.3 * Math.Pow(bt - 1, 3) + 1.3 * Math.Pow(bt - 1, 2);
            // ---- the bubble's own reactions ----
            // popB is the bounce when the system is armed, sqB the squash when
            // it is disarmed, prsB the dip as it is pressed open, and the last
            // term a slow breath while armed. rotKick twists it as it ignites
            // and dipY drops it as it goes out. Without these the bubble simply
            // scaled in and out - the one control that is on screen the whole
            // time a game is being played, and it never reacted to anything.
            double popB = togActive && TogDir == 1 ? 1 + 0.14 * Math.Sin(tg * 7.85) * Math.Pow(1 - tg, 1.5) : 1;
            double sqB = togActive && TogDir == -1 ? 1 - 0.05 * Math.Sin(3.14159 * tg) : 1;
            double prsB = ScatAt != 0 && now - ScatAt < 120 ? 1 - 0.06 * (1 - (now - ScatAt) / 120.0) : 1;
            double pulseB = Armed ? (Math.Sin(DecT(now) * 0.00417) + 1) / 2 : 0;
            double bsc = (0.55 + 0.45 * eb) * popB * sqB * prsB * (1 + 0.05 * h3T) * (Armed ? 1 + 0.02 * (pulseB - 0.5) : 1);
            double rotKick = togActive && TogDir == 1 ? 10 * Math.Sin(tg * 6.9) * Math.Pow(1 - tg, 2) : 0;
            double rotB = -35 * Math.Pow(1 - Clamp(bt, 0.0, 1.0), 2) + rotKick;
            double dipY = togActive && TogDir == -1 ? 7 * Math.Sin(3.14159 * Math.Min(tg * 1.15, 1.0)) * (1 - 0.35 * tg) : 0;
            int stS = PushShift(0, dipY);
            int st = PushXform(BBX, BBY, bsc, rotB);
            Bubble(Math.Min(bt * 1.6, 1.0) * f, now, acc);
            Pop(st); Pop(stS);
        }
        Pop(stO);
        // Outside the transform on purpose: the shards are thrown BY the card
        // imploding, so they must not implode with it.
        if (CloseAt != 0 && ct > 0.30)
        {
            double p2 = (ct - 0.30) / 0.70, e2 = 1 - Math.Pow(1 - p2, 3);
            for (int i = 1; i <= 8; i++)
            {
                double a_ = (i * 45 + 25 * e2) * 0.0174533, rr_ = 10 + e2 * 38, srad = 2.6 * (1 - e2) + 0.5;
                FillEll(oAX + rr_ * Math.Cos(a_) - srad, oAY + rr_ * Math.Sin(a_) - srad, srad * 2, srad * 2,
                        SBrush(Alpha(acc, R(210 * (1 - e2)))));
            }
        }
        Pop(stY);
        // ---- collapsed, the window IS the bubble ----
        // Same fault the hub had: a 364 x 184 window with a 40 px bubble in the
        // corner of it, and the other nine tenths still taking every click that
        // lands on them - over a game, which is the whole point of these cards.
        // The bubble's rings reach 43 px and the hover ping 42, so 60 clears it.
        if (MinT >= 0.999 && MinStart == 0 && CloseAt == 0) SetCrop(BBX - 60, BBY - 60, 120, 120);
        else SetCrop(0, 0, 0, 0);
        // same as the hub: a drag is the compositor moving a window, not this
        // renderer needing sixty frames a second of it
        Tim(_dragOn == 2 ? Pace.TICK_S : Pace.TICK_A);
    }
    void Card(long now, uint acc, double f, double tg, bool togActive, Action<double, long, double, double, double, double> card)
    {
        double cx = PAD, cy = PAD;
        long ims = now - IntroAt; bool stg = ims < 700;
        double s1 = stg ? Ease3(Clamp((ims - 60) / 340.0, 0.0, 1.0)) : 1.0, s2 = stg ? Ease3(Clamp((ims - 140) / 340.0, 0.0, 1.0)) : 1.0;
        double s3 = stg ? Ease3(Clamp((ims - 220) / 340.0, 0.0, 1.0)) : 1.0, s4 = stg ? Ease3(Clamp((ims - 300) / 340.0, 0.0, 1.0)) : 1.0;
        double hdy = -(1 - s1) * 6;
        ShadowDraw(cx, cy + 3, CW, CH, 16, 10, 7, 7 + 4 * DragT, f);
        FillRR(cx, cy, CW, CH, 16, VBrush(cx, cy, CW, CH, FA(0xF2222438, f), FA(0xF8121423, f)));
        bool en = Armed;
        double pulse = en ? (Math.Sin(DecT(now) * 0.00417) + 1) / 2 : 0;
        double emb = (Math.Sin(DecT(now) * 0.0016) + 1) / 2, emb2 = (Math.Sin(DecT(now) * 0.004) + 1) / 2;
        double stT = StateT;
        int stC = PushG(); ClipRR(cx, cy, CW, CH, 16);
        // PanelBackdrop, not Mini: the overlay is a top-level card like the hub,
        // gate, gallery, loading and cropper, and it was the only one of the six
        // never given the shared backdrop.
        PanelBackdrop(cx, cy, CW, CH, acc, f, now, 0.95);
        FillRect(cx, cy, CW, CH, LineBrush(cx - 1, cy - 1, CW + 2, CH + 2, FA(0x0EFFFFFF, f), 0x00FFFFFF, 2));
        // the scan band: brisk armed, a faint drift dormant
        double per = en ? 3000.0 : 4600.0, swp = DecT(now) % per / per, swx = cx - 26 + (CW + 52) * swp;
        uint c0s = (en ? AccHi(acc, 0.3) : acc) & 0xFFFFFF;
        uint c1s = FA(Alpha(c0s, 16 + 34 * stT), f);
        FillRect(swx - 24, cy, 24, CH, LineBrush(swx - 25, cy - 2, 26, CH + 4, c0s, c1s, 0));
        FillRect(swx, cy, 24, CH, LineBrush(swx - 1, cy - 2, 26, CH + 4, c1s, c0s, 0));
        for (int i = 1; i <= 2; i++) { double rg = 66 + i * 24; FillEll(cx + 46 - rg, cy + 65 - rg, rg * 2, rg * 2, SBrush(FA(Alpha(acc, R(9 + 5 * pulse + 5 * emb2)), f * s2))); }
        // the accent rail every card in the suite carries
        FillRect(cx, cy, 14, CH, SBrush(FA(Alpha(acc, Math.Min(255, 46 + 24 * pulse + 16 * emb)), f)));
        FillRect(cx, cy, 4.5, CH, VBrush(cx, cy, 4.5, CH, FA(Alpha(acc, 245), f), FA(Alpha(acc, R(110 + 40 * emb)), f)));
        if (tg < 1)                                                              // the toggle sweep, in the direction it was thrown
        {
            double e = Ease3(tg), bcx0 = cx + CW * (TogDir == 1 ? e : 1 - e);
            uint sc = TogDir == 1 ? 0xFFFFFFu : acc & 0xFFFFFF;
            uint c1 = FA(Alpha(sc, 64 * (1 - tg)), f), c0 = sc & 0xFFFFFF;
            FillRect(bcx0 - 28, cy, 28, CH, LineBrush(bcx0 - 29, cy - 2, 30, CH + 4, c0, c1, 0));
            FillRect(bcx0, cy, 28, CH, LineBrush(bcx0 - 1, cy - 2, 30, CH + 4, c1, c0, 0));
        }
        Pop(stC);

        StrokeRR(cx, cy, CW, CH, 16, Pen(FA(0x28FFFFFF, f), 1));
        var pnH = Pen(FA(0x16FFFFFF, f), 1);
        Line(cx + 18, cy + 1.5, cx + CW - 18, cy + 1.5, pnH);
        Line(cx + 18, cy + CH - 1.5, cx + CW - 18, cy + CH - 1.5, Pen(FA(0x30000000, f), 1));
        for (int k = 0; k < 4; k++)                                              // corner glints, twinkling in sequence
        {
            double shim = (Math.Sin(DecT(now) * (en ? 0.006 : 0.003) + k * 1.5708) + 1) / 2;
            double ga = Math.Min(255, 105 + 40 * pulse * stT + 38 * shim + 58 * shim * stT + 100 * (1 - tg));
            double xk = k == 1 || k == 2 ? cx + CW - 31.5 : cx + 0.5, yk = k >= 2 ? cy + CH - 31.5 : cy + 0.5;
            double ang = k == 0 ? 195 : k == 1 ? 285 : k == 2 ? 15 : 105;
            Arc(xk, yk, 31, 31, ang, 60, Pen(FA(Alpha(acc, ga), f), 2));
        }
        FadeLine(cx + 20, cx + CW - 20, cy + 30, 0x30FFFFFF, f);
        FadeLine(cx + 20, cx + CW - 20, cy + 100, 0x30FFFFFF, f);

        Txt(AppInfo.AppName, cx + 36, cy + 6 + hdy, 90, 20, Fonts.fBrand, FA(0xCFE8EAF6, f * s1), Fmt.L);
        FillEll(cx + 36 + Fonts.MeasureW(AppInfo.AppName, Fonts.fBrand) + 6, cy + 13.5 + hdy, 5, 5,
                SBrush(FA(Alpha(acc, en ? R(200 + 55 * pulse) : Math.Min(255, R(150 + 35 * emb2 + 48 * Blink(now)))), f * s1)));
        // ---- the six-dot grip ----
        // It lifts and its dots swell on hover, and a wave runs across them: a
        // static field of dots reads as decoration, not as a handle.
        double gg = Math.Max(hgT, DragT);
        if (gg > 0.01)
        {
            FillRR(cx + CW / 2 - 17, cy + 7 + hdy, 34, 17, 7, SBrush(FA(Alpha(acc, R(30 * gg)), f * s1)));
            StrokeRR(cx + CW / 2 - 17, cy + 7 + hdy, 34, 17, 7, Pen(FA(Alpha(acc, R(90 * gg)), f * s1), 1));
        }
        double gLift = 0.9 * hgT, gDot = 2.4 + 0.8 * hgT, gOff = (gDot - 2.4) / 2;
        var gb = SBrush(FA(Alpha(Mix(0xFFFFFF, acc, 0.6 * hgT), R(46 + 105 * hgT)), f * s1));
        for (int i = 0; i < 6; i++)
        {
            int gc = i % 3, gr = i / 3;
            double gwv = hgT * 1.1 * Math.Sin(DecT(now) * 0.006 - gc * 0.9);
            FillEll(cx + CW / 2 - 8.2 + gc * 7 - gOff, cy + 11.8 + gr * 5.6 + hdy - gOff - gLift + gwv, gDot, gDot, gb);
        }
        // minimise and close. The .ahk's own indices: minimise leans on h2T's
        // neighbour and close on h1T's, so the pair drifts apart under the pointer.
        double em = 1 + 0.28 * h1T, ec = 1 + 0.28 * h2T;
        double bmx_ = BMX - 3 * h2T, bcx_ = BCX + 3 * h1T;
        double ym = BTY + hdy - 1.2 * h1T, yc = BTY + hdy - 1.2 * h2T;
        FillEll(bmx_ - 7 * em, ym - 7 * em, 14 * em, 14 * em, SBrush(FA(Alpha(0xFFFFFF, R(22 + 34 * h1T)), f * s1)));
        Ell(bmx_ - 7 * em, ym - 7 * em, 14 * em, 14 * em, Pen(FA(Alpha(0xFFFFFF, R(42 + 60 * h1T)), f * s1), 1));
        Line(bmx_ - 3.5 * em, ym, bmx_ + 3.5 * em, ym, Pen(FA(Alpha(0xE8EAF6, R(168 + 87 * h1T)), f * s1), 1.5));
        FillEll(bcx_ - 7 * ec, yc - 7 * ec, 14 * ec, 14 * ec, SBrush(FA(Alpha(acc, R(36 + 60 * h2T)), f * s1)));
        Ell(bcx_ - 7 * ec, yc - 7 * ec, 14 * ec, 14 * ec, Pen(FA(Alpha(acc, R(100 + 80 * h2T)), f * s1), 1));
        var pnX = Pen(FA(Alpha(AccHi(acc, 0.4), R(210 + 45 * h2T)), f * s1), 1.5);
        Line(bcx_ - 3.2 * ec, yc - 3.2 * ec, bcx_ + 3.2 * ec, yc + 3.2 * ec, pnX);
        Line(bcx_ - 3.2 * ec, yc + 3.2 * ec, bcx_ + 3.2 * ec, yc - 3.2 * ec, pnX);
        if (en)                                                                  // armed: the portrait gathers a halo
            for (int i = 1; i <= 3; i++) { double r_ = 27 + i * (2.0 + 1.8 * pulse); FillEll(AVX - r_, AVY - r_, r_ * 2, r_ * 2, SBrush(FA(Alpha(acc, R(20 - i * 5)), f * s2))); }
        Portrait(AVX, AVY, f * s2, now, acc);
        card(f, now, s2, s3, s4, tg);
        if (DragT > 0.01)                                                        // the MOVING veil, over everything the card drew
        {
            double d = DragT;
            FillRR(cx, cy, CW, CH, 16, SBrush(FA(Alpha(0x0B0C14, 122 * d), f)));
            var pnV = Pen(FA(Alpha(acc, 205 * d), f), 1.6);
            PenDash(pnV, 1); PenDashOff(pnV, DecT(now) * 0.03 % 1000);
            StrokeRR(cx + 3, cy + 3, CW - 6, CH - 6, 13, pnV);
            double mcx = cx + CW / 2, mcy = cy + CH / 2 - 7;
            var pnA = Pen(FA(Alpha(0xFFFFFF, 225 * d), f), 1.8);
            for (int k = 0; k < 4; k++)
            {
                double a_ = k * 1.5708;
                double x2 = mcx + 13 * Math.Cos(a_), y2 = mcy + 13 * Math.Sin(a_);
                Line(mcx + 4 * Math.Cos(a_), mcy + 4 * Math.Sin(a_), x2, y2, pnA);
                Line(x2, y2, x2 - 4.5 * Math.Cos(a_ - 0.5), y2 - 4.5 * Math.Sin(a_ - 0.5), pnA);
                Line(x2, y2, x2 - 4.5 * Math.Cos(a_ + 0.5), y2 - 4.5 * Math.Sin(a_ + 0.5), pnA);
            }
            Txt("MOVING", mcx - 40, mcy + 16, 80, 14, Fonts.fBadge, FA(Alpha(0xFFFFFF, 235 * d), f), Fmt.C);
        }
    }
    /// <summary>The card's portrait: the profile picture, its ring, the CHANGE plate on hover, and the orbiting arcs.</summary>
    protected void Portrait(double ax, double ay, double f, long now, uint acc)
    {
        ProfilePlate.Avatar(ax, ay, 26, f, now);
        if (hovT > 0.01)
        {
            int st = PushG(); ClipPath(new Avalonia.Media.EllipseGeometry(new Rect(ax - 26, ay - 26, 52, 52)));
            FillEll(ax - 26, ay - 26, 52, 52, SBrush(FA(Alpha(0x05060C, R(170 * hovT)), f)));
            double gy = ay - 13 + (1 - hovT) * 7;
            var pn = Pen(FA(Alpha(0xFFFFFF, R(210 * hovT)), f), 1.2);
            StrokeRR(ax - 7, gy, 14, 10, 2, pn);
            Line(ax - 4.5, gy + 7.5, ax - 1.5, gy + 3.5, pn); Line(ax - 1.5, gy + 3.5, ax + 2, gy + 7.5, pn); Line(ax + 2, gy + 7.5, ax + 4.5, gy + 5, pn);
            FillEll(ax + 2.2, gy + 1.6, 2.2, 2.2, SBrush(FA(Alpha(0xFFFFFF, R(220 * hovT)), f)));
            Txt("CHANGE", ax - 26, ay + 2 + (1 - hovT) * 7, 52, 14, Fonts.fBadge, FA(Alpha(0xFFFFFF, R(245 * hovT)), f), Fmt.C);
            Pop(st);
        }
        Ell(ax - 25, ay - 25, 50, 50, Pen(FA(0x50000000, f), 1));
        ProfilePlate.Ring(ax, ay, 26.5, AccHi(acc, 0.5 * hovT), f, hovT, now);
        Orbit(ax, ay, f, now, acc);
    }
    protected virtual void Orbit(double ax, double ay, double f, long now, uint acc)
    {
        double da = DecT(now) * 0.055 % 360;
        Arc(ax - 26.5, ay - 26.5, 53, 53, da, 50, Pen(FA(Alpha(acc, 55), f), 2.5));
        Arc(ax - 26.5, ay - 26.5, 53, 53, da + 30, 20, Pen(FA(Alpha(acc, R(120 + 45 * (Math.Sin(DecT(now) * 0.004) + 1) / 2)), f), 2.5));
    }
    /// <summary>DrawBubble: the minimised state - the portrait in its rings, chevrons and OPEN on hover.</summary>
    void Bubble(double f, long now, uint acc)
    {
        bool en3 = Armed;
        double pul3 = en3 ? (Math.Sin(DecT(now) * 0.00417) + 1) / 2 : 0;
        double emb4 = (Math.Sin(DecT(now) * 0.0016) + 1) / 2, hx3 = h3T;
        uint bc3 = en3 ? acc : AccHi(acc, 0.14);                              // the lifted dormant tone
        // armed: three soft halos that breathe with the pulse. dormant: nothing
        // until the pointer is on it, and then one.
        if (en3)
            for (int i = 1; i <= 3; i++)
            {
                double r_ = 31 + i * (2.4 + 2.2 * pul3) + 3 * hx3;
                FillEll(BBX - r_, BBY - r_, r_ * 2, r_ * 2, SBrush(FA(Alpha(acc, R(22 - i * 6 + 8 * hx3)), f)));
            }
        else if (hx3 > 0.01) FillEll(BBX - 36, BBY - 36, 72, 72, SBrush(FA(Alpha(acc, R(26 * hx3)), f)));
        for (int i = 1; i <= 5; i++)                                          // the drop stack, deeper under a drag
            FillEll(BBX - 30 - i, BBY - 28 - i, 60 + i * 2, 60 + i * 2, SBrush(FA(Alpha(0x000000, R(9 + 4 * DragT)), f)));
        // one heavy 120-degree arc: parked at 15 when armed, drifting when not
        double crA = en3 ? 15 : DecT(now) * 0.02 % 360;
        Arc(BBX - 33, BBY - 33, 66, 66, crA, 120, Pen(FA(Alpha(bc3, R(en3 ? 55 + 30 * pul3 : 62 + 30 * emb4)), f), 5));
        ProfilePlate.Avatar(BBX, BBY, 30, f, now);
        if (h3T > 0.01)
        {
            int st = PushG(); ClipPath(new Avalonia.Media.EllipseGeometry(new Rect(BBX - 30, BBY - 30, 60, 60)));
            FillEll(BBX - 30, BBY - 30, 60, 60, SBrush(FA(Alpha(0x05060C, R(152 * h3T)), f)));
            double gy = BBY - 12 + (1 - h3T) * 6;
            var pn = Pen(FA(Alpha(0xFFFFFF, R(215 * h3T)), f), 1.4);
            Line(BBX - 5, gy + 4, BBX, gy - 1, pn); Line(BBX, gy - 1, BBX + 5, gy + 4, pn);
            Line(BBX - 5, gy + 10, BBX, gy + 5, pn); Line(BBX, gy + 5, BBX + 5, gy + 10, pn);
            Txt("OPEN", BBX - 26, BBY + 3 + (1 - h3T) * 7, 52, 14, Fonts.fBadge, FA(Alpha(0xFFFFFF, R(245 * h3T)), f), Fmt.C);
            Pop(st);
        }
        Ell(BBX - 26, BBY - 26, 52, 52, Pen(FA(0x2EFFFFFF, f), 1));
        Ell(BBX - 29, BBY - 29, 58, 58, Pen(FA(0x50000000, f), 1));
        Ell(BBX - 32, BBY - 32, 64, 64, Pen(FA(0x66000000, f), 1));
        // ---- armed or not, said in arcs ----
        // Two comet pairs sweeping one way with faint arcs turning the other,
        // against four arcs that breathe out of phase. This is the difference
        // between the two states at a glance, and it was not ported: the ring
        // below it changes, but only in detail.
        double tp3 = TogAt != 0 ? Math.Min((now - TogAt) / TOG_MS, 1.0) : 1.0;
        if (en3)
        {
            double aa = DecT(now) * 0.05 % 360;
            double flash = TogDir == 1 && tp3 < 1 ? (1 - tp3) * 0.6 : 0;
            uint cc = AccHi(acc, flash);
            for (int i = 0; i < 2; i++)
            {
                double b0 = aa + i * 180;
                Arc(BBX - 30.75, BBY - 30.75, 61.5, 61.5, b0, 104, Pen(FA(Alpha(cc, 78), f), 2.6));
                Arc(BBX - 30.75, BBY - 30.75, 61.5, 61.5, b0 + 40, 64, Pen(FA(Alpha(cc, 160), f), 2.6));
                Arc(BBX - 30.75, BBY - 30.75, 61.5, 61.5, b0 + 76, 28, Pen(FA(Alpha(cc, 245), f), 2.8));
            }
            double ba2 = -DecT(now) * 0.07 % 360;
            var pnB = Pen(FA(Alpha(acc, 70), f), 1.6);
            Arc(BBX - 33.2, BBY - 33.2, 66.4, 66.4, ba2, 58, pnB);
            Arc(BBX - 33.2, BBY - 33.2, 66.4, 66.4, ba2 + 180, 58, pnB);
        }
        else
        {
            // a flicker as it goes out, like a filament
            double flick = TogDir == -1 && tp3 < 1 ? 0.55 + 0.45 * Math.Abs(Math.Sin(DecT(now) * 0.045)) : 1;
            for (int i = 0; i < 4; i++)
            {
                double e4 = 150 + 85 * (Math.Sin(DecT(now) * 0.0022 + i * 1.5708) + 1) / 2;
                Arc(BBX - 30.75, BBY - 30.75, 61.5, 61.5, i * 90 + 25 + 6 * Math.Sin(DecT(now) * 0.0011), 40,
                    Pen(FA(Alpha(bc3, R(Math.Min(255, e4) * flick)), f), 2.6));
            }
        }
        Ell(BBX - 35.2 - 2.5 * hx3, BBY - 35.2 - 2.5 * hx3, 70.4 + 5 * hx3, 70.4 + 5 * hx3,
            Pen(FA(Alpha(bc3, R((en3 ? 24 : 32) + 20 * hx3)), f), 1));
        // ---- the segment ring ----
        // 24 arcs. Armed, a bright head runs the ring and a second, faster one
        // runs against it, each arc widening under the head; dormant, the whole
        // ring drifts and a glint passes over it. A toggle ignites or
        // extinguishes them in sequence rather than switching the ring at once.
        double sct = ScatAt != 0 && now - ScatAt < 380 ? (now - ScatAt) / 380.0 : -1;
        double tp = TogAt != 0 ? Math.Min((now - TogAt) / TOG_MS, 1.0) : 1.0;
        bool en2 = Armed;
        double hx = h3T, emb3 = (Math.Sin(DecT(now) * 0.0016) + 1) / 2;
        uint bcol = en2 ? acc : AccHi(acc, 0.14);
        double head = DecT(now) * 0.18 % 360, head2 = 360 - DecT(now) * 0.11 % 360;
        double drift = DecT(now) * 0.008 % 360, glint = DecT(now) * 0.055 % 360;
        double igniteA = TogDir == 1 && tp < 1 ? 360 * Ease3(tp) : -1;
        double extingA = TogDir == -1 && tp < 1 ? 360 * Ease3(tp) : -1;
        for (int i = 1; i <= 24; i++)
        {
            double segA = (i - 1) * 15, segD = segA, boost = 0.0, al;
            uint colr;
            if (en2)
            {
                double d_ = (segA - head + 540) % 360 - 180;
                boost = Math.Pow(Math.Max(0.0, Math.Cos(d_ * 0.017453)), 7);
                double d2 = (segA - head2 + 540) % 360 - 180;
                al = Math.Min(255, 34 + 210 * boost + 95 * Math.Pow(Math.Max(0.0, Math.Cos(d2 * 0.017453)), 5));
                colr = acc;
                if (igniteA >= 0) { al = segA <= igniteA ? 255 : 22; colr = segA <= igniteA ? AccHi(acc, 0.5 * (1 - tp)) : acc; }
            }
            else
            {
                segD = segA + drift;
                double g_ = (segD - glint + 540) % 360 - 180;
                al = (i % 2 != 0 ? 85 : 44) + 110 * Math.Pow(Math.Max(0.0, Math.Cos(g_ * 0.017453)), 5);
                colr = bcol;
                if (extingA >= 0 && segA <= extingA) al = 16;
            }
            double rr_ = (i % 2 != 0 ? 34.0 : 36.2) + 2.5 * hx;
            if (sct >= 0) { double e = Ease3(sct); rr_ += 16 * e; al *= 1 - e; }
            double sw = 6 + 5 * boost;
            Arc(BBX - rr_ - 0.5, BBY - rr_ - 0.5, rr_ * 2 + 1, rr_ * 2 + 1, (en2 ? segA : segD) + (15 - sw) / 2, sw, Pen(FA(Alpha(colr, R(al)), f), 2.2));
        }
        // dotted counter-march ring + outer twinkle ticks
        var pnR = Pen(FA(Alpha(bcol, R(en2 ? 70 : 55 + 20 * emb3)), f), 1.3);
        PenDash(pnR, 2); PenDashOff(pnR, 1000 - DecT(now) * 0.02 % 1000);
        Ell(BBX - 38.8, BBY - 38.8, 77.6, 77.6, pnR);
        double tb = -DecT(now) * 0.012;
        for (int i = 1; i <= 12; i++)
        {
            double a_ = (tb + i * 30) * 0.017453, tw = (Math.Sin(DecT(now) * 0.002 + i * 0.52) + 1) / 2;
            double al = en2 ? 45 + 70 * tw : 38 + 42 * tw;
            Line(BBX + 40 * Math.Cos(a_), BBY + 40 * Math.Sin(a_), BBX + 42.6 * Math.Cos(a_), BBY + 42.6 * Math.Sin(a_), Pen(FA(Alpha(bcol, R(al)), f), 1.3));
        }
        if (en2)
        {
            double ha = head * 0.017453;
            FillEll(BBX + 35.1 * Math.Cos(ha) - 2.2, BBY + 35.1 * Math.Sin(ha) - 2.2, 4.4, 4.4, SBrush(FA(Alpha(AccHi(acc, 0.6), 255), f)));
            for (int k = 0; k < 3; k++)
            {
                double twA = (k * 120 + DecT(now) * 0.02) * 0.017453;
                double twI = Math.Pow(Math.Max(0.0, Math.Sin(DecT(now) * 0.003 + k * 2.1)), 9);
                FillEll(BBX + 35 * Math.Cos(twA) - 1.4, BBY + 35 * Math.Sin(twA) - 1.4, 2.8, 2.8, SBrush(FA(Alpha(0xFFFFFF, R(200 * twI)), f)));
            }
        }
        if (hx > 0.01)                                                            // hover: a ping off the bubble
        {
            double prt = DecT(now) % 950 / 950.0, r_ = 31 + prt * 11;
            Ell(BBX - r_, BBY - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(acc, 130 * hx * (1 - prt)), f), 2.2 * (1 - prt) + 0.4));
        }
        if (TogDir == 1 && tp > 0.5 && tp < 1)                                    // the toggle's own ring, thrown as the ignition finishes
        {
            double e = (tp - 0.5) / 0.5, r_ = 30 + e * 22;
            Ell(BBX - r_, BBY - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(acc, 150 * (1 - e)), f), 2.4 * (1 - e) + 0.4));
        }
        // ---- opening: the scatter ring; landing: the burst ----
        if (sct >= 0)
        {
            double e = Ease3(sct), r_ = 30 + e * 12;
            Ell(BBX - r_, BBY - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(0xFFFFFF, 190 * (1 - e)), f), 2.6 * (1 - e) + 0.4));
        }
        if (BurstAt != 0 && now >= BurstAt)
        {
            double btm = (now - BurstAt) / 450.0;
            if (btm >= 1) BurstAt = 0;
            else
            {
                double e = 1 - Math.Pow(1 - btm, 3), r_ = 30 + e * 18;
                Ell(BBX - r_, BBY - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(acc, 170 * (1 - e)), f), 2.6 * (1 - e) + 0.4));
            }
        }
        // the 2x2 grid button, as the hub's own pill carries
        double qx = BBX + 12, qy = BBY + 12;
        FillEll(qx + 1, qy + 2.5, 20, 19, SBrush(FA(0x59000000, f)));
        FillRR(qx, qy, 20, 20, 6, VBrush(qx, qy, 20, 20, FA(0xFF3A3D58, f), FA(0xFF20223A, f)));
        MicroBackdrop(qx, qy, 20, 20, 6, acc, f, now, 0.8);
        StrokeRR(qx, qy, 20, 20, 6, Pen(FA(Alpha(acc, 165), f), 1));
        var qb = SBrush(FA(Alpha(acc, 240), f));
        for (int q = 1; q <= 4; q++) FillRR(qx + 5.4 + (q - 1) % 2 * 5.4, qy + 5.4 + (q - 1) / 2 * 5.4, 3.8, 3.8, 1.1, qb);
    }
    static void Grip(double gx, double gy, double t, uint acc, double f)
    {
        for (int i = 0; i < 6; i++) { double dx = gx - 10 + (i % 3) * 10, dy = gy - 4 + (i / 3) * 8; FillEll(dx - 1.6, dy - 1.6, 3.2, 3.2, SBrush(FA(Alpha(Mix(0xFF9AA8C0, AccHi(acc, 0.4), t), R(120 + 110 * t)), f))); }
    }
    /// <summary>The footer's key chip, as the .ahk draws it under both cards.</summary>
    protected void KeyChip(double x, double y, double w, string label, bool listening, uint acc, double f, double hv, long flashAt, long now)
    {
        FillRR(x, y, w, 20, 6, SBrush(FA(Mix(0xFF171A30, 0xFF232744, hv), f)));
        MicroBackdrop(x, y, w, 20, 6, acc, f, now, 0.9);
        StrokeRR(x, y, w, 20, 6, Pen(FA(Alpha(acc, R(120 + 110 * hv)), f), 1));
        TxtP(listening ? "???" : label, x, y, w, 20, Fonts.fBadge, FA(Alpha(acc, R(225 + 30 * hv)), f), Fmt.C);
        if (flashAt != 0 && now - flashAt < 520) { double e = Ease3((now - flashAt) / 520.0), ex = e * 9; StrokeRR(x - ex, y - ex * 0.5, w + ex * 2, 20 + ex, 6 + ex * 0.4, Pen(FA(Alpha(acc, R(160 * (1 - e))), f), 1.8 * (1 - e) + 0.3)); }
    }
    public static LayeredWindow Open(FskCard card, string title)
    {
        // ---- ShowActivated only ----
        // These cards used to carry WS_EX_NOACTIVATE as well, copied from the
        // .ahk. It stops a CLICK activating the window, and the .ahk can afford
        // that because it draws with GDI on a timer and nothing throttles it.
        // Here the window is composited, so a process that can never reach the
        // foreground gets held at whatever a background frame cap is set to -
        // and since clicking cannot activate it, nothing gets it back. Asking
        // for the foreground from a window the system will not activate does not
        // work either: Windows refuses SetForegroundWindow to a process that
        // does not already hold it.
        //
        // ShowActivated = false is the half that was actually wanted, and it is
        // enough: APPEARING never takes the game's input, which is what an
        // overlay must not do. Being CLICKED does, which is what any window
        // does, and is the only way the person can interact with it at full
        // rate. The markers keep the style - they are click-through and never
        // receive one.
        var win = new LayeredWindow(card, title, topmost: true, toolWindow: true) { ShowActivated = false };
        win.Show(); card.Place(); card.Tim(Pace.TICK_A);
        return win;
    }
}

/// <summary>
/// The block's overlay (the .ahk's Render / DrawCard): the portrait, the
/// mouse with its chevrons feeding the Q keycap, the ENABLED / DISABLED
/// pill, and the keybind row with the shield.
/// </summary>
public sealed class AbCard : FskCard
{
    protected override bool Armed => Ab.Enabled;
    protected override double StateT => AnimT;
    double _stT => AnimT;
    public AbCard() : base("ab") { }
    protected override int KeyZone(double ux, double uy)
    {
        double bw = Keys.ChipW(Keys.RebindOn && Keys.RebindTgt == 1 ? "???" : Keys.Label(Keys.BindKey));
        return ux >= PAD + 24 && ux <= PAD + 24 + bw && uy >= PAD + 106 && uy <= PAD + 126 ? 6 : 0;
    }
    protected override void CardPress(int z) { if (z == 6) Keys.RebindClick(1); }
    protected override void Orbit(double ax, double ay, double f, long now, uint acc)
    {
        if (_stT < 0.5) { base.Orbit(ax, ay, f, now, acc); return; }
        double sa = DecT(now) * 0.09 % 360; uint cc = AccHi(acc, 0.4);
        Arc(ax - 26.5, ay - 26.5, 53, 53, sa, 64, Pen(FA(Alpha(cc, 80), f), 2.5));
        Arc(ax - 26.5, ay - 26.5, 53, 53, sa + 24, 40, Pen(FA(Alpha(cc, 160), f), 2.5));
        Arc(ax - 26.5, ay - 26.5, 53, 53, sa + 44, 20, Pen(FA(Alpha(cc, 245), f), 2.7));
        Arc(ax - 26.5, ay - 26.5, 53, 53, 360 - DecT(now) * 0.06 % 360, 40, Pen(FA(Alpha(acc, 90), f), 1.8));
    }
    protected override void Frame(long now)
    {
        if (!Ab.On && CloseAt == 0) CloseAt = now;
        // no EK ease here: AnimT is driven over ANIM_MS by Body, so an armed card
        // opened fresh has to start at its own state rather than walk up to it
        if (AnimStart == 0 && AnimT != (Ab.Enabled ? 1.0 : 0.0) && TogAt == 0) { AnimT = Ab.Enabled ? 1.0 : 0.0; AnimTo = AnimT; }
        uint acc = Mix(HubState.Accent, 0xFF34D399, _stT);
        Body(now, acc, (f, n, s2, s3, s4, tg) =>
        {
            double cx = PAD, cy = PAD, dm = 1 - _stT;
            double pulse = Ab.Enabled ? (Math.Sin(DecT(n) * 0.00417) + 1) / 2 : 0, emb2 = (Math.Sin(DecT(n) * 0.004) + 1) / 2;
            double hb = DecT(n) % 2200; double blink = hb < 140 || (hb > 300 && hb < 440) ? 1 : 0;
            double rowY = cy + 56;
            double clf = Ab.LastClick != 0 ? (n - Ab.LastClick) / 260.0 : 2, fe = clf < 1 ? 1 - Math.Pow(1 - clf, 3) : 1;
            // the mouse
            double mx = cx + 88, my = rowY - 15 + (Ab.Enabled ? 0.8 * Math.Sin(DecT(n) * 0.004) : 0.5 * Math.Sin(DecT(n) * 0.0028));
            FillRR(mx, my, 20, 30, 9, SBrush(FA(0x12FFFFFF, f * s3)));
            int stM = PushG(); ClipRR(mx, my, 20, 30, 9);
            FillRect(mx, my, 10, 13, SBrush(FA(Alpha(acc, R(Math.Min(255, 205 + 50 * pulse + 24 * emb2 * dm + 45 * (1 - fe)))), f * s3)));
            Pop(stM);
            StrokeRR(mx, my, 20, 30, 9, Pen(FA(0x5CFFFFFF, f * s3), 1.4));
            var pnM = Pen(FA(0x3CFFFFFF, f * s3), 1);
            Line(mx + 10, my, mx + 10, my + 13, pnM); Line(mx, my + 13, mx + 20, my + 13, pnM);
            FillRR(mx + 8.4, my + 4, 3.2, 6.2, 1.6, SBrush(FA(Alpha(0xFFFFFF, R(70 + 90 * pulse * _stT + 30 * emb2 * dm)), f * s3)));
            if (clf < 1) { double r_ = 4 + fe * 14; Ell(mx + 5 - r_, my + 6.5 - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(acc, R(180 * (1 - fe))), f * s3), 2 * (1 - fe) + 0.4)); }
            // the chevrons
            for (int k = 0; k < 3; k++)
            {
                double x0 = mx + 25 + k * 6;
                double ca = Ab.Enabled ? 70 + 185 * Math.Pow(Math.Max(0.0, Math.Sin(DecT(n) * 0.005 - k * 0.9)), 3) : 50 + 85 * Math.Pow(Math.Max(0.0, Math.Sin(DecT(n) * 0.0025 - k * 0.9)), 3);
                var pnC = Pen(FA(Alpha(Mix(0xFFE8EAF6, acc, 0.35 * _stT), R(ca)), f * s3), 1.8);
                Line(x0, rowY - 3.8, x0 + 3.6, rowY, pnC); Line(x0 + 3.6, rowY, x0, rowY + 3.8, pnC);
            }
            if (Ab.Enabled) for (int i = 0; i < 2; i++) { double pph = (DecT(n) + i * 450) % 900 / 900.0; FillEll(mx + 22 + 26 * pph - 1.6, rowY - 1.6, 3.2, 3.2, SBrush(FA(Alpha(AccHi(acc, 0.4), R(235 * Math.Sin(3.14159 * pph))), f * s3))); }
            // the Q keycap in its well
            double kx = mx + 50;
            FillRR(kx - 1.5, rowY - 12.5, 29, 26, 8, SBrush(FA(0xFF171A30, f * s3)));
            double ky = rowY - 13 + 1.2 * _stT + 0.9 * pulse + 2.2 * (1 - fe);
            FillRR(kx, ky, 26, 26, 7, VBrush(kx, ky, 26, 26, FA(0xFF31344C, f * s3), FA(0xFF20223A, f * s3)));
            MicroBackdrop(kx, ky, 26, 26, 7, acc, f * s3, n, 0.85);
            StrokeRR(kx, ky, 26, 26, 7, Pen(FA(Mix(0x30FFFFFF, Alpha(acc, 130), _stT), f * s3), 1));
            Txt("Q", kx, ky - 1, 26, 26, Fonts.fKey, FA(Alpha(acc, 235), f * s3), Fmt.C);
            // the label and the state pill
            FillEll(cx + 89, cy + 75.5, 4, 4, SBrush(FA(Alpha(acc, R(Math.Min(255, (120 + 120 * (Math.Sin(DecT(n) * 0.006) + 1) / 2) * Math.Max(_stT, 0.35) + 50 * blink * dm))), f * s3)));
            Txt("external block", cx + 98, cy + 70, 150, 16, Fonts.fHint, FA(0x62C7CBE0, f * s3), Fmt.L);
            double pw_ = Lerp(Fonts.wOff, Fonts.wOn, _stT) + 46, px1 = cx + CW - 20 - pw_;
            double tp = TogAt != 0 ? Math.Min((n - TogAt) / 650.0, 1.0) : 1.0, ps = tp < 1 ? 1 + 0.07 * Math.Sin(3.14159 * Math.Min(tp * 1.5, 1.0)) : 1;
            int stP = PushXform(px1 + pw_ / 2, rowY, ps, 0);
            FillRR(px1, rowY - 14, pw_, 28, 14, VBrush(px1, rowY - 14, pw_, 28, FA(Alpha(acc, 58), f * s3), FA(Alpha(acc, 26), f * s3)));
            MicroBackdrop(px1, rowY - 14, pw_, 28, 14, acc, f * s3, n, 0.85);
            StrokeRR(px1, rowY - 14, pw_, 28, 14, Pen(FA(Alpha(acc, 105), f * s3), 1));
            double ddx = px1 + 15;
            if (Ab.Enabled) for (int q = 1; q <= 3; q++) { double r_ = 5 + q * (2.6 + 2.2 * pulse); FillEll(ddx - r_, rowY - r_, r_ * 2, r_ * 2, SBrush(FA(Alpha(acc, R(26 - q * 6)), f * s3))); }
            FillEll(ddx - 4, rowY - 4, 8, 8, SBrush(FA(Alpha(acc, R(Ab.Enabled ? 250 : Math.Min(255, 165 + 30 * emb2 + 50 * blink))), f * s3)));
            FillEll(ddx - 2.4, rowY - 2.6, 1.8, 1.8, SBrush(FA(0x8CFFFFFF, f * s3)));
            // the label crossfades THROUGH the change and slides as it does,
            // rather than swapping under a pill that is still moving
            double txtA = AnimStart != 0 ? Math.Abs(_stT * 2 - 1) : 1.0;
            Txt(_stT >= 0.5 ? "ENABLED" : "DISABLED", ddx + 10 + 3 * (1 - txtA), rowY - 12, pw_ - 34, 24, Fonts.fStatus, FA(Alpha(acc, R(245 * txtA)), f * s3), Fmt.L);
            if (RippleStart != 0)                                                // the toggle's ring, off the status dot
            {
                double rt = (n - RippleStart) / 480.0;
                if (rt < 1)
                {
                    double e = 1 - Math.Pow(1 - rt, 3), r_ = 5 + e * 24;
                    Ell(ddx - r_, rowY - r_, r_ * 2, r_ * 2, Pen(FA(Alpha(acc, R(150 * (1 - e))), f * s3), 2.4 * (1 - e) + 0.4));
                }
            }
            Pop(stP);
            // the footer
            double hy = cy + 116, bw_ = Keys.ChipW(Keys.RebindOn && Keys.RebindTgt == 1 ? "???" : Keys.Label(Keys.BindKey)), hv6 = h4T;
            KeyChip(cx + 24, hy - 10, bw_, Keys.Label(Keys.BindKey), Keys.RebindOn && Keys.RebindTgt == 1, acc, f * s4, hv6, Keys.BindFlashAt, n);
            Txt(Keys.RebindOn && Keys.RebindTgt == 1 ? "press any key" : "external block keybind", cx + 24 + bw_ + 10, hy - 10, 190, 20, Fonts.fHint, FA(0x6EC7CBE0, f * s4), Fmt.L);
            var sh = Shield(cx + CW - 37, hy - 8, 13, 16);
            FillPath(sh, SBrush(FA(Alpha(acc, R(70 + 60 * _stT)), f * s4)));
        });
    }
    static Avalonia.Media.Geometry Shield(double x, double y, double w, double h)
    {
        var g = new Avalonia.Media.StreamGeometry();
        using (var c = g.Open()) { c.BeginFigure(new Point(x + w / 2, y), true); c.LineTo(new Point(x + w, y + h * 0.22)); c.LineTo(new Point(x + w, y + h * 0.55)); c.QuadraticBezierTo(new Point(x + w * 0.95, y + h * 0.85), new Point(x + w / 2, y + h)); c.QuadraticBezierTo(new Point(x + w * 0.05, y + h * 0.85), new Point(x, y + h * 0.55)); c.LineTo(new Point(x, y + h * 0.22)); c.EndFigure(true); }
        return g;
    }
}

/// <summary>
/// PUZZLE AI's overlay (PuzOvDrawCard): the portrait, the puzzles-solved
/// counter, the speed slider that is the same number as the hub's, and the
/// solve / grid keybind row.
/// </summary>
public sealed class PuzCard : FskCard
{
    public PuzCard() : base("puz") { }
    protected override bool Armed => Puz.Solving;
    protected override double StateT => _solvT;
    double _solvT;
    const double SlX = PAD + 88, SlW = CW - 108, SlY = PAD + 90;
    static (double b1x, double b1w, double l1x, double b2x, double b2w, double l2x) KeyGeom()
    {
        double b1x = PAD + 24, b1w = Keys.ChipW(Keys.RebindOn && Keys.RebindTgt == 2 ? "???" : Keys.Label(Keys.PuzKey));
        double l1x = b1x + b1w + 9, b2x = l1x + Fonts.MeasureW("solve", Fonts.fHint) + 16;
        double b2w = Keys.ChipW(Keys.RebindOn && Keys.RebindTgt == 3 ? "???" : Keys.Label(Keys.MkKey)), l2x = b2x + b2w + 9;
        return (b1x, b1w, l1x, b2x, b2w, l2x);
    }
    protected override int KeyZone(double ux, double uy)
    {
        var g = KeyGeom();
        if (uy >= PAD + 106 && uy <= PAD + 126)
        {
            if (ux >= g.b1x && ux <= g.b1x + g.b1w) return 6;
            if (ux >= g.b2x && ux <= g.b2x + g.b2w) return 8;
        }
        if (ux >= SlX - 8 && ux <= SlX + SlW + 8 && uy >= SlY - 11 && uy <= SlY + 11) return 9;
        return 0;
    }
    protected override void CardPress(int z)
    {
        if (z == 9) { Puz.SlideSet((PtrX - SlX) / SlW); _sld = true; return; }
        if (z == 6) Keys.RebindClick(2);
        else if (z == 8) Keys.RebindClick(3);
    }
    protected override void CardMove(double ux, double uy) { if (_sld && Pressed) { Puz.SlideSet((ux - SlX) / SlW); Tim(Pace.TICK_A); } }
    protected override void CardRelease(int z) { _sld = false; }
    bool _sld;
    protected override void Orbit(double ax, double ay, double f, long now, uint acc)
    {
        if (!Puz.Solving) { base.Orbit(ax, ay, f, now, acc); return; }
        double sa = DecT(now) * 0.09 % 360; uint cc = AccHi(acc, 0.4);
        Arc(ax - 26.5, ay - 26.5, 53, 53, sa, 64, Pen(FA(Alpha(cc, 80), f), 2.5));
        Arc(ax - 26.5, ay - 26.5, 53, 53, sa + 24, 40, Pen(FA(Alpha(cc, 160), f), 2.5));
        Arc(ax - 26.5, ay - 26.5, 53, 53, sa + 44, 20, Pen(FA(Alpha(cc, 245), f), 2.7));
    }
    static int Total() => (int)Ini.ReadInt(Paths.IniFile, "puzzle", "solved", 0);
    protected override void Frame(long now)
    {
        if (!Puz.On && CloseAt == 0) CloseAt = now;
        _solvT += ((Puz.Solving ? 1.0 : 0.0) - _solvT) * EK(0.2);
        uint acc = HubState.Accent;
        Body(now, acc, (f, n, s2, s3, s4, tg) =>
        {
            double cx = PAD, cy = PAD, emb2 = (Math.Sin(DecT(n) * 0.006) + 1) / 2;
            double rowY = cy + 48;
            FillEll(cx + 89, rowY - 2, 4, 4, SBrush(FA(Alpha(acc, R(120 + 120 * emb2)), f * s3)));
            Txt("puzzles solved", cx + 98, rowY - 8, 120, 16, Fonts.fHint, FA(0x62C7CBE0, f * s3), Fmt.L);
            string cnt = Total().ToString();
            double pw_ = Math.Max(78, Fonts.MeasureW(cnt, Fonts.fStatus) + 46), px1 = cx + CW - 20 - pw_;
            FillRR(px1, rowY - 14, pw_, 28, 14, VBrush(px1, rowY - 14, pw_, 28, FA(Alpha(acc, 58), f * s3), FA(Alpha(acc, 26), f * s3)));
            MicroBackdrop(px1, rowY - 14, pw_, 28, 14, acc, f * s3, n, 0.85);
            StrokeRR(px1, rowY - 14, pw_, 28, 14, Pen(FA(Alpha(acc, 105), f * s3), 1));
            double ddx = px1 + 15;
            if (Puz.Solving) for (int q = 1; q <= 3; q++) { double r_ = 5 + q * (2.6 + 2.2 * emb2); FillEll(ddx - r_, rowY - r_, r_ * 2, r_ * 2, SBrush(FA(Alpha(acc, R(26 - q * 6)), f * s3))); }
            FillEll(ddx - 4, rowY - 4, 8, 8, SBrush(FA(Alpha(acc, R(Puz.Solving ? 250 : 165 + 30 * emb2)), f * s3)));
            FillEll(ddx - 2.4, rowY - 2.6, 1.8, 1.8, SBrush(FA(0x8CFFFFFF, f * s3)));
            if (Puz.Solving) { double oa = DecT(n) * 0.0042; FillEll(ddx + 7.5 * Math.Cos(oa) - 1.3, rowY + 7.5 * Math.Sin(oa) - 1.3, 2.6, 2.6, SBrush(FA(Alpha(AccHi(acc, 0.5), 220), f * s3))); }
            Txt(cnt, ddx + 10, rowY - 12, pw_ - 34, 24, Fonts.fStatus, FA(Alpha(acc, 245), f * s3), Fmt.L);
            // the speed row
            double slx = SlX, sly = SlY, hvS = ZoneAt(PtrX, PtrY) == 9 ? 1.0 : 0.0;
            Txt("speed", cx + 88, sly - 25, 44, 16, Fonts.fHint, FA(0x62C7CBE0, f * s3), Fmt.L);
            Txt(Puz.SpeedPct + "%  \u00B7  " + Math.Round(1000 / Puz.FrameMs) + " fps", cx + 140, sly - 24, CW - 160, 14, HubSurface.Live?.HL.fXs ?? Fonts.fBadge, FA(Alpha(Puz.SpeedT > 0.72 ? 0xFFFBBF24 : 0xFFC7CBE0, 170), f * s3), Fmt.R);
            FillRR(slx, sly - 1.5, SlW, 3, 1.5, SBrush(FA(Alpha(0xFFFFFF, 26), f * s3)));
            FillRR(slx, sly - 1.5, SlW * Puz.SpeedT, 3, 1.5, SBrush(FA(Alpha(acc, 190), f * s3)));
            for (int q = 0; q < 2; q++) FillRR(slx + q * SlW, sly - 5, 1, 10, 0.5, SBrush(FA(Alpha(0xFFFFFF, 40), f * s3)));
            double knx = slx + SlW * Puz.SpeedT, kr2 = 6 + 1.6 * hvS + (_sld ? 1.4 : 0);
            FillEll(knx - kr2, sly - kr2 + 1.5, kr2 * 2, kr2 * 2, SBrush(FA(Alpha(0x000000, 90), f * s3)));
            FillEll(knx - kr2, sly - kr2, kr2 * 2, kr2 * 2, SBrush(FA(Alpha(0xFEFEFE, 245), f * s3)));
            FillEll(knx - 2.2, sly - 2.2, 4.4, 4.4, SBrush(FA(Alpha(acc, 215), f * s3)));
            // the footer: solve and grid
            var g = KeyGeom(); double hy = cy + 116; int hzn = ZoneAt(PtrX, PtrY);
            KeyChip(g.b1x, hy - 10, g.b1w, Keys.Label(Keys.PuzKey), Keys.RebindOn && Keys.RebindTgt == 2, acc, f * s4, hzn == 6 ? 1.0 : 0.0, Keys.PuzFlashAt, n);
            KeyChip(g.b2x, hy - 10, g.b2w, Keys.Label(Keys.MkKey), Keys.RebindOn && Keys.RebindTgt == 3, acc, f * s4, hzn == 8 ? 1.0 : 0.0, Keys.MkFlashAt, n);
            if (Keys.RebindOn && (Keys.RebindTgt == 2 || Keys.RebindTgt == 3)) Txt("press any key", cx + CW - 130, hy - 10, 110, 20, Fonts.fHint, FA(Alpha(acc, 190), f * s4), Fmt.R);
            else
            {
                Txt("solve", g.l1x, hy - 10, g.b2x - g.l1x - 8, 20, Fonts.fHint, FA(0x6EC7CBE0, f * s4), Fmt.L);
                Txt("grid", g.l2x, hy - 10, cx + CW - 20 - g.l2x, 20, Fonts.fHint, FA(0x6EC7CBE0, f * s4), Fmt.L);
            }
        });
    }
}

/// <summary>The grid markers (PuzMarkersBuild): a click-through window over the board - the outer box, the four corner arms, a mark at every cell centre.</summary>
public sealed class MarkerSurface : Surface
{
    public double Ox, Oy;
    public MarkerSurface(double w, double h) : base(w, h, 1.0) { }
    protected override int ZoneAt(double ux, double uy) => 0;
    protected override void Frame(long now)
    {
        if (!OperatingSystem.IsWindows()) return;
        uint acc = HubState.Accent;
        int n = Puz.GridN; double sx = PuzSolver.SX, sy = PuzSolver.SY;
        double bx = PuzSolver.PX(1) - sx / 2 - Ox, by = PuzSolver.PY(1) - sy / 2 - Oy, bw = sx * n, bh = sy * n;
        StrokeRR(bx, by, bw, bh, 6, Pen(Alpha(acc, 42), 1));
        double arm = Math.Min(16, Math.Min(bw, bh) / 5);
        var pnA = Pen(Alpha(AccHi(acc, 0.35), 190), 1.6);
        for (int k = 0; k < 4; k++) { double cx = k == 1 || k == 2 ? bx + bw : bx, cy = k >= 2 ? by + bh : by, dxs = k == 1 || k == 2 ? -1 : 1, dys = k >= 2 ? -1 : 1; Line(cx, cy, cx + dxs * arm, cy, pnA); Line(cx, cy, cx, cy + dys * arm, pnA); }
        for (int r = 1; r <= n; r++) for (int c = 1; c <= n; c++)
        {
            double px = PuzSolver.PX(c) - Ox, py = PuzSolver.PY(r) - Oy;
            FillEll(px - 6, py - 6, 12, 12, SBrushP(Alpha(acc, 26)));
            FillEll(px - 2.4, py - 2.4, 4.8, 4.8, SBrushP(Alpha(AccHi(acc, 0.3), 225)));
            FillEll(px - 0.9, py - 0.9, 1.8, 1.8, SBrushP(Alpha(0xFFFFFF, 200)));
        }
        Tim(0);
    }
}

/// <summary>
/// GRID SETUP (PuzCalCard): the small card the .ahk parks at the top of the
/// primary screen while SET GRID waits for the board's two outer corners -
/// the brand, ESC cancels, the reticle, the instruction and its note, and
/// the warning line when a click was refused. Clicking the card itself
/// nudges it to the other end of the screen instead of taking the corner.
/// </summary>
public sealed class CalCard : Surface
{
    public const double W = 470, H = 218, PADC = 22;
    public string Warn = ""; public long WarnAt, OpenAt = Clock.Tick;
    /// <summary>PZ_calStepAt: a STEP change replays the middle of the card rather than the whole thing.</summary>
    public long StepAt = Clock.Tick; public int Step = 1;
    public CalCard() : base(W + PADC * 2, H + PADC * 2, UiScale.Factor) { }
    protected override int ZoneAt(double ux, double uy) => 0;
    protected override void Frame(long now)
    {
        uint acc = HubState.Accent;
        if (Step != Puz.Cal) { Step = Puz.Cal; StepAt = now; }
        double it = Math.Min((now - OpenAt) / 420.0, 1.0), ei = 1 - Math.Pow(1 - it, 3);
        double f = Math.Min(it * 1.7, 1.0);
        long ims = now - OpenAt; bool stg = ims < 800;
        double s1 = stg ? Ease3(Clamp((ims - 40) / 360.0, 0.0, 1.0)) : 1.0, s2 = stg ? Ease3(Clamp((ims - 130) / 360.0, 0.0, 1.0)) : 1.0;
        double s3 = stg ? Ease3(Clamp((ims - 220) / 360.0, 0.0, 1.0)) : 1.0;
        double pulse = (Math.Sin(DecT(now) * 0.005) + 1) / 2;
        double kick = StepAt != 0 && now - StepAt < 520 ? 1 - Ease3((now - StepAt) / 520.0) : 0.0;
        // the warning holds for 3.2 s and then fades over the last 800 ms
        double warnT = Warn != "" && WarnAt != 0 && now - WarnAt < 4000 ? Clamp(1 - (now - WarnAt - 3200) / 800.0, 0.0, 1.0) : 0.0;
        uint edge = warnT > 0.01 ? 0xFFFBBF24 : acc;
        double cx = PADC, cy = PADC;
        int stCard = PushXform(cx + W / 2, cy + H / 2, 0.955 + 0.045 * ei, 0);
        ShadowDraw(cx, cy + 3, W, H, 16, 10, 8, 8, f);
        FillRR(cx, cy, W, H, 16, VBrush(cx, cy, W, H, FA(0xF2222438, f), FA(0xF8121423, f)));
        int stC = PushG(); ClipRR(cx, cy, W, H, 16);
        PanelBackdrop(cx, cy, W, H, acc, f, now, 0.95);
        FillRect(cx, cy, W, H, LineBrush(cx - 1, cy - 1, W + 2, H + 2, FA(0x0EFFFFFF, f), 0x00FFFFFF, 2));
        // travelling scan band - brisk, because this card is waiting on you
        double per = 2600.0, swx = cx - 26 + (W + 52) * (DecT(now) % per / per);
        uint c0s = AccHi(acc, 0.3) & 0xFFFFFF, c1s = FA(Alpha(c0s, 38), f);
        FillRect(swx - 24, cy, 24, H, LineBrush(swx - 25, cy - 2, 26, H + 4, c0s, c1s, 0));
        FillRect(swx, cy, 24, H, LineBrush(swx - 1, cy - 2, 26, H + 4, c1s, c0s, 0));
        FillRect(cx, cy, 14, H, SBrush(FA(Alpha(edge, Math.Min(255, 46 + 24 * pulse + 70 * warnT)), f)));
        FillRect(cx, cy, 4.5, H, VBrush(cx, cy, 4.5, H, FA(Alpha(edge, 245), f), FA(Alpha(edge, 120), f)));
        Pop(stC);
        StrokeRR(cx, cy, W, H, 16, Pen(FA(0x28FFFFFF, f), 1));
        if (warnT > 0.01) StrokeRR(cx + 0.8, cy + 0.8, W - 1.6, H - 1.6, 15, Pen(FA(Alpha(0xFFFBBF24, R(200 * warnT)), f), 1.5));
        Line(cx + 18, cy + 1.5, cx + W - 18, cy + 1.5, Pen(FA(0x16FFFFFF, f), 1));
        Line(cx + 18, cy + H - 1.5, cx + W - 18, cy + H - 1.5, Pen(FA(0x30000000, f), 1));
        for (int k = 0; k < 4; k++)
        {
            double shim = (Math.Sin(DecT(now) * 0.006 + k * 1.5708) + 1) / 2, ga = Math.Min(255, 120 + 40 * pulse + 58 * shim + 90 * kick);
            double xk = k == 1 || k == 2 ? cx + W - 31.5 : cx + 0.5, yk = k >= 2 ? cy + H - 31.5 : cy + 0.5;
            double ang = k == 0 ? 195 : k == 1 ? 285 : k == 2 ? 15 : 105;
            Arc(xk, yk, 31, 31, ang, 60, Pen(FA(Alpha(edge, R(ga)), f), 2));
        }
        double hdy = -(1 - s1) * 6;
        Txt(AppInfo.AppName, cx + 14, cy + 8 + hdy, 90, 20, Fonts.fBrand, FA(0xCFE8EAF6, f * s1), Fmt.L);
        FillEll(cx + 14 + Fonts.MeasureW(AppInfo.AppName, Fonts.fBrand) + 6, cy + 15.5 + hdy, 5, 5, SBrush(FA(Alpha(acc, R(160 + 90 * pulse)), f * s1)));
        Txt("GRID SETUP", cx + 14, cy + 24 + hdy, 200, 16, Fonts.fBadge, FA(Alpha(AccHi(acc, 0.4), 215), f * s1), Fmt.L);
        double ew = 92, ex = cx + W - 20 - ew, ey = cy + 12 + hdy;
        FillRR(ex, ey, ew, 20, 7, SBrush(FA(Alpha(0xFFFFFF, 12), f * s1)));
        MicroBackdrop(ex, ey, ew, 20, 7, acc, f * s1, now, 0.8);
        StrokeRR(ex, ey, ew, 20, 7, Pen(FA(Alpha(acc, 90), f * s1), 1));
        FillRR(ex + 7, ey + 4, 26, 12, 3, VBrush(ex + 7, ey + 4, 26, 12, FA(0xFF343752, f * s1), FA(0xFF1C1E33, f * s1)));
        StrokeRR(ex + 7, ey + 4, 26, 12, 3, Pen(FA(Alpha(acc, 130), f * s1), 1));
        TxtP("ESC", ex + 7, ey + 2, 26, 15, Fonts.fBadge, FA(0xE0E8EAF6, f * s1), Fmt.C);
        Txt("cancels", ex + 39, ey + 2, ew - 44, 16, Fonts.fHint, FA(0x9AC7CBE0, f * s1), Fmt.L);
        FadeLine(cx + 20, cx + W - 20, cy + 48, 0x30FFFFFF, f * s1);
        Reticle(cx + 56, cy + 92 + (1 - s2) * 8, 26, acc, f * s2, now, Puz.Cal);
        double tx = cx + 104 + (1 - s3) * 10, tw = W - 124;
        string ttl = Puz.Cal == 1 ? "Click the TOP-LEFT corner of the board" : "Now click the BOTTOM-RIGHT corner";
        Txt(FFMElide(ttl, HubSurface.Live?.HL.fV ?? Fonts.fStatus, tw), tx, cy + 56, tw, 26, HubSurface.Live?.HL.fV ?? Fonts.fStatus, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.18), 245), f * s3), Fmt.L);
        string sub = Warn != "" && warnT > 0.01 ? Warn : Puz.Cal == 1 ? "The outer corner of the whole grid, not the first circle." : "The opposite outer corner, diagonally across from the first.";
        if (warnT > 0.01)                                                        // a caution triangle, drawn rather than fonted so it takes the fade
        {
            double wx = tx + 5, wy = cy + 92;
            var pnW = Pen(FA(Alpha(AccHi(0xFFFBBF24, 0.35), 235), f * s3), 1.3);
            Line(wx, wy - 5, wx + 5, wy + 4, pnW); Line(wx + 5, wy + 4, wx - 5, wy + 4, pnW); Line(wx - 5, wy + 4, wx, wy - 5, pnW);
        }
        double subOff = warnT > 0.01 ? 18 : 0;
        Txt(FFMElide(sub, Fonts.fHint, tw - subOff), tx + subOff, cy + 84, tw, 18, Fonts.fHint,
            FA(Alpha(warnT > 0.01 ? 0xFFFBBF24 : 0xFFC7CBE0, 200), f * s3), Fmt.L);
        FadeLine(cx + 20, cx + W - 20, cy + H - 44, 0x1EFFFFFF, f * s3);
        Txt("the hub is hidden until this finishes", cx + 104, cy + H - 34, W - 124, 18, Fonts.fHint, FA(0x62C7CBE0, f * s3), Fmt.L);
        Pop(stCard);
        Tim(Pace.TICK_A);
    }
    static void Reticle(double cx, double cy, double r, uint acc, double f, long now, int step)
    {
        Ell(cx - r, cy - r, r * 2, r * 2, Pen(FA(Alpha(acc, 70), f), 1));
        double sp = DecT(now) * 0.06 % 360;
        Arc(cx - r, cy - r, r * 2, r * 2, sp, 70, Pen(FA(Alpha(AccHi(acc, 0.4), 210), f), 2));
        var pn = Pen(FA(Alpha(acc, 150), f), 1.2);
        Line(cx - r - 6, cy, cx - r + 8, cy, pn); Line(cx + r - 8, cy, cx + r + 6, cy, pn);
        Line(cx, cy - r - 6, cx, cy - r + 8, pn); Line(cx, cy + r - 8, cx, cy + r + 6, pn);
        double bx = cx - 11, by = cy - 11;
        StrokeRR(bx, by, 22, 22, 3, Pen(FA(Alpha(acc, 120), f), 1));
        double hx = step == 1 ? bx : bx + 22, hy = step == 1 ? by : by + 22;
        FillEll(hx - 3.4, hy - 3.4, 6.8, 6.8, SBrush(FA(Alpha(AccHi(acc, 0.45), R(180 + 70 * (Math.Sin(DecT(now) * 0.006) + 1) / 2)), f)));
    }
}

[SupportedOSPlatform("windows")]
public static class FskOverlays
{
    [DllImport("user32.dll")] static extern int GetWindowLongW(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLongW(IntPtr h, int i, int v);
    static LayeredWindow? _ab, _puz, _mk, _cal; static AbCard? _abCard; static PuzCard? _puzCard; static MarkerSurface? _mkS; static CalCard? _calS; static int _calSlot = 1;
    public static bool MkWant;
    public static void Load() { MkWant = Ini.ReadInt(Paths.IniFile, "puzzle", "markers", 0) != 0; }
    /// <summary>WS_EX_NOACTIVATE: the window takes clicks without ever taking the foreground.</summary>
    internal static void NoActivate(Window w) { NoActivate(w, true); }
    /// <summary>
    /// On, and off again. The style exists to protect a GAME from losing its
    /// input to an overlay - with no client running there is nothing to protect
    /// and nothing to justify a window the person cannot focus, so it comes off
    /// and the card behaves like any other window.
    /// </summary>
    internal static void NoActivate(Window w, bool on)
    {
        try
        {
            var h = w.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (h == IntPtr.Zero) return;
            int ex = GetWindowLongW(h, -20);
            int want = on ? ex | 0x8000000 : ex & ~0x8000000;
            if (want != ex) SetWindowLongW(h, -20, want);
        }
        catch { }
    }
    static void ClickThrough(Window w) { try { var h = w.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero; if (h != IntPtr.Zero) SetWindowLongW(h, -20, GetWindowLongW(h, -20) | 0x20 | 0x80000 | 0x8000000); } catch { } }
    public static void AbSync()
    {
        if (Ab.On && _abCard is { Closing: true }) { _abCard.Revive(); return; }
        if (Ab.On && _ab is null) { _abCard = new AbCard { OnClosed = () => { _ab = null; _abCard = null; if (Ab.On) Ab.Disable(); } }; _ab = FskCard.Open(_abCard, "YURIBLOCK"); }
        else if (!Ab.On && _abCard is not null) _abCard.Dismiss();
    }
    public static void PuzSync()
    {
        if (Puz.On && _puzCard is { Closing: true }) _puzCard.Revive();
        else if (Puz.On && _puz is null) { _puzCard = new PuzCard { OnClosed = () => { _puz = null; _puzCard = null; if (Puz.On) Puz.Disable(); } }; _puz = FskCard.Open(_puzCard, "YURIPUZZLE"); }
        else if (!Puz.On && _puzCard is not null) _puzCard.Dismiss();
        MarkersApply();
        CalApply();
    }
    /// <summary>ToggleBind reached the block card: start its state ease, ripple and sweep.</summary>
    public static void AbToggled(int dir, double to) => _abCard?.ToggleAnim(dir, to);
    /// <summary>A solve starting or ending does the same for the puzzle card.</summary>
    public static void PuzToggled(int dir, double to) => _puzCard?.ToggleAnim(dir, to);
    public static void MarkersToggle() { MkWant = !MkWant; Ini.Write(Paths.IniFile, "puzzle", "markers", MkWant ? 1L : 0L); MarkersApply(); Puz.Say(MkWant ? "grid markers on" : "grid markers off", true); }
    /// <summary>PuzMarkersKill: drop the surface so the next Apply redraws it - the markers are drawn for one offset.</summary>
    public static void MarkersKill() { if (_mk is not null) { _mkS?.Stop(); _mk.Close(); _mk = null; _mkS = null; } }
    /// <summary>
    /// Out of the way, but still there. Building a window is the slow part -
    /// the grid was destroyed for the duration of every solve and rebuilt
    /// afterwards, and that rebuild is the pause between asking for the grid and
    /// seeing it. Hiding costs nothing and the timer stops either way.
    /// </summary>
    static void MarkersHide() { if (_mk is not null) { _mkS?.Stop(); _mk.Hide(); } }
    public static void MarkersApply()
    {
        bool show = MkWant && Puz.On && !Puz.Solving && Puz.Cal == 0;
        // the offset is a fact about where the board is sitting right now, and
        // markers drawn from a stale one are the symptom being debugged rather
        // than a way of debugging it. Skipped while solving - Solve has already
        // guarded, and re-entering mid-board would rebuild under a drag.
        if (show) Puz.BoardApply();
        // Solving and calibrating are pauses, not endings: keep the window and
        // hide it, so coming back is a Show rather than a build. Only the module
        // going off actually tears it down.
        if (!show) { if (Puz.On && MkWant) MarkersHide(); else MarkersKill(); DbgApply(); return; }
        PuzSolver.ScreenSync();
        int n = Puz.GridN; double sx = PuzSolver.SX, sy = PuzSolver.SY, pad = Math.Round(Math.Max(sx, sy) / 2) + 16;
        double ox = PuzSolver.PX(1) - pad, oy = PuzSolver.PY(1) - pad, w = PuzSolver.PX(n) - PuzSolver.PX(1) + 2 * pad, h = PuzSolver.PY(n) - PuzSolver.PY(1) + 2 * pad;
        if (w < 8 || h < 8) return;
        if (_mk is not null && (_mkS!.Bw != w || _mkS.Bh != h)) { _mkS.Stop(); _mk.Close(); _mk = null; _mkS = null; }
        if (_mk is null)
        {
            _mkS = new MarkerSurface(w, h) { Ox = ox, Oy = oy };
            // ShowActivated BEFORE the first Show. The extended style that makes
            // these non-activating was only applied afterwards, so the very Show
            // that brought the markers back took the foreground off the game -
            // and a client set to throttle in the background dropped its frame
            // rate the moment a puzzle finished and the grid reappeared.
            _mk = new LayeredWindow(_mkS, "YURIGRIDMARKS", topmost: true, toolWindow: true, scale: _ => 1.0) { ShowActivated = false };
            _mk.Show(); ClickThrough(_mk);
        }
        _mkS!.Ox = ox; _mkS.Oy = oy;
        _mk.Position = new PixelPoint((int)ox, (int)oy);
        if (!_mk.IsVisible) _mk.Show();
        _mkS.Tim(Pace.TICK_A);
        // The inspector LAST. Built first, the grid waited behind a second
        // window's construction before it could be shown - and the grid is the
        // thing that was asked for.
        DbgApply();
    }
    public static void CalApply(string warn = "")
    {
        bool show = Puz.Cal != 0;
        if (!show) { if (_cal is not null) { _calS?.Stop(); _cal.Close(); _cal = null; _calS = null; _calSlot = 1; } MarkersApply(); return; }
        if (_cal is null)
        {
            PuzSolver.ScreenSync();
            _calS = new CalCard();
            _cal = new LayeredWindow(_calS, "YURIGRIDCAL", topmost: true, toolWindow: true);
            _cal.Show(); ClickThrough(_cal);
        }
        if (warn != "") { _calS!.Warn = warn; _calS.WarnAt = Clock.Tick; }
        _calS!.OpenAt = Clock.Tick;
        CalPlace();
        _calS.Tim(Pace.TICK_A);
        MarkersApply();
    }
    static void CalPlace()
    {
        if (_cal is null || _calS is null) return;
        var wa = _cal.Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        int w = (int)(_calS.Bw * _calS.K), h = (int)(_calS.Bh * _calS.K), off = (int)Math.Round((42 - CalCard.PADC) * _calS.K);
        _cal.Position = new PixelPoint(wa.X + (wa.Width - w) / 2, _calSlot == 2 ? wa.Bottom - h - off : wa.Y + off);
    }
    /// <summary>PuzCalNudge: the corner the user needs is under the card, so the card moves to the other end and asks again.</summary>
    public static bool CalNudgeIfOver(int x, int y)
    {
        if (_cal is null || _calS is null) return false;
        var p = _cal.Position; int w = (int)(_calS.Bw * _calS.K), h = (int)(_calS.Bh * _calS.K);
        if (x < p.X || x >= p.X + w || y < p.Y || y >= p.Y + h) return false;
        _calSlot = _calSlot == 1 ? 2 : 1;
        CalApply("that corner was under this card - it has moved, click it again");
        return true;
    }
    /// <summary>
    /// HitZoneAtCursor(): is this screen point over a live hit zone of either
    /// card? The block's HotIf carries `!HitZoneAtCursor()`, because the cards
    /// are WS_EX_NOACTIVATE - clicking one while the client is focused leaves
    /// Roblox in front, and without this the press is eaten and sent as Q.
    /// </summary>
    public static bool HitAt(int x, int y) => Over(_ab, _abCard, x, y) || Over(_puz, _puzCard, x, y);
    static bool Over(LayeredWindow? w, FskCard? c, int x, int y)
    {
        if (w is null || c is null) return false;
        try { var p = w.Position; double k = Math.Max(0.001, c.K); return c.ZoneAtPublic((x - p.X) / k, (y - p.Y) / k) != 0; }
        catch { return false; }
    }
    // ---- the GRID INSPECTOR ----
    // Separate from the markers because the markers are WS_EX_TRANSPARENT - the
    // game has to keep receiving clicks through them - and a click-through
    // button is not a button. This one is not transparent and not activatable,
    // so it takes the click without ever stealing focus from the game.
    static LayeredWindow? _dbg; static DbgCard? _dbgS;
    public static bool MkDbg = true;
    /// <summary>Shown exactly when the markers are: the inspector describes the grid the markers draw, and one without the other is half an answer.</summary>
    public static void DbgApply()
    {
        bool want = MkDbg && MkWant && Puz.On && !Puz.Solving && Puz.Cal == 0;
        if (!want)
        {
            if (_dbg is null) return;
            if (Puz.On && MkWant && MkDbg) { _dbgS?.Stop(); _dbg.Hide(); }      // a pause, as above
            else { _dbgS?.Stop(); _dbg.Close(); _dbg = null; _dbgS = null; }
            return;
        }
        if (_dbg is null)
        {
            PuzSolver.ScreenSync();
            _dbgS = new DbgCard();
            _dbg = new LayeredWindow(_dbgS, "YURIGRIDDBG", topmost: true, toolWindow: true) { ShowActivated = false };
            _dbg.Show();
            // Layered, but NOT transparent and NOT non-activating: it has
            // buttons, and a window whose buttons cannot take the foreground is
            // a window the person cannot use at full rate. See FskCard.Open.
            try { var h = _dbg.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero; if (h != IntPtr.Zero) SetWindowLongW(h, -20, GetWindowLongW(h, -20) | 0x80000); } catch { }
        }
        DbgPlace();
        if (!_dbg.IsVisible) _dbg.Show();
        _dbgS!.Tim(Pace.TICK_S);
    }
    static void DbgPlace()
    {
        if (_dbg is null || _dbgS is null) return;
        var wa = _dbg.Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        int w = (int)(_dbgS.Bw * _dbgS.K), h = (int)(_dbgS.Bh * _dbgS.K);
        // PZ_dbgX := PZ_mkX + PZ_mkW + 10, and PZ_mkW is the MARKER SURFACE's
        // width - which is the board plus a half-cell and 16 px of margin either
        // side, where the corner brackets are drawn. Measuring off the board's
        // own rect instead put the card about a half-cell inside the marker
        // window, so the two overlapped down the whole edge.
        double sxm = PuzSolver.SX, sym = PuzSolver.SY, padm = Math.Round(Math.Max(sxm, sym) / 2) + 16;
        int mkx = (int)Math.Round(PuzSolver.PX(1) - padm), mky = (int)Math.Round(PuzSolver.PY(1) - padm);
        int mkw = (int)Math.Round(PuzSolver.PX(Puz.GridN) - PuzSolver.PX(1) + 2 * padm);
        int x = mkx + mkw + 16;
        if (x + w > wa.Right - 8) x = Math.Max(wa.X + 8, mkx - w - 10);          // only when the right genuinely will not hold it
        int y = Math.Clamp(mky, wa.Y + 8, Math.Max(wa.Y + 8, wa.Bottom - h - 8));
        _dbg.Position = new PixelPoint(Math.Max(wa.X + 8, x), y);
    }
    public static void CloseAll() { _abCard?.Dismiss(); _puzCard?.Dismiss(); if (_mk is not null) { _mk.Close(); _mk = null; } if (_cal is not null) { _cal.Close(); _cal = null; } if (_dbg is not null) { _dbg.Close(); _dbg = null; } }
}

/// <summary>
/// GRID INSPECTOR (PuzDbgPaint): every number the grid is made of, and the ones
/// derived from it that are worth checking against the board by eye. It sits
/// beside the markers because that is where it gets used - a value here and the
/// dot it describes are in the same glance.
///
/// Designed to the suite's language rather than styled for its own sake: the
/// panel plate, accent hairline and micro-backdrop the hub cards use, section
/// rules instead of blank lines, labels in the small face and values in the
/// MONOSPACE one so the digits form a column that can be scanned downward.
/// Colour carries meaning only - accent for the grid's identity, amber when
/// something is off nominal, red when a solve would refuse.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DbgCard : Surface
{
    public const double W = 286, H = 500;
    const double LH = 18, SH = 11;                      // a value row, and the gap a section rule opens
    const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_OFF = 0xFFFB7185;
    const int Z_DOWN = 1, Z_UP = 2;
    readonly Dictionary<int, (double x, double y, double w, double h)> _hit = new();
    readonly Dictionary<int, double> _hv = new();
    int _down;
    public DbgCard() : base(W, H, UiScale.Factor) { }

    protected override int ZoneAt(double ux, double uy)
    {
        foreach (var (z, r) in _hit) if (ux >= r.x && ux <= r.x + r.w && uy >= r.y && uy <= r.y + r.h) return z;
        return 0;
    }
    /// <summary>The zone table, for the harness.</summary>
    public int ZoneAtPublicDbg(double ux, double uy) => ZoneAt(ux, uy);
    protected override void OnZoneDown(int z, PointerPressedEventArgs e)
    {
        // Same as the cards: a deliberate press may take the foreground when
        // there is no client in front of it to take anything from.
        try { if (Win is { } w && !(OperatingSystem.IsWindows() && WinHooks.RobloxInFront()) && !w.IsActive) w.Activate(); } catch { }
        _down = z; Tim(Pace.TICK_A);
    }
    /// <summary>Only fires when the release lands on the same control as the press, which lets a mis-click be dragged off and cancelled.</summary>
    protected override void OnZoneUp(int z, PointerReleasedEventArgs e)
    {
        if (_down != 0 && z == _down)
        {
            if (z == Z_DOWN) Puz.BoardSet(0);
            else if (z == Z_UP) Puz.BoardSet(PuzSolver.BannerDy());
        }
        _down = 0; Tim(Pace.TICK_S);
    }
    protected override void OnDragCancel() { _down = 0; }

    static string Num(double v) { string t = v.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0'); return t.TrimEnd('.'); }
    void Row(double x, double y, double w, string label, string val, uint col)
    {
        Txt(label, x, y + 1, 60, LH, Fonts.fHint, FA(0x8AC7CBE0, 1.0), Fmt.L);
        Txt(val, x + 66, y, w - 66, LH, HubSurface.Live?.HL.fM ?? Fonts.fBadge, FA(col, 1.0), Fmt.L);
    }
    static void Rule(double x, double y, double w, uint acc) => FillRR(x, y, w, 1, 0.5, VBrush(x, y, w, 1, Alpha(acc, 60), Alpha(acc, 6)));
    /// <summary>An inspector button: its own widget rather than FFMBtn, which reads the hub's hover map and lives on the hub's surface.</summary>
    void Btn(int z, double bx, double by, double bw, double bh, string label, bool active, uint acc, long now)
    {
        _hit[z] = (bx, by, bw, bh);
        double hv = _hv.GetValueOrDefault(z), pr = _down == z ? 1.0 : 0.0;
        // press dips and shrinks; hover lifts a pixel. Both small - a control
        // this size reads as broken if it moves more than it is tall.
        double lift = -1.5 * hv + 2.0 * pr, sc = 1 - 0.02 * pr;
        double cxm = bx + bw / 2, cym = by + bh / 2 + lift;
        int st = PushXform(cxm, cym, sc, 0);
        double x0 = cxm - bw / 2, y0 = cym - bh / 2;
        if (active)
        {
            FillRR(x0, y0, bw, bh, 7, VBrush(x0, y0, bw, bh, Alpha(AccHi(acc, 0.45), 170 + 40 * hv), Alpha(acc, 120 + 40 * hv)));
            StrokeRR(x0, y0, bw, bh, 7, Pen(Alpha(AccHi(acc, 0.5), 220), 1.2));
        }
        else
        {
            FillRR(x0, y0, bw, bh, 7, SBrush(Alpha(0xFFFFFF, R(8 + 16 * hv))));
            StrokeRR(x0, y0, bw, bh, 7, Pen(Alpha(acc, R(70 + 70 * hv)), 1));
        }
        Txt(label, x0, y0 + bh / 2 - 9, bw, 18, Fonts.fBadge,
            Alpha(active ? 0xFFFFFFFF : Mix(0xFFC7CBE0, AccHi(acc, 0.4), hv), active ? 250 : R(170 + 70 * hv)), Fmt.C);
        Pop(st);
    }

    protected override void Frame(long now)
    {
        uint acc = HubState.Accent;
        if (!Puz.Solving) PuzSolver.ScreenSync();
        int hz = ZoneCursor();
        foreach (var z in new[] { Z_DOWN, Z_UP }) { double v = _hv.GetValueOrDefault(z); v += ((hz == z ? 1.0 : 0.0) - v) * EK(0.2); _hv[z] = v < 0.004 && hz != z ? 0 : v; }

        double dx = 0, dy = 0, dw = W, dh = H;
        // the plate: same construction as a hub card, so it reads as part of the
        // suite rather than a console someone taped on
        FillRR(dx, dy, dw, dh, 10, VBrush(dx, dy, dw, dh, 0xF20E1020, 0xF2080A14));
        MicroBackdrop(dx, dy, dw, dh, 10, acc, 1.0, now, 0.85);
        StrokeRR(dx, dy, dw, dh, 10, Pen(Alpha(acc, 70), 1));
        FillRR(dx + 1, dy + 10, 2, dh - 20, 1, VBrush(dx + 1, dy + 10, 2, dh - 20, Alpha(AccHi(acc, 0.45), 210), Alpha(acc, 40)));

        int n = Puz.GridN;
        int off = (int)Math.Round(PuzSolver.SampleOff * PuzSolver.SX);
        PuzSolver.BoardRectPublic(out int cbx, out int cby, out int cbw, out int cbh);
        bool cust = Puz.Custom, fits = PuzSolver.GridOnScreen();
        double x = dx + 14, w = dw - 26, y = dy + 12;

        Txt("GRID", x, y, 60, 18, Fonts.fBrand, FA(Alpha(Mix(0xFFE8EAF6, acc, 0.25), 245), 1.0), Fmt.L);
        Txt(cust ? "CUSTOM" : "BUILT IN", x + 46, y + 2, w - 46, 16, Fonts.fBadge,
            FA(Alpha(cust ? AccHi(acc, 0.4) : 0xFFC7CBE0, cust ? 240 : 165), 1.0), Fmt.L);
        // this screen's entry, so "saved none" means none FOR THIS RESOLUTION
        string sv = Ini.Read(Paths.IniFile, "puzzle", "grid" + n, "");
        Txt("gen " + Puz.GridGen + "  \u00B7  saved " + (sv == "" ? "none" : Num(double.TryParse(sv.Split(',')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s0) ? s0 : 0)),
            x, y + 20, w, 15, Fonts.fHint, FA(0x7AC7CBE0, 1.0), Fmt.L);
        y += 40; Rule(x, y, w, acc); y += SH;

        Row(x, y, w, "origin", Num(PuzSolver.X0) + ", " + Num(PuzSolver.Y0), 0xFFE8EAF6); y += LH;
        Row(x, y, w, "pitch", Num(PuzSolver.SX) + " x " + Num(PuzSolver.SY), 0xFFE8EAF6); y += LH;
        Row(x, y, w, "cells", n + " x " + n, 0xFF9AA8C0); y += LH;
        y += 3; Rule(x, y, w, acc); y += SH;

        Row(x, y, w, "1,1", PuzSolver.PX(1) + ", " + PuzSolver.PY(1), 0xFFC7CBE0); y += LH;
        Row(x, y, w, n + "," + n, PuzSolver.PX(n) + ", " + PuzSolver.PY(n), 0xFFC7CBE0); y += LH;
        Row(x, y, w, "span", (PuzSolver.PX(n) - PuzSolver.PX(1)) + " x " + (PuzSolver.PY(n) - PuzSolver.PY(1)), 0xFF9AA8C0); y += LH;
        Row(x, y, w, "box", Math.Round(PuzSolver.X0 - PuzSolver.SX / 2) + ", " + Math.Round(PuzSolver.Y0 - PuzSolver.SY / 2 + Puz.BoardDy)
            + "  " + Math.Round(PuzSolver.SX * n) + " x " + Math.Round(PuzSolver.SY * n), 0xFF9AA8C0); y += LH;
        Row(x, y, w, "mid", Math.Round(PuzSolver.X0 + (n / 2.0 - 0.5) * PuzSolver.SX) + ", "
            + Math.Round(PuzSolver.Y0 + (n / 2.0 - 0.5) * PuzSolver.SY + Puz.BoardDy), 0xFF9AA8C0); y += LH;
        y += 3; Rule(x, y, w, acc); y += SH;

        // ---- the board offset, given its own block ----
        // It is the one line here that changes while you watch, and the one that
        // explains a solve going wrong, so it gets the colour and the space.
        bool shifted = Puz.BoardDy != 0;
        Row(x, y, w, "board", shifted ? "UP    +" + Puz.BoardDy + " px" : "DOWN   on the grid", shifted ? AMBER : C_ON); y += LH;
        Row(x, y, w, "from", Puz.DyPin ? "you (button)" : !Puz.On ? "module off" : Puz.Sess != 0 ? "rule \u00B7 past board 1" : "rule \u00B7 board 1",
            Puz.DyPin ? AccHi(acc, 0.35) : 0xFF6E7590); y += LH;
        // the buttons sit in the block they act on, so the state and the control
        // that changes it are read in one glance
        double bw2 = (w - 8) / 2;
        Btn(Z_DOWN, x, y + 3, bw2, 27, "DOWN BOARD", !shifted, acc, now);
        Btn(Z_UP, x + bw2 + 8, y + 3, bw2, 27, "UP BOARD", shifted, acc, now);
        y += 39; Rule(x, y, w, acc); y += SH;

        Row(x, y, w, "read", cbx + ", " + cby + "  " + cbw + " x " + cbh, 0xFF9AA8C0); y += LH;
        Row(x, y, w, "probe", off + " px off  \u00B7  thr " + PuzSolver.Thresh, 0xFF6E7590); y += LH;
        // stated because a correct grid that still will not solve is almost
        // always the drawing rather than the geometry
        Row(x, y, w, "walk", Math.Round(PuzSolver.CellMs) + " ms/cell  \u00B7  " + Math.Round(PuzSolver.PxPerMs, 1) + " px/ms", 0xFF6E7590); y += LH;
        Row(x, y, w, "retry", "verifying each line", 0xFF6E7590); y += LH;
        y += 3; Rule(x, y, w, acc); y += SH;

        Row(x, y, w, "screen", PuzSolver.vw + " x " + PuzSolver.vh, 0xFF6E7590); y += LH;
        // EXACT means this resolution is in the measured table - the difference
        // between "this should be right" and "this is a reasonable guess"
        Row(x, y, w, "ref", cust ? "saved grid  \u00B7  fixed"
            : PuzSolver.DefScale().ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "  \u00B7  " + (PuzSolver.RefExact ? "measured" : "interpolated"),
            cust ? 0xFF6E7590 : PuzSolver.RefExact ? C_ON : AMBER); y += LH;
        Row(x, y, w, "solve", fits ? "ready" : "REFUSED - off screen", fits ? C_ON : C_OFF); y += LH;
        Row(x, y, w, "banner", Puz.BannerFix ? PuzSolver.BannerDy() + " px" : "off", 0xFF4E5470); y += LH;

        Tim(hz != 0 || _down != 0 ? Pace.TICK_A : Pace.TICK_S);
    }
}
