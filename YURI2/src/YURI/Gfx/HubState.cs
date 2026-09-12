using Yuri.Core;

namespace Yuri.Gfx;

/// <summary>
/// The hub's persisted globals ([hub] and [profile] in zeal.ini) and the
/// theme transition. In the .ahk these are loose globals read by every drawing
/// helper (hubAccent, hubTheme, hubLowPerf, hubOpacity, bgOpacity, thT ...);
/// they keep their names here so the module ports read the same way.
/// </summary>
public static class HubState
{
    public const uint C_ON  = 0xFF34D399;
    public const uint C_OFF = 0xFFFB7185;
    /// <summary>The six ACCENTS: rose (default), orange, amber, violet, sky, teal.</summary>
    public static readonly uint[] Accents = { 0xFFFB7185, 0xFFF97316, 0xFFFBBF24, 0xFFA78BFA, 0xFF38BDF8, 0xFF2DD4BF };

    public static uint   Accent   = 0xFFFB7185;   // hubAccent
    public static uint   Cur      = 0xFFFB7185;   // hubCur: the accent as drawn (SelTick eases it)
    public static int    Theme    = 0;            // hubTheme: 0 dark, 1 light
    public static int    ArmTint  = 1;            // hubArmTint
    public static bool   LowPerf  = false;        // hubLowPerf: LOW PERFORMANCE MODE (flat surfaces, no textures)
    public static bool   Lean     = false;        // hubLean: set per frame on a slow machine (frames > ~22 ms)
    public static bool   Raster   = true;         // hubRaster: plate cache on
    public static bool   Prof     = false;        // hubProf: [hub] profile=1 -> YURI\perf.log
    public static bool   OnTop    = true;         // hubTop
    public static bool   TrueMin  = false;        // hubTrueMin
    public static bool   AutoUpdate = false;      // [hub] autoupdate
    public static double Opacity  = 1.0;          // hubOpacity: every element's alpha (ElA)
    public static double BgOpacity = 1.0;         // bgOpacity: the card plates' alpha (BgScale)
    public static double Mag      = 1.2;          // hubMag: the hub's size over its base 832x572
    public static int    ProfileFont = 1;         // [profile] font: 1 Segoe UI, 2 Bahnschrift, 3 Consolas, 4 Georgia
    public static string ProfName = "USERNAME", ProfBio = "EMPTY BIO"; public static int ProfRing = 1;   // [profile] name / bio / ring

    // ---- theme transition: THEME_MS, thT / thAt / thFrom / thTo ----
    public const int THEME_MS = 520;
    public static double ThT;                     // 0 dark ... 1 light, eased
    static long _thAt; static double _thFrom, _thTo;
    /// <summary>thAt while the theme is moving, else null.</summary>
    public static long? ThemeMoving => _thAt == 0 ? null : _thAt;

    public static void Load()
    {
        var ini = Paths.IniFile;
        Accent   = Ini.ReadArgb(ini, "hub", "accent", 0xFFFB7185); Cur = Accent;
        Theme    = (int)Ini.ReadInt(ini, "hub", "theme", 0);
        ArmTint  = (int)Ini.ReadInt(ini, "hub", "armtint", 1);
        LowPerf  = Ini.ReadInt(ini, "hub", "lowperf", 0) != 0;
        OnTop    = Ini.ReadInt(ini, "hub", "ontop", 1) != 0;
        TrueMin  = Ini.ReadInt(ini, "hub", "truemin", 0) != 0;
        Raster   = Ini.ReadInt(ini, "hub", "raster", 1) != 0;
        Prof     = Ini.ReadInt(ini, "hub", "profile", 0) != 0;
        AutoUpdate = Ini.ReadInt(ini, "hub", "autoupdate", 0) != 0;
        Opacity  = Math.Clamp(Ini.ReadNum(ini, "hub", "opacity", 1.0), 0.35, 1.0);
        BgOpacity = Math.Clamp(Ini.ReadNum(ini, "hub", "bg", 1.0), 0.15, 1.0);
        Mag      = Math.Clamp(Ini.ReadNum(ini, "hub", "scale", 1.2), 1.0, 1.6);
        ProfileFont = (int)Math.Clamp(Ini.ReadInt(ini, "profile", "font", 1), 1, 4);
        ProfName = Ini.Read(ini, "profile", "name", "USERNAME"); if (ProfName.Trim() == "") ProfName = "USERNAME";
        ProfBio = Ini.Read(ini, "profile", "bio", "EMPTY BIO"); if (ProfBio.Trim() == "") ProfBio = "EMPTY BIO";
        ProfRing = (int)Math.Clamp(Ini.ReadInt(ini, "profile", "ring", 1), 1, 8);
        ThT = Theme == 1 ? 1.0 : 0.0; _thTo = ThT; _thFrom = ThT; _thAt = 0;
    }

    /// <summary>Start the eased move to the other theme (the THEME switch).</summary>
    public static void ThemeSet(int theme)
    {
        Theme = theme;
        _thFrom = ThT; _thTo = theme == 1 ? 1.0 : 0.0; _thAt = Clock.Tick;
        Ini.Write(Paths.IniFile, "hub", "theme", theme);
    }

    /// <summary>ThemeTick(): advance thT; called at the top of every frame.</summary>
    public static void ThemeTick()
    {
        if (_thAt == 0) return;
        double t = Math.Min((Clock.Tick - _thAt) / (double)THEME_MS, 1.0);
        ThT = Col.Lerp(_thFrom, _thTo, 1 - Math.Pow(1 - t, 3));
        if (t >= 1) { _thAt = 0; ThT = _thTo; }
    }
}
