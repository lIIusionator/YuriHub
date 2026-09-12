using Avalonia.Threading;

namespace Yuri.Core;

/// <summary>
/// SCREEN TIME. One tick a second: this hour's count, the all-time total,
/// and (when a system is armed or a placed script runs) the "system on"
/// count; flushed to zeal.ini's [usage] section every minute and at a day
/// change, under the .ahk's own keys (d&lt;yyyyMMdd&gt;, s&lt;yyyyMMdd&gt;, total, days).
/// </summary>
public static class Usage
{
    public static string Day = "";
    public static readonly int[] Tot = new int[24];        // usgTot, 1-based in the .ahk; index hour-1 here
    public static readonly int[] Sys = new int[24];        // usgSys
    public static long AllTot;                             // usgAllTot
    public static long Days;                               // usgDays
    /// <summary>Set by the modules that count as "system on": the autoblock, a running placed script.</summary>
    public static Func<bool>? SystemOn;
    static int _flushN;
    static DispatcherTimer? _timer;

    public static void Start()
    {
        Load();
        _timer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick());
        _timer.Start();
    }

    public static void Load()
    {
        Day = DateTime.Now.ToString("yyyyMMdd");
        Array.Clear(Tot); Array.Clear(Sys);
        string d = Ini.Read(Paths.IniFile, "usage", "d" + Day, "");
        string s = Ini.Read(Paths.IniFile, "usage", "s" + Day, "");
        if (d != "")
        {
            var pa = d.Split(',');
            for (int i = 0; i < Math.Min(24, pa.Length); i++) if (int.TryParse(pa[i].Trim(), out var v)) Tot[i] = v;
        }
        if (s != "")
        {
            var ps = s.Split(',');
            for (int i = 0; i < Math.Min(24, ps.Length); i++) if (int.TryParse(ps[i].Trim(), out var v)) Sys[i] = v;
        }
        AllTot = Ini.ReadInt(Paths.IniFile, "usage", "total", 0);
        Days = Ini.ReadInt(Paths.IniFile, "usage", "days", 0);
        if (d == "")
        {
            Days += 1;
            Ini.Write(Paths.IniFile, "usage", "days", Days);
        }
    }

    public static void Flush()
    {
        Ini.Write(Paths.IniFile, "usage", "d" + Day, string.Join(",", Tot));
        Ini.Write(Paths.IniFile, "usage", "s" + Day, string.Join(",", Sys));
        Ini.Write(Paths.IniFile, "usage", "total", AllTot);
    }

    static void Tick()
    {
        string dk = DateTime.Now.ToString("yyyyMMdd");
        if (dk != Day) { Flush(); Load(); }
        int hr = DateTime.Now.Hour;
        Tot[hr] += 1;
        AllTot += 1;
        if (SystemOn?.Invoke() == true) Sys[hr] += 1;
        if (++_flushN >= 60) { _flushN = 0; Flush(); }
    }

    /// <summary>FmtHrs(secs): "3h 12m" or "12m".</summary>
    public static string FmtHrs(long secs)
    {
        long h = secs / 3600, m = (secs / 60) % 60;
        return h != 0 ? $"{h}h {m}m" : $"{m}m";
    }
    /// <summary>FmtUp(ms): "h:mm:ss" past an hour, else "mm:ss".</summary>
    public static string FmtUp(long ms)
    {
        long t = ms / 1000;
        return t >= 3600 ? $"{t / 3600}:{(t / 60) % 60:00}:{t % 60:00}" : $"{t / 60:00}:{t % 60:00}";
    }
}
