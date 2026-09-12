using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.Forsaken;

/// <summary>
/// PUZZLE AI's state and settings (puzOn, PZ_SpeedT, PZ_gridNi, the custom
/// grid, the session, the status line, the clash card). The solver itself -
/// the capture, the pairing, the search and the glide - is the next piece;
/// until it lands SET GRID and the solve key say so.
/// </summary>
public static class Puz
{
    public static bool On; public static long FlashAt; public static int Cal; public static bool Solving; public static int Sess; public static long SessAt;
    public static string Msg = "idle"; public static bool MsgOk; public static long MsgAt; public static string Stage = "";
    public static readonly int[] SIZES = { 6, 7, 10, 25 };
    public static int GridNi = 2; public static double SpeedT = 1.0;
    public static double CustBX, CustBY, CustBW, CustBH; public static bool Custom;                 // PZ_Grid: the board's outer box
    /// <summary>PZ_gridGen: bumped on every change, so the cached tables and the marker surface know to rebuild.</summary>
    public static int GridGen;
    // ---- THE BOARD MOVES ON THE FIRST PUZZLE. NOTHING IS DETECTED. ----
    // Measured off two full-screen captures: with the three-line banner above it
    // the board's frame sits one banner lower and every dot row with it; columns
    // are identical either way. The banner is up for the FIRST puzzle after the
    // module is armed and gone for every puzzle after, so it is a rule and not a
    // question - board 1 of a session is UP, everything after it is DOWN.
    //
    // It is a TRANSLATION, independent of the calibration: it works on the
    // built-in default and on a grid the user set, because the shift is a
    // property of the game's layout and not of where the board happens to be.
    public static bool BannerFix = true;      // false pins the offset at 0 and ignores the banner
    public static int BoardDy;                // the live offset: 0 or PuzSolver.BannerDy()
    public static bool DyPin;                 // true once a button has been pressed this session
    /// <summary>The rule, unless the user has taken the wheel with DOWN BOARD / UP BOARD.</summary>
    public static int BoardApply()
    {
        if (DyPin) return BoardDy;
        // Pure arithmetic off the reference table - no platform call - so the
        // rule holds (and can be tested) wherever the module is compiled.
        BoardDy = Sess == 0 && BannerFix ? PuzSolver.BannerDy() : 0;
        return BoardDy;
    }
    public static void BoardSet(int dy, bool pin = true)
    {
        if (BoardDy == dy && DyPin == pin) return;
        BoardDy = dy; DyPin = pin; GridGen++;
        // Apply already rebuilds when the geometry changes; killing first only
        // guaranteed the slow path.
        if (OperatingSystem.IsWindows()) FskOverlays.MarkersApply();
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    static int _calX1, _calY1;
    public static string Solved => Ini.Read(Paths.IniFile, "puzzle", "solved", "0");
    public const double FrameFast = 5, FrameSlow = 42; public static double FrameMs = FrameFast;
    public static string ClashKey = "", ClashWho = "", ClashOwn = ""; public static long ClashAt, ClashOut;
    public static bool ClashUp => ClashKey != "";
    public static int GridN => SIZES[Math.Clamp(GridNi, 1, SIZES.Length) - 1];
    public static double SX => OperatingSystem.IsWindows() ? PuzSolver.SX : (Custom ? CustBW / GridN : 660.0 / GridN);
    public static void Load()
    {
        if (OperatingSystem.IsWindows()) { PuzSolver.Load(); FskOverlays.Load(); }
        GridNi = Math.Clamp((int)Ini.ReadInt(Paths.IniFile, "puzzle", "gridn", 2), 1, SIZES.Length);
        SpeedT = Math.Clamp(Ini.ReadNum(Paths.IniFile, "puzzle", "speed", 1.0), 0.0, 1.0);
        SpeedApply();
        GridLoad();
    }
    /// <summary>PuzGridLoad / PuzGridSave: the custom box under [puzzle] grid, per grid size.</summary>
    static void GridLoad()
    {
        string v = Ini.Read(Paths.IniFile, "puzzle", "grid" + GridN, "");
        var p = v.Split(',');
        if (p.Length == 4 && double.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bx) && double.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var by) && double.TryParse(p[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bw) && double.TryParse(p[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bh) && bw > 8 && bh > 8)
        { CustBX = bx; CustBY = by; CustBW = bw; CustBH = bh; Custom = true; }
        else Custom = false;
    }
    public static void GridSet(double bx, double by, double bw, double bh)
    {
        CustBX = bx; CustBY = by; CustBW = bw; CustBH = bh; Custom = true; GridGen++;
        Ini.Write(Paths.IniFile, "puzzle", "grid" + GridN, string.Join(",", new[] { bx, by, bw, bh }.Select(d => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))));
    }
    public static void Say(string msg, bool ok) { Msg = msg; MsgOk = ok; MsgAt = Clock.Tick; HubSurface.Live?.Tim(Pace.TICK_A); }
    public static void Enable()
    {
        if (On) return;
        if (!Os.IsWin) { Say("PUZZLE AI is a Windows feature", false); return; }
        string ck = ClashOn(2);
        if (ck != "") { ClashRaise("PUZZLE AI", "EXTERNAL BLOCK REBIND", ck); return; }
        On = true; FlashAt = Clock.Tick; Sess = 0; SessAt = Clock.Tick; MsgAt = 0;
        BoardDy = 0; DyPin = false;                      // a session's offset does not survive arming
        SpeedApply();
        // Build the grid's window NOW, while the module is being armed, rather
        // than on the first press of the markers key: the build is what the
        // pause before the grid appears actually is, and arming is a moment
        // where nobody is waiting on it.
        if (OperatingSystem.IsWindows()) { WinHooks.Ensure(); FskOverlays.PuzSync(); FskOverlays.MarkersApply(); }
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Disable()
    {
        if (!On) return;
        if (Keys.RebindOn && (Keys.RebindTgt == 2 || Keys.RebindTgt == 3)) Keys.Cancel();
        On = false; FlashAt = Clock.Tick; Sess = 0; SessAt = 0; MsgAt = 0; Cal = 0; Solving = false;
        BoardDy = 0; DyPin = false;
        if (OperatingSystem.IsWindows()) FskOverlays.PuzSync();
        HubSurface.Live?.Tim(Pace.TICK_A);
    }
    public static void Toggle() { if (On) Disable(); else Enable(); }
    public static void SpeedApply() { FrameMs = FrameSlow + (FrameFast - FrameSlow) * Math.Clamp(SpeedT, 0.0, 1.0); }
    public static void SlideSet(double t) { SpeedT = Math.Clamp(t, 0.0, 1.0); SpeedApply(); Ini.Write(Paths.IniFile, "puzzle", "speed", SpeedT); }
    public static int SpeedPct => (int)Math.Round(100 * Math.Clamp(SpeedT, 0.0, 1.0));
    public static void GridPick(int i) { if (i < 1 || i > SIZES.Length) return; GridNi = i; GridGen++; Ini.Write(Paths.IniFile, "puzzle", "gridn", (long)i); GridLoad(); if (OperatingSystem.IsWindows()) FskOverlays.MarkersApply(); HubSurface.Live?.Tim(Pace.TICK_A); }
    public static void GridReset() { Custom = false; CustBX = CustBY = CustBW = CustBH = 0; GridGen++; Ini.Write(Paths.IniFile, "puzzle", "grid" + GridN, ""); Say("grid back to the default - " + GridN + "x" + GridN + " at the screen's middle", true); }
    /// <summary>SET GRID: the next two clicks anywhere on the screen are the TOP-LEFT cell's centre and the BOTTOM-RIGHT cell's centre; the box is derived from them.</summary>
    public static void SetGrid()
    {
        if (Cal != 0) { Cal = 0; Say("grid setup cancelled", false); if (OperatingSystem.IsWindows()) FskOverlays.CalApply(); return; }
        if (!Os.IsWin) { Say("PUZZLE AI is a Windows feature", false); return; }
        if (OperatingSystem.IsWindows()) WinHooks.Ensure();
        Cal = 1; Say("click the board's TOP-LEFT corner", true);
        if (OperatingSystem.IsWindows()) FskOverlays.CalApply();
    }
    /// <summary>PuzCalClick: the two clicks are the board's OUTER corners - the whole grid's box, not a cell centre. A click on the card itself moves the card instead.</summary>
    public static bool OnMouse(int msg, int x, int y, bool injected)
    {
        if (Cal == 0 || injected || msg != 0x201) return false;
        if (OperatingSystem.IsWindows() && FskOverlays.CalNudgeIfOver(x, y)) return true;
        if (Cal == 1)
        {
            _calX1 = x; _calY1 = y; Cal = 2;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (OperatingSystem.IsWindows()) FskOverlays.CalApply(); HubSurface.Live?.Tim(Pace.TICK_A); });
            return true;
        }
        int n = GridN;
        double ax = Math.Min(_calX1, x), ay = Math.Min(_calY1, y), bx = Math.Max(_calX1, x), by = Math.Max(_calY1, y);
        if (bx - ax < n * 5 || by - ay < n * 5)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (OperatingSystem.IsWindows()) FskOverlays.CalApply("too small - click OPPOSITE corners of the " + n + "x" + n + " box"); });
            return true;
        }
        double sx = (bx - ax) / n;
        Cal = 0;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            GridSet(ax, ay, bx - ax, by - ay);
            Say("grid set - " + Math.Round(sx) + " px cells" + (OperatingSystem.IsWindows() && FskOverlays.MkWant ? "" : " (markers off - press " + (Keys.MkKey == "" ? "the markers key" : Keys.Label(Keys.MkKey)) + " to check)"), true);
            if (OperatingSystem.IsWindows()) FskOverlays.CalApply();
        });
        return true;
    }
    public static string ClashOn(int which)
    {
        if (which == 2) { if (Ab.On && Keys.BindKey != "" && (Keys.BindKey == Keys.PuzKey || Keys.BindKey == Keys.MkKey)) return Keys.Label(Keys.BindKey); }
        else if (On && Keys.BindKey != "" && (Keys.BindKey == Keys.PuzKey || Keys.BindKey == Keys.MkKey)) return Keys.Label(Keys.BindKey);
        return "";
    }
    public static void ClashRaise(string who, string own, string keyLbl) { ClashKey = keyLbl; ClashWho = who; ClashOwn = own; ClashAt = Clock.Tick; ClashOut = 0; HubSurface.Live?.Tim(Pace.TICK_A); }
    public static void ClashDismiss() { if (ClashKey == "" || ClashOut != 0) return; ClashOut = Clock.Tick; HubSurface.Live?.Tim(Pace.TICK_A); }
    public static void ClashExpire() { if (ClashOut == 0 || Clock.Tick - ClashOut < 300) return; ClashKey = ""; ClashWho = ""; ClashOwn = ""; ClashAt = 0; ClashOut = 0; }
    /// <summary>The solve key starts a session or cancels the one running; the markers key reports the grid; ESC cancels SET GRID and the conflict card.</summary>
    public static bool OnKey(int vk, bool down)
    {
        if (!On || !down) return false;
        int pk = Keys.VkOf(Keys.PuzKey), mk = Keys.VkOf(Keys.MkKey);
        if (pk != 0 && vk == pk) { if (OperatingSystem.IsWindows() && (PuzSolver.Solving || WinHooks.RobloxInFront())) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (OperatingSystem.IsWindows()) PuzSolver.SolveHK(); }); return false; }
        if (mk != 0 && vk == mk) { Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (OperatingSystem.IsWindows()) FskOverlays.MarkersToggle(); }); return true; }
        if (vk == 27 && Cal != 0) { Cal = 0; Avalonia.Threading.Dispatcher.UIThread.Post(() => { Say("grid setup cancelled", false); if (OperatingSystem.IsWindows()) FskOverlays.CalApply(); }); return true; }
        if (vk == 27 && ClashUp && ClashOut == 0) { Avalonia.Threading.Dispatcher.UIThread.Post(ClashDismiss); return false; }
        return false;
    }
    /// <summary>OnKey's other half: the two keys when they are bound to a side or middle mouse button.</summary>
    public static bool OnBtn(string name, bool down)
    {
        if (!On || !down || name == "") return false;
        if (PuzKeyIs(name)) { if (OperatingSystem.IsWindows() && (PuzSolver.Solving || WinHooks.RobloxInFront())) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (OperatingSystem.IsWindows()) PuzSolver.SolveHK(); }); return false; }
        if (Keys.MkKey == name) { Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (OperatingSystem.IsWindows()) FskOverlays.MarkersToggle(); }); return true; }
        return false;
    }
    static bool PuzKeyIs(string name) => Keys.PuzKey == name;
}
