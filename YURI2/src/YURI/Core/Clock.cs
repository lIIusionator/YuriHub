using System.Diagnostics;

namespace Yuri.Core;

/// <summary>A_TickCount, QueryPerformanceCounter, FormatTime — one place.</summary>
public static class Clock
{
    /// <summary>A_TickCount: milliseconds, monotonic. Every `now` in a render loop is this.</summary>
    public static long Tick => Environment.TickCount64;
    /// <summary>QueryPerformanceCounter / QueryPerformanceFrequency (hubQpf).</summary>
    public static long Qpc() => Stopwatch.GetTimestamp();
    public static readonly long Qpf = Stopwatch.Frequency;
    public static double QpcMs(long dt) => dt * 1000.0 / Qpf;

    static string _clkStr = ""; static long _clkAt;
    /// <summary>ClockStr(): HH:mm:ss, refreshed every 500 ms.</summary>
    public static string ClockStr()
    {
        if (Tick - _clkAt >= 500 || _clkStr == "") { _clkStr = DateTime.Now.ToString("HH:mm:ss"); _clkAt = Tick; }
        return _clkStr;
    }
    static int _curHr; static long _curHrAt;
    /// <summary>CurHour(): 1..24, refreshed every 10 s.</summary>
    public static int CurHour()
    {
        if (Tick - _curHrAt >= 10000 || _curHrAt == 0) { _curHr = DateTime.Now.Hour + 1; _curHrAt = Tick; }
        return _curHr;
    }
}
