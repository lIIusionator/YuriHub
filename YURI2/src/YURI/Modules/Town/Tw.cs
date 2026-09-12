using Yuri.Core;

namespace Yuri.Modules.Town;

/// <summary>The town state (TW): the three towns' spark counts and levels, the spark in play, the burst / plus / level ribbon timers, the bar.</summary>
public static class Tw
{
    public static readonly int[] N = new int[3];
    public static readonly int[] LV = { 3, 8, 15, 25, 40 };
    public static readonly string[][] REW = { new[] { "bunting between the lamps", "a ferris wheel", "a lighthouse", "a blimp", "fireworks all night" }, new[] { "kites over the roofs", "a food truck", "a tram", "a rooftop party", "a parade" }, new[] { "sheep in the meadow", "a windmill", "a carousel", "a balloon", "a festival" } };
    public static double sx, sy; public static long at, next, burstAt, plusAt, lvAt; public static int last, kind = 1, plusN = 1, lvNew, barK; public static double bx, by, barE;
    public static int Town = 1; public static long TownAt; public static int TownPrev;
    public static int LevelOf(double n) { int lv = 0; for (int i = 0; i < LV.Length; i++) if (n >= LV[i]) lv = i + 1; return lv; }
    public static double Level(double k) => LevelOf(N[(int)k - 1]);
    public static void Load()
    {
        Town = Math.Clamp((int)Ini.ReadInt(Paths.IniFile, "hub", "town", 1), 1, 4);
        for (int i = 1; i <= 3; i++) N[i - 1] = Math.Max(0, (int)Ini.ReadInt(Paths.IniFile, "town", "n" + i, 0));
        next = Clock.Tick + 4000;
    }
}
