using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using static Yuri.Gfx.G;

namespace Yuri.Gfx;

/// <summary>
/// Bitmaps: the embedded art (avares://), files on disk (cached by path and
/// write time), and the fitted blits the .ahk did through its FITTED-IMAGE
/// CACHE — BlitFitRR (cover-fit into a rounded rect, with a zoom and an
/// anchor) and BlitCircleFit. Skia scales on the fly, so there is no cache
/// of fitted copies here; a decode is cached per source.
/// </summary>
public static class Img
{
    static readonly Dictionary<string, Bitmap?> Assets = new();
    static readonly Dictionary<string, (Bitmap? bmp, DateTime stamp, long checkedAt)> Files = new();

    /// <summary>An embedded asset by file name, e.g. "comm_art_1.jpg"; null when it is missing.</summary>
    public static Bitmap? Asset(string name)
    {
        if (Assets.TryGetValue(name, out var hit)) return hit;
        Bitmap? bmp = null;
        try
        {
            using var s = AssetLoader.Open(new Uri("avares://YURI/Assets/" + name));
            bmp = new Bitmap(s);
        }
        catch { }
        Assets[name] = bmp;
        return bmp;
    }

    /// <summary>A file on disk; decoded once per write time, null when unreadable.</summary>
    public static Bitmap? File(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        long now = Core.Clock.Tick;
        if (Files.TryGetValue(path, out var e) && now - e.checkedAt < 2000) return e.bmp;   // the disk is asked at most every two seconds
        DateTime stamp;
        try { if (!System.IO.File.Exists(path)) { Files[path] = (null, default, now); return null; } stamp = System.IO.File.GetLastWriteTimeUtc(path); }
        catch { return null; }
        if (e.bmp is not null && e.stamp == stamp) { Files[path] = (e.bmp, stamp, now); return e.bmp; }
        Bitmap? bmp = null;
        try { bmp = new Bitmap(path); } catch { }
        Files[path] = (bmp, stamp, now);
        return bmp;
    }
    public static void Forget(string path) => Files.Remove(path);

    /// <summary>BlitFitRR: cover-fit the picture into the rounded rect, zoomed by zm about the anchor (px, py) in 0..1.</summary>
    public static void FitRR(Bitmap? bmp, double x, double y, double w, double h, double r, double f, double zm = 1.0, double px = 0.5, double py = 0.5)
    {
        if (bmp is null || w <= 0 || h <= 0 || f <= 0.004) return;
        double sw = bmp.PixelSize.Width, sh = bmp.PixelSize.Height;
        if (sw <= 0 || sh <= 0) return;
        double da = w / h, sa = sw / sh;
        double sw1, sh1;
        if (sa >= da) { sh1 = sh; sw1 = sh * da; } else { sw1 = sw; sh1 = sw / da; }
        sw1 /= zm; sh1 /= zm;
        double sx1 = (sw - sw1) * px, sy1 = (sh - sh1) * py;
        int cl = PushG();
        ClipRR(x, y, w, h, r);
        DrawImage(bmp, new Rect(x, y, w, h), new Rect(sx1, sy1, sw1, sh1), f);
        Pop(cl);
    }
    /// <summary>The whole picture stretched into the rect (GdipDrawImageRectRect over the full source), clipped to a rounded rect.</summary>
    public static void DrawRR(Bitmap? bmp, double x, double y, double w, double h, double r, double f)
    {
        if (bmp is null || w <= 0 || h <= 0 || f <= 0.004) return;
        int cl = PushG();
        ClipRR(x, y, w, h, r);
        DrawImage(bmp, new Rect(x, y, w, h), new Rect(0, 0, bmp.PixelSize.Width, bmp.PixelSize.Height), f);
        Pop(cl);
    }
    /// <summary>BlitCircleFit: cover-fit into a circle of radius rad about (cx, cy); false when there is no picture.</summary>
    public static bool CircleFit(Bitmap? bmp, double cx, double cy, double rad, double f)
    {
        if (bmp is null) return false;
        double sw = bmp.PixelSize.Width, sh = bmp.PixelSize.Height;
        if (sw <= 0 || sh <= 0) return false;
        double side = Math.Min(sw, sh);
        double sx1 = (sw - side) / 2, sy1 = (sh - side) / 2;
        int cl = PushG();
        ClipPath(new Avalonia.Media.EllipseGeometry(new Rect(cx - rad, cy - rad, rad * 2, rad * 2)));
        DrawImage(bmp, new Rect(cx - rad, cy - rad, rad * 2, rad * 2), new Rect(sx1, sy1, side, side), f);
        Pop(cl);
        return true;
    }
}
