using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Yuri.Core;

namespace Yuri.Gfx;

/// <summary>
/// The .ahk's drawing vocabulary — SBrush, VBrush, Pen, FillRR, StrokeRR,
/// FillEll, Arc, Line, Txt, PushXform, Pop, the clip — over Avalonia's
/// DrawingContext. Names and argument orders are the .ahk's so a render
/// function ports line for line. Colours are 0xAARRGGBB uints; a brush or pen
/// is null when it would be invisible and every primitive skips a null, which
/// is what the .ahk's 0 handles did. DelB / DelP are no-ops kept for the port.
///
/// One surface renders at a time (the UI thread), so the current context is
/// static, exactly as `G` was a global: Begin at the top of a frame, End after.
/// Coordinates are the surface's LOGICAL units; the surface pushes the scale.
/// </summary>
public static class G
{
    public static DrawingContext Ctx = null!;
    public static double Scale = 1.0;                 // bdScale: the scale the frame draws at

    enum Kind { Xform, Clip, Opacity }
    sealed record Entry(DrawingContext.PushedState State, Kind Kind, Matrix M);
    static readonly List<Entry> Stack = new();

    public static void Begin(DrawingContext ctx, double scale)
    {
        Ctx = ctx; Scale = scale; Stack.Clear();
    }
    public static void End()
    {
        for (int i = Stack.Count - 1; i >= 0; i--) Stack[i].State.Dispose();
        Stack.Clear();
        Ctx = null!;
    }

    // ---- colours -> brushes / pens ----
    public static IBrush? SBrushP(uint c)
    {
        uint k = Col.ElA(c);
        return (k & 0xFF000000) == 0 ? null : new ImmutableSolidColorBrush(Col.ToColor(k));
    }
    public static IBrush? SBrush(uint c) => SBrushP(Col.TH(c));
    public static IBrush? Solid(uint argb) => (argb & 0xFF000000) == 0 ? null : new ImmutableSolidColorBrush(Col.ToColor(argb));

    /// <summary>VBrush: a vertical gradient over the rect (top c1, bottom c2), through TH and ElA.</summary>
    public static IBrush? VBrush(double x, double y, double w, double h, uint c1, uint c2)
    {
        if (HubState.LowPerf) return SBrush(c1);
        c1 = Col.ElA(Col.TH(c1)); c2 = Col.ElA(Col.TH(c2));
        if (((c1 | c2) & 0xFF000000) == 0) return null;
        return Grad(new Point(x, y - 1), new Point(x, y + h + 1), c1, c2);
    }
    /// <summary>HBrush: a horizontal gradient over the rect (left c1, right c2).</summary>
    public static IBrush? HBrush(double x, double y, double w, double h, uint c1, uint c2)
    {
        if (HubState.LowPerf) return SBrush(c1);
        c1 = Col.ElA(Col.TH(c1)); c2 = Col.ElA(Col.TH(c2));
        if (((c1 | c2) & 0xFF000000) == 0) return null;
        return Grad(new Point(x - 1, y), new Point(x + w + 1, y), c1, c2);
    }
    /// <summary>VBrushP / HBrushP: the same without the theme translation.</summary>
    public static IBrush? VBrushP(double x, double y, double w, double h, uint c1, uint c2)
        => Grad(new Point(x, y - 1), new Point(x, y + h + 1), Col.ElA(c1), Col.ElA(c2));
    public static IBrush? HBrushP(double x, double y, double w, double h, uint c1, uint c2)
        => Grad(new Point(x - 1, y), new Point(x + w + 1, y), Col.ElA(c1), Col.ElA(c2));
    /// <summary>BgV: a card plate's gradient — through BgScale then TH, and flat in LOW PERFORMANCE MODE.</summary>
    public static IBrush? BgV(double x, double y, double w, double h, uint c1, uint c2)
    {
        if (HubState.LowPerf) return SBrushP(Col.ElA(Col.TH(Col.BgScale(c1))));
        return Grad(new Point(x, y - 1), new Point(x, y + h + 1), Col.TH(Col.BgScale(c1)), Col.TH(Col.BgScale(c2)));
    }
    /// <summary>
    /// GdipCreateLineBrushFromRect with an explicit rect: a raw gradient between
    /// two absolute points (mode 0 horizontal, 1 vertical, 2 forward diagonal).
    /// No theme translation — the callers that used it already did their own.
    /// </summary>
    public static IBrush? LineBrush(double x, double y, double w, double h, uint c1, uint c2, int mode)
    {
        if (((c1 | c2) & 0xFF000000) == 0) return null;
        return mode switch
        {
            1 => Grad(new Point(x, y), new Point(x, y + h), c1, c2),
            2 => Grad(new Point(x, y), new Point(x + w, y + h), c1, c2),
            _ => Grad(new Point(x, y), new Point(x + w, y), c1, c2),
        };
    }
    static IBrush Grad(Point a, Point b, uint c1, uint c2)
        => new ImmutableLinearGradientBrush(
            new[] { new ImmutableGradientStop(0, Col.ToColor(c1)), new ImmutableGradientStop(1, Col.ToColor(c2)) },
            1.0, null, null, GradientSpreadMethod.Pad,
            new RelativePoint(a, RelativeUnit.Absolute), new RelativePoint(b, RelativeUnit.Absolute));

    public static Pen? PenP(uint c, double w)
    {
        uint k = Col.ElA(c);
        if ((k & 0xFF000000) == 0) return null;
        return new Pen(new ImmutableSolidColorBrush(Col.ToColor(k)), w);
    }
    public static Pen? Pen(uint c, double w) => PenP(Col.TH(c), w);
    /// <summary>PenDash(p, style): GDI+ dash styles — 1 dash, 2 dot, 3 dash-dot, 4 dash-dot-dot, 0 solid.</summary>
    public static void PenDash(Pen? p, int style)
    {
        if (p is null) return;
        p.DashStyle = style switch
        {
            1 => new DashStyle(new[] { 3.0, 1.0 }, 0),
            2 => new DashStyle(new[] { 1.0, 1.0 }, 0),
            3 => new DashStyle(new[] { 3.0, 1.0, 1.0, 1.0 }, 0),
            4 => new DashStyle(new[] { 3.0, 1.0, 1.0, 1.0, 1.0, 1.0 }, 0),
            _ => null,
        };
    }
    /// <summary>PenDashOff(p, off): the dash phase, in pen widths.</summary>
    public static void PenDashOff(Pen? p, double off)
    {
        if (p?.DashStyle is DashStyle ds) p.DashStyle = new DashStyle(ds.Dashes, off);
    }
    public static void DelB(IBrush? b) { }
    public static void DelP(IPen? p) { }

    // ---- geometry ----
    static double RR(double w, double h, double r) => HubState.LowPerf ? 0.0 : Math.Min(r, Math.Min(w / 2, h / 2));
    public static RoundedRect RRect(double x, double y, double w, double h, double r)
    {
        w = Math.Max(w, 0.0); h = Math.Max(h, 0.0);
        double rr = RR(w, h, r);
        return rr <= 0.01 ? new RoundedRect(new Rect(x, y, w, h)) : new RoundedRect(new Rect(x, y, w, h), rr);
    }
    /// <summary>RRPath(x, y, w, h, r): a rounded-rect geometry, for clips and fills that need a path.</summary>
    public static Geometry RRPath(double x, double y, double w, double h, double r)
    {
        var rr = RRect(x, y, w, h, r);
        return new RectangleGeometry(rr.Rect) { RadiusX = rr.RadiiTopLeft.X, RadiusY = rr.RadiiTopLeft.Y };
    }

    public static void FillRR(double x, double y, double w, double h, double r, IBrush? brush)
    {
        if (brush is null) return;
        if (HubState.LowPerf) { Ctx.DrawRectangle(brush, null, new Rect(x, y, Math.Max(w, 0), Math.Max(h, 0))); return; }
        long t0 = Perf.On ? Perf.Now() : 0;
        Ctx.DrawRectangle(brush, null, RRect(x, y, w, h, r));
        if (Perf.On) Perf.Add("fill", t0);
    }
    public static void StrokeRR(double x, double y, double w, double h, double r, IPen? pen)
    {
        if (pen is null) return;
        if (HubState.LowPerf) { Ctx.DrawRectangle(null, pen, new Rect(x, y, Math.Max(w, 0), Math.Max(h, 0))); return; }
        long t0 = Perf.On ? Perf.Now() : 0;
        Ctx.DrawRectangle(null, pen, RRect(x, y, w, h, r));
        if (Perf.On) Perf.Add("stroke", t0);
    }
    /// <summary>GdipFillRectangle: a plain rectangle (the .ahk called it directly for gradient bands).</summary>
    public static void FillRect(double x, double y, double w, double h, IBrush? brush)
    {
        if (brush is null || w <= 0 || h <= 0) return;
        Ctx.DrawRectangle(brush, null, new Rect(x, y, w, h));
    }
    public static void FillEll(double x, double y, double w, double h, IBrush? brush)
    {
        if (brush is null) return;
        long t0 = Perf.On ? Perf.Now() : 0;
        Ctx.DrawEllipse(brush, null, new Point(x + w / 2, y + h / 2), w / 2, h / 2);
        if (Perf.On) Perf.Add("ell", t0);
    }
    public static void Ell(double x, double y, double w, double h, IPen? pen)
    {
        if (pen is null) return;
        Ctx.DrawEllipse(null, pen, new Point(x + w / 2, y + h / 2), w / 2, h / 2);
    }
    public static void Line(double x1, double y1, double x2, double y2, IPen? pen)
    {
        if (pen is null) return;
        long t0 = Perf.On ? Perf.Now() : 0;
        Ctx.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
        if (Perf.On) Perf.Add("line", t0);
    }
    /// <summary>Arc(x, y, w, h, a1, sweep, pen): degrees, clockwise from +x, as GdipDrawArc.</summary>
    public static void Arc(double x, double y, double w, double h, double a1, double sweep, IPen? pen)
    {
        if (pen is null || w <= 0 || h <= 0) return;
        Ctx.DrawGeometry(null, pen, ArcGeo(x, y, w, h, a1, sweep, false));
    }
    /// <summary>GdipFillPie.</summary>
    public static void FillPie(double x, double y, double w, double h, double a1, double sweep, IBrush? brush)
    {
        if (brush is null || w <= 0 || h <= 0) return;
        Ctx.DrawGeometry(brush, null, ArcGeo(x, y, w, h, a1, sweep, true));
    }
    static StreamGeometry ArcGeo(double x, double y, double w, double h, double a1, double sweep, bool pie)
    {
        double rx = w / 2, ry = h / 2, cx = x + rx, cy = y + ry;
        double s = a1 * Math.PI / 180, e = (a1 + sweep) * Math.PI / 180;
        var p0 = new Point(cx + rx * Math.Cos(s), cy + ry * Math.Sin(s));
        var p1 = new Point(cx + rx * Math.Cos(e), cy + ry * Math.Sin(e));
        var geo = new StreamGeometry();
        using var g = geo.Open();
        if (pie) { g.BeginFigure(new Point(cx, cy), true); g.LineTo(p0); }
        else g.BeginFigure(p0, false);
        if (Math.Abs(sweep) >= 360) { p1 = new Point(cx + rx * Math.Cos(s + Math.PI), cy + ry * Math.Sin(s + Math.PI)); g.ArcTo(p1, new Size(rx, ry), 0, false, SweepDirection.Clockwise); g.ArcTo(p0, new Size(rx, ry), 0, false, SweepDirection.Clockwise); }
        else g.ArcTo(p1, new Size(rx, ry), 0, Math.Abs(sweep) > 180, sweep >= 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
        g.EndFigure(pie);
        return geo;
    }
    public static void FillPath(Geometry path, IBrush? brush) { if (brush is not null) Ctx.DrawGeometry(brush, null, path); }
    public static void StrokePath(Geometry path, IPen? pen) { if (pen is not null) Ctx.DrawGeometry(null, pen, path); }

    /// <summary>ShieldPath(x, y, w, h): the badge outline.</summary>
    public static Geometry ShieldPath(double x, double y, double w, double h)
    {
        var geo = new StreamGeometry();
        using var g = geo.Open();
        g.BeginFigure(new Point(x, y), true);
        g.LineTo(new Point(x + w, y));
        g.LineTo(new Point(x + w, y + h * 0.5));
        g.CubicBezierTo(new Point(x + w, y + h * 0.8), new Point(x + w * 0.72, y + h * 0.92), new Point(x + w / 2, y + h));
        g.CubicBezierTo(new Point(x + w * 0.28, y + h * 0.92), new Point(x, y + h * 0.8), new Point(x, y + h * 0.5));
        g.EndFigure(true);
        return geo;
    }

    // ---- text ----
    /// <summary>Txt: one line, no wrap, no clip, vertically centred in the rect, aligned by fmt; colour through TH and ElA.</summary>
    public static void Txt(string s, double x, double y, double w, double h, Font font, uint c, Fmt fmt)
    {
        if (string.IsNullOrEmpty(s)) return;
        long t0 = Perf.On ? Perf.Now() : 0;
        TxtP(s, x, y, w, h, font, Col.TH(c), fmt);
        if (Perf.On) Perf.Add("txt", t0);
    }
    /// <summary>TxtP: Txt without the theme translation.</summary>
    public static void TxtP(string s, double x, double y, double w, double h, Font font, uint c, Fmt fmt)
    {
        var b = SBrushP(c);
        if (b is null || string.IsNullOrEmpty(s)) return;
        var ft = Fonts.Cached(s, font);
        ft.SetForegroundBrush(b);
        if (Perf.On) Perf.Count("txtc");
        double tw = ft.WidthIncludingTrailingWhitespace + font.Pad * 2;
        double x0 = fmt == Fmt.L ? x : fmt == Fmt.C ? x + (w - tw) / 2 : x + w - tw;
        Ctx.DrawText(ft, new Point(x0 + font.Pad, y + (h - ft.Height) / 2));
    }
    public static double MeasureW(string s, Font font) => Fonts.MeasureW(s, font);

    // ---- images ----
    /// <summary>GdipDrawImageRectRect with an alpha matrix: draw `src` of the image into `dst` at opacity a.</summary>
    public static void DrawImage(IImage img, Rect dst, Rect src, double a = 1.0)
    {
        if (a <= 0.004) return;
        long t0 = Perf.On ? Perf.Now() : 0;
        if (a >= 0.996) Ctx.DrawImage(img, src, dst);
        else { using (Ctx.PushOpacity(a)) Ctx.DrawImage(img, src, dst); }
        if (Perf.On) Perf.Add("img", t0);
    }

    // ---- transform / clip / opacity, with GDI+ save/restore semantics ----
    /// <summary>PushXform(px, py, s, rot): scale (and rotate) about a point. Returns a token for Pop.</summary>
    public static int PushXformXY(double px, double py, double sx, double sy, double rot)
    {
        var m = Matrix.CreateTranslation(-px, -py) * Matrix.CreateScale(sx, sy);
        if (rot != 0) m *= Matrix.CreateRotation(rot * Math.PI / 180);
        m *= Matrix.CreateTranslation(px, py);
        return Push(Ctx.PushTransform(m), Kind.Xform, m);
    }
    public static int PushXform(double px, double py, double s, double rot)
    {
        var m = Matrix.CreateTranslation(-px, -py) * Matrix.CreateScale(s, s);
        if (rot != 0) m *= Matrix.CreateRotation(rot * Math.PI / 180);
        m *= Matrix.CreateTranslation(px, py);
        return Push(Ctx.PushTransform(m), Kind.Xform, m);
    }
    public static int PushShift(double dx, double dy)
    {
        var m = Matrix.CreateTranslation(dx, dy);
        return Push(Ctx.PushTransform(m), Kind.Xform, m);
    }
    /// <summary>PushG(): GdipSaveGraphics — a token for the clip state that follows.</summary>
    public static int PushG() => Stack.Count;
    /// <summary>Pop(st): GdipRestoreGraphics — undo everything pushed since the token.</summary>
    public static void Pop(int token)
    {
        while (Stack.Count > token) { Stack[^1].State.Dispose(); Stack.RemoveAt(Stack.Count - 1); }
    }
    /// <summary>GdipSetClipPath with a rounded rect (intersect).</summary>
    public static void ClipRR(double x, double y, double w, double h, double r)
        => Push(Ctx.PushClip(RRect(x, y, w, h, r)), Kind.Clip, Matrix.Identity);
    public static void ClipRect(double x, double y, double w, double h)
        => Push(Ctx.PushClip(new Rect(x, y, Math.Max(w, 0), Math.Max(h, 0))), Kind.Clip, Matrix.Identity);
    public static void ClipPath(Geometry path)
        => Push(Ctx.PushGeometryClip(path), Kind.Clip, Matrix.Identity);
    /// <summary>GdipResetClip: drop every clip; transforms pushed after them are re-applied.</summary>
    public static void ResetClip()
    {
        int first = -1;
        for (int i = 0; i < Stack.Count; i++) if (Stack[i].Kind == Kind.Clip) { first = i; break; }
        if (first < 0) return;
        var keep = new List<Entry>();
        for (int i = Stack.Count - 1; i >= first; i--)
        {
            Stack[i].State.Dispose();
            if (Stack[i].Kind != Kind.Clip) keep.Add(Stack[i]);
            Stack.RemoveAt(i);
        }
        for (int i = keep.Count - 1; i >= 0; i--)
        {
            var e = keep[i];
            Push(e.Kind == Kind.Opacity ? Ctx.PushOpacity(e.M.M11) : Ctx.PushTransform(e.M), e.Kind, e.M);
        }
    }
    /// <summary>The layered window's whole-surface alpha (UpdateLayeredWindow's blend), 0..1.</summary>
    public static int PushOpacity(double a)
        => Push(Ctx.PushOpacity(Math.Clamp(a, 0, 1)), Kind.Opacity, Matrix.CreateScale(Math.Clamp(a, 0, 1), 1));

    static int Push(DrawingContext.PushedState st, Kind k, Matrix m)
    {
        Stack.Add(new Entry(st, k, m));
        return Stack.Count - 1;
    }
}
