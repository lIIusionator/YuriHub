using System.Text;

namespace Yuri.Core;

/// <summary>
/// The frame-rate tiers and the pacing. PERIODS in ms, so smaller is faster:
///   TICK_A 16  active: something animates or the pointer is on the hub
///   TICK_S 33  settled
///   TICK_Z 50  deep idle: nothing animating, pointer away for HUB_DEEP_MS
///   TICK_G 100 in-game stand-down (FPS BOOST on, Roblox in front), from deep idle only
///   TICK_BG 1000 nobody can see it: covered by a full-screen game
///   TICK_LP 250 LOW PERFORMANCE MODE's one idle rate; every other idle tier is a STOP there
/// The active tier asks for a frame every TICK_A ms. A frame that takes 40 ms
/// cannot be delivered every 16, so the renderer times itself and asks for the
/// next frame no sooner than the CHEAPEST recent frame plus a fixed gap for the
/// message loop (Note). DtK is the effective period over TICK_A; Ease.EK uses
/// it so an ease covers the same wall time whatever the frame rate.
/// </summary>
public static class Pace
{
    public const int TICK_A = 16, TICK_S = 33, TICK_D = 33, TICK_Z = 50, TICK_G = 100, TICK_BG = 1000, TICK_LP = 250;
    public const int HUB_DEEP_MS = 4000;
    public const int ANIM_MS = 240, INTRO_MS = 520, RIPPLE_MS = 480, MIN_MS = 340, CLOSE_MS = 420, TOG_MS = 650;

    public static int HubPace = 16;            // hubPace: the period the active tier is allowed
    public static int PEff;                    // hubPEff
    public static double FtEma;                // hubFtEma: for the readout on the PERFORMANCE tile
    public static double DtK = 1.0;            // hubDtK
    static readonly List<double> Ring = new(); // hubFtRing: the last eight frames

    /// <summary>HubPaceNote(ms): fed the draw time of every frame.</summary>
    public static void Note(double ms)
    {
        if (ms <= 0 || ms > 2000) return;
        FtEma = FtEma != 0 ? FtEma * 0.85 + ms * 0.15 : ms;
        Ring.Add(ms);
        if (Ring.Count > 8) Ring.RemoveAt(0);
        double mn = ms;
        foreach (var v in Ring) if (v < mn) mn = v;
        // the cheapest recent frame decides the tier; a spike is ignored, a slow machine is tracked
        HubPace = mn <= 12 ? 16 : mn <= 21 ? 25 : mn <= 29 ? 33 : mn <= 36 ? 40 : mn <= 46 ? 50 : 66;
    }

    /// <summary>ModalTick(): the splash / gate / picker loops' one rate.</summary>
    public static int ModalTick() => Gfx.HubState.LowPerf ? 40 : TICK_A;
}

/// <summary>
/// The frame profiler. [hub] profile=1 in zeal.ini. Every primitive then times
/// itself, and once a second a line goes to YURI\perf.log saying where the
/// frame went. Off, the primitives pay one static read each.
/// </summary>
public static class Perf
{
    public static bool On => Gfx.HubState.Prof;
    static readonly Dictionary<string, long> T = new();
    static readonly Dictionary<string, long> N = new();
    static long _at; static int _fr; static double _draw, _pres;

    public static long Now() => Clock.Qpc();
    public static void Add(string k, long t0)
    {
        T[k] = T.GetValueOrDefault(k) + (Clock.Qpc() - t0);
        N[k] = N.GetValueOrDefault(k) + 1;
    }
    public static void Count(string k) => N[k] = N.GetValueOrDefault(k) + 1;

    /// <summary>The buckets, per frame, for the bench - the same numbers perf.log carries, without waiting a second for them.</summary>
    public static void Reset() { T.Clear(); N.Clear(); }
    public static string Report(int frames)
    {
        double ms = 1000.0 / Clock.Qpf; int fr = Math.Max(1, frames);
        var parts = new List<string>();
        foreach (var kv in T.OrderByDescending(k => k.Value))
            parts.Add($"{kv.Key} {Math.Round(kv.Value * ms / fr, 2)}ms/{Math.Round((double)N.GetValueOrDefault(kv.Key) / fr)}");
        return string.Join("  ", parts);
    }

    /// <summary>PfFrame(): called at the end of every frame with the frame's draw and present ticks.</summary>
    public static void Frame(long drawT, long presT, long now)
    {
        _fr++; _draw += drawT; _pres += presT;
        if (_at == 0) _at = now;
        if (now - _at < 1000) return;
        double ms = 1000.0 / Clock.Qpf;
        int fr = Math.Max(1, _fr);
        var line = new StringBuilder();
        line.Append(DateTime.Now.ToString("HH:mm:ss")).Append("  ").Append(fr).Append(" frames  ")
            .Append("frame ").Append(Math.Round((_draw + _pres) * ms / fr, 1)).Append(" ms = draw ").Append(Math.Round(_draw * ms / fr, 1))
            .Append(" + present ").Append(Math.Round(_pres * ms / fr, 1)).Append(" |");
        double acc = 0;
        foreach (var k in new[] { "txt", "fill", "stroke", "line", "ell", "tex", "shadow", "img" })
        {
            long tk = T.GetValueOrDefault(k), nk = N.GetValueOrDefault(k);
            acc += tk;
            string extra = (k == "txt" && nk > 0) ? " " + Math.Round(100.0 * N.GetValueOrDefault("txtc") / nk) + "% cached" : "";
            extra += (k == "fill" && nk > 0) ? " " + Math.Round(100.0 * N.GetValueOrDefault("fillc") / nk) + "% plates" : "";
            line.Append(' ').Append(k).Append(' ').Append(Math.Round(tk * ms / fr, 1)).Append('/').Append(Math.Round((double)nk / fr)).Append(extra).Append(" |");
        }
        line.Append(" rest ").Append(Math.Round((_draw - acc) * ms / fr, 1));
        try { File.AppendAllText(Paths.PerfLog, line + "\n", Encoding.UTF8); } catch { }
        T.Clear(); N.Clear(); _at = now; _fr = 0; _draw = 0; _pres = 0;
    }
}
