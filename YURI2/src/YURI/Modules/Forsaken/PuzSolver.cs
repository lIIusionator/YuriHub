using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Forsaken;

public sealed class PDot { public int r, c; public uint col; }
public sealed class PPair { public PDot a = null!, b = null!; }

/// <summary>
/// PUZZLE AI's solver, the .ahk's Puz* engine on a worker thread: the grid
/// geometry (the reference table per screen size, or the grid you set), the
/// scan (one pixel per cell, off-centre to clear the digit), the pairing
/// (exact colour, then to within eight per channel, then nearest mutual),
/// the search (a depth-first walk that keeps every other pair's ends alive
/// and every remaining pair reachable), the reroute pass (fewer corners),
/// and the drawing (a hover, press, glide cell by cell at the slider's
/// frame budget, a verify capture, the release). Held key = keep going
/// board after board; a second press cancels.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PuzSolver
{
    // ---- constants, the .ahk's ----
    const double SampleOffset = 0.22, CellFrames = 1.5, StartFrames = 1.1, PressFrames = 1.0, CornerFrames = 2.0, EndFrames = 0.75, SlackFrames = 0.25, LagFrames = 1.0, CellDwell = 0.0, ApproachSpeed = 160.0, SlowStep = 1.8, PACE_UP = 1.30, PACE_DOWN = 0.90, PACE_MAX = 2.60;
    const int BrightThresh = 70, PairTol = 60, TailBackCells = 0, JigglePx = 2, JiggleDwell = 5, FrameInterval = 1, ScanSettle = 24, SolveMs = 4000, BoardMs = 12000, DepthMax = 480, SettleMs = 40, MaxRuns = 300, RepairMax = 2, MaxFails = 6, RetryWait = 120, NextPoll = 15, NextTimeout = 2500, PACE_RUN = 2;
    static readonly (int w, int h, double box, double cx, double cy, int ban)[] REF = { (800, 600, 196.80, 7.50, 18.50, 31), (1024, 768, 256.20, -0.55, -0.55, 39), (1920, 1080, 517.20, -0.50, -0.50, 54) };
    // ---- state ----
    public static int vx, vy, vw, vh;
    public static bool Solving; static volatile bool _cancel, _cancelUp, _stop; static int _iter, _iterCap, _solveOut; static long _solveEnd, _boardEnd; static string _boardOut = "";
    static double _retryMul = 1.0, _paceMul = 1.0; static int _paceOk, _runs, _total, _sess, _lines; static string _stage = "";
    public static int tScan, tPair, tSolve, tOpt, tDraw;
    static Thread? _worker;

    [DllImport("user32.dll")] static extern int GetSystemMetrics(int n);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
    [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr hdc, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct BITMAPINFO { public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant; public uint pad; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    static readonly IntPtr OurTag = new(0x59555249);

    // ---- geometry ----
    /// <summary>
    /// PuzScreenSync: the virtual screen. Defensive because every number the
    /// grid is made of is derived from it - a metrics read that fails must not
    /// leave the solver believing the desktop is zero wide.
    /// </summary>
    public static void ScreenSync()
    {
        try { vx = GetSystemMetrics(76); vy = GetSystemMetrics(77); vw = GetSystemMetrics(78); vh = GetSystemMetrics(79); } catch { }
        if (vw < 1 || vh < 1) { vx = 0; vy = 0; vw = 1920; vh = 1080; }
    }
    static int N => Puz.GridN;
    /// <summary>PZ_RefExact: this resolution is IN the measured table, so the default is a measurement rather than an interpolation.</summary>
    public static bool RefExact;
    static (double p, double ox, double oy, double ban) DefRef()
    {
        if (vw < 1) ScreenSync();
        double box, cx, cy, ban;
        var exact = REF.FirstOrDefault(e => e.w == vw && e.h == vh);
        if (exact.w != 0) { box = exact.box; cx = exact.cx; cy = exact.cy; ban = exact.ban; RefExact = true; }
        else
        {
            RefExact = false;
            int lo = 0, hi = REF.Length - 1;
            for (int i = 0; i < REF.Length - 1; i++) if (vw >= REF[i].w && vw <= REF[i + 1].w) { lo = i; hi = i + 1; break; }
            if (vw < REF[0].w) { lo = 0; hi = 1; } else if (vw > REF[^1].w) { lo = REF.Length - 2; hi = REF.Length - 1; }
            var A = REF[lo]; var B = REF[hi];
            double t = B.w == A.w ? 0 : (vw - A.w) / (double)(B.w - A.w);
            // the banner is TEXT and tracks HEIGHT, so it interpolates on height
            // even though everything else here interpolates on width
            double tb = B.h == A.h ? 0 : (vh - A.h) / (double)(B.h - A.h);
            box = A.box + (B.box - A.box) * t; cx = A.cx + (B.cx - A.cx) * t; cy = A.cy + (B.cy - A.cy) * t;
            ban = A.ban + (B.ban - A.ban) * tb;
        }
        double p = box / N;
        return (p, (N - 1) / 2.0 * p - cx, (N - 1) / 2.0 * p - cy, ban);
    }
    /// <summary>PZ_BannerDy(): how far the board drops when the three-line banner is above it.</summary>
    public static int BannerDy() => (int)Math.Round(DefRef().ban);
    /// <summary>PuzDefScale(): the reference pitch this screen's default is derived from, against the measured 1080p one.</summary>
    public static double DefScale() { _ = DefRef(); return Math.Round(DefRef().p * N / 517.20, 3); }
    public static double SX => Puz.Custom ? Math.Round(Puz.CustBW / N, 4) : Math.Round(DefRef().p, 4);
    public static double SY => Puz.Custom ? Math.Round(Puz.CustBH / N, 4) : Math.Round(DefRef().p, 4);
    public static double X0 => Puz.Custom ? Math.Round(Puz.CustBX + SX / 2, 2) : Math.Round((vx + vw / 2.0) - DefRef().ox, 2);
    public static double Y0 => Puz.Custom ? Math.Round(Puz.CustBY + SY / 2, 2) : Math.Round((vy + vh / 2.0) - DefRef().oy, 2);
    public static int PX(int c) => (int)Math.Round(X0 + (c - 1) * SX);
    // The calibration is X0/Y0 and the pitch. PZ_boardDy is a fact about where
    // the panel is sitting right now, so it is added at READ rather than baked
    // in - otherwise the two would stop being separable, and the offset would
    // have to invalidate the calibration every time the banner appeared.
    public static int PY(int r) => (int)Math.Round(Y0 + (r - 1) * SY + Puz.BoardDy);
    public static bool GridOnScreen() { int x0 = PX(1), y0 = PY(1), x1 = PX(N), y1 = PY(N); return x0 >= vx && x1 <= vx + vw && y0 >= vy && y1 <= vy + vh; }
    /// <summary>The inspector's readout: the constants it reports, and the derived numbers worth checking against the board by eye.</summary>
    public static double SampleOff => SampleOffset;
    public static int Thresh => BrightThresh;
    public static double CellMs => CellFrames * Puz.FrameMs * PitchScale;
    public static double PxPerMs => SX / Math.Max(1, CellMs);
    public static void BoardRectPublic(out int bx, out int by, out int bw, out int bh) => BoardRect(out bx, out by, out bw, out bh);
    static void BoardRect(out int bx, out int by, out int bw, out int bh) { int off = (int)Math.Round(SampleOffset * SX); bx = PX(1) - off - 2; by = PY(1) - 2; bw = PX(N) + off + 2 - bx + 1; bh = PY(N) + 2 - by + 1; }

    // ---- capture ----
    public sealed class Cap { public int x, y, w, h; public byte[] px = Array.Empty<byte>(); public uint At(int cx, int cy) { if (cx < 0 || cy < 0 || cx >= w || cy >= h) return 0; int i = (cy * w + cx) * 4; return (uint)(px[i + 2] << 16 | px[i + 1] << 8 | px[i]); } }
    public static Func<int, int, int, int, Cap>? CaptureOverride;
    static Cap Capture(int x, int y, int w, int h)
    {
        if (CaptureOverride is not null) return CaptureOverride(x, y, w, h);
        var cap = new Cap { x = x, y = y, w = w, h = h, px = new byte[w * h * 4] };
        IntPtr sdc = GetDC(IntPtr.Zero), mdc = CreateCompatibleDC(sdc);
        var bmi = new BITMAPINFO { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        IntPtr bmp = CreateDIBSection(mdc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
        if (bmp != IntPtr.Zero)
        {
            IntPtr old = SelectObject(mdc, bmp);
            BitBlt(mdc, 0, 0, w, h, sdc, x, y, 0x00CC0020);
            Marshal.Copy(bits, cap.px, 0, cap.px.Length);
            SelectObject(mdc, old); DeleteObject(bmp);
        }
        DeleteDC(mdc); ReleaseDC(IntPtr.Zero, sdc);
        return cap;
    }
    static int Brightness(uint col) => Math.Max((int)(col >> 16 & 0xFF), Math.Max((int)(col >> 8 & 0xFF), (int)(col & 0xFF)));
    static double ColorDist(uint a, uint b) { double dr = (int)(a >> 16 & 0xFF) - (int)(b >> 16 & 0xFF), dg = (int)(a >> 8 & 0xFF) - (int)(b >> 8 & 0xFF), db = (int)(a & 0xFF) - (int)(b & 0xFF); return Math.Sqrt(dr * dr + dg * dg + db * db); }
    static long ColorDist2(uint a, uint b) { long dr = (int)(a >> 16 & 0xFF) - (int)(b >> 16 & 0xFF), dg = (int)(a >> 8 & 0xFF) - (int)(b >> 8 & 0xFF), db = (int)(a & 0xFF) - (int)(b & 0xFF); return dr * dr + dg * dg + db * db; }
    static uint ColorKey(uint col, int shift) => (uint)((((col >> 16) & 0xFF) >> shift) << 16 | (((col >> 8) & 0xFF) >> shift) << 8 | ((col & 0xFF) >> shift));
    /// <summary>PuzScanGrid: one pixel per cell, off-centre to clear the digit, a second try on the other side when the first is dark.</summary>
    public static List<PDot> ScanGrid(Cap? cap = null)
    {
        var dots = new List<PDot>();
        int off = (int)Math.Round(SampleOffset * SX);
        int x0, y0;
        if (cap is null) { BoardRect(out x0, out y0, out int w, out int h); cap = Capture(x0, y0, w, h); } else { x0 = cap.x; y0 = cap.y; }
        for (int r = 1; r <= N; r++)
        {
            int ry = PY(r) - y0;
            for (int c = 1; c <= N; c++)
            {
                int cx = PX(c) - x0;
                uint col = cap.At(cx + off, ry);
                if (Brightness(col) <= BrightThresh) col = cap.At(cx - off, ry);
                if (Brightness(col) > BrightThresh) dots.Add(new PDot { r = r, c = c, col = col });
            }
        }
        return dots;
    }

    // ---- pairing ----
    static List<int> MatchBucket(List<PDot> dots, List<int> ix, List<PPair> pairs)
    {
        var used = new HashSet<int>();
        foreach (int i in ix)
        {
            if (BoardOver("pairing") || CancelPoll()) break;
            if (used.Contains(i)) continue;
            int bj = -1, bd = 999999;
            foreach (int j in ix) { if (j == i || used.Contains(j)) continue; int dd = Math.Abs(dots[i].r - dots[j].r) + Math.Abs(dots[i].c - dots[j].c); if (dd < bd) { bd = dd; bj = j; } }
            if (bj < 0) continue;
            used.Add(i); used.Add(bj); pairs.Add(new PPair { a = dots[i], b = dots[bj] });
        }
        return ix.Where(i => !used.Contains(i)).ToList();
    }
    static List<int> PairByKey(List<PDot> dots, List<int> ix, int shift, List<PPair> pairs)
    {
        var buckets = new Dictionary<uint, List<int>>();
        foreach (int i in ix) { uint k = ColorKey(dots[i].col, shift); if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<int>(); l.Add(i); }
        var left = new List<int>();
        foreach (var bx in buckets.Values)
        {
            if (bx.Count == 2) { pairs.Add(new PPair { a = dots[bx[0]], b = dots[bx[1]] }); continue; }
            if (bx.Count == 1) { left.Add(bx[0]); continue; }
            left.AddRange(MatchBucket(dots, bx, pairs));
        }
        return left;
    }
    static int PairDist(PPair p) => Math.Abs(p.a.r - p.b.r) + Math.Abs(p.a.c - p.b.c);
    public static List<PPair>? GroupPairs(List<PDot> dots)
    {
        if (dots.Count % 2 == 1) { Puz.Say("detection error - " + dots.Count + " dots is an odd number, check the grid", false); return null; }
        var pairs = new List<PPair>();
        var ix = Enumerable.Range(0, dots.Count).ToList();
        ix = PairByKey(dots, ix, 0, pairs);
        ix = PairByKey(dots, ix, 3, pairs);
        int far = 0; long farLim = (long)PairTol * PairTol;
        while (ix.Count >= 2)
        {
            if (BoardOver("pairing") || CancelPoll()) break;
            var near = new Dictionary<int, int>(); var nd = new Dictionary<int, long>();
            foreach (int i in ix) { int bj = -1; long bd = long.MaxValue; foreach (int j in ix) { if (j == i) continue; long dd = ColorDist2(dots[i].col, dots[j].col); if (dd < bd) { bd = dd; bj = j; } } near[i] = bj; nd[i] = bd; }
            var took = new HashSet<int>(); bool got = false;
            foreach (int i in ix)
            {
                int j = near[i];
                if (j < 0 || took.Contains(i) || took.Contains(j) || near[j] != i) continue;
                took.Add(i); took.Add(j); got = true;
                if (nd[i] > farLim) far++;
                pairs.Add(new PPair { a = dots[i], b = dots[j] });
            }
            if (!got)
            {
                int bi = -1, bj = -1; long bd = long.MaxValue;
                foreach (int i in ix) if (near[i] >= 0 && nd[i] < bd) { bd = nd[i]; bi = i; bj = near[i]; }
                if (bi < 0) break;
                took.Add(bi); took.Add(bj); if (bd > farLim) far++;
                pairs.Add(new PPair { a = dots[bi], b = dots[bj] });
            }
            ix = ix.Where(i => !took.Contains(i)).ToList();
        }
        if (pairs.Count == 0) { Puz.Say("detection error - no pairs could be matched", false); return null; }
        if (far > 0) Puz.Say(far + " pair(s) matched loosely - colours are close on this board", false);
        for (int i = 1; i < pairs.Count; i++) { int j = i; while (j > 0 && PairDist(pairs[j - 1]) > PairDist(pairs[j])) { (pairs[j - 1], pairs[j]) = (pairs[j], pairs[j - 1]); j--; } }
        return pairs;
    }

    // ---- the search ----
    static int IterMax => 2000000 * N * N / 36;
    static int SolveMax => (int)Math.Round(SolveMs * Math.Clamp(N * N / 100.0, 1.0, 4.0));
    public static Dictionary<int, List<(int r, int c)>>? SolveBoard(List<PPair> pairs)
    {
        _iter = 0; _iterCap = IterMax; _stop = false; _solveEnd = Math.Min(Clock.Tick + SolveMax, _boardEnd); _solveOut = 0;
        var board = new int[N + 2, N + 2];
        for (int i = 0; i < pairs.Count; i++) { board[pairs[i].a.r, pairs[i].a.c] = i + 1; board[pairs[i].b.r, pairs[i].b.c] = i + 1; }
        var paths = new Dictionary<int, List<(int r, int c)>>();
        return RoutePair(1, pairs, board, paths, 0) ? paths : null;
    }
    static bool RoutePair(int k, List<PPair> pairs, int[,] board, Dictionary<int, List<(int, int)>> paths, int depth)
    {
        if (k > pairs.Count) return true;
        var p = pairs[k - 1];
        var path = new List<(int, int)> { (p.a.r, p.a.c) };
        return DFS(k, pairs, board, paths, p.a.r, p.a.c, p.b.r, p.b.c, path, 0, 0, depth);
    }
    static int FreeDeg(int[,] b, int r, int c) { int d = 0; if (r > 1 && b[r - 1, c] == 0) d++; if (r < N && b[r + 1, c] == 0) d++; if (c > 1 && b[r, c - 1] == 0) d++; if (c < N && b[r, c + 1] == 0) d++; return d; }
    static bool EndAlive(int k, List<PPair> pairs, int[,] b, int r, int c)
    {
        int j = b[r, c];
        if (j <= k) return true;
        var p = pairs[j - 1]; var o = p.a.r == r && p.a.c == c ? p.b : p.a;
        if (Math.Abs(r - o.r) + Math.Abs(c - o.c) == 1) return true;
        return FreeDeg(b, r, c) > 0;
    }
    static bool TouchesSelf(int k, int[,] b, int nr, int nc, int pr, int pc, int tr, int tc)
    {
        if (nr > 1 && b[nr - 1, nc] == k && !(nr - 1 == pr && nc == pc) && !(nr - 1 == tr && nc == tc)) return true;
        if (nr < N && b[nr + 1, nc] == k && !(nr + 1 == pr && nc == pc) && !(nr + 1 == tr && nc == tc)) return true;
        if (nc > 1 && b[nr, nc - 1] == k && !(nr == pr && nc - 1 == pc) && !(nr == tr && nc - 1 == tc)) return true;
        if (nc < N && b[nr, nc + 1] == k && !(nr == pr && nc + 1 == pc) && !(nr == tr && nc + 1 == tc)) return true;
        return false;
    }
    static bool StepOk(int k, List<PPair> pairs, int[,] b, int nr, int nc, int tr, int tc)
    {
        if (Math.Abs(nr - tr) + Math.Abs(nc - tc) != 1 && (FreeDeg(b, nr, nc) == 0 || FreeDeg(b, tr, tc) == 0)) return false;
        if (nr > 1 && !EndAlive(k, pairs, b, nr - 1, nc)) return false;
        if (nr < N && !EndAlive(k, pairs, b, nr + 1, nc)) return false;
        if (nc > 1 && !EndAlive(k, pairs, b, nr, nc - 1)) return false;
        if (nc < N && !EndAlive(k, pairs, b, nr, nc + 1)) return false;
        return true;
    }
    static int DKey((int r, int c) d, int r, int c, int tr, int tc, int dr, int dc) => 2 * (Math.Abs(d.r - tr) + Math.Abs(d.c - tc)) + (d.r - r == dr && d.c - c == dc ? 0 : 1);
    static bool DFS(int k, List<PPair> pairs, int[,] board, Dictionary<int, List<(int, int)>> paths, int r, int c, int tr, int tc, List<(int, int)> path, int dr, int dc, int depth)
    {
        _iter++;
        if (SolveStop()) return false;
        if (depth > DepthMax) { _solveOut = 2; return false; }
        var dirs = new[] { (r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1) };
        var ky = dirs.Select(d => DKey(d, r, c, tr, tc, dr, dc)).ToArray();
        for (int i = 1; i < 4; i++) { int j = i; while (j > 0 && ky[j - 1] > ky[j]) { (dirs[j - 1], dirs[j]) = (dirs[j], dirs[j - 1]); (ky[j - 1], ky[j]) = (ky[j], ky[j - 1]); j--; } }
        foreach (var (nr, nc) in dirs)
        {
            if (nr < 1 || nr > N || nc < 1 || nc > N) continue;
            if (nr == tr && nc == tc)
            {
                path.Add((nr, nc)); paths[k] = new List<(int, int)>(path);
                if (RemainingReachable(k + 1, pairs, board) && RoutePair(k + 1, pairs, board, paths, depth + 1)) return true;
                paths.Remove(k); path.RemoveAt(path.Count - 1);
                continue;
            }
            if (board[nr, nc] != 0) continue;
            if (TouchesSelf(k, board, nr, nc, r, c, tr, tc)) continue;
            board[nr, nc] = k; path.Add((nr, nc));
            if (StepOk(k, pairs, board, nr, nc, tr, tc) && DFS(k, pairs, board, paths, nr, nc, tr, tc, path, nr - r, nc - c, depth + 1)) return true;
            path.RemoveAt(path.Count - 1); board[nr, nc] = 0;
        }
        return false;
    }
    static bool SolveStop()
    {
        if (_stop) return true;
        if (_iter > _iterCap) { _solveOut = 1; _stop = true; return true; }
        if (_iter % 256 == 0)
        {
            if (CancelPoll()) { _solveOut = 4; _stop = true; return true; }
            if (Clock.Tick > _solveEnd) { _solveOut = 3; _stop = true; return true; }
        }
        return false;
    }
    static bool RemainingReachable(int fromK, List<PPair> pairs, int[,] board) { for (int k = fromK; k <= pairs.Count; k++) { var p = pairs[k - 1]; if (!Reachable(p.a.r, p.a.c, p.b.r, p.b.c, board)) return false; } return true; }
    static int[] _seen = Array.Empty<int>(); static int _seenN, _seenS;
    static bool Reachable(int sr, int sc, int tr, int tc, int[,] board)
    {
        if (Math.Abs(sr - tr) + Math.Abs(sc - tc) == 1) return true;
        int kb = N + 1;
        if (_seenN != N) { _seen = new int[N * kb + N + 1]; _seenN = N; _seenS = 0; }
        _seenS++;
        _seen[sr * kb + sc] = _seenS;
        var q = new List<int> { sr * kb + sc }; int qi = 0;
        while (qi < q.Count)
        {
            _iter++; if (SolveStop()) return false;
            int cur = q[qi++], r = cur / kb, c = cur - r * kb;
            if (r > 1) { if (r - 1 == tr && c == tc) return true; int key = cur - kb; if (board[r - 1, c] == 0 && _seen[key] != _seenS) { _seen[key] = _seenS; q.Add(key); } }
            if (r < N) { if (r + 1 == tr && c == tc) return true; int key = cur + kb; if (board[r + 1, c] == 0 && _seen[key] != _seenS) { _seen[key] = _seenS; q.Add(key); } }
            if (c > 1) { if (r == tr && c - 1 == tc) return true; int key = cur - 1; if (board[r, c - 1] == 0 && _seen[key] != _seenS) { _seen[key] = _seenS; q.Add(key); } }
            if (c < N) { if (r == tr && c + 1 == tc) return true; int key = cur + 1; if (board[r, c + 1] == 0 && _seen[key] != _seenS) { _seen[key] = _seenS; q.Add(key); } }
        }
        return false;
    }

    // ---- the reroute pass ----
    static bool IsCorner(List<(int r, int c)> p, int i) => p[i + 1].r - p[i].r != p[i].r - p[i - 1].r || p[i + 1].c - p[i].c != p[i].c - p[i - 1].c;
    static int PathCost(List<(int r, int c)> p) { int c = 0; for (int i = 1; i < p.Count - 1; i++) if (IsCorner(p, i)) c++; return 2 * (p.Count - 1) + 3 * c; }
    static List<(int, int)> CandCol(int sr, int sc, int tr, int tc, int x) { var cells = new List<(int, int)> { (sr, sc) }; int c = sc; while (c != x) { c += x > sc ? 1 : -1; cells.Add((sr, c)); } int r = sr; while (r != tr) { r += tr > sr ? 1 : -1; cells.Add((r, x)); } while (c != tc) { c += tc > x ? 1 : -1; cells.Add((tr, c)); } return cells; }
    static List<(int, int)> CandRow(int sr, int sc, int tr, int tc, int y) { var cells = new List<(int, int)> { (sr, sc) }; int r = sr; while (r != y) { r += y > sr ? 1 : -1; cells.Add((r, sc)); } int c = sc; while (c != tc) { c += tc > sc ? 1 : -1; cells.Add((y, c)); } while (r != tr) { r += tr > y ? 1 : -1; cells.Add((r, tc)); } return cells; }
    static bool TryRoute(List<(int r, int c)> cells, int[,] board) { var seen = new HashSet<int>(); int kb = N + 1; foreach (var (r, c) in cells) { if (board[r, c] != 0) return false; if (!seen.Add(r * kb + c)) return false; } return true; }
    static List<(int, int)>? BestReroute(int sr, int sc, int tr, int tc, int[,] board, int budget)
    {
        List<(int, int)>? best = null; int bc = budget;
        for (int x = 1; x <= N; x++) foreach (var cells in new[] { CandCol(sr, sc, tr, tc, x), CandRow(sr, sc, tr, tc, x) }) if (TryRoute(cells, board) && PathCost(cells) < bc) { bc = PathCost(cells); best = cells; }
        return best;
    }
    public static void OptimizePaths(Dictionary<int, List<(int r, int c)>> paths)
    {
        var board = new int[N + 2, N + 2];
        foreach (var (k, p) in paths) foreach (var (r, c) in p) board[r, c] = k;
        for (int round = 0; round < 2; round++)
        {
            bool improved = false;
            foreach (var k in paths.Keys.ToList())
            {
                if (BoardOver("optimise") || CancelPoll()) return;
                var p = paths[k];
                foreach (var (r, c) in p) board[r, c] = 0;
                var np = BestReroute(p[0].r, p[0].c, p[^1].r, p[^1].c, board, PathCost(p));
                if (np is not null) { paths[k] = np; p = np; improved = true; }
                foreach (var (r, c) in p) board[r, c] = k;
            }
            if (!improved) break;
        }
    }

    // ---- input ----
    static void Mouse(uint flags, int dx = 0, int dy = 0) { var inp = new INPUT[] { new() { type = 0, mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = flags, dwExtraInfo = OurTag } } }; SendInput(1, inp, Marshal.SizeOf<INPUT>()); }
    public static Action<int, int>? MoveOverride; public static Action<bool>? ButtonOverride;
    static void MoveTo(double x, double y) { if (MoveOverride is not null) { MoveOverride((int)x, (int)y); return; } if (vw < 1) ScreenSync(); Mouse(0xC001, (int)Math.Ceiling((x - vx) * 65536.0 / vw), (int)Math.Ceiling((y - vy) * 65536.0 / vh)); }
    static void Button(bool down) { if (ButtonOverride is not null) { ButtonOverride(down); return; } Mouse(down ? 0x0002u : 0x0004u); }
    static (int x, int y) CursorPos() { if (MoveOverride is not null) return _fakeCur; GetCursorPos(out var p); return (p.x, p.y); }
    static (int x, int y) _fakeCur;
    static double Ms => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
    static void WaitUntil(double target) { while (true) { double rem = target - Ms; if (rem <= 0.2) return; if (rem > 2) Thread.Sleep(1); else Thread.Yield(); } }
    static void GlideTo(double x0, double y0, double x1, double y1, double speed, bool ease = false)
    {
        double dist = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        if (dist < 1) { MoveTo(x1, y1); return; }
        double dur = dist / speed, t0 = Ms, step = FrameInterval; int lx = -99999, ly = -99999;
        while (true)
        {
            double el = Ms - t0; if (el >= dur) break;
            double t = el / dur; if (ease) t = t * t * (3 - 2 * t);
            int nx = (int)Math.Round(x0 + (x1 - x0) * t), ny = (int)Math.Round(y0 + (y1 - y0) * t);
            if (nx != lx || ny != ly) { MoveTo(nx, ny); lx = nx; ly = ny; }
            WaitUntil(t0 + step); step += FrameInterval;
        }
        MoveTo(x1, y1);
    }
    static double PitchScale => Math.Max(1.0, SX / 86.2) * _retryMul * _paceMul;
    static double CellBudget(List<(int r, int c)> path, int i, int n) => (i < n - 1 && IsCorner(path, i) ? CornerFrames : CellFrames) * Puz.FrameMs * PitchScale;
    static void CellGlide(double x0, double y0, double x1, double y1, double bud)
    {
        double t0 = Ms, trav = bud * (1.0 - Math.Clamp(CellDwell, 0.0, 0.9)), dist = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
        GlideTo(x0, y0, x1, y1, trav > 0.5 ? dist / trav : 9999);
        WaitUntil(t0 + bud);
    }
    static void DwellIn(int x, int y, int ms) { double t0 = Ms; MoveTo(x + JigglePx, y); Thread.Sleep(JiggleDwell); MoveTo(x, y); int rem = ms - (int)Math.Round(Ms - t0); if (rem > 0) Thread.Sleep(rem); }
    static void ApproachDot(int x, int y) { var (cx, cy) = CursorPos(); if (Math.Abs(cx - x) + Math.Abs(cy - y) > 2) GlideTo(cx, cy, x, y, ApproachSpeed, true); else MoveTo(x, y); DwellIn(x, y, (int)Math.Round(Puz.FrameMs * StartFrames)); }
    static void ParkCursor() { int tx = (int)Math.Round(PX(1) - SX), ty = PY(1); var (cx, cy) = CursorPos(); if (Math.Abs(cx - tx) + Math.Abs(cy - ty) > 2) GlideTo(cx, cy, tx, ty, ApproachSpeed, true); else MoveTo(tx, ty); }
    /// <summary>PuzDrawPath: hover on the first dot, press, glide cell by cell, hold on the last dot, release.</summary>
    static void DrawPath(List<(int r, int c)> path)
    {
        bool down = false;
        try
        {
            int n = path.Count, sx = PX(path[0].c), sy = PY(path[0].r);
            ApproachDot(sx, sy);
            Button(true); down = true;
            Thread.Sleep((int)Math.Round(Puz.FrameMs * (PressFrames + LagFrames)));
            double cx = sx, cy = sy;
            for (int i = 1; i < n; i++)
            {
                int tx = PX(path[i].c), ty = PY(path[i].r);
                CellGlide(cx, cy, tx, ty, CellBudget(path, i, n));
                cx = tx; cy = ty;
                if (CancelPoll()) break;
            }
            if (!_cancel && TailBackCells > 0) { int k = Math.Min(TailBackCells, n - 2); for (int q = 1; q <= k; q++) TailStep(path, n - 1 - q, n); for (int q = 1; q <= k; q++) TailStep(path, n - 1 - k + q, n); }
            WaitUntil(Ms + (EndFrames + SlackFrames) * Puz.FrameMs);
            Button(false); down = false;
        }
        finally { if (down) Button(false); }
    }
    static void TailStep(List<(int r, int c)> path, int i, int n) { var (cx, cy) = CursorPos(); CellGlide(cx, cy, PX(path[i].c), PY(path[i].r), CellBudget(path, i, n)); }
    static int AuditPath(List<PPair> pairs, int k, List<(int r, int c)> path)
    {
        int n = path.Count; if (n < 3) return 0;
        BoardRect(out int bx0, out int by0, out int bw0, out int bh0);
        int lox = PX(path[1].c), hix = lox, loy = PY(path[1].r), hiy = loy;
        for (int i = 1; i < n - 1; i++) { int px = PX(path[i].c), py = PY(path[i].r); lox = Math.Min(lox, px); hix = Math.Max(hix, px); loy = Math.Min(loy, py); hiy = Math.Max(hiy, py); }
        const int apad = 4;
        int x0 = Math.Max(bx0, lox - apad), y0 = Math.Max(by0, loy - apad), x1 = Math.Min(bx0 + bw0, hix + apad + 1), y1 = Math.Min(by0 + bh0, hiy + apad + 1), w0 = x1 - x0, h0 = y1 - y0;
        if (w0 < 1 || h0 < 1) return 0;
        var cap = Capture(x0, y0, w0, h0); uint col = pairs[k - 1].a.col; int miss = 0;
        for (int i = 1; i < n - 1; i++) { uint got = cap.At(PX(path[i].c) - x0, PY(path[i].r) - y0); if (ColorDist(got, col) >= PairTol) miss++; }
        return miss;
    }
    static void VerifyLine(List<PPair> pairs, int k, List<(int r, int c)> path)
    {
        if (path.Count < 3) return;
        if (AuditPath(pairs, k, path) == 0) { if (++_paceOk >= PACE_RUN) { _paceOk = 0; _paceMul = Math.Max(1.0, _paceMul * PACE_DOWN); } return; }
        _paceOk = 0; _paceMul = Math.Min(_paceMul * PACE_UP, PACE_MAX); _retryMul = SlowStep;
        try { DrawPath(path); } finally { _retryMul = 1.0; }
    }

    // ---- the session ----
    static bool BoardOver(string stage) { if (Clock.Tick <= _boardEnd) return false; if (_boardOut == "") _boardOut = stage; return true; }
    static string StageMs() { var parts = new List<string>(); foreach (var (nm, v) in new[] { ("scan", tScan), ("pair", tPair), ("solve", tSolve), ("opt", tOpt), ("draw", tDraw) }) if (v >= 5) parts.Add(nm + " " + v); return parts.Count == 0 ? "under 5ms" : string.Join(" ", parts) + "ms"; }
    public static Func<bool>? HeldOverride; public static Func<bool>? FocusOverride;
    static bool KeyHeld() { int vk = Keys.VkOf(Keys.PuzKey); return vk != 0 && WinHooks.IsDown(vk); }
    static bool Focused() => FocusOverride?.Invoke() ?? WinHooks.RobloxInFront();
    static bool Held()
    {
        if (HeldOverride is not null) return HeldOverride();
        if (Keys.PuzKey == "" || !Puz.On || !KeyHeld()) return false;
        if (Focused()) return true;
        Thread.Sleep(90);
        return Focused();
    }
    static bool CancelPoll()
    {
        if (_cancel) return true;
        if (HeldOverride is not null) return false;
        if (Keys.PuzKey == "") return false;
        if (!KeyHeld()) { _cancelUp = true; return false; }
        if (_cancelUp) _cancel = true;
        return _cancel;
    }
    static bool LooksLikeDeal(List<PDot> dots)
    {
        if (dots.Count < 4) return false;
        var seen = new Dictionary<uint, int>(); int over = 0;
        foreach (var d in dots) { seen[d.col] = seen.TryGetValue(d.col, out var v) ? v + 1 : 1; if (seen[d.col] == 3) over++; if (over >= 2 || seen[d.col] > 4) return false; }
        return true;
    }
    static string BoardSig(List<PDot> dots) => string.Concat(dots.Select(d => d.r + "," + d.c + "," + d.col + ";"));
    static List<PDot>? WaitNextBoard(string prev)
    {
        long t0 = Clock.Tick; string seen = ""; int nSame = 0; bool first = true;
        while (Clock.Tick - t0 < NextTimeout)
        {
            if (!Held() || CancelPoll()) return null;
            if (!first) Thread.Sleep(NextPoll);
            first = false;
            var dots = ScanGrid();
            if (dots.Count < 4 || dots.Count % 2 != 0 || !LooksLikeDeal(dots)) continue;
            string sig = BoardSig(dots);
            if (sig == prev) continue;
            if (sig == seen) { if (++nSame >= 1) return dots; } else { seen = sig; nSame = 0; }
        }
        return null;
    }
    static int DrawBudget(int np) { double perLine = Puz.FrameMs * (StartFrames + PressFrames + LagFrames + EndFrames + SlackFrames + CellFrames * N); return Math.Max(3000, (int)Math.Round(3 * np * perLine * _paceMul)); }
    /// <summary>PuzSolveOne: scan, pair, solve, optimise, draw - one board. Returns its signature, or "" when it did not finish.</summary>
    public static string SolveOne(List<PDot>? dots)
    {
        _solveOut = 0; _boardEnd = Clock.Tick + BoardMs + Math.Max(0, SolveMax - SolveMs); _boardOut = "";
        tScan = tPair = tSolve = tOpt = tDraw = 0;
        long t0 = Clock.Tick;
        if (dots is null)
        {
            ParkCursor(); Thread.Sleep(Math.Min(ScanSettle, Math.Max(10, (int)Math.Round(2 * Puz.FrameMs))));
            // Per board, from the session counter - see Puz.BoardApply. Nothing
            // is scanned; board 1 is up, the rest are down, a button overrides.
            Stage("scanning");
            Puz.BoardApply();
            dots = ScanGrid();
        }
        if (dots.Count == 0) { Puz.Say("no dots detected", false); return ""; }
        if (!LooksLikeDeal(dots)) { Puz.Say("that board is already drawn on - waiting for a new deal", false); return ""; }
        tScan = (int)(Clock.Tick - t0);
        string sig = BoardSig(dots);
        t0 = Clock.Tick; Stage("pairing");
        var pairs = GroupPairs(dots);
        tPair = (int)(Clock.Tick - t0);
        if (pairs is null) return "";
        if (BoardOver("pairing")) { Puz.Say("gave up in " + _boardOut + " - " + StageMs(), false); return ""; }
        t0 = Clock.Tick; Stage("solving");
        var paths = SolveBoard(pairs);
        tSolve = (int)(Clock.Tick - t0);
        if (paths is null)
        {
            string lbl = N + "x" + N;
            Puz.Say(_solveOut == 1 ? "gave up after " + _iterCap + " steps - " + lbl + " board, " + pairs.Count + " pairs" : _solveOut == 2 ? "path got too long for the solver - " + lbl + " is at the limit" : _solveOut == 3 ? "out of time after " + Math.Round(SolveMs / 1000.0, 1) + "s - " + pairs.Count + " pairs on a " + lbl + " board" : _solveOut == 4 ? "cancelled" : "no solution - check the grid", false);
            return "";
        }
        t0 = Clock.Tick; Stage("optimising");
        OptimizePaths(paths);
        tOpt = (int)(Clock.Tick - t0);
        _lines = pairs.Count;
        t0 = Clock.Tick; _boardEnd = Clock.Tick + DrawBudget(pairs.Count);
        if (BoardOver("optimise")) { Puz.Say("gave up in " + _boardOut + " - " + StageMs(), false); return ""; }
        for (int k = 1; k <= pairs.Count; k++)
        {
            if (!Focused()) { Puz.Say("stopped - focus left the game mid-board", false); return ""; }
            if (CancelPoll()) { tDraw = (int)(Clock.Tick - t0); Puz.Say("cancelled after " + (k - 1) + " of " + pairs.Count + " lines", false); return ""; }
            if (BoardOver("drawing")) { tDraw = (int)(Clock.Tick - t0); Puz.Say("gave up in " + _boardOut + " after " + (k - 1) + " of " + pairs.Count + " lines - " + StageMs(), false); return ""; }
            Stage("drawing " + k + "/" + pairs.Count);
            DrawPath(paths[k]);
            if (k < pairs.Count) VerifyLine(pairs, k, paths[k]);
        }
        tDraw = (int)(Clock.Tick - t0);
        return sig;
    }
    static void Stage(string s) { _stage = s; Avalonia.Threading.Dispatcher.UIThread.Post(() => { Puz.Stage = s; HubSurface.Live?.Tim(Pace.TICK_A); }); }
    /// <summary>PuzSolve: the session - one board, then as long as the key is held, the next deal after each.</summary>
    public static void Solve()
    {
        if (Solving || !Puz.On) return;
        ScreenSync();
        if (!GridOnScreen()) { Puz.Say("the grid is not on this screen - it has not been changed", false); return; }
        _cancel = false; _cancelUp = false; Solving = true; Puz.Solving = true; _runs = 0; _paceMul = 1.0; _paceOk = 0;
        FskOverlays.MarkersApply();
        _worker = new Thread(SessionBody) { IsBackground = true, Name = "puzzle-ai" };
        _worker.Start();
    }
    static void SessionBody()
    {
        try
        {
            Thread.Sleep(SettleMs);
            string prev = ""; List<PDot>? ready = null; int fails = 0, iters = 0, repair = 0; string lastSig = "";
            while (true)
            {
                iters++;
                string sig = "";
                try { sig = SolveOne(ready); } catch (Exception e) { Puz.Say("error: " + e.Message, false); }
                ready = null;
                if (_cancel) { Puz.Say("cancelled" + (_runs != 0 ? " after " + _runs + " board" + (_runs == 1 ? "" : "s") : ""), false); break; }
                if (sig == "")
                {
                    if (_solveOut != 0) break;
                    if (!Held()) break;
                    if (++fails >= MaxFails) { Puz.Say("stopped - board unreadable", false); break; }
                    Thread.Sleep(RetryWait); continue;
                }
                fails = 0; prev = sig;
                if (sig != lastSig) { lastSig = sig; repair = 0; _runs++; _total++; _sess++; Avalonia.Threading.Dispatcher.UIThread.Post(() => Puz.Sess = _sess); }
                Puz.Say((_runs > 1 ? _runs + " boards solved" : _lines + " lines drawn") + "  \u00B7  " + StageMs(), true);
                if (!Held()) break;
                if (iters >= MaxRuns) { Puz.Say("stopped after " + _runs + " boards", true); break; }
                ParkCursor(); Stage("waiting for the next deal");
                ready = WaitNextBoard(prev);
                if (ready is null) { if (!Held()) break; if (++repair > RepairMax) { Puz.Say("stopped - no new board", true); break; } prev = ""; continue; }
            }
        }
        finally
        {
            Solving = false; Ini.Write(Paths.IniFile, "puzzle", "solved", (long)_total);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { Puz.Solving = false; Puz.Stage = ""; FskOverlays.MarkersApply(); HubSurface.Live?.Tim(Pace.TICK_A); });
        }
    }
    /// <summary>PuzSolveHK: the solve key - start a session, or cancel the one running.</summary>
    public static void SolveHK() { if (Solving) { _cancel = true; return; } Solve(); }
    public static void Load() { _total = (int)Ini.ReadInt(Paths.IniFile, "puzzle", "solved", 0); ScreenSync(); }
}
