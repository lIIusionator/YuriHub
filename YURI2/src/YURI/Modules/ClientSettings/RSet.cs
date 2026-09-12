using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using Yuri.Core;
using Yuri.Platform;
using Yuri.Shell.Hub;

namespace Yuri.Modules.ClientSettings;

/// <summary>One row of the module: a key, a name, a hint, its group, the control kind, the labels, the flag maps per label and/or the xml property with its values.</summary>
public sealed class RSetOpt
{
    public string K = "", N = "", H = ""; public int Grp; public string Ui = "drop";
    public List<string> V = new();
    public List<Dictionary<string, string>>? F;
    public string? Xml; public string Xt = "int"; public bool Pref;
    public List<string>? X;
    public int Al, Alp;                                                  // 1 all on the allowlist, alp: partly
}

/// <summary>
/// CLIENT SETTINGS - module 4's data. A front end over the two places
/// Roblox keeps client configuration: fast flags (written into every
/// install's ClientAppSettings.json with a journal of what each key held
/// before, so REVERT takes back exactly what APPLY added) and
/// GlobalBasicSettings_13.xml (edited in place after one backup; only
/// properties that already exist are touched). The 32 rows, the three
/// presets, the raw flag layer (RSET.extra), the FLAG SOURCE switch, the
/// row locks and overrides, APPLY / REVERT, boot.
/// </summary>
public static class RSet
{
    public const uint AMBER = 0xFFFBBF24, C_ON = 0xFF34D399, C_OFF = 0xFFFB7185, C_ACC = 0xFFFB7185;
    public const int SEPH = 26, CH = 192;
    // ---- the allowlist (Sept 2025) ----
    static readonly HashSet<string> Allow = new() { "DFIntCSGLevelOfDetailSwitchingDistance", "DFIntCSGLevelOfDetailSwitchingDistanceL12", "DFIntCSGLevelOfDetailSwitchingDistanceL23", "DFIntCSGLevelOfDetailSwitchingDistanceL34",
        "FFlagHandleAltEnterFullscreenManually", "DFFlagTextureQualityOverrideEnabled", "DFIntTextureQualityOverride", "FIntDebugForceMSAASamples", "DFFlagDisableDPIScale", "FFlagDebugGraphicsPreferD3D11", "FFlagDebugSkyGray",
        "DFFlagDebugPauseVoxelizer", "DFIntDebugFRMQualityLevelOverride", "FIntFRMMaxGrassDistance", "FIntFRMMinGrassDistance", "FFlagDebugGraphicsPreferVulkan", "FFlagDebugGraphicsPreferOpenGL", "FIntGrassMovementReducedMotionFactor" };
    public static readonly List<RSetOpt> Opts = Build();
    public static int SysN => Opts.Count;
    public static readonly Dictionary<string, int> Pick = new();
    public static readonly Dictionary<string, long> FlashAt = new();
    public static readonly Dictionary<string, string> Extra = new();
    public static readonly Dictionary<string, long> FxFlash = new();
    public static bool On; public static double OnT; public static long ApplyAt;
    public static string Msg = "NOT APPLIED"; public static uint MsgCol = 0xFFC7CBE0; public static long MsgAt;
    public static double Scr, ScrT;
    public static int Preset; public static long PresetAt;
    public static bool SrcOn; public static int SrcSel = 1; public static double SrcT, SrcSelT = 1; public static long SrcAt;
    public static int Dd, DdPrev, DdTop; public static double DdT; public static long DdAt;
    public static int Sld;
    public static string XmlPath = ""; static long _xmlAt; public static readonly Dictionary<string, string> XmlHave = new();
    static List<string> _paths = new(); static long _pathsAt;
    public static int FxVer, WrCut;
    public static string Font = "", Logo = "", LogoS = "";
    public static Func<int>? IcoOrigCount;
    static DispatcherTimer? _pend;
    public static string Dir => Path.Combine(Paths.Root, "settings");
    public static string XmlBak => Path.Combine(Dir, "GlobalBasicSettings.bak.xml");
    static string XmlOwnPath => Path.Combine(Dir, "xml_owned.json");
    static string PrevPath => Path.Combine(Dir, "appsettings_prev.json");
    public static Func<List<string>>? PathsOverride, XmlOverride;
    public static bool NoProc;

    static List<RSetOpt> Build()
    {
        var o = new List<RSetOpt>();
        Dictionary<string, string> M(params string[] kv) { var d = new Dictionary<string, string>(); for (int i = 0; i + 1 < kv.Length; i += 2) d[kv[i]] = kv[i + 1]; return d; }
        o.Add(new() { K = "apply", N = "APPLY SETTINGS", H = "close roblox first - the client file is only writable then", Grp = 0, Ui = "action", V = { "APPLY" }, F = new() { M() } });
        o.Add(new() { K = "revert", N = "REVERT SETTINGS", H = "put every setting back to the roblox default", Grp = 0, Ui = "action", V = { "REVERT" }, F = new() { M() } });
        o.Add(new() { K = "preset", N = "QUALITY PRESET", H = "set every graphics row at once", Grp = 0, Ui = "preset", V = { "HIGH", "MEDIUM", "LOW" }, F = new() { M() } });
        o.Add(new() { K = "render", N = "RENDERING MODE", H = "which graphics api the client asks for", Grp = 1, V = { "AUTO", "D3D11", "VULKAN", "OPENGL" },
            F = new() { M(), M("FFlagDebugGraphicsPreferD3D11", "True"), M("FFlagDebugGraphicsPreferVulkan", "True", "FFlagDebugGraphicsDisableDirect3D11", "True"), M("FFlagDebugGraphicsPreferOpenGL", "True", "FFlagDebugGraphicsDisableDirect3D11", "True") } });
        o.Add(new() { K = "light", N = "LIGHTING TECHNOLOGY", H = "voxel is cheapest, future is prettiest", Grp = 1, V = { "AUTO", "VOXEL", "SHADOWMAP", "FUTURE" },
            F = new() { M(), M("DFFlagDebugRenderForceTechnologyVoxel", "True"), M("FFlagDebugForceFutureIsBrightPhase2", "True"), M("FFlagDebugForceFutureIsBrightPhase3", "True") } });
        var fr = new RSetOpt { K = "frm", N = "FRM QUALITY", H = "the engine's own quality ladder, 1 to 21", Grp = 1, Ui = "slider", V = { "AUTO" }, F = new() { M() } };
        for (int i = 1; i <= 21; i++) { fr.V.Add(i.ToString()); fr.F.Add(M("DFIntDebugFRMQualityLevelOverride", i.ToString())); }
        o.Add(fr);
        o.Add(new() { K = "tex", N = "TEXTURE QUALITY", H = "0 is flat colour, 3 keeps full detail", Grp = 1, V = { "AUTO", "0", "1", "2", "3" },
            F = new() { M(), M("DFFlagTextureQualityOverrideEnabled", "True", "DFIntTextureQualityOverride", "0"), M("DFFlagTextureQualityOverrideEnabled", "True", "DFIntTextureQualityOverride", "1"), M("DFFlagTextureQualityOverrideEnabled", "True", "DFIntTextureQualityOverride", "2"), M("DFFlagTextureQualityOverrideEnabled", "True", "DFIntTextureQualityOverride", "3") } });
        var mq = new RSetOpt { K = "mesh", N = "MESH QUALITY", H = "distance meshes keep full detail before dropping LOD", Grp = 1, V = { "AUTO", "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" }, F = new() { M() } };
        foreach (int d0 in new[] { 0, 500, 1000, 2000, 4000 })
            mq.F.Add(d0 == 0 ? M("DFIntCSGLevelOfDetailSwitchingDistance", "0", "DFIntCSGLevelOfDetailSwitchingDistanceL12", "0", "DFIntCSGLevelOfDetailSwitchingDistanceL23", "0", "DFIntCSGLevelOfDetailSwitchingDistanceL34", "0")
                : M("DFIntCSGLevelOfDetailSwitchingDistance", d0.ToString(), "DFIntCSGLevelOfDetailSwitchingDistanceL12", (d0 + (int)Math.Round(d0 * 0.5)).ToString(), "DFIntCSGLevelOfDetailSwitchingDistanceL23", (d0 * 2).ToString(), "DFIntCSGLevelOfDetailSwitchingDistanceL34", (d0 * 3).ToString()));
        o.Add(mq);
        o.Add(new() { K = "msaa", N = "ANTI-ALIASING", H = "msaa samples - smoother edges, more gpu", Grp = 1, V = { "AUTO", "x1", "x2", "x4" }, F = new() { M(), M("FIntDebugForceMSAASamples", "1"), M("FIntDebugForceMSAASamples", "2"), M("FIntDebugForceMSAASamples", "4") } });
        var fp2 = new RSetOpt { K = "fps", N = "FRAMERATE CAP", H = "the frame rate the client aims for", Grp = 1, Ui = "slider", Xml = "FramerateCap", Xt = "int", V = { "AUTO" }, X = new() { "" }, F = new() { M() } };
        for (int i = 1; i <= 36; i++) { string t2 = (i * 10).ToString(); fp2.V.Add(t2); fp2.X.Add(t2); }
        fp2.V.Add("MAX"); fp2.X.Add("9999");
        for (int i = 1; i < fp2.X.Count; i++) fp2.F.Add(M("DFIntTaskSchedulerTargetFps", fp2.X[i], "FFlagGameBasicSettingsFramerateCap5", "True", "FFlagTaskSchedulerLimitTargetFpsTo2402", "False", "FFlagDebugForce60FpsThrottle", "False"));
        o.Add(fp2);
        o.Add(new() { K = "dpi", N = "DISPLAY SCALING", H = "keep full render quality above 100% scaling", Grp = 1, V = { "AUTO", "PRESERVE" }, F = new() { M(), M("DFFlagDisableDPIScale", "True") } });
        o.Add(new() { K = "shadow", N = "SHADOWS", H = "real-time and baked shadow rendering", Grp = 1, V = { "AUTO", "SOFTER", "OFF" }, F = new() { M(), M("FIntRenderShadowIntensity", "50"), M("FIntRenderShadowIntensity", "0", "DFFlagDebugPauseVoxelizer", "True") } });
        o.Add(new() { K = "shmap", N = "SHADOW DETAIL", H = "shadow map resolution - sharper edges cost gpu", Grp = 1, V = { "AUTO", "1024", "2048", "4096" }, F = new() { M(), M("FIntRenderShadowmapResolution", "1024"), M("FIntRenderShadowmapResolution", "2048"), M("FIntRenderShadowmapResolution", "4096") } });
        o.Add(new() { K = "postfx", N = "POST-PROCESSING", H = "bloom, blur, depth of field and colour correction", Grp = 1, V = { "AUTO", "ON", "OFF" }, F = new() { M(), M("FFlagDisablePostFx", "False"), M("FFlagDisablePostFx", "True") } });
        o.Add(new() { K = "font", N = "ROBLOX FONT", H = "swap the client's typeface for a ttf of your own", Grp = 1, Ui = "file", V = { "DEFAULT", "CUSTOM" }, F = new() { M(), M() } });
        o.Add(new() { K = "logo", N = "ROBLOX LOGO", H = "client logo art and the desktop icon", Grp = 1, Ui = "file", V = { "DEFAULT", "CUSTOM" }, F = new() { M(), M() } });
        o.Add(new() { K = "logos", N = "ROBLOX STUDIO LOGO", H = "studio's logo art and its desktop icon", Grp = 1, Ui = "file", V = { "DEFAULT", "CUSTOM" }, F = new() { M(), M() } });
        o.Add(new() { K = "logorst", N = "RESTORE LOGOS", H = "put both apps' original logos and icons back", Grp = 1, Ui = "action", V = { "RESTORE" }, F = new() { M() } });
        var gq = new RSetOpt { K = "gfx", N = "GRAPHICS QUALITY", H = "the in-client quality slider, 1 to 10", Grp = 2, Ui = "slider", Xml = "SavedQualityLevel", Xt = "token", V = { "KEEP", "AUTO" }, X = new() { "", "0" } };
        for (int i = 1; i <= 10; i++) { gq.V.Add(i.ToString()); gq.X.Add(i.ToString()); }
        o.Add(gq);
        o.Add(new() { K = "maxq", N = "MAX QUALITY", H = "the client's max-quality switch - graphics pinned at the top of the range", Grp = 2, Xml = "MaxQualityEnabled", Xt = "bool", Pref = true, V = { "KEEP", "ON", "OFF" }, X = new() { "", "true", "false" } });
        o.Add(new() { K = "gmode", N = "GRAPHICS MODE", H = "what the client optimizes for - frames, detail, or between", Grp = 2, Xml = "GraphicsOptimizationMode", Xt = "token", Pref = true, V = { "KEEP", "PERFORMANCE", "BALANCED", "QUALITY" }, X = new() { "", "0", "1", "2" } });
        var mv = new RSetOpt { K = "vol", N = "MASTER VOLUME", H = "the client's own volume slider", Grp = 2, Ui = "slider", Xml = "MasterVolume", Xt = "float", V = { "KEEP" }, X = new() { "" } };
        for (int i = 0; i <= 20; i++) { int pc = i * 5; mv.V.Add(pc + "%"); mv.X.Add((pc / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)); }
        o.Add(mv);
        var ms = new RSetOpt { K = "sens", N = "MOUSE SENSITIVITY", H = "camera speed - 1.0 is the roblox default", Grp = 2, Ui = "slider", Xml = "MouseSensitivity", Xt = "float", V = { "KEEP" }, X = new() { "" } };
        for (int i = 1; i <= 30; i++) { string sv = (i / 10.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture); ms.V.Add(sv); ms.X.Add(sv); }
        o.Add(ms);
        o.Add(new() { K = "shiftlock", N = "SHIFT LOCK SWITCH", H = "let shift lock be toggled in game - the menu's own switch", Grp = 2, Xml = "ControlMode", Xt = "token", Pref = true, V = { "KEEP", "ON", "OFF" }, X = new() { "", "1", "0" } });
        o.Add(new() { K = "cammode", N = "CAMERA MODE", H = "how the camera follows you - classic, follow, orbital, toggle", Grp = 2, Xml = "ComputerCameraMovementMode", Xt = "token", Pref = true, V = { "KEEP", "DEFAULT", "CLASSIC", "FOLLOW", "ORBITAL", "CAMERA TOGGLE" }, X = new() { "", "0", "1", "2", "3", "4" } });
        o.Add(new() { K = "movemode", N = "MOVEMENT MODE", H = "keyboard and mouse, or click to move", Grp = 2, Xml = "ComputerMovementMode", Xt = "token", Pref = true, V = { "KEEP", "DEFAULT", "KEYBOARD & MOUSE", "CLICK TO MOVE" }, X = new() { "", "0", "1", "2" } });
        o.Add(new() { K = "fullscr", N = "FULL SCREEN", H = "open the client full screen, or in a window", Grp = 2, Xml = "Fullscreen", Xt = "bool", Pref = true, V = { "KEEP", "ON", "OFF" }, X = new() { "", "true", "false" } });
        o.Add(new() { K = "startmax", N = "START MAXIMIZED", H = "a windowed client opens maximised", Grp = 2, Xml = "StartMaximized", Xt = "bool", Pref = true, V = { "KEEP", "ON", "OFF" }, X = new() { "", "true", "false" } });
        o.Add(new() { K = "redmot", N = "REDUCED MOTION", H = "the accessibility switch that calms the interface animations", Grp = 2, Xml = "ReducedMotion", Xt = "bool", Pref = true, V = { "KEEP", "ON", "OFF" }, X = new() { "", "true", "false" } });
        var ut = new RSetOpt { K = "uitrans", N = "UI TRANSPARENCY", H = "menu background transparency - 100% is roblox's own, 0% is solid", Grp = 2, Ui = "slider", Xml = "PreferredTransparency", Xt = "float", Pref = true, V = { "KEEP" }, X = new() { "" } };
        for (int i = 0; i <= 10; i++) { int pc = i * 10; ut.V.Add(pc + "%"); ut.X.Add((pc / 100.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)); }
        o.Add(ut);
        o.Add(new() { K = "fxedit", N = "EDIT FAST FLAGS", H = "add or override the flags this module writes", Grp = 3, Ui = "action", V = { "EDIT" } });
        o.Add(new() { K = "src", N = "FLAG SOURCE", H = "which half of this module reaches the client file", Grp = 3, Ui = "source", V = { "MIX", "CLIENT SYSTEMS", "EDIT FAST FLAGS" } });
        foreach (var op in o) { int v = RowAl(op); op.Al = v == 1 ? 1 : 0; op.Alp = v == 2 ? 1 : 0; }
        return o;
    }

    public static int FlagStatus(string name) => Allow.Contains(name) ? 1 : 2;
    static int RowAl(RSetOpt o)
    {
        if (o.F is null) return 1;
        int yes = 0, no = 0;
        foreach (var m in o.F) foreach (var k in m.Keys) if (Allow.Contains(k)) yes++; else no++;
        if (no == 0) return 1;
        return yes > 0 ? 2 : 0;
    }
    public static RSetOpt? By(string k) => Opts.FirstOrDefault(o => o.K == k);
    public static int Idx(RSetOpt o) => Pick.TryGetValue(o.K, out var v) ? Math.Clamp(v, 1, o.V.Count) : 1;
    public static double SlideFrac(int i) { var o = Opts[i - 1]; int n = o.V.Count; return n > 1 ? (Idx(o) - 1) / (double)(n - 1) : 0.0; }
    public static string GrpName(int g) => g == 0 ? "ACTIONS" : g == 1 ? "CLIENT FILES" : g == 2 ? "CLIENT SETTINGS FILE" : "FAST FLAGS";
    public static double RowY(int i, double rsrg)
    {
        double y = 12 + (i - 1) * rsrg;
        for (int j = 2; j <= i; j++) if (Opts[j - 1].Grp != Opts[j - 2].Grp) y += SEPH;
        return y;
    }
    public static double Total(double rsrg) => RowY(SysN, rsrg) + rsrg - 12;

    public static void Say(string msg, uint col = 0) { Msg = msg; MsgCol = col != 0 ? col : C_ON; MsgAt = Clock.Tick; HubSurface.Live?.Tim(Pace.TICK_A); }
    public static void FxBump() => FxVer++;
    public static void ExtraSave()
    {
        FxBump();
        Ini.Write(Paths.IniFile, "rset", "extra", string.Join("|", Extra.Select(kv => kv.Key + "=" + kv.Value)));
    }
    static void ExtraLoad()
    {
        string raw = Ini.Read(Paths.IniFile, "rset", "extra", "");
        if (raw == "") return;
        foreach (var part in raw.Split('|'))
        {
            int p = part.IndexOf('=');
            if (p < 0) continue;
            string k = part[..p].Trim(), v = part[(p + 1)..].Trim();
            if (FlagNameOK(k) && v != "") Extra[k] = v;
        }
        FxBump();
    }
    public static bool FlagNameOK(string n) { n = n.Trim(); return n.Length >= 4 && n.Length <= 96 && Regex.IsMatch(n, "^[A-Za-z][A-Za-z0-9_]*$"); }

    // ---- presets ----
    public static int PresetIdx(RSetOpt o, string name) { int i = o.V.IndexOf(name); return i < 0 ? 1 : i + 1; }
    public static Dictionary<string, string> PresetTable(int which) => which == 1
        ? new() { ["render"] = "AUTO", ["light"] = "FUTURE", ["frm"] = "AUTO", ["tex"] = "3", ["mesh"] = "ULTRA", ["msaa"] = "x4", ["fps"] = "240", ["dpi"] = "PRESERVE", ["gfx"] = "10", ["maxq"] = "ON", ["gmode"] = "QUALITY", ["shadow"] = "AUTO", ["shmap"] = "4096", ["postfx"] = "ON" }
        : which == 2
        ? new() { ["render"] = "AUTO", ["light"] = "SHADOWMAP", ["frm"] = "AUTO", ["tex"] = "2", ["mesh"] = "MEDIUM", ["msaa"] = "x2", ["fps"] = "120", ["dpi"] = "AUTO", ["gfx"] = "5", ["maxq"] = "OFF", ["gmode"] = "BALANCED", ["shadow"] = "SOFTER", ["shmap"] = "2048", ["postfx"] = "AUTO" }
        : new() { ["render"] = "AUTO", ["light"] = "VOXEL", ["frm"] = "AUTO", ["tex"] = "0", ["mesh"] = "OFF", ["msaa"] = "x1", ["fps"] = "MAX", ["dpi"] = "AUTO", ["gfx"] = "1", ["maxq"] = "OFF", ["gmode"] = "PERFORMANCE", ["shadow"] = "OFF", ["shmap"] = "1024", ["postfx"] = "OFF" };
    public static void ApplyPreset(int which)
    {
        string nm = which == 1 ? "HIGH" : which == 2 ? "MEDIUM" : "LOW";
        int n = 0, skipped = 0;
        foreach (var (k, want) in PresetTable(which))
        {
            var o = By(k);
            if (o is null) continue;
            if (RowLocked(o)) { skipped++; continue; }
            int idx = PresetIdx(o, want);
            if (idx == Idx(o)) continue;
            Pick[k] = idx; FlashAt[k] = Clock.Tick;
            Ini.Write(Paths.IniFile, "rset", k, (long)idx);
            n++;
        }
        Preset = which; PresetAt = Clock.Tick;
        string tail = skipped > 0 ? "  \u00B7  " + skipped + " TAKEN OVER, SKIPPED" : "";
        if (On) Say(nm + " PRESET STAGED - PRESS APPLY AGAIN TO WRITE IT" + tail, AMBER);
        else Say(nm + " PRESET STAGED  \u00B7  " + n + " ROW(S) CHANGED" + tail + "  -  PRESS APPLY", C_ON);
    }
    public static int PresetActive()
    {
        for (int w = 1; w <= 3; w++)
        {
            bool hit = true;
            foreach (var (k, want) in PresetTable(w))
            {
                var o = By(k);
                if (o is null || RowLocked(o)) continue;
                if (Idx(o) != PresetIdx(o, want)) { hit = false; break; }
            }
            if (hit) return w;
        }
        return 0;
    }
    public static int Chosen()
    {
        int n = 0;
        foreach (var o in Opts) { if (o.Ui is "action" or "preset" or "source") continue; if (Idx(o) > 1 && !RowMoot(o)) n++; }
        return n + Extra.Count;
    }
    // ---- FLAG SOURCE ----
    public static void SrcToggle()
    {
        SrcOn = !SrcOn; SrcAt = Clock.Tick; FlashAt["src"] = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", "srcon", SrcOn ? 1L : 0L);
        FxBump();
        Say(SrcOn ? (SrcSel == 1 ? "CLIENT SYSTEMS" : "EDIT FAST FLAGS") + " ONLY - THE OTHER HALF IS IGNORED" : "MIXING BOTH - OVERRIDES WIN ON A SHARED NAME", On ? AMBER : 0);
    }
    public static void SrcSelect(int which)
    {
        if (which < 1 || which > 2 || (SrcSel == which && SrcOn)) return;
        SrcSel = which; SrcOn = true; SrcAt = Clock.Tick; FlashAt["src"] = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", "srcpick", (long)SrcSel); Ini.Write(Paths.IniFile, "rset", "srcon", 1L);
        FxBump();
        Say((which == 1 ? "CLIENT SYSTEMS" : "EDIT FAST FLAGS") + " ONLY - THE OTHER HALF IS IGNORED", On ? AMBER : 0);
    }
    public static bool SrcRows => !(SrcOn && SrcSel == 2);
    public static bool SrcExtra => !(SrcOn && SrcSel == 1);

    // ---- locks, overrides ----
    static readonly Dictionary<string, RSetOpt> _rowOf = new();
    public static RSetOpt? FlagRowOf(string name)
    {
        if (_rowOf.Count == 0) foreach (var o in Opts) if (o.F is not null) foreach (var m in o.F) foreach (var k in m.Keys) _rowOf.TryAdd(k, o);
        return _rowOf.TryGetValue(name, out var r) ? r : null;
    }
    static int _lockSig = -1; static HashSet<string> _lockMemo = new();
    public static HashSet<string> LockedRows()
    {
        if (!SrcExtra) return new();
        if (_lockSig == FxVer) return _lockMemo;
        var m = new HashSet<string>();
        foreach (var k in Extra.Keys) { var o = FlagRowOf(k); if (o is not null) m.Add(o.K); }
        _lockSig = FxVer; _lockMemo = m;
        return m;
    }
    public static bool RowLocked(RSetOpt o) => LockedRows().Contains(o.K);
    public static string RowOverride(RSetOpt o)
    {
        string k = o.K;
        if (k is "gfx" or "maxq") { var f = By("frm"); if (f is not null && Idx(f) > 1) return "FRM QUALITY  \u00B7  its override pins the level  \u00B7  set it to AUTO to use this"; }
        else if (k == "shmap")
        {
            var sh = By("shadow"); var lt = By("light");
            if (sh is not null && Idx(sh) == PresetIdx(sh, "OFF")) return "SHADOWS OFF  \u00B7  nothing to map";
            if (lt is not null && Idx(lt) == PresetIdx(lt, "VOXEL")) return "LIGHTING TECHNOLOGY VOXEL  \u00B7  it draws no shadow maps";
        }
        else if (k == "startmax") { var fs = By("fullscr"); if (fs is not null && Idx(fs) == PresetIdx(fs, "ON")) return "FULL SCREEN ON  \u00B7  no window to maximise"; }
        return "";
    }
    public static bool RowMoot(RSetOpt o) => RowOverride(o) != "";
    public static void LinkPick(string k)
    {
        if (k != "maxq" && k != "gfx") return;
        var gq = By("gfx"); var mq = By("maxq");
        if (gq is null || mq is null) return;
        int top = PresetIdx(gq, "10"), onI = PresetIdx(mq, "ON"), offI = PresetIdx(mq, "OFF");
        if (Idx(mq) != onI || Idx(gq) == top) return;
        if (k == "maxq") { Pick["gfx"] = top; FlashAt["gfx"] = Clock.Tick; Ini.Write(Paths.IniFile, "rset", "gfx", (long)top); Say("GRAPHICS QUALITY FOLLOWED TO 10 - MAX QUALITY IS THE SLIDER AT ITS TOP", AMBER); }
        else { Pick["maxq"] = offI; FlashAt["maxq"] = Clock.Tick; Ini.Write(Paths.IniFile, "rset", "maxq", (long)offI); Say("MAX QUALITY OFF - THE SLIDER LEFT ITS TOP", AMBER); }
    }
    public static void SetPick(RSetOpt o, int idx)
    {
        if (idx < 1 || idx > o.V.Count || idx == Idx(o)) return;
        Pick[o.K] = idx; FlashAt[o.K] = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", o.K, (long)idx);
        if (On) Say("CHANGED - PRESS APPLY AGAIN TO WRITE IT", AMBER);
        LinkPick(o.K);
    }
    /// <summary>RSetTakeRow(o): the row's current pair moves into the raw layer as overrides; the row goes back to AUTO.</summary>
    public static int TakeRow(RSetOpt o)
    {
        if (o.F is null) return 0;
        int i = Idx(o);
        if (i <= 1 || i > o.F.Count) return 0;
        int n = 0;
        foreach (var (k, v) in o.F[i - 1]) if (!Extra.ContainsKey(k)) { Extra[k] = v; FxFlash[k] = Clock.Tick; n++; }
        Pick[o.K] = 1; FlashAt[o.K] = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", o.K, 1L);
        ExtraSave();
        return n;
    }
    public static string FlagOwner(string name)
    {
        foreach (var o in Opts) { if (o.F is null) continue; int i = Idx(o); if (i <= 1 || i > o.F.Count) continue; if (o.F[i - 1].ContainsKey(name)) return o.N; }
        return "";
    }

    // ---- the flag list ----
    public static Dictionary<string, string> PresetFlags()
    {
        var res = new Dictionary<string, string>();
        foreach (var o in Opts)
        {
            if (o.F is null || RowMoot(o)) continue;
            int i = Idx(o);
            if (i <= 1 || i > o.F.Count) continue;
            foreach (var (k, v) in o.F[i - 1]) res[k] = v;
        }
        return res;
    }
    public static Dictionary<string, string> Flags()
    {
        if (SrcOn && SrcSel == 2) return new Dictionary<string, string>(Extra);
        var res = PresetFlags();
        if (SrcOn && SrcSel == 1) return res;
        foreach (var (k, v) in Extra) res[k] = v;
        return res;
    }

    // ---- the xml ----
    public static string Xml(bool force = false)
    {
        if (!force && _xmlAt != 0 && Clock.Tick - _xmlAt < 4000) return XmlPath;
        _xmlAt = Clock.Tick; XmlPath = "";
        if (XmlOverride is not null) { var l = XmlOverride(); XmlPath = l.Count > 0 ? l[0] : ""; ReadXml(); return XmlPath; }
        string dir = Os.IsWin ? Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", "Roblox")
                   : Os.IsMac ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Preferences") : "";
        if (dir == "" || !Directory.Exists(dir)) return "";
        string best = ""; int bestN = -1;
        try
        {
            foreach (var f in Directory.GetFiles(dir, "GlobalBasicSettings_*.xml"))
            {
                var m = Regex.Match(Path.GetFileName(f), "_(\\d+)\\.xml$");
                int n = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                if (n >= bestN) { bestN = n; best = f; }
            }
        }
        catch { }
        XmlPath = best;
        ReadXml();
        return best;
    }
    public static void ReadXml()
    {
        XmlHave.Clear();
        if (XmlPath == "" || !File.Exists(XmlPath)) return;
        string body; try { body = File.ReadAllText(XmlPath, Encoding.UTF8); } catch { return; }
        if (body == "") return;
        foreach (var o in Opts)
        {
            if (o.Xml is null) continue;
            var m = Regex.Match(body, "<\\w+\\s+name=\"" + Regex.Escape(o.Xml) + "\"\\s*>(.*?)</\\w+>", RegexOptions.Singleline);
            if (m.Success) XmlHave[o.Xml] = m.Groups[1].Value.Trim();
        }
    }
    public static string XmlLabel(RSetOpt o, string raw)
    {
        if (o.X is null) return raw;
        for (int j = 0; j < o.X.Count; j++)
        {
            string xv = o.X[j];
            if (xv == "") continue;
            if (xv == raw || (double.TryParse(xv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a) && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b) && a == b)) return o.V[j];
        }
        return raw;
    }
    public static void EnsureDirs() { try { Directory.CreateDirectory(Dir); Directory.CreateDirectory(Path.Combine(Dir, "fonts")); } catch { } }
    static bool XmlSet(ref string body, string prop, string val, string tag)
    {
        var m = Regex.Match(body, "(<\\w+\\s+name=\"" + Regex.Escape(prop) + "\"\\s*>)(.*?)(</\\w+>)", RegexOptions.Singleline);
        if (m.Success)
        {
            if (m.Groups[2].Value.Contains('<')) return false;
            body = body[..m.Index] + m.Groups[1].Value + val + m.Groups[3].Value + body[(m.Index + m.Length)..];
            return true;
        }
        var mi = Regex.Match(body, "<Item\\s+class=\"UserGameSettings\"[^>]*>\\s*<Properties>", RegexOptions.Singleline);
        if (!mi.Success) mi = Regex.Match(body, "<Properties>", RegexOptions.Singleline);
        if (!mi.Success) return false;
        string ins = "\n\t\t<" + tag + " name=\"" + prop + "\">" + val + "</" + tag + ">";
        body = body[..(mi.Index + mi.Length)] + ins + body[(mi.Index + mi.Length)..];
        return true;
    }
    public static string XmlRead(string path, string prop)
    {
        string body; try { body = File.ReadAllText(path, Encoding.UTF8); } catch { return ""; }
        var m = Regex.Match(body, "<\\w+\\s+name=\"" + Regex.Escape(prop) + "\"\\s*>(.*?)</\\w+>", RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }
    static bool XmlSetExisting(ref string body, string prop, string val)
    {
        var m = Regex.Match(body, "(<\\w+\\s+name=\"" + Regex.Escape(prop) + "\"\\s*>)(.*?)(</\\w+>)", RegexOptions.Singleline);
        if (!m.Success || m.Groups[2].Value.Contains('<')) return false;
        body = body[..m.Index] + m.Groups[1].Value + val + m.Groups[3].Value + body[(m.Index + m.Length)..];
        return true;
    }
    static bool XmlDel(ref string body, string prop)
    {
        var m = Regex.Match(body, "(\\r?\\n)?[ \\t]*(<\\w+\\s+name=\"" + Regex.Escape(prop) + "\"\\s*>)(.*?)(</\\w+>)", RegexOptions.Singleline);
        if (!m.Success || m.Groups[3].Value.Contains('<')) return false;
        body = body[..m.Index] + body[(m.Index + m.Length)..];
        return true;
    }
    static string XmlTwin(RSetOpt o) => o.Xml == "SavedQualityLevel" ? "GraphicsQualityLevel" : "";
    static Dictionary<string, string> XmlOwned() { if (!File.Exists(XmlOwnPath)) return new(); try { return ParseJson(File.ReadAllText(XmlOwnPath, Encoding.UTF8)); } catch { return new(); } }
    static void XmlOwnSave(Dictionary<string, string> m)
    {
        EnsureDirs();
        if (m.Count == 0) { try { File.Delete(XmlOwnPath); } catch { } return; }
        try { File.WriteAllText(XmlOwnPath, DumpJson(m), new UTF8Encoding(false)); } catch { }
    }
    public static bool XmlOwes() => XmlOwned().Count > 0 || File.Exists(XmlBak);
    static HashSet<string> XmlRevertSet()
    {
        var own = XmlOwned();
        if (own.Count > 0) return new HashSet<string>(own.Keys);
        var res = new HashSet<string>();
        foreach (var o in Opts) { if (o.Xml is null || o.Pref) continue; res.Add(o.Xml); string tw = XmlTwin(o); if (tw != "") res.Add(tw); }
        return res;
    }

    // ---- ClientAppSettings.json ----
    public static List<string> AppSettingsPaths(bool force = false)
    {
        if (!force && _pathsAt != 0 && Clock.Tick - _pathsAt < 4000) return _paths;
        _pathsAt = Clock.Tick;
        if (PathsOverride is not null) { _paths = PathsOverride(); return _paths; }
        var res = new List<string>();
        try
        {
            if (Os.IsWin)
            {
                string lad = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";
                if (lad == "") { _paths = res; return res; }
                string vers = Path.Combine(lad, "Roblox", "Versions");
                if (Directory.Exists(vers)) foreach (var v in Directory.GetDirectories(vers, "version-*")) if (File.Exists(Path.Combine(v, "RobloxPlayerBeta.exe"))) res.Add(Path.Combine(v, "ClientSettings"));
                foreach (var b in new[] { "Fishstrap", "Bloxstrap", "Voidstrap" })
                {
                    string root = Path.Combine(lad, b);
                    if (!Directory.Exists(root)) continue;
                    res.Add(Path.Combine(root, "Modifications", "ClientSettings"));
                    string bv = Path.Combine(root, "Versions");
                    if (Directory.Exists(bv)) foreach (var v in Directory.GetDirectories(bv, "version-*")) if (File.Exists(Path.Combine(v, "RobloxPlayerBeta.exe"))) res.Add(Path.Combine(v, "ClientSettings"));
                }
                if (!NoProc)
                    foreach (var p in Roblox.Processes())
                    {
                        string exe = ""; try { exe = p.MainModule?.FileName ?? ""; } catch { }
                        if (exe == "") continue;
                        string d = Path.Combine(Path.GetDirectoryName(exe) ?? "", "ClientSettings");
                        if (!res.Contains(d, StringComparer.OrdinalIgnoreCase)) res.Add(d);
                    }
            }
            else if (Os.IsMac)
            {
                foreach (var app in new[] { "/Applications/Roblox.app", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications", "Roblox.app") })
                    if (Directory.Exists(app)) res.Add(Path.Combine(app, "Contents", "MacOS", "ClientSettings"));
            }
        }
        catch { }
        _paths = res;
        return res;
    }
    public static Dictionary<string, string> ParseJson(string body)
    {
        var m = new Dictionary<string, string>();
        foreach (Match mt in Regex.Matches(body, "\"([A-Za-z0-9_]+)\"\\s*:\\s*\"([^\"]*)\"")) m[mt.Groups[1].Value] = mt.Groups[2].Value;
        foreach (Match mt in Regex.Matches(body, "\"([A-Za-z0-9_]+)\"\\s*:\\s*(true|false|-?\\d+(?:\\.\\d+)?)")) m.TryAdd(mt.Groups[1].Value, mt.Groups[2].Value);
        return m;
    }
    public static string DumpJson(Dictionary<string, string> m)
    {
        var sb = new StringBuilder("{"); int n = 0;
        foreach (var (k, v) in m) { sb.Append(n++ > 0 ? "," : "").Append("\n    \"").Append(FastFlags.Ffm.Esc(k)).Append("\": \"").Append(FastFlags.Ffm.Esc(v)).Append('"'); }
        return sb.Append("\n}").ToString();
    }
    static Dictionary<string, string> KnownKeys()
    {
        var res = new Dictionary<string, string>();
        foreach (var o in Opts) if (o.F is not null) foreach (var fm in o.F) foreach (var k in fm.Keys) res[k] = "1";
        foreach (var k in Extra.Keys) res[k] = "1";
        if (File.Exists(PrevPath)) { try { foreach (var k in ParseJson(File.ReadAllText(PrevPath, Encoding.UTF8)).Keys) res[k] = "1"; } catch { } }
        return res;
    }
    /// <summary>RSetWriteAppSettings(write): merge the module's flags into every install's file (recording what each key held), or take every known key back out.</summary>
    public static int WriteAppSettings(bool write)
    {
        var dirs = AppSettingsPaths(true);
        if (dirs.Count == 0) return 0;
        EnsureDirs();
        var prev = new Dictionary<string, string>();
        if (File.Exists(PrevPath)) { try { prev = ParseJson(File.ReadAllText(PrevPath, Encoding.UTF8)); } catch { } }
        var mine = Flags();
        if (write) foreach (var k in mine.Keys) prev.TryAdd(k, "\f");
        var known = write ? new Dictionary<string, string>() : KnownKeys();
        var cut = new HashSet<string>();
        int ok = 0;
        foreach (var d in dirs)
        {
            string p = Path.Combine(d, "ClientAppSettings.json");
            if (!write && !File.Exists(p)) continue;
            try { Directory.CreateDirectory(d); } catch { }
            var curM = new Dictionary<string, string>();
            if (File.Exists(p)) { try { curM = ParseJson(File.ReadAllText(p, Encoding.UTF8)); } catch { } }
            if (write)
            {
                foreach (var k in prev.Keys.ToList()) if (prev[k] == "\f" && curM.TryGetValue(k, out var cv)) prev[k] = cv;
                foreach (var (k, v) in mine) curM[k] = v;
            }
            else foreach (var k in known.Keys) if (curM.Remove(k)) cut.Add(k);
            try { File.WriteAllText(p, DumpJson(curM), new UTF8Encoding(false)); ok++; } catch { }
        }
        if (write) { try { File.WriteAllText(PrevPath, DumpJson(prev), new UTF8Encoding(false)); } catch { } }
        else { try { File.Delete(PrevPath); } catch { } WrCut = cut.Count; }
        return ok;
    }

    // ---- APPLY / REVERT ----
    public static Func<int>? FontApplyHook, LogoApplyHook, IconApplyHook, FontRevertHook, LogoRevertHook, IconRevertHook, FpsLiveHook, FpsLiveRevertHook;
    public static Action? LogoClearHook;
    static bool RbxRunning() => !NoProc && Roblox.IsRunning();
    public static void Apply()
    {
        int nf = Flags().Count;
        int ncs = WriteAppSettings(true);
        int nx = 0, xerr = 0, xmiss = 0;
        bool xbusy = RbxRunning();
        string path = xbusy ? "" : Xml(true);
        if (path != "")
        {
            EnsureDirs();
            try { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); } catch { }
            if (!File.Exists(XmlBak)) { try { File.Copy(path, XmlBak, false); } catch { } }
            string body = ""; try { body = File.ReadAllText(path, Encoding.UTF8); } catch { }
            if (body == "") xerr++;
            else
            {
                var want = new Dictionary<string, string>();
                foreach (var o in Opts)
                {
                    if (o.Xml is null || o.X is null || RowMoot(o)) continue;
                    int i = Idx(o);
                    if (i <= 1 || i > o.X.Count || o.X[i - 1] == "") continue;
                    if (XmlSet(ref body, o.Xml, o.X[i - 1], o.Xt)) want[o.Xml] = o.X[i - 1]; else xerr++;
                    string tw = XmlTwin(o);
                    if (tw != "")
                    {
                        if (o.X[i - 1] == "0") { if (XmlDel(ref body, tw)) want[tw] = ""; }
                        else if (XmlSetExisting(ref body, tw, o.X[i - 1])) want[tw] = o.X[i - 1];
                    }
                }
                if (want.Count > 0)
                {
                    bool ok = false;
                    try { File.WriteAllText(path, body, new UTF8Encoding(false)); ok = true; } catch { }
                    if (ok)
                    {
                        var own = XmlOwned();
                        foreach (var (k, v) in want) { if (XmlRead(path, k) == v) { nx++; own[k] = v == "" ? "\f" : v; } else xmiss++; }
                        XmlOwnSave(own);
                        if (nx > 0) { try { File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly); } catch { } }
                    }
                    else xerr += want.Count;
                }
            }
            ReadXml();
        }
        var fo = By("font"); int nfo = fo is not null && Idx(fo) > 1 ? FontApplyHook?.Invoke() ?? 0 : 0;
        var lg = By("logo"); var lgs = By("logos");
        int nlg = (lg is not null && Idx(lg) > 1) || (lgs is not null && Idx(lgs) > 1) ? LogoApplyHook?.Invoke() ?? 0 : 0;
        int lfp = FpsLiveHook?.Invoke() ?? 0;
        bool lgWanted = (lg is not null && Idx(lg) > 1 && Logo != "") || (lgs is not null && Idx(lgs) > 1 && LogoS != "");
        int nai = (lg is not null && Idx(lg) > 1) || (lgs is not null && Idx(lgs) > 1) ? IconApplyHook?.Invoke() ?? 0 : 0;
        On = true; ApplyAt = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", "on", 1L);
        string rbx = RbxRunning() ? "  -  RESTART ROBLOX" : "";
        string fs = (nfo > 0 ? ", " + nfo + " FONT FILE(S)" : "") + (nlg > 0 ? ", " + nlg + " LOGO FILE(S)" : "") + (nai > 0 ? ", " + nai + " SHORTCUT ICON(S)" : "")
            + (lfp == 1 ? ", FPS CAP LIVE NOW" : lfp is 2 or 3 ? ", FPS CAP SAVED - RESTART ROBLOX TO GO ABOVE 240" : lfp == -1 ? ", FPS CAP NEEDS A RESTART" : "")
            + (lgWanted && nlg == 0 && nai == 0 ? ", LOGO MATCHED NOTHING" : "");
        int xw = Opts.Count(o => o.Xml is not null && Idx(o) > 1 && !RowMoot(o));
        if (xbusy && xw > 0) Say(nf + " FLAG(S) WRITTEN  -  CLOSE ROBLOX, THEN APPLY AGAIN FOR THE REST", AMBER);
        else if (xmiss > 0 || xerr > 0) Say("WROTE " + nx + " OF " + xw + " VALUE(S) - THE REST WOULD NOT STICK", C_OFF);
        else if (ncs > 0 || nx > 0 || nfo > 0) Say("APPLIED  " + nf + " FLAG(S) TO " + ncs + " CLIENT(S), " + nx + " VALUE(S)" + fs + rbx, C_ON);
        else if (nf > 0 && ncs == 0) Say("NO ROBLOX INSTALL FOUND TO WRITE TO", C_OFF);
        else Say("NOTHING TO APPLY - EVERY ROW IS ON AUTO", AMBER);
    }
    static (int nx, bool pend) XmlRevertFile()
    {
        if (!XmlOwes()) return (0, false);
        if (RbxRunning()) return (0, true);
        string path = Xml(true);
        if (path == "") { XmlOwnSave(new()); try { File.Delete(XmlBak); } catch { } return (0, false); }
        try { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); } catch { }
        string body = ""; try { body = File.ReadAllText(path, Encoding.UTF8); } catch { }
        if (body == "") return (0, true);
        int cut = 0;
        var set = XmlRevertSet();
        foreach (var prop in set) if (XmlDel(ref body, prop)) cut++;
        bool ok = true;
        if (cut > 0) { ok = false; try { File.WriteAllText(path, body, new UTF8Encoding(false)); ok = true; } catch { } }
        if (!ok) { ReadXml(); return (0, true); }
        int left = set.Count(prop => XmlRead(path, prop) != "");
        if (left > 0) { ReadXml(); return (0, true); }
        XmlOwnSave(new()); try { File.Delete(XmlBak); } catch { }
        ReadXml();
        return (cut, false);
    }
    static void XmlPendArm()
    {
        if (On || !XmlOwes()) return;
        _pend ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _pend.Tick -= OnPend; _pend.Tick += OnPend;
        _pend.Start();
    }
    static void OnPend(object? s, EventArgs e)
    {
        if (On || !XmlOwes()) { _pend?.Stop(); return; }
        if (RbxRunning()) return;
        var r = XmlRevertFile();
        if (r.pend) return;
        _pend?.Stop();
        Say((r.nx > 0 ? r.nx + " CLIENT SETTING(S) BACK TO ROBLOX DEFAULT" : "CLIENT SETTINGS FILE CLEAN") + "  -  REVERT FINISHED", C_ON);
    }
    public static void Revert()
    {
        WrCut = 0;
        int ncs = WriteAppSettings(false);
        int nf = WrCut;
        var xr = XmlRevertFile();
        int nfo = FontRevertHook?.Invoke() ?? 0, nlg = LogoRevertHook?.Invoke() ?? 0, nai = IconRevertHook?.Invoke() ?? 0;
        int lfp = FpsLiveRevertHook?.Invoke() ?? 0;
        int nr = HubToDefault();
        On = false; ApplyAt = Clock.Tick;
        Ini.Write(Paths.IniFile, "rset", "on", 0L);
        if (xr.pend) XmlPendArm();
        string rbx = RbxRunning() ? "  -  RESTART ROBLOX" : "";
        string xs = xr.nx > 0 ? ", " + xr.nx + " CLIENT SETTING(S) BACK TO ROBLOX DEFAULT" : "";
        string fs = (nfo > 0 ? ", " + nfo + " FONT FILE(S)" : "") + (nlg > 0 ? ", " + nlg + " LOGO FILE(S)" : "") + (nai > 0 ? ", " + nai + " SHORTCUT ICON(S)" : "")
            + (lfp > 0 ? ", FPS CAP LIFTED LIVE" : lfp == -1 ? ", FPS CAP NEEDS A RESTART" : "") + (nr > 0 ? ", " + nr + " SETTING(S) BACK TO DEFAULT" : "");
        if (xr.pend) Say("REVERTED " + nf + " FLAG(S)" + fs + "  -  THE CLIENT SETTINGS FILE GOES BACK WHEN ROBLOX CLOSES", AMBER);
        else Say("REVERTED  " + nf + " FLAG(S) FROM " + ncs + " CLIENT(S)" + xs + fs + rbx, C_ON);
    }
    public static Action? FxCloseHook;
    static int HubToDefault()
    {
        FxCloseHook?.Invoke();
        int n = RowsToDefault();
        if (Extra.Count > 0) { n += Extra.Count; Extra.Clear(); ExtraSave(); }
        if (SrcOn || SrcSel != 1) { n++; SrcOn = false; SrcSel = 1; SrcAt = Clock.Tick; FlashAt["src"] = Clock.Tick; Ini.Write(Paths.IniFile, "rset", "srcon", 0L); Ini.Write(Paths.IniFile, "rset", "srcpick", 1L); FxBump(); }
        if (Font != "") { n++; Font = ""; Ini.Write(Paths.IniFile, "rset", "fontfile", ""); }
        if (Logo != "" || LogoS != "" || (LogoClearHook is not null && (RSetLogo.LogoSub != "" || RSetLogo.LogoSubS != ""))) { n++; Logo = ""; LogoS = ""; Ini.Write(Paths.IniFile, "rset", "logofile", ""); Ini.Write(Paths.IniFile, "rset", "logofileS", ""); LogoClearHook?.Invoke(); }
        return n;
    }
    public static void Toggle() { if (On) Revert(); else Apply(); }
    public static int RowsToDefault()
    {
        Dd = 0;
        int n = 0;
        foreach (var o in Opts) { if (Idx(o) != 1) { FlashAt[o.K] = Clock.Tick; n++; } Pick[o.K] = 1; Ini.Write(Paths.IniFile, "rset", o.K, 1L); }
        Preset = 0;
        return n;
    }
    public static void Reset() { RowsToDefault(); Say("ALL ROWS BACK TO AUTO", 0); }
    public static void OpenXml()
    {
        string p = Xml(true);
        if (p == "") { Say("NO CLIENT SETTINGS FILE FOUND", C_OFF); return; }
        try
        {
            if (Os.IsWin) Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + p + "\"") { UseShellExecute = true });
            else if (Os.IsMac) Process.Start("open", new[] { "-R", p });
        }
        catch { }
        Say("OPENED THE CLIENT SETTINGS FILE", 0);
    }
    public static void Boot()
    {
        EnsureDirs();
        if (Ini.ReadInt(Paths.IniFile, "rset", "meshladder", 0) == 0) { Ini.Delete(Paths.IniFile, "rset", "mesh"); Ini.Write(Paths.IniFile, "rset", "meshladder", 1L); }
        foreach (var o in Opts) Pick[o.K] = Math.Clamp((int)Ini.ReadInt(Paths.IniFile, "rset", o.K, 1), 1, o.V.Count);
        ExtraLoad();
        Font = Ini.Read(Paths.IniFile, "rset", "fontfile", ""); if (Font != "" && !File.Exists(Font)) Font = "";
        Logo = Ini.Read(Paths.IniFile, "rset", "logofile", ""); if (Logo != "" && !File.Exists(Logo)) Logo = "";
        LogoS = Ini.Read(Paths.IniFile, "rset", "logofileS", ""); if (LogoS != "" && !File.Exists(LogoS)) LogoS = "";
        if (Font == "") Pick["font"] = 1;
        SrcOn = Ini.ReadInt(Paths.IniFile, "rset", "srcon", 0) != 0;
        SrcSel = Math.Clamp((int)Ini.ReadInt(Paths.IniFile, "rset", "srcpick", 1), 1, 2);
        SrcT = SrcOn ? 1 : 0; SrcSelT = SrcSel;
        On = Ini.ReadInt(Paths.IniFile, "rset", "on", 0) != 0;
        OnT = On ? 1 : 0;
        Xml(true);
        Msg = On ? "APPLIED" : "NOT APPLIED"; MsgCol = On ? C_ON : 0xFFC7CBE0;
        XmlPendArm();
    }
    // ---- dropdown ----
    public static void DdOpen(int i) { var o = Opts[i - 1]; Dd = i; DdAt = Clock.Tick; DdPrev = i; int n = Math.Min(o.V.Count, 7); DdTop = Math.Max(0, Math.Min(Idx(o) - n, o.V.Count - n)); }
    public static void DdClose() { if (Dd == 0) return; Dd = 0; DdAt = Clock.Tick; }
    public static void DdPick(int it)
    {
        if (Dd == 0) return;
        var o = Opts[Dd - 1];
        int idx = DdTop + it;
        if (idx >= 1 && idx <= o.V.Count && idx != Idx(o)) SetPick(o, idx);
        DdClose();
    }
}
