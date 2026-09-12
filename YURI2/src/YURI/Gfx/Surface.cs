using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Yuri.Core;

namespace Yuri.Gfx;

/// <summary>
/// One of the .ahk's layered GDI+ surfaces: a base size in LOGICAL units
/// (bw x bh) drawn at scale k, a render loop with the .ahk's tiers (Tim), and
/// the pointer resolved to a zone id by the subclass's ZoneAt — the same
/// contract HubZone / GZone / LZone had. The hover map H eases each zone's
/// value toward 1 while the pointer is on it (HoverTick), so a ported line
/// like <c>hv := HL.h.Get(zid, 0.0)</c> becomes <c>H.GetValueOrDefault(zid)</c>.
///
/// Frame(now) is the render loop body: everything the .ahk did between
/// `now := A_TickCount` and UpdateLayeredWindow. The window's alpha and the
/// intro's vertical drift, which UpdateLayeredWindow applied to the whole
/// bitmap, are G.PushOpacity / G.PushShift inside the frame.
/// </summary>
public abstract class Surface : Control
{
    public readonly double Bw, Bh;
    public double K { get; private set; }
    public int Period { get; private set; }               // hubP: 0 stopped
    public int PEff { get; private set; }                 // hubPEff: the period after pacing
    public bool Hidden;                                    // hubHidden
    /// <summary>Does this surface's window have the foreground? Windows that never activate (the macro cards) report true, since they have no focus to lose.</summary>
    public bool Active { get; private set; } = true;
    /// <summary>The foreground went elsewhere. Anything holding the keyboard has to let go of it.</summary>
    protected virtual void OnDeactivated() { }
    public void TrackActiveOn(Window w) => TrackActive(w);
    protected void TrackActive(Window w)
    {
        w.Activated += (_, _) => { Active = true; Poke(); };
        w.Deactivated += (_, _) => { Active = false; OnDeactivated(); };
    }

    readonly DispatcherTimer _timer;
    long _lastFrameAt;

    // ---- the crop ----
    // A collapsed hub is a 74 x 96 pill inside an 832 x 572 window, and the rest
    // of that window is empty and still takes every click that lands on it.
    // SetWindowRgn solves it on Windows and has no macOS counterpart, so the
    // window itself SHRINKS to the part being drawn: the surface reports the
    // crop's size, the render translates by its origin, and the pointer is put
    // back into surface space on the way in. One mechanism, both platforms.
    public double CropX, CropY, CropW, CropH;              // CropW 0 = no crop
    bool Cropped => CropW > 0 && CropH > 0;
    /// <summary>Confine the window to this rect of the surface. Zero width clears it. The window moves with it, so what is drawn does not appear to jump.</summary>
    bool _cropQueued;
    /// <summary>
    /// Where the window's top-left WOULD be with no crop. Everything that stores
    /// or reasons about a position means the card's own origin, not the bubble's
    /// - saving the cropped one and restoring it uncropped moves the card by the
    /// crop on the next launch.
    /// </summary>
    public PixelPoint UncroppedPos()
    {
        var p = Win?.Position ?? default;
        return Cropped ? new PixelPoint(p.X - (int)Math.Round(CropX * K), p.Y - (int)Math.Round(CropY * K)) : p;
    }
    public void SetCrop(double x, double y, double w, double h)
    {
        if (Math.Abs(CropX - x) < 0.5 && Math.Abs(CropY - y) < 0.5 && Math.Abs(CropW - w) < 0.5 && Math.Abs(CropH - h) < 0.5) return;
        // Frame runs INSIDE the render pass, and resizing there throws: a visual
        // cannot be invalidated while it is being drawn. The change is applied
        // on the next dispatcher turn instead.
        if (_cropQueued) return;
        _cropQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _cropQueued = false;
            double oldX = Cropped ? CropX : 0, oldY = Cropped ? CropY : 0;
            CropX = x; CropY = y; CropW = w; CropH = h;
            double newX = Cropped ? CropX : 0, newY = Cropped ? CropY : 0;
            Width = (Cropped ? CropW : Bw) * K; Height = (Cropped ? CropH : Bh) * K;
            InvalidateMeasure();
            if (Win is { } w2)
            {
                var p = w2.Position;
                w2.Position = new PixelPoint(p.X + (int)Math.Round((newX - oldX) * K), p.Y + (int)Math.Round((newY - oldY) * K));
            }
            InvalidateVisual();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    // ---- pointer, in logical units ----
    public double PtrX, PtrY;
    public bool PtrIn;
    public long PtrAt;                                     // the last pointer contact, for the deep-idle tiers
    public bool Pressed;
    public int PressZone;

    // ---- hover map ----
    public readonly Dictionary<int, double> H = new();     // HL.h
    public readonly HashSet<int> Hz = new();               // HL.hz: every zone id that has ever been hovered
    public double HMax;                                    // hmax

    protected Surface(double bw, double bh, double k)
    {
        Bw = bw; Bh = bh;
        SetScale(k);
        ClipToBounds = false;
        Focusable = true;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(Pace.TICK_A) };
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    public void SetScale(double k)
    {
        K = k;
        Width = (CropW > 0 ? CropW : Bw) * k; Height = (CropH > 0 ? CropH : Bh) * k;
        InvalidateMeasure();
    }

    /// <summary>
    /// HubTim(p): ask for a frame every p ms; 0 stops. The active tier is
    /// held at the pacing floor (Pace.HubPace) on a machine whose frames cost
    /// more than TICK_A; LOW PERFORMANCE MODE maps every tier to 40 / 250 / stop.
    /// </summary>
    public void Tim(int p)
    {
        if (Hidden && p != 0) return;
        if (HubState.LowPerf && p != 0) p = LowPerfMap(p);
        int pe = (!HubState.LowPerf && p != 0 && p <= Pace.TICK_A) ? Math.Max(p, Pace.HubPace) : p;
        if (p != Period || pe != PEff)
        {
            Period = p; PEff = pe;
            if (pe <= 0) _timer.Stop();
            else { _timer.Interval = TimeSpan.FromMilliseconds(pe); if (!_timer.IsEnabled) _timer.Start(); }
        }
        Pace.DtK = (!HubState.LowPerf && p == Pace.TICK_A && pe > Pace.TICK_A) ? pe / (double)Pace.TICK_A : 1.0;
    }
    /// <summary>RendTim's low-performance mapping - the loading screen, the gate and the other renderers: the fast tier 40 ms, everything else 220. HubSurface overrides with HubTim's 40 / 250 / stop.</summary>
    protected virtual int LowPerfMap(int p) => p <= Pace.TICK_A || p == 40 ? 40 : 220;
    /// <summary>Stop the loop and drop the frames (the window is closing).</summary>
    public void Stop() { _timer.Stop(); Period = 0; PEff = 0; }

    /// <summary>
    /// A frame that throws would take the process down from inside the render
    /// pass. The .ahk swallowed its faults; so does this, unless a host installs
    /// a handler (the test harness rethrows).
    /// </summary>
    public static Action<Exception>? OnFrameError;

    public sealed override void Render(DrawingContext ctx)
    {
        if (Hidden) return;
        long now = Clock.Tick;
        long t0 = Clock.Qpc();
        using (ctx.PushTransform(Cropped ? Matrix.CreateTranslation(-CropX, -CropY) * Matrix.CreateScale(K, K) : Matrix.CreateScale(K, K)))
        {
            G.Begin(ctx, K);
            try { Frame(now); }
            catch (Exception ex) { OnFrameError?.Invoke(ex); }
            finally { G.End(); }
        }
        long t1 = Clock.Qpc();
        double ms = Clock.QpcMs(t1 - t0);
        Pace.Note(ms);
        HubState.Lean = !HubState.LowPerf && Pace.FtEma > 22;
        if (Perf.On) Perf.Frame(t1 - t0, 0, now);
        _lastFrameAt = now;
    }

    /// <summary>The render loop body. `now` is A_TickCount.</summary>
    protected abstract void Frame(long now);

    /// <summary>HubZone(ux, uy): the zone id under a logical point, 0 for none.</summary>
    protected virtual int ZoneAt(double ux, double uy) => 0;
    /// <summary>HubZoneCursor(): the zone under the pointer now.</summary>
    public int ZoneCursor() => PtrIn ? ZoneAt(PtrX, PtrY) : 0;

    /// <summary>
    /// The per-frame hover ease over every known zone: v -> 1 on the hovered
    /// zone, -> 0 on the rest, by EK(0.2); LOW PERFORMANCE MODE snaps. Returns hmax.
    /// </summary>
    public double HoverTick(int hz)
    {
        if (hz != 0) Hz.Add(hz);
        HMax = 0;
        foreach (var z in Hz)
        {
            double v = H.GetValueOrDefault(z);
            if (v == 0.0 && hz != z) continue;
            if (HubState.LowPerf) v = hz == z ? 1.0 : 0.0;
            else
            {
                v += ((hz == z ? 1.0 : 0.0) - v) * Ease.EK(0.2);
                v = (v < 0.004 && hz != z) ? 0.0 : v;
            }
            H[z] = v;
            if (v > HMax) HMax = v;
        }
        return HMax;
    }
    public double Hv(int zid) => H.GetValueOrDefault(zid);

    // ---- pointer routing ----
    protected virtual void OnZoneDown(int z, PointerPressedEventArgs e) { }
    protected virtual void OnZoneUp(int z, PointerReleasedEventArgs e) { }
    protected virtual void OnZoneRightDown(int z, PointerPressedEventArgs e) { }
    protected virtual void OnZoneWheel(int z, double delta, PointerWheelEventArgs e) { }
    protected virtual void OnPointerMove(double ux, double uy) { }
    /// <summary>Any pointer contact: the cue to leave an idle tier.</summary>
    protected virtual void Poke() { }
    /// <summary>The press ended without a release we saw (capture lost, a window move): drop any drag.</summary>
    protected virtual void OnDragCancel() { }

    void Track(PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        PtrX = p.X / K + (Cropped ? CropX : 0); PtrY = p.Y / K + (Cropped ? CropY : 0); PtrIn = true; PtrAt = Clock.Tick;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        Track(e);
        OnPointerMove(PtrX, PtrY);
        DragTo(e);
        Poke();
        e.Handled = true;
    }
    protected override void OnPointerEntered(PointerEventArgs e) { Track(e); Poke(); }
    protected override void OnPointerExited(PointerEventArgs e) { PtrIn = false; Poke(); }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Track(e);
        Focus();
        int z = ZoneAt(PtrX, PtrY);
        var pt = e.GetCurrentPoint(this).Properties;
        if (pt.IsRightButtonPressed) OnZoneRightDown(z, e);
        else { Pressed = true; PressZone = z; OnZoneDown(z, e); }
        Poke();
        e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        Track(e);
        int z = ZoneAt(PtrX, PtrY);
        Pressed = false;
        bool wasDrag = Dragging;
        if (wasDrag) { Dragging = false; try { e.Pointer.Capture(null); } catch { } }
        OnZoneUp(z, e);
        if (wasDrag) OnDragCancel();
        Poke();
        e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        if (Pressed) { Pressed = false; }
        if (Dragging) { Dragging = false; OnDragCancel(); }
        Poke();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        Track(e);
        OnZoneWheel(ZoneAt(PtrX, PtrY), e.Delta.Y, e);
        Poke();
        e.Handled = true;
    }

    /// <summary>Move the window by the pointer (the grip): the platform's own drag.</summary>
    // ---- the window drag, done by hand ----
    // The card keeps rendering while it moves - the lift, the tilt from its
    // own velocity, the MOVING veil - which the platform's move loop would
    // never allow (on Windows it runs inside the call and no frame ticks).
    // Screen-space arithmetic, so the pointer's window-relative coordinates
    // shifting under the moving window cannot feed back into the position.
    public bool Dragging { get; protected set; }
    /// <summary>False while a press may still turn out to be a click (the folded badge): the window does not move yet.</summary>
    public bool DragMoves = true;
    PixelPoint _dragGrip, _dragWin0, _dragPrevPos;
    public double DragGripX, DragGripY;                                 // where the press landed, in surface units
    protected void BeginDrag(PointerPressedEventArgs e)
    {
        if (Win is not { } w) return;
        Dragging = true; DragMoves = true;
        var pos = e.GetPosition(this);
        DragGripX = pos.X / K; DragGripY = pos.Y / K;
        _dragGrip = this.PointToScreen(pos);
        _dragWin0 = w.Position; _dragPrevPos = w.Position;
        try { e.Pointer.Capture(this); } catch { }
    }
    void DragTo(PointerEventArgs e)
    {
        if (!Dragging || !DragMoves || Win is not { } w) return;
        PixelPoint sp;
        try { sp = this.PointToScreen(e.GetPosition(this)); } catch { return; }
        var np = new PixelPoint(_dragWin0.X + (sp.X - _dragGrip.X), _dragWin0.Y + (sp.Y - _dragGrip.Y));
        try
        {
            // HandleBounds: the grip itself stays on the screen, whatever hangs off the edge
            var scr = w.Screens.ScreenFromPoint(sp) ?? w.Screens.Primary;
            if (scr is not null)
            {
                var wa = scr.WorkingArea;
                double kk = K * scr.Scaling;
                int pad = (int)Math.Round(8 * kk);
                // The grip is in SURFACE space; these are offsets from the
                // WINDOW's origin, and a cropped window starts at (CropX, CropY)
                // of the surface. Without taking that off, a collapsed card
                // clamped as though its grip were a crop-width away from where
                // it is, and the window was pushed off the edge of the screen.
                double gx = DragGripX - (Cropped ? CropX : 0), gy = DragGripY - (Cropped ? CropY : 0);
                int hx0 = (int)Math.Round((gx - 18) * kk), hx1 = (int)Math.Round((gx + 18) * kk);
                int hy0 = (int)Math.Round((gy - 12) * kk), hy1 = (int)Math.Round((gy + 12) * kk);
                int lox = wa.X + pad - hx0, hix = wa.X + wa.Width - pad - hx1, loy = wa.Y + pad - hy0, hiy = wa.Y + wa.Height - pad - hy1;
                if (hix < lox) lox = hix = (lox + hix) / 2;
                if (hiy < loy) loy = hiy = (loy + hiy) / 2;
                np = new PixelPoint(Math.Clamp(np.X, lox, hix), Math.Clamp(np.Y, loy, hiy));
            }
        }
        catch { }
        if (np != w.Position) w.Position = np;
    }
    /// <summary>The window's horizontal travel since the last frame, in surface units - the tilt reads it.</summary>
    public double DragVelX()
    {
        if (Win is not { } w) return 0;
        var p = w.Position;
        double v = (p.X - _dragPrevPos.X) / Math.Max(0.5, K);
        _dragPrevPos = p;
        return Dragging ? v : 0;
    }
    public void EndDrag()
    {
        if (!Dragging) return;
        Dragging = false;
        OnDragCancel();
    }
    protected Window? Win => this.GetVisualRoot() as Window;
}
