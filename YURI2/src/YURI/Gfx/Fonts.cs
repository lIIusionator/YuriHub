using System.Globalization;
using Avalonia.Media;

namespace Yuri.Gfx;

/// <summary>A GDI+ font as the .ahk used it: a family, a pixel size (UnitPixel), bold or not.</summary>
public sealed class Font
{
    public readonly Typeface Face;
    public readonly double Size;        // em height in logical pixels — GDI+ unit 2 (UnitPixel) under the world transform
    public readonly bool Bold;
    public Font(FontFamily family, double size, bool bold)
    {
        Face = new Typeface(family, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal);
        Size = size; Bold = bold;
    }
    /// <summary>GDI+ DrawString / MeasureString pad the layout by about 1/6 em each side.</summary>
    public double Pad => Size / 6.0;
}

/// <summary>Text alignment: fmtL / fmtC / fmtR, always vertically centred, no wrap, no clip.</summary>
public enum Fmt { L = 0, C = 1, R = 2 }

/// <summary>
/// fam / famE / famM and the hub's fonts. The profile's font picks the
/// family: 1 Segoe UI, 2 Bahnschrift, 3 Consolas, 4 Georgia — with a macOS
/// twin for each, since none of the first three ship there. Avalonia falls
/// back per glyph for symbols and emoji, so the emoji twin (fHintE) is the
/// same face.
/// </summary>
public static class Fonts
{
    public static FontFamily Fam { get; private set; } = new("Segoe UI");
    public static FontFamily FamE { get; private set; } = new("Segoe UI Emoji");
    public static FontFamily FamM { get; private set; } = new("Consolas");

    public static Font fStatus = null!, fKey = null!, fBadge = null!, fHint = null!, fBrand = null!, fHintE = null!;
    public static Font fP = null!, fPs = null!;                       // ProfFontApply: the profile name and bio, in the chosen HUB FONT
    public static double wOn, wOff, wZeal, wGal;                  // measured once, like the .ahk's globals
    public static double qqqBW;

    public static void Init(int profileFont)
    {
        // Windows name first, then the macOS stand-in, then anything.
        Fam = profileFont switch
        {
            2 => new FontFamily("Bahnschrift, Avenir Next Condensed, DIN Alternate, Arial Narrow, Arial"),
            3 => new FontFamily("Consolas, Menlo, Courier New, monospace"),
            4 => new FontFamily("Georgia, Times New Roman, serif"),
            _ => new FontFamily("Segoe UI, Helvetica Neue, Arial, sans-serif"),
        };
        FamE = new FontFamily("Segoe UI Emoji, Segoe UI Symbol, Apple Color Emoji, Noto Color Emoji");
        FamM = new FontFamily("Consolas, Menlo, DejaVu Sans Mono, Courier New, monospace");
        fStatus = New(13.5, true); fKey = New(14, true);
        fBadge = New(9.5, true); fHint = New(10.5, false); fBrand = New(10.5, true);
        fHintE = fHint;
        fP = New(10.5, true); fPs = New(8.5, false);
        Measure.Clear();
        wOn = MeasureW("ENABLED", fStatus); wOff = MeasureW("DISABLED", fStatus);
        wZeal = MeasureW(Core.AppInfo.AppName, fBrand);
        wGal = MeasureW("GALLERY", fBrand);
        qqqBW = Math.Max(38, MeasureW("???", fBadge) + 16);
    }

    /// <summary>NewFont(sz, style): style 1 bold, 0 regular.</summary>
    public static Font New(double size, bool bold) => new(Fam, size, bold);
    public static Font NewE(double size, bool bold) => new(FamE, size, bold);
    public static Font NewM(double size, bool bold) => new(FamM, size, bold);

    // ---- MeasureW: cached per (text, font), like the .ahk's static Map ----
    static readonly Dictionary<(string, Font), double> Measure = new();
    /// <summary>The glyph advance alone, no pads: the distance from a drawn string's first glyph to where its next glyph would start.</summary>
    public static double Adv(string s, Font font) => s.Length == 0 ? 0 : MeasureW(s, font) - font.Pad * 2;
    public static double MeasureW(string s, Font font)
    {
        if (s.Length == 0) return font.Pad * 2;
        if (Measure.TryGetValue((s, font), out var w)) return w;
        var ft = Layout(s, font, null);
        w = ft.WidthIncludingTrailingWhitespace + font.Pad * 2;
        if (Measure.Count < 8192) Measure[(s, font)] = w;
        return w;
    }
    public static void MeasureClear() { Measure.Clear(); Runs.Clear(); Laid.Clear(); }

    // ---- one layout per string, not one per draw ----
    // TxtP built a fresh FormattedText every time it drew, which re-shapes the
    // run - the single most expensive thing in a frame, and the hub draws twenty
    // to thirty strings in every one of them. The layout is independent of
    // colour, so it is cached and the brush is set on it at draw time.
    static readonly Dictionary<(string, Font), FormattedText> Laid = new();
    public static FormattedText Cached(string s, Font font)
    {
        if (Laid.TryGetValue((s, font), out var ft)) return ft;
        ft = Layout(s, font, null);
        if (Laid.Count < 4096) Laid[(s, font)] = ft;
        return ft;
    }

    // ---- Run: the caret x of every character, from ONE shaped layout ----
    // Measuring prefixes separately re-shapes each prefix, and the rounding
    // drifts a fraction of a pixel per glyph. Over a dozen wide letters that is
    // enough for a selection to end visibly short of the text it covers and for
    // the caret to sit where nothing is going to be typed. Hit-testing a single
    // layout is exact by construction and cannot drift.
    static readonly Dictionary<(string, Font), double[]> Runs = new();
    public static double[] Run(string s, Font font)
    {
        if (s.Length == 0) return Zero;
        if (Runs.TryGetValue((s, font), out var xs)) return xs;
        var tl = new Avalonia.Media.TextFormatting.TextLayout(s, font.Face, font.Size, null, maxLines: 1);
        xs = new double[s.Length + 1];
        for (int i = 0; i < s.Length; i++) xs[i] = tl.HitTestTextPosition(i).X;
        xs[s.Length] = Layout(s, font, null).WidthIncludingTrailingWhitespace;
        if (Runs.Count < 4096) Runs[(s, font)] = xs;
        return xs;
    }
    static readonly double[] Zero = { 0.0 };
    /// <summary>The x of the caret sitting before character i of s, in s's own shaped run.</summary>
    public static double AdvAt(string s, Font font, int i) => Run(s, font)[Math.Clamp(i, 0, s.Length)];

    /// <summary>One line, no wrap: the FormattedText behind Txt and MeasureW.</summary>
    public static FormattedText Layout(string s, Font font, IBrush? brush)
        => new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font.Face, font.Size, brush)
        { MaxLineCount = 1, Trimming = TextTrimming.None };

    /// <summary>BuildScr(text, font): per-character x offsets for the scramble effect.</summary>
    public static Scr BuildScr(string text, Font font)
    {
        var chs = new List<string>(); var xs = new List<double>();
        for (int i = 0; i < text.Length; i++)
        {
            chs.Add(text[i].ToString());
            xs.Add(i == 0 ? 0.0 : MeasureW(text[..i], font));
        }
        return new Scr(chs, xs, MeasureW(text, font));
    }
    public sealed record Scr(List<string> Chs, List<double> Xs, double W);
}
