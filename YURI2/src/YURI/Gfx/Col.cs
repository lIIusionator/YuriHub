using Avalonia.Media;
using Yuri.Core;

namespace Yuri.Gfx;

/// <summary>
/// Colour arithmetic on 0xAARRGGBB uints, exactly as the .ahk does it, so a
/// ported line like <c>Alpha(Mix(c1, c2, hv), 90 + 60*a)</c> reads and
/// rounds the same. Rounding is AutoHotkey's Round (half away from zero).
/// </summary>
public static class Col
{
    public static int R(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>Alpha(c, a): the colour's RGB with alpha a (0..255).</summary>
    public static uint Alpha(uint c, double a) => ((uint)(R(a) & 0xFF) << 24) | (c & 0xFFFFFF);
    /// <summary>FA(c, f): the colour's alpha scaled by f.</summary>
    public static uint FA(uint c, double f)
        => f >= 0.996 ? c : ((uint)(R(((c >> 24) & 0xFF) * f) & 0xFF) << 24) | (c & 0xFFFFFF);
    public static uint Mix(uint c1, uint c2, double t)
    {
        int a = R(Lerp((c1 >> 24) & 0xFF, (c2 >> 24) & 0xFF, t));
        int r = R(Lerp((c1 >> 16) & 0xFF, (c2 >> 16) & 0xFF, t));
        int g = R(Lerp((c1 >> 8) & 0xFF, (c2 >> 8) & 0xFF, t));
        int b = R(Lerp(c1 & 0xFF, c2 & 0xFF, t));
        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    }
    public static uint THMix(uint c1, uint c2, double t) => Mix(TH(c1), TH(c2), t);

    /// <summary>ElA(c): every element's alpha follows hubOpacity.</summary>
    public static uint ElA(uint c)
        => HubState.Opacity >= 0.996 ? c : ((uint)(R(((c >> 24) & 0xFF) * HubState.Opacity) & 0xFF) << 24) | (c & 0xFFFFFF);
    /// <summary>BgScale(c): the card plates' alpha follows bgOpacity.</summary>
    public static uint BgScale(uint c)
        => HubState.BgOpacity >= 0.996 ? (0xFF000000 | (c & 0xFFFFFF))
         : ((uint)(R(((c >> 24) & 0xFF) * HubState.BgOpacity) & 0xFF) << 24) | (c & 0xFFFFFF);

    // ---- AccHi: the accent's highlight, per theme, memoised per thT ----
    static Dictionary<long, uint> _ahMemo = new(); static double _ahMemoT = -1;
    public static uint AccHi(uint c, double t)
    {
        long ky = (long)c * 4096 + R(t * 1000);
        if (HubState.ThT != _ahMemoT) { _ahMemo = new(); _ahMemoT = HubState.ThT; }
        else if (_ahMemo.TryGetValue(ky, out var hit)) return hit;
        var v = AccHiCalc(c, t);
        if (_ahMemo.Count < 2048) _ahMemo[ky] = v;
        return v;
    }
    static uint AccHiCalc(uint c, double t)
    {
        uint dk = Mix(c, 0xFFFFFFFF, t);                  // the dark-theme highlight
        double thT = HubState.ThT;
        if (thT <= 0.002) return dk;
        uint m = Mix(c, 0xFF6A707E, t);
        int r = (int)((m >> 16) & 0xFF), g = (int)((m >> 8) & 0xFF), b = (int)(m & 0xFF);
        int sp = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        if (sp > 0 && sp < 0x3C)
        {
            double k = Math.Min(0x3C / (double)sp, 6.0);
            int y = (r * 30 + g * 59 + b * 11) / 100;
            r = Clamp(R(y + (r - y) * k), 0, 255);
            g = Clamp(R(y + (g - y) * k), 0, 255);
            b = Clamp(R(y + (b - y) * k), 0, 255);
            m = (m & 0xFF000000) | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }
        return thT >= 0.998 ? m : Mix(dk, m, thT);
    }

    /// <summary>AccSat(c): the accent saturated for the light theme.</summary>
    public static uint AccSat(uint c)
    {
        double thT = HubState.ThT;
        if (thT <= 0.002) return c;
        int r = (int)((c >> 16) & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)(c & 0xFF);
        int sp = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        if (sp <= 0 || sp >= 0x3C) return c;
        double k = Math.Min(0x3C / (double)sp, 6.0);
        int y = (r * 30 + g * 59 + b * 11) / 100;
        int nr = Clamp(R(y + (r - y) * k), 0, 255);
        int ng = Clamp(R(y + (g - y) * k), 0, 255);
        int nb = Clamp(R(y + (b - y) * k), 0, 255);
        uint m = (c & 0xFF000000) | ((uint)nr << 16) | ((uint)ng << 8) | (uint)nb;
        return thT >= 0.998 ? m : Mix(c, m, thT);
    }

    // ---- TH: the theme translation every brush and pen goes through ----
    static Dictionary<uint, uint> _thMemo = new(); static double _thMemoT = -1;
    public static uint TH(uint c)
    {
        double thT = HubState.ThT;
        if (thT <= 0.002) return c;
        if (thT != _thMemoT) { _thMemo = new(); _thMemoT = thT; }
        else if (_thMemo.TryGetValue(c, out var hit)) return hit;
        uint lc = THLight(c);
        uint v = thT >= 0.998 ? lc : Mix(c, lc, thT);
        if (_thMemo.Count < 2048) _thMemo[c] = v;
        return v;
    }

    static readonly Dictionary<uint, uint> LightMap = new()
    {
        [0x222438] = 0xFFFFFF, [0x121423] = 0xFCFCFD, [0x171A30] = 0xFFFFFF, [0x12141F] = 0xFDFDFE,
        [0x161930] = 0xFFFFFF, [0x10121C] = 0xFCFCFD, [0x10131F] = 0xFFFFFF, [0x0C0E17] = 0xFAFBFC,
        [0x141728] = 0xFFFFFF, [0x1B2038] = 0xF2F5FA, [0x1E2340] = 0xF6F8FC, [0x20254A] = 0xEEF3FB,
        [0x1C2142] = 0xF5F8FC, [0x232744] = 0xF0F4FB, [0x31344C] = 0xFFFFFF, [0x20223A] = 0xF1F3F7,
        [0x41466A] = 0xF6F8FC, [0x262A45] = 0xE9EDF4, [0x33354E] = 0xDFE3EB, [0x3A3D58] = 0xF2F4F8,
        [0x23263C] = 0xFFFFFF, [0x191C2E] = 0xF4F6F9, [0x0B0C14] = 0xF3F5F8, [0x05060C] = 0xF6F8FB,
        [0x6B7392] = 0x99A2B8,
    };
    /// <summary>THLight(c): the light-theme twin of a dark-theme colour.</summary>
    public static uint THLight(uint c)
    {
        uint rgb = c & 0xFFFFFF;
        if (rgb == 0 || rgb == 0xFEFEFE) return c;
        if (LightMap.TryGetValue(rgb, out var mapped)) return (c & 0xFF000000) | mapped;
        int r = (int)((rgb >> 16) & 0xFF), g = (int)((rgb >> 8) & 0xFF), b = (int)(rgb & 0xFF);
        int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
        if (mn >= 0xB0)
        {
            int a = (int)((c >> 24) & 0xFF);
            double sw = Clamp((0x40 - (mx - mn)) / 40.0, 0.0, 1.0);     // 1 neutral, 0 tinted
            if (mx - mn >= 8 && a > 0 && a < 0xFF)
            {
                a = Math.Min(0xFF, R(255 * Math.Sqrt(a / 255.0)));
                if (((c >> 24) & 0xFF) >= 0x30) a = Math.Max(a, 0x9B);
            }
            if (a <= 0x60 && sw > 0.5)
                return ((uint)(Math.Min(0xFF, R(a * 2.7)) & 0xFF) << 24) | 0x9BA3B4;
            double lum = (r * 30 + g * 59 + b * 11) / 25500.0;
            double kk = lum > 0.58 ? 0.58 / lum : 1.0;
            int nr = R(r * kk + (r * 0.13 - r * kk) * sw);
            int ng = R(g * kk + (g * 0.13 - g * kk) * sw);
            int nb = R(b * kk + (b * 0.15 - b * kk) * sw);
            return ((uint)a << 24) | ((uint)nr << 16) | ((uint)ng << 8) | (uint)nb;
        }
        if (mx <= 0x70)
        {
            int t = Math.Min(0xFE, 0xF8 + R((0x70 - (r * 30 + g * 59 + b * 11) / 100) * 0.05));
            return (c & 0xFF000000) | ((uint)t << 16) | ((uint)t << 8) | (uint)Math.Min(0xFF, t + 1);
        }
        if (mx - mn < 0x38)
        {
            int a2 = (int)((c >> 24) & 0xFF);
            if (a2 >= 0x30 && a2 < 0xFF)
                a2 = Math.Max(Math.Min(0xFF, R(255 * Math.Sqrt(a2 / 255.0))), 0xAD);
            double w2 = Clamp((0x38 - (mx - mn)) / 20.0, 0.0, 1.0);
            double lum = (r * 30 + g * 59 + b * 11) / 25500.0;
            double kk = lum > 0.58 ? 0.58 / lum : 1.0;
            int nr = R(r * kk + (r * 0.32 - r * kk) * w2);
            int ng = R(g * kk + (g * 0.32 - g * kk) * w2);
            int nb = R(b * kk + (b * 0.34 - b * kk) * w2);
            return ((uint)a2 << 24) | ((uint)nr << 16) | ((uint)ng << 8) | (uint)nb;
        }
        {
            double lum = (r * 30 + g * 59 + b * 11) / 25500.0;
            if (lum > 0.58)
            {
                double k = 0.58 / lum;
                return (c & 0xFF000000) | ((uint)R(r * k) << 16) | ((uint)R(g * k) << 8) | (uint)R(b * k);
            }
        }
        return c;
    }

    public static Color ToColor(uint argb)
        => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}

/// <summary>The easing curves, and the frame-rate compensation EK.</summary>
public static class Ease
{
    public static double Ease3(double t) => t <= 0 ? 0.0 : t >= 1 ? 1.0 : 1 - Math.Pow(1 - t, 3);
    public static double Ease3Inv(double y) => y <= 0 ? 0.0 : y >= 1 ? 1.0 : 1 - Math.Pow(1 - y, 1.0 / 3);
    public static double EBackOut(double t, double c1) => 1 + (c1 + 1) * Math.Pow(t - 1, 3) + c1 * (t - 1) * (t - 1);
    public static double MinIn(double t)
    {
        if (t < 0.20) return -0.06 * Math.Sin(t / 0.20 * 3.14159);
        double u = (t - 0.20) / 0.80;
        return u * u * (3 - 2 * u);
    }
    /// <summary>DecT(now): decoration time — frozen in LOW PERFORMANCE MODE so ambient motion stops.</summary>
    public static double DecT(long now) => HubState.LowPerf ? 0 : now;
    /// <summary>
    /// EK(k): an ease step that covers the same wall time whatever the frame
    /// rate; hubDtK is the effective period over TICK_A (see Pace).
    /// </summary>
    public static double EK(double k)
        => HubState.LowPerf ? 1.0 : ((Pace.DtK <= 1.0 || Pace.DtK > 2.1) ? k : 1 - Math.Pow(1 - k, Pace.DtK));
}
